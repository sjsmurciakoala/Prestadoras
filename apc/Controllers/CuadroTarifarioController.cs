using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/cuadros-tarifarios")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class CuadroTarifarioController : ControllerBase
{
    private readonly ICuadroTarifarioService _service;

    public CuadroTarifarioController(ICuadroTarifarioService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetCuadros(CancellationToken ct)
    {
        var items = await _service.GetCuadrosAsync(ct);
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
    public async Task<IActionResult> GuardarCuadro([FromBody] CuadroTarifarioSaveRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.GuardarCuadroAsync(request, usuario, ct);
        return Ok(response);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("{cuadroId:long}/desactivar")]
    public async Task<IActionResult> DesactivarCuadro(long cuadroId, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.DesactivarCuadroAsync(cuadroId, usuario, ct);
        return Ok(response);
    }

    // ── Reglas ──

    [HttpGet("{cuadroId:long}/reglas")]
    public async Task<IActionResult> GetReglas(long cuadroId, CancellationToken ct)
    {
        var items = await _service.GetReglasAsync(cuadroId, ct);
        return Ok(items);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("reglas")]
    public async Task<IActionResult> GuardarRegla([FromBody] ReglaTarifariaSaveRequest request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.GuardarReglaAsync(request, usuario, ct);
        return Ok(response);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("reglas/{reglaId:long}/eliminar")]
    public async Task<IActionResult> EliminarRegla(long reglaId, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.EliminarReglaAsync(reglaId, usuario, ct);
        return Ok(response);
    }
}
