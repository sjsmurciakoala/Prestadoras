# Bugs del motor de facturación V3 — descubiertos 2026-05-13

**Contexto**: durante la demo E2E con BD Azure (Empresa Demo, ruta 01001), salieron a luz **2 bugs graves** del motor de cálculo de factura. Estos bugs no son de Azure — son del código legacy que vino desde PROD APC y NUNCA se calcularon correctamente.

**Bloquea Sprint 3 día 4-5 "Motor unificado de facturación"** porque sin arreglar esto no se puede emitir factura formal SAR consistente.

---

## Reproducción

Cliente `LZV2CF1JFW` (Jessel, Empresa Demo, ruta 01001):
- categoría: DOMESTICO
- condición: SIN_MEDICION
- segmento: DOMESTICA_BAJA
- saldo previo: 0

Snapshot V3 que el server envía al app (verificado, **correcto**):

| Servicio | Cuadro | Regla | Valor esperado |
|---|---|---|---|
| AGUA_POTABLE | APC_AGUA_SM_DOMESTICA_BAJA | MONTO_FIJO | 199.27 |
| ALCANTARILLADO | APC_ALC_NA_DOMESTICA_BAJA | PORCENTAJE_SERVICIO 60% AGUA | 199.27 × 0.60 = **119.56** |
| TASA_AMBIENTAL | APC_TAMB_SM_DOMESTICA_BAJA | MONTO_FIJO | 5.90 |
| TASA_SVA_ERSAPS | CFG_ERSAPS_NA_DOMESTICO | 2% AGUA + 2% ALCANT | 3.99 + 2.39 = **6.38** |

**Total correcto: 199.27 + 119.56 + 5.90 + 6.38 = `L. 331.11`**

(Coincide con el saldo previo del cliente `090041002`, que sí fue facturado en periodo 2026/2 antes de la copia a Azure → eso confirma que el cálculo histórico SÍ daba 331.11. **Algo se rompió en el camino del legacy → V3**.)

---

## Bug #1 — App Android: motor de cálculo da números enormes

**Síntoma**: app calcula L. 12,404.48 (~37x lo correcto).
**Esperado**: L. 331.11.

**Conflict en cola de sync**:
```
SYNC_CONFLICT_TOTAL — TotalApp=12404.48 TotalServidor=209.16
```

