using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.PeriodosComerciales;
using SIAD.Services.PeriodosComerciales;
using apc.Security;

namespace apc.Controllers;

/// <summary>
/// Períodos comerciales (plan 2026-07-02, Fase 7): listado, apertura
/// secuencial y cierres con checklist (ciclo → mes). Los SPs de BD validan
/// las transiciones y mantienen el espejo historialmes para el WS.
/// </summary>
[ApiController]
[Route("api/ventas/periodos-comerciales")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.PeriodosComerciales)]
public sealed class PeriodosComercialesController : ControllerBase
{
    private readonly ICompanyAccessValidator accessValidator;
    private readonly IPeriodoComercialService periodoService;

    public PeriodosComercialesController(ICompanyAccessValidator accessValidator,
        IPeriodoComercialService periodoService)
    {
        this.accessValidator = accessValidator;
        this.periodoService = periodoService;
    }

    /// <summary>Períodos comerciales de la empresa con sus ciclos.</summary>
    [HttpGet("{companyId:long}")]
    public async Task<IActionResult> Listar(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await periodoService.ListarAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar los períodos comerciales: {ex.Message}" });
        }
    }

    /// <summary>Rutas del ciclo con su avance de facturación del mes.</summary>
    [HttpGet("{companyId:long}/ciclos/{periodoCicloId:long}/rutas")]
    public async Task<IActionResult> RutasCiclo(long companyId, long periodoCicloId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await periodoService.RutasCicloAsync(companyId, periodoCicloId, ct));
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar las rutas del ciclo: {ex.Message}" });
        }
    }

    /// <summary>Checklist del cierre del mes comercial.</summary>
    [HttpGet("{companyId:long}/{periodoComercialId:long}/checklist")]
    public async Task<IActionResult> Checklist(long companyId, long periodoComercialId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await periodoService.ChecklistCierreAsync(companyId, periodoComercialId, ct));
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al evaluar el checklist de cierre: {ex.Message}" });
        }
    }

    /// <summary>
    /// Apertura integral (Fase B): valida secuencia, crea período+ciclo con la
    /// fecha límite del calendario de facturación, genera la planilla de
    /// lectura y devuelve el resumen con avisos.
    /// </summary>
    [HttpPost("{companyId:long}/abrir")]
    public async Task<IActionResult> Abrir(long companyId, [FromBody] AbrirPeriodoComercialRequest request,
        CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            return Ok(await periodoService.AbrirAsync(companyId, request.Anio, request.Mes,
                request.Ciclo, usuario, ct));
        }
        catch (PostgresException ex)
        {
            // sp_adm_periodo_ciclo_abrir valida secuencia/estados con RAISE EXCEPTION
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al abrir el ciclo: {ex.Message}" });
        }
    }

    /// <summary>Preview de la apertura: qué pasaría, sin escribir nada.</summary>
    [HttpGet("{companyId:long}/abrir/preview")]
    public async Task<IActionResult> PreviewApertura(long companyId, [FromQuery] int anio, [FromQuery] int mes,
        [FromQuery] string? ciclo, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await periodoService.PreviewAperturaAsync(companyId, anio, mes, ciclo, ct));
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al calcular el preview de apertura: {ex.Message}" });
        }
    }

    /// <summary>Próximo ciclo a abrir según el calendario de facturación (204 si no hay).</summary>
    [HttpGet("{companyId:long}/abrir/sugerencia")]
    public async Task<IActionResult> SugerenciaApertura(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var sugerencia = await periodoService.SugerenciaAperturaAsync(companyId, ct);
            return sugerencia is null ? NoContent() : Ok(sugerencia);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al calcular la sugerencia de apertura: {ex.Message}" });
        }
    }

    /// <summary>
    /// Deshace una apertura de ciclo (borra planilla + ciclo, y el período si
    /// queda vacío). El SP lo rechaza si ya hay lecturas o facturas.
    /// </summary>
    [HttpPost("{companyId:long}/ciclos/{periodoCicloId:long}/deshacer")]
    public async Task<IActionResult> DeshacerApertura(long companyId, long periodoCicloId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            return Ok(await periodoService.DeshacerAperturaAsync(companyId, periodoCicloId, usuario, ct));
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al deshacer la apertura: {ex.Message}" });
        }
    }

    /// <summary>Cierra un ciclo del período (sin forzar exige cero rutas pendientes).</summary>
    [HttpPost("{companyId:long}/ciclos/{periodoCicloId:long}/cerrar")]
    public async Task<IActionResult> CerrarCiclo(long companyId, long periodoCicloId,
        [FromBody] CerrarCicloRequest request, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            await periodoService.CerrarCicloAsync(companyId, periodoCicloId, usuario, request.Forzar, ct);
            return Ok();
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cerrar el ciclo: {ex.Message}" });
        }
    }

    /// <summary>Cierra el mes comercial (checklist en verde obligatorio).</summary>
    [HttpPost("{companyId:long}/{periodoComercialId:long}/cerrar")]
    public async Task<IActionResult> CerrarMes(long companyId, long periodoComercialId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            await periodoService.CerrarMesAsync(companyId, periodoComercialId, usuario, ct);
            return Ok();
        }
        catch (PostgresException ex)
        {
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cerrar el mes comercial: {ex.Message}" });
        }
    }
}
