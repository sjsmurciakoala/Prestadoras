using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Pagos a proveedores: la vista unificada de cuentas por pagar de compra (contado + crédito)
/// y el registro/anulación de abonos con movimiento bancario. Thin: valida, resuelve usuario
/// y delega en <see cref="ICompraCxpService"/>.
/// </summary>
[ApiController]
[Route("api/almacen/pagos-compra")]
[ModuleAuthorize(PermissionModules.Compras)]
public sealed class PagosCompraController : ControllerBase
{
    private readonly ICompraCxpService _service;

    public PagosCompraController(ICompraCxpService service) => _service = service;

    private string Usuario => User?.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CompraCxpFilterDto filtro, CancellationToken ct)
        => Ok(await _service.ListarAsync(filtro, ct));

    [HttpGet("{cxpId:int}/abonos")]
    public async Task<IActionResult> GetAbonos(int cxpId, CancellationToken ct)
        => Ok(await _service.ListarAbonosAsync(cxpId, ct));

    [HttpGet("cuentas-bancarias")]
    public async Task<IActionResult> GetCuentasBancarias(CancellationToken ct)
        => Ok(await _service.ListarCuentasBancariasAsync(ct));

    [HttpGet("cuentas-contables")]
    public async Task<IActionResult> GetCuentasContables(CancellationToken ct)
        => Ok(await _service.ListarCuentasContablesAsync(ct));

    [HttpGet("contabilidad-activa")]
    public async Task<IActionResult> GetContabilidadActiva(CancellationToken ct)
        => Ok(new CompraContabilidadEstadoDto { ContabilidadActiva = await _service.ObtenerContabilidadActivaAsync(ct) });

    [HttpGet("{cxpId:int}/abonos/{numeroAbono:int}/partida")]
    public async Task<IActionResult> GetPartidaAbono(int cxpId, int numeroAbono, CancellationToken ct)
    {
        var partida = await _service.ObtenerPartidaAbonoAsync(cxpId, numeroAbono, ct);
        return partida is null ? NoContent() : Ok(partida);
    }

    /// <summary>Comprobante de pago (PDF, inline) del abono: el documento operativo del egreso.</summary>
    [HttpGet("{cxpId:int}/abonos/{numeroAbono:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePagoPdf(int cxpId, int numeroAbono, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionPagoAsync(cxpId, numeroAbono, Usuario, ct);
        if (datos is null) return NotFound();

        using var report = new Rpt_Dev_Comprobante_PagoCompra(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);
        Response.Headers.ContentDisposition = $"inline; filename=Pago-{cxpId}-{numeroAbono}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    /// <summary>Comprobante de la partida contable (PDF, inline) del pago; 404 si el pago no generó asiento.</summary>
    [HttpGet("{cxpId:int}/abonos/{numeroAbono:int}/partida/pdf")]
    public async Task<IActionResult> GetPartidaPagoPdf(int cxpId, int numeroAbono, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionPartidaPagoAsync(cxpId, numeroAbono, Usuario, ct);
        if (datos is null) return NotFound();

        using var report = new Rpt_Dev_Partida_Contable(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);
        Response.Headers.ContentDisposition = $"inline; filename=Partida-Pago-{cxpId}-{numeroAbono}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    [HttpPost("{cxpId:int}/abonos")]
    public async Task<IActionResult> RegistrarAbono(int cxpId, [FromBody] CompraCxpAbonoUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.RegistrarAbonoAsync(cxpId, dto, Usuario, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Anula un pago: reversa el movimiento bancario y reabre la cuenta por pagar. POST pero se
    /// autoriza como Edit (cambia el estado de un documento existente).
    /// </summary>
    [HttpPost("{cxpId:int}/abonos/{numeroAbono:int}/anular")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> AnularAbono(int cxpId, int numeroAbono, [FromBody] AnularPagoRequest? request, CancellationToken ct)
    {
        try
        {
            var ok = await _service.AnularAbonoAsync(cxpId, numeroAbono, request?.Motivo ?? string.Empty, Usuario, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public sealed class AnularPagoRequest
    {
        public string? Motivo { get; set; }
    }
}
