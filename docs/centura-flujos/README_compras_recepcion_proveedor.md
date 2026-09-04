# Recepción de compra de almacén (factura de proveedor) — diseño del flujo para el portal

Fecha de revisión: 2026-07-30
Fuentes:
- Centura legacy: `E:\Koala\Users\Dell\Documents\GitHub\SIAD_Centura\APP ZIP\GA_IN.APT`, formulario `frmFacturacionPRV` (líneas 51931–54803).
- Modelo del portal: `SIAD.Core/Entities/alm_*`, `SIAD.Services/Almacen/*`, `apc.Client/Pages/Almacen/*`.
- Diseño aprobado (mockup): https://claude.ai/code/artifact/d5c86137-bc3b-49e3-829f-728f127ba5ed
- Complementa: [`README_isv_ingresos_almacen.md`](README_isv_ingresos_almacen.md) (política de ISV en la compra).

Motivo: migrar la **captura** de compras de almacén (recepción de factura de proveedor) desde Centura al portal. Documento de diseño previo a la implementación; **nada de esto está implementado ni aplicado en BD**.

> **Método:** en Centura el carácter `!` marca comentario (código muerto). Abajo se distingue lo vigente. Las líneas `GA_IN.APT:NNNN` son trazables.

---

## 1. Respuesta corta

Hoy en el portal **no existe la captura de compras**: `/almacen/compras` ([ComprasList.razor](../../apc.Client/Pages/Almacen/ComprasList.razor)) es **solo consulta**, sobre una tabla plana `alm_compra` que hoy solo contiene el histórico migrado de SIMAFI. La pantalla que sí captura es la de Centura (`frmFacturacionPRV`, "Facturación" de proveedor).

Lo que hay que construir en el portal:
1. **Pantalla de recepción** (mockup aprobado).
2. **Servicio de alta** de la compra (crear filas `alm_compra` con `origen = SIAD`).
3. **Habilitar el tipo COMPRA en el motor de posteo** (`InventarioPostingService`), que hoy postea carga inicial, ajustes y reversa, pero **lanza `NotSupportedException` para compra** ([InventarioPostingService.cs:46-55](../../SIAD.Services/Almacen/InventarioPostingService.cs)).

Dos decisiones del usuario amplían/ajustan el legacy:
- Se permite **compra directa sin orden de compra** (Centura siempre exige O/C).
- Un artículo solo se puede comprar a un proveedor si existe en almacén **con su código de proveedor** (`alm_articulo_proveedor.codigo_upc`); si falta, se registra en un **popup** sin salir de la pantalla.

---

## 2. Estado actual del portal (punto de partida)

| Pieza | Archivo | Estado |
|---|---|---|
| Listado de compras (consulta) | `ComprasList.razor`, `ComprasService.GetAsync` | Existe. Solo lectura. |
| Tabla de compras | `alm_compra` (plana, línea por artículo) | Existe. Hoy solo histórico SIMAFI + posteo por `uuid`/`posteado`/`origen`. Sin cabecera ni captura. |
| Kardex | `alm_kardex` (inmutable, solo INSERT; trigger `trg_alm_kardex_inmutable`) | Existe. |
| Existencia/costo por bodega | `alm_articulo_bodega` (`existencia`, `costo_promedio`, `ultimo_costo`, `existencia_transito`) | Existe. |
| Código de proveedor del artículo | `alm_articulo_proveedor.codigo_upc` (+ `.costo`, `.principal`) | Existe. Se captura en la pestaña Proveedores del artículo. |
| ISV de compra por tipo | `alm_tipo_articulo.impuesto_tasa_id` | Existe (versión simple: gravado → suma al costo). Ver README ISV. |
| Motor de posteo | `InventarioPostingService.PostearAsync` | Existe. **No postea COMPRA** (NotSupportedException). El promedio ponderado móvil ya está implementado (AjustePositivo). |
| Alta/posteo de compra | — | **No existe.** |

