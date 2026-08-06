# Traslado entre bodegas con tránsito y recepción parcial — Diseño

Fecha: 2026-08-04
Estado: **diseño aprobado en decisiones clave, sin implementar.**

Fase 5 de [`2026-08-01-movimientos-almacen-catalogo-diseno.md`](2026-08-01-movimientos-almacen-catalogo-diseno.md).
Es el flujo **más usado del legacy**: 62.599 asientos entre `TFE`/`TFS` en `INV_KARDEX` de MERENDON,
más que la facturación (ver [`README_entradas_salidas_almacen.md`](../centura-flujos/README_entradas_salidas_almacen.md) §3.1 y §4.1).

> ### Decisiones tomadas por el usuario (2026-08-04) — leer antes que el resto
>
> 1. **Modelo con tránsito, dos pasos.** No es instantáneo como Centura: la mercadería sale de
>    origen, viaja (queda en `existencia_transito` de destino), y entra a destino en un acto
>    posterior de recepción. Refleja mercadería en camino entre sucursales.
> 2. **Recepción PARCIAL por renglón.** Un envío se puede recibir en varias tandas. Cada acto de
>    recepción es un evento propio con su fecha, usuario y asientos. El traslado queda «En tránsito»
>    mientras quede algo por recibir y pasa a «Recibido» cuando todo llegó.
> 3. **Tipo de catálogo nuevo `TRF` «Traslado entre bodegas»** (clase `TRASLADO`). `TFE`/`TFS` de la
>    semilla quedan **inactivos** como histórico del legacy — no se usan para captura nueva. Un
>    traslado es un documento con **un** tipo, aunque genere asientos en dos bodegas.
> 4. **Los dos modos coexisten, elegidos por un interruptor en el propio traslado** (columna
>    `requiere_recepcion` en el hdr): «con recepción» = dos pasos con tránsito (decisión 1);
>    «directo» = un solo paso, la mercadería sale de origen y entra a destino en el mismo instante,
>    como Centura. **El directo NO es un camino aparte del motor:** es `envío + recepción total
>    automática` ejecutados en la misma transacción, reutilizando exactamente la misma maquinaria
>    (§4.4). Así no hay caso especial en el motor ni riesgo de colisión de uuid entre modos.

---

## 1. Modelo conceptual

Un traslado es un documento `alm_movimiento_hdr` de **clase nueva `TRASLADO`**, con
`bodega_id` = origen y `bodega_destino_id` = destino. Vive en dos actos, análogo a
**orden de compra → recepciones** y a **requisición → descargos**:

```
  ENVÍO (despacho)                          RECEPCIÓN(es) — una o varias, parciales
  ┌───────────────────────────┐            ┌────────────────────────────────────────┐
  │ por cada renglón:          │            │ por cada renglón recibido:              │
  │  · sale de ORIGEN al costo │            │  · libera existencia_transito de destino│
  │    promedio vigente        │  tránsito  │  · entra a existencia real de DESTINO    │
  │    → asiento kardex TRASLADO│  ───────▶  │    al costo con que salió de origen      │
  │    (salidas>0) en origen   │            │    → asiento kardex TRASLADO (ingresos>0) │
  │  · += existencia_transito  │            │    en destino                            │
  │    de DESTINO              │            │  · += cantidad_recibida del renglón      │
  └───────────────────────────┘            └────────────────────────────────────────┘
   estado → EN TRÁNSITO (2)                  estado → EN TRÁNSITO mientras falte algo;
                                              RECIBIDO (3) cuando todo llegó
```

**El costo viaja con la mercadería.** Sale de origen al costo promedio vigente de origen; ese costo
se congela en el renglón (`alm_movimiento_dtl.costo_real`) y es el costo con que entra a destino.
Es lo que hace Centura (`GA_IN.APT`, «El costo viaja con la mercadería»,
[README §4.1](../centura-flujos/README_entradas_salidas_almacen.md)) y evita que el promedio de
destino se calcule con un costo distinto al que efectivamente salió.

**Mientras viaja, la mercadería no cuenta como existencia disponible en ninguna bodega.** Salió de
la existencia real de origen y aún no entró a la de destino; queda en `existencia_transito` de
destino (columna que existe desde `2026-07-13_alm_articulo_bodega_comprometido_transito_costos.sql`
y **nunca tuvo escritor** — este diseño es su primer productor). El rollup del artículo
(`ArticuloRollupService`) suma solo existencia real de ubicaciones activas, así que el total baja
mientras dura el tránsito. Es el comportamiento correcto de un modelo con tránsito.

### Por qué el tránsito NO es un asiento del kardex

El kardex (`alm_kardex`) es el libro de la **existencia real** de un par (artículo, bodega). La
mercadería en tránsito no es existencia real de destino todavía. Igual que
`existencia_comprometida` (reserva por requisición), `existencia_transito` es un **saldo
materializado** que mantiene el servicio, no el motor de kardex. Así, el traslado genera
exactamente **dos asientos por unidad trasladada** —salida de origen en el envío, entrada a destino
en la recepción—, uno por bodega, que es justo lo que dice el comentario ya escrito en
`alm_kardex.bodega_destino_id`. Entre ambos, el puente es `existencia_transito`.

---

## 2. Modelo de datos

### 2.1 `alm_movimiento_hdr` — columnas nuevas (ALTER)

