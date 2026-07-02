# Flujo de Cheques — GA_CP.APT
## Módulo: Cuentas por Pagar (CXP)
 
**Archivo fuente:** `APP MERENDON/GA_CP.APT`
**Fecha de revisión:** 2026-03-25
**Diálogos analizados:** `dlgPagoDeCXP`, `dlgEmisionCheques`
 
---
 
## Tabla de Contenido
 
1. [Resumen del Módulo](#resumen-del-módulo)
2. [dlgPagoDeCXP — Emisión de Cheques Provisionados](#1-dlgpagodecxp--emisión-de-cheques-provisionados)
   - [Acceso y Seguridad](#acceso-y-seguridad)
   - [Pantalla y Controles](#pantalla-y-controles)
   - [Flujo de Usuario](#flujo-de-usuario)
   - [Lógica de MU_PROCESAR](#lógica-de-mu_procesar)
   - [Funciones Internas](#funciones-internas)
   - [Validaciones](#validaciones)
3. [dlgEmisionCheques — Emisión de Cheques Directos](#2-dlgemisioncheques--emisión-de-cheques-directos)
   - [Acceso y Seguridad](#acceso-y-seguridad-1)
   - [Pantalla y Controles](#pantalla-y-controles-1)
   - [Flujo de Usuario](#flujo-de-usuario-1)
   - [Lógica de MU_GRABAR](#lógica-de-mu_grabar)
   - [Funciones Internas](#funciones-internas-1)
   - [Caso Especial: Anulación de Cheque](#caso-especial-anulación-de-cheque)
4. [Tablas de Base de Datos](#tablas-de-base-de-datos)
5. [Clases Funcionales Utilizadas](#clases-funcionales-utilizadas)
6. [Dependencias y Flujo de Datos entre Diálogos](#dependencias-y-flujo-de-datos-entre-diálogos)
 
---
 
## Resumen del Módulo
 
El módulo de Cuentas por Pagar de `GA_CP.APT` gestiona dos tipos de emisión de cheques:
 
| Diálogo | Tipo | Descripción |
|---|---|---|
| `dlgPagoDeCXP` | **Provisionado** | Paga documentos existentes (facturas, gastos, OC). Genera cheque en cola de impresión. |
| `dlgEmisionCheques` | **Directo** | Emite cheques libres con partida contable manual. No requiere documento previo. |
 
Ambos flujos actualizan: Kardex de cuentas bancarias, Kardex de proveedores, partidas contables y encabezado/detalle de cheques.
 
---
 
## 1. `dlgPagoDeCXP` — Emisión de Cheques Provisionados
 
### Acceso y Seguridad
 
- **Menú:** *Emisión de Cheques Provisionados*
- **Control de acceso:** `arrSeguridad[12]`
- **Tipo de diálogo:** Modal
- **Padre:** `hWndForm`
- **Llamada:** `Call SalModalDialog( dlgPagoDeCXP, hWndForm )`
 
---
 
### Pantalla y Controles
 
#### Encabezado
 
| Control | Tipo | Descripción |
|---|---|---|
| `dfCodProveedor` | Data Field | Código del proveedor. Editable. |
| `cmbNombreProveedor` | Combo Box | Lista de proveedores de `PRV_PROVEEDORES`. |
| `rbPagoLempiras` | Radio Button | Selecciona moneda Lempiras. Activo por defecto. |
| `rbPagoDolares` | Radio Button | Selecciona moneda Dólares. |
| `cbCasaCambio` | Check Box | Pago a través de casa de cambio. **Oculto/inactivo actualmente.** |
| `cmbCasaCambio` | Combo Box | Lista casas de cambio (`AXL_CS_DE_CAMBIO`). **Oculto.** |
| `dfTasaCambio` | Data Field | Tasa de cambio pactada. Se inicializa desde `CNF_CONFIGURACION` código `'21'`. **Oculto.** |
 
#### Tipo de cuenta por pagar (Radio Buttons)
 
| Control | Título | Fuente de datos |
|---|---|---|
| `rbFacturasMercancia` | Facturas de mercancía | `PRV_FACTURAS_HDR` + `PRV_DESGLOSE_PAGO` + `OC_ORDENCOMP_HDR` |
| `rbOtrasObligaciones` | Otras obligaciones | `GST_GASTOS_VARIOS` |
| `rbOCPrepagadas` | Órdenes de compra prepagadas | `OC_ORDENCOMP_HDR` (**Oculto**) |
 
#### Tabla de Documentos (`tblDocumentos`)
 
| Columna | Título | Tipo | Editable | Visible |
|---|---|---|---|---|
| `colNumDocumento` | # documento | String(25) | No | Sí |
| `colCorDoc` | Correlativo | String | No | Sí |
| `colNumPago` | No. pago | Numérico | No | Sí |
| `colFechaVencimiento` | Fecha Venc. | Fecha | No | Sí |
| `colValor` | Valor | Numérico | No | Sí |
| `colFactorCambio` | Factor Cambio | Numérico | Sí (OC Prepag.) | Sí |
| `colTotalDocumento` | Total Docto | Numérico | No | Sí |
| `colValorAPagar` | Valor a pagar | Numérico | Sí | Sí |
| `colTipoTransaccionP` | Tipo | String(25) | No | **No** |
| `colObservacionesP` | Observaciones | String(254) | No | **No** |
| `colValorPagado` | Valor pagado | Numérico | Sí | **No** |
| `colPk_Docto` | Docto_Referencia | String | No | **No** |
 
#### Tabla de Bancos (`tblBancos`)
 
| Columna | Título | Tipo | Descripción |
|---|---|---|---|
| `colBanco` | Banco | Combo | Bancos filtrados por tipo de moneda. Código en `colBanco.nCodigo`. |
| `colCuentaBancaria` | Cuenta Bancaria | Combo | Cuentas del banco seleccionado. |
| `colCorrelativo` | # de Cheque | String | Número de cheque siguiente. Solo lectura. |
| `colSaldoCuenta` | Saldo Actual | Numérico | Saldo actual de la cuenta. |
| `colEmiteCheque` | Emite Cheque | String | `'1'` si emite cheques físicos. **Oculto.** |
| `colTipoCuenta` | Moneda | String | `'Lempiras'` o `'Dolares'`. |
| `colMonto` | Monto | Numérico | Monto del cheque a emitir. Editable. |
| `colConcepto` | Concepto | String(194) | Descripción del pago. Editable. |
| `colTasaPromedio` | Tasa promedio | Numérico | Tasa promedio de la cuenta. **Oculto.** |
| `colCodBanco` | Cod Banco | Numérico | Código interno del banco. **Oculto.** |
| `colCodCuenta` | Cod Cuenta | Numérico | Código interno de la cuenta. **Oculto.** |
| `colNumCheque` | Num Cheque | String | Número de cheque definitivo (asignado al grabar). **Oculto.** |
| `colObservacion` | Observación | Long String | Observación adicional. Editable. |
 
#### Totales y Botones
 
| Control | Descripción |
|---|---|
| `dfTotalDocumentos` | Suma total de documentos cargados en `tblDocumentos`. Solo lectura. |
| `dfSumaSeleccionada` | Suma de `colValorAPagar` de filas **seleccionadas**. Solo lectura. |
| `pbAdicionarBanco` | Agrega fila a `tblBancos`. Asigna `colMonto = dfSumaSeleccionada`. |
| `pbEliminarBanco` | Elimina fila seleccionada de `tblBancos`. |
| `pbDescartar` | Limpia `tblBancos` con confirmación. |
| `pbProcesarPago` | **Ejecuta el proceso de pago.** Llama `MU_PROCESAR`. |
| `pb5` | Cierra el diálogo con confirmación. |
| `pbRetencion` | Abre `dlgAplicacionRetencion`. Visible solo si aplica retención. |
 
---
 
### Flujo de Usuario
 
```
1. Ingresar/seleccionar proveedor
      ↓
   Obtiene CUENTA_CONTABLE del proveedor → sCC_Destino
      ↓
2. Seleccionar tipo de moneda (Lempiras / Dólares)
      ↓
   Recarga tblBancos con cuentas del tipo de moneda seleccionado
      ↓
3. Seleccionar tipo de documento a pagar
      ↓
   Se carga tblDocumentos con documentos pendientes del proveedor (STATUS='2')
      ↓
4. Seleccionar filas en tblDocumentos (marcar ROW_Selected)
      ↓
   dfSumaSeleccionada se actualiza automáticamente
      ↓
5. Agregar fila en tblBancos (pbAdicionarBanco)
      ↓
   Seleccionar banco → Seleccionar cuenta → Se obtiene número de cheque y saldo
      ↓
   Ingresar Monto y Concepto en tblBancos
      ↓
6. Clic en "Procesar" → MU_PROCESAR
```
 
---
 
### Lógica de MU_PROCESAR
 
El procesamiento se ejecuta en cadena estricta. Cualquier fallo ejecuta `ROLLBACK` completo.
 
```
MU_PROCESAR
│
├── SET bHayChequesPendientes = FALSE
├── SET dfFechaPartida = SYSTEM_DATE
│
├── VerificaValidacion()
│     ├── FluValidarInformacion() — valida campos requeridos
│     ├── ObtenerCorrelativoTransaccion('P', 'PAC') → sCorrelativoPRV
│     ├── ObtenerCorrelativoTransaccion('B', 'PAC') → sCorrelativoBNC
│     └── Valida: dfSumaSeleccionada == nSumaMontoBancario
│           Si cbCasaCambio: valida (dfSumaSeleccionada * dfTasaCambio) == nSumaMontoBancario + nMontoRetencion
│
├── ActualizaKardexBNC()
│     └── Por cada fila de tblBancos:
│           ├── Si colEmiteCheque='1':
│           │     └── clsBC.i_SigNumCheque() → colNumCheque (número definitivo)
│           │         UPDATE CNF_CONFIGURACION (incrementa correlativo 48 o 49)
│           ├── Graba clsKardex_Cuentas (tipo PAC, salida de cuenta bancaria)
│           └── Si cuenta en Dólares: nMonto = colMonto * colTasaPromedio
│
├── ActualizaKardexPRV()
│     └── Por cada documento seleccionado en tblDocumentos:
│           ├── Graba clsKardex_Proveedores (tipo PAC)
│           │     Observaciones: "Pago a cuenta de documento # X con cheque #Y de cuenta Z"
│           └── Si rbPagoDolares: ActualizaDiferencialCambio()
│                 ├── nDiferencial = nTasaPromedio - colFactorCambio
│                 └── Tipo: UFC (pérdida) o PFC (ganancia)
│
├── SaldarDocumentos()
│     └── Por cada documento seleccionado:
│           ├── rbFacturasMercancia:
│           │     ├── UPDATE PRV_DESGLOSE_PAGO SET STATUS='1'
│           │     └── Si pago parcial: INSERT PRV_DESGLOSE_PAGO (nueva cuota)
│           ├── rbOCPrepagadas:
│           │     └── UPDATE OC_ORDENCOMP_HDR SET PAGADA='1'
│           └── rbOtrasObligaciones:
│                 ├── DELETE GST_GASTOS_VARIOS WHERE REFERENCIA=colNumDocumento
│                 └── Si pago parcial: INSERT GST_GASTOS_VARIOS (nuevo registro remanente)
│
├── GenerarCheques()
│     └── Por cada fila de tblBancos donde colEmiteCheque='1':
│           ├── SET bHayChequesPendientes = TRUE
│           ├── INSERT PGS_PAGOS_A_CUENTA (monto, num_cheque, nombre, status='1')
│           ├── INSERT BNC_CHEQUE_HDR (encabezado del cheque)
│           │     Campos: NUM_CHEQUE, COD_CUENTA, NOMBRE, FECHA_TRANSACCION, VALOR,
│           │             VALOR_LETRAS (en texto), SALDO, USUARIO_CREO, COD_BANCO,
│           │             DESTINO='PROVEEDOR', CC_DESTINO, CODIGO_DESTINO, SINOPSIS, DESCRIPCION
│           └── Por cada documento seleccionado:
│                 INSERT BNC_CHEQUE_DTL (detalle: DETALLE, VALOR_DETALLE=colValorPagado-nMontoRetencion,
│                                        NUM_DOC, TIPO, CUENTA_CONTABLE)
│
├── Partidas.FluGetNumPartida()
│     └── Obtiene número de partida contable
│
├── CargarCampos()
│     └── Asigna valores a objeto Partidas:
│           sSinopsis, nCodTipoPartida=10, dtFechaPartida, sUsuarioCreo, nTasaCambio=1
│
├── Partidas.Agregar()
│     └── INSERT CNT_PARTIDAS_HDR
│
├── GuardarDetallePartida()
│     ├── SELECT CUENTA_CONTABLE FROM BNC_CUENTAS → sCodCuentaContableBanco
│     ├── INSERT CNT_PARTIDAS_DTL: CRÉDITO sCodCuentaContableBanco (salida banco)
│     ├── INSERT CNT_PARTIDAS_DTL: CARGO sCC_Destino (cuenta proveedor)
│     └── UPDATE BNC_KARDEX_CUENTAS SET PDA=1 WHERE NUM_CHEQUE=colNumCheque
│
├── UPDATE BNC_CHEQUE_HDR SET COD_PARTIDA = Partidas.nCodPartida
│
└── SqlCommit
      └── Si bHayChequesPendientes:
            SalModalDialog(dlgCheques, hWndForm, 'Lempiras') — impresión de cheques
```
 
---
 
### Funciones Internas
 
| Función | Descripción |
|---|---|
| `VerificaValidacion` | Valida campos, obtiene correlativos y verifica que suma documentos == suma bancaria. |
| `ActualizaKardexPRV` | Graba movimiento tipo `PAC` en kardex de proveedores por cada documento seleccionado. |
| `ActualizaDiferencialCambio` | Genera asiento por diferencial cambiario (`UFC` pérdida / `PFC` ganancia). |
| `ActualizaKardexBNC` | Asigna número de cheque definitivo y graba movimiento en kardex bancario. |
| `SaldarDocumentos` | Marca documentos como pagados o genera cuota remanente si el pago es parcial. |
| `GenerarCheques` | Inserta registros en `PGS_PAGOS_A_CUENTA`, `BNC_CHEQUE_HDR` y `BNC_CHEQUE_DTL`. |
| `GenerarCuotaFactura` | Inserta nueva cuota en `PRV_DESGLOSE_PAGO` cuando el pago es parcial. |
| `GenerarCuotaGasto` | Inserta registro remanente en `GST_GASTOS_VARIOS` cuando el pago es parcial. |
| `GuardarDetallePartida` | Inserta las dos líneas de la partida contable (banco y proveedor) y marca el kardex. |
| `CargarCampos` | Carga los atributos del objeto `Partidas` antes de grabar. |
| `tblDocumentos.ActualizarValorAPagar` | Recorre todas las filas y actualiza `colValorAPagar` según la moneda seleccionada. |
 
---
 
### Validaciones
 
| Campo / Condición | Mensaje de error |
|---|---|
| `dfCodProveedor` inexistente | "No existe el código especificado" |
| `colValorAPagar > colTotalDocumento` | "El valor digitado es mayor que el total del documento!" |
| `colValorAPagar <= 0` | "El valor digitado no puede ser menor o igual a cero!" |
| Campo en blanco en `tblBancos` | "Campo no puede quedar en blanco" |
| Suma documentos ≠ suma bancaria | "La suma a pagar difiere a la de los cheques bancarios. Por favor verifique..." |
| `dfTasaCambio <= 0` (si casa de cambio) | "La tasa de cambio debe ser mayor que cero!" |
| Cambio de moneda con datos en tblBancos | "La tabla de cuentas bancarias será borrada. ¿Está seguro?" |
 
---
 
## 2. `dlgEmisionCheques` — Emisión de Cheques Directos
 
### Acceso y Seguridad
 
- **Menú:** *Emisión de Cheques Directos*
- **Control de acceso:** `arrSeguridad[14]`
- **Tipo de diálogo:** Modal
- **Padre:** `hWndMDI`
- **Llamada:**
```centura
If NOT SalModalDialog(dlgEmisionCheques, hWndMDI)
    Return FALSE
Return TRUE
```
 
---
 
### Pantalla y Controles
 
#### Encabezado
 
| Control | Tipo | Descripción |
|---|---|---|
| `cmbTransaccionBco` | Combo Box | Tipo de transacción bancaria (`BNC_TIPOSTRANSACC`). Por defecto filtra `PAC`. |
| `dfNombreCheque` | Data Field (180) | "A favor de": nombre del beneficiario. Visible cuando `sEntraSale='S'`. |
| `dfNumCheque` | Data Field | Número de cheque a anular. Visible solo en transacciones tipo `'ANULA'`. |
| `mlSinopsis` | Multiline (4096) | Sinopsis/descripción de la transacción. |
| `mlObservacion` | Multiline (4096) | Observación detallada (se copia al concepto del cheque). |
 
#### Datos Bancarios
 
| Control | Tipo | Descripción |
|---|---|---|
| `cmbBanco` | Combo Box | Bancos que emiten cheques (`EMITE_CHEQUES=1`). |
| `cmbCuenta` | Combo Box | Cuentas del banco seleccionado con `EMITE_CHEQUES=1`. |
| `dfMonto` | Data Field | Monto en lempiras. Calculado si cuenta en dólares. |
| `dfDolares` | Data Field | Monto en dólares (visible si cuenta en dólares). |
| `dfCambio_Dol` | Data Field | Tasa de cambio. Se obtiene de `BNC_CUENTAS.TASA_PROMEDIO`. Editable. |
| `dfReferencia2` | Data Field (25) | Número de cheque reservado. Se llena automáticamente al seleccionar cuenta. |
| `cmbTipoPartida` | Combo Box | Tipo de partida contable (`CNT_TIPOSPARTIDA`). |
 
#### Tabla de Partidas (`tblPartidasBco`)
 
| Columna | Título | Editable | Descripción |
|---|---|---|---|
| `colCuenta` | Código Cuenta | Sí | Cuenta contable. Validada contra `CNT_CATALOGO`. |
| `colDescripcion` | Nombre de la cuenta | No | Nombre de la cuenta contable. **Oculto.** |
| `colCentroCosto` | Centro Costo | Sí | Default `'99-0'`. |
| `colCodCliente` | Cód. Cliente | No | Default `'999997'`. |
| `colMarcaGrupo` | Marca Grupo | No | Default `'9898'`. |
| `colConcepto` | Concepto | Sí | Se copia de `mlObservacion`. |
| `colComprobante` | Comprobante | — | Número de cheque para la línea. |
| `colCargos` | Cargos | — | Monto cargo (entrada). |
| `colCreditos` | Créditos | — | Monto crédito (salida). |
| `colCorrelativoPda` | Correlativo | — | `0` para primera línea (banco), `n+1` para siguientes. |
 
#### Botones
 
| Control | Descripción |
|---|---|
| `pbNueva` | Habilita campos para nueva transacción. |
| `pbInsertar` (F5) | Agrega línea al `tblPartidasBco`. Valida banco y cuenta previamente. |
| `pbEliminar` | Elimina línea seleccionada del `tblPartidasBco`. |
| `pbCalcular` | Abre la Calculadora de Windows (`CALC.EXE`). |
| `pbGrabar` | Ejecuta `MU_GRABAR`. |
| `pbCerrar` | Cierra el diálogo. |
| `pbRetencion` | Aplica retención (oculto, se activa según configuración). |
 
---
 
### Flujo de Usuario
 
```
1. SAM_CreateComplete
      ↓
   Deshabilita pbInsertar, pbEliminar, pbGrabar
   Llama pbNueva.SAM_Click → habilita campos
      ↓
2. Seleccionar tipo de transacción en cmbTransaccionBco
      ↓
   Recupera DESTINO, ENTRA_SALE, EMITE_CHEQUE, CORRELATIVO
   Muestra/oculta dfNombreCheque o dfNumCheque según ENTRA_SALE/DESTINO
      ↓
3. Si transacción tipo 'ANULA':
      ↓ ver sección "Caso Especial: Anulación"
   Si transacción normal (ej. PAC):
      ↓
4. Ingresar "A favor de" (dfNombreCheque)
      ↓
5. Ingresar Sinopsis y Observación
      ↓
6. Seleccionar Banco → Seleccionar Cuenta
      ↓
   Obtiene TIPO_CUENTA, TASA_PROMEDIO, sNumCheque = clsBC.i_SigNumCheque()
   dfReferencia2 = sNumCheque (número reservado)
   Si cuenta en Dólares: muestra dfDolares y dfCambio_Dol
      ↓
7. Ingresar Monto (o Dólares + Tasa si aplica)
      ↓
   dfSubTotal = dfMonto - (dfMonto * config('13') / 100)
      ↓
8. Seleccionar Tipo de Partida
      ↓
9. Clic pbInsertar para agregar líneas al tblPartidasBco
      ↓
   Línea 0 (banco): cuenta contable del banco, crédito = dfMonto - nMontoRetencion2
   Líneas siguientes: cuenta del proveedor si aplica retención
      ↓
10. Clic pbGrabar → MU_GRABAR
```
 
---
 
### Lógica de MU_GRABAR
 
```
MU_GRABAR
│
├── Valida que los campos principales no estén vacíos
│     (cmbTransaccionBco, cmbTipoPartida, dfMonto, cmbBanco, cmbCuenta)
│
├── Si cbAplRetencion:
│     └── cmbProveedor.ObtenerCodigo() — valida que no esté vacío
│
├── GetTransactionNextID('B', cmbTransaccionBco.sCodigo, sCorrelativoBNC)
│     └── Obtiene correlativo de la transacción bancaria
│
├── ActKardexCuentas()
│     └── Graba movimiento en BNC_KARDEX_CUENTAS
│
├── PartidasP.FluGetNumPartida()
│     └── Obtiene número de partida contable disponible
│
├── CargarCampos()
│     └── Asigna: sSinopsis, nCodTipoPartida, dtFechaPartida, sUsuarioCreo, nTasaCambio
│
├── PartidasP.Agregar()
│     └── INSERT CNT_PARTIDAS_HDR
│
├── PartidasP.AgregarPartidas(tblPartidasBco)
│     └── INSERT CNT_PARTIDAS_DTL por cada línea de la tabla
│           dfNumPartida = PartidasP.nCodPartida
│
├── EmitirCheque()
│     ├── Si nEmiteCheque = 1:
│     │     ├── INSERT PGS_PAGOS_A_CUENTA (monto, sNumCheque, nombre, status='1')
│     │     ├── INSERT BNC_CHEQUE_HDR:
│     │     │     NUM_CHEQUE, COD_CUENTA, NOMBRE=dfNombreCheque,
│     │     │     VALOR = dfMonto - nMontoRetencion2,
│     │     │     VALOR_LETRAS (en texto), SALDO, USUARIO_CREO,
│     │     │     TIPO_CHEQUE='', DESTINO='MANUAL',
│     │     │     COD_PARTIDA=dfNumPartida, DESCRIPCION=mlObservacion
│     │     ├── INSERT BNC_CHEQUE_DTL (DETALLE=mlSinopsis, VALOR_DETALLE=nMontoTotal)
│     │     ├── SELECT CUENTA_CONTABLE FROM BNC_CUENTAS → sCuentaBco
│     │     ├── SELECT CONCEPTO FROM CNT_PARTIDAS_DTL WHERE COD_CUENTA=sCuentaBco
│     │     ├── UPDATE CNT_PARTIDAS_DTL SET CONCEPTO = sConcepto || ' Cheque No.' || sNumCheque
│     │     └── clsBC.i_Cerrar() — confirma el correlativo de cheque usado
│     └── Si nEmiteCheque = 0: Return TRUE (sin generar cheque físico)
│
└── SqlCommit
      ├── FluLimpiarCampos()
      └── Mensaje: "La transacción ha sido posteada"
```
 
---
 
### Funciones Internas
 
| Función | Descripción |
|---|---|
| `EmitirCheque` | Genera el cheque en `BNC_CHEQUE_HDR`, `BNC_CHEQUE_DTL` y `PGS_PAGOS_A_CUENTA`. Actualiza concepto de la partida con el número de cheque. |
| `ActKardexCuentas` | Registra el movimiento en el kardex de cuentas bancarias. |
| `CargarCampos` | Inicializa el objeto `PartidasP` con datos del formulario. |
| `CalcularTotales` | Recalcula `dfTotCargos` y `dfTotCreditos` a partir de `tblPartidasBco`. |
| `ValidarPresupuestos` | Si `CNF_CONFIGURACION('778') != '0'`, valida presupuesto del centro de costo. |
 
---
 
### Caso Especial: Anulación de Cheque
 
Cuando el usuario selecciona una transacción con `DESTINO='ANULA'`:
 
```
1. Se muestra dfNumCheque (campo "Cheque #")
      ↓
2. SAM_Validate de dfNumCheque:
      ├── Verifica existencia en BNC_CHEQUE_HDR
      │     Si NO existe → pregunta si desea anular otro cheque
      └── Si existe → confirma anulación
            ├── SELECT COD_PARTIDA, COD_BANCO, COD_CUENTA, VALOR
            │     FROM BNC_CHEQUE_HDR WHERE NUM_CHEQUE=dfNumCheque
            ├── Carga cmbBanco y cmbCuenta con los datos del cheque
            ├── Deshabilita cmbBanco, dfMonto, cmbCuenta (no editables en anulación)
            └── SalTblPopulate(tblPartidasBco):
                  SELECT COD_CUENTA, ' ', 'ANULACION CHEQUE #X',
                         COMPROBANTE, CREDITOS, CARGOS   ← invertidos
                  FROM CNT_PARTIDAS_DTL WHERE COD_PARTIDA=nPartidaConsulta
                  → Marca todas las filas como ROW_New para grabar
```
 
---
 
## Tablas de Base de Datos
 
### Tablas de Lectura (SELECT)
 
| Tabla | Uso |
|---|---|
| `PRV_PROVEEDORES` | Nombre, código y cuenta contable del proveedor. |
| `PRV_FACTURAS_HDR` | Encabezado de facturas de compra. |
| `PRV_DESGLOSE_PAGO` | Cuotas de pago de facturas. `STATUS='2'` = pendiente. |
| `OC_ORDENCOMP_HDR` | Órdenes de compra. `PREPAGADA='1'`, `PAGADA='0'`. |
| `GST_GASTOS_VARIOS` | Otras obligaciones pendientes. `STATUS='2'`. |
| `BNC_BANCOS` | Catálogo de bancos. |
| `BNC_CUENTAS` | Cuentas bancarias: saldo, tasa promedio, tipo, si emite cheques. |
| `BNC_TIPOSTRANSACC` | Tipos de transacción bancaria. |
| `CNT_CATALOGO` | Catálogo contable para validar cuentas. |
| `CNT_TIPOSPARTIDA` | Tipos de partida contable. |
| `CNF_CONFIGURACION` | Parámetros: tasa de cambio (`'21'`), # cheque Lempiras (`'48'`), # cheque Dólares (`'49'`), retención ISR (`'13'`), manejo dólares (`'5001'`). |
| `AXL_CS_DE_CAMBIO` | Catálogo de casas de cambio. |
 
### Tablas de Escritura (INSERT / UPDATE / DELETE)
 
| Tabla | Operaciones | Descripción |
|---|---|---|
| `PRV_DESGLOSE_PAGO` | UPDATE, INSERT | Marca cuota como pagada (`STATUS='1'`). Inserta nueva cuota si pago parcial. |
| `OC_ORDENCOMP_HDR` | UPDATE | Marca OC como pagada (`PAGADA='1'`). |
| `GST_GASTOS_VARIOS` | DELETE, INSERT | Elimina obligación pagada. Inserta remanente si pago parcial. |
| `BNC_KARDEX_CUENTAS` | INSERT, UPDATE | Movimiento de salida de cuenta bancaria. Marca `PDA=1` al generar partida. |
| `PGS_PAGOS_A_CUENTA` | INSERT | Cola de cheques pendientes de impresión. `STATUS='1'`. |
| `BNC_CHEQUE_HDR` | INSERT, UPDATE | Encabezado del cheque emitido. Se actualiza `COD_PARTIDA` al final. |
| `BNC_CHEQUE_DTL` | INSERT | Detalle del cheque (documentos asociados). |
| `CNT_PARTIDAS_HDR` | INSERT | Encabezado de la partida contable. |
| `CNT_PARTIDAS_DTL` | INSERT, UPDATE | Líneas de la partida contable. Se actualiza el concepto con el # de cheque. |
| `CNF_CONFIGURACION` | UPDATE | Incrementa correlativo de cheques (`'48'` Lempiras, `'49'` Dólares). |
 
---
 
## Clases Funcionales Utilizadas
 
| Clase | Variable | Uso |
|---|---|---|
| `clsKardex_Proveedores` | `clsKardex` | Graba movimientos en kardex de proveedores. |
| `clsKardex_Cuentas` | `clsKardex` | Graba movimientos en kardex de cuentas bancarias. |
| `clsPartidas` | `Partidas` / `PartidasP` | Genera partidas contables (`CNT_PARTIDAS_HDR` + `CNT_PARTIDAS_DTL`). |
| `clsLotes` | Variable sin nombre | Genera partidas en modo lote (comentado en `dlgEmisionCheques`). |
| `clsfcBancosCheque` | `clsBC` | Maneja el correlativo de cheques: `i_Inicializar()`, `i_SigNumCheque()`, `i_Cerrar()`. |
| `clsCbGenericoStr` | `cmbNombreProveedor`, `cmbTransaccionBco` | Combo genérico con búsqueda por código string. |
| `clsCbGenerico` | `cmbBanco`, `cmbCuenta`, `cmbTipoPartida` | Combo genérico con búsqueda por código numérico. |
 
---
 
## Dependencias y Flujo de Datos entre Diálogos
 
```
Menú CXP
│
├── dlgPagoDeCXP (Cheques Provisionados)
│     │
│     ├── Al procesar exitosamente con bHayChequesPendientes=TRUE:
│     │     └── dlgCheques (Cola de impresión, moneda='Lempiras')
│     │
│     └── pbRetencion:
│           └── dlgAplicacionRetencion (aplicación de retenciones ISR)
│
└── dlgEmisionCheques (Cheques Directos)
      │
      └── SAM_Close / pbCerrar:
            Call SalEndDialog(dlgEmisionCheques, 0)
```
 
### Variables Compartidas Clave en `dlgPagoDeCXP`
 
| Variable | Tipo | Descripción |
|---|---|---|
| `sCorrelativoPRV` | String | Correlativo de la transacción en kardex de proveedores. |
| `sCorrelativoBNC` | String | Correlativo de la transacción bancaria. |
| `sCC_Destino` | String | Cuenta contable del proveedor (destino del cargo). |
| `nSumaMontoBancario` | Number | Suma total de `colMonto` en `tblBancos`. |
| `nTasaPromedio` | Number | Promedio de tasas en `tblBancos`. |
| `bHayChequesPendientes` | Boolean | `TRUE` si al menos una cuenta bancaria emite cheques. |
| `nMontoRetencion` | Number | Monto de retención aplicada (de `dlgAplicacionRetencion`). |
| `ntipoRetencion` | Number | `1` = otras obligaciones, `2` = facturas mercancía. |
| `sPopQuery` | Long String | Query guardada para repoblar `tblDocumentos` tras retención. |
| `Partidas` | clsPartidas | Objeto para generación de partida contable. |
 
---
 
*Documento generado en base al análisis del código fuente `GA_CP.APT`. Revisión: 2026-03-25.*
