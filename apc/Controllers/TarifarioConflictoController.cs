using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/tarifario/conflictos-v3")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class TarifarioConflictoController : ControllerBase
{
    private readonly ITarifarioConflictoService _service;

    public TarifarioConflictoController(ITarifarioConflictoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery] string? estadoCodigo,
        [FromQuery] string? rutaCodigo,
        [FromQuery] int? clienteId,
        CancellationToken ct)
    {
        var items = await _service.GetAsync(search, estadoCodigo, rutaCodigo, clienteId, ct);
        return Ok(items);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("resolver")]
    public async Task<IActionResult> Resolver([FromBody] TarifarioConflictoResolveRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.ResolverAsync(request, usuario, ct);
        return Ok(response);
    }
}
