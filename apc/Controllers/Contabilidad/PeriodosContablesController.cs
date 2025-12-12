using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;
using SIAD.Services.Contabilidad;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/periodos")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public sealed class PeriodosContablesController : ControllerBase
{
    private readonly IPeriodoContableService _periodoService;
    private readonly ICurrentCompanyService _currentCompanyService;

    public PeriodosContablesController(IPeriodoContableService periodoService, 
        ICurrentCompanyService currentCompanyService)
    {
        _periodoService = periodoService;
        _currentCompanyService = currentCompanyService;
    }

    /// <summary>
    /// Obtiene el período activo de una empresa.
    /// </summary>
    [HttpGet("{companyId}/activo")]
    public async Task<IActionResult> ObtenerPeriodoActivo(long companyId, CancellationToken ct)
    {
        try
        {
            var periodo = await _periodoService.ObtenerPeriodoActivoAsync(companyId, ct);
            
            if (periodo is null)
            {
                return NotFound(new { detail = "No existe período activo para esta empresa." });
            }

            return Ok(periodo);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener el período: {ex.Message}" });
        }
    }

    /// <summary>
    /// Verifica si existe un período abierto.
    /// </summary>
    [HttpGet("{companyId}/existe-abierto")]
    public async Task<IActionResult> ExistePeriodoAbierto(long companyId, CancellationToken ct)
    {
        try
        {
            var existe = await _periodoService.ExistePeriodoAbiertoAsync(companyId, ct);
            return Ok(new { existe });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al validar período: {ex.Message}" });
        }
    }
}
