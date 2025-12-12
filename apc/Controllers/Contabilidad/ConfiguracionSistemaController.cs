using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public sealed class ConfiguracionSistemaController : ControllerBase
{
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly IConfiguracionSistemaService configuracionService;

    public ConfiguracionSistemaController(SiadDbContext dbContext, ICurrentCompanyService currentCompanyService,
        IConfiguracionSistemaService configuracionService)
    {
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
        this.configuracionService = configuracionService;
    }

    /// <summary>
    /// Obtiene la configuracion del sistema de una empresa
    /// </summary>
    [HttpGet("configuracion/{companyId}")]
    public async Task<IActionResult> ObtenerConfiguracion(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var config = await configuracionService.ObtenerAsync(companyId, ct);

            if (config == null)
            {
                var hoy = DateTime.Today;
                return Ok(new ConfiguracionSistemaDto
                {
                    Principal = new ConfiguracionPrincipalDto
                    {
                        FechaInicioEjercicio = new DateTime(hoy.Year, 1, 1),
                        FechaFinEjercicio = new DateTime(hoy.Year, 12, 31),
                        MesesCalculados = 12,
                        SeparadorCodigo = "-",
                        FormatoCuentas = "###-###-##",
                        FormatoCentros = "###-##",
                        SymbolSaldoAcreedor = "CR",
                        MontoMaximo = 99999999999m,
                        FrecuenciaDepreciacion = "Mensual",
                        UltimaDepreciacion = null
                    },
                    CuentasUtilidad = new CuentasUtilidadDto(),
                    EstadoSituacionFinanciera = new EstadoSituacionFinancieraDto(),
                    LineasResultado = new List<LineaResultadoDto>(),
                    LineasBalance = new List<BalanceSheetLineDto>(),
                    Correlativos = new List<CorrelativoDto>()
                });
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener la configuracion: {ex.Message}" });
        }
    }

    /// <summary>
    /// Guarda la configuracion del sistema
    /// </summary>
    [HttpPost("configuracion/{companyId}")]
    public async Task<IActionResult> GuardarConfiguracion(long companyId, [FromBody] ConfiguracionSistemaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var existePlanCuentas = await configuracionService.ExistePlanCuentasAsync(companyId, ct);
            if (!existePlanCuentas)
            {
                return BadRequest(new
                {
                    detail = "No existe un plan de cuentas para esta empresa. Por favor, cree primero el plan de cuentas."
                });
            }

            var existePeriodo = await configuracionService.ExistePeriodoAbiertoAsync(companyId, ct);
            if (!existePeriodo)
            {
                return BadRequest(new
                {
                    detail = "No existe un período contable abierto. Por favor, cree primero un período."
                });
            }

            var resultado = await configuracionService.GuardarAsync(companyId, dto, usuario, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException()?.Message ?? ex.Message;
            return BadRequest(new { detail = $"Error al guardar la configuracion: {raiz}" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar la configuracion: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene la lista de cuentas contables disponibles
    /// </summary>
    [HttpGet("{companyId}/cuentas")]
    public async Task<IActionResult> ListarCuentas(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var cuentas = await dbContext.con_plan_cuentas
                .AsNoTracking()
                .Where(c => c.company_id == companyId)
                .OrderBy(c => c.code)
                .Select(c => new CuentaContableLookupDto
                {
                    AccountId = c.account_id,
                    Code = c.code,
                    Description = c.name
                })
                .ToListAsync(cancellationToken: ct);

            return Ok(cuentas);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar las cuentas: {ex.Message}" });
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
        return companyIdActual > 0;
    }
}
