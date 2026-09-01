# Requisiciones y descargos de almacén (salida de bodega) — diseño del flujo para el portal

Fecha de revisión: 2026-07-31
Fuentes:
- Centura legacy: `E:\Koala\Users\Dell\Documents\GitHub\SIAD_Centura\APP ZIP\GA_IN.APT` (menús, consumo de la requisición en la O/C, diálogo `dlgSeleccionarRequisiciones`), `GA_CP.APT` (O/C de servicios) y `Casajaar_Final\NEWAPP\GA_AD.APT` (motor de kardex — ver §5.3, **fuente indirecta**).
- Binarios legacy (no hay fuente): `APP ZIP\prGarantias.dll` (Delphi, `TfrmRequisicion` + todo el SQL de requisiciones) y `APP ZIP\prSiad.dll` (C#/WinForms, gemelo del ingreso y las dos pantallas de aprobación).
- Modelo del portal: `SIAD.Core/Entities/alm_*`, `SIAD.Services/Almacen/*`, `apc.Client/Pages/Almacen/*`.
- Mirror Postgres `siad_v3_restore` (solo `SELECT`) para todas las cifras de este documento.
- Complementa: [`README_compras_recepcion_proveedor.md`](README_compras_recepcion_proveedor.md) (patrón cabecera/línea y motor de posteo) y [`README_orden_compra.md`](README_orden_compra.md).

Motivo: migrar la **salida de bodega por requisición** desde el legacy al portal. Documento de diseño previo a la implementación; **nada de esto está implementado ni aplicado en BD**.

> **Método:** en Centura el carácter `!` al inicio marca comentario (código muerto). Abajo se distingue lo vigente. Las líneas `GA_IN.APT:NNNN` son trazables. Lo que no pude verificar en código o en datos va marcado **NO CONFIRMADO**.

---

## 1. Respuesta corta

**Qué hay hoy.** En el portal, requisiciones y descargos son **dos pantallas de solo consulta** sobre dos tablas planas migradas de MySQL `bdsimafi`. `RequisicionesService` y `DescargosService` tienen **dos métodos cada uno** (`GetAsync` + `GetDepartamentosAsync`), ambos de lectura ([RequisicionesService.cs:20](../../SIAD.Services/Almacen/RequisicionesService.cs), [DescargosService.cs:20](../../SIAD.Services/Almacen/DescargosService.cs)). **Nadie escribe** en `alm_requisicion` ni en `alm_descargo`.

**Qué hay en Centura.** El "Ingreso de Requisiciones" **tampoco está en Centura**: el menú solo hace `Call ShowRequisiciones( SYSTEM_USER )` (`GA_IN.APT:2733`), función externa declarada bajo `Library name: prGarantias` (`GA_IN.APT:623`, `:742`). Verificado con grep recursivo: **no hay un solo `INSERT INTO INV_REQUISICION_*` en ningún `.APT`**. Centura solo **consume** requisiciones al armar la orden de compra. La captura, la aprobación y la entrega viven en `prGarantias.dll` (Delphi) y `prSiad.dll` (C#).

**El hallazgo que manda sobre todo el diseño.** En el mirror, el mismo hecho está **contado tres veces**:

| Tabla | Líneas | Valor |
|---|---:|---:|
| `alm_requisicion` | 42.866 | L 68.172.601,59 |
| `alm_descargo` | 42.757 | L 68.044.988,25 |
| `alm_kardex` (`documento_tipo IS NULL`, `tipo_transaccion='202'`) | 42.696 | L 67.996.763,43 |

Cruzando por `(numero, codigo_articulo)`: de **42.673** pares del kardex, **42.666** casan con descargo, **42.654** con requisición y **42.651 están en las tres**. La salida histórica **ya está asentada en el kardex**. De ahí las dos reglas estructurales de todo este diseño:

1. **Un solo documento postea**: el **DESCARGO** (la entrega). La requisición es la solicitud y **nunca toca el kardex**.
2. El histórico plano **no se migra ni se re-postea**; las tablas planas se reutilizan como *línea* de los documentos nuevos (patrón D-3 de compras), con el histórico marcado `origen='SIMAFI'` + `posteado=true` y sin cabecera.

**Qué hay que construir.**
1. Dos **cabeceras** nuevas (`alm_requisicion_hdr`, `alm_descargo_hdr`) + columnas aditivas en las dos tablas planas + dos correlativos por empresa.
2. **Habilitar la salida en el motor** (`InventarioPostingService`), que hoy lanza `NotSupportedException` para todo lo que no sea carga inicial, ajustes, reversa y compra ([InventarioPostingService.cs:37-47](../../SIAD.Services/Almacen/InventarioPostingService.cs)).
3. **Cerrar un defecto latente del motor antes de la primera salida**: la reversa **siempre resta** ([InventarioPostingService.cs:410-420](../../SIAD.Services/Almacen/InventarioPostingService.cs)) y `TipoTransaccionDe(Reversa)` es siempre `"202"` (`:435`). Anular un descargo con el motor tal como está **volvería a descargar** el inventario. Ver §7.1.
4. Servicios de captura/aprobación/despacho, controllers, permisos propios, pantallas y tests.

**Prerrequisito duro que hoy no se cumple.** El costeo: de **638** pares `alm_articulo_bodega`, **241 tienen existencia > 0 y `costo_promedio = 0`** (la Fase 8 del corte no se ha ejecutado) y **3 tienen existencia negativa**. Una salida se valoriza a `fila.costo_promedio` (`:389-394`): habilitar descargos antes del corte grabaría asientos a **valor 0 en un libro inmutable**. Ver §10.2.

---

## 2. Estado actual del portal (punto de partida)

| Pieza | Archivo | Estado |
|---|---|---|
| Listado de requisiciones (consulta) | `RequisicionesList.razor`, `RequisicionesService.GetAsync` | Existe. **Solo lectura**, a nivel línea. Ya cumple el estándar de grid. |
| Listado de descargos (consulta) | `DescargosList.razor`, `DescargosService.GetAsync` | Existe. **Solo lectura**. Ya cumple el estándar de grid. |
| Tabla de requisiciones | `alm_requisicion` (plana, línea por artículo) | Existe. 42.866 líneas, 17.045 números, **100 % `origen='SIMAFI'` + `posteado=true`**, `bodega_id` NULL en el 100 %. Sin cabecera, sin correlativo, sin `cantidad_despachada`. |
| Tabla de descargos | `alm_descargo` (plana) | Existe. 42.757 líneas, `numero_documento` máx. 17124, 100 % SIMAFI/posteado. Sin cabecera. |
| Infraestructura de posteo de ambas | `bodega_id`, `posteado`, `fecha_posteo`, `uuid`, `origen` + 4 CHECK + `uq_*_company_uuid` + `ix_*_pendiente` + `trg_*_blindaje` | **Existe** (script `Database/2026-07-14_alm_documentos_bodega_posteo.sql`). Verificado en el mirror. Idéntica a la de `alm_compra`. |
| Kardex | `alm_kardex` (inmutable, `trg_alm_kardex_inmutable`, SQLSTATE `K0001`) | Existe. `ck_alm_kardex_documento_tipo` **ya admite `REQUISICION` y `DESCARGO`** → no hay que ampliar el CHECK. |
| Existencia/costo por bodega | `alm_articulo_bodega` (`existencia`, `costo_promedio`, `ultimo_costo`, `existencia_comprometida`, `existencia_transito`) | Existe. `existencia_comprometida` y `existencia_transito` **valen 0 en las 638 filas** y **nadie las escribe** (solo se leen en `ArticuloUbicacionService`). Sin CHECK de no-negativos. |
| Motor de posteo | `InventarioPostingService.PostearAsync` | Existe. Postea carga inicial, ajustes, reversa y **compra**. **No postea salidas por documento.** |
| Reversa de salidas | `Calcular(Reversa)` | **Defectuosa para salidas** — ver §7.1. |
| Clave alterna `uq_*_tenant` en las planas | — | **NO EXISTE** en `alm_requisicion` ni en `alm_descargo` (verificado en `pg_constraint`). Prerrequisito de cualquier FK compuesta. |
| Correlativo por empresa | — | **No existe** para requisición ni descargo (solo `alm_compra_correlativo` y `alm_orden_compra_correlativo`). |
| Permisos propios | `PermissionNames` / `PermissionEndpointCatalog` | **No existen**. Ambos controllers cuelgan de `[ModuleAuthorize(PermissionModules.Inventario)]` a nivel clase. |
| Tests | `SIAD.Tests/Almacen/` (15 archivos) | **Ninguno** de requisiciones ni descargos. |
| Reportes | `SIAD.Reports/` | **Ningún** reporte de requisición ni vale de salida. |
| Alta / aprobación / despacho | — | **No existe.** |

---

## 3. El flujo en Centura (referencia de paridad)

### 3.1 Los menús (lo único que sí está en el `.APT`)

Verificado leyendo el bloque directamente:

| Menú | Resource Id | Permiso | Acción vigente | Traza |
|---|---|---|---|---|
| Ensambles | — | `arrSeguridad[3]` | `Call ShowEmsambles( SYSTEM_USER )` | `GA_IN.APT:2717-2723` |
| **Ingreso de Requisiciones** | 13816 | `arrSeguridad[32]` | `Call ShowRequisiciones( SYSTEM_USER )` | `GA_IN.APT:2725-2733` |
| Aprobación de Requisiciones de **Salida de Bodega** | 18102 | `arrSeguridad[33]` | `Call MostrarAprobarSalidasBodega()` — **sin usuario** | `GA_IN.APT:2735-2744` |
| Aprobación de Requisiciones de **Reabastecimiento** | 21857 | `arrSeguridad[33]` | `Call AprobacionRequisiciones( SYSTEM_USER )` | `GA_IN.APT:2746-2754` |
| Reporte de Productos Pendiente de Surtir | 42444 | (sin `Enabled when`) | `Call ShowCmpReq()` | `GA_IN.APT:3322-3331` |
| Reporte de Pedidos Sugeridos | 42445 | (sin `Enabled when`) | `Call showEstCmpReq()` | `GA_IN.APT:3332-3341` |
| Reporte de Requisiciones Pendientes de Entregar | 11619 | `arrSeguridad[8]` | `Call ShowReqPendientes()` | `GA_IN.APT:3342-3351` |
| Reporte de Consumo Por Producto | 1309 | — | **bloque `Menu Actions` VACÍO** (menú visible que no hace nada) | `GA_IN.APT:3352-3359` |

**Código muerto verificado (prefijo `!`), no migrar:**
- `! Call ShowAprobaciones( SYSTEM_USER )` — `GA_IN.APT:2743`. La vigente es la línea siguiente.
- `! Menu Item: &Requisicion` (popup `&Compras`, llamaba a `MostrarSeleccionarRequisicion` con un solo parámetro) — `GA_IN.APT:2544-2551`.
- `! Requisiciones activas / ! If NOT SalStrToNumber(ObtenerDeConfiguracion( 228 ))` en `EliminarMenues` — `GA_IN.APT:4113-4115`. **Hoy los menús de requisiciones NO se ocultan por el parámetro 228.**
- `! Function: ShowReporteConsumoPorDepartamento` — declaración comentada, `GA_IN.APT:825`.

**Los dos tipos de requisición** salen de `INV_REQUISICION_HDR.FLAG_REABASTECIMIENTO` y casan 1:1 con los dos menús de aprobación (`prGarantias.dll`, `qryReqConsulta`):

```sql
case T0.FLAG_REABASTECIMIENTO
     when 0 then 'Salida de bodega'
     when 1 then 'Reabastecimiento'
     else 'Salida de bodega'   -- NULL se trata como salida de bodega
END AS TIPO
```

### 3.2 Ingreso (`TfrmRequisicion`, prGarantias.dll)

Cabecera (`INV_REQUISICION_HDR`, componente `TMSTable` — **no hay SQL literal del INSERT**): `NUM_REQUISICION` (IDENTITY del servidor, `ReadOnly` en el DFM), `SOLICITANTE` (ReadOnly), `DEPARTAMENTO` (lookup a `REC_DEPARTAMENTO`), `USUARIO` (invisible), `OBSERVACION`, `FECHA`, `APROBADA`, `POR_COBRAR`, `COD_PROVEEDOR`, `ID_PROYECTO`, `FLAG_REABASTECIMIENTO`, `ROWID`.

Detalle: **no se edita contra la tabla real**, sino contra una temporal de sesión creada por el propio dataset:

```sql
CREATE TABLE #INV_REQUISICION_DTL (
  [COD_PRODUCTO] varchar(8), [CANTIDAD] float, [COD_CUENTA] varchar(15),
  [COD_CLIENTE] varchar(6), [COD_CENTROCOSTO] varchar(10),
  [COD_UNIDADPEDIDO] varchar(2), [DESCRIPCION] varchar(4000), [VALOR_PRODUCTO] float
)
```

Columnas visibles del grid: **Producto**, **Cantidad**, **Zona** (`COD_CLIENTE`), **Centro Costo** (`COD_CENTROCOSTO`); **Cuenta Contable oculta** (se resuelve por SQL al grabar).

Única sentencia de escritura del ingreso (`muInsertarRequisicion`), en una transacción con rollback:

```sql
DELETE FROM INV_REQUISICION_DTL WHERE NUM_REQUISICION = :NUM_REQUISICION
INSERT INTO INV_REQUISICION_DTL(COD_PRODUCTO, CANTIDAD, NUM_REQUISICION, COD_CUENTA,
       COD_CLIENTE, COD_CENTROCOSTO, COD_UNIDADPEDIDO, DESCRIPCION, VALOR_PRODUCTO)
SELECT COD_PRODUCTO, CANTIDAD, :NUM_REQUISICION,
 (SELECT ISNULL(CASE WHEN CUENTA_ASOCIADA = 'NA' THEN CUENTA_CONTABLE ELSE CUENTA_ASOCIADA END, CUENTA_CONTABLE)
    FROM INV_PRODUCTOS t0 WHERE t0.COD_PRODUCTO = TL.COD_PRODUCTO),
 COD_CLIENTE, COD_CENTROCOSTO, COD_UNIDADPEDIDO, DESCRIPCION, ROUND(VALOR_PRODUCTO,2)
FROM #INV_REQUISICION_DTL TL
UPDATE INV_REQUISICION_HDR SET POR_COBRAR = :POR_COBRAR, COD_PROVEEDOR = :COD_PROVEEDOR,
       ID_PROYECTO = :ID_PROYECTO, FLAG_REABASTECIMIENTO = :FLAG_REABASTECIMIENTO
 WHERE NUM_REQUISICION = :NUM_REQUISICION
-- + UPDATE PR_FICHA_DTL_MAT SET CANT_PRODUCTO_ENTREGADO = ... (acople con el módulo de proyectos)
```

Nótese: **borra y reinserta** todo el detalle, y **`CANTIDAD_APLICADA` no se escribe aquí** (el INSERT no la lista).

Validaciones legibles solo en el **gemelo .NET** (`prSiad/Winforms/FormsImportDelphi/frmRequisicion.cs`): sin renglones no graba (`:81`); **Observación obligatoria** ("Ingrese Observación"); por renglón exige producto, cantidad, **Zona** y **Centro Costo** (`validar_campos`, `:231-260`); descripción opcional. **NO CONFIRMADO** cuáles son las validaciones del formulario Delphi vigente: en el binario no hay literales del tipo "Debe/Ingrese/Seleccione" asociados al ingreso.

### 3.3 Aprobación

Dos pantallas WinForms sobre **la misma tabla y el mismo campo** (`INV_REQUISICION_HDR.APROBADA`):

| | Reabastecimiento (`AprobacionRequisiciones`) | Salida de bodega (`frmAprobarSalidasBodega`) |
|---|---|---|
| Fuente | `VW_APROBAR_REQUISICION` | `VW_APROBAR_REQUISICION_SALIDA` (+ columna `TIPO`) |
| Acción | Botón por fila, **valor fijo 1**, con confirmación | Combo `Estado` (`VW_ESTADOS_REQ`) + **Guardar en lote sobre TODAS las filas** |
| Grid | `ReadOnly = true` | `EditMode = EditOnEnter` |
| Usuario | lo recibe y **nunca lo lee** (`:13`, `:61-65`) | **ni siquiera lo recibe** (`Exports.cs:783`) |
| `catch` | mensaje equivocado ("...Orden de Trabajo") | **VACÍO** — una fila con `DBNull` corta el bucle en silencio |

El **único** UPDATE de estado de todo el ciclo:

```sql
update INV_REQUISICION_hdr set APROBADA = @status where NUM_REQUISICION = @requiza
```

**Sin cláusula de estado previo, sin `FECHA_APROBADA`, sin usuario aprobador, sin bitácora.** Una requisición aprobada, denegada o anulada puede volver a cualquier estado sin traza.

Estados (`APROBADA`, entero) — **dos fuentes discrepan**: `qryReqConsulta` de `prGarantias` lee `2` como *'Contabilizada'* y `SP_ConsultarRequisiciones` de `prSiad` lo lee como *'Denegada'*. Verifiqué por grep en `prGarantias.dll` que **ningún literal asigna `APROBADA = 2`** (solo aparecen `= 1` y `= 0`): quien lo escribe, si existe, está en SQL Server. **NO CONFIRMADO.**

- `0` = En Proceso · `1` = Aprobada · `2` = Contabilizada/Denegada (**NO CONFIRMADO**) · `3`/otro = Anulada.

### 3.4 Entrega ("aplicación") — la salida real

El despacho **no escribe la requisición**: crea un documento aparte (`prGarantias.dll`, `qryInsertAplInv`):

```sql
INSERT INV_APL_REQUISICION_HDR (USUARIO) VALUES (:COD_USUARIO)
SET @ID = @@IDENTITY
INSERT INV_APL_REQUISICION_DTL (NUM_APLICACION, NUM_REQUISICION, COD_PRODUCTO, CANTIDAD /*...*/)
SELECT @ID, NUM_REQUISICION, CANTIDAD_SELECIONAR, COD_PRODUCTO /*...*/
  FROM #INV_REQPROC WHERE SELECCIONADA = 1
DROP TABLE #INV_REQPROC
SELECT @ID AS RESULT
```

**La entrega es PARCIAL por diseño.** La propuesta de cantidad es el pendiente **topado por la existencia física** (`qrySelectDataTmp`):

```sql
CASE WHEN (T0.CANTIDAD - ISNULL(T0.CANTIDAD_APLICADA, 0)) > T2.CANTIDAD_STOCK
     THEN T2.CANTIDAD_STOCK
     ELSE T0.CANTIDAD - ISNULL(T0.CANTIDAD_APLICADA, 0) END
...
WHERE T0.CANTIDAD > T0.CANTIDAD_APLICADA AND T1.APROBADA = 1
```

Y el reporte "Requisiciones Pendientes de Entregar" (`ShowReqPendientes`, `GA_IN.APT:3350`) es literalmente `WHERE T1.CANTIDAD > T1.CANTIDAD_APLICADA AND T0.APROBADA = 1`, con `DATEDIFF(DAY, FECHA_APROBADA, GETDATE()) AS DIAS`.

> **Consecuencia directa (§3.4 ⇒ §6):** el control de la parcialidad es `CANTIDAD` vs `CANTIDAD_APLICADA` a nivel **renglón**, y el tope se comprueba contra la **existencia física total**, no contra un disponible neto.

**Dónde escribe esto el kardex: NO CONFIRMADO.** Rastreé tres fuentes: `GA_IN.APT` no tiene ningún `INSERT INTO INV_KARDEX`; `prGarantias.dll` **no contiene la cadena `INV_KARDEX`** ni un UPDATE de `CANTIDAD_APLICADA`; `prSiad.dll` solo toca `INV_KARDEX_TEMP`. El descargo por salida de bodega se hace **fuera del código disponible** — probablemente un trigger o SP de SQL Server. **No inventé el mecanismo.**

### 3.5 Consumo de la requisición APROBADA en la orden de compra (esto sí está en Centura)

Diálogo `dlgSeleccionarRequisiciones` (`GA_IN.APT:51661`), `On SAM_Create` (verificado leyendo `:51862-51872`):

```sql
SELECT NUM_REQUISICION, H.COD_PROVEEDOR, P.NOMBRE, H.OBSERVACION
  FROM INV_REQUISICION_HDR H
 INNER JOIN PRV_PROVEEDORES P ON H.COD_PROVEEDOR = P.COD_PROVEEDOR
 WHERE APROBADA = 1 AND FLAG_EN_OC != 1
```
> El identificador `dlgSeleccionarRequisiciones` **no aparece en ninguna otra línea** del `.APT`: es el selector viejo, sustituido por el form .NET `frmSeleccionarRequisicion`. Su SQL sigue siendo la mejor evidencia del criterio de elegibilidad. Ojo: `FLAG_EN_OC != 1` **descarta las filas con NULL** (lógica de tres valores de SQL Server).

Al grabar la O/C (`GA_IN.APT:11430-11442`) el consumo es **por bandera, nunca por cantidad**:

```sql
UPDATE INV_REQUISICION_DTL SET FLAG_APLICADO_EN_COMPRA = 1
 WHERE NUM_REQUISICION = :sNumReq AND FLAG_SELECCIONADO_PARA_COMPRA = 1
DELETE TMP_REQUISICION WHERE NUM_REQUISICION = :sNumReq
SELECT TOP 1 NUM_REQUISICION FROM INV_REQUISICION_DTL
 WHERE NUM_REQUISICION = :sNumReq AND FLAG_APLICADO_EN_COMPRA = 0 INTO :sReqAplicada
-- si no queda ninguna:
UPDATE INV_REQUISICION_HDR SET FLAG_EN_OC = 1 WHERE NUM_REQUISICION = :sNumReq
```

**Bug vigente de Centura, no replicar:** en la rama no exonerada (`GA_IN.APT:11516-11529`) la condición está **invertida** (`If sReqAplicada != sNULL`) y el UPDATE apunta a `:sReqAplicada` en vez de `:sNumReq` — cierra la requisición justo cuando *todavía quedan* renglones pendientes.

### 3.6 Impresión — **zona no leída**

- FastReport interno `frRequisicion` (dataset `qryRequisicion`), con campos `NUM_REQUISICION, FECHA, SOLICITANTE, DEPARTAMENTO, PRODUCTO, COD_PRODUCTO, CANTIDAD, OBSERVACION, NOMBRE, COD_CENTROCOSTO, COD_CLIENTE, VALOR_PRODUCTO, TOTAL_PRODUCTO, COD_UNIDAD_SALIDA, NOMBRE_PROVEEDOR, NOMBRE_PROYECTO`.
- `ImprimirRequisicion` (`GA_IN.APT:4069-4098`) **no imprime requisiciones**: es el lanzador genérico del reporte **51** de `AXL_REPORTES`, y su único llamador vigente es el menú "Comprobante de Entrega" (`GA_IN.APT:3117-3119`) con número y tipo de **FACTURA**.
- Del lado .NET: `rptReqProd.rpt`, `rptReqSer.rpt`, `rptResumenReq.rpt`, `rptDistReq.rpt`, `frmRpt_Resumen_Req`, `SP_RPT_LAPSOS_REQUIZA_PRODUCTOS/SERVICIOS`, `SP_REQUISICION_EXPORTAR`.

**El layout no está en ninguna fuente disponible** (vive en `AXL_REPORTES` fila 51 y en los `.rpt`). El portal no tiene ningún reporte equivalente. Ver §11 D-13.

---

## 4. Mapeo Centura → portal (campo a campo)

### 4.1 Documentos

| Centura | Portal propuesto | Nota |
|---|---|---|
| `INV_REQUISICION_HDR` | **`alm_requisicion_hdr`** (NUEVA) | cabecera de la solicitud, **no postea** |
| `INV_REQUISICION_DTL` | `alm_requisicion` (existente, plana) + columnas aditivas | línea de la solicitud |
| `INV_APL_REQUISICION_HDR` | **`alm_descargo_hdr`** (NUEVA) | cabecera de la entrega, **sí postea** |
| `INV_APL_REQUISICION_DTL` | `alm_descargo` (existente, plana) + columnas aditivas | **unidad de posteo**: 1 línea = 1 asiento |
| `INV_KARDEX` | `alm_kardex` | `documento_tipo='DESCARGO'`, `documento_id=alm_descargo.id` |
| `INV_EXISTENCIAS` | `alm_articulo_bodega` | |

### 4.2 Cabecera de requisición

| Dato | Centura | Columna propuesta | Acción |
|---|---|---|---|
| Número | `NUM_REQUISICION` (IDENTITY servidor) | `numero INTEGER` | **NUEVO** — correlativo por empresa, sembrado en 17124 (§6.5) |
| Fecha | `FECHA` | `fecha DATE` | **NUEVO** |
| Solicitante | `SOLICITANTE` | `solicitante VARCHAR(120)` | **NUEVO** (existe en la plana) |
| Usuario que captura | `USUARIO` | `usuario_solicita VARCHAR(100)` | **NUEVO** — distinto del solicitante |
| Departamento | `DEPARTAMENTO` → `REC_DEPARTAMENTO` | `departamento VARCHAR(3)` (texto) | **catálogo NO EXISTE** — ver §11 D-15 |
| Observación | `OBSERVACION` (obligatoria en el gemelo .NET) | `observacion VARCHAR(1000)` | **NUEVO** |
| Bodega | **no existe en la cabecera** (Centura lo pide al entregar) | `bodega_id INTEGER NOT NULL` | **NUEVO** — ver §11 D-6 |
| Estado | `APROBADA` (0/1/2/3) | `estado SMALLINT` numérico | **NUEVO** (§6.4) |
| Tipo | `FLAG_REABASTECIMIENTO` | `tipo SMALLINT` (1 salida / 2 reabastecimiento) | **NUEVO** — ver §11 D-12 |
| Aprobador | `FECHA_APROBADA` (**nadie la escribe**) | `aprobado_por` + `fecha_aprobacion TIMESTAMP` | **NUEVO** — corrige el defecto |
| Cerrada en O/C | `FLAG_EN_OC` | `estado = 6 CerradaEnOC` | ver §11 D-12 |
| Proveedor / proyecto / OT | `COD_PROVEEDOR`, `ID_PROYECTO`, `NUM_OT_TRABAJO`, `POR_COBRAR` | — | **fuera de alcance** — §11 D-12 |

### 4.3 Línea de requisición (`alm_requisicion`)

| Dato | Centura | Columna | Acción |
|---|---|---|---|
| Artículo | `COD_PRODUCTO` | `articulo_id` + `codigo_articulo` | existe |
| Descripción libre | `DESCRIPCION varchar(4000)` | `descripcion VARCHAR(200)` | existe (**capacidad menor**) |
| Cantidad solicitada | `CANTIDAD` | `cantidad NUMERIC(12,2)` | existe |
| **Cantidad entregada** | `CANTIDAD_APLICADA` | **`cantidad_despachada`** | **NUEVO** — el eje de la parcialidad |
| Precio | `VALOR_PRODUCTO` | `precio_unitario`, `total` | existe (referencial) |
| Cuenta contable | `COD_CUENTA` (resuelta por SQL, oculta) | `cuenta_contable` | existe |
| **Zona** | `COD_CLIENTE` (obligatorio) | — | **NO EXISTE** — §11 D-7 |
| **Centro de costo** | `COD_CENTROCOSTO` (obligatorio) | — | **NO EXISTE** (`cnt_centroscosto` = 0 filas) — §11 D-7 |
| Unidad de pedido | `COD_UNIDADPEDIDO` | — | **NO DEFINIDO** — §11 D-16 |
| Enlace a cabecera | — | **`requisicion_hdr_id`** | **NUEVO**, NULL en el histórico |

### 4.4 Línea de descargo (`alm_descargo`) → asiento

| Dato | Centura | Columna | Acción |
|---|---|---|---|
| Requisición servida | `INV_APL_REQUISICION_DTL.NUM_REQUISICION` | `numero_requisicion` (texto/numérico) + **`requisicion_id`** (FK a la línea) | `numero_requisicion` existe; FK **NUEVA** |
| Artículo | `COD_PRODUCTO` | `articulo_id`, `codigo_articulo` | existe |
| Cantidad entregada | `CANTIDAD` (= `CANTIDAD_SELECIONAR`) | `cantidad NUMERIC(12,2)` | existe |
| Costo | `INV_EXISTENCIAS.COSTO_ACTUAL` (promedio) | `precio_unitario`, `total` | existe — **lo estampa el motor al postear**, no la captura |
| Bodega | `COD_BODEGA` | `bodega_id` | existe |
| Enlace a cabecera | `NUM_APLICACION` | **`descargo_hdr_id`** | **NUEVO**, NULL en el histórico |
| Idempotencia | — | `uuid`, `posteado`, `fecha_posteo`, `origen` | existe |

### 4.5 Motor: `clsKardex_Inventario.Grabar` ≡ `InventarioPostingService.PostearAsync`

El motor del portal ya hace lo que hace el legacy, con dos diferencias deliberadas:

| Centura | Portal |
|---|---|
| El signo lo decide un **dato** (`INV_TIPOSTRANSACC.ENTRA_SALE`) | Lo decide el **tipo del enum** (código, no dato) |
| El recosteo lo decide `CAMBIA_COSTO` (dato) | La salida **nunca** mueve el promedio (`:389-394`), regla fija |
| Bloqueo de existencia negativa **configurable** (`CNF_CONFIGURACION` código `'24'`) | Guarda **dura** (`:296-300`) |
| `SALDO_ANTERIOR` = `SUM` sobre todo el historial | `existencia_resultante` = asignación sobre la fila bloqueada |

---

## 5. Reglas de negocio levantadas

### 5.1 CONFIRMADAS (código o datos verificados)

| # | Regla | Fuente |
|---|---|---|
| C-1 | La requisición **no mueve inventario**. La salida la produce la *aplicación/entrega*. | `prGarantias.dll` `qryInsertAplInv`; `GA_IN.APT` sin `INSERT INTO INV_KARDEX` |
| C-2 | La entrega es **parcial por diseño**: `CANTIDAD` vs `CANTIDAD_APLICADA`. | `qrySelectDataTmp`, `qryReqPendientes` |
| C-3 | La cantidad propuesta al entregar es `MIN(pendiente, existencia de la bodega)`. | `qrySelectDataTmp` |
| C-4 | Solo se entrega contra requisición **aprobada** (`APROBADA = 1`). | `qrySelectDataTmp` (`AND T1.APROBADA = 1`) |
| C-5 | La salida se valoriza al **costo promedio** de la bodega, congelado al momento de requisar (caso O/T). | `GA_IN.APT:28166-28179`, `:28478-28492`, `:28742` |
| C-6 | Una salida **no cambia** el costo promedio. | `InventarioPostingService.cs:389-394` |
| C-7 | El número de requisición es **IDENTITY del servidor**, no correlativo de aplicación. | DFM `ReadOnly`; `dtsInventarios.xsd:36-37` (`SCOPE_IDENTITY`) |
| C-8 | Los dos tipos (`FLAG_REABASTECIMIENTO`) casan 1:1 con los dos menús de aprobación. | `qryReqConsulta`; `GA_IN.APT:2735`, `:2746` |
| C-9 | **No hay jerarquía ni segregación de funciones en la aprobación**: mismo `arrSeguridad[33]`, el usuario ni se lee. | `GA_IN.APT:2740`, `:2751`; `AprobacionRequisiciones.cs:13,61-65`; `Exports.cs:783` |
| C-10 | El UPDATE de estado **no valida el estado previo** ni deja traza. | `INV_REQUISICION_HDRTableAdapter.Update_Cambiar_Status` |
| C-11 | `FECHA_APROBADA` existe y **ningún código de aplicación la escribe** (solo la leen 3 reportes). | grep sobre 650 archivos decompilados + los `.APT` |
| C-12 | Regla de visibilidad de la consulta: mías **OR** `dbo.esJefe(dueño, yo)=1` **OR** ya aprobadas. | `prGarantias.dll` `qryReqConsulta` |
| C-13 | Solo las requisiciones `APROBADA = 1 AND FLAG_EN_OC != 1` son elegibles para O/C. | `GA_IN.APT:51864-51871` (leído directamente) |
| C-14 | El consumo en la O/C es **por bandera**, no por cantidad; `CANTIDAD_APLICADA` nunca se toca desde Centura. | `GA_IN.APT:11430-11442` |
| C-15 | **Centura NO reserva stock.** El tope se comprueba contra `CANTIDAD_STOCK` al entregar; `INV_EXISTENCIAS.STOCK_REQUISADO` **no la escribe nadie** en ninguna fuente. | `qrySelectDataTmp`; `GA_AD.APT:3682`; grep = 0 |
| C-16 | **Aprobar NO recorta cantidades**: el único efecto es cambiar `APROBADA`; los grids de aprobación son de solo lectura (o solo editan el combo de estado). | `AprobacionRequisiciones.cs:168`; `frmAprobarSalidasBodega.cs:285-291` |
| C-17 | El histórico del portal es **el mismo hecho tres veces** (42.651 tríos comunes). | mirror (§1) |
| C-18 | El histórico está blindado: 100 % `origen='SIMAFI'` + `posteado=true`, `uuid` NULL. | mirror |
| C-19 | `alm_requisicion.bodega_id` está **NULL en el 100 %** de las 42.866 líneas. | mirror |
| C-20 | El motor **no toca** `existencia_comprometida` en ningún punto. | `InventarioPostingService.cs:90-97` |
| C-21 | La reversa del motor **siempre resta** y `TipoTransaccionDe(Reversa)` es siempre `"202"`. | `InventarioPostingService.cs:410-420`, `:435` |
| C-22 | El motor escribe `documento_id` pero **no** `numero_documento`; el histórico, al revés. | mirror: `COMPRA` 13/13/0 vs histórico 47.215 con `numero_documento` y 0 con `documento_id` |
| C-23 | `ck_alm_kardex_documento_tipo` ya admite `REQUISICION` y `DESCARGO`. | mirror |
| C-24 | No existe `uq_alm_requisicion_tenant` ni `uq_alm_descargo_tenant`. | mirror `pg_constraint` |
| C-25 | 241 de 638 pares tienen existencia > 0 y `costo_promedio = 0`; 3 tienen existencia negativa. | mirror |

### 5.2 NO CONFIRMADAS (no inventar; pedir o decidir)

| # | Punto abierto | Por qué no se puede confirmar |
|---|---|---|
| N-1 | **Qué significa `APROBADA = 2`**: 'Contabilizada' (`qryReqConsulta`) vs 'Denegada' (`SP_ConsultarRequisiciones`). Ningún literal lo escribe. | dos fuentes discrepan; grep en `prGarantias.dll` = solo `0` y `1` |
| N-2 | **Quién escribe `INV_KARDEX` al entregar** y con qué `TIPO_TRANSACCION`. | no está en `.APT`, ni en `prGarantias.dll`, ni en `prSiad.dll` |
| N-3 | **Quién incrementa `CANTIDAD_APLICADA`** y quién pone `INV_APL_REQUISICION_HDR.APLICADA = 1`. | ninguna fuente lo escribe |
| N-4 | Definición de `VW_APROBAR_REQUISICION`, `VW_APROBAR_REQUISICION_SALIDA`, `VW_ESTADOS_REQ`, `dbo.esJefe`, `SP_CentroCostoPorUsuario`, `SP_REQUISICION_DETALLE`, `dbo.ObtenerDeConfiguracion`. | viven en SQL Server (catálogo GRUPOJ), sin acceso |
| N-5 | DDL real de `INV_REQUISICION_HDR/DTL` y de `TMP_REQUISICION` (¿tiene usuario/sesión?). | los tipos reportados vienen de DataSets tipados y de la temporal, no del DDL |
| N-6 | Contenido de `INV_TIPOSTRANSACC` / `INV_TRANSACC_AXL` (`ENTRA_SALE`, `CAMBIA_COSTO`, `AREA_AFECTADA`). Define si el costeo es promedio móvil real. | es dato, no código |
| N-7 | Semántica de `alm_descargo.traslado` (`'T'` en 35.184 filas / `'0'` en 7.573). | ninguna fuente la documenta |
| N-8 | Rol contable de `alm_descargo.cuenta_contable_1/2` y sus `_detalle`. | el propio script de migración pide validarlo con contabilidad |
| N-9 | Qué formulario alimenta `:COD_BODEGA` en `qrySelectDataTmp` (y por qué `CASE :COD_BODEGA WHEN 0 THEN 1`). | no se leyó el form Delphi de la aplicación |
| N-10 | Si `clsKardex_Inventario` de `Casajaar_Final/NEWAPP/GA_AD.APT` es la misma que compiló el ejecutable de "APP ZIP". Los `.apl` incluidos (`GA_IN.APT:130-142`) **no están en el repo**. | fuente indirecta, otra app y otro dialecto SQL |
| N-11 | Cómo se puebla `arrSeguridad` y qué permiso real son los índices 32 y 33. | se declara en un `.apl` ausente |
| N-12 | Cuál de los dos ingresos está activo en producción: `TfrmRequisicion` (Delphi) o `frmRequisicion` (.NET). Ningún `.APT` llama a `MostrarRequisicion`. | pregunta al usuario/empresa |
| N-13 | Contenido del reporte 51 de `AXL_REPORTES` y de los `.rpt` de `prSiad`. | vive en la BD y en binarios de reporte |

---

## 6. Modelo de datos propuesto

### 6.1 Decisión estructural (y por qué **no** se crean tablas `_dtl` nuevas)

> **Objeción integrada.** El diseño DBA proponía `alm_requisicion_hdr/dtl` + `alm_descargo_hdr/dtl` **nuevas** y congelar las planas. Tres revisores lo refutaron y **acepto la corrección**, por dos motivos verificados:
>
> 1. **La premisa era falsa.** `alm_requisicion` y `alm_descargo` tienen **exactamente la misma infraestructura de posteo** que `alm_compra` (mismos 4 CHECK, `uq_*_company_uuid`, `ix_*_pendiente`, `trg_*_blindaje`) — verificado en `pg_constraint`/`pg_indexes`. Están tan listas para postear como lo estaba `alm_compra` cuando se tomó la decisión D-3 de compras.
> 2. **Riesgo de descuadre silencioso.** Con dos tablas capaces de reclamar `documento_tipo='DESCARGO'` y **espacios de `id` solapados** (la plana llega a 42.757; una `_dtl` nueva arranca en 1), el `uuid` `DESCARGO|company|id|par` **colisiona**. El corte de idempotencia devuelve `YaExistia = true` **sin insertar asiento** ([InventarioPostingService.cs:65-77](../../SIAD.Services/Almacen/InventarioPostingService.cs)) y el servicio marcaría la línea como posteada: **salida real sin asiento**, en un libro inmutable.
>
> **Resolución: se aplica el patrón D-3 de compras al pie de la letra** — cabecera nueva + tabla plana existente como línea y unidad de posteo, histórico intacto con `hdr_id` NULL.
>
> Los argumentos del diseño DBA que **sí** se conservan: ausencia total de columnas de ciclo de vida, `estatus VARCHAR(1)` prohibido por `CLAUDE.md` para columnas nuevas, y que el histórico ya está asentado en el kardex. Los que se **retiran** por no resistir los datos: "el histórico engorda toda consulta" (son 42.866 filas y ya existe índice parcial) y "los 18 pares duplicados impiden clave de renglón" (`alm_compra` tiene 33 pares equivalentes y aun así se reusó).

```
alm_requisicion_hdr  (NUEVA)  ── cabecera de la SOLICITUD · NO postea
        │ requisicion_hdr_id (aditiva, NULL en el histórico)
        ▼
alm_requisicion      (EXISTENTE, plana)  ── LÍNEA de la solicitud
        │  + cantidad_despachada  (la parcialidad vive aquí)
        │ requisicion_id (FK compuesta)
        ▼
alm_descargo         (EXISTENTE, plana)  ── LÍNEA de la ENTREGA = **UNIDAD DE POSTEO**
        │ descargo_hdr_id (aditiva, NULL en el histórico)
        ▼
alm_descargo_hdr     (NUEVA)  ── cabecera de la ENTREGA · SÍ postea
        │
        ▼
alm_kardex   documento_tipo = 'DESCARGO'   documento_id = alm_descargo.id
```

### 6.2 La requisición no postea: hay que **desarmar el arma cargada**

> **Objeción integrada (R-2).** Si la requisición nunca postea, `ck_alm_requisicion_uuid_si_siad` obliga un `uuid` que no identifica ningún asiento, y sobre todo **`ix_alm_requisicion_pendiente` (`WHERE origen='SIAD' AND posteado=false`) acumularía para siempre** cada línea nueva en el conjunto que el motor define como "pendiente de postear". Un barrido futuro las convertiría en salidas duplicadas.
>
> **Resolución:** en el mismo script se **elimina** ese índice y se agrega `ck_alm_requisicion_no_postea CHECK (origen='SIMAFI' OR posteado = false)`. El `uuid` se conserva como **identidad de la línea** (satisface el CHECK vigente y da idempotencia al alta), no como identidad de un asiento. Se corrige además el XML de `SIAD.Core/Entities/alm_requisicion.cs`, que hoy afirma lo contrario ("al postearse genera una SALIDA de bodega_id").

### 6.3 DDL completo — `Database/2026-08-01_alm_requisicion_descargo.sql`

```sql
-- =============================================================================
-- Requisiciones y descargos (salida de bodega) — cabeceras, correlativos y parcialidad
-- Fecha: 2026-08-01
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) antes que en el SRV.
--
-- POR QUÉ
--   La captura de requisiciones no existe en el portal ni en Centura: el menú de
--   GA_IN.APT solo llama ShowRequisiciones(), función externa de prGarantias.dll.
--
-- REGLA ESTRUCTURAL (medida en el mirror, ver README §1)
--   alm_requisicion (42.866 líneas), alm_descargo (42.757) y alm_kardex 202 (42.696)
--   son EL MISMO HECHO: 42.651 tríos comunes por (numero, codigo_articulo). Por eso:
--     · SOLO el DESCARGO postea al kardex. La requisición reserva/solicita, nunca asienta.
--     · El histórico NO se migra ni se re-postea (origen='SIMAFI', posteado=true).
--
-- PATRÓN: decisión D-3 de Database/2026-07-31_alm_compra_recepcion.sql — cabecera real
--   nueva + la tabla plana existente sigue siendo la UNIDAD DE POSTEO (un uuid por línea).
--   El motor no cambia de contrato y el histórico queda con hdr_id NULL.
--
-- ADITIVO salvo por: DROP de ix_alm_requisicion_pendiente (§6.2) — deliberado.
-- IDEMPOTENTE (IF NOT EXISTS / DO blocks). REVERSIBLE (bloque de rollback al final).
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 0) Prerrequisitos: fallar temprano y con mensaje accionable, no con error de FK
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'alm_requisicion' AND column_name = 'origen') THEN
        RAISE EXCEPTION 'Falta Database/2026-07-14_alm_documentos_bodega_posteo.sql (no existe alm_requisicion.origen). Aplíquelo primero.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_bodega_company_id') THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_bodega_company_id (FK compuestas).';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_articulo_company_id') THEN
        RAISE EXCEPTION 'Falta la clave alterna uq_alm_articulo_company_id (FK compuestas).';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_kardex_documento_tipo') THEN
        RAISE EXCEPTION 'Falta Database/2026-07-14_alm_kardex_trazabilidad.sql.';
    END IF;
    -- El histórico debe seguir intacto: si hay filas SIAD vivas, decidir antes de seguir.
    IF EXISTS (SELECT 1 FROM alm_requisicion WHERE origen <> 'SIMAFI')
       OR EXISTS (SELECT 1 FROM alm_descargo WHERE origen <> 'SIMAFI') THEN
        RAISE EXCEPTION 'Hay documentos con origen SIAD en las tablas planas. Revise antes de aplicar.';
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 1) Claves alternas que HOY NO EXISTEN (verificado en pg_constraint del mirror).
--    Sin ellas no se puede declarar NINGUNA FK compuesta contra las planas.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_requisicion_tenant') THEN
        ALTER TABLE public.alm_requisicion ADD CONSTRAINT uq_alm_requisicion_tenant UNIQUE (company_id, id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_descargo_tenant') THEN
        ALTER TABLE public.alm_descargo ADD CONSTRAINT uq_alm_descargo_tenant UNIQUE (company_id, id);
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 2) Cabecera de la REQUISICIÓN (solicitud). NO genera asientos.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_requisicion_hdr (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    numero              INTEGER       NOT NULL,   -- correlativo por empresa (continúa el histórico)
    tipo                SMALLINT      NOT NULL DEFAULT 1,   -- 1 salida de bodega · 2 reabastecimiento
    estado              SMALLINT      NOT NULL DEFAULT 1,
    fecha               DATE          NOT NULL,
    fecha_requerida     DATE          NULL,
    bodega_id           INTEGER       NOT NULL,   -- de dónde saldrá la mercadería
    departamento        VARCHAR(3)    NULL,       -- texto: no hay catálogo (ver README §11 D-15)
    solicitante         VARCHAR(120)  NOT NULL,
    cargo_solicitante   VARCHAR(80)   NULL,
    usuario_solicita    VARCHAR(100)  NOT NULL,   -- login; distinto del nombre mostrado
    aplicacion          VARCHAR(254)  NULL,       -- destino/uso (≡ alm_requisicion.aplicacion)
    observacion         VARCHAR(1000) NULL,
    aprobado_por        VARCHAR(100)  NULL,
    fecha_aprobacion    TIMESTAMP WITHOUT TIME ZONE NULL,
    rechazado_por       VARCHAR(100)  NULL,
    fecha_rechazo       TIMESTAMP WITHOUT TIME ZONE NULL,
    motivo_rechazo      VARCHAR(500)  NULL,
    anulado_por         VARCHAR(100)  NULL,
    fecha_anulacion     TIMESTAMP WITHOUT TIME ZONE NULL,
    motivo_anulacion    VARCHAR(500)  NULL,
    total               NUMERIC(14,2) NOT NULL DEFAULT 0,   -- referencial (precio de solicitud)
    uuid                UUID          NULL,       -- idempotencia del ALTA
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_requisicion_hdr_numero UNIQUE (company_id, numero),
    CONSTRAINT uq_alm_requisicion_hdr_tenant UNIQUE (company_id, id),

    -- Dominio del estado. La MÁQUINA DE ESTADOS vive en el servicio, no en un trigger
    -- (mismo criterio que alm_orden_compra, cuyo único control en BD es su CHECK).
    CONSTRAINT ck_alm_requisicion_hdr_estado CHECK (estado IN (1,2,3,4,5,6,8,9)),
    CONSTRAINT ck_alm_requisicion_hdr_tipo   CHECK (tipo IN (1,2)),
    -- Un reabastecimiento NO descuenta stock: no alcanza estados de despacho.
    CONSTRAINT ck_alm_requisicion_hdr_tipo_estado CHECK (tipo = 1 OR estado NOT IN (4,5)),
    -- CerradaEnOC solo existe en reabastecimiento.
    CONSTRAINT ck_alm_requisicion_hdr_cierre_oc   CHECK (tipo = 2 OR estado <> 6),

    -- EVIDENCIA. Corrige frontalmente el defecto de Centura: FECHA_APROBADA existía en la
    -- tabla y NINGÚN código de aplicación la escribía (solo la leían tres reportes).
    CONSTRAINT ck_alm_requisicion_hdr_aprobacion
        CHECK (estado NOT IN (3,4,5,6) OR (aprobado_por IS NOT NULL AND fecha_aprobacion IS NOT NULL)),
    CONSTRAINT ck_alm_requisicion_hdr_rechazo
        CHECK (estado <> 8 OR (rechazado_por IS NOT NULL AND fecha_rechazo IS NOT NULL
                               AND motivo_rechazo IS NOT NULL AND length(btrim(motivo_rechazo)) > 0)),
    CONSTRAINT ck_alm_requisicion_hdr_anulacion
        CHECK (estado <> 9 OR (anulado_por IS NOT NULL AND fecha_anulacion IS NOT NULL)),
    CONSTRAINT ck_alm_requisicion_hdr_solicitante CHECK (length(btrim(solicitante)) > 0)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_requisicion_hdr_bodega') THEN
        ALTER TABLE public.alm_requisicion_hdr
            ADD CONSTRAINT fk_alm_requisicion_hdr_bodega
                FOREIGN KEY (company_id, bodega_id)
                REFERENCES public.alm_bodega (company_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_requisicion_hdr_company_uuid
    ON public.alm_requisicion_hdr (company_id, uuid) WHERE uuid IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_aprobables
    ON public.alm_requisicion_hdr (company_id, fecha) WHERE estado = 2;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_despachables
    ON public.alm_requisicion_hdr (company_id, bodega_id) WHERE estado IN (3,4);
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_para_oc
    ON public.alm_requisicion_hdr (company_id) WHERE tipo = 2 AND estado = 3;
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_departamento
    ON public.alm_requisicion_hdr (company_id, departamento);

COMMENT ON TABLE public.alm_requisicion_hdr IS
 'Cabecera de la requisición interna de materiales (SOLICITUD). NUNCA genera asientos de kardex: la salida la produce el DESCARGO (alm_descargo). El histórico migrado de SIMAFI vive en la tabla plana alm_requisicion con requisicion_hdr_id NULL.';
COMMENT ON COLUMN public.alm_requisicion_hdr.numero IS
 'Correlativo por empresa (alm_requisicion_correlativo), SEMBRADO desde max(alm_requisicion.numero) del histórico (17124 en la empresa 2) para que la numeración nueva CONTINÚE la vieja y ningún número exista dos veces en el módulo.';
COMMENT ON COLUMN public.alm_requisicion_hdr.estado IS
 '1 Borrador · 2 En revisión · 3 Aprobada · 4 Despachada parcial · 5 Despachada · 6 Cerrada en O/C · 8 Rechazada · 9 Anulada. Traducción de INV_REQUISICION_HDR.APROBADA (0/1/2/3) + FLAG_EN_OC; el 0 se DESDOBLA en Borrador/En revisión porque en Centura la bandeja de aprobación no podía distinguirlos. Los estados 4 y 5 son DERIVADOS de cantidad_despachada, no se capturan.';
COMMENT ON COLUMN public.alm_requisicion_hdr.bodega_id IS
 'Bodega de la que saldrá la mercadería. Obligatoria: sin ella no hay par artículo/bodega que despachar. El histórico SIMAFI no la traía (bodega_id NULL en el 100% de sus 42.866 líneas).';

-- -----------------------------------------------------------------------------
-- 3) LÍNEA de la requisición: columnas aditivas sobre la tabla plana existente.
--    DEFAULT siempre en un ALTER COLUMN POSTERIOR, nunca en el ADD COLUMN, para
--    que el centinela NULL sobreviva al backfill.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_requisicion
    ADD COLUMN IF NOT EXISTS requisicion_hdr_id  INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS cantidad_despachada NUMERIC(12,2) NULL,
    ADD COLUMN IF NOT EXISTS aplicado_en_oc      BOOLEAN       NULL;

-- Backfill del histórico: lo descargado se da por entregado por completo.
UPDATE public.alm_requisicion SET cantidad_despachada = cantidad
 WHERE cantidad_despachada IS NULL AND descargado;
UPDATE public.alm_requisicion SET cantidad_despachada = 0 WHERE cantidad_despachada IS NULL;
UPDATE public.alm_requisicion SET aplicado_en_oc = false  WHERE aplicado_en_oc IS NULL;

ALTER TABLE public.alm_requisicion
    ALTER COLUMN cantidad_despachada SET DEFAULT 0,     ALTER COLUMN cantidad_despachada SET NOT NULL,
    ALTER COLUMN aplicado_en_oc      SET DEFAULT false, ALTER COLUMN aplicado_en_oc      SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_requisicion_hdr') THEN
        ALTER TABLE public.alm_requisicion
            ADD CONSTRAINT fk_alm_requisicion_hdr
                FOREIGN KEY (company_id, requisicion_hdr_id)
                REFERENCES public.alm_requisicion_hdr (company_id, id) ON DELETE RESTRICT;
    END IF;

    -- TOPE DE LA PARCIALIDAD: la garantía que Centura nunca tuvo en la base.
    -- Es la RED FINAL; el tope operativo lo valida el servicio bajo FOR UPDATE (§7.3).
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_requisicion_despachada') THEN
        ALTER TABLE public.alm_requisicion
            ADD CONSTRAINT ck_alm_requisicion_despachada
            CHECK (cantidad_despachada >= 0 AND cantidad_despachada <= cantidad);
    END IF;

    -- La requisición NO postea: se desarma el arma cargada (ver README §6.2).
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_requisicion_no_postea') THEN
        ALTER TABLE public.alm_requisicion
            ADD CONSTRAINT ck_alm_requisicion_no_postea
            CHECK (origen = 'SIMAFI' OR posteado = false);
    END IF;
END $$;

-- El índice de "pendiente de postear" define el conjunto que un barrido futuro
-- convertiría en salidas duplicadas. Como la requisición NUNCA postea, se elimina.
DROP INDEX IF EXISTS public.ix_alm_requisicion_pendiente;

CREATE INDEX IF NOT EXISTS ix_alm_requisicion_hdr_id
    ON public.alm_requisicion (company_id, requisicion_hdr_id);
-- Reporte "Requisiciones pendientes de entregar" (el qryReqPendientes de Centura era
-- literalmente CANTIDAD > CANTIDAD_APLICADA). Parcial: el filtro por requisicion_hdr_id
-- NOT NULL es lo que impide que las 42.866 líneas SIMAFI entren al cálculo.
CREATE INDEX IF NOT EXISTS ix_alm_requisicion_pendiente_entrega
    ON public.alm_requisicion (company_id, articulo_id, bodega_id)
    WHERE requisicion_hdr_id IS NOT NULL AND cantidad_despachada < cantidad;

COMMENT ON COLUMN public.alm_requisicion.cantidad_despachada IS
 'Cantidad ya entregada por descargos vigentes. Equivalente de INV_REQUISICION_DTL.CANTIDAD_APLICADA. La escribe el servicio de despacho bajo SELECT ... FOR UPDATE; ck_alm_requisicion_despachada es la red final.';
COMMENT ON COLUMN public.alm_requisicion.requisicion_hdr_id IS
 'Cabecera del documento nuevo. NULL en las 42.866 líneas del histórico SIMAFI, que no tienen cabecera y no se migran.';

-- -----------------------------------------------------------------------------
-- 4) Cabecera del DESCARGO (entrega). Este documento SÍ postea.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_descargo_hdr (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    numero              INTEGER       NOT NULL,
    fecha               DATE          NOT NULL,   -- fecha contable del asiento
    requisicion_hdr_id  INTEGER       NULL,       -- NULL = descargo directo (sin requisición)
    bodega_id           INTEGER       NOT NULL,
    departamento        VARCHAR(3)    NULL,
    entregado_por       VARCHAR(100)  NULL,       -- bodeguero
    recibido_por        VARCHAR(120)  NULL,       -- quien retira
    motivo              VARCHAR(120)  NULL,       -- obligatorio si NO hay requisición
    observaciones       VARCHAR(1000) NULL,
    total               NUMERIC(14,2) NOT NULL DEFAULT 0,  -- valorizado al promedio, lo estampa el posteo
    estado              SMALLINT      NOT NULL DEFAULT 1,  -- 1 Registrado · 9 Anulado
    motivo_anulacion    VARCHAR(500)  NULL,
    anulado_por         VARCHAR(100)  NULL,
    fecha_anulacion     TIMESTAMP WITHOUT TIME ZONE NULL,
    posteado            BOOLEAN       NOT NULL DEFAULT false,
    fecha_posteo        TIMESTAMP WITHOUT TIME ZONE NULL,
    uuid                UUID          NULL,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL,

    CONSTRAINT uq_alm_descargo_hdr_numero UNIQUE (company_id, numero),
    CONSTRAINT uq_alm_descargo_hdr_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_alm_descargo_hdr_estado CHECK (estado IN (1, 9)),
    -- Un descargo directo (sin requisición que lo justifique) exige motivo, igual que el
    -- ajuste de inventario.
    CONSTRAINT ck_alm_descargo_hdr_motivo
        CHECK (requisicion_hdr_id IS NOT NULL
               OR (motivo IS NOT NULL AND length(btrim(motivo)) > 0)),
    CONSTRAINT ck_alm_descargo_hdr_posteo
        CHECK (posteado = false OR (uuid IS NOT NULL AND fecha_posteo IS NOT NULL)),
    CONSTRAINT ck_alm_descargo_hdr_anulacion
        CHECK (estado <> 9 OR (anulado_por IS NOT NULL AND fecha_anulacion IS NOT NULL))
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_hdr_bodega') THEN
        ALTER TABLE public.alm_descargo_hdr
            ADD CONSTRAINT fk_alm_descargo_hdr_bodega
                FOREIGN KEY (company_id, bodega_id)
                REFERENCES public.alm_bodega (company_id, id) ON DELETE RESTRICT;
    END IF;
    -- Con requisicion_hdr_id NULL (descargo directo) MATCH SIMPLE no valida la referencia.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_hdr_requisicion') THEN
        ALTER TABLE public.alm_descargo_hdr
            ADD CONSTRAINT fk_alm_descargo_hdr_requisicion
                FOREIGN KEY (company_id, requisicion_hdr_id)
                REFERENCES public.alm_requisicion_hdr (company_id, id) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_alm_descargo_hdr_company_uuid
    ON public.alm_descargo_hdr (company_id, uuid) WHERE uuid IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_fecha
    ON public.alm_descargo_hdr (company_id, fecha DESC, id DESC);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_requisicion
    ON public.alm_descargo_hdr (company_id, requisicion_hdr_id);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_pendiente
    ON public.alm_descargo_hdr (company_id) WHERE posteado = false AND estado = 1;

COMMENT ON TABLE public.alm_descargo_hdr IS
 'Cabecera del descargo (ENTREGA física de materiales). ÚNICO documento del par requisición/descargo que genera asientos en alm_kardex (documento_tipo = DESCARGO). Una requisición admite N descargos (entrega parcial, como INV_APL_REQUISICION_* de Centura); un descargo sin requisicion_hdr_id es una salida directa justificada por motivo.';
COMMENT ON COLUMN public.alm_descargo_hdr.estado IS
 '1 Registrado · 9 Anulado. Un descargo no se edita: si estuvo mal se anula, se devuelve la cantidad a la requisición y el kardex se corrige con un asiento REVERSA (nunca con UPDATE: trg_alm_kardex_inmutable, SQLSTATE K0001).';

-- -----------------------------------------------------------------------------
-- 5) LÍNEA del descargo = UNIDAD DE POSTEO. Columnas aditivas sobre la plana.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_descargo
    ADD COLUMN IF NOT EXISTS descargo_hdr_id INTEGER NULL,
    ADD COLUMN IF NOT EXISTS requisicion_id  INTEGER NULL;   -- LÍNEA de requisición servida

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_hdr') THEN
        ALTER TABLE public.alm_descargo
            ADD CONSTRAINT fk_alm_descargo_hdr
                FOREIGN KEY (company_id, descargo_hdr_id)
                REFERENCES public.alm_descargo_hdr (company_id, id) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_alm_descargo_requisicion_linea') THEN
        ALTER TABLE public.alm_descargo
            ADD CONSTRAINT fk_alm_descargo_requisicion_linea
                FOREIGN KEY (company_id, requisicion_id)
                REFERENCES public.alm_requisicion (company_id, id) ON DELETE RESTRICT;
    END IF;
    -- Dos renglones del MISMO descargo no pueden servir la misma línea de requisición:
    -- por separado pasan el tope y juntos lo rompen. En compras esta regla vive solo en C#
    -- (RecepcionCompraService.cs:571-581); aquí la garantiza la base.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'uq_alm_descargo_renglon') THEN
        ALTER TABLE public.alm_descargo
            ADD CONSTRAINT uq_alm_descargo_renglon
            UNIQUE (company_id, descargo_hdr_id, requisicion_id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_alm_descargo_hdr_id
    ON public.alm_descargo (company_id, descargo_hdr_id);
CREATE INDEX IF NOT EXISTS ix_alm_descargo_requisicion_linea
    ON public.alm_descargo (company_id, requisicion_id);

COMMENT ON COLUMN public.alm_descargo.descargo_hdr_id IS
 'Cabecera del documento nuevo. NULL en las 42.757 líneas del histórico SIMAFI.';
COMMENT ON COLUMN public.alm_descargo.requisicion_id IS
 'LÍNEA de alm_requisicion que sirve este renglón (FK compuesta tenant-safe). El histórico enlaza por numero_requisicion (NUMERIC, sin FK) y queda NULL aquí.';

-- -----------------------------------------------------------------------------
-- 6) Correlativos por empresa, SEMBRADOS desde el máximo histórico
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_requisicion_correlativo (
    company_id    BIGINT  PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS public.alm_descargo_correlativo (
    company_id    BIGINT  PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);

-- SEMBRADO AUTO-CORREGIBLE: DO UPDATE ... GREATEST, nunca DO NOTHING. Así el script es
-- re-ejecutable y CORRIGE un contador que el servicio ya hubiera creado en 0 (el patrón
-- de RecepcionCompraService inserta VALUES(company,0) ON CONFLICT DO NOTHING, que sobre
-- una tabla CON HISTORIA anularía la siembra en silencio).
INSERT INTO public.alm_requisicion_correlativo (company_id, ultimo_numero)
SELECT company_id, COALESCE(max(numero), 0)::int FROM public.alm_requisicion GROUP BY company_id
ON CONFLICT (company_id) DO UPDATE
   SET ultimo_numero = GREATEST(alm_requisicion_correlativo.ultimo_numero, EXCLUDED.ultimo_numero);

INSERT INTO public.alm_descargo_correlativo (company_id, ultimo_numero)
SELECT company_id, COALESCE(max(numero_documento), 0)::int FROM public.alm_descargo GROUP BY company_id
ON CONFLICT (company_id) DO UPDATE
   SET ultimo_numero = GREATEST(alm_descargo_correlativo.ultimo_numero, EXCLUDED.ultimo_numero);

COMMENT ON TABLE public.alm_requisicion_correlativo IS
 'Último número de requisición emitido por empresa. Sembrado desde max(alm_requisicion.numero) del histórico SIMAFI (17124 en la empresa 2): la primera requisición nueva es la 17125, para que el número siga identificando UN solo documento en todo el módulo.';

-- -----------------------------------------------------------------------------
-- 7) Guarda de no-negativos de la reserva (barata y validada: las 638 filas están en 0)
--    NO se agrega CHECK sobre `existencia`: hay 3 filas negativas legítimas del histórico.
--    OJO: este CHECK NO garantiza comprometida <= existencia (imposible con negativas).
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_alm_articulo_bodega_reserva') THEN
        ALTER TABLE public.alm_articulo_bodega
            ADD CONSTRAINT ck_alm_articulo_bodega_reserva
            CHECK (existencia_comprometida >= 0 AND existencia_transito >= 0);
    END IF;
END $$;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- V1) Las 4 tablas nuevas existen:
--   SELECT table_name FROM information_schema.tables
--    WHERE table_name IN ('alm_requisicion_hdr','alm_descargo_hdr',
--                         'alm_requisicion_correlativo','alm_descargo_correlativo');  -- 4
--
-- V2) TODAS las FK nuevas son COMPUESTAS (company_id, ...):
--   SELECT conrelid::regclass, conname, pg_get_constraintdef(oid) FROM pg_constraint
--    WHERE contype='f' AND conname LIKE 'fk_alm_%requisicion%' OR conname LIKE 'fk_alm_descargo%';
--
-- V3) Correlativos sembrados (empresa 2 → 17124 en ambos):
--   SELECT 'req', company_id, ultimo_numero FROM alm_requisicion_correlativo
--   UNION ALL SELECT 'des', company_id, ultimo_numero FROM alm_descargo_correlativo;
--
-- V4) El histórico NO cambió (deben salir 42.866 / 42.757, 100% SIMAFI/posteado):
--   SELECT 'req' t, origen, posteado, count(*) FROM alm_requisicion GROUP BY 2,3
--   UNION ALL SELECT 'des', origen, posteado, count(*) FROM alm_descargo GROUP BY 2,3;
--
-- V5) El backfill satisface el CHECK (0 filas):
--   SELECT count(*) FROM alm_requisicion WHERE cantidad_despachada > cantidad;
--
-- V6) El arma cargada quedó desarmada (0 filas y índice ausente):
--   SELECT count(*) FROM alm_requisicion WHERE origen='SIAD' AND posteado=false;
--   SELECT indexname FROM pg_indexes WHERE indexname='ix_alm_requisicion_pendiente';  -- vacío
--
-- V7) PRUEBA NEGATIVA — despachar de más debe fallar (ck_alm_requisicion_despachada):
--   UPDATE alm_requisicion SET cantidad_despachada = cantidad + 1 WHERE id = :linea;
--
-- V8) PRUEBA NEGATIVA — postear una requisición SIAD debe fallar (ck_alm_requisicion_no_postea).
-- =============================================================================

-- =============================================================================
-- ROLLBACK (en este orden)
-- =============================================================================
-- BEGIN;
-- ALTER TABLE alm_articulo_bodega DROP CONSTRAINT IF EXISTS ck_alm_articulo_bodega_reserva;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS uq_alm_descargo_renglon;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS fk_alm_descargo_requisicion_linea;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS fk_alm_descargo_hdr;
-- ALTER TABLE alm_descargo    DROP COLUMN IF EXISTS requisicion_id, DROP COLUMN IF EXISTS descargo_hdr_id;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_no_postea;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS ck_alm_requisicion_despachada;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS fk_alm_requisicion_hdr;
-- ALTER TABLE alm_requisicion DROP COLUMN IF EXISTS aplicado_en_oc,
--     DROP COLUMN IF EXISTS cantidad_despachada, DROP COLUMN IF EXISTS requisicion_hdr_id;
-- CREATE INDEX IF NOT EXISTS ix_alm_requisicion_pendiente
--     ON alm_requisicion(company_id) WHERE origen='SIAD' AND posteado=false;
-- DROP TABLE IF EXISTS alm_descargo_hdr;
-- DROP TABLE IF EXISTS alm_requisicion_hdr;
-- DROP TABLE IF EXISTS alm_descargo_correlativo;
-- DROP TABLE IF EXISTS alm_requisicion_correlativo;
-- ALTER TABLE alm_descargo    DROP CONSTRAINT IF EXISTS uq_alm_descargo_tenant;
-- ALTER TABLE alm_requisicion DROP CONSTRAINT IF EXISTS uq_alm_requisicion_tenant;
-- COMMIT;
-- =============================================================================
```

### 6.4 Máquina de estados

`SIAD.Core/Constants/EstadosNumericos.cs`:

```csharp
// Requisición interna (alm_requisicion_hdr.estado). CHECK: estado IN (1,2,3,4,5,6,8,9).
// Traducción de INV_REQUISICION_HDR.APROBADA (0/1/2/3) + FLAG_EN_OC.
public static class EstadoRequisicionHdr
{
    public const short Borrador          = 1;   // APROBADA = 0, aún del solicitante
    public const short EnRevision        = 2;   // APROBADA = 0, ya en la bandeja del aprobador
    public const short Aprobada          = 3;   // APROBADA = 1
    public const short DespachadaParcial = 4;   // DERIVADO: 0 < Σ despachada < Σ cantidad
    public const short Despachada        = 5;   // DERIVADO: Σ despachada = Σ cantidad
    public const short CerradaEnOC       = 6;   // FLAG_EN_OC = 1 (solo tipo 2)
    public const short Rechazada         = 8;   // APROBADA = 2 (lectura de SP_ConsultarRequisiciones)
    public const short Anulada           = 9;   // APROBADA = 3
}

public static class TipoRequisicion
{
    public const short SalidaBodega     = 1;   // FLAG_REABASTECIMIENTO 0/NULL — consume stock
    public const short Reabastecimiento = 2;   // FLAG_REABASTECIMIENTO 1 — va a orden de compra
}

public static class EstadoDescargo { public const short Registrado = 1; public const short Anulado = 9; }
```

> **El `0` de Centura se DESDOBLA** en Borrador / En revisión: en el legacy, `VW_APROBAR_REQUISICION` no puede distinguir "el solicitante todavía la está escribiendo" de "ya la mandó a aprobar". Es la única corrección estructural del modelo de estados.
>
> **El `2` se lee como Rechazada** — no como 'Contabilizada' — porque las dos fuentes del legacy discrepan (N-1) y el portal **no tiene contabilización de requisiciones**. Si D-1 (§11) resulta en que sí debe haberla, el valor 7 queda libre para *Contabilizada*.

**Transiciones y quién las hace:**

| # | Transición | Origen → destino | Permiso | Efectos |
|---|---|---|---|---|
| T1 | Crear | — → 1 | `requisiciones` · Create | correlativo; líneas con `cantidad_despachada = 0` |
| T2 | Editar | 1 → 1 | Edit | reemplazo total de líneas |
| T3 | Enviar | 1 → 2 | Edit | — |
| T4 | Devolver a borrador | 2 → 1 | `requisiciones_aprobacion` · Edit | motivo |
| T5 | **Aprobar** | 2 → 3 | `requisiciones_aprobacion` · Edit | estampa `aprobado_por`/`fecha_aprobacion`; **no** aparta stock (ver D-2) |
| T6 | Rechazar | 2 → 8 | `requisiciones_aprobacion` · Edit | motivo obligatorio |
| T7 | **Despachar** | 3\|4 → 4\|5 | `descargos` · Create | crea el descargo, postea salidas, sube `cantidad_despachada` |
| T8 | Anular descargo | — | `descargos` · Edit | reversa espejo por línea; **recalcula** el estado |
| T9 | Anular requisición | 1\|2\|3 → 9 | Edit | prohibida si alguna línea tiene `cantidad_despachada > 0` |
| T10 | Eliminar | 1 → — | Delete | solo Borrador sin descargos |
| T11 | Llevar a O/C | 3 → 3\|6 | módulo `Compras` · Create | solo `tipo = 2` — ver D-12 |

> **Objeción integrada (R-6, refutada 3×).** La propuesta original ponía este grafo en un **trigger plpgsql**. Se descarta y **la máquina de estados vive en el servicio**, por cuatro razones verificadas:
> - El grafo propuesto **se contradecía**: prohibía `3→9` pero `3→4→2→9` era legal en tres saltos.
> - **Faltaba `3→2`**, que el módulo gemelo sí hace hoy: `RecepcionCompraService.cs:462-466` reabre `Cerrada → RecibidaParcial → Aprobada` al anular. Sin ella, anular el único descargo parcial revienta.
> - Faltaban los **auto-loops** (`1→1` para editar el borrador, `3→3`/`4→4` para el segundo despacho parcial y para el UPDATE de `posteado`).
> - **No hay precedente**: los únicos 4 triggers de almacén en el mirror son de **prohibición pura** (`trg_alm_kardex_inmutable` K0001 y los tres `trg_alm_*_blindaje` K0002); ninguno escribe. `alm_orden_compra`, con los mismos 5 estados, solo tiene su CHECK de dominio.
>
> **Lo que sí se conserva en BD** es lo que un trigger/CHECK *puede* garantizar sin conocer la causa: dominio del estado y **evidencia write-once** de aprobación/rechazo/anulación (los tres `ck_*` de §6.3). Los estados 4 y 5 son **derivados** de `cantidad_despachada`, no capturados — así `3→2` sale gratis y desaparece la excepción "4→2 solo por anulación", que de todos modos un trigger no podía verificar.

### 6.5 Numeración

Correlativo por empresa con el patrón ya probado (`RecepcionCompraService.cs:791-812`): `INSERT ... ON CONFLICT DO NOTHING` + `SELECT ... FOR UPDATE` con el `company_id` **dentro** del SQL crudo, y `ultimo_numero += 1` en memoria.

> **Objeción integrada (R-4).** El patrón copiado de compras inserta `VALUES (company, 0) ON CONFLICT DO NOTHING`. Eso es seguro allá porque `alm_compra_hdr` nació vacía; **aquí hay historia**. Si el servicio corre antes que el script, el contador queda en 0 y la primera requisición sale con el número **1**, que ya existe (verificado: los números 1..10 están todos ocupados, y hay una línea con `numero = 0`). Correcciones adoptadas:
> - El script siembra con `ON CONFLICT DO UPDATE ... GREATEST` (§6.3), no `DO NOTHING`.
> - El alta idempotente del servicio inserta `COALESCE(max(numero), 0)`, no 0.
> - El número queda respaldado por `uq_alm_requisicion_hdr_numero UNIQUE (company_id, numero)` — invariante real, no convención.
> - Precedente que **no** se copia: en compras ya conviven una recepción `#1` nueva (`alm_compra_hdr.numero` 1..7) y una compra `#1` histórica (`alm_compra.numero` 1..1526).

**NO CONFIRMADO:** el espacio de numeración no es continuo (entre 1 y 17124 faltan 80 números y existe el `0`). No afecta al correlativo, pero conviene saberlo antes de afirmar continuidad en un reporte.

### 6.6 Lo que se decide **no** hacer

| Propuesta | Veredicto | Motivo |
|---|---|---|
| `UNIQUE (company_id, requisicion_id, articulo_id)` | **Descartada** | Refutada 3× con datos. De los 18 pares duplicados del histórico, **5 no son basura** (cantidades, precios o cuentas distintas: req. 1533/art 0005 = 50 vs 10; req. 4611/art 5068 = dos cuentas contables y dos precios). Y **36 %** de las requisiciones multilínea mezclan más de una `cuenta_contable`. Pedir el mismo artículo dos veces para dos obras es operación normal. La identidad del renglón es su `id` (como el `ROWID` de Centura), y el pendiente se calcula por **agregación**, que es determinista con N líneas. Si se quiere higiene, va como aviso en la UI, no como constraint. |
| Trigger que **escribe** `cantidad_despachada` | **Descartada** | Refutada 3×. `set_config(...,true)` es transaccional y cualquier sentencia del mismo rol lo enciende: no es infalsificable. Un `FOR UPDATE` fila-a-fila dentro de un trigger `AFTER` toma candados en el orden en que EF emite los INSERT → deadlock con la anulación, que toma los recursos en orden inverso. Y el `AFTER DELETE` legitimaría borrar una línea ya posteada. **El servicio escribe; el CHECK es la red final** (§7.3). |
| Congelar las planas con `CHECK (origen='SIMAFI')` | **Descartada** | Con el patrón D-3 las planas **son** las líneas de los documentos nuevos: congelarlas cierra el camino. La protección equivalente y compatible es `ck_alm_requisicion_no_postea` (§6.2), que impide que una requisición SIAD entre al kardex sin impedir que exista. |

---

## 7. Diseño de implementación

### 7.1 Motor — corregir el defecto latente ANTES de la primera salida

**El defecto (confirmado leyendo el código):** `Calcular(Reversa)` hace siempre `existencia = fila.existencia - m.Cantidad` y devuelve `salidas = m.Cantidad` ([`:410-420`](../../SIAD.Services/Almacen/InventarioPostingService.cs)); `TipoTransaccionDe(Reversa)` es siempre `"202"` (`:435`); y `ValidarAsync(Reversa)` (`:311-324`) solo comprueba que el asiento exista y que coincidan artículo/bodega — **sin guarda de no-negativo**. Es correcto mientras lo único reversible sean **entradas** (hoy: carga inicial y compra; en el mirror hay 2 REVERSA, ambas `202` contra COMPRA). El día que exista un descargo, **anularlo restaría otra vez**: doble descarga y existencia negativa en un libro que no admite UPDATE.

> **Objeción integrada (motor R-2, refutada 3×).** El discriminador propuesto (`original.salidas > 0` vs `original.ingresos > 0`) **no funciona**: `CargaInicialReconciliacion` escribe `ingresos = Cantidad` pero **no mueve la existencia** (`:370-373`), y su asiento es numéricamente idéntico al de una apertura nueva; `AjusteValor` tiene ambos en 0 y no cae en ninguna rama. Además, "restar siempre" es carga estructural de `ReabrirAsync`, que revierte para poder re-postear una `CargaInicialNueva` (que exige existencia previa 0).
>
> **Resolución adoptada — discriminar por `documento_tipo`, no por ingresos/salidas.** Es inequívoco, no toca carga inicial y no puede regresionar nada:

```csharp
// ValidarAsync ahora DEVUELVE el asiento original (ya lo carga en :316-318 y hoy lo descarta)
var original = await ValidarAsync(movimiento, fila, ct);
var (existencia, promedio, ingresos, salidas, costo) = Calcular(movimiento, fila, original);
```

```csharp
case TipoMovimientoInventario.Reversa:
{
    // Espejo SOLO para lo que es inequívocamente una salida por documento. No se toca
    // carga inicial (donde ingresos>0 NO implica delta>0: la reconciliación describe lo
    // que ya hay) ni ajuste de valor (delta 0). Discriminar por ingresos/salidas habría
    // vaciado la bodega al revertir una apertura por reconciliación.
    if (original!.documento_tipo == TipoDocumentoInventario.Descargo)
    {
        // El original SALIÓ → la reversa ENTRA, al costo con el que salió, re-ponderando.
        var e = fila.existencia + original.cantidad;
        var promedioE = e == 0m
            ? original.valor_unitario!.Value
            : ((fila.existencia * fila.costo_promedio) + (original.cantidad * original.valor_unitario!.Value)) / e;
        return (e, promedioE, original.cantidad, 0m, original.valor_unitario!.Value);
    }

    // Comportamiento actual, intacto (des-ponderado medido en el mirror el 2026-07-31).
    var existencia = fila.existencia - m.Cantidad;
    var valorRestante = (fila.existencia * fila.costo_promedio) - (m.Cantidad * m.CostoUnitario);
    var prom = existencia > 0m && valorRestante > 0m ? valorRestante / existencia : fila.costo_promedio;
    return (existencia, prom, 0m, m.Cantidad, m.CostoUnitario);
}
```

Guardas que se agregan de paso (hoy faltan en la rama Reversa):
- `TipoTransaccionDe(Reversa, original)` → `"102"` cuando el original es DESCARGO, `"202"` en el resto.
- **Cantidad y costo salen del asiento ORIGINAL**, no del DTO: hoy un llamador puede mandar otra cifra y des-postear algo distinto a lo posteado, sin que nada lo detecte (el `uuid` `REVERSA|company|kardexId` congela lo primero que llegue).
- Rechazar revertir un asiento cuyo `documento_tipo` ya es `REVERSA`.
- Guarda de no-negativo en la rama que resta (hoy solo vive fuera del motor, en `RecepcionCompraService.cs:380-390`).

**Regresión obligatoria:** `InventarioPostingTests`, `CargaInicialTests`, `RecepcionCompraTests`, `KardexPuntoCorteTests`.

### 7.2 Motor — el tipo nuevo

> **Objeción integrada (motor R-1).** La propuesta llamaba `SalidaRequisicion` y anclaba el `uuid` a la **línea de requisición**. Eso rompe la entrega parcial: en el mirror hay **28 pares** `(numero_requisicion, codigo_articulo)` con **más de un descargo** (p. ej. requisición 8481 con 10 artículos entregados en 2 eventos cada uno). Con el `uuid` en la requisición, el segundo despacho colisiona, el motor devuelve `YaExistia = true` **sin postear** y la mercadería sale sin asiento. **Se ancla al DESCARGO.**

```csharp
/// <summary>
/// Salida de bodega por entrega de materiales (descargo). Sale al costo PROMEDIO vigente del
/// par — el CostoUnitario del DTO se IGNORA — y NO altera el promedio. El documento es la
/// LÍNEA DE DESCARGO (alm_descargo.id), nunca la línea de requisición: una misma línea de
/// requisición se entrega en varios descargos (28 casos medidos en el histórico).
/// </summary>
SalidaDescargo = 8
```

Cambios puntuales, todos localizados:

| Punto | Archivo:línea | Cambio |
|---|---|---|
| Whitelist | `:37-43` | agregar `or TipoMovimientoInventario.SalidaDescargo` |
| `documento_tipo` | `:117-122` | `SalidaDescargo => TipoDocumentoInventario.Descargo` **fijo en el switch**, nunca del DTO |
| `ValidarAsync` | `:294-301` | rama nueva: cantidad > 0, `DocumentoId > 0`, **`fila.costo_promedio > 0`** (§10.2), guarda de no-negativo idéntica a `AjusteNegativo` |
| `Calcular` | `:389-394` | `case SalidaDescargo:` **compartido con `AjusteNegativo`** — la fórmula ya es exactamente la que se necesita, no se inventa nada |
| `TipoTransaccionDe` | `:428-439` | `=> TipoTransaccionKardex.Salida` (`"202"`), no `"103"` |
| `EsAjuste` | `:441-444` | **no se toca**: cae fuera → `es_ajuste = false` |
| `DescripcionDe` | `:446-456` | `"Salida por requisición"` |
| `DerivarUuid` | `:463-482` | **rama propia**, nunca el `default` |

```csharp
// Tipo de documento FIJO, igual que Compra. El comentario de :472-478 lo explica: si se
// tomara m.DocumentoTipo, un llamador que mandara otra cadena generaría un uuid distinto
// para la misma línea y la volvería a postear.
TipoMovimientoInventario.SalidaDescargo =>
    UuidV5.CreateInventario(
        $"{TipoDocumentoInventario.Descargo}|{companyId}|{m.DocumentoId}|{m.ArticuloBodegaId}"),
```

**No hay DDL:** `ck_alm_kardex_documento_tipo` ya admite `DESCARGO` (verificado). `TipoDocumentoInventario.Requisicion` queda **declarado y sin productor**: ningún camino de código lo escribe. Se documenta así en la constante.

> **Por qué NO se agrega `DevolucionRequisicion`:** no tiene respaldo legacy. Lo único parecido es `'DEM'` para materiales de O/T (`GA_IN.APT:28750` → `ModificarInvPorDevolucionOT`, `:1018`), y **no es un contra-asiento**: es un `UPDATE` de la fila de kardex existente, con un `WHERE` que ni siquiera filtra por correlativo (`:1110-1139`) — imposible de replicar. Devolver material sobrante es una **regla nueva**: ver §11 D-8. Anular ≠ devolver; la anulación la cubre la reversa de §7.1.

### 7.3 Servicios

```csharp
// SIAD.Services/Almacen/IRequisicionService.cs
public interface IRequisicionService
{
    Task<IReadOnlyList<RequisicionHdrListItemDto>> GetAsync(RequisicionHdrFilterDto? f, CancellationToken ct = default);
    Task<RequisicionDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<RequisicionDto> CrearAsync(RequisicionDto dto, string user, CancellationToken ct = default);
    Task<RequisicionDto> ActualizarAsync(int id, RequisicionDto dto, string user, CancellationToken ct = default);

    Task<bool> EnviarAsync(int id, string user, CancellationToken ct = default);
    Task<bool> AprobarAsync(int id, string? nota, string user, CancellationToken ct = default);
    Task<bool> RechazarAsync(int id, string motivo, string user, CancellationToken ct = default);
    Task<bool> DevolverABorradorAsync(int id, string motivo, string user, CancellationToken ct = default);
    Task<bool> AnularAsync(int id, string? motivo, string user, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, string user, CancellationToken ct = default);

    /// <summary>Renglones con pendiente &gt; 0 (cantidad − despachada), con la existencia
    /// actual del par. Es lo que llena la grilla del despacho (≡ qrySelectDataTmp).</summary>
    Task<IReadOnlyList<RequisicionPendienteDto>> ObtenerPendientesAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<RequisicionHdrListItemDto>> ObtenerAprobablesAsync(string? depto, CancellationToken ct = default);
    Task<IReadOnlyList<RequisicionHdrListItemDto>> ObtenerDespachablesAsync(int? bodegaId, CancellationToken ct = default);
}

// SIAD.Services/Almacen/IDescargoService.cs   (el servicio de consulta actual pasa a
// DescargoHistoricoService; este es el flujo nuevo)
public interface IDescargoService
{
    Task<IReadOnlyList<DescargoListItemDto>> GetAsync(DescargoFilterDto? f, CancellationToken ct = default);
    Task<DescargoDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Entrega: descarga la requisición y postea una SALIDA por renglón.
    /// Idempotente por <c>DescargoDto.Uuid</c>.</summary>
    Task<DescargoDto> EntregarAsync(DescargoDto dto, string user, CancellationToken ct = default);

    /// <summary>Anula con contra-asiento REVERSA por línea (espejo, §7.1) y devuelve las
    /// cantidades a la requisición, recalculando su estado.</summary>
    Task<bool> AnularAsync(int id, string? motivo, string user, CancellationToken ct = default);
}
```

**`DescargoService.EntregarAsync` — el núcleo:**

```csharp
public async Task<DescargoDto> EntregarAsync(DescargoDto dto, string user, CancellationToken ct = default)
{
    var companyId = _company.GetCompanyId();
    if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

    // ── 0. Idempotencia del DOCUMENTO (mismo corte que RecepcionCompraService:224-235) ──
    if (dto.Uuid.HasValue)
    {
        var ya = await _context.alm_descargo_hdrs.AsNoTracking()
            .FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
        if (ya is not null) return (await GetByIdAsync(ya.id, ct))!;
    }

    var usuario = ClasificacionNormalizer.Usuario(user);
    var ahora   = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    var fecha   = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);

    // TransaccionAmbiente, no BeginTransactionAsync: los tests envuelven cada prueba en
    // BEGIN ... ROLLBACK y anidar no está soportado.
    await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

    // ── 1. Cabecera de requisición bajo candado ─────────────────────────────
    var req = await BloquearRequisicionAsync(dto.RequisicionId, companyId, ct)
        ?? throw new InvalidOperationException("La requisición no existe en la empresa actual.");

    if (req.tipo != TipoRequisicion.SalidaBodega)
        throw new InvalidOperationException(
            "Una requisición de reabastecimiento no se despacha: se convierte en orden de compra.");
    if (req.estado is not (EstadoRequisicionHdr.Aprobada or EstadoRequisicionHdr.DespachadaParcial))
        throw new InvalidOperationException(
            $"La requisición está {RequisicionEstados.Describir(req.estado)}: no admite entregas.");

    // ── 2. Renglones bajo candado, EN ORDEN DETERMINISTA ────────────────────
    // Una requisición admite VARIAS entregas (la parcialidad de Centura: CANTIDAD vs
    // CANTIDAD_APLICADA), así que dos despachos simultáneos leerían el mismo pendiente y
    // entregarían de más entre los dos. El ORDER BY id evita el deadlock cruzado.
    // El company_id va DENTRO del SQL crudo: EF compone su filtro por encima de la consulta.
    var renglones = await _context.alm_requisicions
        .FromSqlInterpolated($@"
            SELECT * FROM alm_requisicion
             WHERE company_id = {companyId} AND requisicion_hdr_id = {req.id}
             ORDER BY id
             FOR UPDATE")
        .ToListAsync(ct);

    var porId = renglones.ToDictionary(r => r.id);
    ValidarRenglonesEntrega(dto.Detalles, porId);   // pertenencia, pendiente, repetidos, > 0

    // ── 3. Pares (artículo, bodega): UNA consulta ANTES del bucle ───────────
    // En compras esto se hace dentro del bucle, con un SaveChanges por artículo nuevo
    // (ResolverParAsync, RecepcionCompraService.cs:754-789). Aquí NO hay nada que crear:
    // no se despacha desde una ubicación que no existe.
    var articuloIds = dto.Detalles.Select(d => porId[d.RequisicionLineaId].articulo_id!.Value)
                                  .Distinct().OrderBy(x => x).ToList();
    var pares = await ResolverParesAsync(articuloIds, req.bodega_id, companyId, ct);

    // ── 4. Correlativo del descargo ─────────────────────────────────────────
    var numero = await SiguienteNumeroAsync(companyId, ct);

    var hdr = new alm_descargo_hdr { /* numero, fecha, requisicion_hdr_id = req.id,
        bodega_id = req.bodega_id, entregado_por, recibido_por, observaciones,
        estado = Registrado, posteado = false, uuid = dto.Uuid ?? Guid.NewGuid(), auditoría */ };

    var lineas = dto.Detalles.Select(d =>
    {
        var r = porId[d.RequisicionLineaId];
        return new alm_descargo
        {
            cabecera        = hdr,        // la FK la resuelve EF: la cabecera aún no tiene id
            requisicion_id  = r.id,
            numero_requisicion = req.numero,   // llave de NEGOCIO, la que el usuario busca
            articulo_id     = r.articulo_id,
            codigo_articulo = r.codigo_articulo,   // snapshot del catálogo, no del cliente
            bodega_id       = req.bodega_id,
            fecha           = fecha,
            departamento    = req.departamento,
            cantidad        = d.Cantidad,
            origen          = OrigenDocumento.Siad,
            uuid            = Guid.NewGuid(),      // identidad de la LÍNEA
            posteado        = false
        };
    }).ToList();

    _context.alm_descargo_hdrs.Add(hdr);
    _context.alm_descargos.AddRange(lineas);
    await _context.SaveChangesAsync(ct);   // las líneas necesitan su id: ES el documento del asiento

    // ── 5. Posteo, una línea a la vez ───────────────────────────────────────
    decimal total = 0m;
    foreach (var linea in lineas)
    {
        var r = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo             = TipoMovimientoInventario.SalidaDescargo,
            ArticuloBodegaId = pares[linea.articulo_id!.Value],
            Cantidad         = linea.cantidad,
            CostoUnitario    = 0m,   // IGNORADO: la salida se valoriza al promedio vigente
            Fecha            = fecha,
            DocumentoTipo    = TipoDocumentoInventario.Descargo,
            DocumentoId      = linea.id,          // la LÍNEA DE DESCARGO, no la de requisición
            Observacion      = $"Requisición {req.numero:00000} · entrega {numero:00000} · depto {req.departamento}"
        }, user, ct);

        // El costo REAL con el que salió lo dice el asiento. Se copia al documento para que
        // sea autoexplicativo: en compras esto obliga a un JOIN contra alm_kardex en cada
        // GetById (RecepcionCompraService.cs:120-129).
        linea.precio_unitario = await CostoDelAsientoAsync(r.KardexId, ct);
        linea.total           = Redondear2(linea.cantidad * linea.precio_unitario);
        linea.posteado        = true;
        linea.fecha_posteo    = ahora;
        total += linea.total;

        // El contador lo escribe EL SERVICIO, bajo el mismo candado; ck_alm_requisicion_despachada
        // es la red final si alguna ruta intentara pasarse.
        var r0 = porId[linea.requisicion_id!.Value];
        r0.cantidad_despachada += linea.cantidad;
        if (r0.cantidad_despachada >= r0.cantidad) { r0.descargado = true; r0.fecha_entrega = fecha; }
    }

    hdr.total = Redondear2(total); hdr.posteado = true; hdr.fecha_posteo = ahora;

    // ── 6. Estado DERIVADO de las cantidades (≡ AplicarDescargaOrden) ───────
    req.estado = renglones.TrueForAll(r => r.cantidad_despachada >= r.cantidad)
        ? EstadoRequisicionHdr.Despachada
        : EstadoRequisicionHdr.DespachadaParcial;
    req.usuariomodificacion = usuario; req.fechamodificacion = ahora;

    await _context.SaveChangesAsync(ct);
    await TransaccionAmbiente.ConfirmarAsync(tx, ct);
    return (await GetByIdAsync(hdr.id, ct))!;
}
```

**Orden de bloqueos — invariante que hay que respetar en todo el módulo:**

```
requisicion_hdr  →  alm_requisicion (ORDER BY id)  →  alm_articulo_bodega (ORDER BY id)
```

Cualquier flujo que lo invierta produce deadlocks intermitentes (`40P01`), que el controller debe traducir.

**Validación del renglón (idéntica en espíritu a `RecepcionCompraService.cs:563-568`):**

```csharp
var pendiente = renglon.cantidad - renglon.cantidad_despachada;
if (d.Cantidad > pendiente)
    throw new InvalidOperationException(
        $"{renglon.codigo_articulo}: se entregan {d.Cantidad:0.##} y solo quedan {pendiente:0.##} pendientes.");
```

**Anulación de descargo (`AnularAsync`):** idempotente; recolecta los asientos `DESCARGO` por `documento_id`; postea un `Reversa` por línea (que con §7.1 **reingresa**); resta `cantidad_despachada` bajo candado; recalcula el estado de la requisición con la misma expresión que en el paso 6 (así `4 → 3` sale gratis); marca `estado = 9` con `anulado_por`/`fecha_anulacion`/`motivo_anulacion` **en columnas propias**, no concatenados en observaciones.

**Mejoras sobre compras que se aplican aquí (y no se repiten):**

| # | Deuda de compras | Corrección |
|---|---|---|
| 1 | `ResolverParAsync` hace un `SaveChangesAsync` por artículo dentro del bucle (`:754-789`) | Una sola consulta antes del bucle; en una salida no hay par que crear |
| 2 | La guarda previa a anular usa `AsNoTracking` (`:381-385`): entre la lectura y el posteo otra transacción puede consumir la mercadería | La guarda se hace sobre la fila **ya bloqueada** por el motor |
| 3 | El costo real exige un JOIN contra `alm_kardex` en cada `GetById` (`:120-129`) | Se copia a `alm_descargo.precio_unitario` al postear |
| 4 | Motivos/autores concatenados en `observaciones` y truncados a 1000 (`AnexarMotivo`, `:474-485`) | Columnas propias (`motivo_anulacion`, `anulado_por`, `fecha_anulacion`) |
| 5 | "Dos renglones no pueden descargar la misma línea" solo vive en C# (`:571-581`) | Respaldada por `uq_alm_descargo_renglon` |
| 6 | El controller no captura `KeyNotFoundException` ni `NotSupportedException` (salen 500) | Se capturan → 404 / 400 |

### 7.4 API

Todas bajo `[ApiController]` con permiso de clase; la conversión a O/C (si se aprueba D-12) va en `OrdenesCompraController` bajo `PermissionModules.Compras`.

```
GET    api/almacen/requisiciones                      View    cabeceras (contrato NUEVO)
GET    api/almacen/requisiciones/{id:int}             View
GET    api/almacen/requisiciones/historico            View    líneas planas (incluye SIMAFI)
GET    api/almacen/requisiciones/departamentos        View
GET    api/almacen/requisiciones/aprobables           View    bandeja del aprobador
GET    api/almacen/requisiciones/despachables         View    bandeja de bodega
GET    api/almacen/requisiciones/{id:int}/pendientes  View    grilla del despacho
POST   api/almacen/requisiciones                      Create
PUT    api/almacen/requisiciones/{id:int}             Edit
DELETE api/almacen/requisiciones/{id:int}             Delete  solo Borrador
POST   api/almacen/requisiciones/{id:int}/enviar      Edit
POST   api/almacen/requisiciones/{id:int}/aprobar     Edit    recurso requisiciones_aprobacion
POST   api/almacen/requisiciones/{id:int}/rechazar    Edit    recurso requisiciones_aprobacion
POST   api/almacen/requisiciones/{id:int}/devolver    Edit
POST   api/almacen/requisiciones/{id:int}/anular      Edit

GET    api/almacen/descargos                          View
GET    api/almacen/descargos/{id:int}                 View
GET    api/almacen/descargos/historico                View
POST   api/almacen/descargos                          Create  entrega
POST   api/almacen/descargos/{id:int}/anular          Edit
```

Las acciones que cambian estado son **POST autorizado como `Edit`**, siguiendo el precedente explícito de `RecepcionesCompraController.cs:89-90` ("cambia el estado de un documento existente, no crea uno nuevo").

**Permisos** — hoy no existe ninguno (`PermissionEndpointCatalog` solo registra carga-inicial y ajustes). Se agregan:

```csharp
public static class PermissionResources { public static class Inventario {
    public const string Requisiciones            = "requisiciones";
    public const string RequisicionesAprobacion  = "requisiciones_aprobacion";
    public const string Descargos                = "descargos";
}}
```

> **Advertencia que debe quedar por escrito.** Por el fallback de `ModuleAuthorize`, un sub-recurso de inventario es **superconjunto** de `module.inventario.*`, no una restricción: declarar `requisiciones_aprobacion` **no impide aprobar** a quien ya tiene `module.inventario.edit`. La **segregación de funciones** (no aprobar lo propio) vive en `AprobarAsync`, comparando `hdr.usuario_solicita` con el usuario actual. Si el requisito es duro, hay que sacar la aprobación del módulo Inventario o extender `ModuleAuthorize` — ver §11 D-4.

**Traducción de errores** (`MensajeDeBd`), además de lo que ya hace compras:

| SqlState / constraint | Mensaje |
|---|---|
| `23514` `ck_alm_requisicion_despachada` | "Se está entregando más de lo solicitado en algún renglón." |
| `23514` `ck_alm_articulo_bodega_reserva` | "El stock comprometido quedaría en negativo: recargue la requisición." |
| `23505` `uq_alm_requisicion_hdr_numero` | "El correlativo de requisición se topó con otro en curso: intente de nuevo." |
| `23505` `uq_alm_descargo_renglon` | "Hay dos renglones contra la misma línea de la requisición: únalos en uno solo." |
| `22003` | "Alguna cantidad o importe está fuera del rango permitido." (ver §10.6) |
| `40P01` | "Otro usuario está despachando esta requisición: intente de nuevo." |
| `K0001` / `K0002` | "El asiento del kardex es inmutable" / "El histórico migrado de SIMAFI no se puede modificar." |

### 7.5 UI

Todas las pantallas siguen el [estándar de grid](../../.github/skills/hodsoft-blazor-devexpress-ui/references/grid-standard.md) (referencia `ClientesList.razor`): `CssClass="grid-solicitudes"`, `PageSize="15"`, `PageSizeSelectorItems {5,10,15,25,50,100}`, `PagerPosition.Bottom`, `ColumnResizeMode.ColumnsContainer`, `LayoutAutoSaving`/`LayoutAutoLoading`, `EmptyDataAreaTemplate`, column chooser. El CSS compartido vive **una sola vez** en `apc/wwwroot/css/siad-grid.css`. Los `DxToastProvider` de página llevan `StickToViewport="true"`.

| Pantalla | Ruta | Rol | Notas |
|---|---|---|---|
| `RequisicionesList.razor` | `/almacen/requisiciones` | solicitante | **Cambia de contrato**: pasa de líneas a **cabeceras**. KPIs: en revisión · aprobadas · por entregar · monto. |
| `RequisicionFormPage.razor` | `/almacen/requisiciones/nueva` y `/{id:int}` | solicitante | Patrón `OrdenCompraFormPage`. Solo editable en Borrador. |
| `RequisicionesAprobacion.razor` | `/almacen/requisiciones/aprobacion` | jefe | Bandeja `estado = 2`. **Una fila = una decisión**, con confirmación. |
| `DescargosList.razor` | `/almacen/descargos` | bodeguero | Entregas nuevas + anulación. |
| `DescargoFormPage.razor` | `/almacen/descargos/nuevo?requisicion={id}` | bodeguero | Grid de pendientes con cantidad propuesta = `MIN(pendiente, existencia)`. |
| `RequisicionesHistorico.razor` | `/almacen/requisiciones/historico` | consulta | Es el grid plano actual, **movido**. |

**Bandeja de aprobación — la corrección de fondo.** En Centura la pantalla de salida de bodega tenía un combo por fila y un botón *Guardar* que recorría **todas** las filas del grid reescribiendo el estado de cada una, con el `catch` **vacío** (`frmAprobarSalidasBodega.cs:134-149`): una fila con `DBNull` cortaba el bucle en silencio. Aquí cada aprobación es **una llamada por documento**, con confirmación y un toast por resultado.

**Formulario de despacho.** Tres columnas por renglón: *Solicitado*, *Pendiente*, *Disponible*, con la cantidad precargada al mínimo — exactamente lo que hacía `qrySelectDataTmp`. Fila en rojo si `Disponible < Pendiente`.

> **Nota de cumplimiento.** `CLAUDE.md` pide llamar `TenantState.EnsureCompanyAsync()` antes de cargar datos del tenant, y **ninguna** página de `apc.Client/Pages/Almacen/` lo hace hoy (único uso en `Layout/AvisosPeriodosBanner.razor`). Las pantallas nuevas **sí** lo llaman; si se decide lo contrario, hay que documentarlo, no heredarlo por omisión.

**Sidebar:** el ítem plano `alm-requisiciones` pasa a subnodo con *Solicitudes* (`MatchExact = true`), *Aprobación*, *Entregas* e *Histórico*.

---

## 8. Estrategia de pruebas y criterios de aceptación

Suite de integración contra Postgres real (`SIAD_TEST_DB`), estilo `RecepcionCompraTests.cs`. Cinco clases nuevas.

**Regla transversal:** *toda prueba que postee o anule debe assertar existencia, `cantidad_despachada` y número de asientos*, no solo el DTO devuelto.

### 8.1 `RequisicionFlujoTests` (13 casos)
Alta nace en Borrador y **no deja ningún asiento** (`Assert.Equal(0, kardex del par)` — si esta aserción se quita, el PR se rechaza); número duplicado lo rechaza el índice único; sin bodega / sin líneas / cantidad ≤ 0 → error y **nada insertado**; aprobar dos veces es idempotente; aprobar sella `aprobado_por`/`fecha_aprobacion` y el CHECK rechaza un estado sin evidencia; anular una requisición con despachos → error y nada cambia; una anulada no revive (el bug de `Update_Cambiar_Status`); tenancy (otro `company_id` no la ve).

### 8.2 `DescargoTests` (16 casos)
Entrega total (existencia baja, promedio **intacto**, un asiento `DESCARGO`/`202`, `es_ajuste = false`, `documento_id = línea`); entrega **parcial** acumulativa (3 + 2 sobre 5 cierra, con dos `uuid` distintos); **más de lo pendiente** → error y `Assert.Equal(1, asientos)`; existencia insuficiente → error; **mismo `Uuid` no duplica** (existencia 70, no 40); **llamador mentiroso**: postear el mismo `DocumentoId` con otro `DocumentoTipo` devuelve `YaExistia` (la prueba que compras no tiene y su propio comentario pide); requisición no aprobada / anulada → error; línea de otra requisición → error; bodega distinta a la de la requisición → error; **par con `costo_promedio = 0` → error** (§10.2); par inactivo → error.

### 8.3 `DescargoAnulacionTests` — **el caso que hoy fallaría**
```
Arrange: requisición aprobada 30, despachados 30 (existencia 100 → 70).
Act:     AnularAsync
Assert:  existencia == 100     ← con la reversa actual (§7.1) saldría 40
         asiento REVERSA con ingresos == 30 y salidas == 0, tipo "102"
         cantidad_despachada == 0, requisición vuelve a Aprobada
         costo_promedio re-ponderado al costo con que salió
```
Más: anular dos veces es idempotente; anular uno de dos descargos deja la requisición en parcial.

### 8.4 `InventarioSalidaTests` (10 casos)
Contrato del motor sin documentos: la salida no mueve el promedio (24 u. a 56.7917 − 6 → 18 a **56.7917 exacto**); existencia exacta a 0 se permite; negativo se rechaza; cantidad 0/negativa se rechaza; `DocumentoId = 0` se rechaza; `documento_tipo` fijo aunque el llamador mande otro; mapea a `"202"`; `es_ajuste = false`; el rollup de cabecera cuadra.

### 8.5 `AntiDuplicacionHistoricoTests` — la guardia que faltó en compras
- El motor **rechaza** postear una línea `origen='SIMAFI'`.
- `Assert.Equal(0, alm_requisicion.Count(r => r.origen=="SIMAFI" && !r.posteado))` — idem descargos.
- Des-postear el histórico → `PostgresException` `K0002`; cambiar `origen` → `K0002`.
- Ejecuta como aserciones las tres consultas críticas de detección (§10.1).

### 8.6 Concurrencia — harness aparte
`IntegrationTestBase` abre **una** conexión y **una** transacción con ROLLBACK (`:17-38`): dos `DbContext` ahí **no compiten por candados**, así que hoy es *imposible* probar el `FOR UPDATE` — y ninguna prueba del repo lo intenta. Se agrega `ConcurrenciaTestBase` con **dos conexiones**, que prueba el candado **sin commitear**: A postea y no confirma; B ejecuta `SET LOCAL lock_timeout = '500ms'` y debe fallar con `SqlState = '55P03'`; ambas hacen ROLLBACK. Cero residuo — indispensable, porque `alm_kardex` tiene `trg_alm_kardex_inmutable` y **lo commiteado ahí no se puede borrar**.

Los escenarios que exigen commit (el segundo despacho ve el resultado del primero y es rechazado) van a una suite `[Trait("Categoria","Sucia")]`, solo contra una BD desechable, fuera de CI.

### 8.7 Criterios de aceptación por fase

| Fase | Criterio |
|---|---|
| **F0** Motor | Reversa espejo implementada y `DescargoAnulacionTests` verde; guarda de costo 0; **`InventarioPostingTests`, `CargaInicialTests`, `RecepcionCompraTests` y `KardexPuntoCorteTests` siguen verdes** (no regresión). Sin esto no se avanza. |
| **F1** BD | Script aditivo, idempotente, con prerrequisitos `RAISE EXCEPTION`; aplicado al mirror; V1–V8 de §6.3 OK; los conteos históricos (42.866 / 42.757) **idénticos** antes y después; registrado en el runbook. |
| **F2** Requisición | `RequisicionFlujoTests` verde, **con la aserción de 0 asientos**. Sin reserva de stock (D-2). |
| **F3** Descargo | `DescargoTests` + `DescargoAnulacionTests` + `AntiDuplicacionHistoricoTests` verdes. **Prerrequisito duro: 0 pares con `existencia > 0 AND costo_promedio = 0`** en el entorno destino. |
| **F4** UI y permisos | Recursos y endpoints registrados; `PermisosInventarioTests` extendido y verde; estándar de grid; confirmación explícita en la anulación. |
| **F5** Puesta en marcha | Detectores de §10.1 en 0 filas durante 7 días; un descargo real anulado y verificado a mano contra el kardex. |

---

## 9. Matriz de dependencias de BD

| # | Objeto | Tipo | Estado | Acción | Script | Orden |
|---|---|---|---|---|---|---|
| 1 | `alm_requisicion.origen/posteado/uuid/bodega_id` + 4 CHECK + `trg_*_blindaje` | columnas/constraints | **existe en el mirror; NO CONSTA en el SRV** | **Registrar y confirmar aplicado** | `Database/2026-07-14_alm_documentos_bodega_posteo.sql` | **0 — prerrequisito duro** |
| 2 | `alm_kardex.uuid` + `ck_alm_kardex_documento_tipo` + `trg_alm_kardex_inmutable` | columnas/constraints | existe (mirror) | verificar en SRV | `Database/2026-07-14_alm_kardex_trazabilidad.sql` | 0 |
| 3 | `uq_alm_bodega_company_id`, `uq_alm_articulo_company_id` | AK | existe | verificar | `Database/2026-07-14_alm_fk_compuestas_tenant.sql` | 0 |
| 4 | Carga inicial / corte (Fase 8) | datos | **pendiente** — 241 pares sin costo | **ejecutar antes de F3** | ver memoria `diseno-carga-inicial-kardex` | 0 |
| 5 | `uq_alm_requisicion_tenant`, `uq_alm_descargo_tenant` | AK | **NO EXISTE** | **CREAR** | `2026-08-01_alm_requisicion_descargo.sql` §1 | 1 |
| 6 | `alm_requisicion_hdr` | tabla | — | **CREAR** | ídem §2 | 2 |
| 7 | `alm_requisicion.requisicion_hdr_id`, `cantidad_despachada`, `aplicado_en_oc` | columnas | — | **ALTER** + backfill | ídem §3 | 3 |
| 8 | `ck_alm_requisicion_despachada`, `ck_alm_requisicion_no_postea` | CHECK | — | **CREAR** | ídem §3 | 3 |
| 9 | `ix_alm_requisicion_pendiente` | índice | existe | **DROP** (deliberado, §6.2) | ídem §3 | 3 |
| 10 | `alm_descargo_hdr` | tabla | — | **CREAR** | ídem §4 | 4 |
| 11 | `alm_descargo.descargo_hdr_id`, `requisicion_id` + FK + `uq_alm_descargo_renglon` | columnas | — | **ALTER** | ídem §5 | 5 |
| 12 | `alm_requisicion_correlativo`, `alm_descargo_correlativo` | tablas | **NO EXISTEN** | **CREAR + SEMBRAR en 17124** | ídem §6 | 6 |
| 13 | `ck_alm_articulo_bodega_reserva` | CHECK | — | **CREAR** (validado: 638 filas en 0) | ídem §7 | 7 |
| 14 | `ck_alm_kardex_documento_tipo` | CHECK | existe y **ya admite DESCARGO** | **sin cambio** | — | — |
| 15 | `TipoMovimientoInventario.SalidaDescargo` | C# | — | **CREAR** (no BD) | — | — |
| 16 | Catálogo de departamentos | tabla | **NO EXISTE** | decidir (D-15) | pendiente | — |
| 17 | `cnt_centroscosto` | tabla | existe, **0 filas** | decidir (D-7) | pendiente | — |

> **Hueco de proceso detectado.** El script `2026-07-14_alm_documentos_bodega_posteo.sql` —del que depende **todo** este diseño— tiene **0 menciones** en `Database/2026-07-23_runbook_despliegue_srv.md` y **0** en `Database/2026-07-30_pendientes_srv.md` (verificado con grep). Su estado en el SRV **no consta**. Es exactamente la categoría que ese registro define como "los que pueden hacer fallar un despliegue". **Hay que registrarlo antes de escribir el script nuevo**, con la skill `runbook-despliegue-srv`. Confirmación para el SRV:
> ```sql
> SELECT count(*) FROM information_schema.columns
>  WHERE table_name IN ('alm_requisicion','alm_descargo')
>    AND column_name IN ('origen','posteado','fecha_posteo','uuid','bodega_id');  -- esperado: 10
> ```

---

## 10. Riesgos

### 10.1 CRÍTICO — Duplicar el inventario (tratamiento a fondo)

**El hecho.** Está medido, no supuesto: `alm_requisicion` (42.866 / L 68.172.601,59), `alm_descargo` (42.757 / L 68.044.988,25) y `alm_kardex` `202` (42.696 / L 67.996.763,43) son **el mismo hecho contado tres veces**; **42.651 tríos** `(numero, codigo_articulo)` están en las tres tablas, y el comentario del descargo aparece **literal** en `alm_kardex.observacion`. La salida histórica **ya está descontada**.

**Los cinco vectores y su mitigación:**

| # | Vector | Por qué puede pasar | Mitigación en este diseño |
|---|---|---|---|
| V1 | Postear el histórico SIMAFI | El índice `ix_*_pendiente` define un conjunto "pendiente de postear"; las entidades EF inicializan `origen = OrigenDocumento.Siad`, así que un INSERT accidental entra como SIAD | El histórico es 100 % `SIMAFI` + `posteado=true`; `trg_*_blindaje` (K0002) impide des-postearlo y cambiar `origen`; **`AntiDuplicacion_ElMotorRechazaUnaLineaSimafi`** lo convierte en prueba |
| V2 | Que **ambos** documentos posteen | Ambas tablas tienen infraestructura idéntica y `ck_alm_kardex_documento_tipo` admite `REQUISICION` **y** `DESCARGO` | **Solo el descargo postea.** `ck_alm_requisicion_no_postea` + `DROP ix_alm_requisicion_pendiente` (§6.2). `TipoDocumentoInventario.Requisicion` queda **sin productor** y se documenta así |
| V3 | **Colisión de `uuid` entre dos tablas** del mismo `documento_tipo` | Si existiera una `alm_descargo_dtl` nueva (id desde 1) junto a la plana (id hasta 42.757), el `uuid` `DESCARGO\|company\|id\|par` sería el mismo. El corte devuelve `YaExistia=true` **sin insertar** y el servicio marca posteado → **salida sin asiento** | **No se crea tabla `_dtl`**: una sola tabla puede reclamar `DESCARGO` (§6.1). El `uuid` deriva del tipo **FIJO** en el switch, nunca del DTO |
| V4 | Re-postear un documento SIAD des-posteado | `alm_documento_blindaje()` permite **a propósito** des-postear un SIAD | El `uuid` deriva del **`id` de la línea**, no de un `uuid` regenerable: re-postear devuelve el mismo asiento. Detector V7 |
| V5 | Re-teclear a mano un documento histórico en el módulo nuevo | Los **47.215** asientos históricos tienen `uuid` NULL → quedan **fuera** de `uq_alm_kardex_company_uuid` (índice parcial) | **NO cerrado por este diseño.** Mitigación: fecha de corte (D-11) + rechazar documentos con fecha anterior al corte. Queda declarado como riesgo residual |

**Detectores (solo `SELECT`, a versionar como `Database/2026-08-XX_verificacion_duplicacion_salidas.sql`).** V-1, V-3 y V-6 se ejecutan además desde `AntiDuplicacionHistoricoTests`.

```sql
-- V-0  PRERREQUISITO de habilitación. Medido hoy: 241 | 3 | 638. Debe quedar 0 | 0.
SELECT count(*) FILTER (WHERE existencia > 0 AND costo_promedio = 0) AS sin_costo,
       count(*) FILTER (WHERE existencia < 0)                        AS negativas,
       count(*)                                                      AS pares
  FROM alm_articulo_bodega;

-- V-1  DETECTOR MAESTRO: el kardex descargó MÁS de lo que dice cualquiera de los documentos.
--      Hoy (histórico intacto) devuelve 0 filas.
WITH k AS (SELECT coalesce(numero_documento::numeric,0) n, codigo_articulo ca, sum(salidas) qty
             FROM alm_kardex
            WHERE salidas > 0 AND (documento_tipo IS NULL OR documento_tipo IN ('REQUISICION','DESCARGO'))
            GROUP BY 1,2),
     r AS (SELECT numero n, codigo_articulo ca, sum(cantidad) qty FROM alm_requisicion
            WHERE codigo_articulo IS NOT NULL GROUP BY 1,2),
     d AS (SELECT numero_requisicion n, codigo_articulo ca, sum(cantidad) qty FROM alm_descargo
            WHERE numero_requisicion IS NOT NULL AND codigo_articulo IS NOT NULL GROUP BY 1,2)
SELECT k.n, k.ca, k.qty AS en_kardex, r.qty AS en_requisicion, d.qty AS en_descargo
  FROM k LEFT JOIN r USING (n, ca) LEFT JOIN d USING (n, ca)
 WHERE k.qty > coalesce(greatest(r.qty, d.qty), k.qty);

-- V-2  Dos asientos de salida para el mismo documento.
SELECT documento_tipo, documento_id, count(*) FROM alm_kardex
 WHERE documento_tipo IN ('REQUISICION','DESCARGO') AND salidas > 0
 GROUP BY 1,2 HAVING count(*) > 1;

-- V-3  Documentos SIAD posteados sin evidencia (el CHECK debería impedirlo).
SELECT 'descargo' t, id, numero_documento FROM alm_descargo
 WHERE origen='SIAD' AND posteado AND (uuid IS NULL OR fecha_posteo IS NULL);

-- V-4  Reversas mal orientadas (caza directa del defecto de §7.1). Hoy: 0 filas.
SELECT r.id reversa, r.salidas, r.ingresos, o.id original, o.salidas o_sal, o.ingresos o_ing
  FROM alm_kardex r JOIN alm_kardex o ON o.id = r.documento_id
 WHERE r.documento_tipo = 'REVERSA'
   AND ((o.salidas > 0 AND r.salidas > 0) OR (o.ingresos > 0 AND r.ingresos > 0));

-- V-5  Salidas del libro NUEVO grabadas a valor 0.
SELECT id, articulo_id, bodega_id, fecha, salidas, valor_unitario, total FROM alm_kardex
 WHERE salidas > 0 AND documento_tipo IS NOT NULL AND coalesce(valor_unitario,0) = 0;

-- V-6  Requisición: lo despachado nunca puede superar lo solicitado.
SELECT id, numero, codigo_articulo, cantidad, cantidad_despachada FROM alm_requisicion
 WHERE cantidad_despachada > cantidad OR cantidad_despachada < 0;

-- V-7  Documento SIAD des-posteado con asiento vivo (re-posteo encubierto).
SELECT d.id, d.numero_documento, k.id AS asiento FROM alm_descargo d
  JOIN alm_kardex k ON k.documento_tipo = 'DESCARGO' AND k.documento_id = d.id
 WHERE d.origen = 'SIAD' AND d.posteado = false;

-- V-8  Cuadre existencia ↔ kardex (solo válido DESPUÉS del corte: filtra uuid IS NOT NULL).
WITH mov AS (SELECT articulo_id, bodega_id, sum(ingresos)-sum(salidas) neto
               FROM alm_kardex WHERE uuid IS NOT NULL GROUP BY 1,2)
SELECT b.articulo_id, b.bodega_id, b.existencia, m.neto, b.existencia - m.neto AS descuadre
  FROM alm_articulo_bodega b JOIN mov m USING (articulo_id, bodega_id)
 WHERE b.existencia <> m.neto;
```

> **Llave partida (C-22).** El motor escribe `documento_id` y **no** `numero_documento`; el histórico, al revés. Por eso V-1 usa `coalesce(numero_documento,0)` y **no** vería una duplicación entre libro viejo y libro nuevo. **Corrección adoptada:** el asiento del descargo estampa **también** `numero_documento = req.numero` (la llave de negocio), para que exista **un solo** detector. La columna es `NUMERIC(11,0)` y el número histórico llega a 17.124: cabe.

### 10.2 CRÍTICO — Salida a costo 0
De los 638 pares, **241 tienen existencia > 0 y `costo_promedio = 0`**. Una salida se valoriza a `fila.costo_promedio`: grabaría `valor_unitario = 0`, `total = 0`, consumo real con costo cero, **irreversible** (kardex inmutable). Mitigación: guarda dura en el motor (§7.2) + prerrequisito de despliegue (F3). Además, **3 pares con existencia negativa** harán que toda salida sea rechazada con un error que el usuario no puede resolver desde la pantalla: hay que sanearlos en el corte.

### 10.3 CRÍTICO — Reversa invertida
Ver §7.1. Es el riesgo con **mayor daño y menor visibilidad**: no falla, miente. Detector V-4 + prueba `DescargoAnulacionTests`.

### 10.4 ALTO — Concurrencia no probable hoy
El `FOR UPDATE` que el propio motor declara imprescindible (`:214-218`) **nunca se ha ejercitado**, ni en compras: el harness usa una conexión. Dos despachos simultáneos tienen `uuid` distintos, así que el índice único **no los frena**; solo el candado. Mitigación: §8.6 + orden de bloqueo declarado en §7.3.

### 10.5 ALTO — Mezcla histórico/nuevo en la misma tabla
`alm_requisicion` tendrá 42.866 líneas SIMAFI con `requisicion_hdr_id` NULL junto a las nuevas. **Toda** consulta del flujo nuevo debe filtrar por `requisicion_hdr_id IS NOT NULL` — un olvido convierte 42.866 líneas migradas en pendientes vivos. El índice `ix_alm_requisicion_pendiente_entrega` lleva ese filtro.

### 10.6 MEDIO — Desbordes numéricos
`alm_descargo.total` y `precio_unitario` son `NUMERIC(11,2)`; `alm_kardex.total` es `NUMERIC(17,4)` y `costo_promedio` `NUMERIC(12,4)`. Una salida grande valorizada al promedio puede **desbordar el documento aunque el asiento quepa** → `22003`. Prueba dedicada + traducción en el controller.

### 10.7 MEDIO — Departamento: el asiento nuevo no lo llevará
`alm_kardex.departamento` está poblada en **42.862 de 47.231** filas (histórico) y el motor **no la escribe** (`:105-145`). Toda salida nueva la dejará NULL y "consumo por departamento" devolverá el histórico y **nada de lo nuevo**. Además hay una **discordancia de tipo**: `alm_requisicion.departamento` es `VARCHAR(3)` y `alm_descargo.departamento` `VARCHAR(2)`.

### 10.8 MEDIO — `traslado = 'T'` en el 82 % del histórico
35.184 filas `'T'` (L 60.730.347,71) vs 7.573 `'0'` (L 7.314.640,54). Si `'T'` fuera traslado entre bodegas, esas filas **no son consumo** y el costeo a gasto del histórico está mal. **NO CONFIRMADO** (N-7). No afecta al flujo nuevo si se ignora, pero sí a cualquier reporte que compare histórico contra nuevo.

### 10.9 MEDIO — Cambio de contrato de la API
`GET api/almacen/requisiciones` pasa de líneas a cabeceras: rompe `RequisicionesClient` y `RequisicionesList.razor`. Backend, cliente, página y sidebar se mueven **juntos** o la pantalla queda en blanco.

### 10.10 BAJO — Huérfanos y basura de migración
33 líneas de requisición y 15 de descargo **sin `articulo_id`**; 15 filas de descargo fantasma (`numero_requisicion` NULL, 13 completamente vacías, 2 con artículo y `total = 0`, `oficina = '01-01'`). Todo detector debe excluirlas o reportará ruido permanente y la gente dejará de mirarlo.

---

## 11. Decisiones que DEBE tomar el usuario

| # | Decisión | Opciones | Recomendación |
|---|---|---|---|
| **D-1** | **¿El descargo genera asiento contable?** Hoy `SIAD.Services/Contabilidad` (21 archivos) **no referencia almacén en ninguno**; `alm_tipo_articulo` ya tiene `cuenta_inventario`/`cuenta_costo_ventas`/`cuenta_ajustes`; `alm_descargo` trae dos pares de cuentas cuyo rol el script de migración **pide validar con contabilidad**; y en Centura existe un estado `2 'Contabilizada'` que **ninguna fuente escribe** (N-1). | (a) fuera de alcance, como el ISV en compras · (b) en la 1ª entrega | **(a)**. Es una pieza entera (póliza, periodo, cuentas) y bloquearía la captura. Si se elige (b), hay que extraer de SQL Server el objeto que pone `APROBADA = 2`. |
| **D-2** | **¿La requisición aprobada aparta stock** (`existencia_comprometida`)**?** Es **regla NUEVA**: Centura **no reserva** (C-15). Hoy la columna vale 0 en las 638 filas y **nadie la escribe**; además la guarda del motor compara contra `existencia`, **no** contra `existencia − comprometida`, así que la reserva sería decorativa. | (a) NO reservar (fiel a Centura) · (b) reservar | **(a)** en la 1ª entrega. Si se elige (b), son obligatorios: (1) que la guarda del motor reste la comprometida; (2) recomputarla **derivada** de las líneas abiertas (nunca `+=`, no es idempotente); (3) liberarla en despacho/anulación/rechazo; (4) decidir qué pasa con las 4 requisiciones aprobadas-no-descargadas del histórico, la más vieja de **2018**. |
| **D-3** | **¿Aprobar puede recortar cantidades?** En Centura **no** (C-16): el único efecto es cambiar `APROBADA`. | (a) solo aprueba/rechaza · (b) `cantidad_aprobada` editable | **(a)**. (b) cambia el proceso (revisión renglón por renglón) y abre el caso "aprobada < ya despachada", que nadie resolvió. Con (a) el tope es `cantidad`, como en el legacy. |
| **D-4** | **¿Quién puede aprobar?** En Centura **no hay control**: mismo `arrSeguridad[33]`, el usuario ni se lee, se puede aprobar lo propio (C-9). Pero la jerarquía **existe** (`dbo.esJefe` + `CNF_USUARIOS.MANAGER`) y se usa para filtrar consultas; y hay un camino abandonado que sí filtraba (`SP_AprobacionRequizas`). | (a) heredar la ausencia · (b) recurso propio + regla "no apruebo lo mío" · (c) jerarquía por departamento | **(b)**. Barato y cierra el agujero. Ojo: por el fallback de `ModuleAuthorize` el recurso **no restringe**; la regla vive en `AprobarAsync` (§7.4). |
| **D-5** | **¿Quién ve qué requisición?** Centura: mías **OR** de mis subordinados **OR** ya aprobadas (C-12). El portal hoy muestra **todo** a cualquiera con permiso de módulo. | (a) todas · (b) replicar la regla | **(a)** en la 1ª entrega (no hay catálogo de jerarquía en el portal); (b) cuando exista D-15. |
| **D-6** | **¿De dónde sale la bodega?** `bodega_id` está NULL en el 100 % del histórico; el stock vive **todo en la bodega id 2 (`'01'`)**, PRIN (id 1) está vacía y `'11'` tiene 4 pares. | (a) la elige el usuario en la cabecera · (b) se deriva del departamento · (c) fija | **(a)** con `'01'` por defecto. `qrySelectDataTmp` hace `CASE :COD_BODEGA WHEN 0 THEN 1` (**NO CONFIRMADO** qué form lo alimenta, N-9), así que no hay regla legacy que replicar. |
| **D-7** | **¿El detalle lleva centro de costo, "Zona" y cuenta contable?** En Centura los tres son del renglón y los dos primeros **obligatorios**. En el portal: `cnt_centroscosto` tiene **0 filas**, `con_centro_costo` 13, y **no hay equivalente de Zona**. | (a) solo cuenta contable heredada del tipo · (b) los tres, creando catálogos | **(a)** en la 1ª entrega. (b) es un proyecto de catálogos aparte. Nota: es esta decisión la que hace inviable un `UNIQUE (requisición, artículo)` (§6.6). |
| **D-8** | **¿Existe devolución a bodega de material ya entregado?** No tiene respaldo legacy (§7.2). | (a) no · (b) sí | **(a)**. La anulación del descargo (reversa) cubre el error de captura. (b) exige definir a qué costo reingresa y si re-pondera. |
| **D-9** | **¿Qué significa `traslado = 'T'`** (35.184 filas)**?** | (a) es consumo · (b) es traslado entre bodegas/oficinas | **Preguntar.** Si es (b), hace falta un documento de **TRASLADO** aparte (`documento_tipo` ya admitido, sin implementar) y el histórico está mal costeado. No bloquea el flujo nuevo. |
| **D-10** | **¿El correlativo continúa desde 17.124 o arranca en 1?** | (a) continuar (17.125) · (b) arrancar en 1 | **(a)**. Con (b) conviven "requisición 5" de 2015 y "requisición 5" de 2026 en el mismo módulo — precedente vivo: en compras ya hay una recepción #1 y una compra #1. |
| **D-11 · RESUELTO EN PARTE** | **(usuario, 2026-08-01) SIMAFI ya no ingresa datos**; se traerá un respaldo de esa base. Queda cerrado el riesgo de colisión de numeración: el correlativo continúa desde 17124 sin que el legacy emita en paralelo. **Sigue abierto**: qué hacer con las 44 requisiciones abiertas y las 4 aprobadas-no-descargadas del histórico (el script del paso 26 **no** las cierra). | — | — |
| **D-11 (original)** | **Fecha de corte y qué pasa con lo abierto.** El histórico corta el **2025-11-12** (hace 8 meses). Quedan **44 requisiciones abiertas** (88 líneas `'P'`) y 4 aprobadas-no-descargadas, la más vieja de 2018. | (a) cerrarlas en el script · (b) dejarlas | **(a)** con `estatus='A'` y observación "cerrada por corte AAAA-MM-DD". Con (b), el KPI "Pendientes" queda clavado para siempre. **Y hay que confirmar si SIMAFI sigue emitiendo**: si sí, su numeración colisiona con la nueva y el blindaje por `uuid` no lo detecta. |
| **D-12** | **¿Entra el flujo de REABASTECIMIENTO** (requisición → O/C)**?** `alm_orden_compra.requisicion_id` existe, se persiste en el alta (`OrdenCompraService.cs:152`), **sin FK, sin índice, sin UI y NULL en el 100 %** de las 4 O/C. `ActualizarAsync` tampoco la toca. | (a) fuera de alcance · (b) incluir | **(a)** en la 1ª entrega; el modelo ya deja `tipo = 2` y el estado 6 preparados. Si (b): agregar la FK compuesta a `alm_requisicion_hdr` (hoy sin backfill, 4 filas NULL) y reusar `CrearAsync` de O/C. |
| **D-13** | **¿Se necesita imprimir el vale de salida?** Centura lo imprime (FastReport + reporte 51); el portal **no tiene ningún reporte** de requisición. | (a) sí · (b) no | **Preguntar.** En bodega el vale firmado respalda la entrega; sin él el flujo nuevo es operativamente peor que Centura. Requiere extraer el layout (fila 51 de `AXL_REPORTES` y los `.rpt`). |
| **D-14** | **¿Bajo qué módulo caen los permisos?** Órdenes/recepciones usan `PermissionModules.Compras`; requisiciones/descargos hoy cuelgan de `Inventario` sin recurso propio. | (a) Inventario · (b) Compras | **(a)**. La salida de bodega es inventario; la compra es otro módulo. |
| **D-15** | **¿Se crea catálogo de departamentos?** No existe **ninguna** tabla `%departament%`: es texto libre y el combo se arma con `SELECT DISTINCT` sobre el histórico. Centura tiene `REC_DEPARTAMENTO` con FK real. | (a) texto libre · (b) catálogo | **(b)** a medio plazo; **(a)** para no bloquear la 1ª entrega. Es prerrequisito de D-5. |
| **D-16** | **¿En qué unidad se pide y se despacha?** Centura distingue `COD_UNIDADPEDIDO` de `COD_UNIDAD_SALIDA`. El portal tiene `unidad_almacenaje_id`/`unidad_salida_id` y `factor_conversion`, pero solo **7 de 634** artículos los tienen y **ninguno** con salida distinta de almacenaje. | (a) una sola unidad · (b) conversión | **(a)**. Recordar que `alm_articulo.unidad_medida` (texto legacy) trae **basura de diámetros**. |
| **D-17** | **¿Los artículos en CONSIGNACIÓN** (tipo `'05'`, 12 artículos) **se despachan con las mismas reglas?** Consignación suele implicar que el material no es propiedad hasta consumirse. | (a) igual · (b) tratamiento aparte | **Preguntar.** Puede cambiar el asiento contable (depende de D-1). |
| **D-18** | **¿Qué artículos son requisables?** Centura filtra `p.status = 1` y ramifica por `REBAJA_INVENTARIO`. El portal tiene `alm_tipo_articulo.maneja_inventario` (los 9 tipos en `true`) y **3 artículos sin tipo asignado**. | (a) `activo AND maneja_inventario` · (b) solo activo | **(a)**, y sanear los 3 huérfanos (no heredan cuentas ni bandera). |

---

## 12. Plan por fases

Cada fase es **entregable y verificable por separado**. Nada se aplica en BD ni se publica hasta que el usuario lo indique.

### Fase 0 — Motor (sin BD, sin UI) · *desbloquea todo lo demás*
- Reversa **espejo** discriminando por `documento_tipo` (§7.1), cantidad y costo tomados del asiento original, guardas de no-negativo y de costo positivo, rechazo de reversa-de-reversa.
- `TipoMovimientoInventario.SalidaDescargo = 8` + las 8 piezas de §7.2.
- **Verificable:** `InventarioSalidaTests` (10) y `DescargoAnulacionTests` verdes, **y** `InventarioPostingTests` + `CargaInicialTests` + `RecepcionCompraTests` + `KardexPuntoCorteTests` sin regresión. Detector V-4 en 0 filas.

### Fase 1 — Base de datos
- **Antes:** registrar `2026-07-14_alm_documentos_bodega_posteo.sql` en el runbook y en el registro de pendientes SRV (hoy **0 menciones**), y confirmar su estado en el servidor.
- Escribir `Database/2026-08-01_alm_requisicion_descargo.sql` (§6.3), aplicarlo **al mirror**, registrarlo con la skill `runbook-despliegue-srv`.
- **Verificable:** V1–V8 de §6.3; los conteos históricos idénticos antes y después; prueba de humo manual dentro de `BEGIN … ROLLBACK`.

### Fase 2 — Requisición (solicitud) · *todavía no mueve inventario*
- Entidades + mapeo EF, `EstadoRequisicionHdr`, DTOs, `RequisicionService` (CRUD + T1–T6, T9, T10), controller, permisos, cliente HTTP.
- **Verificable:** `RequisicionFlujoTests` (13) verdes, **con la aserción explícita de 0 asientos en el kardex**. Detector V-6 en 0 filas.

### Fase 3 — Descargo (la salida)
- `DescargoService.EntregarAsync` / `AnularAsync`, controller, cliente.
- **Prerrequisito duro: V-0 en 0 | 0** en el entorno destino (corte / Fase 8 ejecutada).
- **Verificable:** `DescargoTests` (16), `DescargoAnulacionTests`, `AntiDuplicacionHistoricoTests` (5) y `AlmacenConcurrenciaTests` (3 en CI) verdes. Detectores V-1, V-2, V-5, V-7 en 0 filas.

### Fase 4 — UI y permisos
- `RequisicionesList` (cabeceras), `RequisicionFormPage`, `RequisicionesAprobacion`, `DescargosList`, `DescargoFormPage`, `RequisicionesHistorico`; sidebar; recursos en `PermissionNames` + `PermissionEndpointCatalog`.
- **Backend, cliente, página y sidebar se mueven juntos** (§10.9).
- **Verificable:** `PermisosInventarioTests` extendido y verde; estándar de grid cumplido; anulación con confirmación.

### Fase 5 — Puesta en marcha y vigilancia
- Versionar los detectores como `Database/2026-08-XX_verificacion_duplicacion_salidas.sql` (solo `SELECT`), registrado en el runbook como paso de **verificación**.
- **Verificable:** V-1..V-8 en 0 filas durante 7 días; volumen diario de `DESCARGO` comparado contra el promedio histórico (≈ 11 líneas/día); un descargo real anulado y comprobado a mano contra el kardex.

### Fuera de plan (dependen de decisiones)
Contabilidad (D-1), reserva de stock (D-2), reabastecimiento → O/C (D-12), vale de salida (D-13), catálogos de departamento/centro de costo/zona (D-7, D-15), traslados (D-9), devolución a bodega (D-8), reportes de gestión (`ShowCmpReq`, `showEstCmpReq`, consumo por producto/departamento, lapsos).

---

## 13. Trazabilidad

| Regla / dato | Fuente |
|---|---|
| El menú solo llama a una DLL externa | `GA_IN.APT:2725-2733`; declaración en `:623`, `:742` |
| Los dos menús de aprobación comparten `arrSeguridad[33]` | `GA_IN.APT:2735-2754` (leído directamente) |
| `ShowAprobaciones` es código muerto | `GA_IN.APT:2743` (prefijo `!`) |
| Menú "&Requisicion" del popup Compras, comentado | `GA_IN.APT:2544-2551` |
| Los menús NO se ocultan por el parámetro 228 | `GA_IN.APT:4113-4115` (bloque comentado) |
| Reportes vivos no analizados | `GA_IN.APT:3322-3341` (`ShowCmpReq`, `showEstCmpReq`) |
| Reporte de consumo por producto sin acción | `GA_IN.APT:3352-3359` |
| Reporte "Pendientes de Entregar" | `GA_IN.APT:3342-3351`; `prGarantias.dll` `qryReqPendientes` |
| Detalle contra tabla temporal `#INV_REQUISICION_DTL` | `prGarantias.dll` `qryRequisicionDtl` |
| Única escritura del ingreso | `prGarantias.dll` `muInsertarRequisicion` |
| Cabecera sin SQL literal (`TMSTable`) | `prGarantias.dll` `tblRequisicionHdr` |
| `NUM_REQUISICION` es IDENTITY | DFM `ReadOnly`; `prSiad/Datasets/dtsInventarios.xsd:36-37` |
| Estados 0/1/2/3 y tipos `FLAG_REABASTECIMIENTO` | `prGarantias.dll` `qryReqConsulta` |
| El `2` como 'Denegada' (fuente que discrepa) | `prSiad` `SP_ConsultarRequisicionesTableAdapter` |
| Regla de visibilidad `dbo.esJefe` | `prGarantias.dll` `qryReqConsulta` |
| Único UPDATE de estado, sin estado previo | `prSiad` `INV_REQUISICION_HDRTableAdapter.Update_Cambiar_Status` |
| Aprobación de reabastecimiento: valor fijo 1, grid ReadOnly | `prSiad` `AprobacionRequisiciones.cs:89-109`, `:168` |
| Aprobación de salidas: lote sobre todas las filas, `catch` vacío | `prSiad` `frmAprobarSalidasBodega.cs:134-149` |
| `frmAprobarSalidasBodega` se instancia SIN usuario | `prSiad` `Exports.cs:783` |
| `FECHA_APROBADA` la escribe nadie | grep sobre 650 archivos decompilados + `.APT` |
| Validaciones legibles del ingreso (gemelo .NET) | `prSiad` `frmRequisicion.cs:81-177`, `:231-260` |
| Anulación `APROBADA = 3` | `prSiad` `INV_REQUISICION_HDRTableAdapter.UpdateSTATUS` |
| Entrega = documento aparte con `@@IDENTITY` | `prGarantias.dll` `qryInsertAplInv` |
| Entrega parcial y tope contra existencia | `prGarantias.dll` `qrySelectDataTmp` |
| Requisiciones disponibles para compra | `prGarantias.dll` `qryRequisicionDisponible` |
| Elegibilidad para O/C (`APROBADA=1 AND FLAG_EN_OC != 1`) | `GA_IN.APT:51864-51871` (leído directamente) |
| Consumo por bandera al grabar la O/C | `GA_IN.APT:11430-11442` |
| Bug de `FLAG_EN_OC` invertido | `GA_IN.APT:11516-11529` vs `:11436-11442` |
| Carga de renglones a la O/C | `GA_IN.APT:11007-11020` |
| O/C de servicios (`NUM_REQUISICION` informativo) | `GA_CP.APT:22053-22060`, `:23865-23870`, `:24947-24957` |
| Salida por requisición de O/T (`'REQ'`, área `'D'`) | `GA_IN.APT:28733`, `:28745` |
| Costo congelado al costo promedio de la bodega | `GA_IN.APT:28166-28179`, `:28478-28492` |
| Devolución de O/T `'DEM'` (UPDATE, no contra-asiento) | `GA_IN.APT:1018`, `:1110-1139`, `:28750` |
| Motor de kardex legacy (**fuente indirecta**, ver N-10) | `Casajaar_Final/NEWAPP/GA_AD.APT:3621-3777` |
| Bloqueo de existencia configurable (`CNF_CONFIGURACION` 24) | `GA_AD.APT:3682-3691` |
| `ImprimirRequisicion` = reporte 51, para FACTURAS | `GA_IN.APT:4069-4098`, `:3117-3119` |
| Motor de posteo del portal | `SIAD.Services/Almacen/InventarioPostingService.cs` |
| Reversa siempre resta / siempre `"202"` | `InventarioPostingService.cs:410-420`, `:435` |
| Fórmula de la salida (reutilizable tal cual) | `InventarioPostingService.cs:389-394` |
| `uuid` con tipo de documento FIJO (lección de compras) | `InventarioPostingService.cs:472-478` |
| Candado con `company_id` dentro del SQL crudo | `InventarioPostingService.cs:213-231` |
| Patrón cabecera + tabla plana como unidad de posteo (D-3) | `Database/2026-07-31_alm_compra_recepcion.sql:21-33`, `:183-197` |
| Correlativo por empresa | `RecepcionCompraService.cs:791-812` |
| Estado derivado de las cantidades | `RecepcionCompraService.cs:462-466`, `:592-605` |
| Servicios de consulta actuales | `SIAD.Services/Almacen/RequisicionesService.cs:20`, `DescargosService.cs:20` |
| Cifras del histórico y de los pares | mirror `siad_v3_restore` (solo `SELECT`) |

---

> Nada de esto está implementado ni aplicado en BD. El trabajo es **solo local**; el usuario decide cuándo se commitea, se sube o se aplica en el SRV.
