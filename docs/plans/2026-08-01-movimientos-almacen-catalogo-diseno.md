# Movimientos de almacén con catálogo configurable — Diseño

Fecha: 2026-08-01 · **Actualizado 2026-08-03**
Estado: **Fase 1 implementada y aplicada al mirror.** Fase 2 aprobada en prototipo, sin implementar.

> ### Decisiones y hallazgos del 2026-08-03 (leer antes que el resto)
>
> 1. **El catálogo ya NO se siembra con tipos inventados.** Se importaron los **12 tipos reales**
>    de `dbo.INV_TIPOSTRANSACC` de MERENDON. Los 3 de §3.5 (`SOBRANTE_CONTEO`, `MERMA`,
>    `CORRECCION_COSTO`) **fueron borrados**. §3.5 queda **obsoleta** — ver
>    [`README_entradas_salidas_almacen.md` §3.1](../centura-flujos/README_entradas_salidas_almacen.md).
> 2. **Ya no hay ningún tipo de clase `VALOR`.** Centura no tiene equivalente y el usuario aceptó
>    la paridad estricta. `AjusteValor` sigue existiendo en el motor; simplemente ningún tipo del
>    catálogo lo invoca hoy. Si se necesita corregir costo desde el documento, hay que dar de alta
>    un tipo `VALOR` desde la pantalla.
> 3. **`requiere_autorizacion` se queda como booleano** (decisión del usuario, 2026-08-03). Centura
>    tiene una matriz usuario × tipo (`AXL_USUARIOS_TRN`) que **no se porta**: la evidencia muestra
>    que dejó de mantenerse. §5 sigue vigente tal cual.
> 4. **La pantalla de referencia es `dlgTransaccionesGenericasINV`**, encontrada el 2026-08-03 en
>    `Casajaar_Final/NEWAPP/GA_IN.APT:14142`. El diseño original se hizo por analogía con
>    `AjusteInventarioService` porque se creyó que no existía. **Sí existe**, y §6 se ajusta a ella.
> 5. **Prototipo de la Fase 2 aprobado** por el usuario el 2026-08-03. Cambios que introduce sobre
>    este documento: campo **`documento_referencia`** en la cabecera (el `Referencia1` = `#DOCUMENTO`
>    del legacy), **existencia visible por renglón**, y la columna de costo **deshabilitada en
>    clase `SALIDA`** (equivalente del `PIDE_COSTO` de Centura).
> 6. **⚠️ Vocabulario de la UI ≠ vocabulario del código (decisión del usuario, 2026-08-03).** En lo
>    que ve el usuario, en las **rutas** y en los **permisos**:
>    - la **`clase`** (`ENTRADA`/`SALIDA`/`VALOR`) se rotula **«Tipo»**;
>    - el **catálogo** (`alm_tipo_movimiento`: Merma, Donación) se llama **«Concepto de movimiento»**.
>
>    En el **código y la base NO cambia nada**: la tabla sigue siendo `alm_tipo_movimiento`, la
>    columna `clase`, los servicios `TipoMovimientoService`, los DTOs `TipoMovimientoAlmacenDto`, los
>    archivos `TiposMovimientoList.razor` / `TipoMovimientoForm.razor`. Sólo se renombraron:
>    · ruta `/almacen/conceptos-movimiento` (antes `tipos-movimiento`) y API `api/almacen/conceptos-movimiento`;
>    · recurso y permisos `module.inventario.conceptos_movimiento.*` (antes `tipos_movimiento`);
>    · todas las etiquetas visibles y el nodo de menú «Conceptos de movimiento».
>    El formulario del catálogo se **rediseñó** (grid propio en vez de `DxFormLayout`, que metía los
>    textos de ayuda en celdas angostas y los partía en vertical) — ver `TipoMovimientoForm.razor(.css)`.

Analista, DBA, desarrollador senior y QA hablan en este documento: cada decisión trae su porqué,
el modelo de datos es DDL real, los servicios son firmas reales sobre el código que existe hoy, y
hay un plan de pruebas explícito. Fuente del análisis comparativo:
[`README_entradas_salidas_almacen.md`](../centura-flujos/README_entradas_salidas_almacen.md).

---

## 1. Decisión de arquitectura y su costo

El usuario decidió (2026-08-01, en conversación) reemplazar el plan específico de
requisición→descargo por una arquitectura de **catálogo configurable de tipos de movimiento**,
inspirada en `INV_TIPOSTRANSACC` de Centura, después de que se le señalara el costo explícito:

- **`docs/centura-flujos/README_requisiciones_descargos.md`** (1.576 líneas, 2026-07-31) queda
  **supersedido en su arquitectura** (§6-12: modelo de datos, servicios, plan por fases). Su
  análisis de Centura (§3-5) sigue siendo válido y se cita aquí.
