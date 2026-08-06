# Órdenes de compra — diseño del flujo para el portal

Fecha de revisión: 2026-07-30
Fuentes:
- Centura legacy: `E:\Koala\Users\Dell\Documents\GitHub\SIAD_Centura\APP ZIP\GA_IN.APT`, formulario `frmOCMercaderias` (líneas 6073–11675).
- Modelo del portal: `SIAD.Core/Entities/alm_*`, `SIAD.Services/Almacen/*`.
- Diseño aprobado (mockup): https://claude.ai/code/artifact/7cc8ee95-e907-4cd9-b5c7-e4b854603c95
- Encadena con: [`README_compras_recepcion_proveedor.md`](README_compras_recepcion_proveedor.md) (resuelve su duda **D-2**).

Motivo: el usuario decidió **construir el módulo de órdenes de compra antes de la recepción**, para que el modo "Con O/C" de la recepción tenga de dónde leer. **El portal no tiene hoy ninguna pieza de O/C** — es un módulo nuevo completo. Documento de diseño previo a la implementación; **nada implementado ni aplicado en BD**.

> Método: en Centura `!` marca comentario (código muerto). Trazas `GA_IN.APT:NNNN`.

---

## 1. Respuesta corta

La orden de compra es el **pedido al proveedor** que se aprueba y luego se recibe. En el portal hay que crear: la pantalla (mockup aprobado), **dos tablas nuevas** (cabecera + detalle), el **workflow de estados**, la **numeración por empresa**, y el **enlace con la recepción** (`alm_compra`, que hoy solo tiene `orden_compra` como texto suelto). Los renglones se limitan a **artículos con código del proveedor** (`alm_articulo_proveedor.codigo_upc`), la misma regla de la recepción. Una O/C puede nacer de una **requisición** (`alm_requisicion`).

---

## 2. Alcance — decisiones aprobadas (mockup, 2026-07-30)

| # | Decisión | Estado |
|---|---|---|
| D-OC-1 | Proveedor del catálogo (`prv_proveedores`) como ancla; renglones filtrados a artículos con código de ese proveedor. | Aprobada |
| D-OC-2 | **Aprobación por permiso de rol** (no jerárquica tipo `esJefe`). | **Confirmada** (usuario, 2026-07-30) |
| D-OC-3 | **Descuento en porcentaje** (Centura lo maneja como monto). | Aprobada |
| D-OC-4 | **Bodega destino NO se fija en la O/C**, solo al recibir. | Aprobada |
| D-OC-5 | **Centro de costo por renglón**, registrado, **solo informativo** (sin validar presupuesto). | **Confirmada** (usuario, 2026-07-30) |
| D-OC-6 | **Workflow de estados simple**: Borrador → Aprobada → Recibida (parcial/total) → Cerrada, + Anulada. | Aprobada |
| D-OC-7 | **Carga desde requisición** (`alm_requisicion`) incluida. | Aprobada |
| D-OC-8 | **Se omiten** del legacy: exoneración/resolución fiscal, órdenes de trabajo, orden de importación (moneda/tasa), cobrar a cliente, peso/volumen/paletas. | Aprobada |

---

## 3. El flujo en Centura (referencia de paridad)

Formulario `frmOCMercaderias` "Ordenes de Compra" (`GA_IN.APT:6073`, título :6077).

### 3.1 Cabecera
Proveedor (`dfCodProveedor`/`cmbNombreProveedor`, valida `PRV_PROVEEDORES STATUS='1'`, :7574) → `COD_PROVEEDOR`; No. O/C autonúmero (`dfNumOrdenCompra`) → `NUM_ORDEN_COMPRA`; Fecha (`FECHA_TRANSACCION`); Fecha de emisión (`FECHA_EMISION`, :10274); Términos de pago (`TERMINOS_PAGO`, :7757); Destino/"Para uso de" (`mlPointOfSale`) → `PUNTO_DE_VENTA` (:7825); Calcula ISV (`cbCalculaISV`) → `FLAG_CALCULA_IMPUESTO` (:7859); Descuento (`dfPorcentajeDescuento`, **usado como monto** :6928) → `DESCUENTO`; Otros gastos (`OTROS_GASTOS`, :9771); Totales calculados (`SUB_TOTAL`, `IMPUESTO`, `TOTAL`, `UNIDADES_PEDIDO`). Campos legacy omitidos: Resolución/Exonerada (:7297/:7895), Local/Internacional (:7694), Prepagada (:9803, oculto), Cobrar a cliente (`cb4`/`COD_CLIENTE`, :10304), Peso/Volumen (:9365/:9416).