---

## 3. El flujo en Centura (referencia de paridad)

### 3.1 Cabecera (`Contents` del form)

| Etiqueta | Control | Traza | Notas |
|---|---|---|---|
| O/C No. | `dfNumOrdenCompraX` (combo) | :52390 | Solo O/C no facturadas (`NOT IN (SELECT NUM_ORDEN_COMPRA FROM PRV_FACTURAS_HDR)`, :52486). Al validar trae proveedor, términos, % descuento, otros gastos, prepagada (:52433). |
| Factura No. (SAR) | `dfNumFactura85` + `dfNumFactura84` | :52491, :52521 | Dos campos con guión (ej. `00100101 - 00000001`). |
| Fecha factura proveedor | `dfFecFacturaProveedor` | :52570 | |
| Fecha | `dfFecha1` (= fecha del sistema) | :52938 | Fecha de la transacción. |
| Vence | `dfFechaVencimiento` | :52604 | |
| Tasa/US$ | `dfTasaCambioUSD` | :52825 | Config. código 21; 1 = Lempiras. |
| Descuento (%) | `dfPorcentajeDescuento` / `cbDescuentoFijo`+`dfDescuentoFijo` | :52638, :54000 | Global. Fijo o porcentual. |
| Términos/Pago | `dfTerminosPago` | :53027 | Auto de la O/C; editable. |
| Bodega | `cmbBodegas` | :52668 | Solo bodegas con `PERMITE_COMPRAR = '1'`. Fija la moneda/tasa. |
| CAI | `dfCAI` | :52708 | |
| No. OCE / Otro doc. | `dfOCE` + `cbOtros` ("Otro Documento") | :54158, :54137 | |
| Observaciones | `mlObservaciones` (1000) | :52738 | |
| Detallar ISV | `cb3` "Detallar Impuesto sobre Ventas" | :54114 | **Forzada y bloqueada** si algún renglón trae ISV (:53650-53656). Marcada = ISV separado del costo; desmarcada = ISV capitalizado. |
| Consumo interno | `cbConsumoInterno` | :53159 | → `TIPO_CONSUMO`. |

### 3.2 Grilla `tblFactura` (:53180)

Columnas visibles: **Código** (`colCodigo`), **Descripción del Producto** (`colDescripcion`), **Costo Unitario** (`colCostoActualPRV`, editable), **Cant. Pedida** (`colCantidadPedida`), **Cant. Factura** (`colCantidadRecibida`), **I.V.A.** (`colImpuestoPRV`), **Total** (`colCostoActualPRVTotal`), **Cant. X Paquete** (`colCantidad_X_Unidad`), **Cant. Unidades** (`colCantRecUnidades`). Ocultas: `colISVPorcentaje`, `colcb3`.

Se llena desde la O/C (:53624-53643):
```
SELECT OC_ORDENCOMP_DTL.COD_PRODUCTO, INV_PRODUCTOS.NOMBRE, COSTO_UNITARIO,
       CANTIDAD_PEDIDA, CANTIDAD_PEDIDA, (CANTIDAD_PEDIDA*COSTO_UNITARIO),
       INV_PRODUCTOS.CANT_X_UNIDAD, OC_ORDENCOMP_DTL.IMPUESTO,
       AXL_IMPUESTOS.PORCENTAJE/100, ...
FROM OC_ORDENCOMP_DTL, INV_PRODUCTOS, AXL_IMPUESTOS
WHERE ... AND OC_ORDENCOMP_DTL.NUM_ORDEN_COMPRA = :dfNumOrdenCompraX
```
Validación por renglón: la cantidad facturada no puede exceder la cantidad por aplicar de la O/C (:53388).

### 3.3 Grabación (`MU_GRABAR` / botón `pbGrabar` F2, :54566)