- **Fase 0 de ese plan (motor) se conserva íntegra.** `TipoMovimientoInventario.SalidaDescargo`,
  la reversa espejo por `documento_tipo` y las 11 pruebas asociadas no se tocan: son la capa de
  bajo nivel, agnóstica del documento que la invoque. Este diseño se apoya en ella.
- **Fase 1 de ese plan (BD) se revierte.** `Database/2026-08-01_alm_requisicion_descargo.sql`
  está **aplicado al mirror** (`siad_v3_restore`) con 4 tablas nuevas y 6 FK compuestas. El propio
  script trae su bloque de rollback (líneas 440-463) — se ejecuta cuando el usuario decida aplicar
  este diseño, nunca antes. **No se toca ninguna base de datos como parte de este documento.**
- El flujo de dos actores (solicitante pide, jefe aprueba, bodeguero entrega) que resuelve
  requisición/descargo **no está cubierto** por esta primera entrega — ver §8, Fase 6.

---

## 2. Por qué dos capas y no una

Centura acertó en una cosa que el portal no tiene: el comportamiento de un movimiento no está
compilado, está en una fila de `INV_TIPOSTRANSACC` (`ENTRA_SALE`, `CAMBIA_COSTO`, correlativo
propio). Agregar "donación" o "merma por vencimiento" era un `INSERT`, no un despliegue.

Pero Centura pagó ese poder con 14 defectos verificados (README_entradas_salidas_almacen.md §5):
el motor confiaba en esa fila sin validar que existiera (D-3), reescribía el libro (D-1), y no
tenía concurrencia (D-5). La corrección **no es** meter toda esa lógica en la tabla; es separar
qué es dato de qué es código:

| Capa | Qué es | Dónde vive | Quién la cambia |
|---|---|---|---|
| **Clase de movimiento** | La semántica que el motor sabe ejecutar: suma existencia, resta existencia, corrige valor. Gobierna el costeo, las guardas de negativo, la idempotencia. | `TipoMovimientoInventario` (enum, **ya existe**, sin tocar) | Solo con código + tests + migración de BD |
| **Tipo de movimiento** | El nombre de negocio: "Merma por vencimiento", "Donación", "Sobrante de conteo". Cuenta contable, si exige autorización, si está activo. | `alm_tipo_movimiento` (tabla, **NUEVA**) | El usuario, desde una pantalla de mantenimiento |

El acierto de Centura queda; los 14 defectos no, porque el motor (`InventarioPostingService`) que
ejecuta la clase **no cambia una sola línea** en este diseño. Sigue siendo el único punto de
escritura, con su `FOR UPDATE`, su idempotencia por `uuid` y sus guardas ya probadas.

`ClaseAjusteInventario` (`SIAD.Core/Constants/ClaseAjusteInventario.cs`) **ya es** exactamente este
vocabulario — `ENTRADA` / `SALIDA` / `VALOR` — porque es lo mismo que necesita un ajuste. Este
diseño no inventa una clase nueva: **extiende su rol** de "tipo de ajuste" a "clase de movimiento",
y lo pone a las órdenes del catálogo. El mapeo clase→motor que hoy vive en
`AjusteInventarioService.TipoMovimientoDe:188-194` se reutiliza sin cambios:

```
ENTRADA → TipoMovimientoInventario.AjustePositivo
SALIDA  → TipoMovimientoInventario.AjusteNegativo
VALOR   → TipoMovimientoInventario.AjusteValor
```

---

## 3. Modelo de datos

### 3.1 `alm_tipo_movimiento` — el catálogo (NUEVA)

```sql
CREATE TABLE alm_tipo_movimiento (
    id                    SERIAL        PRIMARY KEY,
    company_id            BIGINT        NOT NULL,
    codigo                VARCHAR(20)   NOT NULL,
    nombre                VARCHAR(80)   NOT NULL,
    clase                 VARCHAR(10)   NOT NULL,   -- ENTRADA | SALIDA | VALOR
    requiere_autorizacion BOOLEAN       NOT NULL DEFAULT false,
    cuenta_contable       VARCHAR(20)   NULL,       -- override; NULL = hereda del tipo de artículo
    activo                BOOLEAN       NOT NULL DEFAULT true,
    orden                 SMALLINT      NOT NULL DEFAULT 0,
    usuariocreacion       VARCHAR(100)  NULL,
    fechacreacion         TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion   VARCHAR(100)  NULL,
    fechamodificacion     TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_tipo_movimiento_codigo UNIQUE (company_id, codigo),
    CONSTRAINT ck_alm_tipo_movimiento_clase CHECK (clase IN ('ENTRADA','SALIDA','VALOR'))
);
```

**Multiempresa real, no global.** Cada `company_id` tiene su propia fila — evita el defecto D-3
de Centura por otra vía: si el catálogo fuera global, una empresa podría desactivar un tipo que
otra tiene en uso. Se siembra igual para todas al aplicarse (§3.4) y cada una lo edita después sin
afectar a las demás.

**Por qué `codigo` y no solo `nombre`:** el código es estable para reportes y el `Observacion` del
asiento del kardex; el nombre se puede retocar (typo, cambio de redacción) sin romper histórico.

