# Abonos a compromisos de proveedores — Plan de implementación

> **For Claude:** REQUIRED SUB-SKILL: usar la skill executing-plans para implementar este plan tarea por tarea.

**Goal:** Permitir abonos parciales (o totales) a los compromisos de proveedores (órdenes de pago directo), con partida contable y movimiento bancario por abono, saldo derivado, anulación del último abono y comprobante PDF.

**Architecture:** Mecanismo **aditivo** sobre `OrdenesPagoDirectoService`: una tabla `prv_compromiso_abono` (libro de abonos) tenant-scoped; el saldo NO se persiste, se deriva de `monto - SUM(abonos vigentes)`; cuando el saldo llega a 0 el servicio marca `status_transacc=true` (Pagado) y al anular el último abono lo reabre. Reutiliza los helpers ya probados de `OrdenesPagoDirectoService` para partida contable y transacciones bancarias (`NormalizeContraProcessingLinesAsync`, `BuildProviderProcessingPartidaLineasAsync`, `RegisterPartidaContableAsync`, `RegisterLinkedBankTransactionsAsync`, `UpdateCompromisoStatusTransaccAsync`) y el patrón de reverso bancario de `IBanTransaccionesService`.

**Tech Stack:** .NET 9, Blazor WASM, DevExpress 25.1.7, PostgreSQL, EF Core (solo lectura del esquema), scripts SQL en `Database/`, xUnit + Npgsql.

---

## Decisiones asumidas (confirmar con el usuario)

- **Sin monto mínimo** de abono: solo se exige `monto > 0` y `monto <= saldo pendiente` (tolerancia 0.01).
- **Saldo derivado**: no hay columna de saldo; `saldo = prv_compromiso_hdr.monto - SUM(monto de abonos con estado 'V')`. Al llegar a 0 se marca `status_transacc=true`.
- **Compatibilidad legacy**: un compromiso ya procesado (`status_transacc=true`) y **sin** filas de abono se interpreta como saldo 0 / Pagado y no admite nuevos abonos.
- **Estado del abono `'V'`/`'A'`** (vigente/anulado). Convención NO invertida, distinta a propósito de `transaccion_abonado`.
- **Anular solo el último abono vigente** (mayor `numero_abono` con estado 'V'), para no dejar huecos en la secuencia.
- **El abono es aditivo**: NO elimina ni reemplaza `MarkAsProcessedAsync`; el pago único legacy sigue existiendo.
- **Identificación del abono por `numeroAbono`** (int, secuencia por compromiso), no por `abono_id`, en toda la cadena (rutas, servicio, cliente, UI, tests).
- **Reverso bancario del abono falla si el movimiento está conciliado** (`IBanTransaccionesService.AnularMovimientoAsync` lanza `ArgumentException`); se ejecuta antes de tocar contabilidad para fallar temprano.
- **`CuentaDestinoProveedorId` es informativo/opcional**: la página no lo captura hoy (no hay endpoint de cuentas bancarias de proveedor en `apc.Client`); se envía `null`. El método bancario resuelve la cuenta origen vía `NormalizeContraProcessingLinesAsync`. Queda como mejora futura habilitar el combo de cuenta destino.

## Antes de empezar: base de datos

- **Task 1** (script SQL) se aplica vía el skill **@guardia-estructura-bd**: tabla nueva = cambio **aditivo** (tarjeta verde), pero igual pasa por la guardia por tocar estructura de BD. El flujo es **mirror** (`siad_v3_restore` @localhost) → **SRV** (`siad_v3` @172.16.0.9), y **lo ejecuta el usuario** — este plan NO aplica el SQL.
- El resto de las tareas (2..12) **compilan sin** tener el SQL aplicado (EF solo lee el esquema en runtime). La única tarea que **requiere la tabla creada en la BD de test** es **Task 13** (tests de integración): apuntar `SIAD_TEST_DB` al **mirror**, nunca a prod.

---

### Task 1: Script SQL — tabla `prv_compromiso_abono`

**Files:**
- Create: `Database/2026-07-17_prv_compromiso_abono.sql`

La aplicación pasa por **@guardia-estructura-bd** (tarjeta aditiva/verde) y el flujo mirror → SRV (lo hace el usuario). Este plan solo redacta el script.

**Paso 1.1** — Crear `Database/2026-07-17_prv_compromiso_abono.sql` con el contenido COMPLETO. Incluye **todas** las columnas que el resto del plan lee/escribe (contra-cuenta, kardex, reverso y auditoría):

```sql
-- =============================================================================
-- Proveedores: abonos parciales a compromisos (ordenes de pago directo)
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUE ESTA TABLA
-- Un compromiso (prv_compromiso_hdr) se pagaba de UNA sola vez (MarkAsProcessedAsync
-- ponia status_transacc=true). Esta tabla es el LIBRO DE ABONOS: cada fila es un pago
-- parcial (o total) con su metodo, su contra-cuenta, su partida contable y, si es
-- bancario, su movimiento de banco vinculado.
--
-- EL SALDO NO SE GUARDA - SE DERIVA
--   saldo = prv_compromiso_hdr.monto - SUM(monto de abonos con estado 'V')
-- Cuando el saldo llega a 0 la capa de servicio marca status_transacc=true (Pagado);
-- al anular el ultimo abono, si estaba Pagado, status_transacc vuelve a false.
--
-- COMPATIBILIDAD: un compromiso legacy ya procesado (status_transacc=true) SIN filas
-- de abono se interpreta como saldo 0 / pagado y no admite nuevos abonos.
--
-- ESTADO 'V'/'A' (convencion NO invertida, distinta a proposito de transaccion_abonado)
--   'V' = VIGENTE  -> suma al total abonado, resta del saldo
--   'A' = ANULADO  -> no cuenta para el saldo
--
-- ANULACION: solo el ULTIMO abono vigente (mayor numero_abono con 'V'). Al anularse se
-- registra su partida inversa (partida_reverso_id) y, si tenia movimiento bancario, su
-- reverso (ban_kardex_id_reverso).
--
-- Cambio aditivo: tabla nueva. No altera ninguna tabla ni dato existente.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS prv_compromiso_abono (
    abono_id             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT       NOT NULL,
    numero_orden         INT          NOT NULL,
    numero_abono         INT          NOT NULL,
    fecha                TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    monto                NUMERIC(15,2) NOT NULL,
    metodo_pago          VARCHAR(20)  NOT NULL,
    cuenta_contra_id     BIGINT,
    banco_cuenta_id      BIGINT,
    ban_kardex_id        BIGINT,
    partida_id           BIGINT,
    partida_reverso_id   BIGINT,
    ban_kardex_id_reverso BIGINT,
    estado               CHAR(1)      NOT NULL DEFAULT 'V',
    motivo_anulacion     VARCHAR(250),
    usuario_creo         VARCHAR(100) NOT NULL,
    fecha_creacion       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_anulacion    VARCHAR(100),
    fecha_anulacion      TIMESTAMP WITHOUT TIME ZONE,
    usuario_modifica     VARCHAR(100),
    fecha_modificacion   TIMESTAMP WITHOUT TIME ZONE,
    rowid                UUID         NOT NULL DEFAULT gen_random_uuid(),

    CONSTRAINT ck_prv_compromiso_abono_monto  CHECK (monto > 0),
    CONSTRAINT ck_prv_compromiso_abono_estado CHECK (estado IN ('V', 'A')),

    -- El hijo vive SIEMPRE en la misma empresa y contra un compromiso existente
    -- (contraparte en BD del query filter tenant; defensa en profundidad).
    -- RESTRICT: no se borra un compromiso que tenga abonos registrados.
    CONSTRAINT fk_prv_compromiso_abono_hdr
        FOREIGN KEY (company_id, numero_orden)
        REFERENCES prv_compromiso_hdr (company_id, numero_orden)
        ON DELETE RESTRICT,

    -- Garantiza la secuencia 1,2,3... por compromiso (y sirve al lookup por prefijo).
    CONSTRAINT uq_prv_compromiso_abono_numero
        UNIQUE (company_id, numero_orden, numero_abono)
);

-- Suma de vigentes / ultimo vigente por compromiso.
CREATE INDEX IF NOT EXISTS ix_prv_compromiso_abono_estado
    ON prv_compromiso_abono (company_id, numero_orden, estado);

COMMENT ON TABLE  prv_compromiso_abono IS
    'Libro de abonos (pagos parciales) de prv_compromiso_hdr. Saldo NO almacenado: saldo = hdr.monto - SUM(monto WHERE estado=''V''). Al llegar a 0 el servicio marca status_transacc=true.';
COMMENT ON COLUMN prv_compromiso_abono.numero_abono IS
    'Secuencia 1,2,3... por compromiso (company_id, numero_orden). Unica por uq_prv_compromiso_abono_numero.';
COMMENT ON COLUMN prv_compromiso_abono.cuenta_contra_id IS
    'AccountId de la contra-cuenta (origen del pago) resuelta por NormalizeContraProcessingLinesAsync.';
COMMENT ON COLUMN prv_compromiso_abono.partida_id IS
    'poliza_id de la partida contable del abono (stage ABO). NumeroPartida se deriva por JOIN a con_partida_hdr.';
COMMENT ON COLUMN prv_compromiso_abono.partida_reverso_id IS
    'poliza_id de la partida inversa generada al anular (stage RAB). NULL mientras este vigente.';
COMMENT ON COLUMN prv_compromiso_abono.ban_kardex_id IS
    'ban_kardex del movimiento bancario del abono (metodos bancarios). NULL si es contable.';
COMMENT ON COLUMN prv_compromiso_abono.ban_kardex_id_reverso IS
    'ban_kardex del reverso bancario generado al anular el abono. NULL mientras este vigente.';
COMMENT ON COLUMN prv_compromiso_abono.estado IS
    'V = vigente (suma al abonado) | A = anulado (no cuenta). Convencion NO invertida.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columnas:
-- SELECT column_name, data_type, character_maximum_length, is_nullable, column_default
--   FROM information_schema.columns WHERE table_name='prv_compromiso_abono' ORDER BY ordinal_position;
-- 2) Constraints:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='prv_compromiso_abono'::regclass ORDER BY contype, conname;
--   -> fk_prv_compromiso_abono_hdr(f), prv_compromiso_abono_pkey(p), uq_prv_compromiso_abono_numero(u),
--      ck_prv_compromiso_abono_monto(c), ck_prv_compromiso_abono_estado(c)
-- 3) Indices:
-- SELECT indexname FROM pg_indexes WHERE tablename='prv_compromiso_abono' ORDER BY indexname;
--   -> ix_prv_compromiso_abono_estado, prv_compromiso_abono_pkey, uq_prv_compromiso_abono_numero
-- 4) El CHECK de monto debe FALLAR:
-- INSERT INTO prv_compromiso_abono (company_id, numero_orden, numero_abono, fecha, monto, metodo_pago, usuario_creo)
-- VALUES (2, 1, 1, now(), 0, 'CONTABLE', 'test');   -- ERROR ck_prv_compromiso_abono_monto
-- =============================================================================
```

**Paso 1.2** — Confirmar idempotencia y cabecera sin ejecutar el SQL.

Run: `Get-Content Database/2026-07-17_prv_compromiso_abono.sql | Select-String -Pattern "IF NOT EXISTS","Regla DB Mirror","VERIFICACION"`
Expected: al menos 3 líneas coincidentes.

---

### Task 2: Entidad `prv_compromiso_abono`

**Files:**
- Create: `SIAD.Core/Entities/prv_compromiso_abono.cs`

Sigue el estilo de `prv_compromiso_hdr.cs` (implementa `ICompanyScopedEntity`, `partial class`, snake_case) y de `prv_proveedor_cuenta_bancaria.cs` (auditoría, `Guid? rowid`). Refleja EXACTAMENTE las columnas de la Task 1.

**Paso 2.1** — Crear `SIAD.Core/Entities/prv_compromiso_abono.cs`:

```csharp
using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_compromiso_abono : ICompanyScopedEntity
{
    public long abono_id { get; set; }

    public long company_id { get; set; }

    public int numero_orden { get; set; }

    public int numero_abono { get; set; }

    public DateTime fecha { get; set; }

    public decimal monto { get; set; }

    public string metodo_pago { get; set; } = null!;

    public long? cuenta_contra_id { get; set; }

    public long? banco_cuenta_id { get; set; }

    public long? ban_kardex_id { get; set; }

    public long? partida_id { get; set; }

    public long? partida_reverso_id { get; set; }

    public long? ban_kardex_id_reverso { get; set; }

    public string estado { get; set; } = "V";

    public string? motivo_anulacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public string? usuario_anulacion { get; set; }

    public DateTime? fecha_anulacion { get; set; }

    public string? usuario_modifica { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public Guid? rowid { get; set; }
}
```

**Paso 2.2** — Verificar por compilación (satisface `ICompanyScopedEntity` con `long company_id`).

Run: `dotnet build SIAD.Core/SIAD.Core.csproj -clp:ErrorsOnly`
Expected: `Build succeeded.` con `0 Error(s)`.

---

### Task 3: DbSet y configuración en `SiadDbContext`

**Files:**
- Modify: `SIAD.Data/SiadDbContext.cs:210` (DbSet junto a los demás `prv_*`)
- Modify: `SIAD.Data/SiadDbContext.cs:1763` (bloque de config tras el de `prv_compromiso_hdr`)

El filtro tenant y el stamping de `company_id` los aplica `SiadDbContext.Tenancy.cs` por reflexión sobre `ICompanyScopedEntity` (líneas 42-62); por eso este bloque **NO** declara `HasQueryFilter`. Identidad con `UseIdentityAlwaysColumn()` como el resto de `prv_*`.

**Paso 3.1** — Localizar la línea 210 y añadir el DbSet inmediatamente después:

```csharp
    public virtual DbSet<prv_compromiso_hdr> prv_compromiso_hdrs { get; set; }

    public virtual DbSet<prv_compromiso_abono> prv_compromiso_abonos { get; set; }
```

**Paso 3.2** — Localizar el cierre del bloque `prv_compromiso_hdr` (líneas 1762-1763):

```csharp
            entity.Property(e => e.nombre_proveedor).HasMaxLength(150);
        });
```

y sustituirlo por (insertando el nuevo bloque justo después):

```csharp
            entity.Property(e => e.nombre_proveedor).HasMaxLength(150);
        });

        modelBuilder.Entity<prv_compromiso_abono>(entity =>
        {
            entity.HasKey(e => e.abono_id).HasName("prv_compromiso_abono_pkey");

            entity.ToTable("prv_compromiso_abono");

            // El filtro tenant y el stamping de company_id los aplica SiadDbContext.Tenancy.cs
            // para toda ICompanyScopedEntity (no se declara HasQueryFilter aqui).
            entity.HasIndex(e => new { e.company_id, e.numero_orden, e.estado })
                .HasDatabaseName("ix_prv_compromiso_abono_estado");
            entity.HasIndex(e => new { e.company_id, e.numero_orden, e.numero_abono })
                .IsUnique()
                .HasDatabaseName("uq_prv_compromiso_abono_numero");

            entity.Property(e => e.abono_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.monto).HasPrecision(15, 2);
            entity.Property(e => e.metodo_pago).HasMaxLength(20);
            entity.Property(e => e.estado).HasMaxLength(1).HasDefaultValue("V");
            entity.Property(e => e.motivo_anulacion).HasMaxLength(250);
            entity.Property(e => e.usuario_creo).HasMaxLength(100);
            entity.Property(e => e.fecha_creacion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuario_anulacion).HasMaxLength(100);
            entity.Property(e => e.fecha_anulacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuario_modifica).HasMaxLength(100);
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });
```

