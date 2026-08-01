using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;
using SIAD.Services.NotasCreditoDebito;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/facturacion/notas")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.NotasCreditoDebito)]
public class NotasCreditoDebitoController : ControllerBase
{
    private readonly INotasCreditoDebitoService _service;

    public NotasCreditoDebitoController(INotasCreditoDebitoService service)
    {
        _service = service;
    }

    private string UsuarioActual => User?.Identity?.Name ?? "system";

    [HttpGet("clientes")]
    public async Task<IActionResult> BuscarClientes([FromQuery] string? query, CancellationToken ct)
        => Ok(await _service.BuscarClientesAsync(query, ct));

    [HttpGet("clientes/{clave}/facturas")]
    public async Task<IActionResult> BuscarFacturasCliente(string clave, CancellationToken ct)
        => Ok(await _service.BuscarFacturasClienteAsync(clave, ct));

    [HttpGet("motivos/anulacion")]
    public async Task<IActionResult> ListarMotivosAnulacion(CancellationToken ct)
        => Ok(await _service.ListarMotivosAnulacionAsync(ct));

    [HttpGet("motivos/aumento")]
    public async Task<IActionResult> ListarMotivosAumento(CancellationToken ct)
        => Ok(await _service.ListarMotivosAumentoAsync(ct));

    [HttpGet("cais")]
    public async Task<IActionResult> ListarCais([FromQuery] short tipoDocumentoFiscalId, CancellationToken ct)
        => Ok(await _service.ListarCaisNotaAsync(tipoDocumentoFiscalId, ct));

    [HttpPost("credito")]
    public async Task<IActionResult> EmitirNotaCredito([FromBody] EmitirNotaCreditoRequestDto dto, CancellationToken ct)
    {
        dto.Usuario = UsuarioActual;
        var resp = await _service.EmitirNotaCreditoAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }

    [HttpPost("debito")]
    public async Task<IActionResult> EmitirNotaDebito([FromBody] EmitirNotaDebitoRequestDto dto, CancellationToken ct)
    {
        dto.Usuario = UsuarioActual;
        var resp = await _service.EmitirNotaDebitoAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }

    [HttpGet("emitidas")]
    public async Task<IActionResult> ListarEmitidas(
        [FromQuery] string? search,
        [FromQuery] string? tipoNota,
        [FromQuery] short? estadoId,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        var filtro = new NotaEmitidaFilterDto
        {
            Search = search,
            TipoNota = tipoNota,
            EstadoId = estadoId,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };

        var result = await _service.ListarNotasEmitidasPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    // ── Impresión / vista previa (pruebas operativas jul-2026) ──

    [HttpGet("{tipoNota}/{notaId:long}/pdf")]
    public async Task<IActionResult> GetNotaPdf(string tipoNota, long notaId, CancellationToken ct)
    {
        var nota = await _service.ObtenerNotaImpresionAsync(tipoNota, notaId, ct);
        if (nota is null)
            return NotFound(new { mensaje = "No se encontró la nota indicada." });

        return NotaPdf(nota);
    }

    [HttpPost("credito/vista-previa-pdf")]
    public async Task<IActionResult> VistaPreviaCreditoPdf([FromBody] EmitirNotaCreditoRequestDto dto, CancellationToken ct)
    {
        dto.Usuario = UsuarioActual;
        var (nota, error) = await _service.GenerarVistaPreviaCreditoAsync(dto, ct);
        if (nota is null)
            return BadRequest(new { mensaje = error ?? "No se pudo generar la vista previa." });

        return NotaPdf(nota);
    }

    [HttpPost("debito/vista-previa-pdf")]
    public async Task<IActionResult> VistaPreviaDebitoPdf([FromBody] EmitirNotaDebitoRequestDto dto, CancellationToken ct)
    {
        dto.Usuario = UsuarioActual;
        var (nota, error) = await _service.GenerarVistaPreviaDebitoAsync(dto, ct);
        if (nota is null)
            return BadRequest(new { mensaje = error ?? "No se pudo generar la vista previa." });

        return NotaPdf(nota);
    }

    private IActionResult NotaPdf(NotaImpresionDto nota)
    {
        using var report = new SIAD.Reports.Rpt_Dev_Nota(nota);
        report.RequestParameters = false;

        using var stream = new System.IO.MemoryStream();
        report.ExportToPdf(stream);

        // inline: vista previa en pestaña, mismo patrón que el recibo de caja.
        var sufijo = nota.EsVistaPrevia ? "VistaPrevia" : nota.NumeroDocumento.Replace("/", "-");
        Response.Headers.ContentDisposition = $"inline; filename=Nota-{nota.TipoNota}-{sufijo}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    // ── Mantenimiento de catálogos de motivos ──

    [HttpGet("motivos/anulacion/crud")]
    public async Task<IActionResult> ListarMotivosAnulacionCrud(CancellationToken ct)
        => Ok(await _service.ListarMotivosAnulacionCrudAsync(ct));

    [HttpGet("motivos/aumento/crud")]
    public async Task<IActionResult> ListarMotivosAumentoCrud(CancellationToken ct)
        => Ok(await _service.ListarMotivosAumentoCrudAsync(ct));

    [HttpPost("motivos/anulacion")]
    public async Task<IActionResult> GuardarMotivoAnulacion([FromBody] MotivoSaveRequestDto dto, CancellationToken ct)
    {
        var resp = await _service.GuardarMotivoAnulacionAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }

    [HttpPost("motivos/aumento")]
    public async Task<IActionResult> GuardarMotivoAumento([FromBody] MotivoSaveRequestDto dto, CancellationToken ct)
    {
        var resp = await _service.GuardarMotivoAumentoAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }
}
