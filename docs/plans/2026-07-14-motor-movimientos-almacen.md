# Motor de movimientos de inventario (Almacén) — Plan de implementación

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Construir el motor transaccional del almacén: el kardex pasa a ser un libro mayor inmutable y los saldos por bodega (existencia, comprometida, tránsito, costo promedio, último costo) dejan de teclearse a mano para volverse consecuencia de asientos posteados.

**Architecture:** Un único servicio, `IInventarioPostingService`, es el **único punto de escritura de stock** en todo el sistema. Cada documento (ajuste, compra, requisición, descargo, traslado) lo invoca; el motor abre una transacción, bloquea la fila de saldo con `FOR UPDATE`, valida, calcula el costo promedio ponderado, inserta el asiento en `alm_kardex` y actualiza `alm_articulo_bodega` — todo atómico e idempotente. `alm_articulo.existencia` queda como rollup de las bodegas activas. Las tablas transaccionales legacy se **evolucionan en sitio**, marcando el histórico SIMAFI como no-posteable.

**Tech Stack:** .NET 9 · EF Core + Npgsql · PostgreSQL · xUnit (integration tests contra Postgres real, cada test en `BEGIN … ROLLBACK`).

**Decisiones tomadas (2026-07-14, con el usuario):**
1. Documentos: **evolucionar las tablas legacy en sitio** (no crear hdr/dtl nuevas).
2. Captura manual de existencia: **se bloquea**; corregir stock exige un documento de Ajuste posteado.
3. Integración contable: **fuera de este alcance** (el asiento deja los campos listos, pero no postea a `con_partida`).

---

## Estado actual (verificado 2026-07-14)

- `alm_compra`, `alm_descargo`, `alm_requisicion`, `alm_kardex` son **solo lectura**. Ningún servicio inserta movimientos. Cero `Add`/`Update` sobre `alm_kardexs` en código de producción.
- La existencia se **captura a mano** en `ArticuloUbicacionService` (`AddAsync`/`UpdateAsync`), con rollup automático a `alm_articulo` vía `RecomputeArticuloAsync` (`ArticuloUbicacionService.cs:229-247`).
- **No hay transacciones** (`BeginTransaction`) en todo `SIAD.Services/Almacen/`.
- **No existe traslado** — la columna `traslado varchar(1)` en compra/descargo está muerta.
- `alm_articulo_bodega` ya tiene (aplicado en mirror y prod el 2026-07-14): `existencia_comprometida`, `existencia_transito`, `costo_promedio`, `ultimo_costo` — **todas en 0, sin quién las llene**.

---

## Tabla de verdad del motor

Cada evento posteado inserta un asiento y mueve saldos:

| Evento | existencia | comprometida | tránsito | costo |
|---|---|---|---|---|
| Orden de compra emitida | — | — | **+** | — |
| Recepción de compra | **+** | — | **−** | recalcula `costo_promedio`, fija `ultimo_costo` |
| Requisición aprobada | — | **+** | — | — |
| Descargo (despacho) | **−** | **−** | — | valoriza salida al `costo_promedio` |
| Requisición rechazada/anulada | — | **−** | — | — |
| Traslado: envío | **−** origen | — | **+** destino | — |
| Traslado: recepción | **+** destino | — | **−** destino | recalcula en destino |
| Ajuste / toma física | **±** | — | — | ajusta valor |

**Costeo — promedio ponderado móvil.** En cada ingreso:

```
costo_promedio = (existencia × costo_promedio + cantidad × costo_entrada) / (existencia + cantidad)
ultimo_costo   = costo_entrada
```

Las salidas **no alteran** el promedio: se valorizan a él (`valor_salida = cantidad × costo_promedio`).

Casos borde de obligado cumplimiento:
- `existencia = 0` (o el denominador da 0) → `costo_promedio = costo_entrada`. Nunca dividir por cero.
- `existencia < 0` → no debe ocurrir: las salidas se bloquean si `disponible < cantidad`.
- `disponible = existencia − comprometida`. El tránsito **no** cuenta como disponible para salidas; sí cuenta para la alerta de reorden (ya está pedido, no lo vuelvas a pedir).