```sql
ALTER TABLE alm_movimiento_hdr
    ADD COLUMN IF NOT EXISTS bodega_destino_id  INTEGER NULL,
    ADD COLUMN IF NOT EXISTS requiere_recepcion BOOLEAN NOT NULL DEFAULT true,  -- true = dos pasos; false = directo
    ADD COLUMN IF NOT EXISTS recibido_por       VARCHAR(100) NULL,   -- de la ÚLTIMA recepción
    ADD COLUMN IF NOT EXISTS fecha_recepcion    TIMESTAMP WITHOUT TIME ZONE NULL;
```

- **`bodega_destino_id`** — destino del traslado. NULL para entrada/salida normal; obligatorio para
  clase `TRASLADO`. FK compuesta tenant-safe a `alm_bodega (company_id, id)`.
- **`requiere_recepcion`** — el interruptor del modo (decisión 4). `true` = con recepción (dos
  pasos, nace `En tránsito`); `false` = directo (un paso, nace `Recibido` vía recepción automática,
  §4.4). Irrelevante para entrada/salida normal (no traslado). DEFAULT `true` = el modo más seguro
  como fallback; el servicio lo setea siempre explícito según el interruptor de la pantalla.
- Se amplían dos CHECK:
  - `ck_alm_movimiento_hdr_estado`: de `(1, 9)` a `(1, 2, 3, 9)`.
  - Nuevo `ck_alm_movimiento_hdr_traslado`: `bodega_destino_id` obligatorio y distinto de
    `bodega_id` **solo** cuando el documento es un traslado. Como el CHECK no puede leer la clase
    (está en otra tabla), se plantea sobre el propio dato: `bodega_destino_id IS NULL OR
    bodega_destino_id <> bodega_id`. La obligatoriedad real («si es TRASLADO, destino no es NULL»)
    la garantiza el **servicio** (la clase vive en `alm_tipo_movimiento`), y una prueba la fija.

Estados (constante `EstadoMovimientoAlmacen`, se amplía):

| Valor | Nombre | Aplica a |
|---|---|---|
| 1 | Registrado | entrada / salida normal (posteada de una) |
| 2 | En tránsito | traslado **con recepción** enviado, con algo aún por recibir |
| 3 | Recibido | traslado con todo recibido — un traslado **directo** nace aquí (recepción automática, §4.4) |
| 9 | Anulado | cualquiera |

> No se agrega un estado «recibido parcial» separado: **En tránsito** cubre «nada recibido» y
> «parcialmente recibido». Lo parcial se deriva de las cantidades por renglón (§2.2). Mantener el
> CHECK con cuatro valores en vez de cinco evita un estado que no cambia ninguna regla.

### 2.2 `alm_movimiento_dtl` — columna nueva (ALTER)

```sql
ALTER TABLE alm_movimiento_dtl
    ADD COLUMN IF NOT EXISTS cantidad_recibida NUMERIC(15,2) NOT NULL DEFAULT 0;
ALTER TABLE alm_movimiento_dtl
    ADD CONSTRAINT ck_alm_movimiento_dtl_recibida
        CHECK (cantidad_recibida >= 0 AND cantidad_recibida <= cantidad);
```

- **`cantidad`** = cantidad enviada (lo que salió de origen).
- **`cantidad_recibida`** = suma de lo ya recibido en destino (crece con cada recepción parcial).
- **En tránsito** de ese renglón = `cantidad - cantidad_recibida`.
- `kardex_id` (existente) = asiento de **salida de origen** (envío).
- `costo_real` (existente) = costo con que salió = costo con que entra a destino.

### 2.2b Backstop de tránsito no-negativo (ALTER sobre `alm_articulo_bodega`)

```sql
ALTER TABLE alm_articulo_bodega
    ADD CONSTRAINT ck_alm_articulo_bodega_transito_no_neg CHECK (existencia_transito >= 0);
```

`existencia_transito` la mueve el servicio (no el motor) con `UPDATE` crudo, sin uuid: es la única
escritura no idempotente del flujo. Un CHECK `>= 0` es la red de última hora que convierte cualquier
descarga de más (por una carrera no cubierta) en un abort atómico en vez de un saldo negativo
silencioso (hallazgos R-1, R-9). **No sustituye** al bloqueo correcto (§4.2/§4.3); lo respalda.

### 2.3 `alm_traslado_recepcion` — cabecera de un acto de recepción (NUEVA)

```sql
CREATE TABLE alm_traslado_recepcion (
    id                 SERIAL       PRIMARY KEY,
    company_id         BIGINT       NOT NULL,
    movimiento_hdr_id  INTEGER      NOT NULL,          -- el traslado que se recibe
    fecha              DATE         NOT NULL,
    observaciones      VARCHAR(500) NULL,
    uuid               UUID         NOT NULL,           -- idempotencia del acto de recepción
    usuariocreacion    VARCHAR(100) NULL,
    fechacreacion      TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT uq_alm_traslado_recepcion_tenant UNIQUE (company_id, id),
    CONSTRAINT uq_alm_traslado_recepcion_uuid   UNIQUE (company_id, uuid),
    CONSTRAINT fk_alm_traslado_recepcion_hdr
        FOREIGN KEY (company_id, movimiento_hdr_id)
        REFERENCES alm_movimiento_hdr (company_id, id) ON DELETE CASCADE
);
```

