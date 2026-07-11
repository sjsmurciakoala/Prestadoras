using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/requisiciones")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class RequisicionesController : ControllerBase
{
    private readonly IRequisicionesService _service;

    public RequisicionesController(IRequisicionesService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RequisicionFilterDto filtro, CancellationToken ct)
    {
        var requisiciones = await _service.GetAsync(filtro, ct);
        return Ok(requisiciones);
    }

    [HttpGet("departamentos")]
    public async Task<IActionResult> GetDepartamentos(CancellationToken ct)
    {
        var departamentos = await _service.GetDepartamentosAsync(ct);
        return Ok(departamentos);
    }
}
