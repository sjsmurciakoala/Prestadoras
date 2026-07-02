# Abonos Especiales en Caja — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Agregar la pestaña "Abonos Especiales" en `Caja.razor` con generación de recibo PDF (DevExpress XtraReport, 80mm) al confirmar cada abono.

**Architecture:** La lógica de abono ya existe en `AbonoService.RegistrarAbonoAsync` (rebaja saldo, genera partida contable). Se añade (1) un método de servicio que ensambla el DTO de recibo consultando `transaccion_abonado` + `factura` + `factura_detalle` + `cliente_maestro` + `cfg_company`, (2) un `XtraReport` programático 80mm, (3) un endpoint GET que lo exporta a PDF, y (4) la pestaña + botón en la UI.

**Tech Stack:** .NET 9, C# 13, Blazor WASM, DevExpress 25.1.7 (`DevExpress.XtraReports.UI`), Npgsql/EF Core 9, ASP.NET Core MVC.

---

### Task 1: DTOs `ReciboAbonoDto` y `ReciboAbonoLineaDto`

**Files:**
- Modify: `SIAD.Core/DTOs/Caja/AbonoDtos.cs`

**Step 1: Agregar los nuevos DTOs al final del archivo**

Abrir `SIAD.Core/DTOs/Caja/AbonoDtos.cs` y añadir al final:

```csharp
public class ReciboAbonoLineaDto
{
    public string Descripcion { get; set; } = string.Empty;
    public string Moneda { get; set; } = "L.";
    public decimal Monto { get; set; }
}

public class ReciboAbonoDto
{
    // Encabezado empresa
    public string EmpresaNombre { get; set; } = string.Empty;
    public byte[]? EmpresaLogo { get; set; }
    public string? EmpresaLogoMime { get; set; }

    // Datos del recibo / factura
    public int NumRecibo { get; set; }
    public string NumFactura { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string FechaEmision { get; set; } = string.Empty;
    public string RtnCliente { get; set; } = string.Empty;
    public string CuentaNo { get; set; } = string.Empty;
    public string Propietario { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;

    // Líneas de cargo (fuente: factura_detalle)
    public List<ReciboAbonoLineaDto> Lineas { get; set; } = new();

    // Totales
    public decimal Total { get; set; }
    public string TotalEnLetras { get; set; } = string.Empty;

    // Pie del recibo
    public string Cajero { get; set; } = string.Empty;
    public string FechaPago { get; set; } = string.Empty;
    public int NumeroTransaccion { get; set; }
    public string GeneradoPor { get; set; } = string.Empty;
}
```

**Step 2: Verificar que compila**

```powershell
dotnet build Prestadoras/SIAD.Core/SIAD.Core.csproj -c Debug --no-restore 2>&1 | Select-String -Pattern "error|warning" | Select-Object -First 20
```
Resultado esperado: 0 errores.

---

### Task 2: Utilidad `NumerosALetras`

**Files:**
- Create: `SIAD.Core/Utilities/NumerosALetras.cs`

**Step 1: Crear el archivo**

```csharp
namespace SIAD.Core.Utilities;

public static class NumerosALetras
{
    private static readonly string[] Unidades =
    [
        "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS",
        "DIECISIETE", "DIECIOCHO", "DIECINUEVE"
    ];

    private static readonly string[] Decenas =
    [
        "", "DIEZ", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA",
        "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    ];

    private static readonly string[] Centenas =
    [
        "", "CIEN", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    ];

    private static readonly string[] Veintiuno =
    [
        "", "VEINTIÚN", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO",
        "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];

    public static string Convertir(decimal numero)
    {
        var entera = (long)Math.Floor(numero);
        var cents = (int)Math.Round((numero - Math.Floor(numero)) * 100);
        var letras = EnterALetras(entera);
        return $"{letras} CON {cents:D2}/100";
    }

    private static string EnterALetras(long n)
    {
        if (n == 0) return "CERO";
        if (n < 0) return "MENOS " + EnterALetras(-n);

        var resultado = string.Empty;

        if (n >= 1_000_000)
        {
            var millones = n / 1_000_000;
            resultado += millones == 1
                ? "UN MILLÓN "
                : EnterALetras(millones) + " MILLONES ";
            n %= 1_000_000;
        }

        if (n >= 1_000)
        {
            var miles = n / 1_000;
            resultado += miles == 1
                ? "MIL "
                : EnterALetras(miles) + " MIL ";
            n %= 1_000;
        }

        if (n >= 100)
        {
            var c = (int)(n / 100);
            resultado += n == 100 ? "CIEN " : Centenas[c] + " ";
            n %= 100;
        }

        if (n >= 20)
        {
            var d = (int)(n / 10);
            var u = (int)(n % 10);
            if (d == 2 && u > 0)
                resultado += Veintiuno[u] + " ";
            else
                resultado += Decenas[d] + (u > 0 ? " Y " + Unidades[u] : "") + " ";
        }
        else if (n > 0)
        {
            resultado += Unidades[(int)n] + " ";
        }

        return resultado.Trim();
    }
}
```