Secuencia vigente (SQL Server):
1. Recalcular (`pbCalcular`) y validar (`FluValidarInformacion`).
2. **Productos nuevos revisados**: si algún `INV_PRODUCTOS.FLAG_REVISADO` = falso, exige revisión por usuario privilegiado (`esJefe`) antes de facturar (:54589-54598).
3. **Correlativo interno de factura**: `CNF_CONFIGURACION` código `'47'`, leído e incrementado con bloqueo por concurrencia → `sNumFacturaPRV2`, rellenado a 6 dígitos (:54600-54629).
4. **INSERT `PRV_FACTURAS_HDR`** (cabecera, :54434 / :54631): `NUM_FACTURA_PROV` (correlativo interno), `NUM_ORDEN_COMPRA`, `DESCUENTO_APLICADO`, `FECHA_TRANSACCION`, `OBSERVACIONES`, `SUB_TOTAL`, `DESCUENTO`, `IMPUESTO`, `TERMINOS_PAGO`, `NUM_FACTURA_EXT`/`NUM_FACTURA_EXTC` (SAR), `VALOR_FACTURA`, `USUARIO_CREO`, `FECHA_CREACION`, `COD_BODEGA`, `TASA_CAMBIO_USD`, `OTROS_GASTOS`, `TIPO_CONSUMO`, `FLETEYSEGURO`, `FECHA_FACTURA_PROVEEDOR`, `CAI`, `FLAG_OTRO_GASTOS`, `OCE`.
5. **INSERT `PRV_FACTURAS_DTL`** (detalle, `FluApplyEdits`, :54478).
6. Correlativo de inventario `('I','COM')` y **`ActKardexExistenciasINV()`** → asienta el kardex y recalcula existencias/costo (README ISV §2.2/2.7; `INV_KARDEX` tipo `'COM'`, `INV_EXISTENCIAS`).
7. **`ActualizarContabilidad()`** → asiento contable.
8. Si la O/C no es prepagada: genera **cuentas por pagar** al proveedor y **desglose de pago** (`PRV_DESGLOSE_PAGO`), con N/C si el saldo del proveedor es negativo (:54504-54536).
9. Correlativo `('P','COM')` y **`ActKardexExistenciasPRV()`** (kardex del proveedor / CxP).
10. **Descarga la O/C**: `UPDATE OC_ORDENCOMP_DTL SET CANTIDAD_APLICADA = CANTIDAD_APLICADA - :cantRecibida` (:54546).
11. `COMMIT`.

---

## 4. Mapeo Centura → portal

### 4.1 Modo de captura

| Modo | Centura | Portal (aprobado) |
|---|---|---|
| **Con orden de compra** (predeterminado) | Único modo. La grilla se llena de la O/C. | La grilla se llena de la O/C. **Depende de que exista un modelo de órdenes de compra en el portal (hoy NO existe** — ver §7 D-2). |
| **Compra directa** (sin O/C) | No existe. | Proveedor como ancla; los renglones se agregan con un buscador. Diseño nuevo. |

### 4.2 Campos → `alm_compra` (tabla plana, una fila por renglón)