**Paso 3.3** — Compilar SIAD.Data.

Run: `dotnet build SIAD.Data/SIAD.Data.csproj -clp:ErrorsOnly`
Expected: `Build succeeded.` con `0 Error(s)`.

**Paso 3.4** — Compilar la solución completa.

Run: `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly`
Expected: `Build succeeded.` con `0 Error(s)`.

---

### Task 4: DTOs canónicos en `SIAD.Core.DTOs.Presupuesto`

**Files:**
- Create: `SIAD.Core/DTOs/Presupuesto/AbonosCompromisoDtos.cs`
- (No se duplican `OrdenPagoDirectoMetodoPago`, `PartidaLineaOrdenPagoDto` ni `AnularOrdenPagoDirectoDto`: se reutilizan de `OrdenesPagoDirectoDtos.cs`.)

DTOs puros (sin lógica): verificación por compilación. **Estos son los DTOs canónicos; el resto de la cadena los referencia textualmente.** La anulación reutiliza el `AnularOrdenPagoDirectoDto` existente (tiene `Motivo`) — NO se crea `AnularAbonoDto`.

**Paso 4.1** — Crear `SIAD.Core/DTOs/Presupuesto/AbonosCompromisoDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

/// <summary>
/// Entrada para registrar un abono (pago parcial o total) contra un compromiso /
/// orden de pago directo. El monto se valida en el servicio contra el saldo pendiente
/// (0 &lt; monto &lt;= saldo). El company_id NO se toma de aqui: lo resuelve
/// ICurrentCompanyService y lo estampa SiadDbContext.
/// </summary>
public sealed class AbonoCompromisoUpsertDto
{
    [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "El monto del abono debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    /// <summary>Uno de <see cref="OrdenPagoDirectoMetodoPago"/> (CONTABLE/DEPOSITO/CHEQUE/TRANSFERENCIA).</summary>
    [Required(ErrorMessage = "El metodo de pago es requerido.")]
    [StringLength(20, ErrorMessage = "El metodo de pago no puede superar 20 caracteres.")]
    public string MetodoPago { get; set; } = string.Empty;

    /// <summary>Cuenta contable de contrapartida (origen del pago). AccountId de con_plan_cuentas.</summary>
    public long? CuentaContraId { get; set; }

    /// <summary>Cuenta bancaria (ban_cuenta) origen cuando el metodo es bancario; el servicio la resuelve si va null.</summary>
    public long? BancoCuentaId { get; set; }

    /// <summary>Cuenta destino del proveedor (prv_proveedor_cuenta_bancaria). Informativo para el pago bancario.</summary>
    public long? CuentaDestinoProveedorId { get; set; }

    /// <summary>Distribucion de contracuentas del abono; si va vacia el servicio arma una linea con CuentaContraId.</summary>
    public List<PartidaLineaOrdenPagoDto> Lineas { get; set; } = new();

    public DateTime? Fecha { get; set; }

    [StringLength(60, ErrorMessage = "El usuario no puede superar 60 caracteres.")]
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>Fila de un abono ya registrado, para listarlo en la vista de saldo.</summary>
public sealed class AbonoCompromisoListItemDto
{
    public long AbonoId { get; set; }

    public int NumeroAbono { get; set; }

    public DateTime Fecha { get; set; }

    public decimal Monto { get; set; }

    public string MetodoPago { get; set; } = string.Empty;

    /// <summary>'V' vigente, 'A' anulado.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Numero de partida (con_partida_hdr.poliza_number) derivado por JOIN por partida_id.</summary>
    public string? NumeroPartida { get; set; }

    public long? PartidaId { get; set; }

    /// <summary>True solo para el ultimo abono vigente (mayor numero_abono con 'V'): unico anulable. Lo calcula el servicio.</summary>
    public bool PuedeAnular { get; set; }
}

/// <summary>
/// Saldo de un compromiso con el desglose de sus abonos. Saldo y Abonado se calculan al
/// vuelo (no se persisten). Pagado = Saldo &lt;= 0 y no anulado.
/// </summary>
public sealed class CompromisoSaldoDto
{
    public int NumeroOrden { get; set; }

    public decimal Monto { get; set; }

    public decimal Abonado { get; set; }

    public decimal Saldo { get; set; }

    public bool Pagado { get; set; }

    public bool Anulado { get; set; }

    /// <summary>Texto para UI: "Anulado" / "Pagado" / "Abonado parcial" / "Pendiente".</summary>
    public string EstadoTexto { get; set; } = string.Empty;

    public IReadOnlyList<AbonoCompromisoListItemDto> Abonos { get; set; } = Array.Empty<AbonoCompromisoListItemDto>();
}

/// <summary>
/// Resultado de RegistrarAbonoAsync y AnularAbonoAsync (servicio, controller, cliente, tests).
/// NO se reutiliza OrdenPagoDirectoOperacionResultadoDto para abonos.
/// </summary>
public sealed class AbonoCompromisoResultadoDto
{
    public bool Success { get; set; }

    public int NumeroOrden { get; set; }

    public int NumeroAbono { get; set; }

    public string? NumeroPartida { get; set; }

    public decimal Saldo { get; set; }

    public bool Pagado { get; set; }

    public string Message { get; set; } = string.Empty;
}
```

**Paso 4.2** — Compilar SIAD.Core.

Run: `dotnet build SIAD.Core/SIAD.Core.csproj -clp:ErrorsOnly`
Expected: `0 Error(s)`. (El `cref` a `OrdenPagoDirectoMetodoPago` resuelve por estar en el mismo ensamblado/namespace.)

---

### Task 5: `GetSaldoConAbonosAsync` (servicio + interfaz)

**Files:**
- Modify: `SIAD.Services/Presupuesto/IOrdenesPagoDirectoService.cs` (firmas tras `MarkAsProcessedAsync`, antes del `}` de cierre en la línea 54)
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` (método público junto a `GetByNumeroOrdenAsync`, L148, + constantes de estado)
- Test: cubierto por integración en Task 13 (requiere Postgres real; sin unit test aquí)

`NumeroPartida` NO es columna: se deriva con LEFT JOIN a `con_partida_hdr` por `poliza_id = partida_id` (columna `poliza_number`, verificada en `OrdenesPagoDirectoService.cs:1644-1651`). Como el filtro global aplica `company_id == CurrentCompanyId` a las lecturas EF, la consulta EF no filtra `company_id` a mano; el JOIN raw sí lo filtra explícitamente.

**Paso 5.1** — Declarar las firmas en la interfaz. Reemplazar el cierre de `MarkAsProcessedAsync` (L50-54):

```csharp
    Task<OrdenPagoDirectoOperacionResultadoDto> MarkAsProcessedAsync(
        int numeroOrden,
        ProcesarOrdenPagoDirectoDto dto,
        CancellationToken ct = default);
}
```

por:

```csharp
    Task<OrdenPagoDirectoOperacionResultadoDto> MarkAsProcessedAsync(
        int numeroOrden,
        ProcesarOrdenPagoDirectoDto dto,
        CancellationToken ct = default);

    Task<CompromisoSaldoDto?> GetSaldoConAbonosAsync(
        int numeroOrden,
        CancellationToken ct = default);
}
```

(El archivo ya tiene `using SIAD.Core.DTOs.Presupuesto;`, así que `CompromisoSaldoDto` queda en scope.)

**Paso 5.2** — Compilar y ver que FALLA (impl inexistente).

Run: `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly`
Expected: FALLA con `CS0535` — `'OrdenesPagoDirectoService' no implementa 'IOrdenesPagoDirectoService.GetSaldoConAbonosAsync(...)'`.

**Paso 5.3** — Agregar las constantes de estado dentro de la clase (junto al campo `_context`, L21):

```csharp
    private const string EstadoAbonoVigente = "V";
    private const string EstadoAbonoAnulado = "A";
```

**Paso 5.4** — Implementar `GetSaldoConAbonosAsync` + helpers, inmediatamente después de `GetByNumeroOrdenAsync`:

```csharp
    public async Task<CompromisoSaldoDto?> GetSaldoConAbonosAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        // Filtro global multi-tenant (company_id == CurrentCompanyId) lo aplica el contexto.
        var compromiso = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(c => c.numero_orden == numeroOrden)
            .Select(c => new { c.numero_orden, c.monto, c.anulado, c.status_transacc })
            .FirstOrDefaultAsync(ct);

        if (compromiso is null)
        {
            return null;
        }

        var abonos = await LoadAbonosAsync(numeroOrden, ct);

        var abonado = abonos
            .Where(a => string.Equals(a.Estado, EstadoAbonoVigente, StringComparison.Ordinal))
            .Sum(a => a.Monto);

        var saldo = compromiso.monto - abonado;

        // Compat legacy: procesado (status_transacc==true) SIN filas de abono => saldo 0 / pagado.
        var procesadoLegacySinAbonos = compromiso.status_transacc == true && abonos.Count == 0;
        if (procesadoLegacySinAbonos)
        {
            abonado = compromiso.monto;
            saldo = 0m;
        }

        if (saldo < 0m)
        {
            saldo = 0m;
        }

        var pagado = !compromiso.anulado && saldo <= 0m;

        return new CompromisoSaldoDto
        {
            NumeroOrden = compromiso.numero_orden,
            Monto = compromiso.monto,
            Abonado = abonado,
            Saldo = saldo,
            Pagado = pagado,
            Anulado = compromiso.anulado,
            EstadoTexto = ResolverEstadoTexto(compromiso.anulado, pagado, abonado),
            Abonos = abonos
        };
    }

    /// <summary>
    /// Carga los abonos del compromiso (numero_partida derivado por JOIN a con_partida_hdr).
    /// Marca PuedeAnular=true solo en el ultimo vigente para no dejar huecos.
    /// </summary>
    private async Task<IReadOnlyList<AbonoCompromisoListItemDto>> LoadAbonosAsync(
        int numeroOrden,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        var abonos = new List<AbonoCompromisoListItemDto>();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
            command.CommandText = @"
SELECT a.abono_id, a.numero_abono, a.fecha, a.monto, a.metodo_pago, a.estado,
       a.partida_id, ph.poliza_number
FROM public.prv_compromiso_abono a
LEFT JOIN public.con_partida_hdr ph
       ON ph.company_id = a.company_id AND ph.poliza_id = a.partida_id
WHERE a.company_id = @company_id AND a.numero_orden = @numero_orden
ORDER BY a.numero_abono;";
            command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("numero_orden", NpgsqlDbType.Integer, numeroOrden);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                abonos.Add(new AbonoCompromisoListItemDto
                {
                    AbonoId = reader.GetInt64(0),
                    NumeroAbono = reader.GetInt32(1),
                    Fecha = reader.GetDateTime(2),
                    Monto = reader.GetDecimal(3),
                    MetodoPago = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Estado = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    PartidaId = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6),
                    NumeroPartida = reader.IsDBNull(7) ? null : reader.GetString(7),
                    PuedeAnular = false
                });
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var ultimoVigente = abonos
            .Where(a => string.Equals(a.Estado, EstadoAbonoVigente, StringComparison.Ordinal))
            .OrderByDescending(a => a.NumeroAbono)
            .FirstOrDefault();
        if (ultimoVigente is not null)
        {
            ultimoVigente.PuedeAnular = true;
        }

        return abonos;
    }

    private static string ResolverEstadoTexto(bool anulado, bool pagado, decimal abonado)
    {
        if (anulado)
        {
            return "Anulado";
        }

        if (pagado)
        {
            return "Pagado";
        }

        return abonado > 0m ? "Abonado parcial" : "Pendiente";
    }
```

> Verificado: `_context` (L21), `EnsureCompanyId()` (L2130), el patrón `_context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction` (L316/862/1886), `using Microsoft.EntityFrameworkCore;` (L4), `using SIAD.Core.DTOs.Presupuesto;` (L10) y `using NpgsqlTypes;` (L7) ya presentes. `con_partida_hdr.poliza_number`/`poliza_id`/`company_id` confirmadas (L1644-1651). En el harness de test (transacción ambiente) el `command.Transaction` toma esa transacción; en producción va `null` (autocommit de lectura), correcto.

**Paso 5.5** — Compilar y ver que PASA.

Run: `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly`
Expected: `0 Errores`. (`EstadoAbonoAnulado` puede quedar sin usar hasta Task 7; el repo NO trata warnings como error.)

**Paso 5.6** — Compilar la solución.

Run: `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly`
Expected: `0 Error(s)`.

---

### Task 6: `RegistrarAbonoAsync` (servicio + interfaz)

**Files:**
- Create: `SIAD.Services/Presupuesto/AbonoCompromisoCalculator.cs`
- Create: `SIAD.Tests/Presupuesto/AbonoCompromisoCalculatorTests.cs`
- Modify: `SIAD.Services/Presupuesto/IOrdenesPagoDirectoService.cs` (firma antes del cierre)
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` (método `RegistrarAbonoAsync` + helpers, tras `MarkAsProcessedAsync`, ~L626)

La lógica pura (saldo + siguiente número de abono) se aísla en `AbonoCompromisoCalculator` para TDD real sin base de datos. El orquestador `RegistrarAbonoAsync` reutiliza los helpers ya probados y se verifica por compilación + los tests de integración (Task 13).

**Concurrencia (canónico):** dentro de la transacción se hace `SELECT ... FOR UPDATE` sobre la fila del compromiso y DESPUÉS se recalcula saldo y `numero_abono`; además se captura `PostgresException` 23505 (unique_violation) devolviendo un resultado de negocio ("Reintente el abono") en vez de 500.

**Reutilización de la transacción (testabilidad):** `RegistrarAbonoAsync` **reutiliza `_context.Database.CurrentTransaction` si existe** (harness `BEGIN...ROLLBACK` de los tests) y solo abre/comitea su propia transacción cuando no hay una ambiente (producción). Todo el trabajo dentro de la transacción usa comandos **raw Npgsql** (como `MarkAsProcessedAsync`), nunca EF, para no romper el enlistado.

**Paso 6.1 — Crear el calculador puro.** Create `SIAD.Services/Presupuesto/AbonoCompromisoCalculator.cs`:

