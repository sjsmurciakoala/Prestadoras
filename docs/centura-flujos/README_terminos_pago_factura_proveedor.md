# Términos de pago en la factura de proveedor — flujo Centura y plan de mejora

Fecha de revisión: 2026-08-11
Fuentes:
- Centura legacy: `E:\Koala\Users\Dell\Documents\GitHub\SIAD_Centura\APP ZIP\GA_IN.APT`
  - Facturación de proveedor: `frmFacturacionPRV` (líneas 51931–54803).
  - Orden de compra: `frmOCMercaderias` (líneas 6073–11675).
  - Ventas (contraste): `frmCotizaciones` (31246–36989), `frmPedidos` (42383–48532), `frmFacturacion` (54804–61023).
- Portal: `apc.Client/Pages/Almacen/RecepcionCompraFormPage.razor`, `SIAD.Services/Almacen/RecepcionCompraService.cs`, `SIAD.Core/DTOs/Almacen/*`.
- Complementa: [`README_compras_recepcion_proveedor.md`](README_compras_recepcion_proveedor.md) (flujo completo de recepción) y [`README_orden_compra.md`](README_orden_compra.md).

Motivo: documentar **cómo el legacy establece y usa los términos de pago al facturar a un proveedor**, para tener una base fiel antes de decidir mejoras en el portal. Este documento es **análisis + propuesta**; la sección 7 está **sujeta a cambios**.

> **Estado (2026-08-11):** las mejoras **§7.1 (catálogo `alm_termino_pago`)** y **§7.2 (autocálculo del vencimiento)** están **IMPLEMENTADAS** en local (rama `feat/almacen-integracion-contable`): tabla, API, pantalla CRUD (`/almacen/terminos-pago`) y cableo en la factura de recepción (combo + vencimiento = fecha factura + días, editable). SQL `Database/2026-08-11_alm_termino_pago.sql` aplicado al **mirror**; SRV pendiente. §7.3–7.6 siguen pendientes.

> **Método:** en Centura el carácter `!` marca comentario (código muerto). Abajo se distingue lo vigente. Las líneas `GA_IN.APT:NNNN` son trazables.

---

## 1. Respuesta corta

En el legacy, para el **proveedor** los términos de pago son **texto libre** (no un catálogo) y la **fecha de vencimiento es de captura manual** (no se calcula). Ambos se **heredan de la orden de compra** al validar el número de O/C. En **ventas** (cliente) es al revés: los términos son un **catálogo** (`CLN_TERMINOS_PAGO`) con días de crédito y descuento por pronto pago, y el vencimiento se **calcula** (`fecha + días`).

El portal hoy replica el comportamiento del proveedor: "Términos de pago" es un cuadro de texto libre y "Vence" es una fecha manual — fiel al legacy, con las mismas limitaciones.

---

## 2. El flujo en Centura (frmFacturacionPRV)

`frmFacturacionPRV` (título "Facturación"): *"Form para efectuar la facturacion de los proveedores, luego de haber generado una orden de compra"* (`GA_IN.APT:51979`).

### 2.1 Origen del dato: la orden de compra

En la O/C (`frmOCMercaderias`), `TERMINOS_PAGO` es un **campo de texto libre** `dfTerminosPago`, clase `clsCamposDatos`, **50 caracteres** (`GA_IN.APT:7757`), que se graba en `OC_ORDENCOMP_HDR.TERMINOS_PAGO` (`GA_IN.APT:11402`, `11611`). No hay validación contra ningún catálogo.

### 2.2 Herencia al facturar

Al teclear/validar el número de O/C en la factura, el sistema comprueba que la O/C exista y **no esté ya facturada** (`NUM_ORDEN_COMPRA NOT IN (SELECT ... FROM PRV_FACTURAS_HDR)`, `GA_IN.APT:52488`) y lee de la O/C sus condiciones:

```
SELECT ALL TERMINOS_PAGO, ROUND(DESCUENTO/SUB_TOTAL*100,2), COD_PROVEEDOR,
           PREPAGADA, TOTAL, OTROS_GASTOS, PUNTO_DE_VENTA
FROM   OC_ORDENCOMP_HDR
WHERE  NUM_ORDEN_COMPRA = :dfNumOrdenCompraX
INTO   :dfTerminosPago, :dfPorcentajeDescuento, :sCodProveedor, :sPrePagada, ...
```
(`GA_IN.APT:52433`, variante no-SQLSERVER en `:52445`.)

