# Entradas y salidas de almacén — análisis del legacy Centura

Fecha de revisión: 2026-08-01
Estado: **análisis**. No hay código ni SQL derivado de este documento todavía.

## Fuentes exactas

| Qué | Archivo | Líneas |
|---|---|---|
| Clase base del kardex | `SIAD_Centura/Casajaar_Final/NEWAPP/GA_ES.APT` | 4144–4168 |
| **Motor de inventario** `clsKardex_Inventario.Grabar` | `SIAD_Centura/Casajaar_Final/NEWAPP/GA_ES.APT` | 4245–4401 |
| Numeración `ObtenerCorrelativoTransaccion` | `SIAD_Centura/Casajaar_Final/NEWAPP/GA_IN.APT` | 1051–1094 |
| Copia divergente del motor (devolución de O/T) | `SIAD_Centura/APP MERENDON/GA_IN.APT` | 1079–1230 |
| Reversión de devolución (`RDP`) | `APP MERENDON/GA_IN.APT` | 1656–1700 |
| Anulación de factura (`CAN`) | `APP MERENDON/GA_IN.APT` | 1878–1970 |
| Entrada por compra (`COM`), dentro de `frmFacturacionPRV` | `APP MERENDON/GA_IN.APT` | 11672, 13876–13990 |
| Salida por facturación (`FAC`) | `APP MERENDON/GA_IN.APT` | 19315–19352 |
| Importaciones (`IME` / `IMS`) | `APP MERENDON/GA_IN.APT` | 22457–22590 |
| Gastos de importación (`GIM`) | `APP MERENDON/GA_IN.APT` | 22731–22795 |
| **Traslado entre bodegas** `dlgTransferBodegas` | `APP MERENDON/GA_IN.APT` | 25207–26200 |
| Cargo de gastos de importación (`CGI`) | `APP MERENDON/GA_IN.APT` | 30958–31130 |

Base de datos: SQL Server (`MERENDON`). **No se consultó**: todo lo de abajo sale del SQL embebido
en el fuente Centura. Las estructuras de tabla están inferidas de los `INSERT` / `UPDATE` / `SELECT`
literales, no del DDL.

---

## 1. Modelo de datos

### `INV_KARDEX` — libro de movimientos

Columnas confirmadas por el `INSERT` de `GA_ES.APT:4310-4320`:

```
COD_BODEGA, COD_PRODUCTO, TIPO_TRANSACCION, FECHA_TRANSACCION,
REFERENCIA1, REFERENCIA2, COSTO, CANTIDAD, USUARIO_CREO, FECHA_CREACION,
SALDO, CORRELATIVO, OBSERVACIONES, VALOR, SALDO_ANTERIOR, VALOR_ANTERIOR,
ULTIMA_TRN, TIPO_TRANSACCION2, REFERENCIA_AFECTA
```

Y por los `UPDATE`: `DIAS_TRN_ANT`, `SUMA_BALANCE`, `ROWID`.

- `CANTIDAD` lleva **signo**: la salida se graba negativa (`GA_ES.APT:4279`).
- `SALDO` / `VALOR` son el snapshot **después** del asiento; `SALDO_ANTERIOR` / `VALOR_ANTERIOR`, antes.
- `TIPO_TRANSACCION2` + `REFERENCIA_AFECTA` son la traza al documento que se está afectando
  (ej.: una nota de crédito que apunta a la factura original).
- No hay `company_id`: una base por empresa.
- No hay clave de idempotencia.

### `INV_EXISTENCIAS` — saldo por (bodega, producto)

Confirmadas por los `UPDATE` de `GA_ES.APT:4322-4400`:

```
COD_BODEGA, COD_PRODUCTO, CANTIDAD_STOCK, SALDO_MONETARIO,
COSTO_ACTUAL, COSTO_ANTERIOR, COSTO_ULTIMO, COSTO_MAS_ALTO,
FECHA_MODIFICACION, USUARIO_MODIFICA
```

Semántica del legacy (importante para no confundir vocabulario):

