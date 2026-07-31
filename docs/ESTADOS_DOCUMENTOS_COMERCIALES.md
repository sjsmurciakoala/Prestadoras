# Estados de documentos comerciales (factura, transaccion_abonado, sync CAI)

> ## ⚡ Actualización F7 (2026-07-30) — el corte de la unificación de cobranza
>
> Este documento describe el mundo ANTERIOR al corte. Desde F7:
>
> - **`transaccion_abonado` está CONGELADA** (H4): histórico de solo lectura con
>   los 12.17M de movimientos migrados de SIMAFI + la era del dual-write. Un
>   candado por trigger rechaza toda escritura salvo
>   `SET LOCAL siad.permitir_escritura_legacy='on'` (solo migración). Sus
>   letras y espejos numéricos quedan como estaban — nadie los mantiene ya.
> - **Los estados operativos viven en el modelo nuevo**:
>   `factura.estado/estado_id` (los pone `CobroService`: `C`=2 saldada, `B`=4
>   parcial — el gap de la `B` quedó cerrado en F1), `adm_pago.estado_id`
>   (`adm_estado_pago`: 1 aplicado, 2 pendiente, 3 anulado, 4 reversado) y
>   `adm_recibo_banco_pendiente.estado_id` (papel para banco).
> - **La vista `vw_transaccion_abonado_vigente` se retiró** (H5). Los reportes
>   leen `vw_rep_movimiento_vigente`, construida sobre factura/adm_pago(+
>   aplicaciones)/NC/ND. La regla de vigencia por letras del §2 es historia.
> - Los SPs legacy de posteo se droppearon; el saldo es
>   `sp_obtener_cliente_saldo` v7 = líneas A/B + cuotas activas + ND vivas.
>
> Las secciones siguientes se conservan como referencia del histórico congelado
> (necesarias para leerlo o auditarlo), no como guía de operación.

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

## 2.3) Códigos de `tipotransaccion` del histórico congelado (SIMAFI)

Referencia para leer/auditar el histórico y para la etiqueta que muestra el
portal (estado de cuenta → columna Tipo). Son los códigos ORIGINALES de
SIMAFI, congelados con la tabla en F7 H4 — **no** son los tipos numéricos del
modelo nuevo (esos viven en `adm_tipo_transaccion`, ids 1–11; el mapa
viejo→nuevo está en `adm_tipo_transaccion_codigo_legacy`).

| Código legacy | Qué es | Etiqueta en el portal |
|---|---|---|
| `101` | Cargo de agua potable (línea de factura) | FACTURA |
| `102` | Cargo de alcantarillado | FACTURA |
| `103` | Cargo de tasa/fondo (concepto SIMAFI) | FACTURA |
| `104` | Cargo de tasa/fondo (concepto SIMAFI) | FACTURA |
| `105` | Otros cargos de facturación | FACTURA |
| `16` | Cargo suelto/ajuste del origen | CARGO |
| `11` | Saldo inicial migrado | SALDO INICIAL |
| `111` | Cuota de convenio de pago (¡fechadas a FUTURO en el origen!) | CUOTA CONVENIO |
| `201` | Pago (recibo de caja SIMAFI) | PAGO |
| `202` | Abono de caja / pago del WS bancario | PAGO |
| `203` | Crédito por traslado a convenio | CONVENIO |
| `205` | Nota de crédito | NOTA CRÉDITO |
| `206` | Nota de débito | NOTA DÉBITO |
| `PLAN*` | Movimientos de planes de pago (era dual-write) | PLAN DE PAGO |
| `SALDO_ANTERIOR` | Residuo migrado (murió como fuente de saldo en H4) | SALDO ANTERIOR |

La traducción a etiqueta vive en `ClientesServices.EtiquetaTipoMovimiento`.
Los significados exactos de 103/104 (qué tasa es cada uno) vienen del catálogo
de conceptos de SIMAFI; si se necesita el desglose fino, la Descripción de la
fila y el detalle de la factura lo traen.

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
