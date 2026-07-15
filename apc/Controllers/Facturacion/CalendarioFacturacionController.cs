using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Facturacion;
using SIAD.Services.Facturacion;
using apc.Security;

namespace apc.Controllers.Facturacion;

/// <summary>
/// Calendario de facturación por empresa (Fase A apertura-ciclo-único,
/// 2026-07-14): fechas de lectura/facturación/vencimiento por año/mes/ciclo
/// (calendariopro). El motor V3 lo lee para fechavence/plazo de la factura.
/// </summary>
[ApiController]
[Route("api/ventas/calendario-facturacion")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CalendarioFacturacion)]
public sealed class CalendarioFacturacionController : ControllerBase
{
    private readonly ICompanyAccessValidator accessValidator;
    private readonly ICalendarioFacturacionService calendarioService;

    public CalendarioFacturacionController(
        ICompanyAccessValidator accessValidator,
        ICalendarioFacturacionService calendarioService)
    {
        this.accessValidator = accessValidator;
        this.calendarioService = calendarioService;
    }

    /// <summary>Años con calendario cargado (descendente).</summary>
    [HttpGet("{companyId:long}/anios")]
    public async Task<IActionResult> ListarAnios(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await calendarioService.ListarAniosAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al listar los años del calendario: {ex.Message}" });
        }
    }

    /// <summary>Calendario completo del año.</summary>
    [HttpGet("{companyId:long}/{anio:int}")]
    public async Task<IActionResult> ObtenerAnio(long companyId, int anio, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await calendarioService.ObtenerAnioAsync(companyId, anio, ct));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener el calendario: {ex.Message}" });
        }
    }

    /// <summary>Persiste el calendario del año (upsert + borra las filas que ya no vienen).</summary>
    [HttpPost("{companyId:long}/{anio:int}")]
    public async Task<IActionResult> GuardarAnio(long companyId, int anio,
        [FromBody] List<CalendarioCicloDto> filas, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            return Ok(await calendarioService.GuardarAnioAsync(companyId, anio, filas ?? new(), usuario, ct));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException() is PostgresException pg ? pg.MessageText : ex.GetBaseException().Message;
            return BadRequest(new { detail = $"Error al guardar el calendario: {raiz}" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar el calendario: {ex.Message}" });
        }
    }

    /// <summary>Copia el calendario de un año a otro desplazando las fechas.</summary>
    [HttpPost("{companyId:long}/copiar")]
    public async Task<IActionResult> CopiarAnio(long companyId,
        [FromBody] CopiarCalendarioAnioRequest request, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        if (request is null)
        {
            return BadRequest(new { detail = "Petición inválida." });
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            return Ok(await calendarioService.CopiarAnioAsync(
                companyId, request.AnioOrigen, request.AnioDestino, usuario, ct));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al copiar el calendario: {ex.Message}" });
        }
    }
}