| Columna | Qué es realmente |
|---|---|
| `SALDO_MONETARIO` | Valor del inventario del par. Se mueve `+= cantidad × costo` |
| `COSTO_ACTUAL` | **El costo promedio** — `SALDO_MONETARIO / CANTIDAD_STOCK` (`GA_ES.APT:4355`) |
| `COSTO_ANTERIOR` | El `COSTO_ACTUAL` previo (se copia antes de recalcular) |
| `COSTO_ULTIMO` | Costo de la última entrada |
| `COSTO_MAS_ALTO` | Pretende ser el máximo histórico. **No lo es** — ver D-9 |

### `INV_TIPOSTRANSACC` — catálogo que gobierna el motor

> ✅ **Verificado contra la BD real el 2026-08-03** (SQL Server, base `MERENDON`). Lo que sigue en
> esta sección se escribió leyendo el fuente y **tenía dos errores**, corregidos abajo. La tabla
> tiene **12 filas** y estas columnas reales: `TIPO_TRANSACCION`, `AREA_AFECTADA`, `ENTRA_SALE`,
> `CORRELATIVO`, `COD_TIPOPARTIDA`, `CUENTA_CONTABLE`, `DEL_SISTEMA`, `COD_DEP_INT`, `NOMBRE`,
> `OBSERVACIONES`, `PDA`, auditoría y `ROWID`.
>
> - ❌ **`CAMBIA_COSTO` NO es columna de esta tabla.** Vive en `INV_TRANSACC_AXL` y **no depende
>   del tipo**, sino de `(AREA_AFECTADA, ENTRA_SALE)`. Su contenido real es uniforme: **toda
>   entrada cambia costo (1), ninguna salida lo cambia (0)**. `INV_TRANSACC_AXL` además tiene
>   `PIDE_COSTO`: 1 en entradas de área `D` y `P`, 0 en área `C` y en todas las salidas.
>   *(La tabla tiene cada combinación duplicada — 12 filas para 6 combinaciones — sin PK; el motor
>   sobrevive porque sólo hace `SqlFetchNext` una vez.)*
> - ✅ **`AREA_AFECTADA` confirmada por contenido**: `C` = Clientes · `P` = Proveedores ·
>   `D` = movimientos internos. Cierra la duda 1 de §8.

Columnas que gobiernan el motor: `TIPO_TRANSACCION`, `AREA_AFECTADA`, `ENTRA_SALE`, `CORRELATIVO`.

Es **la pieza de diseño más valiosa del legacy**: el comportamiento de cada movimiento no está en el
código, está en una fila de tabla. Un tipo nuevo se da de alta sin recompilar.

- `ENTRA_SALE` — `'E'` suma, `'S'` resta (el motor multiplica la cantidad por −1).
- `CAMBIA_COSTO` — booleano: si el movimiento recalcula el costeo o no.
- `AREA_AFECTADA` — discrimina el mismo código de transacción según el subsistema. La PK es
  compuesta `(TIPO_TRANSACCION, AREA_AFECTADA)`.
- `CORRELATIVO` — el consecutivo vive **en la misma fila del catálogo**.

`INV_TRANSACC_AXL` participa del join por `(ENTRA_SALE, AREA_AFECTADA)` pero **no aporta ninguna
columna al SELECT**: funciona como filtro de combinaciones habilitadas (`GA_ES.APT:4276-4283`).

### Otras tablas que condicionan el movimiento

- `INV_PRODUCTOS.REBAJA_INVENTARIO` — si es falso, el producto **no valida existencia** al salir
  (servicios, productos no inventariables).
- `INV_BODEGAS.TIPO_MONEDA` (`'LEMPIRA'` / `'DOLAR'`) y `PERMITE_COMPRAR`. En el traslado,
  el destino se filtra con `PERMITE_COMPRAR <> '1'` (`GA_IN.APT:25277-25280`).
- Configuración `'24'` = *permitir facturar bajo cero*; `'1002'` = mostrar comprobante de despacho
  (`ObtenerDeConfiguracion`).
- `INV_TRANS_NUMSERIE` / `INV_EXIST_NUMSERIE` — control de números de serie, solo activo cuando
  `SqlDatabase = 'TC'` (`GA_IN.APT:25863-25880`).

---

## 2. El motor: `clsKardex_Inventario.Grabar(sAreaAfectada)`

Entradas por variables de instancia: `nCodBodega`, `sCodProducto`, `nCosto`, `nCantidad`
(**siempre positiva**), `sTipoTransaccion`, `sCorrelativo`, `sReferencia1/2`, `sObservaciones`,
`hSqlKardex`. Devuelve `TRUE`/`FALSE`.

