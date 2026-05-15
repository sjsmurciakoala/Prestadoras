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