**Hipótesis del bug**:
- El método `ConstruirFacturaV3DesdeLocal` ([UtilidadesBD.java:3182](../AppLectoresAPC/app/src/main/java/com/example/alberwills/aguaspuertocortes/Utilidades/UtilidadesBD.java#L3182)) probablemente NO distingue entre reglas `MONTO_FIJO`, `PORCENTAJE_SERVICIO` y `RANGO_CONSUMO` correctamente.
- Para clientes SIN_MEDICION (sin lectura), probablemente está usando algún valor "promedio_referencia" (40 según el snapshot de los otros clientes) y multiplicando.
- Mathematics: 199.27 × ~40 + cascadas de porcentajes ≈ 12,404. Algo en esa línea.

**Archivos a revisar (Sprint 3)**:
- `AppLectoresAPC/app/src/main/java/com/example/alberwills/aguaspuertocortes/Utilidades/UtilidadesBD.java`
  - método `ConstruirFacturaV3DesdeLocal` (linea ~3182)
  - método `CalcularRegla*` si existe (no auditado aún)
  - en general: iterar reglas del snapshot respetando `tipo_regla_codigo` y `modo_calculo`

---

## Bug #2 — SP backend: omite ALCANTARILLADO en el total

**Síntoma**: WS recalcula L. 209.16.
**Esperado**: L. 331.11.

**Diferencia**: 331.11 − 209.16 = 121.95 ≈ 119.56 (ALCANTARILLADO) + algo de ERSAPS.

**Hipótesis del bug**:
- El SP que calcula totales en backend NO resuelve reglas tipo `PORCENTAJE_SERVICIO` cuando dependen de otro servicio del mismo paquete.
- O salta servicios con `condicion_medicion = NO_APLICA` (que es ALCANTARILLADO).

**Archivos a revisar (Sprint 3)**:
- `Prestadoras/Database/ddl_v3/20260508_estados_numericos_02_sp_lectura_v3_estado_id.sql` (última versión de `sp_lectura_v3`)
- `Prestadoras/Database/ddl_v3/20260507_sar_compliance_07_sp_calcular_factura_company_id.sql` (`sp_adm_calcular_factura_lectura`)
- Buscar la iteración de reglas: probablemente `if regla_tarifaria.tipo = 'MONTO_FIJO'` y olvidaron el branch PORCENTAJE_SERVICIO o lo calculan mal.

---

## Restricción de diseño (NO negociable)

**El lector trabaja OFFLINE en campo y entrega la factura impresa al cliente AL MOMENTO de la lectura.**

Consecuencias:
- El app DEBE calcular la factura local con el snapshot V3 descargado al inicio del día.
- El WS NO puede ser fuente de verdad del cálculo (la app no está conectada cuando factura).
- El WS valida después al sincronizar: si los totales coinciden → OK. Si no → `SYNC_CONFLICT_TOTAL`.

Esto **descarta** la opción de "WS responde total" que parecía simple.

## Fix correcto: dos motores idénticos (Java + SQL)

El problema no es la arquitectura — es que los dos motores divergen. Sprint 3 día 4-5 tiene que:

| # | Pieza | Cambio |
|---|---|---|
| 1 | **Spec única** del algoritmo de cálculo | Doc nuevo: orden de evaluación, cómo se resuelve `PORCENTAJE_SERVICIO` (depende de otro servicio ya resuelto), redondeos, qué se incluye en el total. |
| 2 | **Motor SQL** (`sp_adm_calcular_factura_lectura` o motor nuevo) | Fix bug #2: incluir reglas `PORCENTAJE_SERVICIO` (ALCANTARILLADO 60% de AGUA, ERSAPS % de AGUA+ALCANT). |
| 3 | **Motor Java** (`ConstruirFacturaV3DesdeLocal`) | Fix bug #1: respetar `tipo_regla_codigo`. MONTO_FIJO usa `monto_fijo` tal cual. RANGO_CONSUMO usa `consumo × monto_unitario` o tarifa por tramo. PORCENTAJE_SERVICIO usa porcentaje × valor del servicio referenciado. |
| 4 | **Tests cruzados** | Dado snapshot V3 X (cliente conocido), ambos motores producen Y idéntico al céntavo. Cliente `LZV2CF1JFW` debe dar `L. 331.11` en ambos. |

**Alineación con gap #11** del PLAN_ENTREGA (motor unificado): ambos motores comparten especificación. NO se unifican en código (son lenguajes distintos), se unifican en **comportamiento**.

---

## Impacto en deadline 25-may

| Cuándo se ve esto | Estado |
|---|---|
| En PROD APC actual | Ya existía. Si actualmente facturan sin problema es porque el legacy todavía corre en paralelo. Cuando se complete el corte a V3, este bug bloquea emisión. |
| En la demo Azure | Bloquea probar E2E completo. |
| En entrega 25-may | **BLOQUEANTE** si no se arregla en Sprint 3. Sin esto NO se puede emitir factura formal SAR. |

Por eso Sprint 3 día 4-5 (motor unificado) sube de prioridad y se ata al gap #11.

---

## Hallazgos colaterales corregidos hoy (data fix en Azure PG)

Estos NO eran bugs del motor, eran datos malos en el seed importado de PROD APC:

| Issue | Fix | Aplicado |
|---|---|---|
| Regla 5 cuadro `APC_AGUA_SM_DOMESTICA_BAJA` (id=1, Empresa Demo): tipo `RANGO_CONSUMO` siendo SIN_MEDICION | UPDATE a `MONTO_FIJO 199.27` | ✅ Azure |
| 24 clientes con `cliente_maestro.tiene_medidor=true` pero `adm_cliente_servicio.condicion=SIN_MEDICION` | UPDATE bulk a `tiene_medidor=false` | ✅ Azure |
| Regla 29 (ALCANTARILLADO) `porcentaje=60.00` en lugar de `0.60` (formato decimal) | UPDATE a `0.60` | ✅ Azure |

**Estas correcciones también deben aplicarse a PROD APC** antes del 25-may. Scripts SQL listos:
- `fix-regla-5-azure.sql`
- `fix-tiene-medidor-azure.sql`
- `20260514_fix_motor_facturacion_v3.sql` (incluye el fix de regla 29 + soporte PORCENTAJE_SERVICIO en SP BASE)

Para PROD APC, generalizar los scripts bulk (no hardcoded `company_id=2`) y aplicar.

---

## Bugs adicionales descubiertos al validar E2E (2026-05-14, tarde)

Después de aplicar los fixes #1 y #2 y emitir 4 facturas E2E con éxito, al revisar el portal Azure surgieron 3 bugs nuevos en flujos colaterales:

### Bug #3 (CRÍTICO) — Correlativo CAI no se actualiza

**Síntoma**: emitidas 4 facturas con correlativos `20001..20005`, pero `adm_cai_facturacion.correlativo_actual` sigue en `20000`. Lo mismo para `adm_cai_bloque_reservado.correlativo_actual`.

**Causa**: `sp_lectura_v3` recibe `p_correlativo_cai`, lo guarda en `factura.correlativocai`, pero **NUNCA hace `UPDATE adm_cai_facturacion SET correlativo_actual = ...`**.

**Impacto**: la próxima emisión va a re-usar `20001` → duplicate key constraint → factura fallará. **Bloquea la operación a partir de la 6ª factura**.

**Fix**: agregar al final del flujo de emisión exitoso en `sp_lectura_v3`:
```sql
UPDATE public.adm_cai_facturacion
   SET correlativo_actual = GREATEST(correlativo_actual, p_correlativo_cai)
 WHERE company_id = p_company_id AND cai_id = p_id_cai;

UPDATE public.adm_cai_bloque_reservado
   SET correlativo_actual = GREATEST(correlativo_actual, p_correlativo_cai)
 WHERE company_id = p_company_id
   AND cai_id = p_id_cai
   AND p_correlativo_cai BETWEEN correlativo_desde AND correlativo_hasta;
```

**Archivos a modificar**:
- `Prestadoras/Database/ddl_v3/20260508_estados_numericos_02_sp_lectura_v3_estado_id.sql` (línea ~220-260 donde inserta en factura)

### Bug #4 — Estado de cuenta del cliente muestra "Saldo Actual" del último movimiento

**Síntoma**: cliente Jessel (LZV2CF1JFW) en /clientes → estado de cuenta:
- Movimientos correctos: AGUA -199.27, ALCANT -119.56, AMB -5.90, ERSAPS -6.38 (4 rows)
- Total acumulado correcto: **L. 331.11**
- Pero el header "Saldo Actual" muestra **L. 199.27** (saldo del último movimiento individual de la lista por `ide DESC`)

**Causa**: el service o SP que arma el "saldo actual" hace `SELECT saldo FROM transaccion_abonado WHERE cliente_clave = ? ORDER BY ide DESC LIMIT 1` y toma el primer registro encontrado (no necesariamente el último cronológico ni el agregado).

**Fix**: hacer la suma agregada: `SELECT SUM(saldo_detalle) FROM transaccion_abonado WHERE cliente_clave = ? AND estado = 'A'` para el "saldo actual", o tomar el último por `fecha_docu DESC, ide DESC` y campo `saldo` (no `saldo_detalle`).

**Archivos a auditar**:
- `Prestadoras/SIAD.Services/Cobranza/CobranzaService.cs` (`ObtenerSaldosClienteAsync`)
- Service del componente "Estado de cuenta" en el portal (a localizar)

### Bug #5 — Captación en Caja "No se encontraron saldos"

**Síntoma**: pantalla `/captacion-pagos` con cliente seleccionado, al "Cargar saldos" → "No se encontraron saldos para el cliente seleccionado", pese a que `transaccion_abonado` tiene 4 movimientos con `saldo_detalle != 0`.

**Causa probable**: el SP/service que arma los saldos pendientes filtra por algún campo legacy que excluye los movimientos generados por la app V3. Posibles candidatos:
- Filtra `tipo_servicio` por una lista hardcoded legacy (ej. "AGUA" en lugar de "AGUA_POTABLE")
- Filtra `numerofactura` con formato legacy en lugar del V3 `000-001-01-NNNNNNNN`
- Filtra `recibo > <X>` que excluye los nuevos

**Archivos a auditar**:
- `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`
- `Prestadoras/Database/ddl_v3/fix_fn_getclientesaldos_posteomanual.sql`

---

## Sobre CAIs — preguntas frecuentes del usuario

### ¿Cómo saber que un CAI está en uso?

Indicadores en `adm_cai_facturacion`:
- `estado_id = 2` (EN_USO) → tiene facturas emitidas asociadas
- `correlativo_actual > rango_desde - 1` → al menos 1 correlativo consumido (cuando se arregle bug #3)
- Existen rows en `factura WHERE idcai = <cai_id>` o en `adm_cai_correlativo_emitido WHERE cai_id = <cai_id>`

Indicadores en `adm_cai_bloque_reservado`:
- `estado_codigo = 'RESERVADO'` → bloque activo
- `correlativo_actual > correlativo_desde - 1` → consumidos correlativos

### Validaciones antes de emitir (`fn_adm_validar_cai_emitible`)

El CAI debe pasar TODAS:
1. `status_id = 1` (legacy activo)
2. `vigencia_desde <= CURRENT_DATE`
3. `vigencia_hasta >= CURRENT_DATE`
4. `fecha_limite_emision >= CURRENT_DATE` (regla SAR)

**Falta validar** (gap detectado 14-may):
- `correlativo_actual < rango_hasta` (no agotado)
- `estado_id ∉ (4=VENCIDA, 5=ANULADA)` (usar el lookup nuevo en lugar de solo status_id legacy)

### ¿Se puede borrar un CAI?

**NO** una vez emitida la primera factura. SAR Acuerdo 481-2017 exige conservar TODOS los CAIs históricos por 5 años. Protección actual:
- FK `factura → adm_cai_facturacion` con RESTRICT → DELETE bloqueado automáticamente

**Solo se puede ANULAR** (`estado_id=5`) si nunca se usó. Si ya está en uso, marcarlo VENCIDA (4) cuando pase fecha límite y emitir uno nuevo.

---

## Resumen consolidado bugs motor V3 al 2026-05-14 (cierre del día)

| # | Bug | Severidad | Estado | Script / archivo |
|---|---|---|---|---|
| 1 | App calcula 12,404.48 en lugar de 331.11 | Crítico | ✅ Resuelto | fix dato regla 29 |
| 2 | Server calcula 209.16 en lugar de 331.11 (omite PORCENTAJE_SERVICIO) | Crítico | ✅ Resuelto | `20260514_fix_motor_facturacion_v3.sql` |
| 3 | `correlativo_actual` no avanza tras emitir factura | Crítico | ✅ **Resuelto + aplicado Azure** | `20260514_bug3_avance_correlativo_cai.sql` + `_FIX_BACKFILL.sql` |
| 4 | Estado de cuenta saldo del último mov, no total | Mediano | ✅ **Resuelto + portal republicado** | `ClientesServices.cs:479` |
| 4b | Grid de movimientos no muestra `numfactura` (1 fila por servicio confunde al lector) | Bajo | ✅ **Resuelto + portal republicado** | `ClienteMovimientoDto.cs`, `ClientesServices.cs:555/610`, `ClienteEstadoCuentaTab.razor` |
| 5 | Captación en Caja "No se encontraron saldos" | Mediano | ✅ **Quick fix + aplicado Azure** | `20260514_bug5_fix_fn_getclientesaldos_posteomanual.sql` + `CaptacionPagosService.cs:2580` |
| 6 | App no incluye saldo previo (cliente con saldo) | Bajo | ✅ Ya estaba fixed (V3_2 09-may) | Pendiente validar E2E con `090041008` (anular factura activa primero) |
| 7 | Descarga de ruta re-trae clientes ya facturados del periodo | Mediano | ✅ **Resuelto + aplicado Azure** | `20260514_bug7_sp_medidores_por_ruta_ws_excluir_facturados.sql` |
| — | Validación CAI: tabla maestra `cfg_estado_cai` + SP con filtros completos | Mejora | ✅ **Aplicado Azure** | `20260514_validacion_cai_seleccion.sql` |

### Lo aplicado en Azure 14-may (tarde)

5 scripts SQL ejecutados sobre `siad_v3` Azure PG + 2 cambios C# del portal republicados:

| Script | Efecto |
|---|---|
| `20260514_bug3_avance_correlativo_cai.sql` | Inyecta `UPDATE correlativo_actual` en `sp_adm_confirmar_correlativo_cai_sync` con `GREATEST` para no retroceder en sync fuera de orden. Backfill bloques + CAI desde `adm_cai_correlativo_emitido`. |
| `20260514_bug3_avance_correlativo_cai_FIX_BACKFILL.sql` | Reset a `correlativo_desde - 1` para bloques sin emisiones, backfill desde emitidos. CAI 1 ahora muestra 205, CAI 2 muestra 20005 (max real emitido). |
| `20260514_bug5_fix_fn_getclientesaldos_posteomanual.sql` | Filtra por `f.saldototal > 0 AND f.estado='A'` (en lugar de `montovalor_saldo > 0`). Usa `LAG()` para `numreciboanterior`. |
| `20260514_validacion_cai_seleccion.sql` | Crea `cfg_estado_cai` (VIGENTE/VENCIDO/AGOTADO/ANULADO/SUSPENDIDO). FK desde `adm_cai_facturacion`. SP `sp_adm_actualizar_estado_cai`. `sp_adm_obtener_o_reservar_bloque_cai_ruta` ahora filtra por `tipo_documento_fiscal_id`, `estado_id=1 VIGENTE`, `fecha_limite_emision >= current_date`, `correlativo_actual < rango_hasta`. |
| Cambio C# `ClientesServices.cs:479` (Bug #4) | `GetEstadoCuentaAsync` ahora llama al SP `sp_obtener_cliente_saldo(p_company_id, clave)` en lugar de query EF ambiguo por `fecha_docu DESC`. |
| Cambio C# `CaptacionPagosService.cs:2580` (Bug #5) | `MapTipoServicio` reconoce `AGUA_POTABLE → AGUA` (antes caía como `OTROS`). |
| Cambio C# `APCService.svc.cs:1211` (cosmético) | `ContractVersion` del wrapper actualizado a `OFFLINE_SNAPSHOT_V3_2` (antes mentía diciendo V3_1; el packageJson interno ya iba en V3_2). |
| `20260514_bug7_sp_medidores_por_ruta_ws_excluir_facturados.sql` | Nuevo parámetro `p_excluir_facturados boolean DEFAULT true` en `sp_medidores_por_ruta_ws`. Filtra clientes con factura activa del período. PG aplica default automáticamente cuando el WS llama con 4 args → no requiere recompilar el WS. Validado: ruta 01001 devuelve 0 clientes (todos facturados), ruta 01002 devuelve 090041008 (factura anulada vuelve a aparecer). |
| Cambio C# `ClienteMovimientoDto.cs` + `ClientesServices.cs:555/610` + `ClienteEstadoCuentaTab.razor` (Bug #4b) | DTO con `NumRecibo` y `NumFactura`. Query EF con subquery correlacionada a `factura` por `(numrecibo, clientecodigo)`. Grid con 2 columnas nuevas (numrecibo y numfactura). Se ven 4 filas repetidas por factura (una por servicio); la consolidación a 1 fila por factura queda para la reforma post-25 de Estado de cuenta. |

---

## Deuda técnica post-25-may (no bloquea entrega, anotado para Sprint 4)

### A. Refactor del modelo `factura` / `factura_detalle`

Discusión 2026-05-14: el usuario cuestionó por qué la tabla `factura` tiene 3 "correlativos" (`id`, `numrecibo`, `numfactura`) y por qué se "repite" `numrecibo` en `factura_detalle`.

**Decisiones**:

1. **Renombrar a `adm_factura` / `adm_factura_detalle`** para alinear con el patrón V3 (`adm_cai_*`, `adm_cliente_*`).
2. **Eliminar `numrecibo` como autoincrement global**. Hoy es secuencia única `factura_numrecibo_seq` desde 3075052 sin valor de negocio. Para documentos **con CAI** (FAC, NC, ND, etc.) el correlativo legal sale del CAI (`numfactura`). Para documentos **sin CAI** (recibos internos, comprobantes de pago) hace falta una secuencia configurable por empresa + tipo.
3. **Crear `adm_documento_secuencia`** como tabla operativa por (empresa, establecimiento, tipo_documento_fiscal): `prefijo`, `longitud_padding`, `valor_actual`, `valor_inicial`, `fecha_reinicio_anual`, `activo`. Reemplaza el autoincrement legacy para documentos sin CAI. **NO sustituye** a `cfg_tipo_documento_fiscal` (catálogo de tipos — eso ya existe con 10 valores) — la complementa.
4. **Eliminar `numdei`** — es duplicado puro de `numfactura` (ver [sp_lectura_v3:317-320](../Database/ddl_v3/20260508_estados_numericos_02_sp_lectura_v3_estado_id.sql#L317-L320)). Terminología pre-Acuerdo SAR 481/2017.
5. **Migrar FK `factura_detalle`** a usar `factura_id` (ya existe la columna) en lugar de `numrecibo`. Agregar FK constraint declarado.

**Alcance**:
- Renombrar tabla en BD (script SQL).
- Refresh scaffold EF Core en `SIAD.Data`.
- Migrar todos los SPs que tocan `factura` o `factura_detalle` (sp_lectura_v3, sp_adm_calcular_factura_lectura, fn_getclientesaldos_posteomanual, captacion_pagos_stored_procedures, etc).
- Migrar todos los services C# del portal.
- Migrar WS WCF (`APCService.svc.cs`).
- Migrar app Android — no toca BD directamente pero usa `numrecibo` como display "Recibo #".

**Esfuerzo estimado**: 3-5 días. No entra en Sprint 3.

#### A.1 Detalle de `adm_documento_secuencia` (discusión 2026-05-14)

**Confusión a resolver**: hoy "recibo" significa dos cosas distintas mezcladas.

| Concepto | Hoy | Qué es realmente |
|---|---|---|
| `factura.numrecibo` | autoincrement global `factura_numrecibo_seq` desde 3075052 | Número interno de orden, sin valor legal. Una factura por fila. |
| "Recibo de pago" | NO existe tabla; el pago vive como filas en `transaccion_abonado` con `tipotransaccion='PAGO'` | Documento que confirma **un pago** contra una factura origen. Puede o no requerir CAI. |

**Modelo propuesto** — dos tablas que conviven:

`cfg_tipo_documento_fiscal` (catálogo, **ya existe**, 10 tipos): define QUÉ tipos existen (FAC=1, NC=6, ND=7, REC=10, etc.) y si `requiere_cai`.

`adm_documento_secuencia` (operativa, **nueva**): define el CORRELATIVO de cada tipo por empresa + establecimiento.

```
adm_documento_secuencia (
  id, company_id, establecimiento_id, tipo_documento_fiscal_id,
  prefijo, longitud_padding, valor_actual, valor_inicial,
  fecha_reinicio_anual, activo
)
```

Ejemplo de filas (empresa APC, establecimiento 1):

| tipo_doc | prefijo | longitud | valor_actual | próximo emitido |
|---|---|---|---|---|
| 1 (FAC) | `F-` | 8 | 4 | `F-00000005` |
| 6 (NC) | `NC-` | 6 | 0 | `NC-000001` |
| RPG (recibo pago) | `REC-` | 8 | 12 | `REC-00000013` |

**Distinción clave**: para una factura tipo FAC, el correlativo **legal SAR** sale del CAI (`adm_cai_facturacion` → `numfactura`). `adm_documento_secuencia` reemplaza el `numrecibo` **interno**, no el `numfactura` legal. Para documentos sin CAI (recibo de pago interno) `adm_documento_secuencia` es la única fuente.

**Flujo**:
```
Emitir factura:
  1. CAI → numfactura = "000-001-01-00020006" (legal SAR)
  2. adm_documento_secuencia FAC → numrecibo_interno = "F-00000005"
Captar pago:
  1. adm_documento_secuencia RPG → recibo_pago = "REC-00000013"
  2. INSERT adm_recibo_pago (recibo_pago, factura_id, monto, ...)  ← tabla nueva
```

**Implicaciones**:
- ✅ Multi-empresa nativo (hoy `factura_numrecibo_seq` es global — empresas 2 y 3 compartirían correlativo).
- ✅ Multi-establecimiento, reset anual configurable, sin saltos de correlativo por crashes a medio INSERT.
- ✅ Captación gana recibo formal numerado (hoy el pago es fila anónima en `transaccion_abonado`).
- ⚠️ Costo: `UPDATE valor_actual` requiere lock de fila → contención bajo alta concurrencia (irrelevante para APC, decenas/día).
- ⚠️ Costo: cada SP emisor debe llamar `sp_adm_obtener_siguiente_correlativo(...)` explícito en lugar del `nextval` implícito.

**Cambios que implica**:
- BD: crear `adm_documento_secuencia` + `adm_recibo_pago` + `sp_adm_obtener_siguiente_correlativo`. Backfill `valor_actual` desde `MAX(numrecibo)`.
- SPs: `sp_lectura_v3` y `sp_adm_facturar_miscelaneo` dejan de hacer `nextval`.
- EF: re-scaffold con las 2 tablas nuevas.
- C#: `CaptacionPagosService` migra pagos a `adm_recibo_pago`.
- App Android: display "Recibo #" usa nuevo correlativo.
- WS WCF: devuelve nuevo correlativo al confirmar lectura.
- Portal: pantalla admin para configurar prefijos/longitudes/valor inicial por tipo.

**Riesgo de migración**: facturas y pagos históricos NO se tocan; la secuencia arranca desde `MAX + 1`. El backfill de pagos a `adm_recibo_pago` es lo más complejo (hoy un pago toca 4 filas de `transaccion_abonado`, una por servicio).

### B. Captación de Pagos — REFACTOR COMPLETO

Usuario decidió 14-may: **"captacion de pagos lo tenemos que reformar completo"**. El fix de hoy (Bug #5) es band-aid para no bloquear demo del 25-may.

**Razones**:
- `fn_getclientesaldos_posteomanual` opera sobre `factura_detalle.montovalor_saldo` que carga semántica ambigua (saldo previo al insertar la factura, no saldo pendiente actual).
- No hay manejo real de pagos parciales por servicio.
- `MapTipoServicio` en C# hard-codea mapping de códigos legacy ↔ V3 — debe leer de catálogo.
- 3 tabs en la pantalla (Lectoras, Manual, Misceláneos) con 3 flujos divergentes que repiten lógica.
- `transaccion_abonado` schema legacy con 28 columnas, varias dead (`docufuente2`, `aplicar_alca`, etc).

**Alcance** (Sprint 4 post-25):
- Reescribir `fn_getclientesaldos_posteomanual` con semántica clara: saldo pendiente = (monto facturado − pagos aplicados). Usar `pago_factura_detalle` o tabla nueva normalizada de aplicaciones.
- Unificar los 3 flujos de captación en uno solo con strategy pattern.
- Reescribir `transaccion_abonado` o reemplazarla por `adm_movimiento_cuenta_corriente` normalizada.

### C. Facturación de Misceláneos — REFACTOR COMPLETO

Misma decisión que Captación. Hoy comparte SP `sp_obtener_cliente_saldo` (firma vieja sin `company_id`, deprecated) y `MapTipoServicio` legacy.

**Alcance** (Sprint 4 post-25):
- Migrar `FacturacionMiscelaneosService` a usar firmas V3 con `company_id`.
- Quitar referencias a `numdei`, `tipofacturacion='S'`/`'M'` legacy.
- Definir contrato claro sobre qué es "misceláneo" vs "factura de servicio" vs "nota de débito" en el nuevo modelo SAR.

### D. Filtrado de descarga de ruta (Bug #7) — ✅ Cerrado 14-may

Aplicado: `20260514_bug7_sp_medidores_por_ruta_ws_excluir_facturados.sql`. El SP recibe 5to parámetro `p_excluir_facturados boolean DEFAULT true`. PostgreSQL aplica el default cuando el WS C# llama con 4 args, así que no requirió tocar el WS. Anular una factura (`estado='N'`) libera al cliente para re-aparecer en la siguiente descarga.

**Deuda residual post-25**: migrar el filtro a `estado_id` numérico (`cfg_estado_documento_comercial`) cuando se termine la migración de estados, y agregar `p_company_id` al SP para alineación multi-empresa.

### E. Ajustes / exoneraciones por cliente individual — post-25 (decisión 2026-05-14)

Hoy `adm_ajuste_tarifario` define descuentos/exoneraciones/topes **a nivel de cuadro tarifario + condición** (ej. `TERCERA_EDAD_DOMESTICO`). El motor aplica el ajuste a los clientes que cumplen la condición (un atributo compartido por muchos: `maestro_cliente_tercera_edad`, categoría, etc.).

**Lo que NO soporta**: un descuento/exoneración a **un cliente puntual** (convenio especial, institución exonerada, acuerdo comercial). No es un beneficio "de categoría" — es individual.

**Por qué queda post-25**:
- No es bloqueante: el sistema factura igual sin esto.
- Es un cambio de modelo de ~2-3 días que toca el **motor de cálculo** (`sp_adm_calcular_factura_lectura`), el **snapshot offline V3** (para que la app lo aplique) y el **motor Java** de la app. Meterle mano al motor a 11 días del deadline es riesgo alto.
- **No se ata a la reforma de misceláneos** — son cosas distintas (misceláneos = facturar cargos que no son lectura; esto = ajustar el cálculo de la factura de servicio). Es su propio mini-proyecto.

**Alcance cuando se haga (Sprint 4 post-25)**:
- Tabla nueva `adm_ajuste_cliente` (`cliente_id`, tipo de ajuste, porcentaje/monto, **vigencia desde/hasta**, **motivo**, **usuario que autoriza**, traza) — el descuento discrecional **exige gobierno y auditoría**, no un campo suelto.
- Modificar `sp_adm_calcular_factura_lectura` para considerar ajustes por cliente además de los por cuadro.
- Incluir el ajuste por cliente en el snapshot offline V3 + aplicarlo en el motor Java de la app.
- UI para gestionar ajustes por cliente (con el flujo de autorización).

**Workaround hasta entonces**: para un cliente con convenio que se facturó de más, emitir una **Nota de Crédito** (legal, SAR-compliant). No es elegante pero cubre el caso.

### F. Migración de la app Android — post-25 (decisión 2026-05-14)

La app Android (`AppLectoresAPC/`, Java) se va a **migrar completa** post-25. Por eso lo que quedó pendiente del Sprint 3 día 9 sobre la app **no se hace sobre la base actual**:

- **Vista "lecturas con problema"**: hoy existe la cola `LecturaSyncQueueItem` (con `SyncStatus`/`SyncLastError`) y un long-click en `ListaMedidores.java` que muestra el error de una lectura. Falta una pantalla dedicada que liste solo las lecturas con problema + acciones (reintentar/descartar). Se hará en la migración.
- Cualquier otro pulido de UX de la app entra en la migración, no antes.

**Pendiente operativo del 25 que sí depende de la app actual** (no de la migración): clear data + reinstalar el APK vigente para validar bug #6 (saldo previo) — ver GUIA_PRUEBAS_AZURE.