**Reglas invariantes (no negociables):**
- El kardex es **inmutable**: solo `INSERT`. Nunca `UPDATE` ni `DELETE`. Una corrección se hace con un **contra-asiento** (reversa). Blindado con un trigger en la BD, no solo por convención.
- Ningún saldo se teclea. **Todo saldo es recalculable desde cero.**
- Todo posteo es **idempotente** por `uuid`: repetir el mismo posteo no duplica nada. El `uuid` es **por movimiento (línea)**, no por documento, y es determinista desde `(documento_tipo, documento_id, línea)`.

### De dónde se deriva cada saldo (corregido tras revisión, 2026-07-14)

No todos los saldos vienen del mismo lado. Confundirlos fue un error del primer borrador:

| Saldo | Fuente de verdad | Cómo se reconstruye |
|---|---|---|
| `existencia` | **Kardex** | `SUM(ingresos − salidas)` por (artículo, bodega) |
| `costo_promedio` / `ultimo_costo` | **Kardex** | Reprocesando los asientos en orden |
| `existencia_comprometida` | **Documentos abiertos** | `SUM` de líneas de requisición aprobadas y no despachadas |
| `existencia_transito` | **Documentos abiertos** | `SUM` de líneas de orden de compra / traslado emitidas y no recibidas |

Por eso el kardex **no** lleva columnas de comprometida ni tránsito: un evento "requisición aprobada" no mueve existencia y no tiene por qué existir en el libro mayor. Las columnas `existencia_comprometida` / `existencia_transito` de `alm_articulo_bodega` son una **caché** del agregado de documentos abiertos, mantenida por el motor y **reconstruible en cualquier momento** desde esos documentos. Esto es además como lo resuelve SAP: el material ledger solo registra movimientos reales de mercadería; las reservas se derivan de documentos abiertos.

---

## ⚠️ Riesgo crítico de secuencia — leer antes de empezar

`costo_promedio` se creó con `DEFAULT 0`. Hoy hay artículos con `existencia > 0` y `costo_promedio = 0`.

Si se postea una compra **antes** de la carga inicial, el promedio se corrompe:

```
existencia=100, costo_promedio=0, entra 10 @ L.50
→ (100×0 + 10×50) / 110 = 4.55   ← BASURA. Las 100 unidades sí tienen costo real.
```

**Por eso la Fase 2 (carga inicial) va ANTES de la Fase 3 (compras). No se puede reordenar.**

---

## ⚠️ Riesgo crítico de datos — histórico SIMAFI

`alm_compra`, `alm_descargo` y `alm_requisicion` **ya contienen datos históricos** migrados de SIMAFI. Si el motor puede postear esas filas, se duplica el inventario.

**Mitigación (Tarea 1.2, en el mismo script del DDL):** todo lo existente se marca `posteado = true` y `origen = 'SIMAFI'`. El motor solo postea filas con `posteado = false AND origen = 'SIAD'`.

---

# FASE 1 — Esquema

### Tarea 1.1: DDL — trazabilidad e idempotencia en `alm_kardex`

**Files:**
- Create: `Database/2026-07-14_alm_kardex_trazabilidad.sql`
- Modify: `SIAD.Core/Entities/alm_kardex.cs`
- Modify: `SIAD.Data/SiadDbContext.Almacen.cs:88-127`

**Step 1: Escribir el script**