El campo `dfTerminosPago` en la factura es de nuevo **texto libre**, `clsCamposDatos`, **50 caracteres**, editable (`GA_IN.APT:53027`). Se muestra precargado desde la O/C y el usuario puede modificarlo.

### 2.3 Caso prepagada

Si la O/C viene con `PREPAGADA = '1'`, la factura fuerza el término y **bloquea** el campo (`GA_IN.APT:52461-52463`):
```
Set dfTerminosPago = 'PRE-PAGADA'
Call SalDisableWindow(dfTerminosPago)
```
Además toma la tasa de cambio del pago bancario ya realizado (`BNC_KARDEX_CUENTAS`, tipo `OCP`, `GA_IN.APT:52464-52468`) y no genera cuenta por pagar.

### 2.4 Fecha de vencimiento: MANUAL

`dfFechaVencimiento` es un campo de fecha editable (`clsCamposFechas`, formato `dd-MM-yyyy`, máscara `99/99/9999`, `GA_IN.APT:52604`). En **todo** `frmFacturacionPRV` **no existe** ninguna asignación `Set dfFechaVencimiento = fecha + días`: el usuario la teclea. Su único `MU_VALIDAR` omite la validación cuando la O/C es prepagada (`GA_IN.APT:52634-52637`).

### 2.5 Grabación y efecto de los términos

Al registrar (`pbGrabar`/F2), tras el INSERT de cabecera/detalle, kardex de inventario y contabilidad:

1. **Cabecera** `INSERT INTO PRV_FACTURAS_HDR (... TERMINOS_PAGO ...)` con `TERMINOS_PAGO = :dfTerminosPago` (`GA_IN.APT:54442/54465`; rama gemela en `:54631/54639`). **La cabecera guarda el texto de los términos, pero NO guarda la fecha de vencimiento** (solo `FECHA_TRANSACCION`, `FECHA_FACTURA_PROVEEDOR`, `FECHA_CREACION`).
2. **Si la O/C no es prepagada** (`sPrePagada = '0'`, `GA_IN.APT:54504`):
   - Lee `SALDO_ACTUAL` del proveedor (`PRV_PROVEEDORES`); si el proveedor tiene saldo a favor de la empresa, aplica **nota de crédito** (`NotaCreditoPRV`) para saldarlo (`GA_IN.APT:54505-54527`).
   - Genera la **cuenta por pagar** en el kardex del proveedor: correlativo `('P','COM')` + `ActKardexExistenciasPRV()` (`GA_IN.APT:54537-54539`).
   - Inserta el **plan de pago** en `PRV_DESGLOSE_PAGO` con `FECHA_PAGO = :dfFechaVencimiento`, `VALOR_A_PAGAR = total`, `STATUS = '2'`, `NUM_PAGO = 1` (`GA_IN.APT:54531-54534`).
3. **Descarga la O/C**: `UPDATE OC_ORDENCOMP_DTL SET CANTIDAD_APLICADA = CANTIDAD_APLICADA - :cantRecibida` (`GA_IN.APT:54546`).

Es decir, **la fecha de vencimiento solo cobra efecto en el desglose de pago** (`PRV_DESGLOSE_PAGO`), no en la cabecera de la factura. Los términos de pago (texto) quedan como referencia informativa.

---

## 3. Contraste con ventas (por qué importa)

En ventas los términos SÍ son estructurados y automáticos:

