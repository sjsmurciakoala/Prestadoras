# Estados de documentos comerciales (factura, transaccion_abonado, sync CAI)

Fecha: 2026-07-17
Alcance: facturación/caja/app de lectores. Complementa
[ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md](ESTANDAR_ESTADOS_Y_FLUJO_CONTABLE.md)
(que cubre el ámbito contable) y las constantes de
[SIAD.Core/Constants/EstadosNumericos.cs](../SIAD.Core/Constants/EstadosNumericos.cs).

Regla general del repo: los estados string son legacy en migración a lookups
numéricos (`estado_id` + catálogos `cfg_estado_*`, aplicados 2026-05-07/08).
**No introducir estados string nuevos**; los writes/reads nuevos usan `*_id`.

## 1) `factura.estado` (+ espejo `factura.estado_id`)

Catálogo oficial: `cfg_estado_documento_comercial`.

| Código | estado_id | Significado | Quién lo pone |
|---|---|---|---|
| `A` | 1 | **Activa / pendiente de pago** | `sp_lectura_v3` al emitir; reverso de abono si no quedan abonos vigentes |
| `B` | ⚠️ sin id (cae en 1) | **Parcialmente abonada** | `AbonoService` al registrar un abono que no salda la factura |
| `C` | 2 | **Cobrada / compensada** — pagada en caja, o compensada porque `sp_lectura_v3` emitió una factura de servicio más nueva del mismo cliente | caja/banco al saldar; `sp_lectura_v3` sobre las anteriores (`tipofacturacion='S'`) |
| `N` | 3 | **Anulada** | anulación |

- La vista **Facturas App** (`/mi-app/facturas`) muestra esta columna tal cual
  (`Estado factura`); lo normal en facturas recién sincronizadas es `A`.
- ⚠️ **Gap detectado (2026-07-17):** `B` no existe en
  `cfg_estado_documento_comercial` ni en `EstadoDocumentoComercial` (C#); el
  backfill lo mapeó a `estado_id=1`. Mientras no se agregue al catálogo, el
  espejo numérico **no distingue** una factura parcialmente abonada de una
  activa. Consumidores que filtran por string usan `A`/`B`/`C` (ver
  `AbonoService.BuscarFacturasConSaldoAsync`).

## 2) `transaccion_abonado.estado` (+ espejo `estado_id`)

⚠️ **La misma letra significa cosas distintas según `tipotransaccion`** — este
es el principal foco de confusión:

### 2.1 Cargos (facturación, migración SIMAFI)

| Código | Significado |
|---|---|
| `A` | Activo / pendiente |
| `C` | Cobrado / compensado |

### 2.2 Abonos de caja (`tipotransaccion = '202'`)

| Código | Significado | Referencia |
|---|---|---|
| `C` | Abono posteado (cobrado) | `AbonoService.RegistrarAbonoAsync` |
| `P` | Recibo generado, **pendiente de pago** | `AbonoService.GenerarReciboPendienteAsync` |
| `A` | **Anulado / reversado** | `AbonoService.ReversarAbonoAsync` (`transaccion.estado = "A"`) |

- ⚠️ **Gap detectado (2026-07-17):** el catálogo numérico asume el significado
  de cargos (`A` → `estado_id=1` "Activa/pendiente"), por lo que un abono 202
  **anulado** queda con `estado_id=1`. Al depurar o reportar sobre
  `transaccion_abonado`, filtrar SIEMPRE junto con `tipotransaccion`; no
  confiar en `estado_id` para abonos hasta que se corrija el mapeo.
- Los pagos del canal bancario (F8) también son 202 pero se reversan por
  `sp_ban_ws_reversar`, nunca desde caja (marca `WSBANCO:` en `trans_aplicar`).

## 3) Sincronización CAI de la app (`adm_cai_correlativo_emitido.estado_codigo`)

Catálogo: `cfg_estado_correlativo_cai` / `EstadoCorrelativoCai` (C#).
La vista Facturas App lo muestra como insignia (`Sync`).

| Código string | estado_id | Significado | Insignia en la vista |
|---|---|---|---|
| `PENDIENTE` / `PENDING_OFFLINE` | 1 | Correlativo reservado offline, sin confirmar | PENDIENTE (ámbar) |
| `PENDING_SYNC` | 2 | Subida en proceso de confirmación | PEND. SYNC (ámbar) |
| `CONFIRMADO` | 3 | Factura emitida y correlativo confirmado | CONFIRMADA (verde) |
| `SYNC_CONFLICT` | 4 | Conflicto (total no coincide, duplicado, etc.); ver `detalle_conflicto` | CONFLICTO (rojo) |
| `ANULADO` | 5 | Correlativo anulado | — |

Conflictos detallados: `adm_lectura_v3_conflicto_sync`
(`cfg_estado_conflicto_sync`: 1 pendiente, 2 revisado, 3 cerrado).

## 4) Otros catálogos relacionados

- `cfg_estado_cai`, `cfg_estado_bloque_cai` (1 reservado, 2 agotado, 3 expirado)
  — ciclo de vida de CAI y bloques offline.
- `cfg_estado_documento_fiscal` — documentos fiscales SAR.
- `historialmes` fue retirado (plan apertura ciclo único); los períodos viven en
  `adm_periodo_comercial(_ciclo)` con su propio `status_id` (ver F7).

## 5) Pendientes / correcciones sugeridas

1. Agregar `B` (parcialmente abonada) a `cfg_estado_documento_comercial` y a
   `EstadoDocumentoComercial`, con backfill de facturas en `B`.
2. Separar el catálogo de estados de **abonos** del de cargos (o normalizar el
   reverso de abonos a `N`), para que `estado_id` deje de mezclar "activo" con
   "anulado" en 202.
3. `AbonoService.ListarAbonosDelDiaAsync` etiqueta `A → "ANULADO"` sólo para
   202; si se reusa el patrón en otra consulta, validar el `tipotransaccion`.