```sql
-- =============================================================================
-- Kardex: trazabilidad al documento origen, idempotencia y snapshot de saldos
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
-- =============================================================================
BEGIN;

ALTER TABLE alm_kardex
    ADD COLUMN IF NOT EXISTS uuid              UUID,
    ADD COLUMN IF NOT EXISTS documento_tipo    VARCHAR(20),
    ADD COLUMN IF NOT EXISTS documento_id      INT,
    ADD COLUMN IF NOT EXISTS bodega_destino_id INT REFERENCES alm_bodega(id),
    ADD COLUMN IF NOT EXISTS existencia_post   NUMERIC(15,2),
    ADD COLUMN IF NOT EXISTS costo_promedio_post NUMERIC(12,4),
    ADD COLUMN IF NOT EXISTS usuariocreacion   VARCHAR(100),
    ADD COLUMN IF NOT EXISTS fechacreacion     TIMESTAMP WITHOUT TIME ZONE;

-- Idempotencia: un uuid no puede postearse dos veces en la misma empresa.
CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_kardex_company_uuid
    ON alm_kardex(company_id, uuid) WHERE uuid IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_alm_kardex_documento
    ON alm_kardex(documento_tipo, documento_id);

COMMENT ON COLUMN alm_kardex.uuid IS 'Clave de idempotencia del posteo. Reintentar el mismo posteo no duplica el asiento.';
COMMENT ON COLUMN alm_kardex.existencia_post IS 'Snapshot del saldo de existencia DESPUÉS del asiento. Permite auditar y reconstruir saldos.';
COMMENT ON COLUMN alm_kardex.costo_promedio_post IS 'Snapshot del costo promedio DESPUÉS del asiento.';

COMMIT;
```

**Step 2: Aplicar al mirror**

Run:
```bash
PGPASSWORD='Koala@2021' psql -h localhost -U postgres -d siad_v3_restore -v ON_ERROR_STOP=1 \
  -f Database/2026-07-14_alm_kardex_trazabilidad.sql
```
Expected: `BEGIN / ALTER TABLE / CREATE INDEX / … / COMMIT`

> Producción (`siad_v3` @ 172.16.0.9) la aplica el usuario, salvo que pida lo contrario.

**Step 3: Reflejar en la entidad y el mapeo EF**

En `alm_kardex.cs` agregar las propiedades (`Guid? uuid`, `string? documento_tipo`, `int? documento_id`, `int? bodega_destino_id`, `decimal? existencia_post`, `decimal? costo_promedio_post`, auditoría). En `SiadDbContext.Almacen.cs` agregar precisiones e índices, siguiendo el estilo del bloque `alm_kardex` existente.

**Step 4: Compilar**

Run: `dotnet build SIAD.Data/SIAD.Data.csproj`
Expected: 0 errores.

---

### Tarea 1.2: DDL — bodega, posteo y blindaje del histórico en los documentos

**Files:**
- Create: `Database/2026-07-14_alm_documentos_bodega_posteo.sql`
- Modify: `SIAD.Core/Entities/alm_compra.cs`, `alm_descargo.cs`, `alm_requisicion.cs`
- Modify: `SIAD.Data/SiadDbContext.Almacen.cs`

**Step 1: Escribir el script** (⚠️ el backfill del histórico es la parte crítica)

```sql
-- =============================================================================
-- Documentos de almacén: bodega, estado de posteo e idempotencia.
-- Blinda el histórico SIMAFI para que el motor NUNCA lo postee.
-- Fecha: 2026-07-14
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost)
-- =============================================================================
BEGIN;

DO $$
DECLARE t TEXT;
BEGIN
  FOREACH t IN ARRAY ARRAY['alm_compra','alm_descargo','alm_requisicion'] LOOP
    EXECUTE format($f$
      ALTER TABLE %I
        ADD COLUMN IF NOT EXISTS bodega_id    INT REFERENCES alm_bodega(id),
        ADD COLUMN IF NOT EXISTS posteado     BOOLEAN NOT NULL DEFAULT false,
        ADD COLUMN IF NOT EXISTS fecha_posteo TIMESTAMP WITHOUT TIME ZONE,
        ADD COLUMN IF NOT EXISTS uuid         UUID,
        ADD COLUMN IF NOT EXISTS origen       VARCHAR(10) NOT NULL DEFAULT 'SIAD';
      CREATE INDEX IF NOT EXISTS ix_%I_posteo ON %I(posteado, origen);
    $f$, t, t, t);
  END LOOP;
END $$;

-- BLINDAJE: todo lo que ya existe es histórico SIMAFI. Queda posteado y no-posteable.
UPDATE alm_compra      SET origen='SIMAFI', posteado=true WHERE origen='SIAD' AND posteado=false;
UPDATE alm_descargo    SET origen='SIMAFI', posteado=true WHERE origen='SIAD' AND posteado=false;
UPDATE alm_requisicion SET origen='SIMAFI', posteado=true WHERE origen='SIAD' AND posteado=false;

COMMIT;
```

