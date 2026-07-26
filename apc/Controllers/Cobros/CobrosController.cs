using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Cobros;
using SIAD.Services.Cobros;
using apc.Security;

namespace apc.Controllers.Cobros;

// Motor único de cobro (unificación cobranza F2). En F3 la pantalla única de
// caja consume estos endpoints; en F2 conviven con los controllers legacy
// (captacionpagos / abono) cuyas fachadas delegan en el mismo motor.
[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Caja)]
public class CobrosController : ControllerBase
{
    private readonly ICobroService _cobroService;

    public CobrosController(ICobroService cobroService)
    {
        _cobroService = cobroService;
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] CobroCrearDto request, CancellationToken ct)
    {
        request.Usuario = User?.Identity?.Name ?? "system";
        // El canal HTTP del portal es siempre ventanilla; el WS bancario entra
        // por su propio host (F5) y la app por MobileApi.
        request.Canal = CanalCobro.Caja;
        var result = await _cobroService.RegistrarCobroAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("reverso")]
    public async Task<IActionResult> Reversar([FromBody] CobroReversoDto request, CancellationToken ct)
    {
        request.Usuario = User?.Identity?.Name ?? "system";
        var result = await _cobroService.ReversarCobroAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