### 2.4 `alm_traslado_recepcion_dtl` — renglón recibido, unidad de posteo de la entrada (NUEVA)

```sql
CREATE TABLE alm_traslado_recepcion_dtl (
    id                INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id        BIGINT        NOT NULL,
    recepcion_id      INTEGER       NOT NULL,           -- alm_traslado_recepcion
    movimiento_dtl_id INTEGER       NOT NULL,           -- renglón del traslado que se recibe
    articulo_id       INTEGER       NOT NULL,
    cantidad          NUMERIC(15,2) NOT NULL,           -- lo recibido en ESTE acto
    costo_real        NUMERIC(12,4) NOT NULL,           -- copiado del renglón del traslado
    total             NUMERIC(14,2) NOT NULL DEFAULT 0,
    kardex_id         INTEGER       NULL,               -- asiento de ENTRADA a destino
    uuid              UUID          NOT NULL,
    CONSTRAINT uq_alm_traslado_recepcion_dtl_uuid   UNIQUE (company_id, uuid),
    CONSTRAINT uq_alm_traslado_recepcion_dtl_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_traslado_recepcion_dtl_cant   CHECK (cantidad > 0),
    CONSTRAINT fk_alm_traslado_recepcion_dtl_rec
        FOREIGN KEY (company_id, recepcion_id)
        REFERENCES alm_traslado_recepcion (company_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_alm_traslado_recepcion_dtl_dtl
        FOREIGN KEY (company_id, movimiento_dtl_id)
        REFERENCES alm_movimiento_dtl (company_id, id) ON DELETE RESTRICT,
    -- FK tenant-safe del artículo, igual que la tabla hermana alm_movimiento_dtl (hallazgo R-7):
    CONSTRAINT fk_alm_traslado_recepcion_dtl_articulo
        FOREIGN KEY (company_id, articulo_id)
        REFERENCES alm_articulo (company_id, id) ON DELETE RESTRICT
);
CREATE INDEX ix_alm_traslado_recepcion_dtl_articulo ON alm_traslado_recepcion_dtl (company_id, articulo_id);
CREATE INDEX ix_alm_traslado_recepcion_dtl_rec ON alm_traslado_recepcion_dtl (company_id, recepcion_id);
```

> **Ambas entidades nuevas implementan `SIAD.Core.Tenancy.ICompanyScopedEntity`** (hallazgo R-13): la
> columna `company_id` y las FK no bastan — el aislamiento de lectura y el estampado en INSERT los da
> el global query filter, que solo cubre entidades que implementan la interfaz. Se agrega un test de
> lectura cross-tenant directa sobre las dos tablas.

**La unidad de posteo de la entrada es el renglón de recepción, no el del traslado.** Es el mismo
patrón, y por la misma razón, que `SalidaDescargo`: «una misma línea de requisición se entrega en
varios descargos». Aquí, un renglón de traslado se recibe en varias recepciones; anclar la
idempotencia de la entrada a la línea del traslado haría que la segunda recepción se tomara por un
reintento de la primera y entrara mercadería sin asiento.

### 2.5 Catálogo: clase `TRASLADO` y el tipo `TRF`

```sql
-- Ampliar el CHECK de clase
ALTER TABLE alm_tipo_movimiento DROP CONSTRAINT ck_alm_tipo_movimiento_clase;
ALTER TABLE alm_tipo_movimiento ADD  CONSTRAINT ck_alm_tipo_movimiento_clase
    CHECK (clase IN ('ENTRADA','SALIDA','VALOR','TRASLADO'));

-- Sembrar el tipo TRF por empresa con bodegas (ON CONFLICT DO NOTHING)
INSERT INTO alm_tipo_movimiento (company_id, codigo, nombre, clase, activo, orden, usuariocreacion, fechacreacion)
SELECT DISTINCT company_id, 'TRF', 'TRASLADO ENTRE BODEGAS', 'TRASLADO', true, 0, 'seed-fase5', (now() AT TIME ZONE 'utc')
FROM alm_bodega
ON CONFLICT (company_id, codigo) DO NOTHING;
```

**Vocabulario de clase — NO sobrecargar `ClaseAjusteInventario`** (hallazgo R-6). `ClaseAjusteInventario`
es el espejo declarado del CHECK de **otra** tabla (`ck_alm_ajuste_inventario_clase`, que sigue en
`ENTRADA/SALIDA/VALOR`) y lo consume `AjusteInventarioService` (ajuste de una línea). Si se le
agregara `TRASLADO`, `AjusteInventarioService.EsValida` aceptaría un valor que su tabla rechaza →
`23514` crudo. En su lugar se crea un vocabulario propio del catálogo de movimientos:

```csharp
public static class ClaseMovimientoInventario   // espejo de ck_alm_tipo_movimiento_clase
{
    public const string Entrada  = "ENTRADA";
    public const string Salida   = "SALIDA";
    public const string Valor    = "VALOR";
    public const string Traslado = "TRASLADO";
    public static readonly string[] Todas = [Entrada, Salida, Valor, Traslado];
    public static bool EsValida(string? c) => !string.IsNullOrWhiteSpace(c) && Array.IndexOf(Todas, c) >= 0;
}
```