| Dato | Centura | Columna `alm_compra` | Acción |
|---|---|---|---|
| Fecha transacción | `FECHA_TRANSACCION` | `fecha` | existe |
| Fecha factura proveedor | `FECHA_FACTURA_PROVEEDOR` | `fecha_factura` | existe |
| Artículo | `COD_PRODUCTO` | `articulo_id` (+ `codigo_articulo` snapshot) | existe |
| Cantidad recibida | `colCantidadRecibida` | `cantidad` | existe |
| Costo unitario | `colCostoActualPRV` | `precio_unitario` | existe |
| ISV del renglón | `colImpuestoPRV` | `impuesto` | existe |
| Descuento | `DESCUENTO` | `descuento` | existe |
| Total renglón | `colCostoActualPRVTotal` | `total` | existe |
| Bodega destino | `COD_BODEGA` | `bodega_id` | existe (CHECK obliga en SIAD) |
| Tipo de compra | (términos) | `tipo_compra`, `plazo_dias` | existe |
| Proveedor (catálogo) | `COD_PROVEEDOR` | **`cod_proveedor`** (hoy solo `proveedor` texto) | **NUEVO** |
| No. factura SAR | `NUM_FACTURA_EXT` + `NUM_FACTURA_EXTC` | **`numero_factura_sar`** (string; `numero_factura` es `decimal`, no cabe) | **NUEVO** |
| CAI | `CAI` | **`cai`** | **NUEVO** |
| Vencimiento | `FECHA_VENCIMIENTO` | **`fecha_vencimiento`** | **NUEVO** |
| Términos de pago | `TERMINOS_PAGO` | **`terminos_pago`** | **NUEVO** |
| Moneda + tasa | `TASA_CAMBIO_USD` | **`moneda`**, **`tasa_cambio`** | **NUEVO** |
| Consumo interno | `TIPO_CONSUMO` | **`consumo_interno`** | **NUEVO** |
| Override ISV por factura | `cb3` | **`detallar_isv`** | **NUEVO** |
| Agrupador de documento (N renglones = 1 factura) | `NUM_FACTURA_PROV` | **`codigo_compra`** o `numero` (definir) | decidir (§7 D-3) |
| Idempotencia / posteo | — | `uuid`, `posteado`, `fecha_posteo`, `origen` | existe |

### 4.3 Numeración interna
Centura usa `CNF_CONFIGURACION` código `47`. En el portal, correlativo **por `company_id`** (tabla/secuencia nueva o el patrón de correlativos ya usado en presupuesto/cheques). **NUEVO**.

### 4.4 Kardex y existencias → motor de posteo
`ActKardexExistenciasINV()` de Centura ≡ `InventarioPostingService.PostearAsync` del portal, que ya:
- deriva `uuid` determinista e idempotencia,
- bloquea la fila (`FOR UPDATE`),
- aplica **promedio ponderado móvil** sobre `alm_articulo_bodega` (`existencia`, `costo_promedio`, `ultimo_costo`),
- inserta el asiento inmutable en `alm_kardex`,
- recalcula el rollup de cabecera del artículo.

Falta: un **tipo `COMPRA`** en `TipoMovimientoInventario` + `documento_tipo` COMPRA + su rama de `Calcular`/`Validar` (entrada con costo > 0; la fórmula del promedio ya existe en `AjustePositivo`). El costo del asiento = costo del renglón, **con ISV incluido para artículos gravados** (versión simple, README ISV).

### 4.5 Fuera de alcance de la 1ª entrega (a decidir)
- **Contabilidad** (`ActualizarContabilidad`): el asiento del ISV depende de D1/D12 (contador). Ver §7.
- **Cuentas por pagar / desglose de pago / N/C** al proveedor (`PRV_DESGLOSE_PAGO`): en el portal esto toca el módulo de proveedores/compromisos (compromisos ya migrados de SIMAFI). Dependencia a definir.
- **Descarga de la O/C** (`CANTIDAD_APLICADA`): solo aplica al modo Con O/C.

---

## 5. Reglas de negocio (decisiones tomadas)

