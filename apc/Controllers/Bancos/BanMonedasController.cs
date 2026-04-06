using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/monedas")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BanMonedasController : ControllerBase
{
    private readonly IBanMonedasService service;
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;

    public BanMonedasController(
        IBanMonedasService service,
        SiadDbContext dbContext,
        ICurrentCompanyService currentCompanyService)
    {
        this.service = service;
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long companyId, CancellationToken ct)
    {
        if (companyId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un companyId valido." });
        }

        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        var monedas = await service.GetAsync(companyId, ct);
        return Ok(monedas);
    }

    private async Task<bool> ValidarAccesoEmpresaAsync(long companyId, CancellationToken ct)
    {
        if (companyId <= 0)
        {
            return false;
        }

        var existe = await dbContext.cfg_companies
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId, ct);

        if (!existe)
        {
            return false;
        }

        var actual = currentCompanyService.GetCompanyId();
        return actual == companyId;
    }
}

