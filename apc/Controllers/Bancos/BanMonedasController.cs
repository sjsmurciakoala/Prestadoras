using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/monedas")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BanMonedasController : ControllerBase
{
    private readonly IBanMonedasService service;
    private readonly ICompanyAccessValidator accessValidator;

    public BanMonedasController(
        IBanMonedasService service,
        ICompanyAccessValidator accessValidator)
    {
        this.service = service;
        this.accessValidator = accessValidator;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long companyId, CancellationToken ct)
    {
        if (companyId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un companyId valido." });
        }

        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var monedas = await service.GetAsync(companyId, ct);
        return Ok(monedas);
    }
}