`TipoMovimientoService` (validación de `clase`), `MovimientoAlmacenService` y el nuevo
`TrasladoAlmacenService` migran sus referencias de `ClaseAjusteInventario.*` a
`ClaseMovimientoInventario.*`. `ClaseAjusteInventario` queda **intacto en 3 valores** para el path de
ajustes. Ampliar `ck_alm_ajuste_inventario_clase` **no** es la salida: `TRASLADO` no es un ajuste.

---

## 3. Motor (`InventarioPostingService`) — dos valores de clase nuevos

Hasta hoy el motor «no se tocaba». La Fase 5 lo extiende con dos clases; el resto de su contrato
(idempotencia por uuid, `FOR UPDATE`, kardex inmutable, reversa) no cambia.

```csharp
public enum TipoMovimientoInventario
{
    // ... 1..8 sin cambios ...
    TrasladoSalida  = 9,   // envío: sale de ORIGEN
    TrasladoEntrada = 10   // recepción: entra a DESTINO
}
```

`MovimientoInventarioDto` gana **`int? BodegaDestinoId`** (solo informativo; se copia al asiento).

| Aspecto | `TrasladoSalida` | `TrasladoEntrada` |
|---|---|---|
| Cálculo | = `AjusteNegativo`: resta existencia, **no** cambia promedio, sale al promedio vigente | = `AjustePositivo`: suma existencia, promedio ponderado móvil |
| `documento_tipo` | `TRASLADO` (fijo, como Compra/Descargo) | `TRASLADO` (fijo) |
| `bodega_destino_id` en asiento | destino | origen (informativo) |
| `es_ajuste` | `false` | `false` |
| guardas | cantidad>0, existencia suficiente, costo promedio>0 (no despachar a costo 0) | cantidad>0, costo>0 |
| uuid | `TRASLADO_SALIDA\|company\|movimiento_dtl_id\|articuloBodegaId` | `TRASLADO_ENTRADA\|company\|recepcion_dtl_id\|articuloBodegaId` |
| `documento_id` | `alm_movimiento_dtl.id` (renglón del traslado) | `alm_traslado_recepcion_dtl.id` (renglón de recepción) |

**Reversa espejo.** El motor decide la dirección de una reversa por `original.documento_tipo`. Hoy:
`Descargo` → devolver (entra). Para `TRASLADO`, un asiento puede ser salida (envío) o entrada
(recepción); se discrimina por `original.salidas > 0`:

```csharp
var esDevolucion =
    original?.documento_tipo == TipoDocumentoInventario.Descargo
    || (original?.documento_tipo == TipoDocumentoInventario.Traslado && original.salidas > 0m);
```

Es seguro mirar `salidas` aquí (a diferencia de `CargaInicialReconciliacion`, que escribe
`ingresos>0` sin mover existencia): **todo** asiento de traslado mueve existencia real.

**Son TRES los sitios que discriminan por `documento_tipo == Descargo` y hay que extender a
`Traslado`, no dos** (hallazgo R-2, crítico). El diseño original listaba solo `Calcular` y
`TipoTransaccionDe` y omitía el tercero:

1. `Calcular`, rama `Reversa` (`InventarioPostingService.cs:480`) — dirección del cálculo.
2. `TipoTransaccionDe` (`:517`) — código 102/202 del asiento.
3. **`ValidarAsync`, la guarda de negativo (`:371-378`)** — `if (original.documento_tipo != Descargo
   && fila.existencia - original.cantidad < 0m) throw`. Revertir una `TrasladoSalida` es una
   **devolución** que SUMA a origen; pero como `Traslado != Descargo`, la guarda se evalúa sobre
   origen, que **ya despachó** su stock. Trasladar el grueso del stock (origen queda en 0) haría que
   anular fallara SIEMPRE con «la mercadería ya salió». Es intermitente según stock: pasa en pruebas
   con stock holgado y falla en producción. **Debe usar la misma `esDevolucion`:**
   `if (!esDevolucion && fila.existencia - original.cantidad < 0m) throw …`.

Los tres sitios usan exactamente la misma expresión `esDevolucion`.

---

## 4. Servicio

Servicio propio `TrasladoAlmacenService` (`ITrasladoAlmacenService`): el flujo de dos actos y la
recepción parcial son bastante distintos de la captura de una sola vía de `MovimientoAlmacenService`.
Reutiliza `alm_movimiento_hdr/dtl`, el correlativo y el motor.

```csharp
public interface ITrasladoAlmacenService
{
    Task<IReadOnlyList<TrasladoListItemDto>> GetAsync(TrasladoFilterDto? f, CancellationToken ct = default);
    Task<TrasladoDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Crea el documento en EN TRÁNSITO, postea la salida de origen de cada renglón y
    /// carga el tránsito de destino. Todo o nada. Idempotente por uuid de cabecera.</summary>
    Task<TrasladoDto> EnviarAsync(TrasladoDto dto, string user, CancellationToken ct = default);

    /// <summary>Recibe una tanda: por cada renglón indicado, libera tránsito y entra a destino al
    /// costo congelado. Suma cantidad_recibida; si el traslado queda completo, pasa a RECIBIDO.
    /// Idempotente por uuid del acto de recepción.</summary>
    Task<TrasladoDto> RecibirAsync(int trasladoId, RecepcionTrasladoDto dto, string user, CancellationToken ct = default);

    /// <summary>Revierte según estado: en tránsito revierte solo la salida de origen y descarga el
    /// tránsito pendiente; recibido/parcial revierte además las entradas a destino (guarda de
    /// negativo). Idempotente.</summary>
    Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default);
}
```

