# Flujo: Impresión de cheques (COMPAGOL / COMPAGOLG)

Migración de los dos formatos de impresión de cheque de la **APP Española** (Centura/Delphi
ReportBuilder) al módulo de **Bancos** del SIAD (Blazor + DevExpress, reportes por código).

- **Fecha:** 2026-07-21
- **Estado:** Implementado en local (rama `Cambios_almacen1.0`), compila y verificado el render de los PDF con datos de ejemplo. Pendiente: calibración de posiciones del cheque físico y prueba end-to-end (requiere el esquema `ban_cheque` aplicado en la BD).

## 1. Origen (Centura / Delphi)

Ruta: `C:\Users\Dell\Documents\Github\SIAD_Centura\APP ESPAÑOLA\`

| Reporte QRP | Rol | Fuente de datos |
|---|---|---|
| `COMPAGOL.QRP` (20 KB, Times New Roman + Arial) | **Comprobante interno / voucher contable.** Título "Solicitud de emisión de cheque", datos del pago, distribución contable (cargos/créditos) y firmas ELABORADO/REVISADO/APROBADO/Vo.Bo. | `CHE2000.QSD` |
| `COMPAGOLG.QRP` (10 KB, Book Antiqua) | **Cheque para el cliente.** Se imprime SOBRE el cheque preimpreso del banco: lugar y fecha, beneficiario, monto en letras (`*** … ***`) y monto en número. | `CHE2000.QSD` |

**SQL fuente** (`APP ZIP/OKTOPUS/SIA/REP/QUE/CHE2000.QSD`), reconstruido:

```sql
SELECT BNC_CHEQUE_HDR.NUM_CHEQUE, BNC_CHEQUE_HDR.NOMBRE, BNC_CHEQUE_HDR.FECHA_TRANSACCION,
       VALOR, VALOR_LETRAS, BNC_BANCOS.NOMBRE AS BANCO, DESCRIPCION AS NUM_CTA,
       CNT_PARTIDAS_DTL.COD_CUENTA, CARGOS, CREDITOS, COMPROBANTE, CONCEPTO,
       COD_CENTROCOSTO, USER, CNT_PARTIDAS_HDR.SINOPSIS, C.NOMBRE AS NOMBRE_CUENTA
FROM PGS_PAGOS_A_CUENTA, BNC_CHEQUE_HDR, BNC_BANCOS, BNC_CUENTAS,
     CNT_PARTIDAS_DTL, CNT_PARTIDAS_HDR, CNT_CATALOGO C
