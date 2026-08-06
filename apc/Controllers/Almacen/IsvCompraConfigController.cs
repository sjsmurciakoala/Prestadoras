using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/isv-compras")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class IsvCompraConfigController : ControllerBase
{
    private readonly IIsvCompraConfigService _service;
    public IsvCompraConfigController(IIsvCompraConfigService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _service.ObtenerAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] IsvCompraConfigDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.GuardarAsync(dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
