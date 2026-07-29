# Cheques: numeración por cuenta y bitácora — Plan de implementación

> **For Claude:** REQUIRED SUB-SKILL: usar la skill executing-plans para implementar este plan tarea por tarea.
> Diseño aprobado: [2026-07-21-cheques-numeracion-bitacora-design.md](2026-07-21-cheques-numeracion-bitacora-design.md)

**Goal:** Numeración automática de cheques por cuenta bancaria tipo CHEQUES (`proximo_cheque` + `cheque_maximo` nuevo) y bitácora `ban_cheque` de emisión/anulación, integrada en las 3 vías de emisión y en la anulación de movimientos.

**Architecture:** Servicio nuevo `ChequesService` (módulo Bancos) como punto único: emite dentro de la transacción del pago (lock `FOR UPDATE` sobre `ban_cuenta`, valida agotamiento, inserta `ban_cheque`, incrementa correlativo) y anula por `ban_kardex_id` desde el chokepoint `BanTransaccionesService.AnularMovimientoAsync`. La lógica pura de numeración se aísla en `ChequeNumeracionCalculator` (TDD sin BD). UI: campos en el formulario de cuenta + página nueva `/bancos/cheques` con el grid estándar.

**Tech Stack:** .NET 9, Blazor WASM, DevExpress 25.1.7, PostgreSQL (raw Npgsql + EF Core), xUnit.

---

## Reglas transversales

- **El SQL de la Task 1 lo aplica EL USUARIO** (mirror `siad_v3_restore` @localhost → SRV), pasando por la skill **@guardia-estructura-bd** (tarjeta verde: aditivo). Las tareas de código compilan sin el SQL aplicado; solo los tests de integración (Task 12) requieren la tabla en la BD de test.
- Convención de estado del cheque: `'E'` emitido / `'A'` anulado (NO invertida).
- `cheque_maximo = 0` ⇒ sin límite (no se valida agotamiento).
- Si `proximo_cheque <= 0` al emitir ⇒ se normaliza a 1.
- Todo mensaje de error de agotamiento: `"La cuenta bancaria agotó su numeración de cheques (máximo {max}). Actualice la numeración en la gestión de la cuenta."`
- Antes de tocar API de componentes DevExpress: consultar el MCP `dxdocs` (obligatorio, CLAUDE.md).

### Task 0: Verificación previa

**Paso 0.1** — Confirmar rama y estado: `git status` en `Cambios_almacen1.0` (hay cambios sin commitear de otra feature; NO tocarlos).

**Paso 0.2** — Compilación base: `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)` antes de empezar.

---

### Task 1: Script SQL `Database/2026-07-21_cheques_numeracion_bitacora.sql`

**Files:** Create: `Database/2026-07-21_cheques_numeracion_bitacora.sql`

**Paso 1.1** — Crear el archivo con este contenido completo:

```sql
-- =============================================================================
-- Bancos: numeracion de cheques por cuenta y bitacora de emision/anulacion
-- Fecha: 2026-07-21
-- Regla DB Mirror: aplicar tambien en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUE
-- Las cuentas tipo CHEQUES ya traen ban_cuenta.proximo_cheque (migrado de SIMAFI
-- ctacheques.ncheque el 2026-07-09) pero ningun flujo lo usa: hoy se paga con
-- metodo CHEQUE sin asignar numero. Se agrega:
--   1) ban_cuenta.cheque_maximo  -> ultimo numero autorizado del talonario
--                                    (0 = sin limite, no se valida agotamiento)
--   2) tabla ban_cheque          -> libro/bitacora: una fila por cheque emitido
--                                    o anulado, con numero unico por cuenta.
-- Lo consumen ChequesService.EmitirChequeAsync / AnularPorKardexAsync /
-- AnularSiguienteNumeroAsync (SIAD.Services/Bancos), enganchados en
-- OrdenesPagoDirectoService (procesar/abonar compromisos con metodo CHEQUE) y
-- BanTransaccionesService (transaccion manual con tipo emite_cheque='S';
-- anulacion de movimientos).
--
-- CRITERIO (definido con el usuario 2026-07-21): numeracion automatica no
-- editable al pagar; todas las vias de emision; anulacion automatica al
-- reversar el movimiento + anulacion manual de un numero (cheque danado).
--
-- ESTADO 'E'/'A' (convencion NO invertida):
--   'E' = EMITIDO   'A' = ANULADO
--
-- Cambio ADITIVO y reversible: una columna nueva con DEFAULT y una tabla nueva.
-- No altera datos existentes.
-- =============================================================================
BEGIN;

ALTER TABLE public.ban_cuenta
    ADD COLUMN IF NOT EXISTS cheque_maximo NUMERIC(28,0) NOT NULL DEFAULT 0;

COMMENT ON COLUMN public.ban_cuenta.cheque_maximo IS
    'Ultimo numero de cheque autorizado del talonario (0 = sin limite). Se valida contra proximo_cheque al emitir.';

CREATE TABLE IF NOT EXISTS public.ban_cheque (
    cheque_id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id           BIGINT        NOT NULL,
    banco_cuenta_id      BIGINT        NOT NULL,
    numero_cheque        NUMERIC(28,0) NOT NULL,
    fecha_emision        TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    monto                NUMERIC(15,2) NOT NULL DEFAULT 0,
    beneficiario         VARCHAR(200),
    concepto             VARCHAR(250),
    origen               VARCHAR(20)   NOT NULL,
    origen_documento     VARCHAR(50),
    ban_kardex_id        BIGINT,
    partida_id           BIGINT,
    ban_kardex_id_reverso BIGINT,
    estado               CHAR(1)       NOT NULL DEFAULT 'E',
    usuario_emision      VARCHAR(100)  NOT NULL,
    fecha_creacion       TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    motivo_anulacion     VARCHAR(250),
    usuario_anulacion    VARCHAR(100),
    fecha_anulacion      TIMESTAMP WITHOUT TIME ZONE,
    rowid                UUID          NOT NULL DEFAULT gen_random_uuid(),

    CONSTRAINT ck_ban_cheque_estado CHECK (estado IN ('E', 'A')),
    CONSTRAINT ck_ban_cheque_origen CHECK (origen IN ('PROCESAR', 'ABONO', 'TRANSACCION', 'MANUAL')),
    CONSTRAINT ck_ban_cheque_numero CHECK (numero_cheque > 0),

    -- RESTRICT: no se borra una cuenta con cheques registrados.
    -- Tenant-safe: el cheque vive SIEMPRE en la misma empresa que su cuenta
    -- (convencion del modulo bancos: FK compuesta contra la AK uq_ban_cuenta_company_id).
    CONSTRAINT fk_ban_cheque_cuenta
        FOREIGN KEY (company_id, banco_cuenta_id)
        REFERENCES public.ban_cuenta (company_id, banco_cuenta_id)
        ON DELETE RESTRICT,

    -- Numeros irrepetibles por cuenta (defensa en BD; el FOR UPDATE del
    -- servicio serializa, esto es el respaldo ante carreras).
    CONSTRAINT uq_ban_cheque_numero
        UNIQUE (company_id, banco_cuenta_id, numero_cheque)
);

CREATE INDEX IF NOT EXISTS ix_ban_cheque_cuenta_estado
    ON public.ban_cheque (company_id, banco_cuenta_id, estado);

-- Anulacion por reverso: localizar el cheque vigente de un movimiento.
CREATE INDEX IF NOT EXISTS ix_ban_cheque_kardex
    ON public.ban_cheque (company_id, ban_kardex_id);

COMMENT ON TABLE  public.ban_cheque IS
    'Libro/bitacora de cheques por cuenta bancaria. Una fila por cheque: emision (estado=''E'') y anulacion (estado=''A'', por reverso del movimiento o manual/danado). Numero unico por (company, cuenta).';
COMMENT ON COLUMN public.ban_cheque.origen IS
    'PROCESAR = procesar compromiso | ABONO = abono a compromiso | TRANSACCION = transaccion bancaria manual | MANUAL = numero anulado sin pago (cheque danado).';
COMMENT ON COLUMN public.ban_cheque.ban_kardex_id IS
    'Movimiento bancario (ban_kardex) que emitio el cheque. NULL solo en origen MANUAL.';
COMMENT ON COLUMN public.ban_cheque.ban_kardex_id_reverso IS
    'ban_kardex del reverso que anulo el cheque. NULL si esta vigente o si la anulacion fue manual.';

COMMIT;

-- =============================================================================
-- VERIFICACION (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columna nueva:
-- SELECT column_name, data_type, column_default FROM information_schema.columns
--  WHERE table_name='ban_cuenta' AND column_name='cheque_maximo';
-- 2) Tabla y constraints:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='ban_cheque'::regclass ORDER BY contype, conname;
--   -> ck_ban_cheque_estado(c), ck_ban_cheque_origen(c), ck_ban_cheque_numero(c),
--      fk_ban_cheque_cuenta(f), ban_cheque_pkey(p), uq_ban_cheque_numero(u)
-- 3) Indices:
-- SELECT indexname FROM pg_indexes WHERE tablename='ban_cheque' ORDER BY indexname;
-- 4) El CHECK de estado debe FALLAR:
-- INSERT INTO ban_cheque (company_id, banco_cuenta_id, numero_cheque, fecha_emision, origen, usuario_emision)
-- VALUES (2, 1, 1, now(), 'PROCESAR', 'test');  -- ok si la cuenta 1 existe; luego:
-- UPDATE ban_cheque SET estado='X' WHERE numero_cheque=1;  -- ERROR ck_ban_cheque_estado
-- =============================================================================
```

