# Fase 6 — Requisición → aprobación → descargo (dos actores) — Plan de implementación

Fecha: 2026-08-04
Fuente de análisis (vigente): [`README_requisiciones_descargos.md`](../centura-flujos/README_requisiciones_descargos.md)
(1.576 líneas). Este plan **adapta** ese análisis al estado actual del código (que cambió desde el
2026-07-31: ya existen el motor de salida, el módulo de movimientos, `ClaseMovimientoInventario`, etc.)
y fija las decisiones tomadas por el usuario el 2026-08-04.

> ### Alcance elegido: **flujo completo de dos actores** (Fork A)
> El usuario eligió el flujo real: requisición (solicitud) → aprobación → descargo con **entrega
> parcial**, no la salida simple como movimiento.

## Decisiones cerradas (2026-08-04)

- **D-4 Aprobación por PERMISO** (no jerarquía, no catálogo de departamentos): cualquier usuario con
  `module.inventario.requisiciones.aprobar` aprueba. Sin regla de auto-aprobación por ahora (fácil de
  añadir después). ⇒ departamento sigue **texto libre** (D-15a) y **todos ven todas** (D-5a).
- **D-13 Sin vale imprimible** en esta entrega (queda para una fase de reportería posterior).
- Resto = recomendación del análisis para 1ª entrega: **D-1** sin contabilidad · **D-2** NO reservar
  stock (fiel a Centura) · **D-3** aprobar no recorta cantidades · **D-6** bodega elegida por el
  usuario, '01' por defecto · **D-7** solo cuenta contable heredada del tipo · **D-8** sin devolución
  (la anulación cubre) · **D-10/D-11** correlativo continúa desde 17.124 y las históricas abiertas se
  cierran en el script del corte · **D-12** reabastecimiento→O/C fuera de alcance · **D-14** permisos
  bajo Inventario · **D-16** una sola unidad · **D-17** consignación igual · **D-18** requisables =
  `activo AND maneja_inventario`. **D-9** (`traslado='T'` del histórico) no bloquea; anotado.

## Lo que YA está hecho (no rehacer)

- **Fase 0 — motor:** `TipoMovimientoInventario.SalidaDescargo` posteando salidas (guarda de costo 0 y
  de negativo), y la **reversa-espejo** que discrimina por `documento_tipo=Descargo` (corregida en la
  Fase 5.1; ya no "vuelve a descargar" al anular). `ck_alm_kardex_documento_tipo` admite `DESCARGO`.
- **Fase 1 — BD:** `Database/2026-08-01_alm_requisicion_descargo.sql` (**paso 26 del runbook, aplicado
  al mirror**). Crea `alm_requisicion_hdr`, `alm_descargo_hdr`, sus dos correlativos (sembrados desde
  el máximo histórico), las columnas aditivas en las planas (`requisicion_hdr_id`, `cantidad_despachada`,
  `aplicado_en_oc`, `descargo_hdr_id`, `requisicion_id`), las FK compuestas, el `ck` de parcialidad, el
  `ck_alm_requisicion_no_postea`, el DROP de `ix_alm_requisicion_pendiente` (desarmar el arma cargada) y
  el `ck` de reserva no-negativa. **No se revierte: se construye encima.**
  - **Máquina de estados (del CHECK):** `1` Borrador · `2` En revisión · `3` Aprobada · `4` Despachada
    parcial · `5` Despachada total · `6` Cerrada en O/C · `8` Rechazada · `9` Anulada. Los estados 4/5
    son **derivados** de `cantidad_despachada`, no se capturan a mano.

## Prerrequisito operativo (no bloquea construir; sí usar)

El descargo se valoriza al costo promedio; hay **241 pares con `costo_promedio=0`** (Fase 8 del corte
sin correr). El motor **rechaza** salir a costo 0 (no corrompe), así que los descargos de esos pares
quedan **bloqueados hasta que el usuario corra el corte**. Los tests usan pares con costo > 0.

## Sub-fases restantes

| # | Entregable | Verificable |
|---|---|---|
| **6.2 Requisición (backend)** | Entidad `alm_requisicion_hdr` + columnas nuevas en `alm_requisicion`; `EstadoRequisicionHdr`; DTOs; `RequisicionService` (crear/editar borrador, enviar a revisión, aprobar, rechazar, anular); controller + permisos (`requisiciones.{view,create,edit,aprobar}`); cliente. **NO mueve inventario.** | `RequisicionFlujoTests` en verde, con aserción de **0 asientos en el kardex** |
| **6.3 Descargo (backend)** | Entidad `alm_descargo_hdr` + columnas nuevas en `alm_descargo`; `DescargoService.EntregarAsync` (parcial, tope `cantidad − cantidad_despachada` bajo `FOR UPDATE`, postea `SalidaDescargo` por línea, sube `cantidad_despachada`, deriva estado 4/5 de la requisición) + `AnularAsync` (reversa por línea, devuelve cantidad); controller + permisos; cliente. | `DescargoTests` + `DescargoAnulacionTests` en verde |
| **6.4 UI + permisos** | Lista de requisiciones (cabeceras), form de requisición, pantalla de aprobación, lista/detalle de descargo con entrega parcial, sidebar. | prueba de humo logueada (usuario) |
| **6.5 Puesta en marcha** | Detectores de duplicación como script `SELECT` de verificación en `Database/`, registrado en el runbook. | V-1..V-8 en 0 filas |

## Notas de implementación (del análisis, ya validadas contra el código)

- **La unidad de posteo del descargo es la LÍNEA** (`alm_descargo`), con su `uuid`. El motor ancla la
  idempotencia a `(Descargo, company, alm_descargo.id, par)` — ya implementado en `DerivarUuid`.
- **Entrega parcial:** el tope es `cantidad − cantidad_despachada` a nivel línea de requisición, contra
  la **existencia física** de la bodega; se valida bajo `SELECT … FOR UPDATE` y el `ck` de la BD es la
  red final. Una línea de requisición admite **N** descargos.
- **Anular un descargo:** reversa por línea (el motor ya lo hace bien) + **restar** `cantidad_despachada`
  de la requisición + recomputar su estado (5→4→3). Nunca `UPDATE` del kardex.
- **Concurrencia:** tomar el correlativo antes que los pares (lección R-17 de la Fase 5) y pre-bloquear
  los pares en orden ascendente de `alm_articulo_bodega.id`.
- **Anti-duplicación:** el histórico está blindado (`origen='SIMAFI'`, `posteado=true`, `uuid` NULL); el
  servicio nunca toca esas filas. La cabecera nueva enlaza por `requisicion_hdr_id`/`descargo_hdr_id`.
