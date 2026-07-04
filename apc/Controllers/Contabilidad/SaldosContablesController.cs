using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;
using apc.Security;

namespace apc.Controllers.Contabilidad;

/// <summary>
/// Reconciliación del caché oficial de saldos con_saldo_cuenta contra el
/// libro con_partida_dtl (plan 2026-07-02, Fase 6). Solo lectura: la
/// reconstrucción (sp_con_reconstruir_saldo_cuenta) se corre por SQL en
/// ventana de mantenimiento, a propósito sin endpoint.
/// </summary>
[ApiController]
[Route("api/contabilidad/saldos")]
[ModuleAuthorize(PermissionModules.Contabilidad, PermissionResources.Contabilidad.Saldos)]
public sealed class SaldosContablesController : ControllerBase
{
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly ISaldosService saldosService;

    public SaldosContablesController(SiadDbContext dbContext, ICurrentCompanyService currentCompanyService,
        ISaldosService saldosService)
    {
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
        this.saldosService = saldosService;
    }

    /// <summary>
    /// Verificación caché vs libro por período/cuenta. 0 divergencias =
    /// consistente; divergencias &gt; 0 = reconstruir en mantenimiento.
    /// </summary>
    [HttpGet("{companyId:long}/verificacion")]
    public async Task<IActionResult> Verificacion(long companyId, [FromQuery] long? periodId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await saldosService.VerificarAsync(companyId, periodId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al verificar los saldos contables: {ex.Message}" });
        }
    }

    private async Task<bool> ValidarAccesoEmpresaAsync(long companyId, CancellationToken ct)
    {
        var empresaExiste = await dbContext.cfg_companies
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId, cancellationToken: ct);

        if (!empresaExiste)
        {
            return false;
        }

        var companyIdActual = currentCompanyService.GetCompanyId();
        return companyIdActual > 0 && companyIdActual == companyId;
    }
}