Algoritmo literal (`GA_ES.APT:4276-4401`):

1. **Resolver el tipo**: lee `ENTRA_SALE` y `CAMBIA_COSTO` de `INV_TIPOSTRANSACC ⋈ INV_TRANSACC_AXL`
   por `(TIPO_TRANSACCION, AREA_AFECTADA)`.
2. Si `ENTRA_SALE = 'S'` → `nCantidad = nCantidad × (−1)`.
3. **Saldo anterior**: `SELECT SUM(CANTIDAD), SUM(COSTO*CANTIDAD) FROM INV_KARDEX WHERE COD_BODEGA=? AND COD_PRODUCTO=?`
4. `nSaldo = nSaldoAnterior + nCantidad` ; `nValor = nValorAnterior + (nCantidad × nCosto)`
5. **Validación de existencia** — solo si es salida **y** `INV_PRODUCTOS.REBAJA_INVENTARIO`:
   compara contra `INV_EXISTENCIAS.CANTIDAD_STOCK`; si no alcanza y la configuración `'24'` no
   permite bajo cero, mensaje y `FALSE`.
6. `INSERT INTO INV_KARDEX` con los snapshots.
7. `UPDATE INV_EXISTENCIAS SET SALDO_MONETARIO += cantidad×costo, CANTIDAD_STOCK += cantidad`.
8. Si `CAMBIA_COSTO`:
   - lee `COSTO_ULTIMO` en la variable `nCostoMasAlto`, y si `nCosto` es mayor, lo sustituye;
   - `UPDATE INV_EXISTENCIAS SET COSTO_MAS_ALTO=?, COSTO_ULTIMO=nCosto, COSTO_ANTERIOR=COSTO_ACTUAL,`
     `COSTO_ACTUAL = @ABS(SALDO_MONETARIO / CANTIDAD_STOCK)` — con rama alterna `COSTO_ACTUAL = nCosto`
     cuando `nSaldo = 0`.

**Método de costeo: promedio ponderado móvil**, obtenido implícitamente (valor acumulado ÷ cantidad),
no con la fórmula explícita. Las salidas **también** mueven `SALDO_MONETARIO`, al costo que le pase
el llamador — y ahí está el problema principal (D-4).

`Grabar` **no abre ni cierra transacción**: recibe un `Sql Handle` y espera que el llamador haga
`SqlCommit` / `ROLLBACK`.

---

## 3. Catálogo de movimientos

> ⚠️ **La tabla de abajo salió de leer el FUENTE, no la BD.** Verificada contra `MERENDON` el
> 2026-08-03: **9 de esos códigos no existen** ni en el catálogo ni en el kardex
> (`N/C`, `N/D`, `NCP`, `NDP`, `RDP`, `IME`, `IMS`, `GIM`, `CGI`, `REQ`, `DEM`). El defecto **D-3
> nunca llegó a materializarse en datos**. El catálogo real es el de §3.1.

### 3.1 Contenido real de `INV_TIPOSTRANSACC` (12 filas, 2026-08-03)

| Código | Área | E/S | Correlativo | Cuenta legacy | Nombre | Asientos en `INV_KARDEX` |
|---|:--:|:--:|--:|---|---|--:|
| `TFS` | D | S | 000467 | 114305 | SALIDAS POR TRANSFERENCIA | **37.165** |
| `TFE` | D | E | 000467 | 114405 | ENTRADAS POR TRANSFERENCIA | **25.434** |
| `FAC` | C | S | 029707 | 1105010100 | FACTURACION | 24.417 |
| `DPI` | C | E | 000001 | 199100 | DEVOLUCIONES DE PRODUCTO | 11.731 |
| `AIS` | D | S | 000428 | 115010101 | AJUSTE DE INVENTARIO -- SALIDA | 1.667 |
| `AIE` | D | E | 000722 | 115010101 | AJUSTE DE INVENTARIO -- ENTRADA | 942 |
| `COM` | P | E | 000123 | 199100 | COMPRAS A PROVEEDORES | 264 |
| `NPG` | D | S | 000388 | 641135 | PUBLICIDAD | 182 |
| `CAN` | C | E | 002151 | 199100 | CANCELACION DE FACTURACION | 130 |
| `DEP` | C | E | 000001 | 115010101 | DEVOLUCIONES A CLIENTES | 0 |
| `APL` | D | S | 000001 | 1100501 | APLICACION DE REQUISICIONES | 0 |
| `TTR` | P | E | 000001 | *(null)* | DEVOLUCIONES A PROVEEDORES | 0 |