| Aspecto | Proveedor (`frmFacturacionPRV`) | Cliente (`frmFacturacion`/`frmPedidos`/`frmCotizaciones`) |
|---|---|---|
| Origen del término | Texto libre heredado de la O/C | **Catálogo** `CLN_TERMINOS_PAGO` (combo `cmbTerminosPago`/`cmbC`/`cmbP`) |
| Días de crédito | No existen como dato | `CLN_TERMINOS_PAGO.DIAS` (`GA_IN.APT:36158` "DIAS AS DIAS_VENCE") |
| Descuento pronto pago | No existe | `CLN_TERMINOS_PAGO.DESCUENTO` → `nDescuentoTermino` (`GA_IN.APT:32910`) |
| Término por defecto | No | Sí (`CLN_TERMINOS_PAGO WHERE [DEFAULT]=1`, `GA_IN.APT:42331`) |
| Término por entidad | No | Por cliente: `CLN_TERMINOS_CLN` (`GA_IN.APT:56046`) |
| Fecha de vencimiento | **Manual** (`dfFechaVencimiento`) | **Calculada**: `dfFechaVencimiento = dfFechaSistema + dfDiasCancelar` (`GA_IN.APT:56314`, `60364`, `62448`, `66655`, `68685`) |

Estructura conocida de `CLN_TERMINOS_PAGO` (por el uso en código, no por DDL): `COD_TERMINO`, `NOMBRE`, `DIAS`, `DESCUENTO` (pronto pago), `DEFAULT`. Relación cliente↔término: `CLN_TERMINOS_CLN (COD_CLIENTE, COD_TERMINO)`.

---

## 4. Estado en el portal (paridad actual)

| Dato | Legacy proveedor | Portal (`RecepcionCompraFormPage`) | Nota |
|---|---|---|---|
| Términos de pago | `dfTerminosPago` texto libre (50) | `DxTextBox` libre → `alm_compra_hdr.terminos_pago` | Fiel al legacy |
| Vencimiento | `dfFechaVencimiento` manual | `DxDateEdit` manual → `fecha_vencimiento` | Fiel al legacy |
| Prepagada | fuerza `'PRE-PAGADA'` + bloquea | No modelado | Ver §7 |
| Cuenta por pagar / desglose | `PRV_DESGLOSE_PAGO` + kardex PRV | **Fuera de alcance** (D-7 del README de recepción) | El vencimiento aún no alimenta CxP en el portal |

Conclusión: en el portal el vencimiento **se captura pero todavía no tiene consumidor** (no hay CxP/compromiso ligado a la recepción). Es un dato "informativo" hasta que se conecte con proveedores/compromisos.

---

## 5. Dependencias de BD

| Objeto | Módulo | Estado | Relevancia |
|---|---|---|---|
| `alm_compra_hdr.terminos_pago` | portal | existente (paso 25, sin aplicar SRV) | guarda el texto libre |
| `alm_compra_hdr.fecha_vencimiento` | portal | existente (paso 25, sin aplicar SRV) | manual, sin consumidor aún |
| `CLN_TERMINOS_PAGO` | legacy | existente (SQL Server MERENDON) | catálogo de referencia para el diseño |
| `PRV_DESGLOSE_PAGO` | legacy | existente | destino real del vencimiento en el legacy |
| Catálogo de términos para proveedor en el portal | portal | **no existe** | requerido por las mejoras §7.1–7.2 |

> Cualquier cambio de estructura que salga de §7 debe pasar por la skill **guardia-estructura-bd** y registrarse con **runbook-despliegue-srv**. Hoy no hay ningún script nuevo.

---

## 6. Trazabilidad

| Regla / dato | Fuente |
|---|---|
| Form y descripción | `GA_IN.APT:51931`, `:51979` |
| `dfTerminosPago` en O/C (texto libre 50) | `GA_IN.APT:7757` |
| INSERT `OC_ORDENCOMP_HDR.TERMINOS_PAGO` | `GA_IN.APT:11402`, `:11611` |
| Combo O/C solo no facturadas | `GA_IN.APT:52488` |
| Herencia de términos/prepagada al facturar | `GA_IN.APT:52433`, `:52445` |
| Prepagada fuerza `'PRE-PAGADA'` + bloquea | `GA_IN.APT:52461-52463` |
| `dfFechaVencimiento` manual (definición) | `GA_IN.APT:52604` |
| `dfTerminosPago` en factura (texto libre 50) | `GA_IN.APT:53027` |
| INSERT `PRV_FACTURAS_HDR.TERMINOS_PAGO` | `GA_IN.APT:54442`, `:54631/54639` |
| CxP / desglose de pago (`PRV_DESGLOSE_PAGO`, `FECHA_PAGO`) | `GA_IN.APT:54504-54534` |
| Descarga O/C (`CANTIDAD_APLICADA`) | `GA_IN.APT:54546` |
| Ventas: catálogo + días | `GA_IN.APT:32902-32911`, `:36158`, `:42331` |
| Ventas: vencimiento calculado | `GA_IN.APT:56314`, `:60364`, `:62448` |
| Portal | `apc.Client/Pages/Almacen/RecepcionCompraFormPage.razor`, `SIAD.Services/Almacen/RecepcionCompraService.cs` |