### 3.2 Grilla `tblOrdenCompra` (:8082)
Código propio (`colMyCode` → `COD_PRODUCTO`, poblado de `INV_PRODUCTOS` filtrado por proveedor vía **`LINK_PROD_PROV STATUS='1'`** :8142); Código proveedor (`colYourCode`); Rubro/Capítulo (`colCodRubro`); Centro de costo (`colCentroCosto` → `COD_CENTROCOSTO`, :8323); Descripción (`INV_PRODUCTOS.NOMBRE`); Cantidad pedida (`colQuantity` → `CANTIDAD_PEDIDA`); Costo unitario (`colPrice` → `COSTO_UNITARIO`); Impuesto (`colImpuestoOC` = precio×cant×%ISV cuando `cbCalculaISV`, :8497) → `IMPUESTO`; `CANTIDAD_APLICADA` (lo ya recibido/facturado, :52470, se descuenta al facturar).

### 3.3 Estados `COD_STATUS_OC`
Catálogo configurable `OC_STATUS` (flags `FLAG_FACTURAR/FLAG_FLOW_BEGIN/FLAG_EMITIDA/...`) + permisos por función `CNF_FUNCIONES_SOC` (:6462-6475). Valores hallados: **NULL** = creada sin aprobar; **1** = Aprobada (:6996); **8** = Almacenada/archivada, irreversible (:7396); **9** = Anulada (:2601); **7** = excluida de facturación. El SELECT de O/C facturables excluye `9,7,8` y `NULL` (:52485).

### 3.4 Numeración, aprobación, requisiciones
- **Numeración**: correlativo `CNF_CONFIGURACION` código `'12'` (normal) / `'45'` (exonerada) (:7935/:11477).
- **Aprobación**: `pbAprobar` fija `COD_STATUS_OC=1`, **solo visible si** `dbo.esJefe(USUARIO_CREO, :SYSTEM_USER)=1 AND COD_STATUS_OC IS NULL` (:6551, :6996) — jerárquica. Coexiste con el workflow `cmbStatusOC` (:6483).
- **Requisición**: botón "Requisiciones" carga renglones de `INV_REQUISICION_DTL WHERE FLAG_SELECCIONADO_PARA_COMPRA=1` (:11007); al grabar marca `FLAG_APLICADO_EN_COMPRA=1` y, si no quedan pendientes, `INV_REQUISICION_HDR.FLAG_EN_OC=1` (:11430-11440).

---

## 4. Modelo de datos propuesto (portal — TODO NUEVO)

Nomenclatura propuesta: prefijo `alm_` (el módulo de almacén). **Confirmar** (§9 D-OC-c). Multitenant vía `ICompanyScopedEntity` (todas las tablas llevan `company_id` con filtro global y stamping, como el resto del portal).

### 4.1 `alm_orden_compra` (cabecera)
| Columna | Tipo | Notas |
|---|---|---|
| `id` | int PK | |
| `company_id` | bigint | tenant |
| `numero` | text/int | correlativo por empresa (§4.3) |
| `fecha` | date | |
| `fecha_emision` | date | |
| `cod_proveedor` | text | FK lógica a `prv_proveedores` (keyless/multiempresa; se valida en servicio) |
| `terminos_pago` | text | |
| `destino_uso` | text | ≡ `PUNTO_DE_VENTA` |
| `calcula_isv` | bool | |
| `descuento` | numeric | **porcentaje** (D-OC-3) |
| `otros_gastos` | numeric | |
| `sub_total`, `impuesto`, `total` | numeric | calculados |
| `estado` | smallint | numérico (§4.4), ver `EstadosNumericos.cs` |
| `observaciones` | text | |
| `aprobado_por`, `fecha_aprobacion` | text/timestamp | auditoría de aprobación |
| `requisicion_id` | int? | FK a `alm_requisicion` si nació de una (D-OC-7) |
| `usuariocreacion/modificacion`, `fechacreacion/modificacion` | | auditoría estándar |