**Step 2: Verificar el blindaje ANTES de seguir**

Run:
```sql
SELECT 'compra' t, origen, posteado, count(*) FROM alm_compra GROUP BY 2,3
UNION ALL SELECT 'descargo', origen, posteado, count(*) FROM alm_descargo GROUP BY 2,3
UNION ALL SELECT 'requisicion', origen, posteado, count(*) FROM alm_requisicion GROUP BY 2,3;
```
Expected: **cero filas** con `origen='SIAD'`. Si aparece alguna, DETENERSE — el blindaje falló.

**Step 3–4:** Reflejar en entidades + mapeo EF y compilar (0 errores).

---

### Tarea 1.3: DDL — tabla de traslados (no existe legacy)

**Files:**
- Create: `Database/2026-07-14_alm_traslado.sql`
- Create: `SIAD.Core/Entities/alm_traslado.cs`
- Modify: `SIAD.Data/SiadDbContext.Almacen.cs`

Tabla `alm_traslado`: `id`, `company_id`, `numero`, `fecha`, `articulo_id`, `bodega_origen_id`, `bodega_destino_id`, `cantidad NUMERIC(15,2)`, `estado VARCHAR(1)` (`E`=emitido/en tránsito, `R`=recibido, `A`=anulado), `uuid`, auditoría. Implementa `ICompanyScopedEntity`. Índice único `(company_id, numero)`.

---

# FASE 2 — El motor de posteo

### Tarea 2.1: Contratos (tipos de movimiento y DTOs)

**Files:**
- Create: `SIAD.Core/DTOs/Almacen/TipoMovimientoInventario.cs`
- Create: `SIAD.Core/DTOs/Almacen/MovimientoInventarioDto.cs`
- Create: `SIAD.Core/DTOs/Almacen/PosteoResultDto.cs`

```csharp
public enum TipoMovimientoInventario
{
    AjustePositivo, AjusteNegativo, CargaInicial,
    OrdenCompraEmitida, RecepcionCompra,
    RequisicionAprobada, RequisicionAnulada,
    Descargo,
    TrasladoEnvio, TrasladoRecepcion,
    Reversa
}

public sealed record MovimientoInventarioDto
{
    public required Guid Uuid { get; init; }                  // idempotencia
    public required TipoMovimientoInventario Tipo { get; init; }
    public required int ArticuloId { get; init; }
    public required int BodegaId { get; init; }
    public int? BodegaDestinoId { get; init; }                // solo traslados
    public required decimal Cantidad { get; init; }           // SIEMPRE positiva; el signo lo decide el Tipo
    public decimal CostoUnitario { get; init; }               // solo ingresos
    public DateOnly? Fecha { get; init; }
    public string? DocumentoTipo { get; init; }
    public int? DocumentoId { get; init; }
    public string? Descripcion { get; init; }
}
```

### Tarea 2.2: `IInventarioPostingService` — TDD

**Files:**
- Create: `SIAD.Services/Almacen/IInventarioPostingService.cs`
- Create: `SIAD.Services/Almacen/InventarioPostingService.cs`
- Create: `SIAD.Tests/Almacen/InventarioPostingTests.cs`
- Modify: `SIAD.Services/ServiceRegistration.cs`

**Step 1: Escribir los tests que fallan** (uno por invariante; empezar por el costeo)