- **D-A. Dos modos**: Con O/C (predeterminado) y Compra directa. *Usuario, 2026-07-30.*
- **D-B. Código de proveedor obligatorio**: solo se compra un artículo a un proveedor si existe `alm_articulo_proveedor` (activo) con `codigo_upc` para ese par. Si falta, popup que da de alta la relación (mismo patrón que `ArticuloProveedoresTab`) sin salir de la pantalla. Se valida contra el proveedor de la cabecera (un artículo puede tener varios proveedores). *Usuario, 2026-07-30.*
- **D-C. ISV**: por defecto se resuelve por `alm_tipo_articulo.impuesto_tasa_id` (gravado = suma al costo; exento/NULL = no). La casilla "Detallar ISV" se conserva como **override manual por factura**. *Usuario, 2026-07-30.*
- **D-D. Consumo interno**: casilla, se persiste (`consumo_interno`).
- **D-E. Costo > 0**: el motor rechaza costo 0 (corrompería el promedio). Aplica a cada renglón.
- **D-G. Una O/C admite una o más facturas** (*usuario, 2026-07-31*): la recepción es acumulativa. Cada factura descarga lo que recibe en `cantidad_aplicada`, la orden queda **Recibida parcial** mientras quede pendiente y pasa a **Cerrada** cuando todos sus renglones están cubiertos. Nunca se puede recibir más de lo pendiente, ni sumando facturas: el servicio bloquea los renglones de la orden con `FOR UPDATE` antes de validar el tope, para que dos recepciones simultáneas no lean el mismo pendiente.
- **D-F. Factura irrepetible por proveedor**: índice único parcial sobre `(company_id, cod_proveedor, numero_factura_sar)` que **excluye las anuladas** (`estado <> 9`). Una factura anulada se puede recapturar. Consecuencia verificada en el mirror: si después se intenta **des-anular** la original, la BD lo rechaza con `unique_violation` — si algún día se implementa "reactivar", el servicio debe validarlo antes y devolver un mensaje claro, no un 500.

---

## 6. Matriz de dependencias de BD (para producción)

| Objeto | Tipo | Estado | Acción | Script |
|---|---|---|---|---|
| `alm_compra` | tabla | existente | **ALTER**: `cod_proveedor`, `numero_factura_sar`, `cai`, `fecha_vencimiento`, `terminos_pago`, `moneda`, `tasa_cambio`, `consumo_interno`, `detallar_isv`, agrupador de documento | pendiente de crear |
| Correlativo de recepción por empresa | secuencia/tabla | — | **CREAR** | pendiente |
| `alm_kardex` | tabla | existente | sin cambio (documento_tipo COMPRA es dato, no DDL) | — |
| `alm_articulo_bodega` | tabla | existente | sin cambio | — |
| `alm_articulo_proveedor` | tabla | existente | sin cambio (ya tiene `codigo_upc`, `costo`) | — |
| `alm_tipo_articulo.impuesto_tasa_id` | columna | existente (SQL paso 20 sin aplicar en SRV) | verificar aplicado antes de compras | ver README ISV |
| Constante `TipoDocumentoInventario.Compra` / `TipoMovimientoInventario.Compra` | C# | — | **CREAR** (no BD) | — |
| Modelo de órdenes de compra (modo Con O/C) | tablas | **no existe en el portal** | decidir alcance (§7 D-2) | pendiente |

> Cuando estos cambios se materialicen en scripts `Database/*.sql`, deben registrarse con la skill **runbook-despliegue-srv** en el runbook del SRV. Aún no se ha creado ningún script.

---

## 7. Casos borde y dudas pendientes

> **Confirmado por el usuario (2026-07-31):** la **contabilidad** (D-1) y las **cuentas por pagar / desglose de pago** (D-7) quedan **PENDIENTES a propósito** — se implementarán más adelante, en otra entrega. La recepción opera sin ellas: captura, costea y mueve el kardex.

