using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;
using apc.Security;

namespace apc.Controllers.Contabilidad;

/// <summary>
/// Lote manual de partidas de facturación (plan 2026-07-02, Fase 3).
/// Preview, generación (posteo vía motor único), historial y pendientes.
/// </summary>
[ApiController]
[Route("api/contabilidad/lote-facturacion")]
[ModuleAuthorize(PermissionModules.Contabilidad, PermissionResources.Contabilidad.Integracion)]
public sealed class LoteFacturacionController : ControllerBase
{
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly ILoteFacturacionService loteService;

    public LoteFacturacionController(SiadDbContext dbContext, ICurrentCompanyService currentCompanyService,
        ILoteFacturacionService loteService)
    {
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
        this.loteService = loteService;
    }

    /// <summary>Preview agregado del lote (no escribe nada).</summary>
    [HttpGet("{companyId:long}/preview")]
    public async Task<IActionResult> Preview(long companyId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta,
        [FromQuery] string modo, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await loteService.PreviewAsync(companyId, desde, hasta, modo, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (PostgresException ex)
        {
            // fn_con_preview_partidas_facturacion valida config/cuentas con RAISE EXCEPTION.
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al generar la vista previa del lote: {ex.Message}" });
        }
    }

    /// <summary>Genera y postea el lote de partidas (idempotente).</summary>
    [HttpPost("{companyId:long}/generar")]
    public async Task<IActionResult> Generar(long companyId, [FromBody] LoteGenerarRequestDto request,
        CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await loteService.GenerarAsync(companyId, request.Desde, request.Hasta,
                request.Modo, usuario, ct);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (PostgresException ex)
        {
            // sp_con_generar_partidas_facturacion valida config, asiento VENTAS,
            // resolución de cuentas y período con RAISE EXCEPTION.
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al generar el lote de partidas: {ex.Message}" });
        }
    }

    /// <summary>Últimos lotes generados de la empresa.</summary>
    [HttpGet("{companyId:long}/historial")]
    public async Task<IActionResult> Historial(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await loteService.HistorialAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar el historial de lotes: {ex.Message}" });
        }
    }

    /// <summary>Pendientes de regularización del módulo VENTAS.</summary>
    [HttpGet("{companyId:long}/pendientes")]
    public async Task<IActionResult> Pendientes(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await loteService.PendientesAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar los pendientes de regularización: {ex.Message}" });
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