```csharp
[Fact] // El caso que corrompe todo si sale mal
public async Task Ingreso_recalcula_costo_promedio_ponderado()
{
    // saldo: 100 u @ L.10  →  entra 50 u @ L.16
    // esperado: (100*10 + 50*16) / 150 = 12.00 ; ultimo_costo = 16
}

[Fact] public async Task Ingreso_con_existencia_cero_toma_el_costo_de_entrada() { }
[Fact] public async Task Salida_valoriza_al_promedio_y_NO_lo_altera() { }
[Fact] public async Task Salida_sin_disponible_es_rechazada() { }
[Fact] public async Task Requisicion_aprobada_solo_mueve_comprometida() { }
[Fact] public async Task Descargo_baja_existencia_y_comprometida() { }
[Fact] public async Task Postear_dos_veces_el_mismo_uuid_no_duplica() { }   // idempotencia
[Fact] public async Task Fallo_a_media_transaccion_no_deja_kardex_sin_saldo() { }  // atomicidad
[Fact] public async Task El_rollup_de_alm_articulo_suma_solo_bodegas_activas() { }
```

**Step 2:** Correr y verificar que fallan.
Run: `$env:SIAD_TEST_DB='<conn>'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~InventarioPostingTests"`

**Step 3: Implementar el motor**

Esqueleto obligatorio — la secuencia importa:

```csharp
public async Task<PosteoResultDto> PostearAsync(MovimientoInventarioDto mov, CancellationToken ct)
{
    await using var tx = await _context.Database.BeginTransactionAsync(ct);

    // 1. IDEMPOTENCIA — ¿ya se posteó este uuid?
    var yaPosteado = await _context.alm_kardexs.AsNoTracking()
        .FirstOrDefaultAsync(k => k.uuid == mov.Uuid, ct);
    if (yaPosteado is not null) return PosteoResultDto.YaPosteado(yaPosteado.id);

    // 2. BLOQUEO PESIMISTA de la fila de saldo. Sin esto, dos ingresos
    //    concurrentes corrompen el promedio (read-modify-write).
    var saldo = await _context.alm_articulo_bodegas
        .FromSqlInterpolated($@"
            SELECT * FROM alm_articulo_bodega
            WHERE company_id = {_company.CompanyId}
              AND articulo_id = {mov.ArticuloId}
              AND bodega_id  = {mov.BodegaId}
            FOR UPDATE")
        .FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("El artículo no tiene ubicación en esa bodega.");

    // 3. VALIDAR + 4. CALCULAR + 5. APLICAR (según mov.Tipo)
    //    - salidas: si (saldo.existencia - saldo.existencia_comprometida) < mov.Cantidad → rechazar
    //    - ingresos: recalcular costo_promedio ANTES de sumar existencia
    // 6. INSERTAR el asiento en alm_kardex (con existencia_post / costo_promedio_post)
    // 7. ROLLUP a alm_articulo
    // 8. UN SOLO SaveChangesAsync

    await _context.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return PosteoResultDto.Ok(...);
}
```

**Step 4:** Correr los tests hasta que pasen todos.

> **Nota de concurrencia:** el `FOR UPDATE` no es opcional. Sin él, dos recepciones simultáneas del mismo artículo leen el mismo `costo_promedio`, calculan sobre el mismo saldo y la última pisa a la primera — el promedio queda mal y **no hay forma de detectarlo después**.

---

# FASE 3 — Carga inicial y cierre de la captura manual

> Esta fase **debe correr antes de la Fase 4**. Ver "Riesgo crítico de secuencia".

### Tarea 3.1: Postear el asiento de apertura

Un comando (o endpoint one-shot) que, por cada fila activa de `alm_articulo_bodega` con `existencia <> 0`, postea un movimiento `CargaInicial`:
- `cantidad = existencia actual`
- `costo_unitario = alm_articulo.valor_unitario` (el costo conocido del artículo; si es 0, se reporta y **no** se postea — hay que decidir el costo con el usuario)

Efecto: siembra `costo_promedio` con un valor real y deja el kardex con un asiento de apertura que cuadra con los saldos. Idempotente por `uuid` derivado de `(articulo_id, bodega_id, 'APERTURA')`.

