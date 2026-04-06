using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;
using apc.Security;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/terceros")]
[ModuleAuthorize(PermissionModules.Contabilidad)]
public sealed class TercerosController : ControllerBase
{
    private readonly ITerceroService _terceros;

    public TercerosController(ITerceroService terceros) => _terceros = terceros;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var list = await _terceros.GetTercerosAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] TerceroUpsertDto dto, CancellationToken ct)
    {
        try
        {
            var id = await _terceros.SaveTerceroAsync(dto, ct);
            return Ok(new { ThirdPartyId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Error de validación", Detail = ex.Message });
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
    {
        try
        {
            var ok = await _terceros.DeleteTerceroAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Error", Detail = ex.Message });
        }
    }

    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar(CancellationToken ct)
    {
        var userId = User?.Identity?.Name ?? "SYSTEM";
        var result = await _terceros.SincronizarDesdeProveedoresYClientesAsync(userId, ct);
        return Ok(result);
    }
}