```csharp
namespace SIAD.Services.Presupuesto;

/// <summary>Estado minimo de una fila de abono para el calculo de saldo.</summary>
public readonly record struct AbonoLineaState(int NumeroAbono, decimal Monto, string? Estado);

/// <summary>Resultado del calculo de saldo y numeracion de abonos de un compromiso.</summary>
public readonly record struct AbonoComputeResult(decimal SaldoActual, int SiguienteNumeroAbono);

/// <summary>
/// Logica pura (sin base de datos) del saldo pendiente y el siguiente numero_abono.
/// El saldo NO se persiste: se deriva de (monto - SUM(abonos vigentes)).
/// Convencion de estado: 'V' vigente, 'A' anulado.
/// </summary>
public static class AbonoCompromisoCalculator
{
    public const string EstadoVigente = "V";
    public const string EstadoAnulado = "A";

    public static AbonoComputeResult Compute(
        decimal montoCompromiso,
        bool procesadoLegacy,
        IReadOnlyCollection<AbonoLineaState> abonos)
    {
        ArgumentNullException.ThrowIfNull(abonos);

        var totalVigente = abonos
            .Where(a => string.Equals(a.Estado, EstadoVigente, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Monto);

        // Compat: compromiso legacy procesado y SIN filas de abono => saldo 0.
        var saldo = (abonos.Count == 0 && procesadoLegacy)
            ? 0m
            : montoCompromiso - totalVigente;

        if (saldo < 0m)
        {
            saldo = 0m;
        }

        // numero_abono corre sobre TODAS las filas (incluidas anuladas) para no reusar numeros.
        var siguiente = (abonos.Count == 0 ? 0 : abonos.Max(a => a.NumeroAbono)) + 1;

        return new AbonoComputeResult(saldo, siguiente);
    }
}
```

**Paso 6.2 — Escribir el test que falla (TDD, lógica pura).** Create `SIAD.Tests/Presupuesto/AbonoCompromisoCalculatorTests.cs`:

```csharp
using SIAD.Services.Presupuesto;

namespace SIAD.Tests.Presupuesto;

public class AbonoCompromisoCalculatorTests
{
    private static AbonoLineaState L(int numeroAbono, decimal monto, string estado)
        => new(numeroAbono, monto, estado);

    [Fact]
    public void SinAbonos_NoProcesado_SaldoEsMontoCompleto_YProximoEsUno()
    {
        var s = AbonoCompromisoCalculator.Compute(1000m, procesadoLegacy: false, Array.Empty<AbonoLineaState>());
        Assert.Equal(1000m, s.SaldoActual);
        Assert.Equal(1, s.SiguienteNumeroAbono);
    }

    [Fact]
    public void SinAbonos_ProcesadoLegacy_SaldoCero_YaPagado()
    {
        var s = AbonoCompromisoCalculator.Compute(1000m, procesadoLegacy: true, Array.Empty<AbonoLineaState>());
        Assert.Equal(0m, s.SaldoActual);
        Assert.Equal(1, s.SiguienteNumeroAbono);
    }

    [Fact]
    public void SoloAbonosVigentesRestan_YAnuladosSeIgnoran()
    {
        var s = AbonoCompromisoCalculator.Compute(1000m, procesadoLegacy: false, new[]
        {
            L(1, 300m, "V"),
            L(2, 200m, "A"), // anulado: no resta
            L(3, 100m, "V"),
        });
        Assert.Equal(600m, s.SaldoActual);       // 1000 - 300 - 100
        Assert.Equal(4, s.SiguienteNumeroAbono); // max(1,2,3) + 1
    }

    [Fact]
    public void AbonosCubrenTotal_SaldoCero()
    {
        var s = AbonoCompromisoCalculator.Compute(500m, procesadoLegacy: true, new[] { L(1, 500m, "V") });
        Assert.Equal(0m, s.SaldoActual);
        Assert.Equal(2, s.SiguienteNumeroAbono);
    }
}
```

**Paso 6.3 — Correr y ver que falla por compilación.**

Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~AbonoCompromisoCalculatorTests"`
Expected: falla la compilación con `CS0246: 'AbonoCompromisoCalculator' / 'AbonoLineaState' could not be found` (aún no existe el calculador cuando se escribe el test primero).

**Paso 6.4 — Correr y ver VERDE.** (Tras el paso 6.1.)

Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~AbonoCompromisoCalculatorTests"`
Expected: `Passed! - Failed: 0, Passed: 4, Skipped: 0`.

**Paso 6.5 — Agregar la firma a la interfaz** `IOrdenesPagoDirectoService.cs` (antes del `}` de cierre; el archivo ya trae `using SIAD.Core.DTOs.Presupuesto;`):

```csharp
    Task<AbonoCompromisoResultadoDto> RegistrarAbonoAsync(
        int numeroOrden,
        AbonoCompromisoUpsertDto dto,
        CancellationToken ct = default);
```

Run: `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly`
Expected: **falla** con `CS0535` (`OrdenesPagoDirectoService` no implementa `RegistrarAbonoAsync`) — se resuelve en 6.6.

---
**Paso 6.6 — Implementar `RegistrarAbonoAsync` + helpers.** En `OrdenesPagoDirectoService.cs`, tras el cierre de `MarkAsProcessedAsync` (~L626). Reutiliza los helpers existentes (verificados): `NormalizeMetodoPago` (L1825), `NormalizeContraProcessingLinesAsync` (L1089), `BuildProviderProcessingPartidaLineasAsync` (L679), `RegisterPartidaContableAsync` (L1005), `RegisterLinkedBankTransactionsAsync` (L1178), `UpdateCompromisoStatusTransaccAsync` (L1062), `HasPartidaContableRegistradaAsync` (L2862), `BuildCreatePartidaFromStoredAsync` (L798), `BuildDocumentNumber`/`BuildPartidaNumber` (L3615/3618), `GetCurrentUser` (L3621), `EnsureCompanyId` (L2130).

```csharp

    public async Task<AbonoCompromisoResultadoDto> RegistrarAbonoAsync(
        int numeroOrden,
        AbonoCompromisoUpsertDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        // Pre-lectura (fuera de transaccion) para rechazos tempranos y datos del compromiso.
        var orden = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new { x.numero_orden, x.fecha, x.concepto, x.monto, x.cod_proveedor, x.status_transacc, x.anulado })
            .FirstOrDefaultAsync(ct);

        if (orden is null)
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden no existe." };
        if (orden.anulado)
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden esta anulada y no admite abonos." };
        if (dto.Monto <= 0m)
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "El monto del abono debe ser mayor a cero." };

        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? GetCurrentUser() : dto.Usuario.Trim();
        var metodoPago = NormalizeMetodoPago(dto.MetodoPago);
        // fecha local (sin TZ), coherente con TIMESTAMP WITHOUT TIME ZONE del resto del modulo prv_*.
        var fechaAbono = (dto.Fecha ?? DateTime.Now).Date;
        var descripcionBase = $"Abono - {(orden.concepto?.Trim() ?? $"Orden pago directo {numeroOrden}")}";
        if (descripcionBase.Length > 200) descripcionBase = descripcionBase[..200];

        // Contracuentas del abono (por el MONTO DEL ABONO). Preparacion read-only, fuera de la transaccion.
        IReadOnlyList<PartidaLineaOrdenPagoDto> lineasContraDto;
        if (dto.Lineas is { Count: > 0 })
        {
            lineasContraDto = dto.Lineas;
        }
        else if (dto.CuentaContraId is > 0)
        {
            lineasContraDto = new List<PartidaLineaOrdenPagoDto>
            {
                new() { CuentaId = dto.CuentaContraId.Value, Descripcion = descripcionBase, Debito = dto.Monto, Credito = 0m }
            };
        }
        else
        {
            throw new ArgumentException(
                "Debe seleccionar al menos una cuenta contra (origen) para registrar el abono.", nameof(dto));
        }

        // Normaliza contra el monto del ABONO (resuelve BancoCuentaId de cada contracuenta).
        var lineasContra = await NormalizeContraProcessingLinesAsync(lineasContraDto, dto.Monto, descripcionBase, ct);
        var lineasPartida = await BuildProviderProcessingPartidaLineasAsync(
            orden.cod_proveedor, lineasContra, dto.Monto, descripcionBase, ct);
        var cuentaContraId = lineasContra.Select(x => (long?)x.AccountId).FirstOrDefault();

        var faltaPartidaCreacion = !await HasPartidaContableRegistradaAsync(numeroOrden, ct);
        List<PartidaLineaOrden>? lineasCreacion = null;
        if (faltaPartidaCreacion)
        {
            (lineasCreacion, _, _) = await BuildCreatePartidaFromStoredAsync(numeroOrden, ct);
        }

        var companyId = EnsureCompanyId();
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();

        // Reusar la transaccion ambiente (tests) o abrir una propia (produccion).
        var ambient = _context.Database.CurrentTransaction;
        var ownsConnection = false;
        NpgsqlTransaction tx;
        var ownsTx = false;
        if (ambient is not null)
        {
            tx = (NpgsqlTransaction)ambient.GetDbTransaction();
        }
        else
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                ownsConnection = true;
            }
            tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            ownsTx = true;
        }

        try
        {
            // (a) Concurrencia: bloquear la fila del compromiso ANTES de recalcular.
            await LockCompromisoRowAsync(connection, tx, companyId, numeroOrden, ct);

            // (b) Recalcular saldo y numero_abono BAJO el lock (autoritativo) via el calculador.
            var abonoStates = await ReadAbonoStatesAsync(connection, tx, companyId, numeroOrden, ct);
            var estado = AbonoCompromisoCalculator.Compute(orden.monto, orden.status_transacc == true, abonoStates);

            if (estado.SaldoActual <= 0m)
            {
                if (ownsTx) await tx.RollbackAsync(ct);
                return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, Saldo = 0m, Pagado = true, Message = "El compromiso ya esta pagado; no admite mas abonos." };
            }
            if (dto.Monto - estado.SaldoActual > 0.01m)
            {
                if (ownsTx) await tx.RollbackAsync(ct);
                return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, Saldo = estado.SaldoActual, Message = $"El abono ({dto.Monto:N2}) no puede superar el saldo pendiente ({estado.SaldoActual:N2})." };
            }

            var numeroAbono = estado.SiguienteNumeroAbono;
            var descripcionAbono = $"Abono {numeroAbono} - {(orden.concepto?.Trim() ?? $"Orden pago directo {numeroOrden}")}";
            if (descripcionAbono.Length > 200) descripcionAbono = descripcionAbono[..200];
            var partidaNumber = BuildPartidaNumber(numeroOrden, $"ABO{numeroAbono}");

            long? partidaId;
            long? bancoCuentaId = null;
            long? banKardexId = null;

            // (c) Si el compromiso nunca genero la partida GEN de creacion, generarla ahora.
            if (faltaPartidaCreacion && lineasCreacion is not null)
            {
                await RegisterPartidaContableAsync(
                    connection, tx, BuildDocumentNumber(numeroOrden), BuildPartidaNumber(numeroOrden, "GEN"),
                    orden.fecha.Date, orden.concepto?.Trim() ?? $"Orden pago directo {numeroOrden}",
                    usuario, lineasCreacion, ct);
            }

            // (d) Partida contable del abono.
            partidaId = await RegisterPartidaContableAsync(
                connection, tx, BuildDocumentNumber(numeroOrden), partidaNumber,
                fechaAbono, descripcionAbono, usuario, lineasPartida, ct);

            // (e) Si el metodo es bancario, transaccion bancaria vinculada.
            if (OrdenPagoDirectoMetodoPago.EsBancario(metodoPago))
            {
                if (!partidaId.HasValue || partidaId.Value <= 0)
                    throw new InvalidOperationException("No se pudo resolver la partida contable del abono para vincular la transaccion bancaria.");

                var kardexIds = await RegisterLinkedBankTransactionsAsync(
                    connection, tx, numeroOrden, fechaAbono, descripcionAbono, usuario, metodoPago, partidaId.Value, lineasContra, ct);

                banKardexId = kardexIds.Count > 0 ? kardexIds[0] : null;
                bancoCuentaId = lineasContra.Where(x => x.BancoCuentaId is > 0).Select(x => x.BancoCuentaId).FirstOrDefault();
            }

            // (f) Persistir la fila del abono (estado 'V').
            await InsertAbonoRowAsync(
                connection, tx, companyId, numeroOrden, numeroAbono, fechaAbono, dto.Monto, metodoPago,
                cuentaContraId, bancoCuentaId, banKardexId, partidaId, usuario, ct);

            // (g) Si el abono liquida el saldo, marcar el compromiso como pagado.
            var saldoTrasAbono = estado.SaldoActual - dto.Monto;
            var quedaPagado = saldoTrasAbono <= 0.01m;
            if (quedaPagado)
            {
                await UpdateCompromisoStatusTransaccAsync(connection, tx, companyId, numeroOrden, statusTransacc: true, ct);
            }

            if (ownsTx) await tx.CommitAsync(ct);

            return new AbonoCompromisoResultadoDto
            {
                Success = true,
                NumeroOrden = numeroOrden,
                NumeroAbono = numeroAbono,
                NumeroPartida = partidaNumber,
                Saldo = quedaPagado ? 0m : saldoTrasAbono,
                Pagado = quedaPagado,
                Message = quedaPagado
                    ? $"Se registro el abono {numeroAbono} y el compromiso quedo pagado."
                    : $"Se registro el abono {numeroAbono}. Saldo pendiente: {saldoTrasAbono:N2}."
            };
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Carrera contra uq_prv_compromiso_abono_numero: negocio, no 500.
            if (ownsTx) await tx.RollbackAsync(ct);
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "Otro abono se registro al mismo tiempo. Reintente el abono." };
        }
        catch
        {
            if (ownsTx) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
            if (ownsConnection) await connection.CloseAsync();
        }
    }

    /// <summary>Bloquea la fila del compromiso (FOR UPDATE) para serializar abonos concurrentes.</summary>
    private static async Task LockCompromisoRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long companyId, int numeroOrden, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT 1 FROM public.prv_compromiso_hdr
 WHERE company_id = @company_id AND numero_orden = @numero_orden
 FOR UPDATE;";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("numero_orden", NpgsqlDbType.Integer, numeroOrden);
        await command.ExecuteScalarAsync(ct);
    }

    /// <summary>Lee (bajo el lock) las filas de abono para alimentar AbonoCompromisoCalculator.</summary>
    private static async Task<List<AbonoLineaState>> ReadAbonoStatesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long companyId, int numeroOrden, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT numero_abono, monto, estado
FROM public.prv_compromiso_abono
WHERE company_id = @company_id AND numero_orden = @numero_orden;";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("numero_orden", NpgsqlDbType.Integer, numeroOrden);

        var states = new List<AbonoLineaState>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            states.Add(new AbonoLineaState(
                reader.GetInt32(0),
                reader.GetDecimal(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return states;
    }

    /// <summary>Inserta la fila del abono con las columnas canonicas (fecha_creacion la pone el DEFAULT now()).</summary>
    private static async Task InsertAbonoRowAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long companyId, int numeroOrden, int numeroAbono,
        DateTime fecha, decimal monto, string metodoPago, long? cuentaContraId, long? bancoCuentaId, long? banKardexId,
        long? partidaId, string usuario, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = @"
INSERT INTO public.prv_compromiso_abono
    (company_id, numero_orden, numero_abono, fecha, monto, metodo_pago,
     cuenta_contra_id, banco_cuenta_id, ban_kardex_id, partida_id, estado, usuario_creo)
VALUES
    (@company_id, @numero_orden, @numero_abono, @fecha, @monto, @metodo_pago,
     @cuenta_contra_id, @banco_cuenta_id, @ban_kardex_id, @partida_id, @estado, @usuario_creo);";

        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("numero_orden", NpgsqlDbType.Integer, numeroOrden);
        command.Parameters.AddWithValue("numero_abono", NpgsqlDbType.Integer, numeroAbono);
        command.Parameters.AddWithValue("fecha", NpgsqlDbType.Timestamp, fecha);
        command.Parameters.AddWithValue("monto", NpgsqlDbType.Numeric, monto);
        // metodo_pago nunca DBNull: NormalizeMetodoPago siempre retorna valor.
        command.Parameters.AddWithValue("metodo_pago", NpgsqlDbType.Varchar, metodoPago);
        command.Parameters.Add(new NpgsqlParameter("cuenta_contra_id", NpgsqlDbType.Bigint) { Value = cuentaContraId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("banco_cuenta_id", NpgsqlDbType.Bigint) { Value = bancoCuentaId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("ban_kardex_id", NpgsqlDbType.Bigint) { Value = banKardexId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("partida_id", NpgsqlDbType.Bigint) { Value = partidaId ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("estado", NpgsqlDbType.Varchar, AbonoCompromisoCalculator.EstadoVigente);
        command.Parameters.AddWithValue("usuario_creo", NpgsqlDbType.Varchar, usuario);

        await command.ExecuteNonQueryAsync(ct);
    }
```

