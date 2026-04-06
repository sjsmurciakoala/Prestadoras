using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using apc.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tenant;
using SIAD.Services.Tenancy;

namespace apc.Controllers;

[ApiController]
[Route("api/tenant/switch")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class TenantSessionController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITenantCompanyService _tenantCompanyService;

    public TenantSessionController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITenantCompanyService tenantCompanyService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tenantCompanyService = tenantCompanyService;
    }

    [HttpPost]
    public async Task<ActionResult<TenantCompanySwitchResponse>> SwitchAsync(
        [FromBody] TenantCompanySwitchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.CompanyId <= 0)
        {
            return BadRequest("Debe especificar una empresa válida.");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var companyExists = await _tenantCompanyService.ExisteEmpresaAsync(request.CompanyId, cancellationToken);
        if (!companyExists)
        {
            return NotFound($"La empresa con identificador {request.CompanyId} no existe.");
        }

        var currentClaims = await _userManager.GetClaimsAsync(user);
        var requestedValue = request.CompanyId.ToString(CultureInfo.InvariantCulture);
        var existing = currentClaims.FirstOrDefault(c => c.Type == TenantClaimTypes.CompanyId);

        if (existing?.Value == requestedValue)
        {
            return Ok(new TenantCompanySwitchResponse { CompanyId = request.CompanyId });
        }

        if (existing is not null)
        {
            var removeResult = await _userManager.RemoveClaimAsync(user, existing);
            if (!removeResult.Succeeded)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "No fue posible actualizar la empresa actual del usuario.");
            }
        }

        var addResult = await _userManager.AddClaimAsync(user,
            new Claim(TenantClaimTypes.CompanyId, requestedValue));
        if (!addResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                "No fue posible asignar la empresa indicada.");
        }

        await _signInManager.RefreshSignInAsync(user);

        return Ok(new TenantCompanySwitchResponse { CompanyId = request.CompanyId });
    }
}