### 4.2 `alm_orden_compra_detalle` (renglón)
| Columna | Tipo | Notas |
|---|---|---|
| `id` | int PK | |
| `company_id` | bigint | tenant |
| `orden_compra_id` | int FK | → `alm_orden_compra` |
| `articulo_id` | int FK | → `alm_articulo` |
| `codigo_upc` | text | snapshot del código de proveedor (`alm_articulo_proveedor.codigo_upc`) |
| `centro_costo` | text/int | referencia a centro de costo de contabilidad |
| `descripcion` | text | |
| `cantidad_pedida` | numeric | |
| `costo_unitario` | numeric | |
| `impuesto` | numeric | |
| `total` | numeric | |
| `cantidad_aplicada` | numeric | lo ya recibido (lo descuenta la recepción); cierre cuando = pedida |

### 4.3 Numeración
Correlativo **por `company_id`** (mismo patrón ya usado en presupuesto/cheques). Reemplaza el `CNF_CONFIGURACION` código 12/45 de Centura. **NUEVO**.

### 4.4 Estados (numéricos, no strings — ver `EstadosNumericos.cs`)
Propuesta: `1` Borrador · `2` Aprobada · `3` Recibida parcial · `4` Cerrada (recibida total) · `9` Anulada. Transiciones: Borrador→Aprobada (permiso), Aprobada→Recibida parcial/Cerrada (por la recepción), cualquiera antes de recibir→Anulada.

### 4.5 Enlace con la recepción
`alm_compra` gana una FK real al renglón de O/C (p. ej. `orden_compra_detalle_id`) además del `orden_compra` texto actual. Al postear la recepción, se incrementa `cantidad_aplicada` del renglón y, si todos los renglones quedan cubiertos, la O/C pasa a **Cerrada**.

---

## 5. Mapeo Centura → portal

| Centura (`OC_ORDENCOMP_*`) | Portal (`alm_orden_compra*`) |
|---|---|
| `NUM_ORDEN_COMPRA` | `alm_orden_compra.numero` |
| `COD_PROVEEDOR` | `cod_proveedor` |
| `FECHA_TRANSACCION` / `FECHA_EMISION` | `fecha` / `fecha_emision` |
| `TERMINOS_PAGO` | `terminos_pago` |
| `PUNTO_DE_VENTA` | `destino_uso` |
| `FLAG_CALCULA_IMPUESTO` | `calcula_isv` |
| `DESCUENTO` (monto) | `descuento` (**%**, D-OC-3) |
| `OTROS_GASTOS` | `otros_gastos` |
| `SUB_TOTAL`/`IMPUESTO`/`TOTAL` | `sub_total`/`impuesto`/`total` |
| `COD_STATUS_OC` (catálogo `OC_STATUS`) | `estado` (numérico simple, D-OC-6) |
| `COD_PRODUCTO` | `detalle.articulo_id` (+ `codigo_upc`) |
| `COD_CENTROCOSTO` | `detalle.centro_costo` |
| `CANTIDAD_PEDIDA` / `COSTO_UNITARIO` / `IMPUESTO` | `detalle.cantidad_pedida`/`costo_unitario`/`impuesto` |
| `CANTIDAD_APLICADA` | `detalle.cantidad_aplicada` |
| Correlativo `CNF_CONFIGURACION` 12/45 | correlativo por `company_id` |
| `LINK_PROD_PROV` (productos por proveedor) | `alm_articulo_proveedor` (activo, con `codigo_upc`) |
| Requisición `INV_REQUISICION_*` | `alm_requisicion` |
| Aprobación `dbo.esJefe` | permiso de rol (`PermissionNames`) |

---

## 6. Reglas de negocio, estados y validaciones

- Proveedor debe existir y estar **activo**.
- Cada renglón: artículo **con `codigo_upc` activo** para el proveedor de la cabecera (misma regla que la recepción; si falta, alta por popup — reutiliza el flujo ya diseñado).
- Cantidad pedida > 0; costo unitario > 0.
- ISV por renglón según `calcula_isv` y la tasa del tipo de artículo (`alm_tipo_articulo.impuesto_tasa_id`); coherente con la política de la recepción (README ISV).
- **Aprobar**: transición Borrador→Aprobada, protegida por permiso; estampa `aprobado_por`/`fecha_aprobacion`. Una O/C aprobada ya no edita renglones.
- **Anular**: permitida mientras `cantidad_aplicada = 0` en todos los renglones.
- **Recepción parcial**: varias recepciones pueden aplicar contra la misma O/C hasta cerrarla.

---

## 7. Matriz de dependencias de BD (todo NUEVO)

