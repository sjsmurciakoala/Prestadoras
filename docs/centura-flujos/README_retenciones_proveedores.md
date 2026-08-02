# Flujo de Retenciones a Proveedores — APP MERENDON (Centura)

**Archivo fuente principal:** `SIAD_Centura/APP MERENDON/GA_CP.APT` (SQLWindows/Team Developer, codificación Windows-1252, motor SQL Server)
**Fecha de revisión:** 2026-07-21
**Diálogos analizados:** `dlgAplicacionRetencion` (25438–26607), `dlgConsultaRetenciones` (26608–26998), `dlgPagoDeCXP` (3234–6081), `dlgEmisionCheques` (11020–14139), `dlgReversionAnular` (26999–27942), `dlgGstos` (6082–6781), `dlgOCServicios` (6782–11019); además `GA_IN.APT` (frmFacturacionPRV 11672, frmOCMercaderias 6051), `GA_AD.APT` (menús de mantenimiento) y `prSiad.XML`.
**Documento hermano:** el flujo general de cheques/pagos está documentado en [`Database/ddl_v3/# Flujo de Cheques — GA_CP.md`](../../Database/ddl_v3/); este README cubre **solo** la retención y sus puntos de contacto.

> **Regla de lectura del fuente:** en el formato outline de SQLWindows una línea que inicia con `!` es comentario y **sus líneas hijas (más indentadas) son código muerto**. Todos los bloques marcados abajo como "muerto"/"comentado" siguen esta regla (verificada por profundidad de tabulación).

---

## Tabla de contenido

