using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/kardex")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class KardexController : ControllerBase
{
    private readonly IKardexService _service;

    public KardexController(IKardexService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] KardexFilterDto filtro, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filtro.CodigoArticulo))
        {
            return BadRequest(new { message = "Debe indicar el código del artículo." });
        }

        var kardex = await _service.GetByArticuloAsync(filtro, ct);
        return kardex is null ? NotFound() : Ok(kardex);
    }

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos(CancellationToken ct)
    {
        var tipos = await _service.GetTiposMovimientoAsync(ct);
        return Ok(tipos);
    }
}