**El traslado entre bodegas es, con diferencia, lo más usado del módulo**: 62.599 asientos entre
`TFE` y `TFS`, más que la facturación. Está fuera de la primera entrega de SIAD (Fase 5).

Estos 12 se importaron a `alm_tipo_movimiento` el 2026-08-03
(`Database/2026-08-01_alm_tipo_movimiento.sql`), con los 4 internos activos y los 8 restantes
inactivos.

### 3.2 Lo que el fuente postea (análisis original, parcialmente desmentido)

Los tipos que el fuente de Merendón realmente postea:

| Código | Qué es | Área | Dónde |
|---|---|---|---|
| `COM` | Compra / recepción de proveedor | `P` | `GA_IN.APT:13879-13930` |
| `FAC` | Salida por facturación | `C` | `GA_IN.APT:19341` |
| `CAN` | Anulación de factura | `C` | `GA_IN.APT:1928` |
| `N/C`, `N/D` | Nota de crédito / débito a cliente | `C` | `GA_IN.APT` |
| `NCP`, `NDP` | Nota de crédito / débito a proveedor | `P` | `GA_IN.APT` |
| `DPI` | Devolución de producto a inventario | `C` | `GA_IN.APT:1634, 3007` |
| `RDP` | Reversión de devolución | `C` | `GA_IN.APT:1685` |
| `TFE` / `TFS` | **Traslado entre bodegas: entrada / salida** | `D` | `GA_IN.APT:26137, 26164` |
| `IME` / `IMS` | Importación: entrada local / salida internacional | `D` | `GA_IN.APT:22543, 22574` |
| `GIM` | Gastos de importación | `P` | `GA_IN.APT:22786` |
| `CGI` | Cargo de gastos de importación | `P` | `GA_IN.APT:31119` |
| `REQ` | Requisición | — | `GA_IN.APT` |
| `DEM` | Materiales de orden de trabajo | `D` | `GA_IN.APT:37535` — **comentado, inactivo** |

`AREA_AFECTADA` observada: `'C'`, `'P'`, `'D'`. El significado literal **no consta en el fuente**;
por los flujos donde aparece cada una se infiere Clientes / Proveedores / movimientos internos
(traslados, importaciones, órdenes de trabajo). **Confirmar contra la tabla real antes de usarlo.**

---

## 4. Flujos

### 4.1 Traslado entre bodegas — `dlgTransferBodegas`

El único documento del legacy que es *puramente* movimiento de almacén. Vale la pena en detalle
porque es lo que SIAD no tiene.

Pantalla (`GA_IN.APT:25207-25900`): radio Local/Internacional (filtra bodegas por `TIPO_MONEDA`),
combo bodega origen, combo bodega destino (excluye `PERMITE_COMPRAR = '1'`), grilla de productos
(`tblProductosTRF`: código, nombre, cantidad, costo), comentario, comprobante de despacho.

Posteo (`pb3`, `GA_IN.APT:25870-25950`):

1. Valida ambos combos.
2. Toma **dos** correlativos: `TFE` y `TFS`.
3. Recorre la grilla. Por cada renglón:
   - descarta renglones vacíos o marcados como borrados;
   - lee `CANTIDAD_STOCK` del origen y, si no alcanza y no está permitido bajo cero, aborta con foco
     en la celda;
   - `ActKardexExistenciasTFE()` → entrada en destino (`Grabar('D')`);
   - `ActKardexExistenciasTFS()` → salida en origen (`Grabar('D')`);
   - reasigna los números de serie a la bodega destino.
   - si cualquiera de los dos falla → `ROLLBACK` y `FALSE`.
4. `SqlCommit` **al final del recorrido**, mensaje "Transacción posteada", y deshabilita el botón.

**El costo viaja con la mercadería**: ambos asientos usan el mismo `colCostoX` de la grilla, así que
el valor que sale del origen es el que entra al destino. Conceptualmente correcto.

Dos observaciones de diseño: no hay tabla de documento de traslado —el traslado *son* los dos
asientos del kardex unidos por nada más que el comentario y el correlativo—, y no existe traslado en
tránsito: la mercadería sale y entra en el mismo instante.

