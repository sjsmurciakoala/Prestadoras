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
/// Configuración de Integración Contable ↔ Comercial por empresa
/// (plan 2026-07-02, Fase 2).
/// </summary>
[ApiController]
[Route("api/contabilidad/integracion")]
[ModuleAuthorize(PermissionModules.Contabilidad, PermissionResources.Contabilidad.Integracion)]
public sealed class IntegracionContableController : ControllerBase
{
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly IIntegracionContableService integracionService;

    public IntegracionContableController(SiadDbContext dbContext, ICurrentCompanyService currentCompanyService,
        IIntegracionContableService integracionService)
    {
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
        this.integracionService = integracionService;
    }

    /// <summary>Configuración completa (cabecera + matriz + asientos) de la empresa.</summary>
    [HttpGet("{companyId:long}")]
    public async Task<IActionResult> Obtener(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await integracionService.ObtenerAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener la configuración de integración: {ex.Message}" });
        }
    }

    /// <summary>Guarda la configuración completa.</summary>
    [HttpPost("{companyId:long}")]
    public async Task<IActionResult> Guardar(long companyId, [FromBody] IntegracionContableDto dto, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await integracionService.GuardarAsync(companyId, dto, usuario, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException() is PostgresException pg ? pg.MessageText : ex.GetBaseException().Message;
            return BadRequest(new { detail = $"Error al guardar la configuración: {raiz}" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar la configuración: {ex.Message}" });
        }
    }

    /// <summary>Aplica un perfil de auto-llenado (por ahora: ERSAPS).</summary>
    [HttpPost("{companyId:long}/perfil/{perfil}")]
    public async Task<IActionResult> AplicarPerfil(long companyId, string perfil, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await integracionService.AplicarPerfilAsync(companyId, perfil, usuario, ct);
            return Ok(resultado);
        }
        catch (PostgresException ex)
        {
            // sp_con_aplicar_perfil_integracion valida empresa y perfil con RAISE EXCEPTION.
            return BadRequest(new { detail = ex.MessageText });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al aplicar el perfil: {ex.Message}" });
        }
    }

    /// <summary>Valida la configuración persistida (posteo y cobertura según modo).</summary>
    [HttpGet("{companyId:long}/validacion")]
    public async Task<IActionResult> Validar(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await integracionService.ValidarAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al validar la configuración: {ex.Message}" });
        }
    }

    /// <summary>Servicios comerciales activos de la empresa.</summary>
    [HttpGet("{companyId:long}/servicios")]
    public async Task<IActionResult> ListarServicios(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await integracionService.ListarServiciosAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar los servicios: {ex.Message}" });
        }
    }

    /// <summary>Cuentas del plan que permiten posteo directo.</summary>
    [HttpGet("{companyId:long}/cuentas-posteables")]
    public async Task<IActionResult> ListarCuentasPosteables(long companyId, CancellationToken ct)
    {
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await integracionService.ListarCuentasPosteablesAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar las cuentas posteables: {ex.Message}" });
        }
    }

    /// <summary>Categorías de servicio activas.</summary>
    [HttpGet("categorias")]
    public async Task<IActionResult> ListarCategorias(CancellationToken ct)
    {
        try
        {
            return Ok(await integracionService.ListarCategoriasAsync(ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar las categorías: {ex.Message}" });
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