WHERE ... BNC_CHEQUE_HDR.NUM_CHEQUE = :num_cheque ORDER BY CORRELATIVO;
```

Es decir: encabezado del cheque (`BNC_CHEQUE_HDR`) unido a la partida contable
(`CNT_PARTIDAS_HDR/DTL` + catálogo) para la distribución de cargos/créditos.

## 2. Mapeo Centura → SIAD

| Campo Centura | Origen en SIAD |
|---|---|
| `NUM_CHEQUE` | `ban_cheque.numero_cheque` |
| `NOMBRE` (beneficiario) | `ban_cheque.beneficiario` |
| `FECHA_TRANSACCION` | `ban_cheque.fecha_emision` |
| `VALOR` | `ban_cheque.monto` |
| `VALOR_LETRAS` | `NumerosALetras.Convertir(monto)` + `" LEMPIRAS"` |
| `BANCO` | `ban_banco.nombre` (vía `ban_cuenta.ban_banco_id`) |
| `NUM_CTA` | `ban_cuenta.numero_cuenta` |
| `CONCEPTO` | `ban_cheque.concepto` |
| `COMPROBANTE` | `con_partida_hdr.poliza_number` |
| `COD_CUENTA` / `NOMBRE_CUENTA` | `con_partida_dtl.account_id` → `con_plan_cuenta.code` / `.name` |
| `CARGOS` / `CREDITOS` | `con_partida_dtl.debit_amount` / `.credit_amount` |
| `COD_CENTROCOSTO` | `con_partida_dtl.cost_center_id` → `con_centro_costo.code` |
| Empresa (nombre, RTN, dirección, logo) | `cfg_company.commercial_name / legal_name / tax_id / address / logo` |

El vínculo cheque → partida es `ban_cheque.partida_id` → `con_partida_hdr.poliza_id`.

## 3. Archivos (creados / modificados)

**Creados**
- `SIAD.Core/DTOs/Bancos/ChequeImpresionDtos.cs` — `ChequeImpresionDto` + `ChequeDistribucionLineaDto`.
- `SIAD.Reports/Templates/Rpt_Dev_Cheque_Comprobante.cs` — voucher interno (COMPAGOL).
- `SIAD.Reports/Templates/Rpt_Dev_Cheque.cs` — cheque **CORTO** (COMPAGOLG): solo el cheque sobre el preimpreso (papel 8 × 3.5").
- `SIAD.Reports/Templates/Rpt_Dev_Cheque_Detalle.cs` — cheque **LARGO con detalle** (cheque + comprobante en una sola hoja de 19.0 × 21.5 cm, formato Aguas de Puerto Cortés / Banco Lafise). Ver §8.

**Modificados**
- `SIAD.Services/Bancos/IChequesService.cs` + `ChequesService.cs` — método `GetDatosImpresionAsync(chequeId, impresoPor, ct)`; inyecta `IAccountFormatService`; constante `CiudadEmisionCheque`.
- `apc/Controllers/Bancos/ChequesController.cs` — endpoints `GET {chequeId}/comprobante/pdf` y `GET {chequeId}/cheque/pdf`.
- `apc.Client/Services/Bancos/ChequesClient.cs` — `GetComprobantePdfUrl` / `GetChequePdfUrl`.
- `apc.Client/Pages/Bancos/ChequesList.razor` — botones "Imprimir cheque" / "Imprimir comprobante" en el popup de detalle de la bitácora.
- `SIAD.Tests/Bancos/ChequesServiceTests.cs` — stub `IAccountFormatService` para el nuevo constructor.

Patrón seguido: reportes DevExpress "por código puro" (familia de `Rpt_Dev_Compromiso_Proveedor`), `XtraReport` con `DetailBand`, helpers `AddLabel`/`AddLine`/`AddGridRow`, export `ExportToPdf` a PDF `inline`, disparo desde Blazor con `JS.open(url, "_blank")`.

## 4. Endpoints

| Método | Ruta | Reporte | Nombre PDF |
|---|---|---|---|
| GET | `/api/bancos/cheques/{chequeId}/comprobante/pdf` | `Rpt_Dev_Cheque_Comprobante` | `Comprobante-Cheque-NNNNNN.pdf` |
| GET | `/api/bancos/cheques/{chequeId}/cheque/pdf` | `Rpt_Dev_Cheque` (corto) | `Cheque-NNNNNN.pdf` |
| GET | `/api/bancos/cheques/{chequeId}/cheque-detalle/pdf` | `Rpt_Dev_Cheque_Detalle` (largo) | `Cheque-Detalle-NNNNNN.pdf` |

Ambos bajo `[ModuleAuthorize(PermissionModules.Bancos)]` (permiso de bancos, ya existente).

## 5. Decisiones tomadas (defaults, ajustables)

1. **Distribución del voucher:** de la partida ligada (`partida_id`). Si el cheque no tiene partida (origen MANUAL/TRANSACCIÓN), el voucher se imprime con la nota *"Cheque sin partida contable asociada"* en lugar de la tabla.
2. **Ciudad del cheque:** de `con_empresa_configuracion.ciudad` (la configuración de la empresa). Si está vacía, el cheque imprime solo la fecha (sin coma suelta).
3. **Monto en letras:** `NumerosALetras.Convertir(monto)` + `" LEMPIRAS"` (mismo criterio que el comprobante de proveedor). En el cheque va entre asteriscos: `*** … ***`.
4. **Anulado:** ambos formatos llevan marca de agua "ANULADO" cuando `ban_cheque.estado = 'A'`.
5. **`origen = MANUAL` cubre dos casos** (desde 2026-07-27): con `estado = 'E'` es un **cheque manual**
   (suelto, sin compromiso, emitido desde Cheques emitidos o desde Proveedores); con `estado = 'A'`
   y monto 0 es la **anulación de un número** (cheque dañado). La UI los rotula distinto según el
   estado/acción del evento.

## 6. Pendientes

- **Calibración del cheque físico:** las posiciones de `Rpt_Dev_Cheque.cs` están en constantes agrupadas (bloque `CALIBRACION`, unidades 1/100"). Ajustar contra una impresión de prueba sobre el cheque real de cada banco. Confirmar además si el cheque va suelto (8" × 3.5", default actual) o en hoja carta con talón.
- **Ciudad configurable** por empresa/cuenta (hoy es constante).
- **Prueba end-to-end:** requiere el esquema `ban_cheque` aplicado en la BD (script `Database/2026-07-21_cheques_numeracion_bitacora.sql`, aún pendiente de aplicar en mirror/SRV). El render de los reportes ya se validó por separado con datos de ejemplo.

## 7. Dependencias de BD

Ninguna nueva. Usa tablas ya existentes: `ban_cheque`, `ban_cuenta`, `ban_banco`, `con_partida_hdr`, `con_partida_dtl`, `con_plan_cuenta`, `con_centro_costo`, `cfg_company`. El esquema `ban_cheque`/`ban_cheque_bitacora` proviene del flujo de numeración/bitácora de cheques (script `Database/2026-07-21_cheques_numeracion_bitacora.sql`).

## 8. Formato de hoja completa (Rpt_Dev_Cheque_Detalle)

> Hay **dos formatos de cheque**: el **corto** (`Rpt_Dev_Cheque`, solo el cheque, 8 × 3.5") y el **largo con detalle** (`Rpt_Dev_Cheque_Detalle`, esta sección). Además del comprobante interno tamaño carta (`Rpt_Dev_Cheque_Comprobante`, §3).

`Rpt_Dev_Cheque_Detalle.cs` imprime **cheque + comprobante en una sola hoja preimpresa** (Aguas de Puerto Cortés / Banco Lafise). Papel custom **19.0 × 21.5 cm** (748 × 846 en 1/100"), márgenes 0, imprime solo los datos variables sobre el preimpreso. Todas las posiciones están en el bloque `CALIBRACION` del reporte (unidades 1/100", 1 cm = 39.37).

- **Cheque (arriba, 0 → 10 cm) — posiciones aproximadas, calibrables:** lugar y fecha, beneficiario, valor y cantidad en letras (esta última sin "LEMPIRAS", que va preimpreso).
- **Comprobante (abajo, pegado al fondo) — medidas exactas dadas por el cliente:**

| Área | Ancho × Alto (cm) | Reporte (1/100") | Posición X/Y (1/100") |
|---|---|---|---|
| Concepto | 15.02 × 5.35 | 591 × 211 | X 12 · Y 429 |
| Valor | 3.3 × 5.35 | 130 × 211 | X 603 · Y 429 |
| Código | 3.2 × 3.9 | 126 × 154 | X 12 · Y 640 |
| Descripción | 10.2 × 3.9 | 402 × 154 | X 138 · Y 640 |
| Debe | 2.5 × 3.9 | 98 × 154 | X 540 · Y 640 |
| Haber | 2.5 × 3.9 | 98 × 154 | X 638 · Y 640 |
| Firmas | alto 1.25 · 4 col. de 4.6 | 49 alto · 181 c/u | Y 793 |

Márgenes del comprobante: izq/der 0.3 cm (12), superior del bloque a 10.0 cm (394), inferior 0.1 cm. En firmas solo se imprime **ELABORADO POR** (usuario logueado); las otras tres quedan en blanco. "Orden de Pago No." = `ban_cheque.origen_documento`.

Render verificado con datos de ejemplo (PDF de la hoja y de la variante anulada). La ciudad sale de `con_empresa_configuracion.ciudad`. **Pendiente:** calibrar las posiciones del **cheque (arriba)** con una impresión de prueba.

## 9. Diálogo de impresión al pagar con cheque

Al **generar un pago con cheque** en cualquiera de los tres flujos, tras emitir el cheque se abre un diálogo (`apc.Client/Pages/Bancos/ChequeImpresionDialog.razor`) para elegir qué imprimir; cada opción abre el PDF en pestaña nueva.

| Flujo | Página | Opciones del diálogo |
|---|---|---|
| Procesar compromiso | `CompromisoProveedorProcesar.razor` | Compromiso · Cheque · Cheque + detalle |
| Abono a compromiso | `CompromisoProveedorAbonar.razor` | Comprobante de abono · Cheque · Cheque + detalle |
| Transacción bancaria | `TransaccionBancariaModal.razor` | Cheque · Cheque + detalle |
| **Cheque manual (suelto)** | `ChequeManualDialog.razor`, abierto desde `ChequesList.razor`, `ProveedoresList.razor` y `ProveedorDetail.razor` | Cheque · Cheque + detalle |

**Cómo llega el `cheque_id`:** `IChequesService.EmitirChequeAsync` ahora devuelve `(cheque_id, numero)`. Se propaga a los DTOs de resultado: `OrdenPagoDirectoOperacionResultadoDto.ChequeId`, `AbonoCompromisoResultadoDto.ChequeId` y el nuevo `BanTransaccionResultadoDto.ChequeId`. El diálogo solo aparece cuando el pago fue con cheque (`ChequeId > 0`).

**Reimpresión desde el detalle de una transacción bancaria:** en el detalle (modal `ReadOnly` de `TransaccionBancariaModal`) hay un botón **"Reimprimir cheque"** (visible solo si el movimiento emitió cheque) que reutiliza el mismo `ChequeImpresionDialog`, junto al botón **"Imprimir comprobante"** de la transacción (`Rpt_DE_Transacciones_Bancarias`) que ya existía. El cheque de un movimiento se resuelve por `ban_kardex_id`: `ChequesService.GetChequeIdVigentePorKardexAsync` → `GET /api/bancos/cheques/por-kardex/{banKardexId}` → cliente `ChequesClient.GetChequeIdPorKardexAsync`.