### 4.1 `EnviarAsync` (concurrencia clave)

1. Valida: tipo clase `TRASLADO` activo, origen ≠ destino, ambas bodegas activas, ≥1 renglón,
   cantidades > 0, sin artículo repetido, **y `dto.Uuid` presente** (contrato de idempotencia,
   hallazgo R-16: el servicio rechaza un uuid nulo en `EnviarAsync` en vez de caer en
   `Guid.NewGuid()`; la UI genera uno estable por operación). Corte de idempotencia primero: si ya
   existe un hdr con ese uuid, se devuelve sin re-postear.
2. Resuelve pares de **origen** y de **destino** para cada artículo. **Rechaza el par si está
   inactivo** (`activo = false`), en origen o en destino (hallazgo R-11: el rollup solo suma pares
   activos, así que trasladar hacia/desde un par inactivo descuadraría el total del artículo). Si el
   artículo no tiene ubicación en destino, **se crea** el par en destino con
   `INSERT … ON CONFLICT (company_id, articulo_id, bodega_id) DO NOTHING` y luego se relee
   (hallazgo R-12: dos envíos concurrentes del mismo artículo a la misma bodega convergen a la misma
   fila sin `23505` crudo); nace `activo = true`, existencia 0. En origen debe existir con existencia
   suficiente.
3. **Pre-bloquea las filas `alm_articulo_bodega` de origen y destino ORDENADAS por `id` ascendente**
   (`SELECT … FOR UPDATE`) dentro de la transacción del servicio, **antes** de postear. El motor,
   al postear cada línea, re-pide `FOR UPDATE` de la fila de origen: como ya está tomada en la misma
   transacción ambiente (`TransaccionAmbiente` no abre otra), es un no-op. Ese orden estable evita el
   deadlock cruzado entre dos traslados A→B y B→A simultáneos.
4. Crea `hdr` (estado `EnTransito`, `bodega_destino_id`) y sus `dtl` (cantidad enviada,
   `cantidad_recibida = 0`).
5. Por cada renglón: motor `TrasladoSalida` sobre el par de origen (asiento salida, `bodega_destino_id`
   = destino); `costo_real` del renglón = costo del asiento; `existencia_transito` de destino += cantidad.
6. **Rollup por artículo una sola vez, al final, en orden ascendente de `articulo_id`** (hallazgo
   R-8): `ArticuloRollupService.RecomputeAsync` toma un candado de fila de `alm_articulo` hasta el
   commit; llamarlo por renglón y en orden variable abre un deadlock ABBA sobre `alm_articulo` entre
   traslados multi-bodega (que es la norma aquí). Acumular los `articulo_id` afectados y recomputarlos
   ordenados cierra el deadlock y evita rollups redundantes. **El motor no debe recomputar por línea
   en el flujo de traslado**; lo hace el servicio al final (requiere un modo del motor que no
   recompute, o recomputar centralizado aquí).
7. Un `SaveChanges`, commit de la transacción ambiente.

Si `requiere_recepcion == false` (directo), tras el paso 6 se encadena la recepción total automática
(§4.4) en la MISMA transacción, antes del commit.

### 4.2 `RecibirAsync` (el punto más delicado — reescrito tras la revisión)

El `RecepcionTrasladoDto` trae el **uuid del acto** (estable, lo genera la UI) y las cantidades por
renglón. El orden de pasos es crítico: **todo lo que decide (estado, pendiente, completitud) se lee
BAJO candado del documento**, no antes (hallazgos R-1, R-3, R-5, R-15).

1. **Corte de idempotencia primero:** buscar `alm_traslado_recepcion` por `(company_id, uuid del
   acto)`; si existe, devolver el traslado sin re-postear (mismo patrón que
   `MovimientoAlmacenService.CrearYPostearAsync:163-168`). El `UNIQUE(company_id, uuid)` es solo la
   red de última hora, no el mecanismo.
2. **`SELECT … FOR UPDATE` de `alm_movimiento_hdr` del traslado** (y de sus `alm_movimiento_dtl`)
   como PRIMER candado, antes de validar nada. Esto serializa todas las recepciones y anulaciones
   del mismo traslado (cierra el TOCTOU y el «traslado pegado en tránsito» de dos recepciones
   concurrentes de renglones distintos). **Re-leer `estado` y `cantidad_recibida` bajo este candado.**
   El traslado debe estar `EnTransito`.
3. **Validar pertenencia** (hallazgo R-10): cada `movimiento_dtl_id` del DTO debe pertenecer al
   traslado cargado y su hdr ser clase `TRASLADO`; resolver los renglones **dentro** de
   `hdr.lineas`, no por id contra el contexto. Rechazar líneas foráneas.
4. Con `cantidad_recibida` ya releída bajo lock: validar `cantidad <= (cantidad - cantidad_recibida)`
   (no sobre-recibir), `cantidad > 0`, par de destino activo.
