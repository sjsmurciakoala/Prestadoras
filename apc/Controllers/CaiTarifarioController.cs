using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/tarifario/cai-offline")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class CaiTarifarioController : ControllerBase
{
    private readonly CaiTarifarioService _service;

    public CaiTarifarioController(CaiTarifarioService service)
    {
        _service = service;
    }

    [HttpGet("cais")]
    public async Task<IActionResult> GetCais(CancellationToken ct)
    {
        var items = await _service.GetCaisAsync(ct);
        return Ok(items);
    }

    [HttpGet("cais/paged")]
    public async Task<IActionResult> GetCaisPaged(
        [FromQuery] string? search,
        [FromQuery] bool? activo,
        [FromQuery] short? estadoId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? sortField = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken ct = default)
    {
        var filter = new CaiFacturacionFilterDto { Search = search, Activo = activo, EstadoId = estadoId };
        var page = await _service.GetCaisPagedAsync(filter, skip, take, sortField, sortDesc, ct);
        return Ok(page);
    }

    [HttpGet("bloques")]
    public async Task<IActionResult> GetBloques(CancellationToken ct)
    {
        var items = await _service.GetBloquesAsync(ct);
        return Ok(items);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("cais")]
    public async Task<IActionResult> GuardarCai([FromBody] CaiFacturacionSaveRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.GuardarCaiAsync(request, usuario, ct);
        return Ok(response);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("bloques/reservar")]
    public async Task<IActionResult> ReservarBloque([FromBody] CaiBloqueReservadoSaveRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.ReservarBloqueAsync(request, usuario, ct);
        return Ok(response);
    }

    [HttpGet("tipos-documento-lookup")]
    public async Task<IActionResult> GetTiposDocumentoLookup(CancellationToken ct)
        => Ok(await _service.GetTiposDocumentoFiscalLookupAsync(ct));

    [HttpGet("estados-lookup")]
    public async Task<IActionResult> GetEstadosLookup(CancellationToken ct)
        => Ok(await _service.GetCaiEstadosLookupAsync(ct));

    public sealed record CambiarEstadoRequest(short EstadoId);

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPatch("cais/{caiId:long}/estado")]
    public async Task<IActionResult> CambiarEstado(long caiId, [FromBody] CambiarEstadoRequest req, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.CambiarEstadoAsync(caiId, req.EstadoId, usuario, ct);
        return Ok(response);
    }
}