---

## 7. Propuestas de mejora — SUJETO A CAMBIOS

> Todo lo de esta sección es **propuesta para discusión**, no diseño cerrado ni implementación. Se prioriza de menor a mayor impacto. Cada punto marca lo que requiere **decisión del usuario/contador**.

### 7.1 Catálogo de términos de pago para proveedor (base de todo) — HECHO 2026-08-11
> Implementado como `alm_termino_pago` (por empresa: `nombre`, `dias`, `es_default`, `activo`). La factura guarda `termino_pago_id` + snapshot en `terminos_pago`/`plazo_dias`. Se mantiene el texto libre para el histórico. Pantalla `/almacen/terminos-pago`.
- **Problema:** el texto libre produce inconsistencia ("30 días", "Crédito 30", "CR30", "cred 30 d"): no se puede filtrar, agrupar, ni derivar vencimientos ni antigüedad de saldos.
- **Propuesta:** catálogo `alm_termino_pago` (o `cmp_termino_pago`) scoped por `company_id`: `{ id, nombre, dias, activo, es_default }`. La factura guarda `termino_pago_id` **más** un snapshot del nombre (para que el histórico no cambie si el catálogo cambia). Es el mismo modelo que ventas ya tiene en el legacy (`CLN_TERMINOS_PAGO`).
- **Compatibilidad:** conservar `terminos_pago` (texto) para no romper el histórico SIMAFI ni las facturas ya capturadas; el catálogo se vuelve la vía nueva.
- **Decisión pendiente:** ¿se adopta catálogo o se mantiene texto libre? ¿Se comparte un único catálogo con ventas o uno propio de compras?

### 7.2 Cálculo automático del vencimiento — HECHO 2026-08-11
> Al elegir el término se propone `fecha_vencimiento = (fecha factura ?? fecha) + días`, editable. El servidor recalcula si el cliente no lo fijó. Se preselecciona el término predeterminado al abrir la factura.
- **Problema:** `fecha_vencimiento` es manual y opcional → olvidos y errores que después distorsionan cualquier CxP/antigüedad.
- **Propuesta:** al elegir el término, autocompletar `fecha_vencimiento = fecha_factura + termino.dias`, **editable** como override (idéntico a ventas: `fecha + días`). Si no hay término (contado), vencimiento = fecha de la factura.
- **Requiere:** 7.1 (los días viven en el catálogo).

### 7.3 Término por defecto por proveedor — HECHO 2026-08-11
> Implementado como columna `prv_proveedores.termino_pago_id` (FK → `alm_termino_pago`). Combo "Término de pago" en el maestro de proveedores; al elegir el proveedor en la factura se precarga su término y recalcula el vencimiento (si no lo tiene, queda el predeterminado global). SQL `Database/2026-08-11_prv_proveedor_termino_pago.sql` en el mirror; SRV pendiente (§3.8).
- **Propuesta:** cada proveedor puede tener su término habitual; al elegir el proveedor se precarga (equivalente a `CLN_TERMINOS_CLN`). Menos tecleo, más consistencia. Tabla `prv_proveedor_termino` o columna en el maestro de proveedores.
- **Decisión tomada:** columna en el maestro de proveedores.

### 7.4 Condición de pago explícita (contado / crédito / prepagado)
- **Problema:** hoy el legacy infiere el comportamiento mezclando "prepagada" (flag de la O/C) con el texto de términos; el portal no distingue.
- **Propuesta:** un campo estructurado `condicion_pago` (enum numérico, siguiendo `EstadosNumericos`): **Contado**, **Crédito**, **Prepagado**. Hace el flujo determinista y es lo que decide si se genera CxP y con qué vencimiento. Sustituye la heurística de texto.

