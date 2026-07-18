using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/tarifario/desglose-abonos")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class DesgloseAbonoConfigController : ControllerBase
{
    private readonly IDesgloseAbonoConfigService _service;

    public DesgloseAbonoConfigController(IDesgloseAbonoConfigService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var items = await _service.GetAsync(ct);
        return Ok(items);
    }

    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes, PermissionAction.Edit)]
    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] DesgloseAbonoGuardarDto request, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var response = await _service.GuardarAsync(request, usuario, ct);
        return Ok(response);
    }
}