### 4.1b `dlgTransaccionesGenericasINV` — LA pantalla de entradas y salidas manuales

> **Añadido 2026-08-03.** El análisis original **no la encontró** y llegó a afirmar que no existía.
> Es el equivalente directo de lo que se está migrando. Declarada en
> `Casajaar_Final/NEWAPP/GA_IN.APT:14142`; en `APP ZIP/GA_IN.APT` y `APP MERENDON/GA_IN.APT` sólo
> se **invoca** (línea 2703 / 2728), por eso no aparecía buscando ahí.

Menú **Inventario → «Transacciones genéricas»**, `Ctrl+G`, protegida por `arrSeguridad[3]`.
Título: *Transacciones Genéricas*. Modal, 11,05" × 4,354".

Su propia descripción en el fuente fija la regla de referencias:

| `AREA_AFECTADA` | `Referencia1` | `Referencia2` |
|:--:|---|---|
| `C` | `NUM_FACTURA` | `COD_CLIENTE` |
| `P` | `NUM_FACT_EXT` | `COD_PROVEEDOR` |
| `D` | `#DOCUMENTO` | *(vacío)* |

> *"Para cada transacción de INVENTARIO deberá haber una transacción relacionada en el área
> respectiva. Excepto para Departamento Interno."*

**Controles y comportamiento**

- `cmbTransaccionesINVQ` — combo de tipos. **No lista el catálogo completo**: lo filtra por
  usuario contra `AXL_USUARIOS_TRN` (ver §4.1c).
- Al elegir tipo (`SAM_Click`) lee `AREA_AFECTADA`, `COD_DEP_INT`, `ENTRA_SALE` y `PIDE_COSTO`, y
  reconfigura la pantalla:
  - `PIDE_COSTO` verdadero → muestra y habilita `tblProductosGen.colKeyCosto`; falso → la oculta y
    deshabilita. **Es la regla de captura del costo**, y coincide con la de SIAD: el costo sólo se
    teclea en entradas; las salidas se valorizan al promedio.
  - Área `C` → etiquetas «Num. Factura» / «Cod. Cliente», muestra `cbTipoFactura`, ref2 **deshabilitada**.
  - Área `P` → «Num. Fact.» / «Cod. Proveedor», ref2 **habilitada**, oculta `cbTipoFactura`.
  - Área `D` → «#documento», oculta ref2 y `cbTipoFactura`.
- `cmbBodega` — bodegas filtradas por `TIPO_MONEDA`.
- `rbLocal` / `rbInternacional` — conmutan ese filtro entre `'LEMPIRA'` y `'DOLAR'`. `rbLocal` es
  el predeterminado. **Sin equivalente en SIAD**: `alm_bodega` no tiene moneda.
- `tblProductosGen` — grilla de renglones (código, cantidad, costo condicional).

### 4.1c `AXL_USUARIOS_TRN` — permisos por usuario × tipo de transacción

```sql
SELECT ALL NOMBRE FROM INV_TIPOSTRANSACC, AXL_USUARIOS_TRN
WHERE INV_TIPOSTRANSACC.TIPO_TRANSACCION = AXL_USUARIOS_TRN.TIPO_TRANSACCION
  AND AXL_USUARIOS_TRN.MAESTRO = 'I' AND AXL_USUARIOS_TRN.USUARIO = SYSTEM_USER
```

`MAESTRO` discrimina el subsistema (`'I'` inventario, y por el mismo patrón `C`/`P`/`B`).
Contenido real para `'I'` (2026-08-03) — **sólo tipos de área `D`**:

| Tipo | Usuarios |
|---|---|
| `AIS` | ADMIN, CLIENTES, DESPT, INVENT, INVPROD, PRODCT, PRODT |
| `AIE` | ADMIN, CLIENTES, DESPT, INVENT, INVPROD, PRODT |
| `APL` | ADMIN, COMPRAS |
| `TFE`, `TFS` | ADMIN, CLIENTES |

Dos observaciones: `NPG` **no está concedido a nadie** y aun así tiene 182 asientos (se postea
fuera de esta pantalla), y los usuarios modernos con correo
(`caja@industriasmerendon.com`, `bodega.choloma@…`, `liquidacion@industriaspinguino.com`) **no
figuran** en la tabla — la matriz quedó desactualizada frente a la operación real.