**Por qué `cuenta_contable` es override y no obligatorio:** ya existe un dueño de esa cuenta
(`alm_tipo_articulo.cuenta_inventario` / `cuenta_ajustes`, ver decisión 3 del diseño de costeo,
`docs/plans/2026-08-01-costeo-articulo-diseno.md:402-410`). El catálogo solo la pisa cuando el
tipo de movimiento la exige contablemente distinta (p. ej. "Donación" contra una cuenta de gasto
que ningún tipo de artículo declara).

**Por qué no hay `DELETE`:** un tipo con movimientos posteados no se puede borrar sin dejar
huérfano el `alm_movimiento_dtl.tipo_movimiento_id`. Se desactiva (`activo = false`); el servicio
rechaza usarlo en un documento nuevo y la pantalla lo oculta del combo, pero el histórico lo sigue
resolviendo.

### 3.2 `alm_movimiento_hdr` — cabecera del documento (NUEVA)

```sql
CREATE TABLE alm_movimiento_hdr (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    numero              INTEGER       NOT NULL,
    tipo_movimiento_id  INTEGER       NOT NULL,
    bodega_id           INTEGER       NOT NULL,
    fecha               DATE          NOT NULL,
    motivo              VARCHAR(120)  NOT NULL,
    observaciones       VARCHAR(1000) NULL,
    total               NUMERIC(14,2) NOT NULL DEFAULT 0,
    estado              SMALLINT      NOT NULL DEFAULT 1,   -- 1 Registrado · 9 Anulado
    posteado            BOOLEAN       NOT NULL DEFAULT false,
    fecha_posteo        TIMESTAMP WITHOUT TIME ZONE NULL,
    anulado_por         VARCHAR(100)  NULL,
    fecha_anulacion     TIMESTAMP WITHOUT TIME ZONE NULL,
    motivo_anulacion    VARCHAR(500)  NULL,
    uuid                UUID          NULL,          -- idempotencia del documento (doble clic, retry)
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_movimiento_hdr_numero UNIQUE (company_id, numero),
    CONSTRAINT ck_alm_movimiento_hdr_estado CHECK (estado IN (1, 9)),
    CONSTRAINT ck_alm_movimiento_hdr_posteo
        CHECK (posteado = false OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL)),
    CONSTRAINT ck_alm_movimiento_hdr_anulacion
        CHECK (estado <> 9 OR (anulado_por IS NOT NULL AND fecha_anulacion IS NOT NULL)),
    CONSTRAINT ck_alm_movimiento_hdr_motivo CHECK (length(btrim(motivo)) > 0)
);
CREATE UNIQUE INDEX uq_alm_movimiento_hdr_company_uuid
    ON alm_movimiento_hdr (company_id, uuid) WHERE uuid IS NOT NULL;
```

FK compuestas tenant-safe contra `alm_tipo_movimiento(company_id, id)` y `alm_bodega(company_id, id)`
(la segunda ya existe como clave alterna, `2026-07-14_alm_fk_compuestas_tenant.sql:101-105`).

**`motivo` es obligatorio siempre**, a diferencia de Centura (donde solo el traslado pedía
comentario) y a diferencia también de `alm_descargo_hdr` (que lo exige solo sin requisición). Aquí
todo documento es, por definición, una salida/entrada fuera del flujo transaccional normal (compra,
descargo formal): siempre necesita una razón.

**`estado` sin máquina de estados compleja.** A diferencia de requisición/descargo (dos actores,
aprobación), este documento se crea y se postea **en el mismo paso**, igual que
`AjusteInventarioService.CrearYPostearAsync` hoy. Es la razón por la que no necesita `Borrador`,
`Enviado`, `Aprobado`: solo `Registrado` (con su asiento ya en el kardex) o `Anulado` (con su
reversa). `requiere_autorizacion` del tipo de movimiento no bifurca el estado del documento — el
permiso que exige se resuelve en el controller (§5), no en un flujo de aprobación.

### 3.3 `alm_movimiento_dtl` — línea, unidad de posteo (NUEVA)

```sql
CREATE TABLE alm_movimiento_dtl (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        BIGINT        NOT NULL,
    movimiento_hdr_id INTEGER       NOT NULL,
    articulo_id       INTEGER       NOT NULL,
    codigo_articulo   VARCHAR(20)   NULL,      -- snapshot del catálogo al momento de capturar
    cantidad          NUMERIC(15,2) NOT NULL DEFAULT 0,
    costo_unitario    NUMERIC(12,4) NOT NULL DEFAULT 0,  -- lo que teclea el usuario (ENTRADA/VALOR)
    costo_real        NUMERIC(12,4) NULL,       -- lo que aplicó el motor, copiado tras postear
    total             NUMERIC(14,2) NOT NULL DEFAULT 0,
    kardex_id         INTEGER       NULL,
    uuid              UUID          NOT NULL,   -- idempotencia de LA LÍNEA
    posteado          BOOLEAN       NOT NULL DEFAULT false,

    CONSTRAINT uq_alm_movimiento_dtl_uuid UNIQUE (company_id, uuid),
    CONSTRAINT ck_alm_movimiento_dtl_cantidad CHECK (cantidad >= 0)
);
CREATE INDEX ix_alm_movimiento_dtl_hdr ON alm_movimiento_dtl (company_id, movimiento_hdr_id);
```

