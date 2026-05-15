using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/tarifario/servicios-v3")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class ServicioTarifarioV3Controller : ControllerBase
{
    private readonly ServicioTarifarioV3Service _service;

    public ServicioTarifarioV3Controller(ServicioTarifarioV3Service service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? search,
        [FromQuery] bool? activo,
        [FromQuery] bool? facturableApp,
        [FromQuery] long? tipoServicioId,
        CancellationToken ct)
    {
        var items = await _service.GetAsync(search, activo, facturableApp, tipoServicioId, ct);
        return Ok(items);
    }

    [HttpGet("{servicioId:long}")]
    public async Task<IActionResult> GetById(long servicioId, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(servicioId, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("catalogos")]
    public async Task<IActionResult> GetCatalogos(CancellationToken ct)
    {
        var catalogos = await _service.GetCatalogosAsync(ct);
        return Ok(catalogos);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] ServicioTarifarioV3EditDto request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.GuardarAsync(request, usuario, ct);
        return Ok(response);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost("{servicioId:long}/desactivar")]
    public async Task<IActionResult> Desactivar(long servicioId, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.DesactivarAsync(servicioId, usuario, ct);
        return Ok(response);
    }
}