**Paso 1.2** — Verificar cabecera/idempotencia sin ejecutar:
Run: `Get-Content Database/2026-07-21_cheques_numeracion_bitacora.sql | Select-String -Pattern "IF NOT EXISTS","Regla DB Mirror","VERIFICACION"`
Expected: ≥ 4 líneas.

**Paso 1.3** — Presentar la tarjeta de @guardia-estructura-bd al usuario (aditivo/verde). **NO ejecutar el SQL**; recordar el flujo mirror → SRV.

---

### Task 2: Entidad `ban_cheque` + campo en `ban_cuenta`

**Files:**
- Create: `SIAD.Core/Entities/ban_cheque.cs`
- Modify: `SIAD.Core/Entities/ban_cuenta.cs:86` (tras `proximo_cheque`)

**Paso 2.1** — Crear `SIAD.Core/Entities/ban_cheque.cs`:

```csharp
using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class ban_cheque : ICompanyScopedEntity
{
    public long cheque_id { get; set; }

    public long company_id { get; set; }

    public long banco_cuenta_id { get; set; }

    public decimal numero_cheque { get; set; }

    public DateTime fecha_emision { get; set; }

    public decimal monto { get; set; }

    public string? beneficiario { get; set; }

    public string? concepto { get; set; }

    public string origen { get; set; } = null!;

    public string? origen_documento { get; set; }

    public long? ban_kardex_id { get; set; }

    public long? partida_id { get; set; }

    public long? ban_kardex_id_reverso { get; set; }

    public string estado { get; set; } = "E";

    public string usuario_emision { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public string? motivo_anulacion { get; set; }

    public string? usuario_anulacion { get; set; }

    public DateTime? fecha_anulacion { get; set; }

    public Guid? rowid { get; set; }
}
```

**Paso 2.2** — En `ban_cuenta.cs`, tras `public decimal proximo_cheque { get; set; }` (L86), agregar:

```csharp
    public decimal cheque_maximo { get; set; }
```

**Paso 2.3** — Compilar: `dotnet build SIAD.Core/SIAD.Core.csproj -clp:ErrorsOnly` → `0 Error(s)`.

> Nota scaffold: `ban_cuenta.cs` y `SiadDbContext.cs` son generados. Al refrescar el scaffold, incluir `ban_cheque` en la lista `-t` y re-aplicar estos cambios (readme §2.7).

---

### Task 3: DbSet y configuración en `SiadDbContext`

**Files:** Modify: `SIAD.Data/SiadDbContext.cs` (DbSet junto a los demás `ban_*`; config junto al bloque de `ban_cuenta`, ~L2540-2580)

**Paso 3.1** — Agregar el DbSet junto a los otros `ban_*` (buscar `public virtual DbSet<ban_cuenta>`):

```csharp
    public virtual DbSet<ban_cheque> ban_cheques { get; set; }
```

**Paso 3.2** — En el bloque de configuración de `ban_cuenta` (buscar `e.proximo_cheque).HasPrecision(28, 4)`, ~L2562), agregar debajo:

```csharp
            entity.Property(e => e.cheque_maximo).HasPrecision(28, 0).HasDefaultValue(0m);
```

**Paso 3.3** — Tras el cierre `});` del bloque de `ban_cuenta`, insertar el bloque nuevo:

```csharp
        modelBuilder.Entity<ban_cheque>(entity =>
        {
            entity.HasKey(e => e.cheque_id).HasName("ban_cheque_pkey");

            entity.ToTable("ban_cheque");

            // Filtro tenant y stamping de company_id: SiadDbContext.Tenancy.cs (ICompanyScopedEntity).
            entity.HasIndex(e => new { e.company_id, e.banco_cuenta_id, e.estado })
                .HasDatabaseName("ix_ban_cheque_cuenta_estado");
            entity.HasIndex(e => new { e.company_id, e.ban_kardex_id })
                .HasDatabaseName("ix_ban_cheque_kardex");
            entity.HasIndex(e => new { e.company_id, e.banco_cuenta_id, e.numero_cheque })
                .IsUnique()
                .HasDatabaseName("uq_ban_cheque_numero");

            entity.Property(e => e.cheque_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.numero_cheque).HasPrecision(28, 0);
            entity.Property(e => e.fecha_emision).HasColumnType("timestamp without time zone");
            entity.Property(e => e.monto).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.beneficiario).HasMaxLength(200);
            entity.Property(e => e.concepto).HasMaxLength(250);
            entity.Property(e => e.origen).HasMaxLength(20);
            entity.Property(e => e.origen_documento).HasMaxLength(50);
            entity.Property(e => e.estado).HasMaxLength(1).HasDefaultValue("E").IsFixedLength();
            entity.Property(e => e.usuario_emision).HasMaxLength(100);
            entity.Property(e => e.fecha_creacion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.motivo_anulacion).HasMaxLength(250);
            entity.Property(e => e.usuario_anulacion).HasMaxLength(100);
            entity.Property(e => e.fecha_anulacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });
```

**Paso 3.4** — `dotnet build SIAD.Data/SIAD.Data.csproj -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 4: Calculadora pura de numeración (TDD)

**Files:**
- Create: `SIAD.Services/Bancos/ChequeNumeracionCalculator.cs`
- Test: `SIAD.Tests/Bancos/ChequeNumeracionCalculatorTests.cs`

**Paso 4.1 — Test que falla primero.** Crear `SIAD.Tests/Bancos/ChequeNumeracionCalculatorTests.cs`:

```csharp
using SIAD.Services.Bancos;

namespace SIAD.Tests.Bancos;