### 7.5 Cerrar el ciclo con Cuentas por Pagar → ver §9 (diseño detallado)
- **Contexto:** el vencimiento en el legacy existe **para** alimentar `PRV_DESGLOSE_PAGO` (plan de pago) y la CxP del proveedor. En el portal eso está fuera de alcance (D-7 del README de recepción).
- **Propuesta:** que registrar la factura genere su **cuenta por pagar** con vencimiento. Los términos estructurados (7.1–7.4) son **prerrequisito**.
- **Decisión tomada (2026-08-11):** **CxP propia de compras** (no reutilizar el módulo de compromisos/OPD), y **todas las facturas —contado y crédito— se pagan en una misma vista**. El diseño completo, con el hallazgo del legacy y las preguntas para el contador, está en **§9**.

### 7.6 Cuotas / plan de pago multi-vencimiento
- **Observación:** el legacy ya insinúa cuotas (`PRV_DESGLOSE_PAGO.NUM_PAGO`), aunque en la práctica graba una sola (`NUM_PAGO = 1`).
- **Propuesta (opcional, depende de 7.5):** permitir varias cuotas con fechas y montos (crédito a plazos). Solo si el negocio lo usa.

### 7.7 Mejoras de captura (UX), independientes del catálogo
- Precargar el término/proveedor desde la O/C ya ocurre; extenderlo a autocompletar vencimiento (7.2).
- Avisar si el vencimiento es anterior a la fecha de la factura o si falta en una compra a crédito.
- Descuento por pronto pago (existe para clientes vía `CLN_TERMINOS_PAGO.DESCUENTO`): evaluar si aplica en compras. **Probablemente no** sin pedido del usuario — marcar como duda.

### 7.8 Orden de implementación sugerido
1. `condicion_pago` (7.4) + catálogo `alm_termino_pago` (7.1) — cambio aditivo, bajo riesgo.
2. Autocompletar vencimiento (7.2) + default por proveedor (7.3) — solo UI/servicio.
3. Integración con CxP/compromisos (7.5) — mayor impacto, requiere decisión del contador.
4. Cuotas (7.6) — opcional.

---

## 8. Dudas abiertas (para el usuario)

1. ¿Se adopta un **catálogo** de términos para proveedor o se mantiene el **texto libre** actual?
2. Si hay catálogo: ¿**compartido** con ventas (`CLN_TERMINOS_PAGO` espejado) o **propio** de compras?
3. ¿El vencimiento debe **calcularse** automáticamente desde los días del término?
4. ¿La factura de proveedor debe **generar la CxP/compromiso** al registrarse, o eso queda en el módulo de proveedores como hoy?
5. ¿Se necesita **descuento por pronto pago** en compras (existe en ventas)?
6. ¿Aplica el concepto **prepagado** (bloqueo de términos + tasa del pago) en el portal?

> Ningún cambio se implementa ni se aplica en BD hasta que el usuario lo indique.

---

## 9. Diseño — Pagos a proveedores: CxP propia de compras (SUJETO A CAMBIOS)

> Diseño para discutir, **NO implementado**. Responde a "las facturas al crédito ¿cómo se pagan?" y al requisito del usuario (2026-08-11): **todas las facturas — contado y crédito — se pagan en la MISMA vista.** Arquitectura elegida: **CxP propia de compras** (no reutilizar el módulo de compromisos/OPD, que es gasto presupuestario).

### 9.1 Lo que confirmó el legacy (verificado 2026-08-11)

- **La contabilidad de la compra está DESCONECTADA:** `Function: ActualizarContabilidad` es un **stub** que solo hace `Return TRUE` (`GA_IN.APT:54318-54326`); la factura lo llama (`:54483`) pero **no hay ni un `INSERT INTO CNT_PARTIDAS`** en todo el flujo de la factura de compra.
- **La CxP SÍ se genera, aparte:** kardex del proveedor `PRV_KARDEX` (movimiento `'COM'` con **saldo corrido**, clase `clsKardex_Proveedores`, INSERT en `GA_CP.APT:26391`) + `PRV_DESGLOSE_PAGO` (plan de pago con `FECHA_PAGO = vencimiento`) + `UPDATE PRV_PROVEEDORES.SALDO_ACTUAL`.
- **Conclusión:** en el legacy la **CxP y la contabilidad van desacopladas**. La cuenta por pagar se lleva como **libro auxiliar** aunque el mayor esté apagado. → Es válido generar la CxP sin contabilidad.

