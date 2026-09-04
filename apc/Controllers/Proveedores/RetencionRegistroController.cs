using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Reports;
using SIAD.Services.Retenciones;
using apc.Security;

namespace apc.Controllers.Proveedores;

/// <summary>
/// Consulta del registro fiscal de retenciones aplicadas (F4): libro prv_retencion_hdr/dtl.
/// <para>
/// Módulo de permisos: <c>proveedores</c>, recurso FINO <c>retenciones</c>
/// (<see cref="PermissionResources.Proveedores.Retenciones"/>). Solo lectura (GET→View); el registro
/// lo escribe el flujo de pago (OrdenesPagoDirecto). El filtro por empresa lo aplica el tenant.
/// </para>
/// </summary>
[ApiController]
[Route("api/proveedores/retenciones")]
[ModuleAuthorize(PermissionModules.Proveedores, PermissionResources.Proveedores.Retenciones)]
public sealed class RetencionRegistroController : ControllerBase
{
    private readonly IRetencionRegistroService _service;

    public RetencionRegistroController(IRetencionRegistroService service) => _service = service;

    /// <summary>Lista paginada/filtrada del registro (proveedor, fechas, estado, folio, búsqueda libre).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RetencionRegistroFilterDto filtro, CancellationToken ct)
        => Ok(await _service.BuscarAsync(filtro, ct));

    /// <summary>Detalle (cabecera + líneas) de una retención por su id.</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var detalle = await _service.GetDetalleAsync(id, ct);
        return detalle is null ? NotFound() : Ok(detalle);
    }

    /// <summary>
    /// Constancia de retención en PDF (inline) por id de cabecera (F5). Reimpresión permitida: solo
    /// lee el libro fiscal, no cambia estado. El PDF se numera con el folio interno.
    /// </summary>
    [HttpGet("{id:long}/constancia/pdf")]
    public async Task<IActionResult> GetConstanciaPdf(long id, CancellationToken ct)
    {
        var datos = await _service.GetDatosConstanciaAsync(id, User.Identity?.Name, ct);
        if (datos is null)
            return NotFound();

        using var report = new Rpt_Dev_Constancia_Retencion(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);
        Response.Headers.ContentDisposition = $"inline; filename=Constancia-Retencion-{datos.Folio}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }

    /// <summary>
    /// Reporte mensual para la declaración (F5): filas a nivel de detalle en el rango de fechas. El
    /// front las agrupa por tipo/proveedor; el total a declarar suma las Vigentes.
    /// </summary>
    [HttpGet("declaracion")]
    public async Task<IActionResult> GetDeclaracion([FromQuery] RetencionDeclaracionFilterDto filtro, CancellationToken ct)
        => Ok(await _service.BuscarDeclaracionAsync(filtro, ct));

    /// <summary>
    /// PDF (inline) del reporte mensual para la declaración (F5.1): mismo filtro que <c>/declaracion</c>,
    /// agrupado por tipo→proveedor con subtotales. El total refleja el filtro (Vigentes ⇒ a declarar).
    /// </summary>
    [HttpGet("declaracion/pdf")]
    public async Task<IActionResult> GetDeclaracionPdf([FromQuery] RetencionDeclaracionFilterDto filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosDeclaracionImpresionAsync(filtro, User?.Identity?.Name ?? "sistema", ct);

        using var report = new Rpt_Dev_Retenciones_Declaracion(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition = $"inline; filename=retenciones_declaracion_{stamp}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }
}
