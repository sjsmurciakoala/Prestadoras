using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Services.Tarifario;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/prueba-calculo")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]
public class PruebaCalculoController : ControllerBase
{
    private readonly IPruebaCalculoService _service;

    public PruebaCalculoController(IPruebaCalculoService service)
    {
        _service = service;
    }

    [HttpPost("calcular")]
    public async Task<IActionResult> Calcular([FromBody] PruebaCalculoRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.CalcularAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Npgsql.PostgresException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