1. [Resumen ejecutivo](#1-resumen-ejecutivo)
2. [Catálogo AXL_RETENCIONES](#2-catálogo-axl_retenciones)
3. [Correlativo fiscal AXL_CORRELATIVOS_DEI](#3-correlativo-fiscal-axl_correlativos_dei)
4. [dlgAplicacionRetencion — el diálogo de retención](#4-dlgaplicacionretencion--el-diálogo-de-retención)
5. [Invocador 1: dlgPagoDeCXP (pago de CxP)](#5-invocador-1-dlgpagodecxp-pago-de-cxp)
6. [Invocador 2: dlgEmisionCheques (cheques manuales)](#6-invocador-2-dlgemisioncheques-cheques-manuales)
7. [Documentos origen](#7-documentos-origen)
8. [Consulta y reimpresión](#8-consulta-y-reimpresión)
9. [Reversión / anulación](#9-reversión--anulación)
10. [Impresión — reporte 433](#10-impresión--reporte-433)
11. [Claves de configuración usadas](#11-claves-de-configuración-usadas)
12. [Defectos y particularidades del código legado](#12-defectos-y-particularidades-del-código-legado)
13. [Matriz de dependencias de BD](#13-matriz-de-dependencias-de-bd)
14. [Mapeo Centura → Prestadoras (SIAD Blazor)](#14-mapeo-centura--prestadoras-siad-blazor)
15. [Supuestos confirmados y dudas pendientes](#15-supuestos-confirmados-y-dudas-pendientes)
16. [Trazabilidad](#16-trazabilidad)

---

## 1. Resumen ejecutivo

Cuando Merendon paga a un proveedor (facturas de compra u otras obligaciones), el usuario puede aplicar una **retención fiscal** antes de emitir el cheque. El subproceso:

1. Toma la **base imponible** del documento (si el documento "calcula impuesto", le quita el ISV dividiendo entre la config `'13'`).
2. Permite cargar **una o varias líneas de retención** desde el catálogo `AXL_RETENCIONES` (cada una con % y cuenta contable propia); el monto retenido por línea = `base × % / 100`.
3. Al guardar, en una sola operación:
   - Emite una **constancia de retención** con numeración fiscal (`AXL_CORRELATIVOS_DEI`, `COD_DEI_DOC = 2`) y **dos CAI**: el del talonario propio (columna `CAI`) y el del documento del proveedor (`CAI_PROVEEDOR`) → `PRV_RETENCIONES_HDR` + `PRV_RETENCIONES_DTL`.
   - Avanza el correlativo fiscal (+1 conservando ceros a la izquierda).
   - Registra en el **kardex del proveedor** un movimiento **negativo** por cada línea (la retención reduce la deuda) y actualiza saldos en `PRV_PROVEEDORES`.
   - Genera **una partida contable por línea** (tipo 15): DEBE cuenta del proveedor / HABER cuenta del catálogo de retención (pasivo por pagar al fisco).
   - **Rebaja el documento origen** (`GST_GASTOS_VARIOS.VALOR` para tipo 1; `PRV_DESGLOSE_PAGO.VALOR_A_PAGAR` para tipo 2).
4. Imprime la constancia (reporte **433**) y devuelve al llamador el total retenido (`nMontoRetencion`) para que **el cheque salga por el neto**.
5. Hay pantalla de **consulta y reimpresión**; **no existe** reversión ni anulación de retenciones.

**Tipos de retención (parámetro `nTipoRetencion`):**

| Tipo | Documento origen | Rebaja | Asignado en |
|---|---|---|---|
| `1` | Otras obligaciones (`GST_GASTOS_VARIOS`, origen `OC_SERVICIOS_HDR`) | `GST_GASTOS_VARIOS.VALOR` | GA_CP.APT:3852 |
| `2` | Facturas de compra (`PRV_FACTURAS_HDR` + `PRV_DESGLOSE_PAGO`) | `PRV_DESGLOSE_PAGO.VALOR_A_PAGAR` | GA_CP.APT:3664 (y fijo `2` en dlgEmisionCheques:13183) |

---

## 2. Catálogo AXL_RETENCIONES

Columnas evidenciadas por consumo (no hay DDL en el repo):

| Columna | Uso |
|---|---|
| `COD_RETENCION` | Código; combo del grid (GA_CP.APT:25855) |
| `NOMBRE` | Descripción; combo por nombre (25908) |
| `PORCENTAJE` | % a aplicar sobre la base (25860–25864) |
| `TIPO_TRANSACCION` | Tipo de movimiento de kardex del proveedor que genera la retención (25864, usado en `GetTransactionNextID('P', …)` 26492) |
| `CUENTA_CONTABLE` | Cuenta del HABER (pasivo retención por pagar), leída en `GuardarDetallePartida` (26413) |

**Mantenimiento del catálogo: no está en los fuentes del repo.** Existe el ítem de menú `GA_AD.APT:1587–1595` ("Tabla Retenciones" → `SalModalDialog(dlgRetenciones, hWndForm)`) pero `dlgRetenciones` vive en una librería `.apl` ausente. En .NET, `prSiad.XML` documenta `AXL_RETENCIONESDataTable`/`TableAdapter` (dtsContabilidad) sin fuente disponible. No hay ningún INSERT/UPDATE/DELETE sobre `AXL_RETENCIONES` en los `.APT`.

---

## 3. Correlativo fiscal AXL_CORRELATIVOS_DEI

Columnas evidenciadas: `COD_CORRELATIVO_DEI` (PK), `COD_DEI_DOC`, `NUM_FACTDEI_ACTUAL`, `NUM_FACTDEI_FINAL`, `CAI`, `FECHA_FINAL` (+ `NOMBRE` vía `VIEW_CORRELATIVOS_DEI`).

**Valores conocidos de `COD_DEI_DOC`:** `1` = facturas (GA_IN), `2` = **constancia de retención a proveedores**, `13` = usado por `frmFacturacion` (significado no documentado en código). El catálogo de tipos (`AXL_DEI_DOC`) solo existe como dataset .NET.

**Lectura del correlativo al abrir el diálogo** (`dfNoRetencion · On SAM_Create`, GA_CP.APT:26158–26169, versión activa):

```sql
SELECT NUM_FACTDEI_ACTUAL,
       CAI,
       CONVERT(INT,NUM_FACTDEI_FINAL) - CONVERT(INT,NUM_FACTDEI_ACTUAL), COD_CORRELATIVO_DEI
FROM AXL_CORRELATIVOS_DEI
WHERE COD_DEI_DOC = 2  AND CAST(GETDATE() AS DATE) <= FECHA_FINAL AND NUM_FACTDEI_ACTUAL <= NUM_FACTDEI_FINAL
INTO :sNoRetencion, :sNumCAIDEI, :nNumeroRetencionRestanteDEI, :nCorrelativoRetencion
```

- Si no hay correlativo vigente → `"NO EXISTE CORRELATIVO...!"` (el diálogo NO se cierra; el guardado fallará después).
- Si restantes `<` config **'104'** → alerta *"Tiene pocos correlativos restantes de la DEI! Asegurese de pedir mas para poder hacer Retenciones."*
- La versión comentada (26147–26157) no filtraba vigencia y usaba `<=` en el umbral.

**Avance del correlativo** (dentro de `RETENCIONHDR`, 26382–26385): `+1` conservando el padding de ceros:

```sql
UPDATE AXL_CORRELATIVOS_DEI
SET NUM_FACTDEI_ACTUAL = REPLICATE('0', LEN(NUM_FACTDEI_ACTUAL) - LEN(CONVERT(INT, NUM_FACTDEI_ACTUAL) +1)) + CONVERT(VARCHAR, CONVERT(INT, NUM_FACTDEI_ACTUAL) +1)
FROM AXL_CORRELATIVOS_DEI
WHERE COD_CORRELATIVO_DEI = :nCorrelativoRetencion
```

El mantenimiento de correlativos es un formulario .NET (`wfrCreateEditCorrelativos` / `wfrListadoCorrelativos` en prSiad.dll, invocado desde GA_AD.APT:1803–1811 vía `showConfiguracionCorrelativos()`); fuente no disponible.

---

## 4. dlgAplicacionRetencion — el diálogo de retención

### 4.1 Contrato (Window Parameters, 26536–26542)

```
String:  sCodProveedores      -- código del proveedor
String:  sDocReferencia       -- clave del documento (colPk_Docto / dfNumDocumento)
Number:  ValorRetencion       -- valor sobre el que se calculará la base
Receive Number: nMontoRetencion  -- SALIDA: total retenido (0 si no se guardó)
Number:  nTipoRetencion       -- 1 = servicios/obligaciones, 2 = facturas de compra
Date/Time: dtFechaEmision     -- fecha (los llamadores pasan SYSTEM_DATE o dfFechaEmision)
```

### 4.2 Apertura (`On SAM_Create`, 26571–26607)

1. Sin proveedor → cierra de inmediato.
2. Carga proveedor: `SELECT COD_PROVEEDOR, NOMBRE, CUENTA_CONTABLE FROM PRV_PROVEEDORES WHERE COD_PROVEEDOR = :sCodProveedores` → `dfCodProveedor`, `dfNombreProveedor`, **`sCuentaProveedor`** (cuenta del DEBE).
3. `bGuardado = TRUE` (semántica **invertida**: TRUE = "pendiente de guardar"), `nCodEmpresa = 1`, deshabilita Guardar/Imprimir.
4. Resuelve el documento origen:
   - **Tipo 1:** `select sc.REFERENCIA, sc.FECHA_FACTURA_PROVEEDOR, FLAG_CALCULA_IMPUESTO, CAI_PROVEEDOR from OC_SERVICIOS_HDR sc where sc.NUM_OC_SERVICIO + '-' + sc.REFERENCIA = :sDocReferencia` → `dfDocRetencion`, `dtFechaEmision`, `bCalculaSubTotal`, `dfCai`.
   - **Tipo 2:** `SELECT NUM_FACTURA_EXTC, FECHA_FACTURA_PROVEEDOR, FLAG_CALCULA_IMPUESTO, CAI FROM PRV_FACTURAS_HDR INNER JOIN OC_ORDENCOMP_HDR ON OC_ORDENCOMP_HDR.NUM_ORDEN_COMPRA = PRV_FACTURAS_HDR.NUM_ORDEN_COMPRA WHERE NUM_FACTURA_PROV = :sDocReferencia` → ídem.
   - Otro tipo: `dfDocRetencion = sDocReferencia` y `bCalculaSubTotal = TRUE` forzado.
5. **Base imponible** (26604–26607, se ejecuta siempre al final):

```
If bCalculaSubTotal
    Set dfMontoRetencion = ValorRetencion / SalStrToNumber( ObtenerDeConfiguracion( "13" ) )
Else
    Set dfMontoRetencion = ValorRetencion
```

Es decir: si el documento marca `FLAG_CALCULA_IMPUESTO`, la base = valor **÷ config '13'** (quitar el ISV); si no, el valor tal cual. Ver §11 y §15 sobre la inconsistencia divisor-vs-porcentaje.

### 4.3 Captura (grid `tblRetenciones`)

- `dfMontoRetencion` es el **único campo editable** del encabezado (la base puede ajustarse a mano).
- `pbIns` inserta fila y copia `dfMontoRetencion` a `colMonto` (base por fila). Validación con defecto: `If dfMontoRetencion < 0 and dfMontoRetencion = NUMBER_Null` (`and` — condición imposible, nunca alerta).
- `colCodRetencion` (combo desde `AXL_RETENCIONES`) al validar trae `NOMBRE, PORCENTAJE, TIPO_TRANSACCION` y calcula:
  - `colRetencion = colMonto × (colPorcentaje / 100)`
  - `dfTotal = SUM(colRetencion)` (columna 4) → habilita `pbGuardar` solo si `dfTotal > 0`.
- `colNombreRetencion` permite escoger por nombre (resuelve el código vía `FluObtenersDeAuxiliar` y revalida).
- `colTipoTransaccion` es columna técnica con el título en blanco (visible y editable — `Visible? Yes` 26020, `Editable? Yes` 26021 — pero sin encabezado; el usuario no la percibe como dato): recibe el tipo de kardex del catálogo.
- `pbDel` elimina fila y recalcula.

### 4.4 Guardado (`pbGuardar · On SAM_Click`, 26196–26254)

**El primer bloque (26197–26218) es código muerto** (hijo de `! If ActualizarPRV()`). El flujo vivo (26219–26254):

1. **`RETENCIONHDR()`** (26372–26390) — inserta la constancia y avanza el correlativo:

```sql
INSERT INTO PRV_RETENCIONES_HDR (CORRELATIVO_RETENCION_DEI, COD_PROVEEDOR, NOMBRE, REFERENCIA,
  TOTAL_RETENCION, FECHA_CREACION, USUARIO_CREO, VALOR, CAI, FECHA_EMISION, CAI_PROVEEDOR)
VALUES (:dfNoRetencion, :dfCodProveedor, :dfNombreProveedor, :dfDocRetencion, :dfTotal,
  :SYSTEM_DATE, :SYSTEM_USER, :dfMontoRetencion, :dlgAplicacionRetencion.sNumCAIDEI, :dtFechaEmision, :dfCai)
```

   Mapeo clave: `TOTAL_RETENCION` = total retenido, `VALOR` = **base imponible**, `CAI` = CAI del talonario propio, `CAI_PROVEEDOR` = CAI del documento del proveedor. Luego el UPDATE de avance (§3).

2. **`RETENCIONDTL()`** (26391–26405) — una fila por línea del grid:

```sql
INSERT INTO PRV_RETENCIONES_DTL (CORRELATIVO_RETENCION_DEI, COD_RETENCION, NOMBRE, MONTO_RETENCION, PORCENTAJE)
VALUES (:dfNoRetencion, :colCodRetencion, :colNombreRetencion, :colRetencion, :colPorcentaje)
```

   (`MONTO_RETENCION` = monto retenido calculado, no la base.)

3. **Loop por cada fila del grid:**
   - Si `nTipoRetencion = 1 or 2` → **`ActualizarPRV()`** (26478–26535), kardex del proveedor con **monto negativo**:
     - `nMonto = -colRetencion`; correlativo con `GetTransactionNextID('P', colTipoTransaccion, sCorrelativo)` (DLL `libdbutils` → SP `spGetTransactionNextID`).
     - Saldo anterior = `SELECT SUM(MONTO), SUM(MONTO_DOLARES) FROM PRV_KARDEX WHERE COD_PROVEEDOR = …`.
     - `UPDATE PRV_PROVEEDORES SET SALDO_ANTERIOR=SALDO_ACTUAL, SALDO_ACTUAL=SALDO_ACTUAL + :nMonto, SALDO_ANT_DOLARES=SALDO_ACT_DOLARES, SALDO_ACT_DOLARES=SALDO_ACT_DOLARES + :nMontoDolares …`.
     - `INSERT INTO PRV_KARDEX (…)` con `TIPO_TRANSACCION = TIPO_TRANSACCION2 = colTipoTransaccion`, `REFERENCIA1 = REFERENCIA2 = REFERENCIA_AFECTA = dfNumDocumento`, `NUM_CHEQUE = NULL`, **`PDA = 1`**, `OBSERVACIONES = colNombreRetencion || dfNombreProveedor`.
     - El `UPDATE` de `COMPRAS_ACUM/COMPRAS_DOLARES` que sigue es **código muerto** (hijo de `! If sEntraSale='E'`).
   - `Partidas.FluGetNumPartida()` (ojo: su `If` no tiene hijos — no condiciona nada), `CargarCampos()` (**tipo de partida 15**, sinopsis `Proveedor/Retención`, tasa 1), `Partidas.Agregar()` y **`GuardarDetallePartida()`** — **una partida contable POR CADA línea de retención**, con dos renglones en `CNT_PARTIDAS_DTL`:

| Renglón | Cuenta | Cargos | Créditos | Fijos |
|---|---|---|---|---|
| DEBE | `sCuentaProveedor` (= `PRV_PROVEEDORES.CUENTA_CONTABLE`) | `colRetencion` | 0 | `COD_EMPRESA=1` literal, `CORRELATIVO=0`, `COD_CENTROCOSTO='99-0'`, `COD_CLIENTE='99997'`, `COD_MARCAGRUPO='9998'` |
| HABER | `sCodCuentaImpuesto` (= `AXL_RETENCIONES.CUENTA_CONTABLE`) | 0 | `colRetencion` | ídem, `COD_EMPRESA=:nCodEmpresa` (=1) |

   `CONCEPTO = colNombreRetencion`, `COMPROBANTE = dfNumDocumento`, `TASACAMBIO = 1`.

4. **`ActualizarValores()`** (26460–26477) — rebaja el documento origen:
   - Tipo 1: `UPDATE GST_GASTOS_VARIOS SET VALOR = VALOR - :dfTotal WHERE REFERENCIA = :dfNumDocumento` — **defecto: si el UPDATE falla, la función igual devuelve TRUE** (el `If NOT …` no tiene cuerpo; el `Return TRUE` es hermano).
   - Tipo 2: `UPDATE PRV_DESGLOSE_PAGO SET VALOR_A_PAGAR = VALOR_A_PAGAR - :dfTotal, VALOR_DOLARES = VALOR_DOLARES - :dfTotal WHERE NUM_FACTURA_PROV = :sDocReferencia` — resta el **mismo monto en lempiras** a la columna de dólares (defecto de moneda).
   - Otro tipo → FALSE (aborta).

5. **Éxito:** `SqlCommit(hSqlInsertar)` (los UPDATE van por `hSqlModificar` **sin commit explícito** en este handler — commit asimétrico), `bGuardado = FALSE`, deshabilita Guardar/Ins/Del, habilita Imprimir, mensaje *"Retención ha sido aplicada con exito...!"*.
6. **Fallo:** mensajes según rama y `ROLLBACK` de ambos handles.

### 4.5 Cierre (`pbSalir`, 26091–26097)

- Si ya se guardó (`NOT bGuardado`) → devuelve `nMontoRetencion = dfTotal` al llamador.
- Si no se guardó → devuelve `nMontoRetencion = 0.00`.
- Siempre `SalEndDialog(hWndForm, TRUE)`.

---

## 5. Invocador 1: dlgPagoDeCXP (pago de CxP)

### 5.1 Asignación del tipo y botón

- Al poblar facturas de mercancía (`rbFacturasMercancia`, rama SQLSERVER): `Set ntipoRetencion = 2` (3664). Query: `PRV_FACTURAS_HDR + PRV_DESGLOSE_PAGO (STATUS='2', VALOR_A_PAGAR > 0) + OC_ORDENCOMP_HDR`.
- Al poblar otras obligaciones (`rbOtrasObligaciones`): `Set ntipoRetencion = 1` (3852). Query: `GST_GASTOS_VARIOS (STATUS='2')`.
- `rbOCPrepagadas` y las ramas no-SQLSERVER **no asignan** `ntipoRetencion`.
- La misma query se guarda en `sPopQuery` para repoblar después.

**Handler activo de `pbRetencion`** (5451–5458; hay 5 versiones anteriores comentadas en 5421–5450 que documentan la evolución — las tres primeras (5422, 5428, 5434) pasaban la base con el descuento `valor − valor×(config13/100)`; la 4.ª y la 5.ª (5440, 5446) ya pasaban `colValorAPagar` sin descuento):

```
On SAM_Click
    If SalModalDialog( dlgAplicacionRetencion, hWndForm, dfCodProveedor, tblDocumentos.colPk_Docto,
            tblDocumentos.colValorAPagar, nMontoRetencion, ntipoRetencion, SYSTEM_DATE )
        If nMontoRetencion != 0
            Call SalTblPopulate( tblDocumentos, hSqlSeleccionar, sPopQuery, TBL_FillAll )
            Call tblDocumentos.ActualizarValorAPagar()
```

Hoy pasa `colValorAPagar` **sin descuento** (la des-impuestación la hace el diálogo con `FLAG_CALCULA_IMPUESTO`), repuebla el grid (porque `ActualizarValores` ya rebajó el documento en BD) y recalcula — lo que además deselecciona todas las filas.

### 5.2 Efecto de la retención en el pago posterior

| Punto | Línea | Comportamiento |
|---|---|---|
| Validación de cuadre | 5508 / 5515–5519 | Con casa de cambio (rama viva pero inaccesible en UI): `suma × tasa == cheques + retención`. **Sin casa de cambio (caso real): la comparación con retención está COMENTADA**; se exige `dfSumaSeleccionada == nSumaMontoBancario` (cheques por el bruto seleccionado). |
| Kardex del proveedor (pago) | 5593 / 5596 | Lempiras: `nMonto = colValorAPagar − nMontoRetencion` **por cada fila seleccionada** (sin prorrateo). Dólares: `colValorAPagar − nMontoRetencion × nTasaPromedio` (precedencia literal). |
| Detalle del cheque | 5930 | `BNC_CHEQUE_DTL.VALOR_DETALLE = colValorPagado − nMontoRetencion` por cada documento. El encabezado `BNC_CHEQUE_HDR.VALOR` usa `colMonto` sin ajustar. |
| Saldado de documentos | 5723–5823 | `SaldarDocumentos` **ignora** la retención (el documento ya fue rebajado por `ActualizarValores`). |
| Partida del pago | 5978–6013 | `GuardarDetallePartida` del pago ignora la retención (ambos lados por `colMonto`); la contra-partida de la retención la hizo el diálogo. |
| Reset | 6074 | `Set nMontoRetencion = 0` solo si TODO el `MU_PROCESAR` tuvo éxito; re-habilita `pbRetencion`/`pb5`. |

---

## 6. Invocador 2: dlgEmisionCheques (cheques manuales)

- **`cbAplRetencion`** (13282–13316): al marcar muestra `cmbProveedor` (sustituye al campo libre `dfNombreCheque`), habilita `dfNumDocumento` y `dfFechaEmision`, muestra `pbRetencion`. Arranca desmarcado (SAM_CreateComplete oculta/deshabilita todo, 13822–13824).
- **`dfSubTotal`** se calcula en el `SAM_Validate` de `dfMonto` (11652–11653): `dfSubTotal = dfMonto − (dfMonto × config('13') / 100)` — aquí la config '13' se usa como **porcentaje** para des-impuestar.
- **Handler activo de `pbRetencion`** (13181–13186): `SalModalDialog(dlgAplicacionRetencion, hWndForm, cmbProveedor.sCodigo, dfNumDocumento, dfSubTotal, nMontoRetencion2, 2, dfFechaEmision)` — tipo **fijo 2**. Si quedó retención, deshabilita `pbCerrar` y `pbRetencion` (obliga a terminar grabando).
- **Efectos del neto (`dfMonto − nMontoRetencion2`):** línea contable del banco (fila 0 de `tblPartidasBco`: crédito en salida 'S' 11991/11995, cargo en entrada 'E' 12000); `BNC_CHEQUE_HDR.VALOR` (13702–13707). En salida 'S' el cargo de la línea del proveedor **lo digita el usuario** (la asignación automática — que ponía el **neto** `dfMonto − nMontoRetencion2` — está comentada en 12017); como la línea del banco lleva el neto al crédito y `PartidasP.FluCuadra()` solo exige cargos = créditos dentro del grid (1883, `CalcularTotales` 13607–13608), el usuario debe digitar el **neto** para cuadrar — la contrapartida de la retención (cuenta del impuesto) va en la partida tipo 15 aparte que creó `dlgAplicacionRetencion`.
- **Validación en MU_GRABAR** (13845–13849): si `cbAplRetencion` y el proveedor está vacío → aborta.
- **Inconsistencias documentadas:** `PGS_PAGOS_A_CUENTA.MONTO` guarda el **bruto** y el beneficiario se toma **antes** de sustituirlo por el proveedor; `VALOR_LETRAS` se convierte sobre el bruto pero `VALOR` es el neto; `TIPO_CHEQUE` se inserta con variable jamás asignada; el kardex bancario registra el bruto.
- **Reset:** `pbNueva` pone `nMontoRetencion2 = 0.00` y re-habilita botones (12603–12605).

---

## 7. Documentos origen

### 7.1 Tipo 1 — OC de servicios → GST_GASTOS_VARIOS

- `dlgOCServicios` ("Solicitud de compra de servicios") captura: proveedor, concepto, `dfReferencia`, `dfFecVencimiento`, monto, `dfFecFacProveedor` (→ `FECHA_FACTURA_PROVEEDOR`, la fecha de emisión de la constancia), **`cbCalImpuest`** (checkbox "Calcula Impuesto" → `FLAG_CALCULA_IMPUESTO`; dentro del diálogo NO calcula nada, solo se persiste), `cbDetallaISV`, **`dfCAI`** (→ `CAI_PROVEEDOR`, máx. 50), números de factura externa (`NUM_FACTURA_EXT/EXTC`).
- Ciclo por `OC_STATUS` (flags `FLAG_FLOW_BEGIN/FLAG_EMITIDA/FLAG_FACTURAR/...`, permisos por `CNF_FUNCIONES_SOC`); al llegar a un status con `FLAG_FACTURAR` (irreversible) inserta el gasto:

```sql
INSERT INTO GST_GASTOS_VARIOS (COD_PROVEEDOR, PROVEEDOR, REFERENCIA, VALOR, CONCEPTO,
  CUENTA_CONTABLE, FEC_VENCIMIENTO, STATUS, TIPO_PAGO, VALOR_DOLARES, TIPO_TRANSACCION, CAI_PROVEEDOR)
VALUES (:dfCodProveedor, :cmbNombreProveedor, :sReferenciaFinal, :dfValor, :sConcepto,
  '0', :dfFecVencimiento, '2', :sTipoPago, :dfValorDolares, 'GST', :dfCAI)
```

  con `REFERENCIA = NUM_OC || '-' || REFERENCIA` — **esta es la clave** que la retención tipo 1 usa para reencontrar `OC_SERVICIOS_HDR` (`NUM_OC_SERVICIO + '-' + REFERENCIA = :sDocReferencia`). Además genera kardex 'GST' y partida tipo 3.
- `dlgGstos` es **solo consulta/edición ligera** (cQuickTable directo a la tabla); no crea gastos.
- **`GST_GASTOS_VARIOS.STATUS`**: siempre `'2'` (vigente); el pago **BORRA** la fila (DELETE + reinserción de remanente si el pago fue parcial); la retención solo resta `VALOR`.

### 7.2 Tipo 2 — Factura de proveedor → PRV_DESGLOSE_PAGO

- `PRV_FACTURAS_HDR` se inserta en **GA_IN.APT** (`frmFacturacionPRV`, 14300–14337): captura `CAI` (`dfCAI`) pero **NO** tiene columna `FLAG_CALCULA_IMPUESTO` — el flag que lee la retención tipo 2 viene de **`OC_ORDENCOMP_HDR.FLAG_CALCULA_IMPUESTO`** (checkbox `cbCalculaISV` en `frmOCMercaderias`) vía el JOIN.
- La cuota inicial: `INSERT INTO PRV_DESGLOSE_PAGO (…, STATUS, NUM_PAGO, …) VALUES (…, '2', 1, …)`.
- **`PRV_DESGLOSE_PAGO.STATUS`**: `'2'` pendiente → `'1'` pagada (en `SaldarDocumentos`); pago parcial inserta nueva cuota `'2'` por el remanente. La retención **no toca STATUS**, solo `VALOR_A_PAGAR`/`VALOR_DOLARES`.

---

## 8. Consulta y reimpresión

`dlgConsultaRetenciones` ("Consulta de Retenciones - Reimpresión"), menú **Consultas → Consulta Retenciones** (GA_CP.APT:2726, seguridad `arrSeguridad[9]`):

- Filtros: `cbTodos` o proveedor (`cmb1`, clase `clsCbProveedores`; la etiqueta dice "Cliente:" pero filtra proveedor).
- Query (26923–26932): `SELECT CORRELATIVO_RETENCION_DEI, FECHA_CREACION, NOMBRE, TOTAL_RETENCION FROM PRV_RETENCIONES_HDR [WHERE COD_PROVEEDOR = :cmb1.sCodigo] ORDER BY CORRELATIVO_RETENCION_DEI ASC`. Sin filtro de fechas; no muestra el detalle.
- Clic en fila → `sNumRetencion`; `pbImprimir` → reporte **433** vía `ImprimirRetencion(433, sNumRetencion)`. Sin validación de fila seleccionada.

---

## 9. Reversión / anulación

**No existe.** `dlgReversionAnular` no menciona retenciones (0 coincidencias en 26999–27942); reversa partidas de cheques ANULADOS de bancos y además su ítem de menú está comentado (línea 2218) — inalcanzable. En todo el sistema Centura las retenciones **solo se insertan y se reimprimen**; no hay DELETE/UPDATE de reversa sobre `PRV_RETENCIONES_*`.

---

## 10. Impresión — reporte 433

- `pbImprimir` (post-guardado) y la reimpresión llaman `ImprimirRetencion(433, <numRetencion>)`.
- **La función `ImprimirRetencion` no está en los fuentes de APP MERENDON** (ahí solo hay llamadas; en esa app vive en una `.apl` ausente). Sin embargo, **su definición completa sí existe en una variante hermana del repo**: `SIAD_Centura/Casajaar_Final/NEWAPP - Interlease - USAR ESTA PARA GA_CC EN ISUZU PLAZA/GA_CP.APT:806` (y copia en la carpeta `- 22-08-2022`): lee `AXL_REPORTES` por `:nNumReporte` (`TITULO_LISTADO, NOMBRE_REPORTE, VARS_ENLACE, VARS_ENTRADA, SELECT_CMD`), antepone la ruta base de `CNF_CONFIGURACION` código `'200'` al nombre de la plantilla y abre el formulario `frmImprimeRetencion` — mismo patrón genérico de `ImprimirComprobante` (GA_CC.APT:7815–7841).
- El registro `NUM_REPORTE = 433` y su plantilla `.QRP` deben existir en la BD MERENDON (verificable solo contra SQL Server).
- prSiad.dll contiene una implementación .NET paralela (`wfrCreateRetencion`, `GenerarRetencion`, `MostrarReporteRetencion`) sin fuente.

---

## 11. Claves de configuración usadas

| Clave | Uso en el flujo | Evidencia |
|---|---|---|
| `'13'` | Tasa de impuesto (ISV) para derivar la base imponible. **Usos incompatibles**: divisor en el diálogo (`valor / config13`, 26605) vs porcentaje en los llamadores (`valor × config13/100`, 11653 y handlers comentados 5222/5422/5428/5434). El valor real vive en `CNF_CONFIGURACION` (SQL Server) — **pendiente de confirmar** (¿1.15 o 15?). | GA_CP.APT:26605, 11653 |
| `'104'` | Umbral de alerta de correlativos DEI restantes (compartida con facturación). | 26168, GA_IN:16234 |
| `'21'` | Tasa de cambio por defecto (casa de cambio). | 3611 |
| `'48'` / `'49'` | Correlativo de cheques Lempiras / Dólares. | 5680–5693 |
| `'52'` / `'47'` | Correlativo de OC de servicios / facturas de proveedor. | 7295–7312, GA_IN:14269–14289 |
| `'200'` | Ruta base de plantillas de reportes. | GA_CC:7836 |
| `'778'` | Interruptor de control presupuestario. | 13764 |
| `'5001'` | Manejo de dólares en emisión de cheques. | 13825 |

`ObtenerDeConfiguracion` es función de librería (`si_gral.apl`, ausente); la implementación Delphi homónima llama el SP `spGetParamFromConfig` sobre `CNF_CONFIGURACION (CODIGO, VALOR)`.

---

## 12. Defectos y particularidades del código legado

**No replicar en la migración** (documentados para no heredarlos):

1. `pbGuardar`: primer bloque completo de guardado es código muerto; el vivo tiene rutas de fallo sin `Return` que caen al ROLLBACK final sin mensaje.
2. `ActualizarValores` tipo 1: el fallo del UPDATE **no aborta** (If sin cuerpo).
3. Tipo 2 resta el monto en lempiras también a `VALOR_DOLARES`.
4. Config '13' usada como divisor y como porcentaje según el punto del código.
5. `ActualizarPRV`: actualización de `COMPRAS_ACUM/COMPRAS_DOLARES` muerta; saldos precalculados en `PRV_PROVEEDORES` + saldo arrastrado en kardex (Prestadoras usa saldo derivado).
6. Una partida contable POR CADA línea de retención, con fijos `'99-0'/'99997'/'9998'` y `COD_EMPRESA=1` literal.
7. Commit asimétrico: `SqlCommit(hSqlInsertar)` sin commit de `hSqlModificar` en el handler.
8. `pbIns` valida con `and` (condición imposible).
9. Sin casa de cambio, la validación de cuadre del pago **no** considera la retención (comparación comentada).
10. La retención se resta **por cada documento seleccionado** en el kardex del pago y en cada `BNC_CHEQUE_DTL` (sin prorrateo entre documentos).
11. En dlgEmisionCheques: `PGS_PAGOS_A_CUENTA` con bruto y beneficiario viejo; `VALOR_LETRAS` sobre el bruto vs `VALOR` neto; `TIPO_CHEQUE` sin asignar; kardex bancario por el bruto.
12. No hay reversión/anulación de constancias; no hay filtro de fechas en la consulta; reimpresión sin validar selección.
13. Avance de correlativo con `REPLICATE`/`CONVERT` en SQL (sin bloqueo explícito de concurrencia).
14. Sin retención en OC prepagadas ni en ramas de motor ≠ SQL Server (`ntipoRetencion` queda con el valor previo).

---

## 13. Matriz de dependencias de BD

Objetos que el flujo Centura usa, con su equivalente/estado en Prestadoras (PostgreSQL, `siad_v3`):

| Objeto MERENDON (SQL Server) | Rol en el flujo | Equivalente Prestadoras | Estado |
|---|---|---|---|
| `AXL_RETENCIONES` | Catálogo: código, nombre, %, tipo transacción, cuenta contable | **No existe** — propuesto `cfg_retencion` + tasas con vigencia + cuenta por empresa | **nuevo** |
| `AXL_CORRELATIVOS_DEI` (`COD_DEI_DOC=2`) | Correlativo fiscal + CAI de la constancia | `adm_cai_facturacion` + `cfg_tipo_documento_fiscal` — el tipo **ya existe**: código `CRT` "Comprobante de retención" (id 9, seed `Database/ddl_v3/20260507_sar_compliance_01_catalogos.sql:34`); falta el talonario CAI de ese tipo y la lógica de emisión | **alterar** (si aplica constancia formal) |
| `PRV_RETENCIONES_HDR` / `PRV_RETENCIONES_DTL` | Constancia emitida (hdr/dtl) | **No existe** — propuesto `prv_retencion_hdr`/`prv_retencion_dtl` ligadas a `prv_compromiso_hdr` | **nuevo** |
| `PRV_PROVEEDORES` (CUENTA_CONTABLE, saldos) | Cuenta del DEBE; saldos precalculados | `prv_proveedores.cuenta_contable`; las columnas de saldo (`saldo_actual`, `saldo_act_dolares`, …) existen como **legado** pero no se mantienen (se insertan NULL) — el saldo se **deriva** | existente |
| `PRV_KARDEX` | Movimiento negativo por retención | La tabla `prv_kardex` existe (entidad + mapeo) pero **ningún servicio la usa**; el saldo del compromiso se deriva de `prv_compromiso_abono` | **decisión de diseño** (la retención afecta el neto de la partida, no un kardex) |
| `CNT_PARTIDAS_HDR/DTL` (partida tipo 15) | Contabilización | `con_partida_hdr/dtl` vía `sp_registrar_partida_contable` (módulo `PROV`, doc `OPD`) | existente (ya cubre líneas de retención en el modelo GENERAL) |
| `GST_GASTOS_VARIOS` / `PRV_DESGLOSE_PAGO` | Documento origen rebajado | `prv_compromiso_hdr` (+ saldo derivado por abonos) | existente |
| `OC_SERVICIOS_HDR` / `PRV_FACTURAS_HDR` / `OC_ORDENCOMP_HDR` | Fuente de fecha, CAI proveedor y `FLAG_CALCULA_IMPUESTO` | `prv_compromiso_hdr` (hoy sin CAI del proveedor ni flag de impuesto) | **evaluar** (columnas nuevas si se requieren) |
| `CNF_CONFIGURACION` ('13','104') | Tasa ISV, umbral alerta | `cfg_impuesto`/`cfg_impuesto_tasa` (ISV con vigencias) ya cubre la tasa | existente |
| `AXL_REPORTES` + `.QRP` 433 | Layout de la constancia | Reporte DevExpress por código (patrón `Rpt_Dev_*`) | **nuevo** |
| `BNC_CHEQUE_HDR/DTL`, `BNC_KARDEX_CUENTAS`, `PGS_PAGOS_A_CUENTA` | Cheque por el neto | `ban_kardex`/`ban_movimiento` vía `sp_ban_kardex_registrar_movimiento` (ya asienta el neto) | existente |

---

## 14. Mapeo Centura → Prestadoras (SIAD Blazor)

| Concepto Centura | Prestadoras hoy | Brecha |
|---|---|---|
| `dlgAplicacionRetencion` (grid catálogo + % + autocálculo) | Modal "Retenciones y deducciones" en [`CompromisoProveedorProcesar.razor`](../../apc.Client/Pages/Proveedores/CompromisoProveedorProcesar.razor) — líneas **manuales** (cuenta + descripción + tipo Retención/Cargo + monto digitado) | Falta catálogo, % y autocálculo de base |
| Neto del cheque (`valor − retención`) | `MarkAsProcessedAsync` modelo GENERAL: banco al HABER por `NetoBanco`; validado y con tests (`ProcesamientoRetencionesTests`) | Cubierto |
| Partida DEBE proveedor / HABER cuenta retención | Modelo GENERAL: línea proveedor automática (DEBE = bruto) + líneas de deducción con su Débito/Crédito real; cuadre validado | Cubierto (la cuenta del HABER hoy se escoge a mano) |
| Constancia `PRV_RETENCIONES_HDR/DTL` | **No existe** — la retención queda diluida en `con_partida_dtl` | Falta registro estructurado |
| Correlativo DEI + CAI + alerta 104 | Infraestructura CAI existe (`adm_cai_facturacion`, rangos, vigencia, `fecha_limite_emision`, `correlativo_actual`, bloques) y el tipo fiscal `CRT` "Comprobante de retención" ya está sembrado en `cfg_tipo_documento_fiscal` | Falta el talonario CAI del tipo CRT + la lógica de emisión (si aplica) |
| Reporte 433 + reimpresión | Patrón `Rpt_Dev_Compromiso_Proveedor`/`Rpt_Dev_Comprobante_Abono` (XtraReport por código, PDF inline) | Falta `Rpt_Dev_Constancia_Retencion` + pantalla de consulta |
| Kardex proveedor negativo + saldos en `PRV_PROVEEDORES` | Saldo derivado (`prv_compromiso_abono`, estado V/A) — decisión deliberada de NO precalcular | No copiar |
| Base sin ISV (`FLAG_CALCULA_IMPUESTO` + config 13) | `cfg_impuesto_tasa` (ISV 15/18, EXENTO/EXONERADO, con vigencias) | Falta usarla para sugerir la base |
| Retención en **abonos parciales** | `RegistrarAbonoAsync` sigue en modelo contra-magnitud (sin líneas de crédito) | Falta extender a modelo GENERAL si se quiere retener en abonos |

---

## 15. Supuestos confirmados y dudas pendientes

**Confirmado en código:**
- La constancia lleva dos CAI (propio y del proveedor) y numeración de talonario fiscal con vigencia y rango.
- La retención es previa al pago, rebaja el documento y el cheque sale por el neto.
- No existe reversión de constancias en Merendon.
- El catálogo tiene cuenta contable y tipo de transacción por código de retención.

**Pendiente (requiere BD MERENDON o al usuario — no verificable en fuentes):**
1. **Valor real de `CNF_CONFIGURACION` código '13'** (¿factor 1.15 o porcentaje 15?) — resuelve la inconsistencia divisor/porcentaje.
2. **Contenido real de `AXL_RETENCIONES`** (qué retenciones usa Merendon: códigos, %, cuentas) — sería el seed del catálogo en Prestadoras.
3. **Layout del reporte 433** (`AXL_REPORTES.NOMBRE_REPORTE` + `.QRP`) — para la paridad visual de la constancia.
4. DDL exacto de `PRV_RETENCIONES_HDR/DTL` y `AXL_CORRELATIVOS_DEI` (tipos/longitudes).
5. ¿Las prestadoras actúan como **agentes de retención** que emiten constancia formal con CAI/correlativo SAR, o basta el registro interno? (define el alcance fiscal de la migración — ver plan).

> Por regla del proyecto, no me conecto a ninguna BD por iniciativa propia; los puntos 1–4 se confirman cuando el usuario lo autorice contra el SQL Server MERENDON.

---

## 16. Trazabilidad

| Fuente | Revisado |
|---|---|
| `GA_CP.APT` (975,878 bytes, 27,942 líneas) — diálogos y funciones citados con línea exacta | 2026-07-21 |
| `GA_IN.APT` (frmFacturacionPRV 11672+, frmOCMercaderias 6051+, correlativos DEI facturación) | 2026-07-21 |
| `GA_AD.APT` (menús 1587–1595, 1803–1811), `GA_CC.APT` (patrón reportes 7815–7841), `GA_CO.APT` (0 hits), `GA_PTFS.APT` (referencias de librería) | 2026-07-21 |
| `prSiad.XML` + strings de `prSiad.dll` (datasets y formularios .NET de retenciones/correlativos) | 2026-07-21 |
| Delphi de referencia: `Casajaar_Final/DLLS/libdbutils/uGeneralFunctions.pas` (`ObtenerDeConfiguracion`, `GetTransactionNextID`) | 2026-07-21 |
| Lado Prestadoras: `OrdenesPagoDirectoService.cs`, `CompromisoProveedorProcesar.razor`, `ProcesamientoRetencionesTests.cs`, `2026-07-14_cfg_impuestos.sql`, `2026-07-17_prv_compromiso_abono.sql`, `SiadDbContext.SarCompliance.cs`, `CaiTarifarioService.cs`, `Rpt_Dev_*.cs`, `PermissionNames.cs` | 2026-07-21 |

Plan de implementación derivado de este análisis: [`docs/plan_retenciones_compromisos_proveedores.md`](../plan_retenciones_compromisos_proveedores.md).