**Verificación de cierre de fase:**
```sql
SELECT count(*) FROM alm_articulo_bodega WHERE activo AND existencia <> 0 AND costo_promedio = 0;
```
Expected: **0**. Si no, no se puede avanzar a compras.

### Tarea 3.2: Bloquear la captura manual de existencia

- `ArticuloUbicacionService.AddAsync` / `UpdateAsync`: dejar de escribir `existencia` desde el DTO (mantener ubicaciones, mín/máx, principal, activo).
- `apc.Client`: el campo Existencia en la pestaña Ubicaciones pasa a **solo lectura**, con enlace a "Registrar ajuste".
- Los saldos derivados (`comprometida`, `transito`, `costo_promedio`, `ultimo_costo`) se muestran pero **nunca** son editables.

### Tarea 3.3: Documento de Ajuste

Endpoint + servicio + UI mínima para postear `AjustePositivo` / `AjusteNegativo` con motivo. Es el reemplazo legítimo de la edición manual.

---

# FASE 4 — Compras (ingreso)

- `POST /api/almacen/compras` → crea la compra (`origen='SIAD'`, `posteado=false`, con `bodega_id`).
- `POST /api/almacen/compras/{id}/postear` → invoca `RecepcionCompra`: `existencia +`, recalcula `costo_promedio`, fija `ultimo_costo`, baja `existencia_transito` si venía de orden de compra. Marca `posteado=true`.
- (Opcional) `POST /api/almacen/compras/{id}/orden` → `OrdenCompraEmitida`: solo `existencia_transito +`.
- Guarda: el endpoint de posteo **rechaza** filas con `origen='SIMAFI'` o `posteado=true`.

# FASE 5 — Requisición → Descargo

- Aprobar requisición → `RequisicionAprobada` (`comprometida +`). Validar que haya disponible.
- Rechazar/anular → `RequisicionAnulada` (`comprometida −`).
- Descargar → `Descargo` (`existencia −`, `comprometida −`, valorizado al promedio). Marca la requisición `descargado=true`.
- Escribir por fin los campos `estatus`/`aprobado`/`rechazado`/`descargado`, que hoy se proyectan al DTO pero nunca se persisten.

# FASE 6 — Traslados

- Envío → `TrasladoEnvio`: `existencia −` en origen, `existencia_transito +` en destino.
- Recepción → `TrasladoRecepcion`: `existencia_transito −` y `existencia +` en destino, recalculando el costo promedio del destino.

# FASE 7 — Reorden, valorización y (aparte) contabilidad

- Alerta de reorden: `(existencia − comprometida + transito) < existencia_minima`.
- Reporte de valorización de inventario (`existencia × costo_promedio` por bodega).
- **Contabilidad: diseño aparte.** El asiento ya deja `debe`/`haber`/`cuenta_contable` listos y `alm_tipo_articulo` tiene las cuentas (`cuenta_inventario`, `cuenta_costo_ventas`, `cuenta_ajustes`).

---

## Definición de "terminado" (por fase)

- Todos los tests de `InventarioPostingTests` en verde.
- `dotnet build HODSOFT_DEVEXPRESS.sln` sin errores.
- Invariante verificable en BD tras cualquier posteo:

```sql
-- Los saldos deben cuadrar contra el kardex, siempre.
SELECT ab.articulo_id, ab.bodega_id, ab.existencia,
       COALESCE(SUM(k.ingresos - k.salidas), 0) AS segun_kardex
FROM alm_articulo_bodega ab
LEFT JOIN alm_kardex k
       ON k.articulo_id = ab.articulo_id AND k.bodega_id = ab.bodega_id
GROUP BY ab.articulo_id, ab.bodega_id, ab.existencia
HAVING ab.existencia <> COALESCE(SUM(k.ingresos - k.salidas), 0);
```
Expected: **cero filas**. Si devuelve algo, el motor tiene una fuga.