public class ChequeNumeracionCalculatorTests
{
    [Fact]
    public void ProximoValido_AsignaEseNumero_YSiguienteEsMasUno()
    {
        var r = ChequeNumeracionCalculator.Compute(proximoCheque: 105m, chequeMaximo: 200m);
        Assert.False(r.Agotado);
        Assert.Equal(105m, r.NumeroAsignado);
        Assert.Equal(106m, r.SiguienteProximo);
    }

    [Fact]
    public void ProximoCeroONegativo_SeNormalizaAUno()
    {
        var r0 = ChequeNumeracionCalculator.Compute(0m, 0m);
        Assert.Equal(1m, r0.NumeroAsignado);
        Assert.Equal(2m, r0.SiguienteProximo);

        var rNeg = ChequeNumeracionCalculator.Compute(-5m, 0m);
        Assert.Equal(1m, rNeg.NumeroAsignado);
    }

    [Fact]
    public void MaximoCero_NoValidaAgotamiento()
    {
        var r = ChequeNumeracionCalculator.Compute(999999m, 0m);
        Assert.False(r.Agotado);
        Assert.Equal(999999m, r.NumeroAsignado);
    }

    [Fact]
    public void ProximoIgualAlMaximo_TodaviaEmite()
    {
        var r = ChequeNumeracionCalculator.Compute(200m, 200m);
        Assert.False(r.Agotado);
        Assert.Equal(200m, r.NumeroAsignado);
    }

    [Fact]
    public void ProximoSuperaElMaximo_Agotado()
    {
        var r = ChequeNumeracionCalculator.Compute(201m, 200m);
        Assert.True(r.Agotado);
    }

    [Fact]
    public void DecimalesDeSimafi_SeTruncanAEntero()
    {
        // proximo_cheque es NUMERIC(28,4) migrado de SIMAFI: puede traer decimales.
        var r = ChequeNumeracionCalculator.Compute(105.0000m, 200m);
        Assert.Equal(105m, r.NumeroAsignado);
        var r2 = ChequeNumeracionCalculator.Compute(105.7m, 200m);
        Assert.Equal(105m, r2.NumeroAsignado);
    }
}
```

**Paso 4.2** — Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~ChequeNumeracionCalculatorTests"`
Expected: FALLA compilación (`CS0246: ChequeNumeracionCalculator`).

**Paso 4.3 — Implementación mínima.** Crear `SIAD.Services/Bancos/ChequeNumeracionCalculator.cs`:

```csharp
namespace SIAD.Services.Bancos;

/// <summary>Resultado del calculo de numeracion de un cheque.</summary>
public readonly record struct ChequeNumeracionResult(decimal NumeroAsignado, decimal SiguienteProximo, bool Agotado);

/// <summary>
/// Logica pura (sin BD) de la numeracion de cheques por cuenta.
/// proximo_cheque llega como NUMERIC(28,4) migrado de SIMAFI (puede traer
/// decimales): se trunca a entero. proximo <= 0 se normaliza a 1.
/// chequeMaximo = 0 significa "sin limite" (no se valida agotamiento).
/// </summary>
public static class ChequeNumeracionCalculator
{
    public static ChequeNumeracionResult Compute(decimal proximoCheque, decimal chequeMaximo)
    {
        var numero = decimal.Truncate(proximoCheque);
        if (numero < 1m)
        {
            numero = 1m;
        }

        var maximo = decimal.Truncate(chequeMaximo);
        var agotado = maximo > 0m && numero > maximo;

        return new ChequeNumeracionResult(numero, numero + 1m, agotado);
    }
}
```

**Paso 4.4** — Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~ChequeNumeracionCalculatorTests"`
Expected: `Passed! - Failed: 0, Passed: 6`.

---

### Task 5: DTOs de cheques

**Files:**
- Create: `SIAD.Core/DTOs/Bancos/ChequesDtos.cs`
- Modify: `SIAD.Core/DTOs/Bancos/BancoCuentaCreateDto.cs:38` (tras `CtaConc`)
- Modify: `SIAD.Core/DTOs/Presupuesto/OrdenesPagoDirectoDtos.cs:210` (`OrdenPagoDirectoOperacionResultadoDto`)
- Modify: `SIAD.Core/DTOs/Presupuesto/AbonosCompromisoDtos.cs` (`AbonoCompromisoResultadoDto`)

**Paso 5.1** — Crear `SIAD.Core/DTOs/Bancos/ChequesDtos.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

/// <summary>Origen de un cheque en la bitacora ban_cheque.</summary>
public static class ChequeOrigen
{
    public const string Procesar = "PROCESAR";
    public const string Abono = "ABONO";
    public const string Transaccion = "TRANSACCION";
    public const string Manual = "MANUAL";
}

/// <summary>Fila de la bitacora de cheques (consulta).</summary>
public sealed class ChequeListItemDto
{
    public long ChequeId { get; set; }

    public long BancoCuentaId { get; set; }

    public string NumeroCuenta { get; set; } = string.Empty;

    public string BancoNombre { get; set; } = string.Empty;

    public decimal NumeroCheque { get; set; }

    public DateTime FechaEmision { get; set; }

    public decimal Monto { get; set; }

    public string? Beneficiario { get; set; }

    public string? Concepto { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string? OrigenDocumento { get; set; }

    public long? BanKardexId { get; set; }

    /// <summary>'E' emitido, 'A' anulado.</summary>
    public string Estado { get; set; } = string.Empty;

    public string UsuarioEmision { get; set; } = string.Empty;

    public string? MotivoAnulacion { get; set; }

    public string? UsuarioAnulacion { get; set; }

    public DateTime? FechaAnulacion { get; set; }
}

/// <summary>Filtros de la consulta de la bitacora.</summary>
public sealed class ChequeFilterDto
{
    public long? BancoCuentaId { get; set; }

    /// <summary>'E' | 'A' | null (todos).</summary>
    public string? Estado { get; set; }

    public DateTime? Desde { get; set; }

    public DateTime? Hasta { get; set; }

    public decimal? NumeroCheque { get; set; }
}

/// <summary>Estado de la numeracion de una cuenta (para "Se emitira el cheque N° X").</summary>
public sealed class ProximoChequeDto
{
    public long BancoCuentaId { get; set; }

    public decimal ProximoCheque { get; set; }

    public decimal ChequeMaximo { get; set; }

    public bool Agotado { get; set; }
}

/// <summary>Entrada de la anulacion manual de un numero (cheque danado).</summary>
public sealed class AnularNumeroChequeDto
{
    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(250, ErrorMessage = "El motivo no puede superar 250 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}
```

**Paso 5.2** — En `BancoCuentaCreateDto.cs`, tras `CtaConc` (L38), agregar:

```csharp

    /// <summary>Proximo numero de cheque a emitir (solo cuentas tipo CHEQUES).</summary>
    [Range(typeof(decimal), "0", "9999999999999999999999999999", ErrorMessage = "El proximo cheque no puede ser negativo.")]
    public decimal ProximoCheque { get; set; }

    /// <summary>Ultimo numero autorizado del talonario (0 = sin limite).</summary>
    [Range(typeof(decimal), "0", "9999999999999999999999999999", ErrorMessage = "El cheque maximo no puede ser negativo.")]
    public decimal ChequeMaximo { get; set; }
```

**Paso 5.3** — En `OrdenPagoDirectoOperacionResultadoDto` (`OrdenesPagoDirectoDtos.cs:202-211`), agregar antes del cierre:

```csharp

    /// <summary>Numeros de cheque emitidos por la operacion (vacio si el metodo no fue CHEQUE).</summary>
    public List<decimal> ChequesEmitidos { get; set; } = new();
```

