using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Services.Cobranza;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/corte-masivo")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Cobranza)]
public class CorteMasivoController : ControllerBase
{
    private readonly ICorteMasivoService _service;

    public CorteMasivoController(ICorteMasivoService service)
        => _service = service;

    // POST api/corte-masivo
    [HttpPost]
    public async Task<IActionResult> Generar(
        [FromBody] GenerarCorteMasivoRequest request, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        var hdr = await _service.GenerarAsync(request, usuario, ct);
        return Ok(hdr);
    }

    // GET api/corte-masivo
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    // GET api/corte-masivo/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerDetalle(int id, CancellationToken ct)
    {
        var detalle = await _service.ObtenerDetalleAsync(id, ct);
        return detalle is null ? NotFound() : Ok(detalle);
    }

    // GET api/corte-masivo/{id}/reimprimir
    [HttpGet("{id:int}/reimprimir")]
    public async Task<IActionResult> ObtenerParaReimpresion(int id, CancellationToken ct)
    {
        var detalle = await _service.ObtenerParaReimpresionAsync(id, ct);
        return detalle is null ? NotFound() : Ok(detalle);
    }

    // GET api/corte-masivo/{id}/imprimir → HTML para imprimir (completo o solo sin pago)
    [HttpGet("{id:int}/imprimir")]
    public async Task<IActionResult> Imprimir(int id,
        [FromQuery] bool soloSinPago = false,
        CancellationToken ct = default)
    {
        var detalle = soloSinPago
            ? await _service.ObtenerParaReimpresionAsync(id, ct)
            : await _service.ObtenerDetalleAsync(id, ct);

        if (detalle is null) return NotFound();

        var hdr    = detalle.Encabezado;
        var es     = System.Globalization.CultureInfo.GetCultureInfo("es-HN");
        Func<string, string> enc = s => System.Net.WebUtility.HtmlEncode(s);
        var titulo = soloSinPago
            ? $"Corte {hdr.Correlativo} — Clientes sin pago"
            : $"Listado de corte — {hdr.Correlativo}";

        var html = new System.Text.StringBuilder();

        // HEAD + CSS (sin raw-string para evitar conflictos con {{ en CSS)
        html.Append("<!DOCTYPE html><html lang=\"es\"><head>")
            .Append("<meta charset=\"utf-8\"/>")
            .Append($"<title>{enc(titulo)}</title>")
            .Append("<style>")
            .Append("body{font-family:Arial,sans-serif;font-size:11pt;margin:15mm 10mm;color:#222}")
            .Append("h1{font-size:14pt;margin:0 0 4px 0}")
            .Append(".sub{font-size:10pt;color:#555;margin-bottom:14px}")
            .Append(".meta{display:flex;gap:30px;margin-bottom:12px;font-size:10pt}")
            .Append("table{width:100%;border-collapse:collapse;font-size:10pt}")
            .Append("th{background:#2c3e50;color:#fff;padding:6px 8px;text-align:left}")
            .Append("td{padding:5px 8px;border-bottom:1px solid #ddd}")
            .Append("tr:nth-child(even) td{background:#f8f9fa}")
            .Append("tfoot td{border-top:2px solid #2c3e50;font-weight:bold;background:#eaf0fb}")
            .Append(".no-print{margin-bottom:16px}")
            .Append("@media print{.no-print{display:none}body{margin:10mm}tr{page-break-inside:avoid}}")
            .Append("</style></head><body>");

        // Botón imprimir
        html.Append("<div class=\"no-print\">")
            .Append("<button onclick=\"window.print()\" style=\"padding:6px 18px;font-size:11pt;cursor:pointer;\">")
            .Append("🖨️ Imprimir / Guardar PDF</button></div>");

        // Encabezado del reporte
        html.Append($"<h1>{enc(titulo)}</h1>")
            .Append($"<p class=\"sub\">Cortes masivos &mdash; generado el {DateTime.Today:dd/MM/yyyy}</p>")
            .Append("<div class=\"meta\">")
            .Append($"<span><b>Correlativo:</b> {enc(hdr.Correlativo)}</span>")
            .Append($"<span><b>Fecha:</b> {hdr.FechaGeneracion:dd/MM/yyyy}</span>")
            .Append($"<span><b>Filtros:</b> {enc(hdr.Criterio ?? string.Empty)}</span>")
            .Append($"<span><b>Total clientes:</b> {detalle.Clientes.Count}</span>")
            .Append("</div>");

        // Tabla
        html.Append("<table><thead><tr>")
            .Append("<th style=\"width:40px\">N&deg;</th>")
            .Append("<th style=\"width:90px\">Clave</th>")
            .Append("<th>Nombre</th>")
            .Append("<th style=\"width:120px;text-align:right\">Saldo</th>")
            .Append("<th style=\"width:90px;text-align:right\">D&iacute;as s/pago</th>")
            .Append("<th style=\"width:65px;text-align:center\">Pagado</th>")
            .Append("</tr></thead><tbody>");

        int n = 1;
        decimal totalSaldo = 0m;
        foreach (var c in detalle.Clientes)
        {
            var saldo = c.SaldoAdeudado ?? 0m;
            totalSaldo += saldo;
            html.Append("<tr>")
                .Append($"<td style=\"text-align:center\">{n++}</td>")
                .Append($"<td>{enc(c.ClienteClave)}</td>")
                .Append($"<td>{enc(c.NombreCliente ?? string.Empty)}</td>")
                .Append($"<td style=\"text-align:right\">{saldo.ToString("N2", es)}</td>")
                .Append($"<td style=\"text-align:right\">{c.DiasSinPago ?? 0}</td>")
                .Append($"<td style=\"text-align:center\">{(c.Pagado ? "Sí" : "No")}</td>")
                .Append("</tr>");
        }

        html.Append("</tbody><tfoot><tr>")
            .Append("<td colspan=\"3\" style=\"text-align:right\">TOTAL:</td>")
            .Append($"<td style=\"text-align:right\">{totalSaldo.ToString("N2", es)}</td>")
            .Append("<td colspan=\"2\"></td>")
            .Append("</tr></tfoot></table></body></html>");

        return Content(html.ToString(), "text/html", System.Text.Encoding.UTF8);
    }