Mismo patrón que `alm_compra` / `alm_descargo`: **la unidad de posteo es la línea**, no la
cabecera. Cada línea deriva su propio `uuid` v5 determinista de `(tipo_documento, hdr_id, linea)`,
así que un reintento de la petición no duplica un renglón sí insertado mientras otro falló.

`costo_real` **existe desde el día uno** — es la mejora ya aplicada en el diseño de descargo
(§7.3 del README supersedido, punto 3 de la tabla "mejoras sobre compras"): copiar el costo real
del asiento al documento evita un `JOIN` contra `alm_kardex` en cada `GetById`.

### 3.4 `alm_movimiento_correlativo`

```sql
CREATE TABLE alm_movimiento_correlativo (
    company_id    BIGINT  PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);
```

Mismo patrón exacto de `alm_compra_correlativo`: `SELECT ... FOR UPDATE` antes de incrementar
(`RecepcionCompraService.cs:791-812`).

### 3.5 Semilla obligatoria del catálogo

El catálogo no puede nacer vacío — sin filas, no hay con qué capturar el primer documento. El
script de aplicación siembra, por cada `company_id` con inventario activo, tres tipos mínimos que
son el equivalente exacto de lo que `ClaseAjusteInventario` ya cubre hoy:

| `codigo` | `nombre` | `clase` |
|---|---|---|
| `SOBRANTE_CONTEO` | Sobrante de conteo físico | `ENTRADA` |
| `MERMA` | Merma / faltante de conteo físico | `SALIDA` |
| `CORRECCION_COSTO` | Corrección de costo | `VALOR` |

Estos tres son la migración 1:1 de lo que hoy se teclea desde `ArticuloUbicacionesTab.razor`. El
usuario amplía el catálogo después (donación, consumo interno, merma por vencimiento) sin tocar
código — es exactamente el poder de `INV_TIPOSTRANSACC` que motivó este diseño.

### 3.6 `alm_ajuste_inventario`: qué pasa con lo que ya existe

La tabla y el servicio (`AjusteInventarioService`, `alm_ajuste_inventario.cs`) son de esta misma
semana. **Antes de decidir migrar o congelar, hay que confirmar cuántas filas tiene hoy** — no se
asume (ver Decisión 2, §9). Con pocas o ninguna fila real, la recomendación es:

- El servicio y la tabla quedan **deprecados**, no borrados: el histórico (si existe) se lee tal
  cual, sin migrarlo a `alm_movimiento_hdr`.
- Toda captura nueva pasa por `IMovimientoAlmacenService`.
- El atajo "Registrar ajuste" de `ArticuloUbicacionesTab.razor:872` (toast que hoy usa el ajuste de
  una línea) se re-apunta al formulario nuevo, con la línea precargada.

---

## 4. Servicios

### 4.1 Catálogo

```csharp
// SIAD.Services/Almacen/ITipoMovimientoService.cs
public interface ITipoMovimientoService
{
    Task<IReadOnlyList<TipoMovimientoListItemDto>> GetAsync(bool soloActivos, CancellationToken ct = default);
    Task<TipoMovimientoDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TipoMovimientoDto> CrearAsync(TipoMovimientoDto dto, string user, CancellationToken ct = default);
    Task<TipoMovimientoDto> ActualizarAsync(int id, TipoMovimientoDto dto, string user, CancellationToken ct = default);

    /// <summary>Desactiva; NO borra. Rechaza si ya tiene movimientos posteados Y se intenta reactivar
    /// con datos incompatibles (clase no se puede cambiar si el tipo ya tiene historial).</summary>
    Task DesactivarAsync(int id, string user, CancellationToken ct = default);
}
```

**Guarda de negocio no obvia:** `clase` no se puede editar una vez que el tipo tiene algún
`alm_movimiento_dtl.posteado = true`. Cambiar de `ENTRADA` a `SALIDA` en un tipo ya usado
reinterpretaría retroactivamente qué significó cada asiento pasado.

### 4.2 Documento

```csharp
// SIAD.Services/Almacen/IMovimientoAlmacenService.cs
public interface IMovimientoAlmacenService
{
    Task<IReadOnlyList<MovimientoAlmacenListItemDto>> GetAsync(MovimientoAlmacenFilterDto? f, CancellationToken ct = default);
    Task<MovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Crea Y postea todas las líneas en la misma transacción. Documento y asiento
    /// nacen juntos: un documento sin asiento es un papel sin efecto (mismo criterio que
    /// AjusteInventarioService hoy).</summary>
    Task<MovimientoAlmacenDto> CrearYPostearAsync(MovimientoAlmacenDto dto, string user, CancellationToken ct = default);

    /// <summary>Reversa espejo por línea + estado 9. Idempotente.</summary>
    Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default);
}
```

