using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using apc.Security;

namespace apc.Controllers.Informes;

[ApiController]
[Route("api/informes/reportes")]
[ModuleAuthorize(PermissionModules.Reporteria)]
public sealed class ReportesDisenoController : ControllerBase
{
    private readonly IReportesDisenoService _service;
    private readonly ICurrentCompanyService _currentCompany;
    private readonly ReportDraftRegenerationService _draftRegeneration;

    public ReportesDisenoController(
        IReportesDisenoService service,
        ICurrentCompanyService currentCompany,
        ReportDraftRegenerationService draftRegeneration)
    {
        _service = service;
        _currentCompany = currentCompany;
        _draftRegeneration = draftRegeneration;
    }

    [HttpGet]
    public async Task<IActionResult> GetCatalogo(CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var items = await _service.ListarAsync(companyId, ct);
        return Ok(items);
    }

    [HttpGet("{codigo}")]
    public async Task<IActionResult> GetDetalle(string codigo, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var item = await _service.ObtenerAsync(companyId, codigo, ct);
        if (item is null)
        {
            return NotFound(CrearProblem("Reporte no encontrado", "No existe un reporte registrado con el código solicitado."));
        }

        return Ok(item);
    }

    [HttpPost]
    [ModuleAuthorize(PermissionModules.Reporteria, PermissionAction.Create)]
    public async Task<IActionResult> Crear([FromBody] ReporteDisenoCreateDto dto, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            var result = await _service.CrearAsync(companyId, dto, ResolveActor(), ct);
            return CreatedAtAction(nameof(GetDetalle), new { codigo = result.Codigo }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible crear el reporte", ex.Message));
        }
    }

    [HttpPost("{codigo}/publicar")]
    [ModuleAuthorize(PermissionModules.Reporteria, PermissionAction.Edit)]
    public async Task<IActionResult> Publicar(string codigo, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            var result = await _service.PublicarAsync(companyId, codigo, ResolveActor(), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible publicar el reporte", ex.Message));
        }
    }

    [HttpPost("{codigo}/regenerar-borrador")]
    [ModuleAuthorize(PermissionModules.Reporteria, PermissionAction.Edit)]
    public async Task<IActionResult> RegenerarBorrador(string codigo, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            await _draftRegeneration.RegenerarBorradorDesdeDatasetActualAsync(companyId, codigo, ResolveActor(), ct);
            var result = await _service.ObtenerAsync(companyId, codigo, ct);
            if (result is null)
            {
                return NotFound(CrearProblem("Reporte no encontrado", "No existe un reporte registrado con el código solicitado."));
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible regenerar el borrador", ex.Message));
        }
    }

    [HttpDelete("{codigo}")]
    [ModuleAuthorize(PermissionModules.Reporteria, PermissionAction.Delete)]
    public async Task<IActionResult> Eliminar(string codigo, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            await _service.EliminarAsync(companyId, codigo, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible eliminar el reporte", ex.Message));
        }
    }

    private string ResolveActor()
        => User.Identity?.Name
           ?? User.FindFirst(ClaimTypes.Email)?.Value
           ?? "reporteria-web";

    private static ProblemDetails CrearProblem(string titulo, string detalle) => new()
    {
        Title = titulo,
        Detail = detalle
    };
}