**Decisión de migración (usuario, 2026-08-03):** SIAD **no porta esta matriz**. Se queda con
`alm_tipo_movimiento.requiere_autorizacion` (booleano) + un permiso global
`module.inventario.movimientos.autorizar_sensibles`. Grano más grueso, pero la evidencia sugiere
que la matriz se heredó y dejó de mantenerse.

### 4.2 Salida por facturación

`ActKardexExistenciasINV` (`GA_IN.APT:19315-19352`) recorre la grilla de la factura y postea `FAC`
por cada renglón con `colRebajaInventario = '1'` y cantidad > 0. El costo que manda es
`tblFactura.colCostoActual` — **el promedio leído en la grilla cuando se capturó la línea**, no el
vigente al postear.

### 4.3 Entrada por compra

`frmFacturacionPRV` (`GA_IN.APT:11672`) postea `COM` con área `'P'` (`:13910`), tomando correlativos
separados para inventario (`'I'`) y proveedores (`'P'`) (`:14144`, `:14201`). Este flujo ya está
documentado aparte en [`README_compras_recepcion_proveedor.md`](README_compras_recepcion_proveedor.md).

### 4.4 Numeración

`ObtenerCorrelativoTransaccion(sMaestro, sTipoTransaccion, hSql, sCorrelativoRet)`
(`Casajaar_Final/NEWAPP/GA_IN.APT:1051-1094`):

1. Elige la tabla por maestro: `'C'`→`CLN_TIPOSTRANSACC`, `'P'`→`PRV_TIPOSTRANSACC`,
   `'B'`→`BNC_TIPOSTRANSACC`, `'I'`→`INV_TIPOSTRANSACC`.
2. **`UPDATE ... SET CORRELATIVO = CORRELATIVO WHERE TIPO_TRANSACCION = ?`** — un no-op cuyo único
   propósito es tomar el lock de la fila.
3. `SELECT CORRELATIVO`, +1, rellena con ceros a 6 posiciones, `UPDATE`.
4. Devuelve el valor **anterior** al incremento.

La intención (bloqueo pesimista antes de leer-modificar-escribir) es correcta. Depende por completo
de que el llamador tenga una transacción abierta en ese mismo handle.

---

## 5. Defectos verificados

Cada uno leído en el fuente, no supuesto.

