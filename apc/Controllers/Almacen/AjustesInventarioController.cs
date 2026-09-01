using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Ajustes de inventario: <b>solo lectura del histórico</b>. La captura de ajustes quedó
/// <b>deprecada en la Fase 4</b> (2026-08-04): toda entrada/salida de stock se registra ahora
/// como documento de movimiento de almacén (<c>IMovimientoAlmacenService</c>), que reemplaza al
/// ajuste de una sola línea. La tabla <c>alm_ajuste_inventario</c> y este GET se conservan para
/// leer el histórico ya posteado; no se borra nada.
/// </summary>
[ApiController]
[Route("api/almacen/ajustes")]
[ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Ajustes)]
public sealed class AjustesInventarioController : ControllerBase
{
    private readonly IAjusteInventarioService _service;

    public AjustesInventarioController(IAjusteInventarioService service)
    {
        _service = service;
    }

    /// <summary>Histórico de ajustes de un par (artículo, bodega). Solo lectura.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPorPar([FromQuery] int articuloId, [FromQuery] int bodegaId, CancellationToken ct)
    {
        if (articuloId <= 0 || bodegaId <= 0)
        {
            return Problem(detail: "Indique el artículo y la bodega.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await _service.GetPorParAsync(articuloId, bodegaId, ct));
    }

    // El POST de captura se retiró en la Fase 4: los ajustes se registran como movimientos de
    // almacén (api/almacen/movimientos). Ver docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md §3.6.
}