- **D-1 (contador).** Destino del ISV de compra: al costo vs. crédito fiscal. Bloquea el **asiento contable**, no la captura ni el costeo simple. Ver README ISV §5 (D1/D12) y memoria `pendiente-isv-al-costo-o-credito`.
- ~~**D-2 (usuario).** Modo **Con O/C**: el portal no tiene modelo de órdenes de compra.~~ **RESUELTO 2026-07-30**: el módulo de O/C se implementó (`alm_orden_compra`, `alm_orden_compra_detalle`, `alm_orden_compra_correlativo`; script `Database/2026-07-30_alm_orden_compra.sql`, paso 23 del runbook). El modo Con O/C es viable desde la 1ª entrega.
- ~~**D-3.** Agrupador de documento.~~ **CERRADO 2026-07-31**: **cabecera real `alm_compra_hdr`**. Los datos de factura (proveedor, SAR, CAI, bodega, moneda, descuento global, otros gastos, flete) son del documento, no del renglón; en la tabla plana se repetirían en cada línea y el descuento/flete global no tendría dónde vivir para prorratearse. `alm_compra` sigue siendo la **unidad de posteo** (un `uuid` por línea): el motor no cambia y el histórico SIMAFI queda con `compra_hdr_id` NULL.
- ~~**D-4.** Numeración.~~ **CERRADO 2026-07-31**: contador por `company_id` (`alm_compra_correlativo`) con `UPDATE ... RETURNING`, idéntico a `alm_orden_compra_correlativo`. No se replica el patrón `CNF_CONFIGURACION` de Centura.
- ~~**D-5.** Número de factura SAR.~~ **CERRADO 2026-07-31**: columna nueva `alm_compra_hdr.numero_factura_sar` `VARCHAR(30)`. `alm_compra.numero_factura` (`NUMERIC(11,0)`) se deja como está, sirviendo al histórico SIMAFI.
- **D-6.** Productos "nuevos/no revisados": Centura bloquea facturar productos con `FLAG_REVISADO` falso hasta que un jefe los revise. ¿Se replica en el portal? (En el portal el artículo ya debe existir con código de proveedor — D-B —, lo que cubre parte de esto.)
- **D-7.** Cuentas por pagar / desglose de pago al proveedor: ¿la recepción genera el compromiso/CxP en el portal (módulo proveedores) o queda como paso aparte?
- **D-8.** Multimoneda (Lps/USD): ¿se soporta en la 1ª entrega o solo Lempiras?

---

## 8. Trazabilidad

| Regla / dato | Fuente |
|---|---|
| Formulario y título | `GA_IN.APT:51931-54803` (frmFacturacionPRV, "Facturación") |
| Combo O/C (solo no facturadas) | `GA_IN.APT:52486` |
| Carga de renglones desde la O/C | `GA_IN.APT:53624-53643` |
| Tope por cantidad de la O/C | `GA_IN.APT:53388` |
| Casilla ISV forzada si hay impuesto | `GA_IN.APT:53650-53656` |
| Correlativo interno (CNF 47) | `GA_IN.APT:54600-54629` |
| INSERT cabecera PRV_FACTURAS_HDR | `GA_IN.APT:54434-54475` |
| Kardex/existencias + contabilidad | `GA_IN.APT:54480-54483` (+ README ISV §2) |
| Cuentas por pagar / desglose de pago | `GA_IN.APT:54504-54536` |
| Descarga de la O/C (CANTIDAD_APLICADA) | `GA_IN.APT:54546` |
| Motor de posteo del portal | `SIAD.Services/Almacen/InventarioPostingService.cs` |
| Tabla de compras del portal | `SIAD.Core/Entities/alm_compra.cs` |
| Código de proveedor del artículo | `SIAD.Core/Entities/alm_articulo_proveedor.cs` (`codigo_upc`) |
| Mockup aprobado | https://claude.ai/code/artifact/d5c86137-bc3b-49e3-829f-728f127ba5ed |

---

## 9. Próximos pasos

