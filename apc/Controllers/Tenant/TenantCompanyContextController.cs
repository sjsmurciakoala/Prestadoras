using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Tenant;
using SIAD.Services.Tenancy;

namespace apc.Controllers;

[ApiController]
[Route("api/tenant/context")]
[Authorize]
public sealed class TenantCompanyContextController : ControllerBase
{
    private readonly ITenantCompanyService tenantCompanyService;
   private readonly ILogger<TenantCompanyContextController> logger;

   public TenantCompanyContextController(ITenantCompanyService tenantCompanyService, ILogger<TenantCompanyContextController> logger)
    {
        this.tenantCompanyService = tenantCompanyService;
       this.logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TenantCompanyContextDto>> GetAsync(CancellationToken ct)
    {
       try
        {
           var currentCompanyId = GetCurrentCompanyId();
           var hasValidCompany = currentCompanyId > 0 &&
                                 await tenantCompanyService.ExisteEmpresaAsync(currentCompanyId, ct);
           var hasCompanies = await tenantCompanyService.HayEmpresasAsync(ct);
           var canManageCompanies = User.IsInRole(RoleNames.SuperAdministrador);

           return Ok(new TenantCompanyContextDto
           {
               CurrentCompanyId = currentCompanyId,
               HasValidCompany = hasValidCompany,
               HasCompanies = hasCompanies,
               CanManageCompanies = canManageCompanies,
               Message = BuildMessage(currentCompanyId, hasValidCompany, hasCompanies, canManageCompanies),
               RecoveryPath = hasCompanies ? "/contabilidad/empresas" : "/contabilidad/empresas/nueva"
           });
       }
       catch (OperationCanceledException) when (ct.IsCancellationRequested)
       {
           // El cliente cancelo la solicitud (navegacion, refresh, timeout). No es un error real.
           logger.LogDebug("Solicitud de contexto de empresa cancelada por el cliente");
           return StatusCode(499); // Client Closed Request (convencion nginx)
       }
    }

    private long GetCurrentCompanyId()
    {
        var claimValue = User.FindFirst(TenantClaimTypes.CompanyId)?.Value;
        return long.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var companyId) && companyId > 0
            ? companyId
            : 0;
    }

    private static string BuildMessage(long currentCompanyId, bool hasValidCompany, bool hasCompanies, bool canManageCompanies)
    {
        if (hasValidCompany)
        {
            return $"La empresa activa {currentCompanyId} es valida.";
        }

        if (!canManageCompanies)
        {
            return !hasCompanies
                ? "No existe ninguna empresa registrada. Contacte al administrador."
                : currentCompanyId > 0
                    ? $"La empresa activa {currentCompanyId} no existe o ya no esta disponible. Contacte al administrador."
                    : "Su usuario no tiene una empresa activa valida. Contacte al administrador.";
        }

        if (!hasCompanies)
        {
            return "No existe ninguna empresa registrada. Cree la primera empresa antes de continuar.";
        }

        return currentCompanyId > 0
            ? $"La empresa activa {currentCompanyId} no existe o ya no esta disponible. Seleccione una empresa valida antes de continuar."
            : "El usuario autenticado no tiene una empresa activa asignada. Seleccione una empresa valida antes de continuar.";
    }
}
