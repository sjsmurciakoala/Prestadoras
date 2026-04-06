using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/tipos-transacciones")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BanTiposTransaccionesController : ControllerBase
{
    private readonly IBanTiposTransaccionesService service;

    public BanTiposTransaccionesController(IBanTiposTransaccionesService service)
    {
        this.service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long companyId, CancellationToken ct)
    {
        if (companyId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un companyId valido." });
        }

        try
        {
            var data = await service.GetAsync(companyId, ct);
            return Ok(data);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }
}