**Paso 5.4** — En `AbonoCompromisoResultadoDto` (`AbonosCompromisoDtos.cs`), agregar antes del cierre:

```csharp

    /// <summary>Numero de cheque emitido por el abono (null si el metodo no fue CHEQUE).</summary>
    public decimal? NumeroCheque { get; set; }
```

**Paso 5.5** — `dotnet build SIAD.Core/SIAD.Core.csproj -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 6: `IChequesService` + `ChequesService`

**Files:**
- Create: `SIAD.Services/Bancos/IChequesService.cs`
- Create: `SIAD.Services/Bancos/ChequesService.cs`
- Modify: `SIAD.Services/ServiceRegistration.cs:141` (junto a `ICuentasBancosService`)

**Paso 6.1** — Crear `SIAD.Services/Bancos/IChequesService.cs`:

```csharp
using Npgsql;
using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IChequesService
{
    /// <summary>
    /// Emite (asigna) el siguiente numero de cheque de la cuenta DENTRO de la
    /// transaccion del llamador: FOR UPDATE sobre ban_cuenta, valida agotamiento
    /// contra cheque_maximo, inserta ban_cheque ('E') e incrementa proximo_cheque.
    /// Lanza InvalidOperationException si la numeracion esta agotada.
    /// </summary>
    Task<decimal> EmitirChequeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long bancoCuentaId,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        long? partidaId,
        string usuario,
        DateTime fechaEmision,
        CancellationToken ct = default);

    /// <summary>
    /// Marca como anulado ('A') el cheque vigente vinculado a un ban_kardex.
    /// No-op (retorna false) si el movimiento no tiene cheque (DEP/TRF/etc.).
    /// </summary>
    Task<bool> AnularPorKardexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long banKardexIdOriginal,
        long banKardexIdReverso,
        string motivo,
        string usuario,
        CancellationToken ct = default);

    /// <summary>
    /// Consume el siguiente numero de la cuenta y lo registra ya anulado
    /// (cheque danado): origen MANUAL, monto 0, sin movimiento bancario.
    /// Abre su propia transaccion.
    /// </summary>
    Task<decimal> AnularSiguienteNumeroAsync(
        long bancoCuentaId,
        string motivo,
        string usuario,
        CancellationToken ct = default);

    Task<ProximoChequeDto?> GetProximoAsync(long bancoCuentaId, CancellationToken ct = default);

    Task<IReadOnlyList<ChequeListItemDto>> BuscarAsync(ChequeFilterDto filtro, CancellationToken ct = default);
}
```

**Paso 6.2** — Crear `SIAD.Services/Bancos/ChequesService.cs`. Copiar el patrón de `EnsureCompanyId()` de `BanTransaccionesService` (misma carpeta) — verificar su implementación exacta antes de escribir:

```csharp
using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class ChequesService : IChequesService
{
    public const string EstadoEmitido = "E";
    public const string EstadoAnulado = "A";

    private const int MaxFilasBusqueda = 5000;

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public ChequesService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<decimal> EmitirChequeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long bancoCuentaId,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        long? partidaId,
        string usuario,
        DateTime fechaEmision,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(origen);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(usuario);

        var companyId = EnsureCompanyId();

        var numeracion = await LockAndComputeNumeracionAsync(connection, transaction, companyId, bancoCuentaId, ct);

        await InsertChequeRowAsync(
            connection, transaction, companyId, bancoCuentaId, numeracion.NumeroAsignado,
            fechaEmision, monto, beneficiario, concepto, origen, origenDocumento,
            banKardexId, partidaId, EstadoEmitido, usuario,
            motivoAnulacion: null, usuarioAnulacion: null, ct);

        await UpdateProximoChequeAsync(connection, transaction, companyId, bancoCuentaId, numeracion.SiguienteProximo, ct);

        return numeracion.NumeroAsignado;
    }

    public async Task<bool> AnularPorKardexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long banKardexIdOriginal,
        long banKardexIdReverso,
        string motivo,
        string usuario,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var companyId = EnsureCompanyId();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