5. Pre-bloquea las filas `alm_articulo_bodega` de destino por `id` asc.
6. Crea `alm_traslado_recepcion` (con el uuid del acto) y sus `_dtl`.
7. Por cada `_dtl`: `existencia_transito` de destino −= cantidad (el CHECK `>= 0` es backstop); motor
   `TrasladoEntrada` sobre el par de destino al `costo_real` del renglón del traslado (asiento
   entrada, `documento_id = recepcion_dtl.id`); **incrementar `cantidad_recibida` con delta en SQL**
   (`SET cantidad_recibida = cantidad_recibida + :n`) para que el CHECK
   `cantidad_recibida <= cantidad` sea un backstop real y no un valor absoluto calculado en memoria
   (hallazgo R-3).
8. **Completitud bajo el candado del paso 2**, con SQL fresco:
   `NOT EXISTS (renglón del traslado con cantidad_recibida < cantidad)` → `hdr.estado = Recibido`,
   `recibido_por`/`fecha_recepcion` sellados; si no, sigue `EnTransito`.
9. Rollup por artículo al final en orden ascendente (§4.1 paso 6). Un `SaveChanges`, commit.

### 4.3 `AnularAsync` (también reescrito por la revisión)

1. **`SELECT … FOR UPDATE` de `alm_movimiento_hdr` del traslado** como PRIMER paso; re-leer `estado`
   y `cantidad_recibida` bajo el candado (hallazgos R-1, R-9). Idempotente: si ya está `Anulado`,
   devolver true sin revertir.
2. **Pre-bloquear TODAS las filas `alm_articulo_bodega` afectadas —pares de origen y de destino de
   todos los renglones— ORDENADAS por `id` ascendente**, antes de revertir nada (hallazgo R-4: el
   diseño original revertía entradas-de-destino antes que salidas-de-origen, orden contrario al
   ascendente, y abría un deadlock ABBA contra un traslado inverso concurrente; pre-bloquear todo en
   orden ascendente vuelve indiferente el orden lógico de reversa).
3. Revierte cada asiento de **entrada** ya posteado (recepciones parciales), con la guarda de negativo
   en destino; **descarga el `existencia_transito` pendiente de destino** (= lo enviado − lo recibido,
   releído bajo lock; el CHECK `>= 0` es backstop); revierte cada asiento de **salida** de origen
   (devuelve la mercadería — la guarda de negativo debe saltarse por `esDevolucion`, §3 sitio 3).
4. `estado = Anulado`, sello de anulación. Commit.

- Idempotente (anular dos veces no revierte dos veces, garantizado por el candado + re-lectura de
  estado). Nunca `UPDATE` sobre el kardex.

### 4.4 Traslado directo (un paso) — `requiere_recepcion = false`

El directo **no es un camino nuevo**: `EnviarAsync` corre su flujo normal (crea el hdr, postea las
salidas de origen, carga el tránsito) y, **si `requiere_recepcion == false`, encadena de inmediato
una recepción total en la MISMA transacción** — crea un `alm_traslado_recepcion` de recepción
automática (`observaciones = "Recepción automática (traslado directo)"`) con un `_dtl` por la
cantidad completa de cada renglón, corre el mismo núcleo de `RecibirAsync` (libera tránsito, postea
la entrada a destino, sube `cantidad_recibida`) y deja el hdr en `Recibido`.

Consecuencias de reutilizar el mismo núcleo, no de duplicarlo:
- **El motor no distingue directo de con-recepción.** La entrada siempre se ancla a un
  `alm_traslado_recepcion_dtl`, así que el uuid `TRASLADO_ENTRADA|company|recepcion_dtl_id|par` es
  el mismo esquema en ambos modos y **no puede colisionar** con el de la salida.
- **El tránsito de destino sube y baja dentro de la misma transacción** (neto cero visible fuera).
  Es una escritura de más que se descarta al confirmar; se acepta a cambio de un único camino de
  código probado por las mismas pruebas.
- **Anular un directo = anular un traslado recibido**: revierte la entrada y la salida (§4.3), sin
  rama aparte.
- **Idempotencia:** el uuid de cabecera cubre el reintento del envío; como la recepción automática
  vive dentro del mismo `EnviarAsync`, un reintento devuelve el documento ya creado sin re-postear.

`MovimientoAlmacenService.CrearYPostearAsync` **rechaza** explícitamente la clase `TRASLADO` con
mensaje accionable («use la pantalla de Traslados»), en vez de caer en el `throw` genérico de
`TipoMovimientoDe`.

---

## 5. API y permisos

```
GET    api/almacen/traslados                    View
GET    api/almacen/traslados/{id:int}           View
POST   api/almacen/traslados                     Create   (enviar)
POST   api/almacen/traslados/{id:int}/recibir    Edit     (recepción parcial)
POST   api/almacen/traslados/{id:int}/anular      Edit
```

Recurso nuevo `PermissionResources.Inventario.Traslados` y permisos
`module.inventario.traslados.{view,create,edit}` (mismo patrón que `Movimientos`), registrados en
`PermissionEndpointCatalog`.

## 6. UI (estándar de grid, referencia `ClientesList.razor`)

| Pantalla | Ruta | Notas |
|---|---|---|
| `TrasladosList.razor` | `/almacen/traslados` | Grid de traslados con estado (En tránsito / Recibido / Anulado) y % recibido. KPIs: en tránsito, recibidos del mes, valor en tránsito. |
| `TrasladoFormPage.razor` | `/almacen/traslados/nuevo` | Origen, destino, fecha, motivo + grilla de renglones (artículo, cantidad, existencia de origen). El costo lo pone el sistema (promedio de origen). |
| Recepción | `/almacen/traslados/{id}` | Detalle con envío + recepciones; botón «Recibir» abre captura de cantidades por renglón (default = pendiente), permite recibir parcial. |