**Step 2: Compilar**

```powershell
dotnet build Prestadoras/SIAD.Core/SIAD.Core.csproj -c Debug --no-restore 2>&1 | Select-String "error"
```
Resultado esperado: 0 errores.

---

### Task 3: Método `GenerarDatosReciboAsync` en el servicio

**Files:**
- Modify: `SIAD.Services/Caja/IAbonoService.cs`
- Modify: `SIAD.Services/Caja/AbonoService.cs`

**Step 1: Agregar la firma a la interfaz**

En `IAbonoService.cs`, añadir al final de la interfaz:

```csharp
Task<ReciboAbonoDto?> GenerarDatosReciboAsync(int transaccionId, CancellationToken ct = default);
```

Asegurarse de que el `using` de DTOs de Caja ya está: `using SIAD.Core.DTOs.Caja;`

**Step 2: Implementar en `AbonoService.cs`**

Añadir el siguiente `using` en la parte superior de `AbonoService.cs` si no existe:
```csharp
using SIAD.Core.Utilities;
```

Agregar el método dentro de la clase `AbonoService`, justo antes del cierre de la llave de clase:

```csharp
public async Task<ReciboAbonoDto?> GenerarDatosReciboAsync(int transaccionId, CancellationToken ct = default)
{
    var companyId = _currentCompanyService.GetCompanyId();

    var transaccion = await _context.transaccion_abonados
        .AsNoTracking()
        .FirstOrDefaultAsync(t => t.company_id == companyId && t.ide == transaccionId, ct);

    if (transaccion is null)
        return null;

    var numRecibo = (int)(transaccion.recibo ?? 0);

    var factura = await _context.facturas
        .AsNoTracking()
        .FirstOrDefaultAsync(f => f.company_id == companyId && f.numrecibo == numRecibo, ct);

    if (factura is null)
        return null;

    var detalles = await _context.factura_detalles
        .AsNoTracking()
        .Where(d => d.factura_id == factura.id)
        .OrderBy(d => d.id)
        .ToListAsync(ct);

    var cliente = await _context.cliente_maestros
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.maestro_cliente_clave == transaccion.cliente_clave, ct);

    var clienteDetalle = await _context.cliente_detalles
        .AsNoTracking()
        .FirstOrDefaultAsync(d => d.clientecodigo == transaccion.cliente_clave, ct);

    var company = await _context.cfg_companies
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

    var lineas = detalles.Select(d => new ReciboAbonoLineaDto
    {
        Descripcion = d.descripcion ?? string.Empty,
        Moneda = "L.",
        Monto = d.montovalor ?? 0m
    }).ToList();

    var total = lineas.Sum(l => l.Monto);

    return new ReciboAbonoDto
    {
        EmpresaNombre = company?.commercial_name ?? string.Empty,
        EmpresaLogo = company?.logo,
        EmpresaLogoMime = company?.logo_mime,

        NumRecibo = numRecibo,
        NumFactura = factura.numfactura ?? numRecibo.ToString(),
        Periodo = factura.periodo ?? string.Empty,
        FechaEmision = factura.fechaemision?.ToString("dd/MM/yy") ?? string.Empty,
        RtnCliente = factura.rtn ?? "0",
        CuentaNo = transaccion.cliente_clave ?? string.Empty,
        Propietario = cliente?.maestro_cliente_nombre ?? string.Empty,
        Direccion = clienteDetalle?.detalle_cliente_direccion ?? string.Empty,

        Lineas = lineas,
        Total = total,
        TotalEnLetras = NumerosALetras.Convertir(total),

        Cajero = transaccion.usuario ?? string.Empty,
        FechaPago = transaccion.fecha_docu?.ToString("dd/MM/yy") ?? string.Empty,
        NumeroTransaccion = transaccionId,
        GeneradoPor = transaccion.usuario ?? string.Empty
    };
}
```

