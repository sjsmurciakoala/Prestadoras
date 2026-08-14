# Antigüedad de saldos de proveedores — plan de implementación

**Fecha:** 2026-08-14
**Rama:** `feat/almacen-integracion-contable`
**Estado:** diseñado · **F0 escrito** (`Database/2026-08-14_prv_antiguedad_saldos.sql`), pendiente de aplicar al mirror
**Prototipo:** `docs/prototipos/2026-08-14-antiguedad-saldos-proveedor.html`
**Molde de estilo:** estado de cuenta del proveedor (`fn_prv_estado_cuenta_*`, 2026-08-13)

---

## 1. Qué se pide

Una **vista (matriz)** y un **reporte** que repartan el saldo por pagar de **cada proveedor**
según cuánto lleva vencido, a una fecha de corte, en **seis tramos**:

> por vencer · 1–30 días · 31–60 días · 61–90 días · 91–120 días · más de 120 días

Cada proveedor es una fila; los tramos son columnas; totales al pie. Es el clásico *aged payables*.

---

## 2. Punto de partida (verificado en el código)

### 2.1 La deuda y los tramos YA existen — pero por un solo proveedor

- **`fn_prv_estado_cuenta_documentos`** (función BASE): concentra todas las reglas de vigencia
  (CxP anulada `estado_id=9` fuera; compromiso `anulado` fuera; ★ compat legacy del compromiso
  procesado sin abonos = saldado; abonos solo `estado='V'`; abono de compromiso al bruto). Devuelve
  cada documento pendiente con su `dias_vencido`.
- **`fn_prv_estado_cuenta_resumen`**: ya calcula los tramos, pero **corta en «>90» (5 tramos)** y
  corre para **un** `cod_proveedor`.
- El aging de **clientes** (`rep_saldo_clientes_antiguedad`) usa los mismos cortes 30/60/90.

### 2.2 Lo único que falta

1. **Consolidar** el cálculo sobre todos los proveedores en una pasada.
2. **Abrir** el último tramo en **91–120** y **más de 120** (hoy la función corta en «>90»).

Ninguna de las dos necesita tablas nuevas: los datos vivos ya están en `alm_compra_cxp` y
`prv_compromiso_hdr`. Por eso **F0 es una sola función de lectura**, no un modelo de datos.

### 2.3 Cuidados que cambian el SQL

- **No duplicar las reglas de vigencia.** El F0 reutiliza la función base con `CROSS JOIN LATERAL`
  (una llamada por proveedor) y solo agrega por tramo. Si una regla cambia, cambia en un lugar.
- **`prv_proveedores` es keyless y `company_id` es `int4`** (`alm_*` es `BIGINT`): cast explícito y
  enlace por `(company_id, cod_proveedor)` sin FK. El `cod_proveedor` se toma **crudo** para el
  `LATERAL` (la base compara por igualdad exacta) y con `TRIM` solo para unir la identidad.
- **El aging es de documentos vivos, no del mayor.** La cartera histórica de SIMAFI
  (~L 101M al haber en `prv_proveedores.cuenta_contable`) no tiene documentos operativos: este saldo
  **no cuadra con la contabilidad** y no debe presentarse como si lo hiciera (igual que el estado de
  cuenta).
- **El compromiso no tiene vencimiento propio** (D2 del estado de cuenta: usa su fecha de emisión).
  Envejece desde la emisión; ver D3 abierta.

---

## 3. Modelo de datos (F0)

**Cero tablas nuevas.** Una función de lectura, aditiva y reversible:

```
fn_prv_antiguedad_saldos(
    p_company_id         BIGINT,
    p_corte              DATE     DEFAULT NULL,   -- NULL = hoy
    p_incluir_por_vencer BOOLEAN  DEFAULT TRUE,   -- FALSE = solo lo vencido
    p_origen             INTEGER  DEFAULT 0,      -- 0 ambos · 1 compras · 2 compromisos
    p_cod_tipoproveedor  INTEGER  DEFAULT NULL    -- NULL = todos los tipos
)
```

Devuelve, por proveedor: identidad (`cod_proveedor`, `proveedor_nombre`, `rtn`, tipo, cuenta),
los seis tramos (`por_vencer`, `tramo_1_30`, `tramo_31_60`, `tramo_61_90`, `tramo_91_120`,
`tramo_mas_120`), `vencido`, `saldo_total` y `documentos_pendientes`. Ordena por saldo desc.

---

## 4. Cómo se calcula

1. **Universo** — proveedores con al menos un documento no anulado: `alm_compra_cxp` (`estado_id<>9`)
   ∪ `prv_compromiso_hdr` (`anulado=FALSE`), filtrado por `p_origen`.