### 9.2 Principios de diseño

1. **Toda factura genera su documento por pagar** (CxP), sea contado o crédito. El **contado** es una CxP que **vence hoy** (`vencimiento = fecha factura`); el **crédito**, `fecha + días`. Así **todas caen en la misma vista de pago**.
2. **CxP propia de compras** (decisión del usuario), NO `prv_compromiso_hdr`. Ver §9.3.
3. **La CxP es independiente de la contabilidad** (como el legacy). El asiento es una **capa opcional** (§9.6), encendida cuando el contador resuelva **D-1**.
4. **Reutilizar el motor de pagos existente** (abonos + retenciones F4 + posteo del pago + reversa) que ya vive en el módulo de compromisos/OPD (`prv_compromiso_abono`, `AbonoCompromisoCalculator`, `PrvContabilidad` module `PROV`).

### 9.3 Modelo de datos propuesto

Tabla nueva **`alm_compra_cxp`** (cuenta por pagar de compra), 1:1 con la factura:

| Campo | Notas |
|---|---|
| `id`, `company_id` | multiempresa (`ICompanyScopedEntity`). |
| `compra_hdr_id` | FK → `alm_compra_hdr`, **UNIQUE** = una CxP por factura (idempotencia). |
| `cod_proveedor` | snapshot. |
| `fecha`, `fecha_vencimiento` | de la factura / del término. |
| `condicion_pago` | numérico (§7.4): Contado / Crédito / Prepagado. |
| `monto` | total de la factura. |
| `saldo` | **derivado** `monto − Σ abonos vigentes` (patrón `AbonoCompromisoCalculator`); materializar sólo si hace falta. |
| `estado_id` | numérico (`EstadosNumericos`): Pendiente / Parcial / Pagada / Anulada. |
| auditoría | usuario/fecha creación-modificación. |

**Abonos:** reutilizar el patrón `prv_compromiso_abono` — tabla hermana `alm_compra_cxp_abono` con la misma forma (`numero_abono`, `fecha`, `monto`, `metodo_pago`, `banco_cuenta_id`, `ban_kardex_id`, `partida_id`, `estado` V/A) **o** generalizar la de compromisos. Decisión abierta (§9.7).

### 9.4 La vista unificada de pagos (lo que pide el prototipo)

Pantalla **"Pagos a proveedores"** que lista **todas las CxP** (contado + crédito):
- **Columnas:** proveedor · No. factura (SAR) · fecha · **vencimiento** · condición · total · **abonado** · **saldo** · estado · **días para vencer / vencida**.
- **Filtros:** proveedor · estado (pendiente/parcial/pagada) · **solo vencidas** · rango de vencimiento · condición.
- **Semáforo** de vencimiento (verde por vencer / ámbar próximo / rojo vencido).
- **Acción Pagar/Abonar** por fila → panel: monto (total o parcial), método (efectivo/cheque/transferencia), cuenta bancaria, **retenciones** (F4, ya existe); genera el movimiento bancario y —si D-1— el asiento.
- **Totales** al pie: por vencer, vencido, saldo total.
- (Opcional) selección múltiple → **pago en lote** a un proveedor.

### 9.5 Contado: ¿se paga solo o entra a la vista? (decisión del usuario)

- **A (recomendada, unifica):** el contado nace como CxP pendiente y **se paga desde la misma vista** ("vence hoy"). Un solo flujo para todo. ← coincide con el requisito "todas en la misma vista".
- **B:** el contado se marca **pagado automáticamente** al registrar la factura. Menos pasos, pero dos caminos.

### 9.6 Contabilidad (capa opcional, Fase 2 — D-1 del contador)

