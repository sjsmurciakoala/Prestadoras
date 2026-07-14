# Plan — Migrar la referencia de artículo de `codigo_articulo` a `id` (módulo Almacén)

Fecha: 2026-07-13 · Estado: **✅ COMPLETADO (fases 1-4)** — los 4 módulos migrados a `articulo_id` en local + producción. Enlazados: kardex 47.203/47.215 · compra 4.482/4.484 · descargo 42.742/42.757 · requisición 42.833/42.866 (≈62 huérfanos totales, código sin artículo). El `codigo_articulo` se conserva como referencia. **Pendiente opcional (fase 5):** endurecer `articulo_id` NOT NULL si se resuelven los huérfanos, y migrar los reportes DevExpress que aún filtran por código.

## 1. Contexto

Al volver **opcional** el `codigo_articulo` (los artículos nuevos usan el `id`/PK como identificador de
negocio y dejan el código en blanco), quedó al descubierto que el resto del módulo de Almacén —**kardex,
compras, requisiciones y descargos**— referencia al artículo **por `codigo_articulo` (texto), no por `id`**,
sin FK real. Consecuencia: un artículo nuevo **sin código no es referenciable** por esos flujos (todos
"colapsan" al código vacío), y el botón "Ver kardex" no sirve para ellos.

Este documento define el alcance para migrar esas referencias a `id`.

## 2. Estado actual (datos de producción, `company_id = 2`)

| Tabla | Filas | ¿Tiene `articulo_id`? | Referencia hoy |
|---|---:|---|---|
| `alm_kardex` | 47,215 | No | `codigo_articulo` |
| `alm_requisicion` | 42,866 | No | `codigo_articulo` |
| `alm_descargo` | 42,757 | No | `codigo_articulo` |
| `alm_compra` | 4,484 | No | `codigo_articulo` |

- **Huérfanos** (movimientos con código que no matchea ningún artículo): **12 en kardex** (0.025%). A revisar/decidir; impacto bajo.
- **~25 archivos** acoplan `codigo_articulo`: entidades (las 4 + `alm_articulo`), servicios (`KardexService`, `ComprasService`, `RequisicionesService`, `DescargosService`, `ArticulosService`), `KardexController`/`KardexClient`, DTOs (`KardexFilterDto`, `RequisicionListItemDto`, `DescargoListItemDto`, `CompraListItemDto`), UI (`ArticulosList`, `KardexArticulo`, `ComprasList`, `RequisicionesList`, `DescargosList`, `StockAlertas`), y tests.

## 3. Objetivo

Que kardex/compras/requisiciones/descargos referencien al artículo por **`articulo_id` (FK a `alm_articulo`)**,
manteniendo `codigo_articulo` como **snapshot/referencia** (SIMAFI). Sin romper la operación ni los reportes
durante la transición.

## 4. Estrategia: por módulo, con backfill y referencia dual

Migrar **un módulo a la vez** (no todo de golpe). Durante la transición, mantener **ambas** referencias
(`codigo_articulo` + `articulo_id`) para no romper consultas/reportes existentes. Orden sugerido:
**Kardex → Descargos → Requisiciones → Compras** (kardex primero por ser el más usado y por el botón roto).

## 5. Fases (aplican a cada una de las 4 tablas)

### Fase 1 — Esquema + backfill
- Migración SQL: `ALTER TABLE alm_X ADD COLUMN articulo_id INTEGER NULL REFERENCES alm_articulo(id) ON DELETE SET NULL;` + índice `ix_alm_X_articulo`.
- Backfill: `UPDATE alm_X x SET articulo_id = a.id FROM alm_articulo a WHERE a.company_id = x.company_id AND a.codigo_articulo = x.codigo_articulo;` (por código exacto).
- Reportar y decidir los **huérfanos** (los que quedan con `articulo_id` nulo). Opciones: dejar nulos, mapear a mano, o crear el artículo faltante.
- Entidad + `SiadDbContext.Almacen.cs`: agregar `articulo_id` + navegación.
- **Riesgo:** volumen (kardex 47k, requisición/descargo ~43k c/u). El `UPDATE` con índice en `codigo_articulo` es eficiente; conviene ventana de baja actividad.

### Fase 2 — Escritura dual (movimientos nuevos)
- Al registrar nuevos movimientos, setear `articulo_id` (del artículo elegido) además del código (que puede ir vacío para artículos nuevos). De aquí en adelante, todo movimiento nace con `articulo_id`.

### Fase 3 — Lectura por id (servicios)
- Cambiar filtros/joins/agrupaciones de `KardexService`/`ComprasService`/`RequisicionesService`/`DescargosService` a usar `articulo_id`, con **fallback a código** para registros viejos sin `articulo_id` (huérfanos) durante la transición.
- `ArticulosService.DeleteAsync`: cambiar el guard de movimientos a `AnyAsync(k => k.articulo_id == id)`.

### Fase 4 — API + Client + DTOs + UI
- Endpoints/filtros aceptan `articuloId` (además/en vez de `codigo`). `KardexFilterDto` + `ArticuloId`.
- UI: `ArticulosList` "Ver kardex" navega por `?articuloId=@item.Id`; `KardexArticulo` preselecciona por id; `ComprasList`/`RequisicionesList`/`DescargosList`/`StockAlertas` seleccionan/muestran por id.
- Los selectores/combos de artículo devuelven `id`.

### Fase 5 — Endurecer (opcional, futuro)
- Si no quedan huérfanos, hacer `articulo_id` `NOT NULL`. El `codigo_articulo` queda como snapshot/referencia histórica.

## 6. Riesgos y decisiones abiertas

1. **Huérfanos (12 en kardex):** decidir manejo (bajo impacto).
2. **Volumen (~137k filas):** backfill eficiente + ventana de aplicación.
3. **Referencia dual en transición:** mantener código + id hasta migrar todos los consumidores.
4. **Reportes DevExpress:** revisar `rep_catalogo_datasets` / layouts / SQL custom que filtren por `codigo_articulo` (podrían romperse si el código deja de poblarse). **No eliminar el código** hasta migrar reportes.
5. **Multi-tenant:** el backfill debe unir por `(company_id, codigo_articulo)`, no solo por código.

## 7. Esfuerzo estimado

**Grande.** ~1–2 sesiones por módulo (esquema+backfill, backend, UI, tests), 4 módulos → significativo.
Recomendado fasear por módulo con verificación entre fases y aplicación de migraciones con aprobación
(local + producción), como se ha venido haciendo.

## 8. Quick win intermedio (mientras se planifica)

Sin emprender la migración completa: **deshabilitar/ocultar el botón "Ver kardex"** en `ArticulosList` para
artículos sin código, para no dejar un botón que no hace nada. Cambio chico, solo UX.