> Verificado: `PostgresErrorCodes.UniqueViolation` (= "23505") existe en Npgsql; el archivo ya usa `PostgresException`. `ConnectionState`/`IsolationLevel`/`CommandType` en scope (`using System.Data;`, presente por el uso en el resto del archivo). `fecha` se bindea como `NpgsqlDbType.Timestamp` (sin TZ). En el harness (tx ambiente) `ownsTx=false`: no comitea ni revierte, el harness hace ROLLBACK.

**Paso 6.7 — Compilar la solución.**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly`
Expected: `0 Error(s)` (desaparece el `CS0535` del paso 6.5).

**Paso 6.8 — Regresión del calculador.**

Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~AbonoCompromisoCalculatorTests"`
Expected: `Passed! - Failed: 0, Passed: 4`.

---

### Task 7: `AnularAbonoAsync` (servicio + interfaz)

**Files:**
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs:1,19-39` (ctor: inyectar `IBanTransaccionesService` como 6º parámetro) + agregar `AnularAbonoAsync` (tras `AnularAsync`, L1923) y el helper `LoadPartidaLineasReversaPorPolizaIdAsync` (tras `LoadPartidaLineasParaReversarAsync`, ~L1988).
- Modify: `SIAD.Services/Presupuesto/IOrdenesPagoDirectoService.cs` (firma nueva).

**Verificado:** ctor actual tiene 5 parámetros (L21-39); `IBanTransaccionesService` ya registrado scoped en `ServiceRegistration.cs:143`. `AnularMovimientoAsync` devuelve `Task<(long BanKardexIdAnulacion, decimal SaldoResultante)>`; lanza `ArgumentException` si el movimiento está conciliado. El servicio localiza el abono por **(company_id, numero_orden, numero_abono)** (canónico: identificación por `numeroAbono`, no `abonoId`).

**Notas de diseño (confirmables):**
1. El reverso de la partida ABO se hace por `poliza_id` exacto (`abono.partida_id`), no por número de orden — `LoadPartidaLineasParaReversarAsync` devolvería la GEN. Por eso el helper nuevo `LoadPartidaLineasReversaPorPolizaIdAsync`.
2. El reverso bancario (`AnularMovimientoAsync`) abre su propia transacción sobre la misma conexión: se ejecuta **antes** de la transacción EF (secuencial), y de paso falla temprano si el movimiento está conciliado. Ventana declarada: fallo del INSERT contable tras un reverso bancario ya comprometido.
3. Semántica opuesta a `AnularAsync` (a propósito): al anular el último abono de un compromiso Pagado se **reabre** (`status_transacc=false`; el saldo es derivado).
4. Reutiliza la transacción ambiente en tests (igual criterio que Task 6): si existe `_context.Database.CurrentTransaction` no abre una nueva ni comitea.

**Paso 7.1 — Inyectar `IBanTransaccionesService`.** Agregar el `using` (bajo `using SIAD.Services.Proveedores;`, L15):

```csharp
using SIAD.Services.Bancos;
```

Reemplazar el bloque de campos + constructor (L21-39) por:

```csharp
    private readonly SiadDbContext _context;
    private readonly IProveedoresService _proveedoresService;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAccountFormatService _accountFormatService;
    private readonly IBanTransaccionesService _banTransaccionesService;

    public OrdenesPagoDirectoService(
        SiadDbContext context,
        IProveedoresService proveedoresService,
        ICurrentCompanyService currentCompanyService,
        IHttpContextAccessor httpContextAccessor,
        IAccountFormatService accountFormatService,
        IBanTransaccionesService banTransaccionesService)
    {
        _context = context;
        _proveedoresService = proveedoresService;
        _currentCompanyService = currentCompanyService;
        _httpContextAccessor = httpContextAccessor;
        _accountFormatService = accountFormatService;
        _banTransaccionesService = banTransaccionesService;
    }
```

**Paso 7.2 — Declarar la firma en la interfaz** (tras `AnularAsync`):

```csharp
    Task<AbonoCompromisoResultadoDto> AnularAbonoAsync(
        int numeroOrden,
        int numeroAbono,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default);
```

**Paso 7.3 — Compilar y ver ROJO.**

Run: `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly`
Expected: falla con `CS0535` (`OrdenesPagoDirectoService` no implementa `AnularAbonoAsync`).

**Paso 7.4 — Implementar el helper de reverso por poliza_id** (tras `LoadPartidaLineasParaReversarAsync`, ~L1988). Usa el overload de 4 args `TableExistsAsync(connection, transaction, tableName, ct)` (L1613):

```csharp
    /// <summary>
    /// Lee las lineas de la partida identificada por poliza_id e invierte debito/credito
    /// para generar la contrapartida. Reversa un abono puntual por su poliza_id exacto.
    /// </summary>
    private async Task<List<PartidaLineaOrden>> LoadPartidaLineasReversaPorPolizaIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long polizaId,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();

        if (polizaId <= 0 || !await TableExistsAsync(connection, transaction, "con_partida_dtl", ct))
        {
            return new List<PartidaLineaOrden>();
        }

        await using var dtlCmd = connection.CreateCommand();
        dtlCmd.Transaction = transaction;
        dtlCmd.CommandText = @"
SELECT account_id, cost_center_id, description, debit_amount, credit_amount
FROM public.con_partida_dtl
WHERE company_id = @company_id AND poliza_id = @poliza_id
ORDER BY line_number;";
        dtlCmd.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        dtlCmd.Parameters.AddWithValue("poliza_id", NpgsqlDbType.Bigint, polizaId);

        var lineas = new List<PartidaLineaOrden>();
        await using var reader = await dtlCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var accountId = reader.GetInt64(0);
            var costCenterId = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
            var description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var debit = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            var credit = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
            // Invertir debito/credito para la contrapartida.
            lineas.Add(new PartidaLineaOrden(accountId, costCenterId, description, credit, debit));
        }

        return lineas;
    }
```

**Paso 7.5 — Implementar `AnularAbonoAsync`** (tras el cierre de `AnularAsync`, L1923):

```csharp
    public async Task<AbonoCompromisoResultadoDto> AnularAbonoAsync(
        int numeroOrden,
        int numeroAbono,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));
        if (numeroAbono <= 0)
            throw new ArgumentException("El numero de abono no es valido.", nameof(numeroAbono));

        ArgumentNullException.ThrowIfNull(dto);

        var companyId = EnsureCompanyId();

        // 1) Localizar el abono por (numero_orden, numero_abono) — el filtro global limita company_id.
        var abono = await _context.prv_compromiso_abonos
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden && x.numero_abono == numeroAbono)
            .Select(x => new { x.numero_abono, x.estado, x.banco_cuenta_id, x.ban_kardex_id, x.partida_id })
            .FirstOrDefaultAsync(ct);

        if (abono is null)
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, NumeroAbono = numeroAbono, Message = "El abono no existe para esta orden." };

        if (!string.Equals(abono.estado, EstadoAbonoVigente, StringComparison.Ordinal))
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, NumeroAbono = numeroAbono, Message = "El abono ya fue anulado." };

        // 2) Debe ser el ULTIMO abono vigente.
        var maxVigente = await _context.prv_compromiso_abonos
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden && x.estado == EstadoAbonoVigente)
            .Select(x => (int?)x.numero_abono)
            .MaxAsync(ct);

        if (maxVigente != abono.numero_abono)
            return new AbonoCompromisoResultadoDto
            {
                Success = false,
                NumeroOrden = numeroOrden,
                NumeroAbono = numeroAbono,
                Message = $"Solo se puede anular el ultimo abono vigente (abono {maxVigente}). Anule primero los abonos posteriores."
            };

        var hdr = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new { x.concepto, x.status_transacc })
            .FirstOrDefaultAsync(ct);

        if (hdr is null)
            return new AbonoCompromisoResultadoDto { Success = false, NumeroOrden = numeroOrden, NumeroAbono = numeroAbono, Message = "La orden no existe." };

        var usuarioActual = GetCurrentUser();
        var fechaReverso = DateTime.Now.Date;   // hora local, sin TZ
        var motivo = string.IsNullOrWhiteSpace(dto.Motivo)
            ? $"Reverso del abono {abono.numero_abono} de la orden {numeroOrden}."
            : dto.Motivo.Trim();
        if (motivo.Length > 250) motivo = motivo[..250];
        var descripcionRev = $"REVERSO ABONO {abono.numero_abono}: {(hdr.concepto ?? $"Orden {numeroOrden}").Trim()}";
        if (descripcionRev.Length > 200) descripcionRev = descripcionRev[..200];

        // 3) Reverso bancario PRIMERO (abre su propia transaccion; no puede anidarse en la EF de abajo).
        long? banKardexReverso = null;
        if (abono.banco_cuenta_id is > 0 && abono.ban_kardex_id is > 0)
        {
            try
            {
                var (kardexAnulacion, _) = await _banTransaccionesService.AnularMovimientoAsync(
                    abono.banco_cuenta_id.Value, abono.ban_kardex_id.Value, motivo, usuarioActual, ct);
                banKardexReverso = kardexAnulacion;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                var limpio = ex.Message.Split(" (Parameter", StringSplitOptions.None)[0];
                return new AbonoCompromisoResultadoDto
                {
                    Success = false,
                    NumeroOrden = numeroOrden,
                    NumeroAbono = numeroAbono,
                    Message = $"No se pudo reversar el movimiento bancario del abono: {limpio}"
                };
            }
        }

        // 4) Contrapartida contable, estado del abono y reapertura, en una transaccion EF (ambiente o propia).
        var ambient = _context.Database.CurrentTransaction;
        var ownedTx = ambient is null
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

        try
        {
            var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
                ?? throw new InvalidOperationException("No se pudo obtener la transaccion activa para la contrapartida del abono.");

            // (a) Partida inversa por el poliza_id guardado en el abono.
            long? partidaReversoId = null;
            var lineasReversa = await LoadPartidaLineasReversaPorPolizaIdAsync(connection, transaction, abono.partida_id ?? 0, ct);
            if (lineasReversa.Count > 0)
            {
                partidaReversoId = await RegisterPartidaContableAsync(
                    connection, transaction, BuildDocumentNumber(numeroOrden),
                    BuildPartidaNumber(numeroOrden, $"RAB{abono.numero_abono}"),
                    fechaReverso, descripcionRev, usuarioActual, lineasReversa, ct);
            }

            // (b) Marcar el abono anulado + auditoria + trazas de reverso.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE public.prv_compromiso_abono
                      SET estado = 'A',
                          motivo_anulacion = {motivo},
                          usuario_anulacion = {usuarioActual},
                          fecha_anulacion = {fechaReverso},
                          partida_reverso_id = {partidaReversoId},
                          ban_kardex_id_reverso = {banKardexReverso}
                    WHERE company_id = {companyId}
                      AND numero_orden = {numeroOrden}
                      AND numero_abono = {numeroAbono}",
                ct);

            // (c) Si el compromiso estaba Pagado, al quitar este abono el saldo vuelve a ser > 0.
            if (hdr.status_transacc == true)
            {
                await UpdateCompromisoStatusTransaccAsync(connection, transaction, companyId, numeroOrden, statusTransacc: false, ct);
            }

            if (ownedTx is not null) await ownedTx.CommitAsync(ct);
        }
        catch
        {
            if (ownedTx is not null) await ownedTx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownedTx is not null) await ownedTx.DisposeAsync();
        }

        var partes = new List<string>();
        if (abono.partida_id is > 0) partes.Add("se genero la contrapartida contable");
        if (banKardexReverso is > 0) partes.Add("se reverso el movimiento bancario");
        var detalle = partes.Count > 0 ? $" y {string.Join(" y ", partes)}." : ".";

        return new AbonoCompromisoResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            NumeroAbono = abono.numero_abono,
            Message = $"El abono {abono.numero_abono} fue anulado correctamente{detalle}"
        };
    }
```

**Paso 7.6 — Compilar servicio y solución.**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly`
Expected: `0 Error(s)` (el ctor con 6 args y la nueva firma quedan resueltos; DI ya registrada en `ServiceRegistration.cs:143`).

> El camino de éxito con banca (reverso bancario + reverso contable) se verifica manualmente contra el mirror; en el harness solo es testeable el camino contable (sin `banco_cuenta_id`) y los guards — ver Task 13, casos 6 y 7.

---

### Task 8: Comprobante PDF del abono

**Files:**
- Modify: `SIAD.Core/DTOs/Presupuesto/AbonosCompromisoDtos.cs` (agregar `CompromisoAbonoImpresionDto`)
- Modify: `SIAD.Services/Presupuesto/IOrdenesPagoDirectoService.cs` (firma `GetDatosImpresionAbonoAsync`)
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` (impl `GetDatosImpresionAbonoAsync`)
- Create: `SIAD.Reports/Templates/Rpt_Dev_Comprobante_Abono.cs`
- Create: `SIAD.Reports/Templates/Rpt_Dev_Comprobante_Abono.Designer.cs`

El DTO **compone** el `OrdenPagoDirectoImpresionDto` existente (empresa + compromiso) para reutilizar la carga de encabezado/empresa de `GetDatosImpresionAsync`, y añade los campos del abono (monto, saldo anterior, saldo restante, método, partida). La identificación es por `numeroAbono` (canónico).

**Paso 8.1 — Agregar `CompromisoAbonoImpresionDto`** al final de `SIAD.Core/DTOs/Presupuesto/AbonosCompromisoDtos.cs`:

```csharp

/// <summary>Datos de impresion del comprobante de un abono (compone el encabezado del compromiso).</summary>
public sealed class CompromisoAbonoImpresionDto
{
    /// <summary>Encabezado empresa + compromiso, reutilizado de GetDatosImpresionAsync.</summary>
    public OrdenPagoDirectoImpresionDto Base { get; set; } = new();

    public int NumeroAbono { get; set; }

    public DateTime FechaAbono { get; set; }

    public decimal MontoAbono { get; set; }

    public decimal SaldoAnterior { get; set; }

