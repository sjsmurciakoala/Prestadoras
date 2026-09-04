using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/valuacion-inventario")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class ValuacionInventarioController : ControllerBase
{
    private readonly IValuacionInventarioService _service;

    public ValuacionInventarioController(IValuacionInventarioService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ValuacionInventarioFilterDto filtro, CancellationToken ct)
    {
        var items = await _service.GetAsync(filtro, ct);
        return Ok(items);
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf([FromQuery] ValuacionInventarioFilterDto filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(filtro, User?.Identity?.Name ?? "sistema", ct);

        // Reutiliza el reporte de existencias por bodega (mismas columnas/agrupación); el título
        // "VALUACIÓN DE INVENTARIO AL ..." y el texto de corte vienen en el DTO de impresión.
        using var report = new Rpt_Dev_Existencias_Bodega(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition = $"inline; filename=valuacion_inventario_{stamp}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }
}