2. **Por proveedor** — `CROSS JOIN LATERAL fn_prv_estado_cuenta_documentos(..., solo_pendientes=TRUE)`
   y `SUM(saldo) FILTER (WHERE dias_vencido BETWEEN …)` por tramo. `p_origen` filtra las ramas.
3. **Presentación** — se une la identidad del maestro, se descarta saldo 0 y se ordena por saldo.
   `p_incluir_por_vencer=FALSE` pone `por_vencer=0` y deja `saldo_total = vencido`.

**Cuadre garantizado** (mismo proveedor y corte, con `incluir_por_vencer=TRUE`):

| Aging | == | Estado de cuenta |
|---|:--:|---|
| `saldo_total` | = | `saldo_total` |
| `por_vencer` | = | `saldo_por_vencer` |
| `vencido` | = | `saldo_vencido` |
| `tramo_91_120 + tramo_mas_120` | = | `antiguedad_mas90` |

El script trae ese cuadre como consulta de verificación (§4 del `.sql`).

---

## 5. Permisos

- `module.proveedores.antiguedad.view` — consultar la matriz y generar el reporte.

Reutiliza el módulo `proveedores`, como el estado de cuenta y las retenciones. Sin permiso de
edición: es un reporte de solo lectura.

---

## 6. Pantallas y reporte

| Ruta | Qué es |
|---|---|
| `/proveedores/antiguedad-saldos` | Matriz: filtros (corte, tipo, origen, solo vencido), KPIs, distribución por tramo y tabla proveedor×tramo con totales |
| *(drill-down)* | Clic en un proveedor abre el **estado de cuenta del proveedor** que ya está en producción — no se construye pantalla de detalle nueva |

Reporte DevExpress por código, patrón `Rpt_Dev_EstadoCuenta_Proveedor`: **cuadro de antigüedad**
(PDF + Excel), con totales y participación por tramo.

---

## 7. Fases

| Fase | Qué entrega | Depende de |
|---|---|---|
| **F0** | SQL: `fn_prv_antiguedad_saldos` (consolidada, 6 tramos, reutiliza la función base) | — |
| **F1** | Backend: DTO, `AntiguedadSaldosProveedorService` (leer matriz + totales), controlador, permiso, cliente HTTP | F0 |
| **F2** | Pantalla matriz `/proveedores/antiguedad-saldos` + drill-down al estado de cuenta | F1 |
| **F3** | Reporte DevExpress: cuadro de antigüedad (PDF/Excel) | F1 |

Orden sugerido: **F0 → F1 → F2 → F3**. Más liviano que evaluación de proveedores: sin captura, sin
catálogo, sin tablas.

---

## 8. Decisiones

### Cerradas

| # | Decisión | Resuelta |
|---|---|---|
| D1 | Seis tramos: por vencer, 1–30, 31–60, 61–90, 91–120, más de 120 días | 2026-08-14 |
| D2 | Reutilizar la función base vía `LATERAL` en lugar de reimplementar las reglas de vigencia | 2026-08-14 |
| D3 | F0 no toca `fn_prv_estado_cuenta_resumen`: el aging es autónomo, la pantalla de estado de cuenta sigue intacta | 2026-08-14 |

### Abiertas (no bloquean F0/F1)

| # | Pregunta | Impacto |
|---|---|---|
| D4 | Base del vencimiento del compromiso: hoy usa la fecha de emisión. ¿Se deriva del término de pago del proveedor (`alm_termino_pago`)? | Cambia `fecha_vencimiento` en la función base — afecta también al estado de cuenta |
| D5 | ¿Se alinea también el estado de cuenta del proveedor a 6 tramos, o se deja en «>90»? | Solo cosmético en esa pantalla; el aging ya trae los 6 |
| D6 | Moneda: el prototipo asume Lempiras. ¿Hay CxP en otra moneda? | Conversión al corte o columna por moneda |
| D7 | Rendimiento del `LATERAL` con muchos proveedores | Si molesta en producción, materializar o reescribir con una sola pasada |

---

## 9. Riesgos

- **No cuadra con el mayor.** El aging mide documentos vivos, no la cartera contable de SIMAFI. Hay
  que comunicarlo en la pantalla y el PDF, no dejar que se lea como saldo contable.
- **`LATERAL` re-escanea por proveedor.** Aceptable para un reporte bajo demanda; medir en el mirror
  con el universo real de MERENDON antes de dar F0 por cerrado (D7).
- **Compromisos sin vencimiento propio** caen en «por vencer» el día de emisión y envejecen desde
  ahí. Mientras no se resuelva D4, un compromiso viejo puede verse más "joven" de lo real.