| # | Defecto | Evidencia | Consecuencia |
|---|---|---|---|
| **D-1** | **El kardex se reescribe.** `ModificarInvPorDevolucionOT` hace `UPDATE INV_KARDEX SET CANTIDAD = CANTIDAD + ? - ?, SALDO=?, VALOR=?, CORRELATIVO=? WHERE COD_BODEGA=? AND COD_PRODUCTO=? AND TIPO_TRANSACCION=?` — **sin correlativo, sin fecha, sin ROWID** | `APP MERENDON/GA_IN.APT:1174-1186` | Actualiza **todas** las filas históricas de ese producto/bodega/tipo. La historia deja de ser auditable |
| **D-2** | El `SELECT ROWID ... WHERE COD_BODEGA=? AND COD_PRODUCTO=?` no lleva `ORDER BY` ni `TOP 1` | `GA_IN.APT:1188-1191` | Toma una fila arbitraria y le pisa `ULTIMA_TRN`, `DIAS_TRN_ANT`, `SUMA_BALANCE` |
| **D-3** | **No se valida `nFetch`** tras leer el catálogo de tipos | `GA_ES.APT:4284` | Si el tipo no existe en `INV_TIPOSTRANSACC`, `sEntraSale` queda vacío → **el movimiento se postea como ENTRADA**. Un error de configuración infla el inventario en silencio |
| **D-4** | **Las salidas mueven `SALDO_MONETARIO` al costo que manda el llamador**, no al promedio vigente | `GA_ES.APT:4322-4327` + `GA_IN.APT:19343` (`colCostoActual` de la grilla) | El promedio se corrompe: entre capturar la línea y postearla puede haber entrado una compra. El valor del inventario deja de cuadrar con la cantidad |
| **D-5** | **Sin control de concurrencia.** El saldo se lee con `SUM` y se graba en el asiento, sin bloqueo | `GA_ES.APT:4287-4292` | Dos usuarios facturando el mismo producto graban snapshots `SALDO`/`VALOR` mutuamente inconsistentes. `CANTIDAD_STOCK` sobrevive (el `+=` es atómico); **el libro miente** |
| **D-6** | **Dos fuentes de verdad.** El saldo anterior sale del kardex (`SUM`); la existencia se lleva incremental en `INV_EXISTENCIAS` | `GA_ES.APT:4287` vs `:4322` | Divergen en silencio. Nada las reconcilia |
| **D-7** | `SUM` sobre todo el kardex del par **en cada movimiento** | `GA_ES.APT:4287-4292` | O(n) por asiento: se degrada con los años. El `ORDER BY FECHA_TRANSACCION DESC` sobre un agregado sin `GROUP BY` es ruido |
| **D-8** | `COSTO_ACTUAL = @ABS(SALDO_MONETARIO / CANTIDAD_STOCK)` | `GA_ES.APT:4355` | El `@ABS` **enmascara** un saldo monetario negativo y devuelve un costo positivo a partir de un valor negativo |
| **D-9** | `COSTO_MAS_ALTO` se siembra leyendo `COSTO_ULTIMO`, no el máximo histórico | `GA_ES.APT:4344-4351` | No es el costo más alto. Es "el mayor entre el último y el actual" |
| **D-10** | División por cero latente: la rama del promedio decide con `nSaldo` (del kardex) pero divide por `CANTIDAD_STOCK` (de existencias) | `GA_ES.APT:4353-4355` vs `:4287` | Si D-6 ya los separó, divide por cero |
| **D-11** | **Transaccionalidad por convención.** `Grabar` no abre transacción; el traslado hace `SqlCommit(hSqlEliminar)` **intermedios** sobre otro handle dentro del bucle que luego puede hacer `ROLLBACK` sobre `hSqlModificar` | `GA_IN.APT:25890-25910` | Un fallo a media lista deja mercadería movida a medias entre bodegas |
| **D-12** | **Lógica del motor duplicada y divergente**: `ModificarInvPorDevolucionOT` es una copia de `Grabar` con `CASE WHEN` en vez de `@IF` y un `UPDATE` en vez de un `INSERT` | `GA_IN.APT:1079-1230` | Toda corrección hay que hacerla dos veces. Ya divergieron |
| **D-13** | La validación de existencia se salta entera si `REBAJA_INVENTARIO` es falso, y aun cuando aplica se puede desactivar globalmente con la configuración `'24'` | `GA_ES.APT:4296-4308` | Existencias negativas por diseño |
| **D-14** | El traslado no valida que origen ≠ destino, ni que la bodega destino esté activa | `GA_IN.APT:25870-25890` | Un traslado a la misma bodega genera dos asientos que se cancelan y ensucian el kardex |

---

## 6. Qué hacer distinto (comparación con el portal SIAD)

### Lo que SIAD ya resolvió mejor y no se debe tocar

| Defecto Centura | Cómo está resuelto hoy |
|---|---|
| D-1, D-2 (reescritura del libro) | Kardex **inmutable** por trigger (`trg_alm_kardex_inmutable`, SQLSTATE K0001). Se corrige con reversa |
| D-5 (concurrencia) | `SELECT ... FOR UPDATE` sobre `alm_articulo_bodega` con `company_id` dentro del SQL crudo (`InventarioPostingService.cs:232-243`) |
| D-7 (SUM por movimiento) | Saldo materializado en `alm_articulo_bodega`, reconstruible desde el kardex |
| D-11 (transaccionalidad) | Una transacción, un solo `SaveChanges` (`InventarioPostingService`) |
| D-12 (motor duplicado) | Un **único** punto de escritura: `IInventarioPostingService` |
| Reintentos | Idempotencia por UUIDv5 determinista + único `(company_id, uuid)` |
| — | Multi-empresa real (`company_id` en todo) |

### Lo que Centura tiene y SIAD **no**

1. **Catálogo parametrizable de tipos de movimiento.** En SIAD el vocabulario es un `enum` compilado
   (`TipoMovimientoInventario`, 8 valores) más un `CHECK` en la base
   (`TipoDocumentoInventario`, 7 valores). Agregar "merma", "donación", "consumo interno",
   "devolución de cliente" o "producción" hoy exige **recompilar y migrar el CHECK**. Centura lo
   resuelve con un `INSERT` en `INV_TIPOSTRANSACC` desde 1999.
