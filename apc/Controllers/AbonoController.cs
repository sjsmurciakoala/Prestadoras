using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Caja;
using SIAD.Services.Caja;
using apc.Security;
using SIAD.Core.Constants;
using SIAD.Reports;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Caja)]
public class AbonoController : ControllerBase
{
    private readonly IAbonoService _abonoService;

    public AbonoController(IAbonoService abonoService)
    {
        _abonoService = abonoService;
    }

    [HttpGet("buscar-facturas")]
    public async Task<IActionResult> BuscarFacturas([FromQuery] string term, CancellationToken ct)
    {
        var result = await _abonoService.BuscarFacturasConSaldoAsync(term, ct);
        return Ok(result);
    }

    [HttpGet("facturas-por-cliente")]
    public async Task<IActionResult> FacturasPorCliente([FromQuery] string clienteClave, CancellationToken ct)
    {
        var result = await _abonoService.ListarFacturasPendientesPorClienteAsync(clienteClave, ct);
        return Ok(result);
    }

    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar([FromBody] AbonoCrearDto request, CancellationToken ct)
    {
        request.Usuario = User?.Identity?.Name ?? "system";
        var result = await _abonoService.RegistrarAbonoAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("arqueo")]
    public async Task<IActionResult> ListarAbonosDelDia([FromQuery] string? usuario, [FromQuery] DateTime? fecha, CancellationToken ct)
    {
        var result = await _abonoService.ListarAbonosDelDiaAsync(usuario, fecha, ct);
        return Ok(result);
    }

    [HttpGet("historial/{clienteClave}")]
    public async Task<IActionResult> GetHistorial(string clienteClave, CancellationToken ct)
    {
        var result = await _abonoService.ListarHistorialPorClienteAsync(clienteClave, ct);
        return Ok(result);
    }

    [HttpGet("saldo-cliente")]
    public async Task<IActionResult> SaldoCliente([FromQuery] string clienteClave, CancellationToken ct)
    {
        var saldo = await _abonoService.ObtenerSaldoClienteAsync(clienteClave, ct);
        return Ok(new ClienteSaldoDto { ClienteClave = clienteClave, SaldoTotal = saldo });
    }

    // F7 H2c: el recibo se arma desde el DOCUMENTO del motor (adm_pago).
    [HttpGet("recibo-pdf/{pagoId:long}")]
    public async Task<IActionResult> GetReciboPdf(long pagoId, CancellationToken ct)
    {
        var datos = await _abonoService.GenerarDatosReciboAsync(pagoId, ct);
        if (datos is null)
            return NotFound(new { mensaje = "No se encontró el cobro indicado." });

        return ReciboPdf(datos, pagoId);
    }

    // F7 H2c: recibo del papel "para banco" aún no cobrado.
    [HttpGet("recibo-pendiente-pdf/{pendienteId:long}")]
    public async Task<IActionResult> GetReciboPendientePdf(long pendienteId, CancellationToken ct)
    {
        var datos = await _abonoService.GenerarDatosReciboPendienteAsync(pendienteId, ct);
        if (datos is null)
            return NotFound(new { mensaje = "No se encontró el recibo pendiente indicado." });

        return ReciboPdf(datos, pendienteId);
    }

    private IActionResult ReciboPdf(SIAD.Core.DTOs.Caja.ReciboAbonoDto datos, long id)
    {
        using var report = new Rpt_Dev_Recibo_Abono(datos);
        report.RequestParameters = false;

        using var stream = new System.IO.MemoryStream();
        report.ExportToPdf(stream);

        // Content-Disposition inline: el navegador muestra el recibo como vista
        // previa en una pestaña en vez de descargarlo (mismo patrón que InformesController).
        Response.Headers.ContentDisposition = $"inline; filename=Recibo-{datos.NumRecibo}-{id}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    [HttpPost("generar-recibo")]
    public async Task<IActionResult> GenerarRecibo([FromBody] GenerarReciboDto request, CancellationToken ct)
    {
        request.Usuario = User?.Identity?.Name ?? "system";
        var result = await _abonoService.GenerarReciboPendienteAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("recibos-pendientes")]
    public async Task<IActionResult> RecibosPendientes([FromQuery] string numFactura, CancellationToken ct)
    {
        var result = await _abonoService.ListarRecibosPendientesPorFacturaAsync(numFactura, ct);
        return Ok(result);
    }

    // Recibos pendientes (para banco) de todas las facturas del cliente — Caja F3
    [HttpGet("recibos-pendientes-cliente")]
    public async Task<IActionResult> RecibosPendientesCliente([FromQuery] string clave, CancellationToken ct)
    {
        var result = await _abonoService.ListarRecibosPendientesPorClienteAsync(clave, ct);
        return Ok(result);
    }

    [HttpGet("historial-factura/{numFactura}")]
    public async Task<IActionResult> HistorialFactura(string numFactura, CancellationToken ct)
    {
        var result = await _abonoService.ListarAbonosPorFacturaAsync(numFactura, ct);
        return Ok(result);
    }

    [HttpPost("anular-pendiente")]
    public async Task<IActionResult> AnularPendiente([FromBody] AnularReciboPendienteDto request, CancellationToken ct)
    {
        request.Usuario = User?.Identity?.Name ?? "system";
        var result = await _abonoService.AnularReciboPendienteAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("especiales")]
    public async Task<IActionResult> ListarEspeciales(
        [FromQuery] string? estado,
        [FromQuery] string? search,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        var filtro = new AbonoEspecialFiltroDto
        {
            Estado = estado,
            Search = search,
            Desde = desde,
            Hasta = hasta,
            Skip = skip,
            Take = take <= 0 ? 15 : take,
            SortField = sortField,
            SortDesc = sortDesc
        };

        var result = await _abonoService.ListarAbonosEspecialesAsync(filtro, ct);
        return Ok(result);
    }

    [HttpGet("especiales/resumen")]
    public async Task<IActionResult> ResumenEspeciales(
        [FromQuery] string? estado,
        [FromQuery] string? search,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken ct)
    {
        var filtro = new AbonoEspecialFiltroDto
        {
            Estado = estado,
            Search = search,
            Desde = desde,
            Hasta = hasta
        };

        var result = await _abonoService.ObtenerResumenAbonosEspecialesAsync(filtro, ct);
        return Ok(result);
    }
}