**`CrearYPostearAsync` — el núcleo, generalización directa de `AjusteInventarioService`:**

```csharp
public async Task<MovimientoAlmacenDto> CrearYPostearAsync(MovimientoAlmacenDto dto, string user, CancellationToken ct = default)
{
    var companyId = _company.GetCompanyId();
    if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

    if (dto.Detalles is null || dto.Detalles.Count == 0)
        throw new InvalidOperationException("El movimiento debe tener al menos un renglón.");

    var motivo = (dto.Motivo ?? string.Empty).Trim();
    if (motivo.Length == 0) throw new InvalidOperationException("El motivo es obligatorio.");

    // ── Idempotencia del DOCUMENTO ───────────────────────────────────────
    if (dto.Uuid.HasValue)
    {
        var ya = await _context.alm_movimiento_hdrs.AsNoTracking()
            .FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
        if (ya is not null) return (await GetByIdAsync(ya.id, ct))!;
    }

    var tipo = await _context.alm_tipo_movimientos.AsNoTracking()
        .FirstOrDefaultAsync(t => t.id == dto.TipoMovimientoId, ct)
        ?? throw new InvalidOperationException("El tipo de movimiento no existe en la empresa actual.");
    if (!tipo.activo)
        throw new InvalidOperationException($"El tipo de movimiento '{tipo.nombre}' está inactivo.");

    // Resuelve un par (artículo, bodega) por renglón; NO crea pares nuevos — a diferencia de
    // compras (ResolverParAsync), un movimiento no da de alta ubicaciones: si el artículo no
    // tiene ubicación en esa bodega, se rechaza (mismo criterio que AjusteInventarioService:59-69).
    var pares = await ResolverParesAsync(dto.Detalles, dto.BodegaId, companyId, ct);

    await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

    var numero = await SiguienteNumeroAsync(companyId, ct);
    var hdr = new alm_movimiento_hdr { /* numero, tipo_movimiento_id, bodega_id, fecha, motivo,
        observaciones, estado = Registrado, posteado = false, uuid = dto.Uuid ?? Guid.NewGuid(),
        auditoría */ };

    var lineas = dto.Detalles.Select(d => new alm_movimiento_dtl
    {
        cabecera = hdr,
        articulo_id = d.ArticuloId,
        cantidad = d.Cantidad,
        costo_unitario = d.CostoUnitario,
        uuid = Guid.NewGuid(),
        posteado = false
    }).ToList();

    _context.alm_movimiento_hdrs.Add(hdr);
    _context.alm_movimiento_dtls.AddRange(lineas);
    await _context.SaveChangesAsync(ct);   // las líneas necesitan su id: ES el documento del asiento

    decimal total = 0m;
    var tipoMotor = TipoMovimientoDe(tipo.clase);   // reutiliza AjusteInventarioService.TipoMovimientoDe

    foreach (var linea in lineas)
    {
        var r = await _posting.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = tipoMotor,
            ArticuloBodegaId = pares[linea.articulo_id],
            Cantidad = linea.cantidad,
            CostoUnitario = linea.costo_unitario,
            Fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today),
            DocumentoTipo = TipoDocumentoInventario.Ajuste,   // el vocabulario cerrado no cambia:
                                                               // sigue siendo un ajuste desde el punto
                                                               // de vista del kardex; lo que cambia es
                                                               // quién lo originó (tipo_movimiento)
            DocumentoId = linea.id,
            Observacion = $"{tipo.nombre} · {motivo}"
        }, user, ct);

        linea.kardex_id = r.KardexId;
        linea.costo_real = linea.costo_unitario > 0 ? linea.costo_unitario : r.CostoPromedioResultante;
        linea.total = Redondear2(linea.cantidad * linea.costo_real!.Value);
        linea.posteado = true;
        total += linea.total;
    }

    hdr.total = Redondear2(total);
    hdr.posteado = true;
    hdr.fecha_posteo = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    await _context.SaveChangesAsync(ct);
    await TransaccionAmbiente.ConfirmarAsync(tx, ct);
    return (await GetByIdAsync(hdr.id, ct))!;
}
```

**Por qué `DocumentoTipo = TipoDocumentoInventario.Ajuste` y no un tipo nuevo:** el vocabulario
cerrado de `documento_tipo` (`SIAD.Core/Constants/TipoDocumentoInventario.cs`) describe la
**naturaleza contable** del asiento frente al kardex — compra, descargo, ajuste — no el catálogo de
negocio que lo originó. Un tipo de movimiento "Donación" sigue siendo, para el libro mayor, un
ajuste de salida. Distinguirlos no exige ampliar el `CHECK` de la base: el nombre de negocio ya
queda en `alm_movimiento_dtl.movimiento_hdr_id → alm_movimiento_hdr.tipo_movimiento_id` y en el
propio texto de `Observacion`. Esto es exactamente la separación de capas de §2: agregar un tipo de
negocio nuevo **no toca la base del kardex**.