2. **Traslado entre bodegas.** No existe en SIAD. `TipoDocumentoInventario.Traslado` y
   `alm_kardex.bodega_destino_id` están declarados y **sin productor**.
3. **Documento multi-línea de movimiento.** `alm_ajuste_inventario` es una cabecera plana de **una
   sola línea** (así lo documenta la propia entidad), sin numeración, con `motivo` de texto libre.
   En Centura todo movimiento se captura como documento con grilla y se postea completo o nada.
4. **Numeración por tipo de movimiento.** SIAD tiene correlativos para compra y orden de compra
   (`alm_compra_correlativo`, `alm_orden_compra_correlativo`); no para movimientos de almacén.

### El hueco más grande: hoy no hay ninguna salida operativa

Verificado: los únicos servicios que invocan el motor son `AjusteInventarioService`,
`ArticuloUbicacionService`, `CargaInicialInventarioService` y `RecepcionCompraService`.
`DescargosService` y `RequisicionesService` son **solo consulta**. `SalidaDescargo` existe en el
enum y únicamente lo ejercitan los tests.

En la práctica: hoy el único modo de sacar mercadería del almacén en el portal es un
`AjusteNegativo` de una línea, capturado desde la pestaña Ubicaciones de la ficha del artículo.

---

## 7. Propuesta

**Idea central: adoptar el acierto de Centura (comportamiento en datos, no en código) sin heredar
ninguno de sus 14 defectos.** Dos niveles bien separados:

- **Clase de movimiento** — `enum` cerrado, es la semántica que el motor sabe ejecutar
  (entrada, salida, ajuste de valor, reversa, traslado). Solo cambia con código y tests.
- **Tipo de movimiento** — catálogo en base (`alm_tipo_movimiento`), abierto y configurable:
  código, nombre, clase a la que pertenece, si afecta el costo, si exige autorización, cuenta
  contable, correlativo. Es el equivalente sano de `INV_TIPOSTRANSACC`.

Así, "merma por vencimiento" y "donación" son dos filas de catálogo con distinta cuenta contable
que el motor ejecuta con el mismo camino probado de salida.

Sobre eso, un **documento único de movimiento de almacén** (`alm_movimiento_hdr` / `alm_movimiento_dtl`)
con estados borrador → aplicado → anulado, posteo atómico de todas las líneas y anulación por reversa
—nunca por `UPDATE`—, que cubre entrada, salida y traslado con una sola pantalla y un solo servicio.

Decisiones abiertas antes de diseñar: alcance de la primera entrega, si el traslado es inmediato o
con tránsito, si los movimientos requieren autorización, y qué hacer con `alm_ajuste_inventario`
(que quedaría absorbido por el documento nuevo).

---

## 8. Dudas — resueltas el 2026-08-03 contra la BD `MERENDON`

1. ✅ **`AREA_AFECTADA`**: `C` = Clientes · `P` = Proveedores · `D` = interno. Confirmado por el
   contenido de la tabla y por el comportamiento de `dlgTransaccionesGenericasINV` (§4.1b).
2. ✅ **Contenido real del catálogo**: 12 filas, listadas en §3.1. Los 17 del análisis por fuente
   eran incorrectos: 9 códigos no existen. **`CAMBIA_COSTO` no es por tipo** (ver §1).
3. ✅ **`INV_TRANSACC_AXL`**: es tabla de atributos por `(AREA_AFECTADA, ENTRA_SALE)` con
   `CAMBIA_COSTO` y `PIDE_COSTO`. No es catálogo de combinaciones válidas. Sin PK y con cada
   combinación duplicada.
4. ⚠️ **`DEM`** sigue sin resolverse, pero pierde relevancia: **no existe en el catálogo ni tiene
   asientos**. Estaba comentado en el fuente y nunca entró en producción.

### Duda nueva (abierta)

5. **¿Desde dónde se postea `NPG`?** Tiene 182 asientos hasta 2026-03-16, hechos por 4 usuarios
   con correo, pero **no está concedido en `AXL_USUARIOS_TRN`** — así que
   `dlgTransaccionesGenericasINV` no pudo haberlos generado. Hay un camino de posteo no
   identificado. No bloquea la migración, pero conviene saberlo antes de dar el módulo por completo.
