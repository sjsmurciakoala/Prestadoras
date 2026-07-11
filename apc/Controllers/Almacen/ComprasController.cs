using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/compras")]
[ModuleAuthorize(PermissionModules.Compras)]
public sealed class ComprasController : ControllerBase
{
    private readonly IComprasService _service;

    public ComprasController(IComprasService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CompraFilterDto filtro, CancellationToken ct)
    {
        var compras = await _service.GetAsync(filtro, ct);
        return Ok(compras);
    }

    [HttpGet("proveedores")]
    public async Task<IActionResult> GetProveedores(CancellationToken ct)
    {
        var proveedores = await _service.GetProveedoresAsync(ct);
        return Ok(proveedores);
    }
}
