using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/descargos")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class DescargosController : ControllerBase
{
    private readonly IDescargosService _service;

    public DescargosController(IDescargosService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DescargoFilterDto filtro, CancellationToken ct)
    {
        var descargos = await _service.GetAsync(filtro, ct);
        return Ok(descargos);
    }

    [HttpGet("departamentos")]
    public async Task<IActionResult> GetDepartamentos(CancellationToken ct)
    {
        var departamentos = await _service.GetDepartamentosAsync(ct);
        return Ok(departamentos);
    }
}