    public decimal SaldoRestante { get; set; }

    public string MetodoPago { get; set; } = string.Empty;

    public string? NumeroPartida { get; set; }

    /// <summary>'V' vigente, 'A' anulado.</summary>
    public string Estado { get; set; } = "V";
}
```

**Paso 8.2 — Declarar la firma en la interfaz:**

```csharp
    Task<CompromisoAbonoImpresionDto?> GetDatosImpresionAbonoAsync(
        int numeroOrden,
        int numeroAbono,
        CancellationToken ct = default);
```

**Paso 8.3 — Implementar `GetDatosImpresionAbonoAsync`** en `OrdenesPagoDirectoService.cs` (junto a `GetDatosImpresionAsync`). Reutiliza `GetDatosImpresionAsync` (encabezado) y `GetSaldoConAbonosAsync` (abonos + monto), calculando saldo anterior/restante desde el desglose:

```csharp
    public async Task<CompromisoAbonoImpresionDto?> GetDatosImpresionAbonoAsync(
        int numeroOrden,
        int numeroAbono,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0 || numeroAbono <= 0)
        {
            return null;
        }

        var baseDatos = await GetDatosImpresionAsync(numeroOrden, ct);
        if (baseDatos is null)
        {
            return null;
        }

        var saldo = await GetSaldoConAbonosAsync(numeroOrden, ct);
        if (saldo is null)
        {
            return null;
        }

        var abono = saldo.Abonos.FirstOrDefault(a => a.NumeroAbono == numeroAbono);
        if (abono is null)
        {
            return null;
        }

        // Saldo anterior = monto - SUM(vigentes con numero_abono < este). Restante = anterior - este (si vigente).
        var vigenteEsteAbono = string.Equals(abono.Estado, EstadoAbonoVigente, StringComparison.Ordinal);
        var abonadoPrevio = saldo.Abonos
            .Where(a => a.NumeroAbono < numeroAbono && string.Equals(a.Estado, EstadoAbonoVigente, StringComparison.Ordinal))
            .Sum(a => a.Monto);
        var saldoAnterior = baseDatos.Compromiso.Monto - abonadoPrevio;
        var saldoRestante = saldoAnterior - (vigenteEsteAbono ? abono.Monto : 0m);
        if (saldoRestante < 0m) saldoRestante = 0m;

        return new CompromisoAbonoImpresionDto
        {
            Base = baseDatos,
            NumeroAbono = abono.NumeroAbono,
            FechaAbono = abono.Fecha,
            MontoAbono = abono.Monto,
            SaldoAnterior = saldoAnterior,
            SaldoRestante = saldoRestante,
            MetodoPago = abono.MetodoPago,
            NumeroPartida = abono.NumeroPartida,
            Estado = abono.Estado
        };
    }
```

> `GetDatosImpresionAsync(int numeroOrden, CancellationToken)` → `Task<OrdenPagoDirectoImpresionDto?>` ya existe (lo usa el controller `GetPdf`). Si su firma real difiere (p. ej. sin `?`), ajustar el `null`-check acorde.

**Paso 8.4 — Compilar Core + Services** (el reporte se crea en 8.5; el build de `apc` se valida en Task 9).

Run: `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly`
Expected: `0 Error(s)`.

---
**Paso 8.5 — Crear el reporte `Rpt_Dev_Comprobante_Abono.cs`.** Estructura y helpers (AddLabel, panel, Money, EsHn, marca de agua ANULADO) tomados de `Rpt_Dev_Compromiso_Proveedor.cs`; comprobante de una página con monto abonado, saldo anterior y saldo restante:

```csharp
using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Reports;

public sealed class Rpt_Dev_Comprobante_Abono : XtraReport
{
    private const float ContentWidth = 750f;
    private const string FontFamily = "Times New Roman";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    public Rpt_Dev_Comprobante_Abono(CompromisoAbonoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        PaperKind = DXPaperKind.Letter;
        PageWidth = 850;
        PageHeight = 1100;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;
        Font = new DXFont(FontFamily, 11f);

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        if (string.Equals(datos.Estado, "A", StringComparison.OrdinalIgnoreCase))
        {
            Watermarks.Add(new XRWatermark
            {
                Id = "MarcaAnulado",
                Text = "ANULADO",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont(FontFamily, 90f, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 190,
                TextPosition = WatermarkPosition.InFront
            });
        }
    }

    private static float BuildDocumento(Band band, CompromisoAbonoImpresionDto datos)
    {
        var b = datos.Base;
        var compromiso = b.Compromiso;
        var y = 0f;

        // Encabezado empresa.
        AddLabel(band, b.EmpresaNombre, 0f, y, 520f, 20f, 14f, bold: true);
        y += 22f;
        if (!string.IsNullOrWhiteSpace(b.EmpresaRtn))
        {
            AddLabel(band, $"R.T.N. {b.EmpresaRtn!.Trim()}", 0f, y, 520f, 13f, 8.5f, color: Color.DimGray);
            y += 14f;
        }
        if (!string.IsNullOrWhiteSpace(b.EmpresaDireccion))
        {
            AddLabel(band, b.EmpresaDireccion!.Trim(), 0f, y, 520f, 13f, 8.5f, color: Color.DimGray);
            y += 14f;
        }

        // Caja de titulo del comprobante.
        var panel = new XRPanel { BoundsF = new RectangleF(520f, 0f, 230f, 72f), Borders = BorderSide.All, BorderWidth = 2f };
        band.Controls.Add(panel);
        AddLabel(panel, "COMPROBANTE DE ABONO", 0f, 6f, 230f, 13f, 8.5f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(panel, $"Compromiso OPD-{compromiso.NumeroOrden}", 0f, 21f, 230f, 16f, 10f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(panel, $"Abono No. {datos.NumeroAbono}", 0f, 39f, 230f, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(panel, $"Fecha: {datos.FechaAbono.ToString("dd/MM/yyyy", EsHn)}", 0f, 57f, 230f, 12f, 8f, align: TextAlignment.MiddleCenter);

        y = Math.Max(y, 78f) + 8f;
        band.Controls.Add(new XRLine { BoundsF = new RectangleF(0f, y, ContentWidth, 3f), LineWidth = 3f });
        y += 12f;

        // Proveedor / concepto.
        AddLabel(band, "Proveedor:", 0f, y, 78f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(compromiso.CodigoProveedor)
            ? compromiso.Proveedor
            : $"{compromiso.CodigoProveedor!.Trim()} - {compromiso.Proveedor}", 80f, y, 670f, 15f, 10f);
        y += 18f;

        AddLabel(band, "Concepto:", 0f, y, 72f, 15f, 10f, bold: true);
        AddLabel(band, compromiso.Concepto, 74f, y, 676f, 15f, 10f);
        y += 20f;

        AddLabel(band, "Metodo de pago:", 0f, y, 110f, 15f, 10f, bold: true);
        AddLabel(band, datos.MetodoPago, 112f, y, 250f, 15f, 10f);
        if (!string.IsNullOrWhiteSpace(datos.NumeroPartida))
        {
            AddLabel(band, "Partida:", 390f, y, 68f, 15f, 10f, bold: true);
            AddLabel(band, datos.NumeroPartida!, 460f, y, 290f, 15f, 10f);
        }
        y += 26f;

        // Cuadro de montos: compromiso, abono, saldo anterior, saldo restante.
        y = AddMontoRow(band, y, "Monto del compromiso", compromiso.Monto, bold: false);
        y = AddMontoRow(band, y, "Saldo anterior", datos.SaldoAnterior, bold: false);
        y = AddMontoRow(band, y, "MONTO ABONADO", datos.MontoAbono, bold: true);
        y = AddMontoRow(band, y, "SALDO RESTANTE", datos.SaldoRestante, bold: true);
        y += 10f;

        var enLetras = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Text = $"SON (abono): {b.MontoEnLetras}",
            Font = new DXFont(FontFamily, 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(8, 8, 0, 0, 100f)
        };
        band.Controls.Add(enLetras);
        y += 30f;

        // Firmas.
        y += 40f;
        string[] titulos = ["ENTREGADO POR", "RECIBIDO CONFORME"];
        const float anchoColumna = 240f;
        const float paso = 380f;
        for (var i = 0; i < titulos.Length; i++)
        {
            var x = i * paso;
            band.Controls.Add(new XRLine { BoundsF = new RectangleF(x, y, anchoColumna, 1.5f), LineWidth = 1f });
            AddLabel(band, titulos[i], x, y + 4f, anchoColumna, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
        }
        AddLabel(band, b.ImpresoPor, 0f, y + 17f, anchoColumna, 11f, 8f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 40f;

        band.Controls.Add(new XRLine { BoundsF = new RectangleF(0f, y, ContentWidth, 2f), LineStyle = DXDashStyle.Dash, LineWidth = 1f, ForeColor = Color.LightGray });
        AddLabel(band, $"Comprobante de abono {datos.NumeroAbono} - Documento OPD-{compromiso.NumeroOrden} - SIAD", 0f, y + 6f, ContentWidth, 12f, 7.5f, color: Color.DimGray);
        AddLabel(band, $"Impreso por {b.ImpresoPor} el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}", 0f, y + 18f, ContentWidth, 12f, 7.5f, color: Color.DimGray);

        return y + 34f;
    }

    private static float AddMontoRow(Band band, float y, string etiqueta, decimal valor, bool bold)
    {
        var panel = new XRPanel { BoundsF = new RectangleF(390f, y, 360f, 22f), Borders = BorderSide.All, BorderWidth = bold ? 2f : 1f };
        band.Controls.Add(panel);
        AddLabel(panel, etiqueta, 8f, 0f, 210f, 22f, bold ? 10f : 9f, bold: bold, TextAlignment.MiddleLeft, color: bold ? Color.Black : Color.DimGray);
        AddLabel(panel, $"L {Money(valor)}", 8f, 0f, 344f, 22f, bold ? 12f : 10f, bold: bold, TextAlignment.MiddleRight);
        return y + 26f;
    }

    private static void AddLabel(
        XRControl parent, string text, float x, float y, float width, float height, float fontSize,
        bool bold = false, TextAlignment align = TextAlignment.MiddleLeft, Color? color = null)
    {
        var estilo = bold ? DXFontStyle.Bold : DXFontStyle.Regular;
        parent.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, y, width, height),
            Text = text,
            Font = new DXFont(FontFamily, fontSize, estilo),
            TextAlignment = align,
            Multiline = false,
            WordWrap = false,
            CanGrow = false,
            ForeColor = color ?? Color.Black,
            Borders = BorderSide.None,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });
    }

    private static string Money(decimal value) => value.ToString("N2", EsHn);
}
```

**Paso 8.6 — Crear `Rpt_Dev_Comprobante_Abono.Designer.cs`** (mínimo; el reporte se construye por código, sin diseñador visual):

```csharp
namespace SIAD.Reports;

partial class Rpt_Dev_Comprobante_Abono
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
    }
}
```

> Nota: `Rpt_Dev_Compromiso_Proveedor` no tiene `.Designer.cs` propio (se arma 100% por código). Para `Rpt_Dev_Comprobante_Abono` no se llama a `InitializeComponent()` en el ctor (igual que el reporte de referencia); el `.Designer.cs` se incluye solo por convención/estructura y no estorba. Si el proyecto marcara el `partial`/`components` sin uso como error, omitir el `.Designer.cs` y dejar el reporte como archivo único (aceptable — el de referencia es archivo único).

**Paso 8.7 — Compilar SIAD.Reports.**

Run: `dotnet build SIAD.Reports/SIAD.Reports.csproj -clp:ErrorsOnly`
Expected: `0 Error(s)`.

---

### Task 9: Endpoints en `OrdenesPagoDirectoController`

**Files:**
- Modify: `apc/Controllers/Presupuesto/OrdenesPagoDirectoController.cs:248` (insertar antes del `}` de cierre de clase, L249, tras `Anular`)

Hereda la política de clase `[Authorize(Policy = AuthorizationPolicies.Contabilidad)]` (L12) — no se agregan atributos de autorización. El controlador ya importa `using SIAD.Core.DTOs.Presupuesto;` (L4) y `using SIAD.Reports;` (L5). Rutas por `numeroAbono:int` (canónico).

**Paso 9.1 — `GET {numeroOrden:int}/saldo`:**

```csharp
    [HttpGet("{numeroOrden:int}/saldo")]
    public async Task<IActionResult> GetSaldo(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        var result = await _service.GetSaldoConAbonosAsync(numeroOrden, ct);
        return result is null ? NotFound() : Ok(result);
    }