**Anulación (`AnularAsync`):** idempotente; recolecta los asientos por `documento_id` de cada línea
posteada, postea una `Reversa` por línea (la reversa YA existe en el motor y ya sabe deshacer un
`AjustePositivo`/`AjusteNegativo`/`AjusteValor` sin cambios), marca `estado = 9` con
`anulado_por`/`fecha_anulacion`/`motivo_anulacion` en columnas propias.

**Orden de bloqueos** (mismo principio que el diseño supersedido, §7.3): el motor bloquea
`alm_articulo_bodega` en el orden que le pasan los pares; el servicio los resuelve **ordenados por
id** antes del bucle, para que dos documentos concurrentes que toquen los mismos artículos no
generen deadlock cruzado.

---

## 5. API y permisos

```
GET    api/almacen/tipos-movimiento                   View
POST   api/almacen/tipos-movimiento                   Create
PUT    api/almacen/tipos-movimiento/{id:int}          Edit
POST   api/almacen/tipos-movimiento/{id:int}/desactivar  Edit

GET    api/almacen/movimientos                        View
GET    api/almacen/movimientos/{id:int}               View
POST   api/almacen/movimientos                        Create
POST   api/almacen/movimientos/{id:int}/anular        Edit
```

Nuevos recursos en `PermissionResources.Inventario` (mismo patrón que `CargaInicial` / `Ajustes`,
`SIAD.Core/Constants/PermissionNames.cs:196-202`):

```csharp
public static class Movimientos
{
    public const string View = "module.inventario.movimientos.view";
    public const string Create = "module.inventario.movimientos.create";
    public const string Edit = "module.inventario.movimientos.edit";
}
public static class TiposMovimiento   // mantenimiento del catálogo — más sensible que capturar
{
    public const string View = "module.inventario.tipos_movimiento.view";
    public const string Create = "module.inventario.tipos_movimiento.create";
    public const string Edit = "module.inventario.tipos_movimiento.edit";
}
```

Registrar ambos en `PermissionEndpointCatalog.Inventario` (`SIAD.Core/Constants/PermissionEndpointCatalog.cs:615-659`),
siguiendo exactamente el patrón `Resource: "movimientos__almacen_movimientos"`.

`requiere_autorizacion` del tipo de movimiento **no crea un permiso nuevo por tipo** — sería
imposible de mantener. Se resuelve con **un** permiso adicional
(`module.inventario.movimientos.autorizar_sensibles`) que el servicio exige **solo** cuando
`tipo.requiere_autorizacion = true`; quien no lo tiene puede capturar los tipos ordinarios y el
servicio rechaza los marcados, con mensaje explícito.

---

## 6. UI

Siguen el [estándar de grid](../../.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md)
(referencia `ClientesList.razor`), mismo criterio que toda pantalla nueva del módulo.

| Pantalla | Ruta | Notas |
|---|---|---|
| `TiposMovimientoList.razor` | `/almacen/tipos-movimiento` | Mantenimiento del catálogo. Patrón `TiposArticuloList.razor`. Columna "En uso" (tiene movimientos posteados) deshabilita el cambio de `clase`. |
| `TipoMovimientoForm.razor` | modal desde la lista | Código, nombre, clase, cuenta contable (opcional), requiere autorización, activo. |
| `MovimientosAlmacenList.razor` | `/almacen/movimientos` | Grid de cabeceras. Filtros: tipo, bodega, fecha, estado. KPI: movimientos del mes, valor entradas, valor salidas. |
| `MovimientoAlmacenFormPage.razor` | `/almacen/movimientos/nuevo` | Cabecera (tipo, bodega, fecha, motivo) + grilla de líneas (artículo, cantidad, costo si aplica). El costo se oculta/deshabilita cuando `tipo.clase = SALIDA` (se valoriza al promedio, igual que hoy). Patrón `OrdenCompraFormPage.razor`. |

**El atajo de la ficha del artículo cambia de destino.** `ArticuloUbicacionesTab.razor:396` hoy
abre el ajuste de una línea; pasa a abrir `MovimientoAlmacenFormPage` con la bodega y el artículo
precargados y un renglón ya puesto — una línea sigue siendo el caso más común, el documento nuevo
solo permite agregar más si hace falta.

---

## 7. Plan de pruebas (QA)

Suite de integración contra Postgres real (`SIAD_TEST_DB`), estilo `InventarioPostingTests.cs`.

**`TipoMovimientoServiceTests`**
1. Crear tipo con código duplicado en la misma empresa → rechaza.
2. Mismo código en dos empresas distintas → ambos se crean (multiempresa real).
3. Clase inválida (`"ENTRADA_X"`) → rechaza (el `CHECK` es la última red, el servicio la primera).
4. Desactivar un tipo sin movimientos → éxito.
5. Cambiar `clase` de un tipo con movimientos posteados → rechaza.
6. Tipo inactivo no aparece en `GetAsync(soloActivos: true)` pero sí en `false`.