Menú: «Traslados entre bodegas» en Almacén, junto a «Movimientos de almacén».

---

## 7. Plan de pruebas (integración contra Postgres real)

**Motor** — `InventarioPostingTrasladoTests`
1. `TrasladoSalida` resta existencia de origen y no mueve el promedio; asiento `documento_tipo=TRASLADO`, `bodega_destino_id`=destino.
2. `TrasladoEntrada` suma existencia de destino con promedio ponderado al costo dado.
3. Reversa de `TrasladoSalida` devuelve a origen; reversa de `TrasladoEntrada` saca de destino (guarda de negativo).
4. `TrasladoSalida` que dejaría negativo en origen → rechaza.

**Servicio** — `TrasladoAlmacenTests`
5. Envío de 3 renglones → 3 asientos de salida, tránsito de destino cargado, estado En tránsito, existencia de origen baja.
6. Recepción total en un acto → entra todo a destino, tránsito en 0, estado Recibido, promedio de destino correcto.
7. Recepción parcial (2 de 3, y una cantidad parcial) → cantidad_recibida sube, sigue En tránsito, tránsito refleja lo pendiente; segunda recepción completa → Recibido.
8. Origen == destino → rechaza. Artículo sin ubicación en destino → crea el par en destino.
9. Recibir más de lo pendiente → rechaza. Recibir un traslado ya Recibido → rechaza / no-op.
10. Idempotencia: reenviar con el mismo uuid no duplica; re-recibir el mismo acto (uuid) no duplica.
11. Anular en tránsito → revierte salida de origen, descarga tránsito. Anular con recepción parcial → revierte entradas + salidas. Anular dos veces → idempotente.
12. Cross-tenant: tipo/bodega de otra empresa → rechaza.
13. `MovimientoAlmacenService.CrearYPostear` con clase TRASLADO → rechaza con mensaje.
14. **Directo (`requiere_recepcion=false`)**: en un solo `EnviarAsync` → existencia baja en origen y sube en destino, tránsito neto 0, estado `Recibido`, una recepción automática registrada, promedio de destino ponderado al costo de origen.
15. **Directo — anulación**: revierte entrada de destino y salida de origen; con la mercadería ya fuera de destino, la guarda de negativo rechaza. Idempotente.
16. **Directo — idempotencia**: reenviar con el mismo uuid de cabecera no vuelve a postear ni duplica la recepción automática.

**Pruebas nacidas de la revisión adversarial** (regresión de correctness):
17. **Anular tras trasladar TODO el stock de origen** (origen queda en 0) → la reversa de la salida NO debe fallar por la guarda de negativo (hallazgo R-2). Es el caso que rompe la implementación literal.
18. **Recibir‖Anular concurrentes** sobre el mismo traslado → sin tránsito negativo, sin unidades huérfanas; uno gana el candado del hdr y el otro relee estado (R-1).
19. **Dos recepciones del mismo renglón con actos distintos** (uuid distinto) → sin lost update: Σ recepciones = cantidad_recibida ≤ enviado; reintento del MISMO acto (uuid) → no-op (R-3, R-15).
20. **Dos recepciones concurrentes de renglones distintos que completan el traslado** → termina en `Recibido`, no pegado en tránsito (R-5).
21. **Traslado hacia/desde un par inactivo** → rechaza (R-11). **Alta concurrente del par destino** → converge sin `23505` (R-12).
22. **Clase TRASLADO no contamina el ajuste de una línea**: `AjusteInventarioService` sigue rechazando TRASLADO limpio (R-6). Lectura cross-tenant directa sobre las tablas de recepción → filtrada (R-13).

**Regresión:** toda la suite `Almacen` (hoy 220/220) sigue en verde — el motor gana ramas nuevas
pero no cambia las existentes.

---

## 8. Plan por sub-fases

| # | Entregable | Verificable con |
|---|---|---|
| **5.1** | Motor: enum + `BodegaDestinoId` en DTO + cálculo/uuid/reversa para TRASLADO **en los 3 sitios** (Calcular, TipoTransaccionDe y la guarda de ValidarAsync); `ClaseMovimientoInventario` nuevo | `InventarioPostingTrasladoTests` en verde (incl. prueba 17) |
| **5.2** | DDL (script `Database/2026-08-04_alm_traslado.sql`, **guardia-estructura-bd** + runbook paso 29): columnas hdr/dtl, 2 tablas de recepción con FK tenant-safe, CHECK estado/traslado/recibida/tránsito≥0, clase TRASLADO, semilla TRF + config EF + entidades (`ICompanyScopedEntity`) | script aplicado al mirror, verificación SQL |
| **5.3** | `TrasladoAlmacenService` (Enviar/Recibir/Anular con candado de hdr y orden id-asc) + rechazo en `MovimientoAlmacenService` + DI + estados | `TrasladoAlmacenTests` en verde (incl. 18-22) |
| **5.4** | Controller + permisos + endpoint catalog | build + regresión |
| **5.5** | Cliente HTTP + 3 vistas + menú | prueba de humo logueada (la hace el usuario) |

---

## 9. Decisiones y limitaciones conocidas