> **Nota:** `_context.cliente_detalles` puede no estar en el DbContext si `cliente_detalle` no tiene un `DbSet`. Verificar en `SiadDbContext.cs`. Si no existe como DbSet, buscar cómo accede el resto del código a esa tabla y usar la misma vía (puede ser via `_context.cliente_maestros.Include(m => m.cliente_detalles)`).

**Step 3: Compilar**

```powershell
dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug --no-restore 2>&1 | Select-String "error"
```
Resultado esperado: 0 errores.

---

### Task 4: Clase `Rpt_Dev_Recibo_Abono` (XtraReport programático 80mm)

**Files:**
- Create: `SIAD.Reports/Templates/Rpt_Dev_Recibo_Abono.cs`

**Step 1: Crear la clase del reporte**

La clase construye el layout completo en el constructor a partir del DTO (sin archivo `.repx` ni `Designer.cs`):

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Caja;

namespace SIAD.Reports;

public sealed class Rpt_Dev_Recibo_Abono : XtraReport
{
    // 80mm receipt: PageWidth=315, usable content width ~275 units (HundredthsOfInch)
    private const float ContentWidth = 275f;

    public Rpt_Dev_Recibo_Abono(ReciboAbonoDto datos)
    {
        PaperKind = DXPaperKind.Custom;
        PageWidth = 315;
        PageHeight = 2000; // Suficientemente alto; DevExpress trunca al contenido real
        Margins = new PaddingInfo(20, 20, 10, 10);
        DataSource = datos.Lineas;
        RequestParameters = false;

        // -- Parámetros escalares --
        AddParam("EmpresaNombre", datos.EmpresaNombre);
        AddParam("NumRecibo", datos.NumRecibo.ToString());
        AddParam("Periodo", datos.Periodo);
        AddParam("FechaEmision", datos.FechaEmision);
        AddParam("RtnCliente", datos.RtnCliente);
        AddParam("CuentaNo", datos.CuentaNo);
        AddParam("Propietario", datos.Propietario);
        AddParam("Direccion", datos.Direccion);
        AddParam("Total", datos.Total.ToString("N2"));
        AddParam("TotalEnLetras", datos.TotalEnLetras);
        AddParam("Cajero", datos.Cajero);
        AddParam("FechaPago", datos.FechaPago);
        AddParam("NumeroTransaccion", datos.NumeroTransaccion.ToString());
        AddParam("GeneradoPor", datos.GeneradoPor);

        // ============================================================
        // REPORT HEADER: Logo + Nombre empresa + datos del recibo
        // ============================================================
        var rh = new ReportHeaderBand { HeightF = BuildHeaderHeight(datos) };

        float y = 4f;

        // Logo (si existe)
        if (datos.EmpresaLogo is { Length: > 0 })
        {
            var logo = new XRPictureBox
            {
                BoundsF = new RectangleF(87.5f, y, 100f, 60f),
                Sizing = ImageSizeMode.Squeeze,
                Image = LoadImage(datos.EmpresaLogo)
            };
            rh.Controls.Add(logo);
            y += 68f;
        }

        // Nombre empresa
        rh.Controls.Add(CenteredLabel(datos.EmpresaNombre, y, 10f, bold: true));
        y += 16f;

        rh.Controls.Add(Dashed(y)); y += 10f;

        // No. Recibo grande centrado
        rh.Controls.Add(CenteredLabel($"No. Recibo   {datos.NumRecibo}", y, 11f, bold: true));
        y += 18f;

        rh.Controls.Add(Dashed(y)); y += 10f;

        // Campos encabezado
        y = AddRow(rh, "Periodo     :", datos.Periodo, y);
        y = AddRow(rh, "Fecha Emision", datos.FechaEmision, y);
        y = AddRow(rh, "RTN Cliente  :", datos.RtnCliente, y);
        y = AddRow(rh, "Cuenta No.  :", datos.CuentaNo, y);
        y = AddRow(rh, "Propietario :", datos.Propietario, y);

        rh.Controls.Add(Dashed(y)); y += 10f;

        // Dirección (puede ser multilínea)
        var dirLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 28f),
            Font = new DXFont("Courier New", 8f),
            Text = $"Direccion: {datos.Direccion}",
            WordWrap = true,
            Multiline = true
        };
        rh.Controls.Add(dirLabel);
        y += 32f;

        rh.Controls.Add(Dashed(y));
        rh.HeightF = y + 12f;

        // ============================================================
        // DETAIL BAND: una fila por línea de cargo (factura_detalle)
        // ============================================================
        var detail = new DetailBand { HeightF = 20f };

        var tbl = new XRTable { BoundsF = new RectangleF(0f, 0f, ContentWidth, 20f) };
        tbl.BeginInit();
        var row = new XRTableRow();
        row.Cells.Add(CreateCell(null, 150f, "[Descripcion]", TextAlignment.MiddleLeft));
        row.Cells.Add(CreateCell(null, 30f, "[Moneda]", TextAlignment.MiddleCenter));
        row.Cells.Add(CreateCell(null, 95f, "[Monto]", TextAlignment.MiddleRight, "{0:N2}"));
        tbl.Rows.Add(row);
        tbl.EndInit();
        detail.Controls.Add(tbl);

        // ============================================================
        // REPORT FOOTER: Total + letras + sello + cajero + pie
        // ============================================================
        var rf = new ReportFooterBand { HeightF = 220f };
        float fy = 0f;

        // Fila Total
        var totalTable = new XRTable { BoundsF = new RectangleF(0f, fy, ContentWidth, 22f) };
        totalTable.BeginInit();
        var totalRow = new XRTableRow();
        totalRow.Cells.Add(CreateCell(null, 180f, "Total L.:", TextAlignment.MiddleRight, bold: true));
        totalRow.Cells.Add(CreateCell(null, 95f, $"?Total", TextAlignment.MiddleRight, bold: true));
        // Override text with parameter
        var totalCell = new XRTableCell
        {
            WidthF = 95f,
            TextAlignment = TextAlignment.MiddleRight,
            Font = new DXFont("Courier New", 9f, DXFontStyle.Bold)
        };
        totalCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "?Total"));
        totalRow.Cells.Add(totalCell);
        totalTable.Rows.Add(totalRow);
        totalTable.EndInit();
        rf.Controls.Add(totalTable);
        fy += 24f;

        // Total en letras
        rf.Controls.Add(CenteredLabel($"···· {datos.TotalEnLetras} ····", fy, 8f));
        fy += 18f;

        rf.Controls.Add(Dashed(fy)); fy += 10f;

        // Sello electrónico
        var sello = new XRLabel
        {
            BoundsF = new RectangleF(20f, fy, ContentWidth - 40f, 28f),
            Font = new DXFont("Courier New", 8f),
            Text = "Sello electronico Sustituye\nFirma y sello Manual del Cajero",
            TextAlignment = TextAlignment.MiddleCenter,
            Multiline = true,
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        rf.Controls.Add(sello);
        fy += 36f;

        fy = AddRowFooter(rf, "Cajero      :", datos.Cajero, fy);
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        fy = AddRowFooter(rf, "Fecha Pago  :", datos.FechaPago, fy);
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        fy = AddRowFooter(rf, "Num. Trans. :", datos.NumeroTransaccion.ToString(), fy);
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        fy = AddRowFooter(rf, "Generado por:", datos.GeneradoPor, fy);

        // Pie
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        rf.Controls.Add(CenteredLabel("Cliente        Copia Caja", fy, 8f));
        fy += 16f;

        rf.HeightF = fy + 4f;

        Bands.AddRange([rh, detail, rf]);
        StyleSheet.Add(new XRControlStyle
        {
            Name = "DetailOddStyle",
            BackColor = Color.FromArgb(245, 245, 245)
        });
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static float BuildHeaderHeight(ReciboAbonoDto datos)
    {
        var h = 4f;
        if (datos.EmpresaLogo is { Length: > 0 }) h += 68f;
        h += 16f + 10f + 18f + 10f; // nombre + dashed + recibo + dashed
        h += 5 * 14f;               // 5 filas de encabezado
        h += 10f + 32f + 12f;       // dashed + direccion + padding
        return h;
    }

    private void AddParam(string name, string value)
    {
        var p = new DevExpress.XtraReports.Parameters.Parameter
        {
            Name = name,
            Type = typeof(string),
            Value = value,
            Visible = false
        };
        Parameters.Add(p);
    }

    private static XRLabel CenteredLabel(string text, float y, float fontSize, bool bold = false)
        => new()
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 14f),
            Font = new DXFont("Courier New", fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            Text = text,
            TextAlignment = TextAlignment.MiddleCenter
        };

    private static XRLine Dashed(float y)
        => new()
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 4f),
            LineStyle = DashStyle.Dash,
            LineColor = Color.Gray
        };

    private static float AddRow(Band band, string label, string value, float y)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, y, 130f, 14f),
            Font = new DXFont("Courier New", 8f),
            Text = label,
            ForeColor = Color.DimGray
        });
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(132f, y, 143f, 14f),
            Font = new DXFont("Courier New", 8f, DXFontStyle.Bold),
            Text = value,
            TextAlignment = TextAlignment.MiddleRight
        });
        return y + 14f;
    }

    private static float AddRowFooter(Band band, string label, string value, float y)
        => AddRow(band, label, value, y) + 2f;

    private static XRTableCell CreateCell(
        XRTableRow? _, float width, string bindingExpr, TextAlignment align,
        string? format = null, bool bold = false)
    {
        var cell = new XRTableCell
        {
            WidthF = width,
            TextAlignment = align,
            Font = new DXFont("Courier New", 8f, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        var eb = new ExpressionBinding("BeforePrint", "Text", bindingExpr);
        if (format is not null) eb.FormatString = format;
        cell.ExpressionBindings.Add(eb);
        return cell;
    }

    private static System.Drawing.Image? LoadImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        using var ms = new System.IO.MemoryStream(bytes);
        return System.Drawing.Image.FromStream(ms);
    }
}
```

> **Nota importante sobre el `Total` en el footer:** La fila total en el `ReportFooterBand` no está vinculada a una expresión de suma del DataSource sino al parámetro `?Total` que ya tiene el valor precalculado en el DTO. Si DevExpress no expande `?Total` en la celda de tabla, usar directamente `datos.Total.ToString("N2")` como texto de la celda en el constructor.

**Step 2: Compilar**

```powershell
dotnet build Prestadoras/SIAD.Reports/SIAD.Reports.csproj -c Debug --no-restore 2>&1 | Select-String "error"
```
Resultado esperado: 0 errores.

---

### Task 5: Endpoint `GET /api/abono/recibo-pdf/{transaccionId}`

**Files:**
- Modify: `apc/Controllers/AbonoController.cs`

**Step 1: Agregar el using de Reports**

En la parte superior de `AbonoController.cs` añadir:
```csharp
using SIAD.Reports;
```

**Step 2: Agregar el action al controlador**

Dentro de la clase `AbonoController`, después del action `arqueo`:

```csharp
[HttpGet("recibo-pdf/{transaccionId:int}")]
public async Task<IActionResult> GetReciboPdf(int transaccionId, CancellationToken ct)
{
    var datos = await _abonoService.GenerarDatosReciboAsync(transaccionId, ct);
    if (datos is null)
        return NotFound(new { mensaje = "No se encontró la transacción indicada." });

    using var report = new Rpt_Dev_Recibo_Abono(datos);
    report.RequestParameters = false;

    using var stream = new MemoryStream();
    report.ExportToPdf(stream);

    var fileName = $"Recibo-{datos.NumRecibo}-{transaccionId}.pdf";
    return File(stream.ToArray(), "application/pdf", fileName);
}
```

**Step 3: Compilar solución completa**

```powershell
dotnet build Prestadoras/HODSOFT_DEVEXPRESS.sln -c Debug 2>&1 | Select-String "error" | Select-Object -First 30
```
Resultado esperado: 0 errores.

---

### Task 6: Nueva pestaña en `Caja.razor`

**Files:**
- Modify: `apc.Client/Pages/Facturacion/CaptacionPagos/Caja.razor`

**Step 1: Agregar el `using` de PosteoAbonos**

`PosteoAbonos.razor` está en el mismo namespace de la carpeta — no se requiere `@using` adicional si ya está el de `CaptacionPagos`.

Verificar que en `_Imports.razor` de `apc.Client` (o en el archivo de la página) existe:
```razor
@using apc.Client.Pages.Facturacion.CaptacionPagos
```
Si no existe, agregarlo.

**Step 2: Añadir la nueva pestaña**

En `Caja.razor`, localizar el bloque de pestañas:
```razor
<DxTabs @bind-ActiveTabIndex="tabActual" RenderStyle="TabsRenderStyle.Default">
    <DxTabPage Text="?? Lectoras">
        ...
    </DxTabPage>
    <DxTabPage Text="??? Miscel&aacute;neos">
        ...
    </DxTabPage>
    <DxTabPage Text="? Manual">
        ...
    </DxTabPage>