UPDATE public.ban_cheque
   SET estado = @estado_anulado,
       motivo_anulacion = @motivo,
       usuario_anulacion = @usuario,
       fecha_anulacion = now(),
       ban_kardex_id_reverso = @kardex_reverso
 WHERE company_id = @company_id
   AND ban_kardex_id = @kardex_original
   AND estado = @estado_emitido;";
        command.Parameters.AddWithValue("estado_anulado", NpgsqlDbType.Char, EstadoAnulado);
        command.Parameters.AddWithValue("motivo", NpgsqlDbType.Varchar, Trunc(motivo, 250) ?? "Reverso del movimiento bancario");
        command.Parameters.AddWithValue("usuario", NpgsqlDbType.Varchar, Trunc(usuario, 100) ?? "sistema");
        command.Parameters.AddWithValue("kardex_reverso", NpgsqlDbType.Bigint, banKardexIdReverso);
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("kardex_original", NpgsqlDbType.Bigint, banKardexIdOriginal);
        command.Parameters.AddWithValue("estado_emitido", NpgsqlDbType.Char, EstadoEmitido);

        var rows = await command.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<decimal> AnularSiguienteNumeroAsync(
        long bancoCuentaId,
        string motivo,
        string usuario,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(usuario);

        var companyId = EnsureCompanyId();
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();

        // Reusar la transaccion ambiente (tests) o abrir una propia (produccion).
        var ambient = _context.Database.CurrentTransaction;
        var ownsConnection = false;
        NpgsqlTransaction tx;
        var ownsTx = false;
        if (ambient is not null)
        {
            tx = (NpgsqlTransaction)Microsoft.EntityFrameworkCore.Storage.TransactionExtensions.GetDbTransaction(ambient);
        }
        else
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                ownsConnection = true;
            }
            tx = await connection.BeginTransactionAsync(ct);
            ownsTx = true;
        }

        try
        {
            var numeracion = await LockAndComputeNumeracionAsync(connection, tx, companyId, bancoCuentaId, ct);

            await InsertChequeRowAsync(
                connection, tx, companyId, bancoCuentaId, numeracion.NumeroAsignado,
                fechaEmision: DateTime.Now, monto: 0m, beneficiario: null,
                concepto: "Anulacion manual de numero de cheque",
                origen: ChequeOrigen.Manual, origenDocumento: null,
                banKardexId: null, partidaId: null, estado: EstadoAnulado, usuario,
                motivoAnulacion: Trunc(motivo, 250), usuarioAnulacion: Trunc(usuario, 100), ct);

            await UpdateProximoChequeAsync(connection, tx, companyId, bancoCuentaId, numeracion.SiguienteProximo, ct);

            if (ownsTx) await tx.CommitAsync(ct);
            return numeracion.NumeroAsignado;
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

    public async Task<ProximoChequeDto?> GetProximoAsync(long bancoCuentaId, CancellationToken ct = default)
    {
        // Filtro global multi-tenant aplicado por el contexto.
        var cuenta = await _context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.banco_cuenta_id == bancoCuentaId)
            .Select(c => new { c.banco_cuenta_id, c.proximo_cheque, c.cheque_maximo })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null)
        {
            return null;
        }

        var numeracion = ChequeNumeracionCalculator.Compute(cuenta.proximo_cheque, cuenta.cheque_maximo);
        return new ProximoChequeDto
        {
            BancoCuentaId = cuenta.banco_cuenta_id,
            ProximoCheque = numeracion.NumeroAsignado,
            ChequeMaximo = decimal.Truncate(cuenta.cheque_maximo),
            Agotado = numeracion.Agotado
        };
    }

    public async Task<IReadOnlyList<ChequeListItemDto>> BuscarAsync(
        ChequeFilterDto filtro,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var query =
            from ch in _context.ban_cheques.AsNoTracking()
            join cu in _context.ban_cuenta.AsNoTracking() on ch.banco_cuenta_id equals cu.banco_cuenta_id
            join bb in _context.ban_bancos.AsNoTracking() on cu.ban_banco_id equals bb.ban_banco_id into bbj
            from bb in bbj.DefaultIfEmpty()
            select new { ch, cu, bb };

        if (filtro.BancoCuentaId is > 0)
        {
            query = query.Where(x => x.ch.banco_cuenta_id == filtro.BancoCuentaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            var estado = filtro.Estado.Trim().ToUpperInvariant();
            query = query.Where(x => x.ch.estado == estado);
        }

        if (filtro.Desde.HasValue)
        {
            var desde = filtro.Desde.Value.Date;
            query = query.Where(x => x.ch.fecha_emision >= desde);
        }

        if (filtro.Hasta.HasValue)
        {
            var hasta = filtro.Hasta.Value.Date.AddDays(1);
            query = query.Where(x => x.ch.fecha_emision < hasta);
        }

        if (filtro.NumeroCheque is > 0)
        {
            query = query.Where(x => x.ch.numero_cheque == filtro.NumeroCheque.Value);
        }

        return await query
            .OrderByDescending(x => x.ch.fecha_emision)
            .ThenByDescending(x => x.ch.numero_cheque)
            .Take(MaxFilasBusqueda)
            .Select(x => new ChequeListItemDto
            {
                ChequeId = x.ch.cheque_id,
                BancoCuentaId = x.ch.banco_cuenta_id,
                NumeroCuenta = x.cu.numero_cuenta,
                BancoNombre = x.bb != null ? x.bb.nombre : string.Empty,
                NumeroCheque = x.ch.numero_cheque,
                FechaEmision = x.ch.fecha_emision,
                Monto = x.ch.monto,
                Beneficiario = x.ch.beneficiario,
                Concepto = x.ch.concepto,
                Origen = x.ch.origen,
                OrigenDocumento = x.ch.origen_documento,
                BanKardexId = x.ch.ban_kardex_id,
                Estado = x.ch.estado,
                UsuarioEmision = x.ch.usuario_emision,
                MotivoAnulacion = x.ch.motivo_anulacion,
                UsuarioAnulacion = x.ch.usuario_anulacion,
                FechaAnulacion = x.ch.fecha_anulacion
            })
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<ChequeNumeracionResult> LockAndComputeNumeracionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT COALESCE(proximo_cheque, 0), COALESCE(cheque_maximo, 0)
  FROM public.ban_cuenta
 WHERE company_id = @company_id AND banco_cuenta_id = @banco_cuenta_id
 FOR UPDATE;";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);

        decimal proximo = 0m, maximo = 0m;
        var encontrada = false;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                encontrada = true;
                proximo = reader.GetDecimal(0);
                maximo = reader.GetDecimal(1);
            }
        }

        if (!encontrada)
        {
            throw new InvalidOperationException($"La cuenta bancaria {bancoCuentaId} no existe.");
        }

        var numeracion = ChequeNumeracionCalculator.Compute(proximo, maximo);
        if (numeracion.Agotado)
        {
            throw new InvalidOperationException(
                $"La cuenta bancaria agotó su numeración de cheques (máximo {decimal.Truncate(maximo):N0}). " +
                "Actualice la numeración en la gestión de la cuenta.");
        }

        return numeracion;
    }

    private static async Task InsertChequeRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        decimal numeroCheque,
        DateTime fechaEmision,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        long? partidaId,
        string estado,
        string usuario,
        string? motivoAnulacion,
        string? usuarioAnulacion,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO public.ban_cheque
    (company_id, banco_cuenta_id, numero_cheque, fecha_emision, monto, beneficiario,
     concepto, origen, origen_documento, ban_kardex_id, partida_id, estado,
     usuario_emision, motivo_anulacion, usuario_anulacion, fecha_anulacion)