1. **Sin contabilidad.** Como el resto del módulo de movimientos, el traslado no postea a
   `con_asiento`. El valor en tránsito es informativo y se calcula **a nivel de renglón**
   (hallazgo R-14): `Σ (cantidad − cantidad_recibida) × costo_real` sobre los `alm_movimiento_dtl`
   de traslados en tránsito. **No** como `existencia_transito × costo_real`: `existencia_transito`
   es un escalar por par que agrega el tránsito de varios traslados a costos distintos, y no hay un
   costo único por par para multiplicar.
2. **Valorización de la salida = promedio vigente de origen** (igual que toda salida). Si entre el
   envío y la recepción el promedio de origen cambia, no afecta lo que ya salió: el costo se congeló
   al enviar.
3. **Anular un traslado recibido exige que la mercadería siga en destino** (guarda de negativo del
   motor). Si ya salió de destino, se corrige con un movimiento de salida, no anulando.
4. **La misma limitación de la clase `VALOR`** (anular no restituye promedio anterior) no aplica al
   traslado: sus asientos son entradas/salidas normales, cuya reversa espejo ya está probada.
5. **Recepción parcial sin sobre-recepción:** no se puede recibir más de lo enviado (CHECK
   `cantidad_recibida <= cantidad`). Diferencias reales de conteo se resuelven con un ajuste aparte.

---

## 10. Hallazgos de la revisión adversarial (2026-08-04) y su resolución

Revisión por 6 dimensiones (concurrencia, idempotencia, reversa, recepción parcial, datos, costeo),
cada hallazgo verificado contra el código real. Todos incorporados arriba. Índice para trazar:

| Ref | Sev | Hallazgo | Dónde se resolvió |
|---|---|---|---|
| **R-1** | alta | TOCTOU: `RecibirAsync`/`AnularAsync` leían estado/`cantidad_recibida` antes del candado; el candado de pares no cubre hdr/dtl | §4.2 paso 2, §4.3 paso 1 (FOR UPDATE del hdr primero) |
| **R-2** | alta | La guarda de negativo `ValidarAsync:371` bloquea la reversa de `TrasladoSalida` (anular falla al mover el grueso del stock) | §3 (3 sitios, no 2) + prueba 17 |
| **R-3** | alta | Lost update de `cantidad_recibida` → sobre-recepción no detectada | §4.2 pasos 2 y 7 (candado + delta en SQL) + prueba 19 |
| **R-4** | media | `AnularAsync` revertía destino→origen, rompiendo el orden id-asc → deadlock ABBA | §4.3 paso 2 (pre-bloqueo id-asc de todo) |
| **R-5** | media | Traslado «pegado» en tránsito con recepciones concurrentes de renglones distintos | §4.2 pasos 2 y 8 (candado del hdr, completitud por SQL) + prueba 20 |
| **R-6** | media | Agregar TRASLADO a `ClaseAjusteInventario` rompe el CHECK de `alm_ajuste_inventario` | §2.5 (`ClaseMovimientoInventario` nuevo) + prueba 22 |
| **R-7** | media | `alm_traslado_recepcion_dtl.articulo_id` sin FK compuesta tenant-safe | §2.4 (FK + índice agregados) |
| **R-8** | media | Deadlock del rollup sobre `alm_articulo` fuera del orden id-asc | §4.1 paso 6 (rollup por artículo, al final, ordenado) |
| **R-9** | media | `AnularAsync` descarga tránsito sin lock/relectura | §4.3 pasos 1-3 + §2.2b (CHECK ≥0) |
| **R-10** | media | Nada garantizaba que el renglón recibido pertenezca al traslado | §4.2 paso 3 (validar pertenencia en `hdr.lineas`) |
| **R-11** | media | Traslado hacia/desde un par **inactivo** descuadra el rollup | §4.1 paso 2 (rechazar par inactivo) + prueba 21 |
| **R-12** | baja | Alta concurrente del par destino → `23505` crudo | §4.1 paso 2 (ON CONFLICT + relectura) + prueba 21 |
| **R-13** | baja | Entidades de recepción sin `ICompanyScopedEntity` | §2.4 (exigidas) + prueba 22 |
| **R-14** | baja | KPI «valor en tránsito» mal definido (escalar×costo-por-renglón) | §9.1 (Σ por renglón) |
| **R-15** | media | `RecibirAsync` no describía el corte de idempotencia por uuid del acto | §4.2 paso 1 |
| **R-16** | baja | Idempotencia del envío depende de uuid de cabecera estable | §4.1 paso 1 (contrato: la UI lo manda, el servicio rechaza nulo) |
| **R-17** | media | **(revisión de la implementación 5.3)** Deadlock ABBA: `EnviarAsync` tomaba pares→correlativo y `MovimientoAlmacenService` toma correlativo→pares (comparten la fila de `alm_movimiento_correlativo` por empresa) | `TrasladoAlmacenService.EnviarAsync`: el correlativo se toma **primero**, antes de resolver/bloquear pares (mismo orden que el movimiento) |

> Nota de método: el verificador de la dimensión *idempotencia* cayó por un error de servidor, pero
> sus dos hallazgos (R-15, R-16) quedaron **corroborados** por las dimensiones *reversa*, *parcial* y
> *concurrencia*, que confirmaron el mismo mecanismo (candado + corte por uuid) desde otro ángulo.
