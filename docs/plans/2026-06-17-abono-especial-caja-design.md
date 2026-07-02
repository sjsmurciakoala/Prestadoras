# Abonos Especiales en Caja — Design Doc
**Fecha:** 2026-06-17  
**Rama:** Modulo_Caja_1.0

## Objetivo
Agregar la pestaña "Abonos Especiales" en la vista Captación en Caja (`Caja.razor`) que permita al cajero aplicar pagos a facturas con saldo pendiente, reducir el saldo de la factura y generar la partida contable automáticamente, con emisión de un recibo impreso en PDF.

## Contexto
- El backend ya implementa el flujo completo en `AbonoService.RegistrarAbonoAsync`:
  - Reduce `factura_detalle.montovalor_saldo` en cascada
  - Actualiza `factura.estado` a "C" (cobrado) o "B" (parcial)
  - Genera póliza contable via `sp_con_generar_comprobante`
- El componente `PosteoAbonos.razor` ya tiene la UI de búsqueda + aplicación de abono
- La forma de pago es Efectivo únicamente en esta fase

## Decisiones de diseño
| Decisión | Elección |
|----------|----------|
| Forma de pago | Solo Efectivo (fase 1) |
| Recibo | DevExpress XtraReport → PDF, abierto en nueva pestaña del browser |
| Logo | Tomado de `LogoBase64` del tenant config (igual que Cartas de Cobro) |

## Diseño del recibo (80mm × auto)
```
[LOGO EMPRESA — centrado]
NOMBRE EMPRESA
─────────────────
No. Recibo   XXXXXXX
─────────────────
Periodo    : YYYY/MM
Fecha Emision: DD/MM/YY
RTN Cliente  : 0
Cuenta No.   : XXXXXXXX
Propietario  : NOMBRE CLIENTE
─────────────────
Direccion: ...
─────────────────
┌───────────────────┬────┬─────────┐
│ Saldo Anterior    │ L. │  563.28 │
│ Agua Potable      │ L. │  163.67 │
│ Fondo Fuentes...  │ L. │    5.00 │
│ Tasa ERSAPS       │ L. │    3.27 │
├───────────────────┴────┼─────────┤
│              Total L.: │  735.22 │
└───────────────────────────────────┘
···· SETECIENTOS TREINTA Y CINCO CON 22/100 ····
─────────────────
┌─────────────────────────────────┐
│  Sello electronico Sustituye    │
│  Firma y sello Manual del Cajero│
└─────────────────────────────────┘
Cajero      : ERNESTO.C
─────────────────
Fecha Pago  : 17/12/25 10:40:56 AM
─────────────────
Numero Transaccion: 16747469
─────────────────
Generado por: JENNIFER
─────────────────
     Cliente    Copia Caja
```

## Capas a construir

### 1. DTO — `ReciboAbonoDto`
`SIAD.Core/DTOs/Caja/AbonoDtos.cs`
- Encabezado empresa: `EmpresaNombre`, `EmpresaLogoBase64`, `EmpresaLogoMime`
- Datos de recibo: `NumRecibo`, `NumFactura`, `Periodo`, `FechaEmision`, `RtnCliente`, `CuentaNo`, `Propietario`, `Direccion`
- Líneas de cargo: `IReadOnlyList<ReciboAbonoLineaDto>` (Descripcion, Moneda, Monto)
- Totales: `Total`, `TotalEnLetras`
- Pie: `Cajero`, `FechaPago`, `NumeroTransaccion`, `GeneradoPor`

### 2. Service — `GenerarDatosReciboAsync`
`SIAD.Services/Caja/AbonoService.cs` + `IAbonoService.cs`
- Recibe `int transaccionId`
- Query: `transaccion_abonado` → `factura` → `factura_detalle` → `cliente_maestro` → config empresa (logo)
- Convierte total a letras en español (`NumerosALetras` utility)

### 3. Report — `Rpt_Dev_Recibo_Abono`
`SIAD.Reports/Templates/Rpt_Dev_Recibo_Abono.cs` + `Designer.cs`
- XtraReport con `ObjectDataSource` (recibe `ReciboAbonoDto`)
- Ancho: 227 units (≈80mm)
- Secciones: ReportHeader (logo + nombre empresa), Detail (campos + tabla + totales + pie)
- `XRPictureBox` para logo
- `XRTable` para desglose de cargos

### 4. Controller endpoint
`apc/Controllers/AbonoController.cs`
- `GET /api/abono/recibo-pdf/{transaccionId}`
- Llama `GenerarDatosReciboAsync` → alimenta el report → exporta a PDF bytes
- Retorna `FileContentResult("application/pdf")`
- Autorización: `PermissionNames.Ventas.View`

### 5. UI — pestaña en Caja
`apc.Client/Pages/Facturacion/CaptacionPagos/Caja.razor`
- Nueva `<DxTabPage Text="Abonos Especiales">` con `<PosteoAbonos ...>`
- `tabActual` cambia a iniciar en 0 (Lectoras) en lugar de 2

### 6. UI — botón "Imprimir Recibo" en PosteoAbonos
`PosteoAbonos.razor`
- Al mostrar el popup de confirmación, agregar botón "Imprimir Recibo"
- Click → `await JS.InvokeVoidAsync("open", $"/api/abono/recibo-pdf/{ultimoAbono.TransaccionId}", "_blank")`

## Fuentes de datos del recibo
| Campo | Tabla / fuente |
|-------|----------------|
| Logo / Nombre empresa | `adm_empresa` o `CartaEmpresaDto` (ya existe en CobranzaService) |
| No. Recibo, Periodo, Fecha Emisión, RTN | `factura` |
| Cuenta No., Propietario | `cliente_maestro` |
| Dirección | `cliente_maestro` o `cliente_detalle` |
| Líneas de cargo | `factura_detalle.descripcion + montovalor` |
| Cajero, Fecha Pago, No. Transacción | `transaccion_abonado` |
| Generado por | `transaccion_abonado.usuario` |

## Utilidad de números a letras
- Clase estática `NumerosALetras` en `SIAD.Core/Utilities/`
- Formato: "SETECIENTOS TREINTA Y CINCO CON 22/100"

## Permisos
- Endpoint usa `[Authorize(Policy = PermissionNames.Ventas.View)]`
- No se requieren permisos adicionales