**`MovimientoAlmacenTests`**
7. Documento de una línea, clase `ENTRADA` → existencia sube, costo promedio recalcula, `costo_real` = costo tecleado.
8. Documento de tres líneas, mezclando artículos distintos, mismo tipo `SALIDA` → tres asientos, un solo documento, `total` = suma.
9. Reintento con el mismo `Uuid` de cabecera → no duplica el documento ni las líneas (idempotencia).
10. Tipo inactivo → rechaza antes de tocar la BD.
11. Tipo de otra empresa (cross-tenant) → rechaza (prueba de fuga de tenant).
12. Artículo sin ubicación en la bodega indicada → rechaza con mensaje accionable, no crea el par.
13. Línea `SALIDA` que dejaría existencia negativa → rechaza (guarda ya existente del motor, verificar que se propaga).
14. Motivo vacío o solo espacios → rechaza.
15. `requiere_autorizacion = true` sin el permiso → 403 desde el controller, no 500.

**`MovimientoAlmacenAnulacionTests`**
16. Anular un documento posteado → reversa por línea, existencia vuelve al estado previo, `estado = 9`.
17. Anular dos veces (mismo id) → idempotente, no duplica la reversa.
18. Anular un documento con `clase = VALOR` → reversa restituye el costo promedio anterior (usa la misma reversa espejo del motor).

**Regresión obligatoria:** `InventarioPostingTests`, `AjusteInventarioTests` (si existen) y toda la
suite de `Almacen` en verde — el motor no cambió, pero el catálogo es una superficie nueva que
puede introducir un `NotSupportedException` no capturado si algún `clase` no mapea.

---

## 8. Plan por fases

### Fase 1 — Catálogo (BD + servicio)
- `alm_tipo_movimiento` + semilla (§3.5).
- `ITipoMovimientoService` + controller + permisos.
- **Verificable:** `TipoMovimientoServiceTests` en verde.

### Fase 2 — Documento genérico (BD + servicio + motor sin cambios) ✅ 2026-08-03
- `alm_movimiento_hdr`, `alm_movimiento_dtl`, `alm_movimiento_correlativo` — **aplicadas al mirror** (paso 28).
- `IMovimientoAlmacenService.CrearYPostearAsync` / `AnularAsync` — implementadas, con FK compuestas y guardas.
- Controlador `MovimientosAlmacenController` + permisos `module.inventario.movimientos.*` (incl. `autorizar_sensibles`).
- **Verificado:** `MovimientoAlmacenTests` (14) + `MovimientoAlmacenAnulacionTests` (6) en verde; regresión de `Almacen` **220/220**.
- **Dos hallazgos de las pruebas** (ver §10): un bug de idempotencia corregido, y una limitación del motor documentada.
- **Falta:** cliente HTTP, las 2 pantallas, el nodo de menú.

### Fase 3 — UI ✅ 2026-08-03 (falta prueba de humo logueada)
- `MovimientosAlmacenList.razor` (lista + anulación con motivo) y `MovimientoAlmacenFormPage.razor`
  (cabecera + grilla de renglones; la columna de costo se **deshabilita en `SALIDA`**, equivale al
  `PIDE_COSTO` de Centura) — **implementadas**. Las de tipos de movimiento son de la Fase 1.
- Cliente `MovimientosAlmacenClient` + registro en `CommonServices`. Nodo de menú en Almacén → Compras.
- `ArticuloListItemDto.ComboTexto` (NUEVA, computada, solo display) para el combo de artículos.
- **Verificado:** solución completa compila (0 errores); el portal arranca sin errores de consola ni
  de servidor. **Pendiente:** prueba de humo logueada (captura de un movimiento de 3 líneas de punta a
  punta) — bloqueada por el login, que hace el usuario.
- **NO hecho** (queda para después): el atajo desde `ArticuloUbicacionesTab.razor` sigue apuntando al
  ajuste de una línea; re-apuntarlo a esta pantalla es un pendiente menor.

### Fase 4 — Deprecación de `alm_ajuste_inventario` ✅ 2026-08-04 (congelar, opción del usuario)
- **Congelar, NO borrar** (sin cambio de BD): la tabla `alm_ajuste_inventario` y el histórico quedan
  legibles. No hizo falta el conteo de filas (Decisión 2): el freeze es seguro sea cual sea.
- **POST de captura retirado** de `AjustesInventarioController` (queda solo el GET del histórico).
  `AjusteInventarioClient.CrearAsync` eliminado; el servicio `AjusteInventarioService.CrearYPostearAsync`
  se conserva (lo usan los tests como helper de posteo), sin superficie HTTP.