</DxTabs>
```

Agregar la nueva pestaña **después de "Manual"**:
```razor
    <DxTabPage Text="Abonos Especiales">
        <div class="p-3">
            <PosteoAbonos Bancos="@bancos"
                          Clientes="@clientes"
                          UsuarioActual="@usuarioActual" />
        </div>
    </DxTabPage>
```

**Step 3: Verificar `tabActual`**

En el bloque `@code`, `tabActual` está inicializado en `2` (Manual). Dejarlo en `2` para no cambiar el comportamiento por defecto.

---

### Task 7: Botón "Imprimir Recibo" en `PosteoAbonos.razor`

**Files:**
- Modify: `apc.Client/Pages/Facturacion/CaptacionPagos/PosteoAbonos.razor`

**Step 1: Inyectar `IJSRuntime`**

Al inicio del archivo, después de las directivas `@inject` existentes, agregar:
```razor
@inject IJSRuntime JS
```

**Step 2: Agregar botón en el `FooterContentTemplate` del popup de confirmación**

Localizar el `FooterContentTemplate` del `DxPopup` en `PosteoAbonos.razor`:
```razor
<FooterContentTemplate Context="popFooter">
    <div class="d-flex justify-content-end w-100">
        <DxButton Text="Entendido" RenderStyle="ButtonRenderStyle.Primary" Click="@(() => ultimoAbono = null)" />
    </div>