| Objeto | Tipo | Estado | Acción | Script |
|---|---|---|---|---|
| `alm_orden_compra` | tabla | — | **CREAR** (multitenant, índices tenant-safe) | pendiente |
| `alm_orden_compra_detalle` | tabla | — | **CREAR** (FK a cabecera y a `alm_articulo`) | pendiente |
| Correlativo de O/C por empresa | secuencia/tabla | — | **CREAR** | pendiente |
| `alm_compra.orden_compra_detalle_id` | columna | existente (tabla) | **ALTER** (FK al renglón de O/C) | pendiente |
| `alm_requisicion` (marca "aplicada en O/C") | tabla | existente | verificar/ALTER si falta la marca | pendiente |
| Permisos de O/C (`module.compras.ordenes.*`) | catálogo C# | — | **CREAR** en `PermissionNames` (+ `PermissionEndpointCatalog`) | — |
| Entidades EF + partial context | C# | — | **CREAR** (`SiadDbContext.*.cs`) | — |

> Cuando estos cambios se materialicen en `Database/*.sql`, registrarlos con la skill **runbook-despliegue-srv**. Aún no se ha creado ningún script.

---

## 8. Dependencias con otros módulos

- **Proveedores** `prv_proveedores` (catálogo, activo).
- **Artículos** `alm_articulo` + `alm_articulo_proveedor` (`codigo_upc`, `costo`).
- **Requisiciones** `alm_requisicion` (origen opcional de renglones).
- **Centros de costo** (contabilidad) — referencia por renglón.
- **Presupuesto** `pst_*` — validación **opcional** (fuera de esta entrega salvo confirmación).
- **Recepción** `alm_compra` — consume la O/C (modo Con O/C) y descuenta `cantidad_aplicada`.

---

## 9. Casos borde y dudas pendientes

- ~~**D-OC-a.** Aprobación.~~ **RESUELTO (2026-07-30):** por **permiso de rol** (no jerárquica).
- ~~**D-OC-b.** Validación de presupuesto.~~ **RESUELTO (2026-07-30):** centro de costo **solo informativo**, sin validar presupuesto.
- **D-OC-c.** Nomenclatura de tablas (`alm_orden_compra` vs `oc_*` vs `com_*`) y de permisos.
- **D-OC-d.** Recepción parcial y cierre automático: confirmar la regla (¿cierra al 100% o admite tolerancia?).
- **D-OC-e.** ¿Multimoneda alguna vez? (hoy solo Lempiras; Centura tiene moneda/tasa solo en Orden de Importación, omitida).
- **D-OC-f.** ISV contable (D1/D12 del contador) — afecta el asiento de la recepción, no la O/C. Ver README ISV.

---

## 10. Trazabilidad

| Regla / dato | Fuente |
|---|---|
| Formulario y título | `GA_IN.APT:6073, :6077` |
| Cabecera (proveedor, términos, destino, ISV, descuento, otros gastos) | `GA_IN.APT:7541-7952`, `:9771` |
| Grilla `tblOrdenCompra` | `GA_IN.APT:8082-8978` |
| Productos por proveedor (`LINK_PROD_PROV`) | `GA_IN.APT:8142` |
| Estados `COD_STATUS_OC` (1/8/9/7, catálogo `OC_STATUS`) | `GA_IN.APT:6996, :7396, :2601, :52485, :6462-6475` |
| Numeración (CNF 12/45) | `GA_IN.APT:7935, :11477, :11389` |
| Aprobación jerárquica (`esJefe`) | `GA_IN.APT:6551, :6996` |
| Carga desde requisición | `GA_IN.APT:10974-11018, :11430-11440` |
| Descuento como monto | `GA_IN.APT:6928` |
| Mockup aprobado | https://claude.ai/code/artifact/7cc8ee95-e907-4cd9-b5c7-e4b854603c95 |
| Enlace con recepción | `README_compras_recepcion_proveedor.md` (D-2) |

---

## 11. Próximos pasos (no ejecutados)

1. Confirmar D-OC-a/b/c/d (aprobación, presupuesto, nomenclatura, cierre).
2. Crear scripts `Database/*.sql` de las tablas nuevas + correlativo + ALTER de `alm_compra`, y registrarlos en el runbook SRV.
3. Entidades EF + partial context + servicio + controller + cliente + pantalla (mockup aprobado).
4. Enganchar la recepción "Con O/C" (cerrar la duda D-2 del README de recepción).
5. Tests de integración (numeración por empresa, estados/transiciones, aprobación por permiso, recepción parcial).

Nada de lo anterior se implementa ni se aplica en BD hasta que el usuario lo indique.
