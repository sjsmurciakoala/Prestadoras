using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/clientes/{clienteId:int}/servicios-tarifario")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class ClienteServicioTarifarioController : ControllerBase
{
    private readonly IClienteServicioTarifarioService _service;

    public ClienteServicioTarifarioController(IClienteServicioTarifarioService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetServicios(int clienteId, CancellationToken ct)
    {
        var items = await _service.GetServiciosClienteAsync(clienteId, ct);
        return Ok(items);
    }

    [HttpGet("catalogos")]
    public async Task<IActionResult> GetCatalogos(CancellationToken ct)
    {
        var catalogos = await _service.GetCatalogosAsync(ct);
        return Ok(catalogos);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost]
    public async Task<IActionResult> Guardar(int clienteId, [FromBody] ClienteServicioSaveRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.GuardarAsync(clienteId, request, usuario, ct);
        return Ok(response);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("desactivar")]
    public async Task<IActionResult> Desactivar(int clienteId, [FromBody] ClienteServicioDesactivarRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.DesactivarAsync(clienteId, request, usuario, ct);
        return Ok(response);
    }
}
