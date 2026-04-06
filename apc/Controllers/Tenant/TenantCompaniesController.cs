using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tenant;
using SIAD.Services.Tenancy;

namespace apc.Controllers;

[ApiController]
[Route("api/tenant/companies")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class TenantCompaniesController : ControllerBase
{
    private readonly ITenantCompanyService _tenantCompanyService;

    public TenantCompaniesController(ITenantCompanyService tenantCompanyService)
    {
        _tenantCompanyService = tenantCompanyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantCompanyDto>>> GetAsync(CancellationToken ct)
    {
        var companies = await _tenantCompanyService.ObtenerEmpresasAsync(ct);
        return Ok(companies);
    }
}