1. ~~Resolver D-2 y D-3/D-4/D-5.~~ **HECHO** (ver §7).
2. ~~Crear el script SQL y registrarlo en el runbook SRV.~~ **HECHO 2026-07-31**: `Database/2026-07-31_alm_compra_recepcion.sql` (cabecera + correlativo + `alm_compra.compra_hdr_id`), registrado como **paso 25** del runbook. **Sin aplicar en ninguna base** — ni mirror ni SRV.
3. ~~Habilitar el tipo COMPRA en `InventarioPostingService`.~~ **HECHO 2026-07-31**: `TipoMovimientoInventario.Compra`, entrada con promedio ponderado móvil (misma fórmula que `AjustePositivo`), asiento con `documento_tipo = COMPRA` y `tipo_transaccion = 102` (entrada), `es_ajuste = false`. El motor **fuerza** el tipo de documento y deriva el uuid de `COMPRA|company|línea de recepción|par`, para que un llamador que mande otro `DocumentoTipo` no pueda postear la misma línea dos veces. Validaciones: cantidad > 0, costo > 0 (regla D-E) y línea de recepción obligatoria.
4. **Entidades HECHAS 2026-07-31**: `alm_compra_hdr`, `alm_compra_correlativo`, `alm_compra.compra_hdr_id` y `alm_compra.orden_compra_detalle_id` (esta última existía en BD desde el paso 23 pero no estaba expuesta), `EstadoRecepcionCompra`, `MonedaCompra` y el mapeo en `SiadDbContext.Almacen.cs`.
5. **Servicio de alta HECHO 2026-07-31**: `IRecepcionCompraService` / `RecepcionCompraService` (+ DTOs y registro en `ServiceRegistration`). En UNA transacción: correlativo por empresa → cabecera → una línea de `alm_compra` por renglón → descarga de la O/C → posteo de cada línea al kardex. Idempotente por `uuid` de documento. Resuelve el ISV con las dos capas (tasa vigente del tipo de artículo + tratamiento de la empresa, con override por factura) y capitaliza al costo solo cuando corresponde. Consultas de apoyo: pendientes de una O/C y órdenes recibibles.
5c. **Anulación HECHA 2026-07-31**: `AnularAsync(id, motivo, user)` + `POST {id}/anular` + botón en el listado con popup de confirmación y motivo. Revierte en el kardex el asiento de cada renglón (contra-asiento REVERSA, nada se borra), devuelve `cantidad_aplicada` a la O/C bajo `FOR UPDATE` y recalcula su estado (Cerrada → Recibida parcial → Aprobada según lo que quede aplicado). **Exige que la mercadería siga en la bodega**: si ya salió, la reversa dejaría existencia negativa y se rechaza con mensaje accionable (corregir con un ajuste). Idempotente. El motivo se anexa a las observaciones como `[ANULADA por <usuario>: <motivo>]`; la traza de quién/cuándo va en `usuariomodificacion`/`fechamodificacion`.
> **Defecto del motor corregido de paso:** la reversa devolvía la existencia pero **no des-ponderaba el costo promedio** — 18 u. a 56.3889 + 6 u. a 58.00 daban 24 a 56.7917, y al revertir quedaban 18 a 56.7917: L.7.25 de valor inventados por un documento que ya no existe (medido en el mirror). Ahora `Calcular` resta al inventario el valor exacto que ese documento le sumó; con existencia 0 (revertir una apertura) conserva el costo en vez de fabricar uno.

5b. **Controller, cliente y pantallas HECHOS 2026-07-31**: `RecepcionesCompraController` (`api/almacen/recepciones-compra`, módulo `compras`), `RecepcionesCompraClient`, `RecepcionesCompraList.razor` (estándar de grid) y `RecepcionCompraFormPage.razor` (modo Con O/C predeterminado / compra directa, cabecera fiel al mockup, renglones con tope por pendiente, totales), más la entrada **"Recepción de compras"** en el menú. **Pendiente**: la **anulación** de una recepción (reversa de los asientos + devolver `cantidad_aplicada`), que hoy no existe: una factura registrada no se puede corregir.
6. **Tests HECHOS**: 7 casos de compra en `InventarioPostingTests.cs` (motor) y 20 en `RecepcionCompraTests.cs` (servicio), incluidos varios facturas contra una misma O/C, el tope acumulado, el ISV capitalizado vs. detallado y la idempotencia. Suite de Almacén: 171/171 contra el mirror.

Nada se aplica en BD hasta que el usuario lo indique.
