using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Services.Presupuesto;
using apc.Security;

namespace apc.Controllers.Presupuesto;

/// <summary>
/// Consultas del control presupuestario (ejecución, compromisos pendientes, kardex) y el
/// interruptor que lo enciende. Thin: valida, resuelve usuario y delega.
/// <para>
/// El guardado de la configuración es la operación sensible: cambia el comportamiento de la
/// aprobación de órdenes en toda la empresa sin desplegar nada.
/// </para>
/// </summary>
[ApiController]
[Route("api/presupuesto/ejecucion")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public sealed class PresupuestoEjecucionController : ControllerBase
{
    private readonly IPresupuestoEjecucionService _service;

    public PresupuestoEjecucionController(IPresupuestoEjecucionService service)
    {
        _service = service;
    }

    private string Usuario => User?.Identity?.Name ?? "system";

    /// <summary>Ejecución por partida: presupuesto, comprometido, ejecutado, pagado y disponible.</summary>
    [HttpGet]
    public async Task<IActionResult> GetEjecucion(
        [FromQuery] PresupuestoEjecucionFilterDto? filtro, CancellationToken ct)
        => Ok(await _service.ListarEjecucionAsync(filtro, ct));

    /// <summary>Órdenes aprobadas que todavía retienen presupuesto comprometido.</summary>
    [HttpGet("compromisos")]
    public async Task<IActionResult> GetCompromisos(
        [FromQuery] PresupuestoCompromisoFilterDto? filtro, CancellationToken ct)
        => Ok(await _service.ListarCompromisosPendientesAsync(filtro, ct));

    /// <summary>Kardex de una partida: su historia completa, con saldos antes y después.</summary>
    [HttpGet("movimientos")]
    public async Task<IActionResult> GetMovimientos(
        [FromQuery] string idPresupuesto, [FromQuery] string cuenta, CancellationToken ct)
        => Ok(await _service.ListarMovimientosAsync(idPresupuesto, cuenta, ct));

    // ── Exportación ──────────────────────────────────────────────────────────
    // El MISMO XtraReport sirve para los dos formatos: PDF va inline (lo muestra
    // PdfPreviewPopup) y Excel va como descarga. Precedente: AntiguedadSaldosController.

    /// <summary>Ejecución presupuestaria en PDF (inline). Mismos filtros que la pantalla.</summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> GetEjecucionPdf(
        [FromQuery] PresupuestoEjecucionFilterDto? filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionEjecucionAsync(filtro, Usuario, ct);

        using var report = new SIAD.Reports.Rpt_Dev_EjecucionPresupuestaria(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        Response.Headers.ContentDisposition = $"inline; filename=ejecucion_presupuestaria_{Stamp()}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }

    /// <summary>Ejecución presupuestaria en Excel (descarga).</summary>
    [HttpGet("excel")]
    public async Task<IActionResult> GetEjecucionExcel(
        [FromQuery] PresupuestoEjecucionFilterDto? filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionEjecucionAsync(filtro, Usuario, ct);

        using var report = new SIAD.Reports.Rpt_Dev_EjecucionPresupuestaria(datos);
        using var ms = new MemoryStream();
        report.ExportToXlsx(ms);

        return File(ms.ToArray(), ExcelMime, $"ejecucion_presupuestaria_{Stamp()}.xlsx");
    }

    /// <summary>Compromisos pendientes en PDF (inline).</summary>
    [HttpGet("compromisos/pdf")]
    public async Task<IActionResult> GetCompromisosPdf(
        [FromQuery] PresupuestoCompromisoFilterDto? filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionCompromisosAsync(filtro, Usuario, ct);

        using var report = new SIAD.Reports.Rpt_Dev_CompromisosPendientes(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        Response.Headers.ContentDisposition = $"inline; filename=compromisos_pendientes_{Stamp()}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }

    /// <summary>Compromisos pendientes en Excel (descarga).</summary>
    [HttpGet("compromisos/excel")]
    public async Task<IActionResult> GetCompromisosExcel(
        [FromQuery] PresupuestoCompromisoFilterDto? filtro, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionCompromisosAsync(filtro, Usuario, ct);

        using var report = new SIAD.Reports.Rpt_Dev_CompromisosPendientes(datos);
        using var ms = new MemoryStream();
        report.ExportToXlsx(ms);

        return File(ms.ToArray(), ExcelMime, $"compromisos_pendientes_{Stamp()}.xlsx");
    }

    private const string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static string Stamp() => DateTime.Now.ToString("yyyyMMdd_HHmm");

    /// <summary>Modo del control por módulo.</summary>
    [HttpGet("configuracion")]
    public async Task<IActionResult> GetConfiguracion(CancellationToken ct)
        => Ok(await _service.ListarConfiguracionAsync(ct));

    /// <summary>
    /// Enciende o apaga el control de un módulo. Requiere permiso de edición de contabilidad: es
    /// la palanca que hace que las órdenes empiecen a rechazarse.
    /// </summary>
    [HttpPut("configuracion")]
    [ModuleAuthorize(PermissionModules.Contabilidad, PermissionAction.Edit)]
    public async Task<IActionResult> GuardarConfiguracion(
        [FromBody] PresupuestoControlConfigDto dto, CancellationToken ct)
    {
        try
        {
            await _service.GuardarConfiguracionAsync(dto, Usuario, ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