    // GET api/corte-masivo/{id}/excel  → Excel listado de clientes (completo o solo sin pago)
    [HttpGet("{id:int}/excel")]
    public async Task<IActionResult> ExportarExcel(int id,
        [FromQuery] bool soloSinPago = false,
        CancellationToken ct = default)
    {
        var detalle = soloSinPago
            ? await _service.ObtenerParaReimpresionAsync(id, ct)
            : await _service.ObtenerDetalleAsync(id, ct);

        if (detalle is null) return NotFound();

        var hdr = detalle.Encabezado;
        var es  = System.Globalization.CultureInfo.GetCultureInfo("es-HN");

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Listado de Corte");

        // ── Encabezado del lote ──────────────────────────────────────────
        ws.Cell(1, 1).Value = "Correlativo:";
        ws.Cell(1, 2).Value = hdr.Correlativo;
        ws.Cell(2, 1).Value = "Período:";
        ws.Cell(2, 2).Value = hdr.PeriodoAnio.HasValue
            ? $"{hdr.PeriodoMes:D2}/{hdr.PeriodoAnio}"
            : hdr.FechaGeneracion.ToString("MM/yyyy");
        ws.Cell(3, 1).Value = "Filtros:";
        ws.Cell(3, 2).Value = hdr.Criterio;
        ws.Cell(4, 1).Value = "Total clientes:";
        ws.Cell(4, 2).Value = hdr.TotalClientes;
        ws.Range(1, 1, 4, 1).Style.Font.Bold = true;

        // ── Cabeceras de columnas ────────────────────────────────────────
        int colRow = 6;
        string[] headers = ["N°", "Clave", "Nombre", "Saldo Adeudado", "Días sin pago", "Pagado"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(colRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2c3e50");
            cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
        }

        // ── Filas de datos ───────────────────────────────────────────────
        int dataRow = colRow + 1;
        int n = 1;
        decimal totalSaldo = 0m;
        foreach (var c in detalle.Clientes)
        {
            var saldo = c.SaldoAdeudado ?? 0m;
            totalSaldo += saldo;
            ws.Cell(dataRow, 1).Value = n++;
            ws.Cell(dataRow, 2).Value = c.ClienteClave;
            ws.Cell(dataRow, 3).Value = c.NombreCliente ?? string.Empty;
            ws.Cell(dataRow, 4).Value = (double)saldo;
            ws.Cell(dataRow, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(dataRow, 5).Value = c.DiasSinPago ?? 0;
            ws.Cell(dataRow, 6).Value = c.Pagado ? "Sí" : "No";

            if (dataRow % 2 == 0)
                ws.Row(dataRow).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f8f9fa");

            dataRow++;
        }

        // ── Fila de totales ──────────────────────────────────────────────
        ws.Cell(dataRow, 3).Value = "TOTAL:";
        ws.Cell(dataRow, 3).Style.Font.Bold = true;
        ws.Cell(dataRow, 3).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Right;
        ws.Cell(dataRow, 4).Value = (double)totalSaldo;
        ws.Cell(dataRow, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(dataRow, 4).Style.Font.Bold = true;
        ws.Row(dataRow).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#eaf0fb");

        ws.Columns().AdjustToContents();

        var fileName = soloSinPago
            ? $"corte-{hdr.Correlativo}-sin-pago.xlsx"
            : $"corte-{hdr.Correlativo}.xlsx";

        using var stream = new System.IO.MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // GET api/corte-masivo/{id}/comparativo-excel
    [HttpGet("{id:int}/comparativo-excel")]
    public async Task<IActionResult> ExportarComparativoExcel(int id, CancellationToken ct = default)
    {
        var detalle = await _service.ObtenerDetalleAsync(id, ct);
        if (detalle is null) return NotFound();

        var hdr = detalle.Encabezado;

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Comparativo");

        // Encabezado del lote
        ws.Cell(1, 1).Value = "Correlativo:";  ws.Cell(1, 2).Value = hdr.Correlativo;
        ws.Cell(2, 1).Value = "Período:";      ws.Cell(2, 2).Value = hdr.PeriodoAnio.HasValue
            ? $"{hdr.PeriodoMes:D2}/{hdr.PeriodoAnio}"
            : hdr.FechaGeneracion.ToString("MM/yyyy");
        ws.Cell(3, 1).Value = "Filtros:";      ws.Cell(3, 2).Value = hdr.Criterio;
        ws.Range(1, 1, 3, 1).Style.Font.Bold = true;

        // Cabeceras
        int colRow = 5;
        string[] headers = ["N°", "Clave", "Nombre", "Monto Corte", "Pagado", "Diferencia"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(colRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#2c3e50");
            cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            cell.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
        }

        // Datos
        int dataRow = colRow + 1;
        int n = 1;
        decimal totalCorte = 0m, totalDif = 0m;
        foreach (var c in detalle.Clientes)
        {
            var montoCorte  = c.SaldoAdeudado ?? 0m;
            var montoPagado = c.Pagado ? montoCorte : 0m;
            var diferencia  = montoCorte - montoPagado;
            totalCorte += montoCorte;
            totalDif   += diferencia;

            ws.Cell(dataRow, 1).Value = n++;
            ws.Cell(dataRow, 2).Value = c.ClienteClave;
            ws.Cell(dataRow, 3).Value = c.NombreCliente ?? string.Empty;
            ws.Cell(dataRow, 4).Value = (double)montoCorte;
            ws.Cell(dataRow, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(dataRow, 5).Value = c.Pagado ? "Sí" : "No";
            ws.Cell(dataRow, 6).Value = (double)diferencia;
            ws.Cell(dataRow, 6).Style.NumberFormat.Format = "#,##0.00";

            if (dataRow % 2 == 0)
                ws.Row(dataRow).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#f8f9fa");

            dataRow++;
        }

        // Totales
        ws.Cell(dataRow, 3).Value = "TOTAL:";
        ws.Cell(dataRow, 3).Style.Font.Bold = true;
        ws.Cell(dataRow, 3).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Right;
        ws.Cell(dataRow, 4).Value = (double)totalCorte;
        ws.Cell(dataRow, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(dataRow, 4).Style.Font.Bold = true;
        ws.Cell(dataRow, 6).Value = (double)totalDif;
        ws.Cell(dataRow, 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(dataRow, 6).Style.Font.Bold = true;
        ws.Row(dataRow).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#eaf0fb");

        ws.Columns().AdjustToContents();

        using var stream = new System.IO.MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"comparativo-corte-{hdr.Correlativo}.xlsx");
    }
}