</FooterContentTemplate>
```

Reemplazar por:
```razor
<FooterContentTemplate Context="popFooter">
    <div class="d-flex justify-content-end gap-2 w-100">
        <DxButton Text="Imprimir Recibo"
                  RenderStyle="ButtonRenderStyle.Secondary"
                  IconCssClass="bi bi-printer"
                  Click="ImprimirRecibo" />
        <DxButton Text="Entendido"
                  RenderStyle="ButtonRenderStyle.Primary"
                  Click="@(() => ultimoAbono = null)" />
    </div>
</FooterContentTemplate>
```

**Step 3: Agregar el método `ImprimirRecibo` en `@code`**

En el bloque `@code`, al final, agregar:
```csharp
private async Task ImprimirRecibo()
{
    if (ultimoAbono is null) return;
    await JS.InvokeVoidAsync("open", $"/api/abono/recibo-pdf/{ultimoAbono.TransaccionId}", "_blank");
}
```

**Step 4: Build final**

```powershell
dotnet build Prestadoras/HODSOFT_DEVEXPRESS.sln -c Debug 2>&1 | Select-String "error" | Select-Object -First 30
```
Resultado esperado: 0 errores de compilación.

**Step 5: Prueba manual del flujo completo**

1. Iniciar la aplicación: `dotnet run --project Prestadoras/apc/apc.csproj`
2. Navegar a `/facturacion/captacion/caja`
3. Confirmar que aparece la pestaña "Abonos Especiales"
4. Buscar una factura con saldo pendiente, ingresar monto, confirmar
5. En el popup de confirmación, hacer clic en "Imprimir Recibo"
6. Verificar que se abre una nueva pestaña con el PDF en formato 80mm
7. Confirmar que el PDF contiene: logo, nombre empresa, datos del recibo, desglose de cargos, total en letras, cajero, fecha pago, número de transacción

---

## Notas de implementación

### Si `cliente_detalles` no está como DbSet
Buscar en `SiadDbContext.cs` el nombre del DbSet. Si está como navegación de `cliente_maestro`, usar:
```csharp
var clienteConDetalle = await _context.cliente_maestros
    .AsNoTracking()
    .Include(m => m.cliente_detalles)
    .FirstOrDefaultAsync(c => c.maestro_cliente_clave == transaccion.cliente_clave, ct);
var direccion = clienteConDetalle?.cliente_detalles.FirstOrDefault()?.detalle_cliente_direccion ?? "";
```

### Si `?Total` no funciona como parámetro en `XRTableCell`
Setear el texto directamente (ya calculado en el DTO):
```csharp
totalCell.Text = datos.Total.ToString("N2");
```
Y remover el `ExpressionBinding` de esa celda.

### Ajuste de altura del `ReportFooterBand`
Si el footer queda cortado, aumentar `rf.HeightF` en el paso de cálculo o establecer manualmente un valor mayor como `400f`.

### Permisos del endpoint
El endpoint hereda `[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Caja)]` del controlador — no se requiere ningún cambio adicional de permisos.
