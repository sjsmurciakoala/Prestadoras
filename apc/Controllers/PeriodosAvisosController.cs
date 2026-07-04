using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIAD.Services.PeriodosComerciales;
using apc.Security;

namespace apc.Controllers;

/// <summary>
/// Avisos de períodos para el banner del portal (F7): mes comercial/contable
/// vencido, período del mes inexistente, facturas sin partida, cola de
/// pendientes y desfase comercial-contable.
/// Solo [Authorize] (sin ModuleAuthorize): el banner es informativo y se
/// muestra a cualquier usuario autenticado del tenant; no expone datos más
/// allá de contadores y códigos de período de su propia empresa.
/// </summary>
[ApiController]
[Route("api/periodos/avisos")]
[Authorize]
public sealed class PeriodosAvisosController : ControllerBase
{
    private readonly ICompanyAccessValidator accessValidator;
    private readonly IPeriodoComercialService periodoService;

    public PeriodosAvisosController(ICompanyAccessValidator accessValidator,
        IPeriodoComercialService periodoService)
    {
        this.accessValidator = accessValidator;
        this.periodoService = periodoService;
    }

    [HttpGet("{companyId:long}")]
    public async Task<IActionResult> Avisos(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await periodoService.AvisosAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar los avisos de períodos: {ex.Message}" });
        }
    }
}