Hoy desacoplada (como el legacy). Cuando se conecte, el estándar sería:
- **Al facturar (nacimiento):** `DEBE Inventario` (+ `ISV crédito fiscal` si aplica) / `HABER Proveedor (CxP)`. — **falta hoy en el portal y en el legacy.**
- **Al pagar:** `DEBE Proveedor` / `HABER Banco` (+ retenciones al HABER). — **el portal ya lo hace** en OPD/abonos (motor `module='PROV'`).

Sin el asiento de nacimiento, el pago debita al proveedor sin contrapartida → mayor descuadrado. Ese asiento es el que **debe definir el contador** (mismo D-1 de la integración contable de compras).

### 9.7 Preguntas concretas para el contador

1. **Asiento de la compra a crédito** (nacimiento de la CxP): ¿cuenta de inventario/gasto al DEBE?, ¿ISV crédito fiscal separado o al costo?, ¿cuenta del proveedor (CxP) al HABER?
2. ¿La CxP contable nace al **registrar la factura** o en un proceso de contabilización aparte?
3. **Contado:** ¿se contabiliza como compra + pago inmediato, o sólo el pago?
4. ¿Se requiere **antigüedad de saldos** (aging) por proveedor como reporte?

### 9.8 Fases de implementación (estado)

- **Fase 0** — `condicion_pago` explícita (§7.4). ✅ **HECHA 2026-08-12** (mirror + build).
- **Fase 1** — CxP propia + vista de pagos + abonos. Sub-fases:
  - **F1a** datos + generación/anulación de la CxP. ✅ **HECHA 2026-08-12** (mirror; `RecepcionCompraTests` 32/32).
  - **F1b** servicio/API de pagos (abono con movimiento bancario + anulación). 🔎 **en diseño** — ver §9.10.
  - **F1c** pantalla de pagos (prototipo aprobado). ⏳ pendiente.
- **Fase 2** — asiento de nacimiento (proveedor al HABER), gated por **D-1**. ⏸
- **Fase 3** — cuotas/plan multi-vencimiento (§7.6), aging, pago en lote.

### 9.9 Trazabilidad del hallazgo legacy

| Regla / dato | Fuente |
|---|---|
| `ActualizarContabilidad` = stub (`Return TRUE`) | `GA_IN.APT:54318-54326` |
| La factura llama a `ActualizarContabilidad()` | `GA_IN.APT:54483`, `:54675` |
| Kardex del proveedor (CxP) `'COM'` con saldo corrido | `GA_IN.APT:54253-54280` (clase) · `GA_CP.APT:26391` (INSERT `PRV_KARDEX`) |
| Plan de pago `PRV_DESGLOSE_PAGO` (`FECHA_PAGO`=vencimiento) | `GA_IN.APT:54531-54534` |
| `UPDATE PRV_PROVEEDORES.SALDO_ACTUAL` | `GA_CP.APT:26385-26390` |
| Motor de pago del portal (reutilizable) | `SIAD.Services/Presupuesto/PrvContabilidad.cs`, `AbonoCompromisoCalculator.cs`, `OrdenesPagoDirectoService.cs` |

### 9.10 F1b — motor de pago (diseño detallado, SUJETO A CAMBIOS)

**Decisión del usuario (2026-08-12):** el pago **mueve el saldo bancario de verdad** (registra el kardex de la cuenta y emite cheque si aplica); el **asiento contable** (póliza) queda **diferido a la Fase 2** (`partida_id` nulo hasta entonces). Coherente con el legacy: el kardex bancario y el del proveedor son auxiliares que se llevan sin el mayor. *(Rumbo propuesto; pendiente de confirmación del usuario antes de implementar.)*

**Piezas del portal que se reutilizan tal cual** (mapeadas del módulo OPD por el análisis de 2026-08-12):

