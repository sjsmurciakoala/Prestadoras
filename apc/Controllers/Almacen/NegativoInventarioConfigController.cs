using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Interruptor de existencia negativa en salidas, por empresa (cfg_inventario_negativo). El override
/// por bodega se administra desde el mantenimiento de bodegas.
/// </summary>
[ApiController]
[Route("api/almacen/existencia-negativa")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class NegativoInventarioConfigController : ControllerBase
{
    private readonly INegativoInventarioConfigService _service;
    public NegativoInventarioConfigController(INegativoInventarioConfigService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _service.ObtenerAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] NegativoInventarioConfigDto dto, CancellationToken ct)
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