VALUES
    (@company_id, @banco_cuenta_id, @numero_cheque, @fecha_emision, @monto, @beneficiario,
     @concepto, @origen, @origen_documento, @ban_kardex_id, @partida_id, @estado,
     @usuario_emision, @motivo_anulacion, @usuario_anulacion,
     CASE WHEN @estado = 'A' THEN now() ELSE NULL END);";

        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        command.Parameters.AddWithValue("numero_cheque", NpgsqlDbType.Numeric, numeroCheque);
        command.Parameters.AddWithValue("fecha_emision", NpgsqlDbType.Timestamp, fechaEmision);
        command.Parameters.AddWithValue("monto", NpgsqlDbType.Numeric, monto);
        command.Parameters.Add(new NpgsqlParameter("beneficiario", NpgsqlDbType.Varchar) { Value = (object?)Trunc(beneficiario, 200) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("concepto", NpgsqlDbType.Varchar) { Value = (object?)Trunc(concepto, 250) ?? DBNull.Value });
        command.Parameters.AddWithValue("origen", NpgsqlDbType.Varchar, origen);
        command.Parameters.Add(new NpgsqlParameter("origen_documento", NpgsqlDbType.Varchar) { Value = (object?)Trunc(origenDocumento, 50) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("ban_kardex_id", NpgsqlDbType.Bigint) { Value = banKardexId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("partida_id", NpgsqlDbType.Bigint) { Value = partidaId ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("estado", NpgsqlDbType.Char, estado);
        command.Parameters.AddWithValue("usuario_emision", NpgsqlDbType.Varchar, Trunc(usuario, 100) ?? "sistema");
        command.Parameters.Add(new NpgsqlParameter("motivo_anulacion", NpgsqlDbType.Varchar) { Value = (object?)motivoAnulacion ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("usuario_anulacion", NpgsqlDbType.Varchar) { Value = (object?)usuarioAnulacion ?? DBNull.Value });

        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateProximoChequeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        decimal siguiente,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
UPDATE public.ban_cuenta
   SET proximo_cheque = @siguiente
 WHERE company_id = @company_id AND banco_cuenta_id = @banco_cuenta_id;";
        command.Parameters.AddWithValue("siguiente", NpgsqlDbType.Numeric, siguiente);
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string? Trunc(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }

    private long EnsureCompanyId()
    {
        // Copiar el cuerpo EXACTO del helper EnsureCompanyId() de BanTransaccionesService
        // (misma carpeta) para resolver el tenant desde ICurrentCompanyService.
        throw new NotImplementedException("Reemplazar por el patron de BanTransaccionesService.EnsureCompanyId()");
    }
}
```

> Verificaciones para el ejecutor: (a) nombre real del DbSet de bancos (`ban_bancos` o `ban_banco`) y de la propiedad nombre de `ban_banco` — ajustar `BuscarAsync`; (b) cuerpo real de `EnsureCompanyId()` en `BanTransaccionesService` — reemplazar el `NotImplementedException`; (c) `GetDbTransaction` viene de `Microsoft.EntityFrameworkCore.Storage` (`using` + llamada de extensión normal `ambient.GetDbTransaction()` es lo idiomático — usar esa forma).

**Paso 6.3** — Registrar en `ServiceRegistration.cs` junto a `ICuentasBancosService` (L141):

```csharp
        services.AddScoped<IChequesService, ChequesService>();
```

**Paso 6.4** — `dotnet build SIAD.Services/SIAD.Services.csproj -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 7: Integración en `OrdenesPagoDirectoService` (procesar + abonar)

**Files:** Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs`

**Paso 7.1** — Inyectar `IChequesService`: agregar `using SIAD.Services.Bancos;` si falta, campo `private readonly IChequesService _cheques;`, parámetro de ctor y asignación (patrón del resto de dependencias del ctor).

**Paso 7.2** — `RegisterLinkedBankTransactionsAsync` (L1946): agregar dos parámetros al final de la firma, antes de `CancellationToken ct`:

```csharp
        string? beneficiario,
        ICollection<decimal>? chequesEmitidos,
```

Dentro del bucle (tras `kardexIds.Add(kardexId);`, L2029), agregar:

```csharp
            if (string.Equals(metodoPago, OrdenPagoDirectoMetodoPago.Cheque, StringComparison.OrdinalIgnoreCase))
            {
                var numeroCheque = await _cheques.EmitirChequeAsync(
                    connection, transaction, grupo.Key,
                    monto: grupo.Sum(x => x.Debit),
                    beneficiario, concepto: descripcionMovimiento,
                    origen: origenCheque, origenDocumento: $"OPD-{numeroOrden}",
                    banKardexId: kardexId, partidaId: partidaId,
                    usuario, fechaEmision: fechaOrden, ct);
                chequesEmitidos?.Add(numeroCheque);
            }
```

donde `origenCheque` es otro parámetro nuevo `string origenCheque` (valores `ChequeOrigen.Procesar` / `ChequeOrigen.Abono`; agregar `using SIAD.Core.DTOs.Bancos;` si falta).

**Paso 7.3** — Mismo hook en `RegisterLinkedBankMovementsGeneralAsync` (L2041): mismos 3 parámetros nuevos; dentro del bucle tras `kardexIds.Add(kardexId);` (L2112), con `monto: lineaBanco.Credito` y `banKardexId: kardexId`.

**Paso 7.4** — Actualizar los llamadores (la compilación los delata: `CS7036`):
- `MarkAsProcessedAsync`: crear `var chequesEmitidos = new List<decimal>();` antes de las llamadas; pasar `beneficiario: orden.nombre_proveedor` (agregar `nombre_proveedor` al `Select` anónimo de la orden si no está), `origenCheque: ChequeOrigen.Procesar`, `chequesEmitidos`. Al armar el resultado: `resultado.ChequesEmitidos = chequesEmitidos;` y si hay cheques, anexar al `Message`: `$" Cheque(s) emitido(s): {string.Join(", ", chequesEmitidos.Select(c => c.ToString("N0")))}."`.
- `RegistrarAbonoAsync`: ídem con `origenCheque: ChequeOrigen.Abono`; al armar `AbonoCompromisoResultadoDto`: `NumeroCheque = chequesEmitidos.Count > 0 ? chequesEmitidos[0] : null` y anexar al mensaje `$" Se emitió el cheque N° {numero:N0}."`.

**Paso 7.5** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

> La anulación del abono NO se toca aquí: `AnularAbonoAsync` delega en `IBanTransaccionesService.AnularMovimientoAsync`, que se engancha en la Task 8.

---

### Task 8: Integración en `BanTransaccionesService` (transacción manual + anulación)

**Files:** Modify: `SIAD.Services/Bancos/BanTransaccionesService.cs`

**Paso 8.1** — Inyectar `IChequesService _cheques` en el ctor (L22-32).

**Paso 8.2** — `RegistrarMovimientoAsync` (L401): tras resolver la moneda (L485) y ANTES de `RegistrarPartidaContableAsync`, cargar la config del tipo:

```csharp
        var tipoEmiteCheque = await context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.tipo_transaccion == idTipoTransaccion.Trim())
            .Select(t => t.emite_cheque)
            .FirstOrDefaultAsync(ct);
        var emiteCheque = tipoEmiteCheque is not null &&
            (tipoEmiteCheque == "S" || tipoEmiteCheque == "Y" || tipoEmiteCheque == "1" ||
             tipoEmiteCheque == "T" || tipoEmiteCheque.ToUpperInvariant() == "TRUE");
```

**Paso 8.3** — Envolver el bloque del SP kardex (L509-557) en una transacción explícita para que kardex + vínculo + cheque sean atómicos:
- Tras abrir la conexión (L505): `await using var dbTransaction = await connection.BeginTransactionAsync(ct);`
- En el comando del SP: `command.Transaction = dbTransaction;`
- En `VincularPartidaEnKardexAsync(...)`: pasar `dbTransaction` (el parámetro opcional ya existe — ver llamada de `AnularMovimientoAsync` L1662-1668).
- Tras el vínculo y antes del `return`:

```csharp
            if (emiteCheque)
            {
                await _cheques.EmitirChequeAsync(
                    connection, dbTransaction, bancoCuentaId,
                    monto: totalContra, beneficiario: descripcion.Trim(),
                    concepto: descripcion.Trim(),
                    origen: ChequeOrigen.Transaccion,
                    origenDocumento: referencia?.Trim(),
                    banKardexId: kardexId, partidaId: partidaId,
                    usuario: usuario.Trim(),
                    fechaEmision: fechaMovimiento.ToDateTime(TimeOnly.MinValue), ct);
            }

            await dbTransaccion.CommitAsync(ct);   // ojo: nombre real de la variable
```

(agregar `using SIAD.Core.DTOs.Bancos;` si falta — ya está, L8).

**Paso 8.4** — `AnularMovimientoAsync` (L1564): tras el bloque de la partida reversa (L1647-1670) y ANTES de `await dbTransaction.CommitAsync(ct);` (L1672):

```csharp
            await _cheques.AnularPorKardexAsync(
                connection, dbTransaction, banKardexIdOriginal, kardexId,
                motivoNormalizado, usuario.Trim(), ct);
```

**Paso 8.5** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

> Nota: `sp_ban_kardex_registrar_movimiento` ya corre dentro de transacción explícita en `OrdenesPagoDirectoService.RegisterLinkedBankMovementAsync` (L2133-2164), así que envolverlo aquí es seguro (no hace COMMIT interno).

---

### Task 9: Cuenta bancaria — servicio y formulario

**Files:**
- Modify: `SIAD.Services/Bancos/CuentasBancosService.cs` (`CreateAsync` L447-466, `UpdateAsync` L502-514, `MapToEditDto` L739-760)
- Modify: `apc.Client/Pages/Bancos/CuentasBancosFormModal.razor` (tras el bloque Saldo actual, L86)

**Paso 9.1** — En `CuentasBancosService`, agregar un helper y la validación:

```csharp
    private static (decimal Proximo, decimal Maximo) NormalizeNumeracionCheques(decimal proximo, decimal maximo)
    {
        proximo = decimal.Truncate(proximo);
        maximo = decimal.Truncate(maximo);
        if (proximo < 0 || maximo < 0)
        {
            throw new ArgumentException("La numeración de cheques no puede ser negativa.");
        }
        if (maximo > 0 && proximo > maximo)
        {
            throw new ArgumentException("El próximo cheque no puede superar el cheque máximo del talonario.");
        }
        return (proximo, maximo);
    }
```

- `CreateAsync`: antes del `new ban_cuenta { ... }` → `var (proximoCheque, chequeMaximo) = NormalizeNumeracionCheques(dto.ProximoCheque, dto.ChequeMaximo);` y en el objeto: `proximo_cheque = proximoCheque, cheque_maximo = chequeMaximo,`.
- `UpdateAsync`: ídem, asignando `entity.proximo_cheque` / `entity.cheque_maximo`.
- `MapToEditDto`: agregar `ProximoCheque = decimal.Truncate(entity.proximo_cheque), ChequeMaximo = decimal.Truncate(entity.cheque_maximo),`.

**Paso 9.2** — En `CuentasBancosFormModal.razor`, tras el `DxFormLayoutItem` de "Saldo actual" (L86), agregar (visibles solo para cuentas de cheques):

```razor
                        @if (EsCuentaCheques)
                        {
                            <DxFormLayoutItem Caption="Próximo cheque" ColSpanMd="4" ColSpanSm="12">
                                <DxSpinEdit TValue="decimal" @bind-Value="EditModel.ProximoCheque"
                                            Min="0m" Increment="1m" Decimals="0"
                                            DisplayFormat="N0"
                                            SpinButtonsVisible="true" />
                                <ValidationMessage For="@(() => EditModel.ProximoCheque)" />
                            </DxFormLayoutItem>
                            <DxFormLayoutItem Caption="Cheque máximo (0 = sin límite)" ColSpanMd="4" ColSpanSm="12">
                                <DxSpinEdit TValue="decimal" @bind-Value="EditModel.ChequeMaximo"
                                            Min="0m" Increment="1m" Decimals="0"
                                            DisplayFormat="N0"
                                            SpinButtonsVisible="true" />
                                <ValidationMessage For="@(() => EditModel.ChequeMaximo)" />
                            </DxFormLayoutItem>
                        }
```

y en el `@code` del componente:

```csharp
    private bool EsCuentaCheques =>
        EditModel?.TipoCuenta?.Contains("CHEQ", StringComparison.OrdinalIgnoreCase) == true;
```

**Paso 9.3** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 10: Controller y cliente HTTP

**Files:**
- Create: `apc/Controllers/Bancos/ChequesController.cs`
- Create: `apc.Client/Services/Bancos/ChequesClient.cs`
- Modify: `apc.Client/CommonServices.cs:73` (junto a `CuentasBancosClient`)

**Paso 10.1** — Crear `apc/Controllers/Bancos/ChequesController.cs` (espejar el estilo de `CuentasBancosController`: resolución de usuario, manejo de `ArgumentException`/`InvalidOperationException` → 400):

```csharp
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/cheques")]
[ModuleAuthorize(PermissionModules.Bancos)]
public class ChequesController : ControllerBase
{
    private readonly IChequesService _cheques;

    public ChequesController(IChequesService cheques)
    {
        _cheques = cheques;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChequeListItemDto>>> Buscar(
        [FromQuery] long? bancoCuentaId,
        [FromQuery] string? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] decimal? numeroCheque,
        CancellationToken ct)
    {
        var filtro = new ChequeFilterDto
        {
            BancoCuentaId = bancoCuentaId,
            Estado = estado,
            Desde = desde,
            Hasta = hasta,
            NumeroCheque = numeroCheque
        };
        return Ok(await _cheques.BuscarAsync(filtro, ct));
    }

    [HttpGet("proximo/{bancoCuentaId:long}")]
    public async Task<ActionResult<ProximoChequeDto>> GetProximo(long bancoCuentaId, CancellationToken ct)
    {
        var proximo = await _cheques.GetProximoAsync(bancoCuentaId, ct);
        return proximo is null ? NotFound() : Ok(proximo);
    }

    [HttpPost("{bancoCuentaId:long}/anular-siguiente")]
    public async Task<ActionResult<decimal>> AnularSiguiente(
        long bancoCuentaId,
        [FromBody] AnularNumeroChequeDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User.Identity?.Name ?? "sistema";
        try
        {
            var numero = await _cheques.AnularSiguienteNumeroAsync(bancoCuentaId, dto.Motivo, usuario, ct);
            return Ok(numero);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
```

> Verificar el namespace real de `ModuleAuthorize` (`apc/Security/ModuleAuthorizeAttribute.cs`) y el patrón de error de los controladores vecinos (algunos devuelven `Problem(...)`); ajustar para ser consistente con `CuentasBancosController`.

**Paso 10.2** — Crear `apc.Client/Services/Bancos/ChequesClient.cs` (espejar `CuentasBancosClient`: `HttpClient` inyectado, extensiones `*WithAuthCheck` de `apc.Client/Services/HttpClientExtensions.cs`):

```csharp
using System.Net.Http.Json;
using SIAD.Core.DTOs.Bancos;

namespace apc.Client.Services.Bancos;

public class ChequesClient
{
    private readonly HttpClient _http;

    public ChequesClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ChequeListItemDto>> BuscarAsync(
        long? bancoCuentaId, string? estado, DateTime? desde, DateTime? hasta, decimal? numeroCheque,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (bancoCuentaId is > 0) query.Add($"bancoCuentaId={bancoCuentaId}");
        if (!string.IsNullOrWhiteSpace(estado)) query.Add($"estado={Uri.EscapeDataString(estado)}");
        if (desde.HasValue) query.Add($"desde={desde.Value:yyyy-MM-dd}");
        if (hasta.HasValue) query.Add($"hasta={hasta.Value:yyyy-MM-dd}");
        if (numeroCheque is > 0) query.Add($"numeroCheque={numeroCheque}");
        var url = "api/bancos/cheques" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);
        return await _http.GetFromJsonAsyncWithAuthCheck<List<ChequeListItemDto>>(url, ct) ?? new();
    }

    public Task<ProximoChequeDto?> GetProximoAsync(long bancoCuentaId, CancellationToken ct = default)
        => _http.GetFromJsonAsyncWithAuthCheck<ProximoChequeDto?>($"api/bancos/cheques/proximo/{bancoCuentaId}", ct);

    public async Task<(bool Success, decimal Numero, string? Error)> AnularSiguienteAsync(
        long bancoCuentaId, string motivo, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsyncWithAuthCheck(
            $"api/bancos/cheques/{bancoCuentaId}/anular-siguiente",
            new AnularNumeroChequeDto { Motivo = motivo }, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var msg = await resp.ObtenerMensajeErrorAsync();
            return (false, 0m, msg);
        }
        var numero = await resp.Content.ReadFromJsonAsync<decimal>(cancellationToken: ct);
        return (true, numero, null);
    }
}
```

> Verificar firmas exactas de las extensiones en `HttpClientExtensions.cs` (nombres de parámetros/overloads) y ajustar.

**Paso 10.3** — Registrar en `CommonServices.cs` junto a `CuentasBancosClient` (L73): `services.AddScoped<ChequesClient>();` (seguro en ambos hosts: solo HttpClient).

**Paso 10.4** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 11: Página `/bancos/cheques` + menú

**Files:**
- Create: `apc.Client/Pages/Bancos/ChequesList.razor` (+ `.razor.css` si hace falta algo específico)
- Modify: `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs:390-396`

**Paso 11.1** — Leer ANTES: `.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md` y la referencia `apc.Client/Pages/Clientes/ClientesList.razor`. La página nueva SIGUE el estándar (siad-grid.css global; el `.razor.css` solo lleva lo específico), aunque el resto del módulo Bancos aún no esté migrado.

**Paso 11.2** — Crear `ChequesList.razor` modelada sobre `ClientesList.razor`:
- `@page "/bancos/cheques"`, `@attribute [Authorize(Policy = PermissionNames.Bancos.View)]`.
- `TenantState.EnsureCompanyAsync()` antes de cargar datos (regla CLAUDE.md).
- Toolbar de filtros: combo Banco (`BancosClient`), combo Cuenta (solo tipo con "CHEQ", vía `CuentasBancosClient` — verificar qué lookup expone), combo Estado (Todos/Emitidos/Anulados), rango de fechas, número de cheque; botón Buscar.
- `DxGrid` estándar con columnas: N° cheque (N0), Fecha, Banco, Cuenta, Beneficiario, Concepto, Monto (N2), Origen, Estado (badge: Emitido verde / Anulado rojo, patrón de badges de `BitacoraMaestrosList.razor`), Usuario.
- Popup de detalle (datos de anulación: motivo, usuario, fecha, kardex de reverso).
- Botón "Anular siguiente número" (habilitado al seleccionar cuenta): popup con `DxMemo` de motivo obligatorio → `ChequesClient.AnularSiguienteAsync`; refresca el grid y muestra el número anulado.
- Cabecera de la página muestra "Próximo cheque: N° X — Máximo: Y" de la cuenta seleccionada (`GetProximoAsync`), con aviso rojo si `Agotado`.

**Paso 11.3** — En `SidebarNavigationDefinition.cs`: agregar a `MatchPrefixes` del grupo `cont-bancos` (L390) el prefijo `"/bancos/cheques"`, y el hijo (tras `bn-transacciones`, L394):

```csharp
                        new SidebarNavItem { Id = "bn-cheques", Text = "Cheques emitidos", NavigateUrl = "/bancos/cheques", MatchPrefixes = ["/bancos/cheques"], IconCssClass = "bi bi-card-checklist" },
```

**Paso 11.4** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 12: Número de cheque en las pantallas de pago

**Files:**
- Modify: `apc.Client/Pages/Proveedores/CompromisoProveedorProcesar.razor` (bloque "Medio de pago" L176-237, `@code`)
- Modify: `apc.Client/Pages/Proveedores/CompromisoProveedorAbonar.razor` (formulario L103-152, `@code`)

**Paso 12.1** — En ambas páginas, inyectar `ChequesClient`. Cuando el método seleccionado sea `CHEQUE` y haya cuenta bancaria seleccionada, llamar `GetProximoAsync(bancoCuentaId)` y mostrar bajo el combo:
- Normal: `Se emitirá el cheque N° {ProximoCheque:N0}.` (texto informativo, clase muted).
- `Agotado == true`: alerta roja `La numeración de cheques de esta cuenta está agotada (máximo {ChequeMaximo:N0}).` y deshabilitar el botón de confirmar mientras el método sea CHEQUE.

Refrescar al cambiar cuenta o método (en los `SelectedItemChanged`/`ValueChanged` existentes).

**Paso 12.2** — Mensajes de éxito: los servicios ya anexan el número al `Message` (Task 7); verificar que ambas páginas muestran `resultado.Message` tal cual (no reconstruyen el texto). Si construyen su propio texto, incorporar `ChequesEmitidos`/`NumeroCheque` del DTO.

**Paso 12.3** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

---

### Task 13: Tests de integración

**Files:** Create: `SIAD.Tests/Bancos/ChequesServiceTests.cs`

**Requisito:** SQL de la Task 1 aplicado en el **mirror** y `SIAD_TEST_DB` apuntando ahí (nunca prod). Sin la variable, los tests quedan `Skipped` (patrón del harness existente).

**Paso 13.1** — Estudiar el harness de `SIAD.Tests` (p.ej. los tests de abonos en `SIAD.Tests/Presupuesto/`): fixture de conexión, `BEGIN ... ROLLBACK` por test, `SIAD_TEST_COMPANY_ID`. Replicarlo.

**Paso 13.2** — Casos (cada uno crea su cuenta CHEQUES de prueba dentro de la transacción):
1. `Emitir_asigna_proximo_e_incrementa`: cuenta con `proximo_cheque=100` → emite 100, `proximo_cheque` queda 101; segunda emisión → 101.
2. `Emitir_normaliza_proximo_cero_a_uno`: `proximo_cheque=0` → emite 1.
3. `Emitir_con_maximo_agotado_lanza_y_no_inserta`: `proximo=201, maximo=200` → `InvalidOperationException` con "agotó su numeración"; 0 filas en `ban_cheque`.
4. `Emitir_en_el_maximo_todavia_funciona`: `proximo=200, maximo=200` → emite 200; la siguiente lanza.
5. `Anular_por_kardex_marca_A`: fila emitida con `ban_kardex_id=X` → `AnularPorKardexAsync(X, Y, ...)` → `estado='A'`, motivo/usuario/fecha/reverso poblados; retorna true.
6. `Anular_por_kardex_sin_cheque_es_noop`: kardex sin cheque → retorna false.
7. `Anular_siguiente_numero_consume_y_queda_A`: → fila `origen='MANUAL'`, `estado='A'`, monto 0; `proximo_cheque` incrementado.
8. `Unicidad_de_numero`: INSERT directo duplicado → `PostgresException` 23505 (`uq_ban_cheque_numero`).

**Paso 13.3** — Run: `$env:SIAD_TEST_DB = '<mirror>'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~ChequesServiceTests"`
Expected: `Failed: 0` (o `Skipped` si no hay `SIAD_TEST_DB`).

**Paso 13.4** — Regresión completa: `dotnet test SIAD.Tests/SIAD.Tests.csproj` → sin fallos nuevos.

---

### Task 14: Verificación final

**Paso 14.1** — `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.

**Paso 14.2** — Smoke con la app corriendo (requiere el SQL aplicado en el mirror y la cadena local apuntando ahí):
1. Editar una cuenta tipo CHEQUES → se ven/guardan Próximo cheque y Cheque máximo.
2. Procesar o abonar un compromiso con método CHEQUE → mensaje con el número; fila en `/bancos/cheques`.
3. Anular el abono → el cheque aparece Anulado con motivo.
4. Transacción bancaria manual con tipo CHQ → cheque en la bitácora.
5. Poner `cheque_maximo` < `proximo_cheque` → el pago con CHEQUE se bloquea con el mensaje de agotamiento.
6. "Anular siguiente número" desde la bitácora → fila MANUAL/Anulado.

**Paso 14.3** — Recordar al usuario: aplicar el SQL en mirror y luego en SRV (con la guardia), y que NO hay commit/push automático (regla del repo: el usuario decide cuándo).

---

## Bitácora de eventos (ampliación 2026-07-21, aprobada por el usuario)

Además del libro `ban_cheque`, tabla de **eventos append-only** `ban_cheque_bitacora` (una fila por evento `EMITIDO`/`ANULADO`; nunca se actualiza ni borra), en el mismo script SQL de la Task 1:

- BD: AK `uq_ban_cheque_company_cheque UNIQUE (company_id, cheque_id)` en `ban_cheque` + tabla `ban_cheque_bitacora` con FK compuesta tenant-safe `(company_id, cheque_id)` → `ban_cheque`, CHECK `accion IN ('EMITIDO','ANULADO')` e índice `(company_id, banco_cuenta_id, fecha)`.
- Escritura (misma transacción de cada operación, vía `InsertBitacoraEventoAsync` en `ChequesService`): `EmitirChequeAsync` → `EMITIDO`; `AnularPorKardexAsync` → `ANULADO` por cada cheque reversado (RETURNING del UPDATE; `ban_kardex_id` = el reverso); `AnularSiguienteNumeroAsync` → un único `ANULADO` origen `MANUAL` (sin `EMITIDO`).
- Lectura: `BuscarBitacoraAsync` (`IChequesService`/`ChequesService`) + `GET api/bancos/cheques/bitacora` + `ChequesClient.BuscarBitacoraAsync`; la página `/bancos/cheques` pasa a mostrar los eventos ("Bitácora de cheques", filtro Acción). `BuscarAsync` (libro) se mantiene.
- Tests: el guard de esquema exige también `ban_cheque_bitacora`; asserts de eventos en emisión (1 `EMITIDO`), anulación por kardex (+1 `ANULADO` con motivo y reverso) y anulación manual (exactamente 1 `ANULADO`, 0 `EMITIDO`).