| Pieza | Ubicación | Uso en la CxP |
|---|---|---|
| `IChequesService.EmitirChequeAsync` | `SIAD.Services/Bancos/ChequesService.cs:35` | Emite el cheque; `origen="COMPRA_CXP"`, `origenDocumento="CXP-{id}"`. Genérico, no sabe de compromisos. |
| `IBanTransaccionesService.AnularMovimientoAsync` | `SIAD.Services/Bancos/BanTransaccionesService.cs:1793` | Reversa del pago al anularlo (recalcula saldos, anula el cheque). |
| SP `sp_ban_kardex_registrar_movimiento` | servidor (no en `Database/`) | Inserta `ban_kardex` y devuelve el saldo corrido de la cuenta (OUT `p_ban_kardex_id`, `p_saldo_resultante`). |
| SP `sp_ban_kardex_anular_movimiento_recalcular` | servidor | Reversa del kardex. |
| Entidades `ban_kardex` / `ban_cheque` / `ban_cuenta` | `SIAD.Core/Entities/` | Kardex bancario (multiempresa `company_id`), cheque, cuenta (`proximo_cheque`, `cheque_maximo`, `saldo_actual`, `cont_account_id`). |
| Constante `ChequeOrigen` | `SIAD.Core/DTOs/.../ChequesDtos.cs:6` | Añadir `CompraCxp`. |

**Lo que se replica/adapta** (hoy acoplado a `prv_compromiso_*`, se rehace contra `alm_compra_cxp`):
- **Lock + saldo** bajo `FOR UPDATE`. Ventaja: `alm_compra_cxp.saldo` es **materializado** → `SELECT saldo … FOR UPDATE` y `UPDATE saldo = saldo − monto` (más simple que recalcular al vuelo). Ref: `OrdenesPagoDirectoService.RegistrarAbonoAsync:988`, `LockCompromisoRowAsync:1263`.
- **Movimiento bancario + cheque:** extraer/replicar `RegisterLinkedBankMovementAsync:2552` parametrizando la referencia y `origen_modulo` (hoy `'PROV_COMPROMISO'` hardcoded).
- **Retenciones** (F4, opcional): `PersistRetencionesAplicadasAsync:1417` + `ValidateRetencionesConsistencia:1380`.
- **Contabilidad:** `PrvContabilidad` → `CompraCxpContabilidad` (module + docType) queda para la **Fase 2**; en F1b **no se postea** (gate por `IntegracionContableConfigSql.ObtenerConfigAsync()`, patrón de `AlmacenContabilidad.cs:100-101`). El kardex bancario tolera `partida_id` nulo (`RegisterLinkedBankMovementAsync:2606-2619`).

**Servicio nuevo `CompraCxpAbonoService` (`SIAD.Services/Almacen/`):**
- `ListarAsync(filtro)` — CxP por pagar: proveedor, vencimiento, condición, monto, abonado, saldo, estado; filtros por estado, proveedor, sólo vencidas, rango.
- `RegistrarAbonoAsync(cxpId, dto)` — en una transacción: (1) `FOR UPDATE` de la CxP; (2) valida `0 < monto ≤ saldo`; (3) movimiento bancario (kardex) + cheque si aplica; (4) retenciones opcionales; (5) INSERT `alm_compra_cxp_abono` (`numero_abono = MAX+1`, estado 'V'); (6) `saldo -= monto`, `estado_id` → Parcial/Pagada. Idempotencia por `uq_alm_compra_cxp_abono_num`.
- `AnularAbonoAsync(cxpId, numeroAbono, motivo)` — sólo el último vigente: reversa bancaria (`AnularMovimientoAsync`), abono → 'A', `saldo += monto`, reabre estado. Idempotente.

**DTOs:** `CompraCxpListItemDto` (vista), `CompraCxpAbonoUpsertDto` (monto, método, `BancoCuentaId`, cheque, retenciones, fecha), `CompraCxpAbonoResultadoDto` (saldo/estado/nº cheque).

**Dependencias de servidor:** los SP `sp_ban_kardex_*` viven en `siad_v3` (no versionados en `Database/`); el mirror los tiene por ser un restore. Los tests de F1b que muevan banco dependen de ellos.

**Trazabilidad del motor OPD reutilizado:** `OrdenesPagoDirectoService.RegistrarAbonoAsync:988` · `RegisterLinkedBankMovementAsync:2552` · `AnularAbonoAsync:3323` · `ChequesService.EmitirChequeAsync:35` · `BanTransaccionesService.AnularMovimientoAsync:1793` · `PrvContabilidad.PostearPagoAsync:41`.

> Ningún cambio se implementa ni se aplica en BD hasta que el usuario lo indique.
