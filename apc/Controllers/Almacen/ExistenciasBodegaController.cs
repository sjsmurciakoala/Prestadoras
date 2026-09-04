using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/existencias-bodega")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class ExistenciasBodegaController : ControllerBase
{
    private readonly IExistenciasBodegaService _service;

    public ExistenciasBodegaController(IExistenciasBodegaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ExistenciasBodegaFilterDto filtro, CancellationToken ct)
    {
        var items = await _service.GetAsync(filtro, ct);
        return Ok(items);
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf([FromQuery] ExistenciasBodegaFilterDto filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(filtro, User?.Identity?.Name ?? "sistema", ct);

        using var report = new Rpt_Dev_Existencias_Bodega(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition = $"inline; filename=existencias_bodega_{stamp}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }
}
