using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.CondicionesLectura;
using SIAD.Services.CondicionesLectura;
using apc.Security;

namespace apc.Controllers.Ventas;

/// <summary>
/// ABM de condiciones de lectura por empresa (app_lectores, 2026-07-06). El
/// catálogo administrado aquí lo consume la app de lectores vía
/// GET /api/condiciones (apc.MobileApi). El `tipo` se elige de la referencia
/// global (comportamiento del motor V3); el admin no inventa tipos.
/// </summary>
[ApiController]
[Route("api/ventas/condiciones-lectura")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CondicionesLectura)]
public sealed class CondicionesLecturaController : ControllerBase
{
    private readonly ICompanyAccessValidator accessValidator;
    private readonly ICondicionesLecturaService condicionesService;

    public CondicionesLecturaController(
        ICompanyAccessValidator accessValidator,
        ICondicionesLecturaService condicionesService)
    {
        this.accessValidator = accessValidator;
        this.condicionesService = condicionesService;
    }

    /// <summary>Catálogo de la empresa: tipos (ref global) + condiciones editables.</summary>
    [HttpGet("{companyId:long}")]
    public async Task<IActionResult> Obtener(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            return Ok(await condicionesService.ObtenerAsync(companyId, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener las condiciones de lectura: {ex.Message}" });
        }
    }

    /// <summary>Persiste el conjunto de condiciones de la empresa (upsert + borra las que ya no vienen).</summary>
    [HttpPost("{companyId:long}")]
    public async Task<IActionResult> Guardar(long companyId, [FromBody] List<CondicionLecturaAdminDto> condiciones, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await condicionesService.GuardarAsync(companyId, condiciones ?? new(), usuario, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException() is PostgresException pg ? pg.MessageText : ex.GetBaseException().Message;
            return BadRequest(new { detail = $"Error al guardar las condiciones de lectura: {raiz}" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar las condiciones de lectura: {ex.Message}" });
        }
    }
}