- **Atajo re-apuntado:** el botón «Registrar ajuste» de `ArticuloUbicacionesTab` ahora navega a
  `/almacen/movimientos/nuevo?bodega=&articulo=` (el form de movimiento precarga bodega + renglón; el
  usuario elige el concepto). Se eliminó el modal de ajuste de una línea de la ficha.
- **Verificado:** solución compila 0 errores. Toda captura de existencia pasa por
  `IMovimientoAlmacenService`.

### Fase 5 — Traslado entre bodegas (fuera de esta entrega, decisión ya tomada por el usuario)
- Nueva clase `TRASLADO` en el catálogo.
- `TipoMovimientoInventario.TrasladoSalida` / `TrasladoEntrada` (NUEVOS valores, motor).
- `alm_movimiento_hdr.bodega_destino_id` (NUEVA columna, nullable, solo para `clase = TRASLADO`).
- Modelo con tránsito, dos pasos (decisión ya tomada): envío descarga origen y carga
  `existencia_transito` en destino (columna **ya existe**, sin escritor hoy); recepción confirma
  entrada en destino y libera el tránsito. Doble `FOR UPDATE` en orden ascendente de
  `alm_articulo_bodega.id` para no producir deadlock cruzado (mismo principio de §4.2).

### Fase 6 — Flujo de dos actores (requisición → aprobación → descargo)
- Explícitamente fuera de esta entrega. Cuando se retome, `README_requisiciones_descargos.md`
  §3-5 (el mapeo Centura y las reglas de negocio levantadas) **sigue siendo la fuente correcta**;
  solo su arquitectura de datos (§6-12) queda descartada a favor de decidir, en ese momento, si
  vive como un `tipo_movimiento` de `clase SALIDA` con un flujo de aprobación propio por encima, o
  como documento aparte. No se decide en este documento.

---

## 10. Hallazgos de QA (2026-08-03)

**Bug de idempotencia corregido.** La primera versión de `CrearYPostearAsync` insertaba todos los
renglones con `uuid = Guid.Empty` y recién después derivaba el uuid real de `alm_movimiento_dtl.id`.
Con una sola línea pasaba; con dos o más, las líneas colisionaban contra
`uq_alm_movimiento_dtl_uuid (company_id, uuid)` (`23505`). La prueba de tres líneas lo destapó. Se
corrigió derivando el uuid de `(company_id, hdr.uuid, posición)` — datos conocidos **antes** del
INSERT—, lo que además ahorró un `SaveChanges`. La idempotencia se mantiene: un reintento con el
mismo `Uuid` de cabecera reproduce los mismos uuid de renglón.

**Limitación del motor documentada (no corregida — fuera de alcance).** Anular un movimiento de
clase `VALOR` **no restituye el costo promedio anterior**: `InventarioPostingService.Aplicar`
(rama `AjusteValor`) graba el costo nuevo sin guardar el previo, así que la reversa no tiene de
dónde sacarlo. Es exactamente el defecto de `2026-08-01-costeo-articulo-diseno.md` §3, corrección 1,
cuya solución (`alm_kardex.costo_promedio_anterior`) es la Fase B de ESE diseño. La prueba
`Anular_CorreccionDeValor_..._LimitacionConocida` fija el comportamiento actual (existencia sí
vuelve, promedio no) y deja escrito qué cambiar cuando la Fase B entre.

---

## 9. Decisiones pendientes del usuario

| # | Decisión | Recomendación |
|---|---|---|
| **1** | ¿Se ejecuta ya el rollback de `alm_requisicion_hdr` / `alm_descargo_hdr` en el mirror (bloque ya escrito en `Database/2026-08-01_alm_requisicion_descargo.sql:440-463`), o se deja la tabla aplicada pero sin nuevas filas hasta decidir la Fase 6? | Ejecutar el rollback ahora: una tabla aplicada y sin dueño es más riesgo (alguien podría escribirle por error) que volver a aplicarla el día que se retome la Fase 6. |
| **2** | ¿Cuántas filas tiene hoy `alm_ajuste_inventario`? Determina si se congela como histórico o se migra. | Consultarlo antes de la Fase 4: `SELECT count(*) FROM alm_ajuste_inventario;` |
| **3** | Semilla inicial del catálogo (§3.5): ¿los 3 tipos mínimos alcanzan para arrancar, o el usuario ya tiene en mente tipos adicionales (donación, consumo interno, merma por vencimiento) que conviene sembrar desde el mismo script? | Empezar con los 3 mínimos; agregar el resto desde la pantalla una vez esté construida — es justamente el punto de que sea configurable. |
| **4** | ¿El permiso `autorizar_sensibles` (§5) es suficiente segregación de funciones para los tipos marcados `requiere_autorizacion`, o hace falta que el creador y el autorizador sean personas distintas (como en la aprobación de requisiciones)? | Suficiente para esta entrega: un movimiento de catálogo (merma, donación) no tiene el mismo perfil de riesgo que una salida de bodega completa. Si se requiere segregación estricta, es la Fase 6. |