```

**Paso 9.2 — `POST {numeroOrden:int}/abonos`** (registrar). `result.Success ? Ok : Conflict`; `ArgumentException→BadRequest`, `KeyNotFoundException→NotFound`, `InvalidOperationException→Conflict`:

```csharp
    [HttpPost("{numeroOrden:int}/abonos")]
    public async Task<IActionResult> RegistrarAbono(
        int numeroOrden,
        [FromBody] AbonoCompromisoUpsertDto dto,
        CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _service.RegistrarAbonoAsync(numeroOrden, dto, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
```

**Paso 9.3 — `POST {numeroOrden:int}/abonos/{numeroAbono:int}/anular`** (reutiliza `AnularOrdenPagoDirectoDto`):

```csharp
    [HttpPost("{numeroOrden:int}/abonos/{numeroAbono:int}/anular")]
    public async Task<IActionResult> AnularAbono(
        int numeroOrden,
        int numeroAbono,
        [FromBody] AnularOrdenPagoDirectoDto dto,
        CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        if (numeroAbono <= 0)
            return BadRequest(new { message = "El numero de abono no es valido." });

        try
        {
            var result = await _service.AnularAbonoAsync(numeroOrden, numeroAbono, dto, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
```

**Paso 9.4 — `GET {numeroOrden:int}/abonos/{numeroAbono:int}/comprobante/pdf`** (copia del patrón `GetPdf` L76-97, `Content-Disposition: inline`):

```csharp
    [HttpGet("{numeroOrden:int}/abonos/{numeroAbono:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobanteAbonoPdf(int numeroOrden, int numeroAbono, CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        if (numeroAbono <= 0)
            return BadRequest(new { message = "El numero de abono no es valido." });

        var datos = await _service.GetDatosImpresionAbonoAsync(numeroOrden, numeroAbono, ct);
        if (datos is null)
        {
            return NotFound(new { message = $"No se encontro el abono {numeroAbono} del compromiso {numeroOrden}." });
        }

        using var report = new Rpt_Dev_Comprobante_Abono(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Comprobante-Abono-{numeroOrden}-{numeroAbono}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }
```

**Paso 9.5 — Compilar `apc`** (requiere Tasks 4-8 integradas).

Run: `dotnet build apc/apc.csproj -clp:ErrorsOnly`
Expected: `0 Error(s)`.

---

### Task 10: Métodos de abonos en `OrdenesPagoDirectoClient`

**Files:**
- Modify: `apc.Client/Services/Presupuesto/OrdenesPagoDirectoClient.cs:19` (URL static tras `GetPdfUrl`)
- Modify: `apc.Client/Services/Presupuesto/OrdenesPagoDirectoClient.cs:208` (métodos antes de `ReadOperationResultAsync`, L210)

**Un solo cliente (canónico): se extiende `OrdenesPagoDirectoClient`** (ya registrado en `CommonServices.cs:100`). NO se crea `AbonosCompromisoClient`. `using System.Net` (L1), `using SIAD.Core.DTOs.Presupuesto` (L5) ya presentes.

**Paso 10.1 — URL static del comprobante** (tras `GetPdfUrl`, L19):

```csharp
    public static string GetComprobanteAbonoUrl(int numeroOrden, int numeroAbono)
        => $"/api/presupuesto/ordenes-pago-directo/{numeroOrden}/abonos/{numeroAbono}/comprobante/pdf";
```

**Paso 10.2 — `GetSaldoConAbonosAsync`** (patrón de `GetByNumeroOrdenAsync`: `null` si inválido o 404):

```csharp
    public async Task<CompromisoSaldoDto?> GetSaldoConAbonosAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/saldo", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<CompromisoSaldoDto>(ct);
    }
```

**Paso 10.3 — `RegistrarAbonoAsync`** (devuelve el DTO canónico de resultado):

```csharp
    public async Task<AbonoCompromisoResultadoDto> RegistrarAbonoAsync(
        int numeroOrden,
        AbonoCompromisoUpsertDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/abonos", dto, ct);
        return await ReadAbonoResultAsync(response, ct);
    }
```

**Paso 10.4 — `AnularAbonoAsync`** (por `numeroAbono:int`):

```csharp
    public async Task<AbonoCompromisoResultadoDto> AnularAbonoAsync(
        int numeroOrden,
        int numeroAbono,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        if (numeroAbono <= 0)
            throw new ArgumentException("El numero de abono no es valido.", nameof(numeroAbono));

        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/abonos/{numeroAbono}/anular", dto, ct);
        return await ReadAbonoResultAsync(response, ct);
    }
```

**Paso 10.5 — Helper `ReadAbonoResultAsync`** (envuelve el resultado igual que `ReadOperationResultAsync` pero para `AbonoCompromisoResultadoDto`; insertar junto a `ReadOperationResultAsync`, L210):

```csharp
    private static async Task<AbonoCompromisoResultadoDto> ReadAbonoResultAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var result = await response.ReadFromJsonAsyncWithAuthCheck<AbonoCompromisoResultadoDto>(ct);
        return result ?? new AbonoCompromisoResultadoDto
        {
            Success = false,
            Message = "No se recibio respuesta del servidor."
        };
    }
```

> `ReadFromJsonAsyncWithAuthCheck<T>(ct)` lanza `UnauthorizedAccessException` en 401/redirect a login; en 409 (`Conflict`) el cuerpo sigue siendo un `AbonoCompromisoResultadoDto` con `Success=false`, así que se deserializa igual (no se trata como error de transporte). Es el mismo criterio que `ReadOperationResultAsync` (L210-221).

**Paso 10.6 — Compilar `apc.Client` y la solución.**

Run: `dotnet build apc.Client/apc.Client.csproj -clp:ErrorsOnly`
Run: `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly`
Expected: `0 Error(s)` en ambos.

---

### Task 11: Página Blazor `CompromisoProveedorAbonar.razor`

**Files:**
- Create: `apc.Client/Pages/Proveedores/CompromisoProveedorAbonar.razor`
- Create: `apc.Client/Pages/Proveedores/CompromisoProveedorAbonar.razor.css`

**Cliente único (canónico):** inyecta `OrdenesPagoDirectoClient` (no `AbonosCompromisoClient`) y consume `GetSaldoConAbonosAsync` → `CompromisoSaldoDto`, `RegistrarAbonoAsync` → `AbonoCompromisoResultadoDto`, `AnularAbonoAsync(int, int, ...)`, y `GetComprobanteAbonoUrl(int, int)`. Los datos del proveedor/concepto/cuenta contable se leen de `GetByNumeroOrdenAsync` → `OrdenPagoDirectoDetalleDto` (el `CompromisoSaldoDto` canónico no los trae). Cuentas origen: `GetCuentasContraProcesamientoAsync` → `List<CuentaContableLookupDto>`.

**Verificado (dxdocs):** `DxSpinEdit<T>` usa `MinValue`/`MaxValue` (no `Min`/`Max`). `OrdenPagoDirectoMetodoPago` es clase estática de constantes `string` (no enum): el combo se arma a mano y el valor enviado es "CONTABLE"/"TRANSFERENCIA"/... compatible con `EsBancario`. `CuentaDestinoProveedorId` se envía `null` (ver Decisiones asumidas). El anular usa `item.NumeroAbono`.

**Paso 11.1 — Crear el `.razor`** (parte visual). Create `apc.Client/Pages/Proveedores/CompromisoProveedorAbonar.razor`:

```razor
@page "/proveedores/compromisos-proveedor/{NumeroOrden:int}/abonar"
@attribute [Authorize(Policy = AuthorizationPolicies.Contabilidad)]

@using DevExpress.Blazor
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.JSInterop
@using SIAD.Core.Constants
@using SIAD.Core.DTOs.Contabilidad
@using SIAD.Core.DTOs.Presupuesto
@using apc.Client.Services.Contabilidad
@using apc.Client.Services.Presupuesto

@inject OrdenesPagoDirectoClient OrdenesPagoDirectoClient
@inject AccountFormatState AccountFormat
@inject NavigationManager Navigation
@inject AuthenticationStateProvider AuthStateProvider
@inject IJSRuntime JS
@inject ILogger<CompromisoProveedorAbonar> Logger
@inject IToastNotificationService ToastService

<PageTitle>SIAD - Abonar Compromiso</PageTitle>

<div class="page-container">
    <DxToastProvider Name="CompromisoProveedorAbonarToast" MaxToastCount="4" />
    <ErrorNotice @ref="errorNotice" />

    <div class="d-flex justify-content-between align-items-center mb-4 header-section">
        <div>
            <h2 class="page-title">Abonos del compromiso de proveedor</h2>
            <p class="page-subtitle">Registre pagos parciales o totales y consulte el historial de abonos.</p>
        </div>
        <DxButton Text="Volver" IconCssClass="bi bi-arrow-left" RenderStyle="ButtonRenderStyle.Secondary"
                  CssClass="btn-modern shadow-sm" Click="Volver" />
    </div>

    @if (isLoading)
    {
        <div class="modern-card">
            <DxSkeletonLine />
            <DxSkeletonLine />
            <DxSkeletonLine />
        </div>
    }
    else if (saldo is null)
    {
        <div class="alert alert-warning" role="alert">No se encontro el compromiso solicitado.</div>
    }
    else
    {
        <div class="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-3">
            <span class="text-muted">Compromiso <strong>@saldo.NumeroOrden</strong> &middot; @ProveedorResumenTexto</span>
            <span class="badge-status @EstadoBadgeClass">@saldo.EstadoTexto</span>
        </div>

        <div class="summary-strip mb-3">
            <div class="summary-card">
                <div class="summary-label">Monto</div>
                <div class="summary-value">@saldo.Monto.ToString("N2")</div>
            </div>
            <div class="summary-card">
                <div class="summary-label">Abonado</div>
                <div class="summary-value">@saldo.Abonado.ToString("N2")</div>
            </div>
            <div class="summary-card">
                <div class="summary-label">Saldo</div>
                <div class="summary-value">@saldo.Saldo.ToString("N2")</div>
            </div>
        </div>

        @if (EstaPagado)
        {
            <div class="alert alert-info" role="alert">
                Este compromiso ya esta pagado (saldo cero). No admite nuevos abonos.
            </div>
        }
        else
        {
            <div class="modern-card mb-3">
                <div class="d-flex justify-content-between align-items-center gap-2 mb-3 section-header">
                    <div>
                        <h5 class="mb-0">Nuevo abono</h5>
                        <small class="text-muted">El monto debe ser mayor a cero y no superar el saldo pendiente.</small>
                    </div>
                    <DxButton Text="Abonar todo el saldo" IconCssClass="bi bi-cash-stack"
                              RenderStyle="ButtonRenderStyle.Secondary" CssClass="btn-modern shadow-sm"
                              Click="AbonarTodoElSaldo" Enabled="@(!isSaving)" />
                </div>

                @if (!TieneCuentaProveedorDisponible)
                {
                    <div class="alert alert-warning mb-3" role="alert">
                        El proveedor no tiene una cuenta contable valida configurada. No se puede registrar el abono.
                    </div>
                }
                @if (cuentasContra.Count == 0)
                {
                    <div class="alert alert-warning mb-3" role="alert">
                        No hay cuentas de origen disponibles para el abono.
                    </div>
                }

                <DxFormLayout CaptionPosition="CaptionPosition.Vertical">
                    <DxFormLayoutItem Caption="Monto del abono" ColSpanMd="4">
                        <DxSpinEdit TValue="decimal" @bind-Value="nuevoMonto"
                                    MinValue="0m" MaxValue="@saldo.Saldo" Increment="0.01m"
                                    DisplayFormat="N2" InputCssClass="text-end"
                                    ValidationEnabled="false" Enabled="@PuedeEditarFormulario" />
                    </DxFormLayoutItem>
                    <DxFormLayoutItem Caption="Metodo de pago" ColSpanMd="4">
                        <DxComboBox Data="@metodosPago" TData="MetodoPagoItem" TValue="string"
                                    @bind-Value="nuevoMetodo"
                                    ValueFieldName="@nameof(MetodoPagoItem.Value)"
                                    TextFieldName="@nameof(MetodoPagoItem.Display)"
                                    NullText="Seleccione metodo" DropDownStyle="DropDownStyle.DropDownList"
                                    Enabled="@PuedeEditarFormulario" />
                    </DxFormLayoutItem>
                    <DxFormLayoutItem Caption="Cuenta de origen" ColSpanMd="4">
                        <DxComboBox Data="@cuentasContra" TData="CuentaContableLookupDto" TValue="long"
                                    @bind-Value="cuentaOrigenId"
                                    ValueFieldName="@nameof(CuentaContableLookupDto.AccountId)"
                                    TextFieldName="@nameof(CuentaContableLookupDto.Display)"
                                    NullText="Seleccione cuenta de origen"
                                    SearchMode="ListSearchMode.AutoSearch"
                                    SearchFilterCondition="ListSearchFilterCondition.Contains"
                                    DropDownStyle="DropDownStyle.DropDown"
                                    AllowUserInput="true"
                                    ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto"
                                    ValidationEnabled="false" Enabled="@PuedeEditarFormulario" />
                    </DxFormLayoutItem>
                </DxFormLayout>

                <div class="total-bar mt-3">
                    <div class="text-muted">Usuario: <strong>@UsuarioActualTexto</strong></div>
                    <DxButton Text="@(isSaving ? "Registrando..." : "Registrar abono")"
                              IconCssClass="bi bi-check2-circle" RenderStyle="ButtonRenderStyle.Success"
                              CssClass="btn-modern shadow-sm" Click="RegistrarAbonoAsync" Enabled="@CanRegistrar" />
                </div>
            </div>
        }

        <div class="modern-card">
            <div class="d-flex justify-content-between align-items-center gap-2 mb-3 section-header">
                <div>
                    <h5 class="mb-0">Historial de abonos</h5>
                    <small class="text-muted">Solo puede anularse el ultimo abono vigente.</small>
                </div>
                <span class="summary-pill">Abonos: <strong>@saldo.Abonos.Count</strong></span>
            </div>

            @if (saldo.Abonos.Count == 0)
            {
                <div class="alert alert-info mb-0" role="alert">Este compromiso aun no tiene abonos registrados.</div>
            }
            else
            {
                <div class="grid-wrapper">
                    <DxGrid Data="@saldo.Abonos" TData="AbonoCompromisoListItemDto"
                            KeyFieldName="@nameof(AbonoCompromisoListItemDto.NumeroAbono)"
                            PageSize="10" ShowFilterRow="false" ShowSearchBox="false" ShowGroupPanel="false"
                            SizeMode="SizeMode.Large" CssClass="grid-modern">
                        <Columns>
                            <DxGridDataColumn FieldName="@nameof(AbonoCompromisoListItemDto.NumeroAbono)" Caption="#"
                                              Width="70px" TextAlignment="GridTextAlignment.Center" />
                            <DxGridDataColumn FieldName="@nameof(AbonoCompromisoListItemDto.Fecha)" Caption="Fecha"
                                              Width="120px" DisplayFormat="dd/MM/yyyy" />
                            <DxGridDataColumn FieldName="@nameof(AbonoCompromisoListItemDto.MetodoPago)" Caption="Metodo" MinWidth="140" />
                            <DxGridDataColumn FieldName="@nameof(AbonoCompromisoListItemDto.Monto)" Caption="Monto"
                                              Width="140px" DisplayFormat="N2" TextAlignment="GridTextAlignment.Right" />
                            <DxGridDataColumn FieldName="@nameof(AbonoCompromisoListItemDto.NumeroPartida)" Caption="Partida" Width="150px">
                                <CellDisplayTemplate Context="cell">
                                    @GetTextOrDash(((AbonoCompromisoListItemDto)cell.DataItem).NumeroPartida)
                                </CellDisplayTemplate>
                            </DxGridDataColumn>
                            <DxGridDataColumn Caption="Estado" Width="120px">
                                <CellDisplayTemplate Context="cell">
                                    @if (cell.DataItem is AbonoCompromisoListItemDto item)
                                    {
                                        <span class="badge-status @(EsVigente(item) ? "active" : "inactive")">
                                            @(EsVigente(item) ? "Vigente" : "Anulado")
                                        </span>
                                    }
                                </CellDisplayTemplate>
                            </DxGridDataColumn>
                            <DxGridDataColumn Caption="Acciones" Width="130px">
                                <CellDisplayTemplate Context="cell">
                                    @if (cell.DataItem is AbonoCompromisoListItemDto item)
                                    {
                                        <div class="d-flex flex-wrap gap-2">
                                            <button type="button" class="btn-icon btn-view" title="Ver comprobante"
                                                    @onclick="@(() => VerComprobanteAsync(item.NumeroAbono))">
                                                <i class="bi bi-printer"></i>
                                            </button>
                                            <button type="button" class="btn-icon btn-delete" title="Anular abono"
                                                    disabled="@(!item.PuedeAnular || isSaving)"
                                                    @onclick="@(() => AnularAbonoAsync(item.NumeroAbono))">
                                                <i class="bi bi-x-circle"></i>
                                            </button>
                                        </div>
                                    }
                                </CellDisplayTemplate>
                            </DxGridDataColumn>
                        </Columns>
                    </DxGrid>
                </div>
            }
        </div>
    }
</div>
```
**Paso 11.2 — Añadir el bloque `@code`** al final del mismo `.razor`:

```razor
@code {
    [Parameter]
    public int NumeroOrden { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    private readonly List<CuentaContableLookupDto> cuentasContra = new();

    // OrdenPagoDirectoMetodoPago es clase estatica de constantes string (Contable="CONTABLE", ...),
    // NO enum. El Value enviado al backend usa esas constantes para ser compatible con EsBancario.
    private readonly List<MetodoPagoItem> metodosPago = new()
    {
        new(OrdenPagoDirectoMetodoPago.Contable, "Contable"),
        new(OrdenPagoDirectoMetodoPago.Deposito, "Deposito"),
        new(OrdenPagoDirectoMetodoPago.Cheque, "Cheque"),
        new(OrdenPagoDirectoMetodoPago.Transferencia, "Transferencia"),
    };

    private CompromisoSaldoDto? saldo;
    private OrdenPagoDirectoDetalleDto? detalle;
    private decimal nuevoMonto;
    private string? nuevoMetodo = OrdenPagoDirectoMetodoPago.Contable;
    private long cuentaOrigenId;
    private bool isLoading = true;
    private bool isSaving;
    private ErrorNotice errorNotice = default!;
    private readonly List<string> validationMessages = new();
    private string usuarioActual = "SISTEMA";

    private bool EstaPagado => saldo is not null && saldo.Saldo <= 0m;
    private bool TieneCuentaProveedorDisponible => !string.IsNullOrWhiteSpace(detalle?.CuentaContableProveedor);
    private bool PuedeEditarFormulario => !isSaving && !EstaPagado && TieneCuentaProveedorDisponible && cuentasContra.Count > 0;
    private string UsuarioActualTexto => string.IsNullOrWhiteSpace(usuarioActual) ? "SISTEMA" : usuarioActual;
    private string ProveedorResumenTexto => BuildProveedorResumenTexto();
    private string EstadoBadgeClass => saldo?.EstadoTexto switch
    {
        "Pagado" => "inactive",
        "Anulado" => "inactive",
        "Abonado parcial" => "partial",
        _ => "pending"
    };
    private bool CanRegistrar => PuedeEditarFormulario &&
                                 nuevoMonto > 0m &&
                                 nuevoMonto <= (saldo?.Saldo ?? 0m) &&
                                 cuentaOrigenId > 0 &&
                                 !string.IsNullOrWhiteSpace(nuevoMetodo);

    protected override async Task OnParametersSetAsync() => await CargarAsync();

    private async Task CargarAsync()
    {
        isLoading = true;
        validationMessages.Clear();
        saldo = null;
        detalle = null;

        try
        {
            await AccountFormat.EnsureLoadedAsync();

            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            usuarioActual = authState.User.Identity?.Name ?? "SISTEMA";

            var saldoTask = OrdenesPagoDirectoClient.GetSaldoConAbonosAsync(NumeroOrden);
            var detalleTask = OrdenesPagoDirectoClient.GetByNumeroOrdenAsync(NumeroOrden);
            var cuentasTask = OrdenesPagoDirectoClient.GetCuentasContraProcesamientoAsync();
            await Task.WhenAll(saldoTask, detalleTask, cuentasTask);

            saldo = await saldoTask;
            detalle = await detalleTask;
            cuentasContra.Clear();
            cuentasContra.AddRange(await cuentasTask);

            if (saldo is null)
            {
                ShowToast("Abonar compromiso", $"No se encontro el compromiso {NumeroOrden}.", ToastRenderStyle.Danger);
                return;
            }

            ResetFormulario();
        }
        catch (Exception ex)
        {
            ShowToast("Abonar compromiso", "No se pudo cargar la pantalla de abonos.", ToastRenderStyle.Danger);
            Logger.LogError(ex, "Error al cargar pantalla de abonos para compromiso {NumeroOrden}", NumeroOrden);
        }
        finally
        {
            isLoading = false;
        }
    }

    private void ResetFormulario()
    {
        nuevoMonto = 0m;
        nuevoMetodo = OrdenPagoDirectoMetodoPago.Contable;
        cuentaOrigenId = 0;
    }

    private void AbonarTodoElSaldo()
    {
        if (saldo is not null)
        {
            nuevoMonto = saldo.Saldo;
        }
    }

    private async Task RegistrarAbonoAsync()
    {
        if (saldo is null || EstaPagado)
        {
            return;
        }

        validationMessages.Clear();
        if (!Validar())
        {
            ShowToast("Validacion", string.Join(" | ", validationMessages), ToastRenderStyle.Warning);
            return;
        }

        try
        {
            isSaving = true;

            var dto = new AbonoCompromisoUpsertDto
            {
                Monto = nuevoMonto,
                MetodoPago = nuevoMetodo!,
                CuentaContraId = cuentaOrigenId,
                CuentaDestinoProveedorId = null, // informativo; combo de cuenta destino pendiente
                Usuario = UsuarioActualTexto
            };

            var result = await OrdenesPagoDirectoClient.RegistrarAbonoAsync(NumeroOrden, dto);
            if (!result.Success)
            {
                ShowToast("Abonar compromiso", result.Message, ToastRenderStyle.Danger);
                return;
            }

            ShowToast("Exito", result.Message, ToastRenderStyle.Success);
            if (result.NumeroAbono > 0)
            {
                await JS.InvokeVoidAsync("open",
                    OrdenesPagoDirectoClient.GetComprobanteAbonoUrl(NumeroOrden, result.NumeroAbono), "_blank");
            }

            await CargarAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error al registrar abono del compromiso {NumeroOrden}", NumeroOrden);
            errorNotice.ShowError("No se pudo registrar el abono.", ex);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task AnularAbonoAsync(int numeroAbono)
    {
        var confirmado = await JS.InvokeAsync<bool>("confirm", $"Anular el abono #{numeroAbono} del compromiso {NumeroOrden}?");
        if (!confirmado)
        {
            return;
        }

        try
        {
            isSaving = true;

            var dto = new AnularOrdenPagoDirectoDto { Motivo = "Anulacion desde pantalla de abonos" };
            var result = await OrdenesPagoDirectoClient.AnularAbonoAsync(NumeroOrden, numeroAbono, dto);
            if (!result.Success)
            {
                ShowToast("Anular abono", result.Message, ToastRenderStyle.Danger);
                return;
            }

            ShowToast("Exito", result.Message, ToastRenderStyle.Success);
            await CargarAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error al anular abono {NumeroAbono} del compromiso {NumeroOrden}", numeroAbono, NumeroOrden);
            errorNotice.ShowError("No se pudo anular el abono.", ex);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task VerComprobanteAsync(int numeroAbono)
        => await JS.InvokeVoidAsync("open", OrdenesPagoDirectoClient.GetComprobanteAbonoUrl(NumeroOrden, numeroAbono), "_blank");

    private bool Validar()
    {
        if (saldo is null)
        {
            validationMessages.Add("No se cargo el saldo del compromiso.");
            return false;
        }

        if (!TieneCuentaProveedorDisponible)
        {
            validationMessages.Add("El proveedor no tiene una cuenta contable valida configurada.");
        }

        if (nuevoMonto <= 0m)
        {
            validationMessages.Add("El monto del abono debe ser mayor a cero.");
        }
        else if (nuevoMonto > saldo.Saldo)
        {
            validationMessages.Add($"El monto no puede superar el saldo pendiente ({saldo.Saldo:N2}).");
        }

        if (cuentaOrigenId <= 0)
        {
            validationMessages.Add("Debe seleccionar la cuenta de origen.");
        }

        if (string.IsNullOrWhiteSpace(nuevoMetodo))
        {
            validationMessages.Add("Debe seleccionar el metodo de pago.");
        }

        return validationMessages.Count == 0;
    }

    private void Volver()
    {
        var returnUrl = NormalizeLocalUrl(ReturnUrl) ?? "/proveedores/compromisos-proveedor";
        Navigation.NavigateTo(returnUrl, true);
    }

    private static bool EsVigente(AbonoCompromisoListItemDto item)
        => string.Equals(item.Estado, "V", StringComparison.OrdinalIgnoreCase);

    private string BuildProveedorResumenTexto()
    {
        var correlativo = detalle?.CorrelativoProveedor?.ToString() ?? "-";
        var proveedor = GetTextOrDash(detalle?.Proveedor);
        if (correlativo == "-")
        {
            return proveedor;
        }
        return proveedor == "-" ? correlativo : $"{correlativo} - {proveedor}";
    }

    private void ShowToast(string title, string message, ToastRenderStyle style)
    {
        ToastService.ShowToast(new ToastOptions
        {
            Title = title,
            Text = message,
            RenderStyle = style,
            ProviderName = "CompromisoProveedorAbonarToast"
        });
    }

    private static string GetTextOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string? NormalizeLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var normalized = url.Trim();
        return !normalized.StartsWith("/", StringComparison.Ordinal) || normalized.StartsWith("//", StringComparison.Ordinal)
            ? null
            : normalized;
    }

    private sealed record MetodoPagoItem(string Value, string Display);
}
```

> Verificado: `OrdenesPagoDirectoClient.GetByNumeroOrdenAsync` (L56) devuelve `OrdenPagoDirectoDetalleDto?` con `CuentaContableProveedor`/`Proveedor`/`CorrelativoProveedor`; `GetCuentasContraProcesamientoAsync` (L90) devuelve `List<CuentaContableLookupDto>` (usa `AccountId`/`Display`). Los toasts, `ErrorNotice`, `AccountFormatState`, `IToastNotificationService` siguen el patrón de `CompromisoProveedorProcesar.razor`.

**Paso 11.3 — Crear el `.razor.css`.** Copiar el contenido de `apc.Client/Pages/Proveedores/CompromisoProveedorProcesar.razor.css` (mismos `page-container`, `modern-card`, `summary-card`, `summary-strip`, `summary-pill`, `badge-status`, `section-header`, `total-bar`, `btn-icon`, `btn-delete`, media queries) y añadir al final, antes del bloque `@media`, las variantes de badge:

```css
.badge-status.pending {
    background-color: #fff3cd;
    color: #664d03;
}

.badge-status.partial {
    background-color: #cfe2ff;
    color: #084298;
}
```

> El look de la grilla `grid-modern` viene de `apc/wwwroot/css/siad-grid.css` (global), NO se redefine aquí. Los `btn-view`/`btn-delete` dentro del `DxGrid` se estilizan desde ese CSS global (scoped `.razor.css` no alcanza componentes hijos sin `::deep`).

**Paso 11.4 — Compilar `apc.Client`.**

Run: `dotnet build apc.Client/apc.Client.csproj -clp:ErrorsOnly`
Expected: `Build succeeded.` con `0 Error(s)`.

**Paso 11.5 — Verificación funcional (opcional).** `dotnet run --project apc/apc.csproj` y navegar a `/proveedores/compromisos-proveedor/<n>/abonar` de un compromiso con saldo > 0.
Expected: cabecera con badge de estado, tres `summary-card`, formulario con monto topado al saldo (`MaxValue`), grilla de historial `grid-modern`. "Registrar abono" deshabilitado hasta elegir monto, cuenta origen y método.

---

### Task 12: Botón "Abonar" en `CompromisosProveedorList.razor`

**Files:**
- Modify: `apc.Client/Pages/Proveedores/CompromisosProveedorList.razor` — columna Acciones (~L213-247; "Emitir pago" cierra en L236, "Eliminar" en L237) y bloque `@code` (`IrAProcesar` termina en L394).

**Paso 12.1 — Botón "Abonar"** entre "Emitir pago" (L236) y "Eliminar" (L237), dentro del `<div class="d-flex flex-wrap gap-2">` de la celda (la variable de la lambda es `item`, verificado L215):

```razor
                                        <button type="button"
                                                class="btn-icon btn-view"
                                                title="Abonos"
                                                @onclick="@(() => IrAAbonar(item.NumeroOrden))">
                                            <i class="bi bi-cash-coin"></i>
                                        </button>
```

**Paso 12.2 — Método `IrAAbonar`** tras `IrAProcesar` (L394). `GetCurrentRelativeUrl()` existe (L463):

```csharp
    private void IrAAbonar(int numeroOrden)
    {
        var returnUrl = Uri.EscapeDataString(GetCurrentRelativeUrl());
        Navigation.NavigateTo($"/proveedores/compromisos-proveedor/{numeroOrden}/abonar?returnUrl={returnUrl}");
    }
```

**Paso 12.3 — Compilar `apc.Client`.**

Run: `dotnet build apc.Client/apc.Client.csproj -clp:ErrorsOnly`
Expected: `Build succeeded.` con `0 Error(s)`.

**Paso 12.4 — Verificación funcional.** Con el portal en ejecución, abrir `/proveedores/compromisos-proveedor`, pulsar el ícono de abonos (`bi bi-cash-coin`).
Expected: navega a `/proveedores/compromisos-proveedor/<n>/abonar?returnUrl=...`; "Volver" regresa al listado.

---

### Task 13: Tests de integración `AbonosCompromisoTests`

**Files:**
- Modify: `SIAD.Tests/SIAD.Tests.csproj:10-20` (agregar `NSubstitute`)
- Create: `SIAD.Tests/Presupuesto/AbonosCompromisoTests.cs`

Requiere la tabla `prv_compromiso_abono` en la BD de test (mirror `siad_v3_restore`). El **ctor del servicio tiene 6 parámetros** (Task 7): el 6º (`IBanTransaccionesService`) se fabrica con NSubstitute. Los casos de escritura funcionan en el harness porque `RegistrarAbonoAsync`/`AnularAbonoAsync` **reutilizan la transacción ambiente** (Tasks 6 y 7). El método `CONTABLE` no toca banca, así que el `IBanTransaccionesService` mockeado no se invoca en estos casos.

**Verificado:** `IntegrationTestBase` expone `CompanyId`/`Connection`/`Transaction`/`Fixture` (patrón de `CompromisoTenancyTests.cs:29-38`). `ICurrentCompanyService.GetCompanyId()`. `prv_proveedor_cuenta_bancaria` **no** es tenant-scoped: PK `proveedor_cuenta_bancaria_id`, sin `company_id`. `con_plan_cuentas.status='ACTIVE'` (no `estado`).

**Paso 13.1 — Agregar NSubstitute** al primer `<ItemGroup>` de `PackageReference` de `SIAD.Tests.csproj`:

```xml
    <PackageReference Include="NSubstitute" Version="5.3.0" />
```

**Paso 13.2 — Restaurar.**

Run: `dotnet restore SIAD.Tests/SIAD.Tests.csproj`
Expected: `Restored ...SIAD.Tests.csproj`, 0 errores `NU1101`.

**Paso 13.3 — Crear `SIAD.Tests/Presupuesto/AbonosCompromisoTests.cs`** (8 casos). Ver el código completo en el paso 13.4; primero se escribe, luego se corre en rojo (API inexistente) y finalmente en verde.

**Paso 13.4 — Contenido del archivo de tests:**

```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Services.Presupuesto;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// Abonos parciales sobre prv_compromiso_hdr: saldo = monto - SUM(vigentes), estados 'V'/'A',
/// partida por abono, rechazo por exceso/metodo invalido, cierre (status_transacc) y anulacion.
/// Cada prueba siembra dentro de la transaccion del harness (BEGIN...ROLLBACK).
/// </summary>
[Collection("Postgres")]
public class AbonosCompromisoTests : IntegrationTestBase
{
    private const int OrdenBase = 970001;

    public AbonosCompromisoTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }

    private SiadDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        var context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        context.Database.UseTransaction(Transaction);
        return context;
    }

    private IOrdenesPagoDirectoService CreateService(SiadDbContext context)
    {
        // Ctor de 6 parametros (Task 7). Solo context + currentCompany son reales; el resto NSubstitute.
        var proveedores = Substitute.For<IProveedoresService>();
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var accountFormat = Substitute.For<IAccountFormatService>();
        var banTransacciones = Substitute.For<IBanTransaccionesService>();

        return new OrdenesPagoDirectoService(
            context,
            proveedores,
            new TestCurrentCompanyService(CompanyId),
            httpAccessor,
            accountFormat,
            banTransacciones);
    }

    // --- Siembra ---

    private async Task SeedCompromisoAsync(int numeroOrden, decimal monto, string? codProveedor = null)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor, status_transacc, anulado)
VALUES (@c, @n, now(), @m, 'abono test', @cp, FALSE, FALSE);

INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad,
     objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, @n, 'CP1', '01', '01', 'OG', 'CG', 'detalle abono', @m);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("m", monto);
        cmd.Parameters.AddWithValue("cp", (object?)codProveedor ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task MarkProcesadoLegacyAsync(int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
UPDATE public.prv_compromiso_hdr SET status_transacc = TRUE
 WHERE company_id = @c AND numero_orden = @n;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<(long debito, long credito)?> ResolveDosCuentasPostingAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT account_id FROM public.con_plan_cuentas
 WHERE company_id = @c AND allows_posting = TRUE AND status = 'ACTIVE'
 ORDER BY account_id LIMIT 2;";
        cmd.Parameters.AddWithValue("c", CompanyId);

        var ids = new List<long>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Add(reader.GetInt64(0));

        return ids.Count >= 2 ? (ids[0], ids[1]) : null;
    }

    // Fuerza tipo_cuenta='AHORRO' en una cuenta bancaria de proveedor (sin company_id; PK proveedor_cuenta_bancaria_id).
    private async Task<(long id, string codProveedor)?> ForzarCuentaProveedorAhorroAsync()
    {
        long id;
        string codProveedor;
        await using (var pick = Connection.CreateCommand())
        {
            pick.Transaction = Transaction;
            pick.CommandText = @"
SELECT proveedor_cuenta_bancaria_id, cod_proveedor
  FROM public.prv_proveedor_cuenta_bancaria
 ORDER BY proveedor_cuenta_bancaria_id LIMIT 1;";
            await using var reader = await pick.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;
            id = reader.GetInt64(0);
            codProveedor = reader.GetString(1);
        }

        await using var upd = Connection.CreateCommand();
        upd.Transaction = Transaction;
        upd.CommandText = @"
UPDATE public.prv_proveedor_cuenta_bancaria SET tipo_cuenta = 'AHORRO'
 WHERE proveedor_cuenta_bancaria_id = @id;";
        upd.Parameters.AddWithValue("id", id);
        await upd.ExecuteNonQueryAsync();

        return (id, codProveedor);
    }

    // --- Verificaciones ---

    private async Task<int> ContarAbonosAsync(int numeroOrden, string estado)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT count(*) FROM public.prv_compromiso_abono
 WHERE company_id = @c AND numero_orden = @n AND estado = @e;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("e", estado);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<bool> AbonoTienePartidaAsync(int numeroOrden, int numeroAbono)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT partida_id IS NOT NULL FROM public.prv_compromiso_abono
 WHERE company_id = @c AND numero_orden = @n AND numero_abono = @a;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("a", numeroAbono);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<bool> StatusTransaccAsync(int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT COALESCE(status_transacc, FALSE) FROM public.prv_compromiso_hdr
 WHERE company_id = @c AND numero_orden = @n;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private static AbonoCompromisoUpsertDto AbonoContable(decimal monto, long ctaDebito, long ctaCredito) => new()
    {
        Monto = monto,
        MetodoPago = OrdenPagoDirectoMetodoPago.Contable,
        Usuario = "tester",
        Fecha = DateTime.Now.Date,
        Lineas = new List<PartidaLineaOrdenPagoDto>
        {
            new() { CuentaId = ctaDebito, Descripcion = "abono debito",  Debito = monto, Credito = 0m },
            new() { CuentaId = ctaCredito, Descripcion = "abono credito", Debito = 0m,    Credito = monto },
        }
    };
```
Continuación del mismo archivo (los 8 casos, dentro de la clase):

```csharp
    // (1) Sin abonos: saldo == monto, no pagado.
    [SkippableFact]
    public async Task Saldo_SinAbonos_EsIgualAlMonto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 1;
        await SeedCompromisoAsync(orden, 1000m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);

        Assert.NotNull(saldo);
        Assert.Equal(1000m, saldo!.Monto);
        Assert.Equal(0m, saldo.Abonado);
        Assert.Equal(1000m, saldo.Saldo);
        Assert.False(saldo.Pagado);
        Assert.Empty(saldo.Abonos);
    }

    // (2) Abono parcial: baja el saldo, crea fila 'V' y su partida.
    [SkippableFact]
    public async Task AbonoParcial_BajaSaldo_CreaFilaVigenteYPartida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveDosCuentasPostingAsync();
        Skip.If(cuentas is null, "No hay >=2 cuentas allows_posting ACTIVE en el tenant de prueba.");

        const int orden = OrdenBase + 2;
        await SeedCompromisoAsync(orden, 1000m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(400m, cuentas!.Value.debito, cuentas.Value.credito), CancellationToken.None);
        Assert.True(res.Success, res.Message);
        Assert.Equal(1, res.NumeroAbono);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(400m, saldo!.Abonado);
        Assert.Equal(600m, saldo.Saldo);
        Assert.False(saldo.Pagado);

        Assert.Equal(1, await ContarAbonosAsync(orden, "V"));
        Assert.True(await AbonoTienePartidaAsync(orden, 1));
        Assert.False(await StatusTransaccAsync(orden));
    }

    // (3) Abono que excede el saldo: rechazado, sin fila.
    [SkippableFact]
    public async Task Abono_QueExcedeSaldo_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveDosCuentasPostingAsync();
        Skip.If(cuentas is null, "No hay >=2 cuentas allows_posting ACTIVE en el tenant de prueba.");

        const int orden = OrdenBase + 3;
        await SeedCompromisoAsync(orden, 500m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(500.01m, cuentas!.Value.debito, cuentas.Value.credito), CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
    }

    // (4) Cheque contra cuenta destino que no es de cheques: rechazado.
    [SkippableFact]
    public async Task Abono_Cheque_DesdeCuentaAhorro_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cta = await ForzarCuentaProveedorAhorroAsync();
        Skip.If(cta is null, "No hay cuentas bancarias de proveedor en el tenant de prueba.");

        const int orden = OrdenBase + 4;
        await SeedCompromisoAsync(orden, 1000m, codProveedor: cta!.Value.codProveedor);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new AbonoCompromisoUpsertDto
        {
            Monto = 200m,
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            CuentaDestinoProveedorId = cta.Value.id,
            Usuario = "tester",
            Fecha = DateTime.Now.Date,
            Lineas = new List<PartidaLineaOrdenPagoDto>()
        };

        var res = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
    }

    // (5) Abonar el 100%: status_transacc=TRUE y Pagado.
    [SkippableFact]
    public async Task AbonoTotal_MarcaPagado_YStatusTransacc()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveDosCuentasPostingAsync();
        Skip.If(cuentas is null, "No hay >=2 cuentas allows_posting ACTIVE en el tenant de prueba.");

        const int orden = OrdenBase + 5;
        await SeedCompromisoAsync(orden, 750m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(750m, cuentas!.Value.debito, cuentas.Value.credito), CancellationToken.None);
        Assert.True(res.Success, res.Message);
        Assert.True(res.Pagado);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(0m, saldo!.Saldo);
        Assert.True(saldo.Pagado);
        Assert.True(await StatusTransaccAsync(orden));
    }

    // (6) Anular el ultimo abono: 'A', restaura saldo, reabre Pagado.
    [SkippableFact]
    public async Task AnularUltimoAbono_LoPoneAnulado_RestauraSaldo_YReabrePagado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveDosCuentasPostingAsync();
        Skip.If(cuentas is null, "No hay >=2 cuentas allows_posting ACTIVE en el tenant de prueba.");

        const int orden = OrdenBase + 6;
        await SeedCompromisoAsync(orden, 300m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var reg = await service.RegistrarAbonoAsync(orden, AbonoContable(300m, cuentas!.Value.debito, cuentas.Value.credito), CancellationToken.None);
        Assert.True(reg.Success, reg.Message);
        Assert.True(await StatusTransaccAsync(orden));

        var anu = await service.AnularAbonoAsync(orden, numeroAbono: 1, new AnularOrdenPagoDirectoDto { Motivo = "prueba" }, CancellationToken.None);
        Assert.True(anu.Success, anu.Message);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(0m, saldo!.Abonado);
        Assert.Equal(300m, saldo.Saldo);
        Assert.False(saldo.Pagado);

        Assert.Equal(1, await ContarAbonosAsync(orden, "A"));
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
        Assert.False(await StatusTransaccAsync(orden));
    }

    // (7) No se puede anular un abono que no es el ultimo vigente.
    [SkippableFact]
    public async Task AnularAbono_QueNoEsElUltimoVigente_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveDosCuentasPostingAsync();
        Skip.If(cuentas is null, "No hay >=2 cuentas allows_posting ACTIVE en el tenant de prueba.");

        const int orden = OrdenBase + 7;
        await SeedCompromisoAsync(orden, 1000m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var a1 = await service.RegistrarAbonoAsync(orden, AbonoContable(200m, cuentas!.Value.debito, cuentas.Value.credito), CancellationToken.None);
        Assert.True(a1.Success, a1.Message);
        var a2 = await service.RegistrarAbonoAsync(orden, AbonoContable(300m, cuentas.Value.debito, cuentas.Value.credito), CancellationToken.None);
        Assert.True(a2.Success, a2.Message);

        var res = await service.AnularAbonoAsync(orden, numeroAbono: 1, new AnularOrdenPagoDirectoDto { Motivo = "x" }, CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(2, await ContarAbonosAsync(orden, "V"));
        Assert.Equal(0, await ContarAbonosAsync(orden, "A"));
    }

    // (8) Compat legacy: procesado sin abonos => saldo 0, no acepta abonos.
    [SkippableFact]
    public async Task Legacy_ProcesadoSinAbonos_SaldoCeroYNoAceptaAbonos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveDosCuentasPostingAsync();
        Skip.If(cuentas is null, "No hay >=2 cuentas allows_posting ACTIVE en el tenant de prueba.");

        const int orden = OrdenBase + 8;
        await SeedCompromisoAsync(orden, 1200m);
        await MarkProcesadoLegacyAsync(orden);

        await using var context = CreateContext();
        var service = CreateService(context);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(0m, saldo!.Saldo);
        Assert.True(saldo.Pagado);
        Assert.Empty(saldo.Abonos);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(100m, cuentas!.Value.debito, cuentas.Value.credito), CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
    }
}
```

**Paso 13.5 — Compilar en ROJO (API incompleta si se corre antes de tiempo).**

Run: `dotnet build SIAD.Tests/SIAD.Tests.csproj -clp:ErrorsOnly`
Expected (si faltan Tasks previas): `CS0246`/`CS1061` sobre `AbonoCompromisoUpsertDto`, `AbonoCompromisoResultadoDto`, `GetSaldoConAbonosAsync`, `RegistrarAbonoAsync`, `AnularAbonoAsync`, o el ctor de 6 args. No se corrigen los tests: se completan las tareas previas.

**Paso 13.6 — Compilar en VERDE** (Tasks 1-12 hechas).

Run: `dotnet build SIAD.Tests/SIAD.Tests.csproj -clp:ErrorsOnly`
Expected: `Build succeeded` / `0 Error(s)`.

**Paso 13.7 — Correr SIN `SIAD_TEST_DB` (todo salteado).**

Run: `$env:SIAD_TEST_DB=$null; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~AbonosCompromisoTests`
Expected: `Passed! - Failed: 0, Passed: 0, Skipped: 8, Total: 8`.

**Paso 13.8 — Correr contra el mirror en VERDE** (tabla `prv_compromiso_abono` ya aplicada en `siad_v3_restore`).

Run: `$env:SIAD_TEST_DB='<cadena_mirror>'; $env:SIAD_TEST_COMPANY_ID='2'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~AbonosCompromisoTests`
Expected: `Passed! - Failed: 0, Passed: 8, Skipped: 0` (o algunos `Skipped` si el tenant 2 no tiene >=2 cuentas `allows_posting`/`ACTIVE` o sin cuentas bancarias de proveedor). Cada test hace BEGIN...ROLLBACK; el mirror queda intacto salvo que `sp_registrar_partida_contable` use transacciones autónomas (ver caveat de `SIAD.Tests/README.md`).

---

## Orden de ejecución y verificación final

Ejecutar las tareas **en orden 1 → 13** (dependencias encadenadas). Resumen de compilación por tarea:

1. **Task 1** — script SQL (no compila; el usuario lo aplica vía @guardia-estructura-bd → mirror → SRV).
2. **Task 2** — entidad → `dotnet build SIAD.Core/SIAD.Core.csproj -clp:ErrorsOnly` → 0 Errores.
3. **Task 3** — DbSet+config → `dotnet build SIAD.Data/SIAD.Data.csproj -clp:ErrorsOnly` → 0 Errores.
4. **Task 4** — DTOs → `dotnet build SIAD.Core/SIAD.Core.csproj -clp:ErrorsOnly` → 0 Errores.
5. **Task 5** — `GetSaldoConAbonosAsync` → `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly` → 0 Errores.
6. **Task 6** — `RegistrarAbonoAsync` + calculador (TDD) → `dotnet test ... --filter "FullyQualifiedName~AbonoCompromisoCalculatorTests"` → Passed 4.
7. **Task 7** — `AnularAbonoAsync` (ctor 6 args) → `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → 0 Errores.
8. **Task 8** — comprobante PDF → `dotnet build SIAD.Reports/SIAD.Reports.csproj -clp:ErrorsOnly` → 0 Errores.
9. **Task 9** — endpoints → `dotnet build apc/apc.csproj -clp:ErrorsOnly` → 0 Errores.
10. **Task 10** — cliente → `dotnet build apc.Client/apc.Client.csproj -clp:ErrorsOnly` → 0 Errores.
11. **Task 11** — página → `dotnet build apc.Client/apc.Client.csproj -clp:ErrorsOnly` → 0 Errores.
12. **Task 12** — botón/navegación → `dotnet build apc.Client/apc.Client.csproj -clp:ErrorsOnly` → 0 Errores.
13. **Task 13** — tests de integración (requiere tabla en el mirror).

**Verificación final:**

```
dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly
```
Expected: `Build succeeded.` con `0 Error(s)` en toda la solución.

```
$env:SIAD_TEST_DB='<cadena_mirror>'; $env:SIAD_TEST_COMPANY_ID='2'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~AbonosCompromisoTests
```
Expected: `Passed! - Failed: 0` (los casos de escritura pueden quedar `Skipped` si falta reference-data en el tenant de prueba; nunca deben `Fail`). El calculador puro corre siempre:

```
dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~AbonoCompromisoCalculatorTests"
```
Expected: `Passed! - Failed: 0, Passed: 4`.

Sin pasos de commit: el usuario decide cuándo subir (commit/push/SRV/publicar).
