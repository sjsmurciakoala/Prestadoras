using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/kardex")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class KardexController : ControllerBase
{
    private readonly IKardexService _service;

    public KardexController(IKardexService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] KardexFilterDto filtro, CancellationToken ct)
    {
        if (!filtro.ArticuloId.HasValue && string.IsNullOrWhiteSpace(filtro.CodigoArticulo))
        {
            return BadRequest(new { message = "Debe indicar el artículo." });
        }

        var kardex = await _service.GetByArticuloAsync(filtro, ct);
        return kardex is null ? NotFound() : Ok(kardex);
    }

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos(CancellationToken ct)
    {
        var tipos = await _service.GetTiposMovimientoAsync(ct);
        return Ok(tipos);
    }

    // ── Libro de movimientos de bodega (todos los artículos de una bodega) ────

    [HttpGet("bodega")]
    public async Task<IActionResult> GetMovimientosBodega(
        [FromQuery] MovimientosBodegaFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        if (!filtro.BodegaId.HasValue)
        {
            return BadRequest(new { message = "Debe indicar la bodega." });
        }

        // Sin tope superior a propósito (mismo criterio que el maestro): al exportar a Excel
        // el grid pide todas las filas de una vez y un clamp truncaría el archivo en silencio.
        if (take <= 0) take = 50;

        var result = await _service.GetMovimientosBodegaPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    [HttpGet("bodega/resumen")]
    public async Task<IActionResult> GetResumenBodega([FromQuery] MovimientosBodegaFilterDto filtro, CancellationToken ct)
    {
        if (!filtro.BodegaId.HasValue)
        {
            return BadRequest(new { message = "Debe indicar la bodega." });
        }

        var resumen = await _service.GetResumenBodegaAsync(filtro, ct);
        return Ok(resumen);
    }

    // ── PDF imprimible ────────────────────────────────────────────────────────

    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdfArticulo([FromQuery] KardexFilterDto filtro, CancellationToken ct)
    {
        if (!filtro.ArticuloId.HasValue && string.IsNullOrWhiteSpace(filtro.CodigoArticulo))
        {
            return BadRequest(new { message = "Debe indicar el artículo." });
        }

        var datos = await _service.GetDatosImpresionArticuloAsync(filtro, User?.Identity?.Name ?? "sistema", ct);

        using var report = new Rpt_Dev_Movimientos_Kardex(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition = $"inline; filename=kardex_{stamp}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("bodega/pdf")]
    public async Task<IActionResult> GetPdfBodega([FromQuery] MovimientosBodegaFilterDto filtro, CancellationToken ct)
    {
        if (!filtro.BodegaId.HasValue)
        {
            return BadRequest(new { message = "Debe indicar la bodega." });
        }

        var datos = await _service.GetDatosImpresionBodegaAsync(filtro, User?.Identity?.Name ?? "sistema", ct);

        using var report = new Rpt_Dev_Movimientos_Kardex(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition = $"inline; filename=movimientos_bodega_{stamp}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }
}
