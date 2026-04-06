using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using apc.Security;

namespace apc.Controllers.Informes;

[ApiController]
[Route("api/informes/reportes/datasets")]
[ModuleAuthorize(PermissionModules.Reporteria)]
public sealed class ReportesDatasetsController : ControllerBase
{
    private readonly IReportesDatasetService _service;
    private readonly ICurrentCompanyService _currentCompany;

    public ReportesDatasetsController(
        IReportesDatasetService service,
        ICurrentCompanyService currentCompany)
    {
        _service = service;
        _currentCompany = currentCompany;
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
            return NotFound(CrearProblem("Dataset no encontrado", "No existe un dataset registrado con el código solicitado."));
        }

        return Ok(item);
    }

    [HttpPost]
    [ModuleAuthorize(PermissionModules.Reporteria, PermissionAction.Create)]
    public async Task<IActionResult> Crear([FromBody] ReporteDatasetCreateDto dto, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            var result = await _service.CrearAsync(companyId, dto, ResolveActor(), AllowSql(), ct);
            return CreatedAtAction(nameof(GetDetalle), new { codigo = result.Codigo }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible registrar el dataset", ex.Message));
        }
    }

    [HttpPut("{codigo}")]
    [ModuleAuthorize(PermissionModules.Reporteria, PermissionAction.Edit)]
    public async Task<IActionResult> Actualizar(string codigo, [FromBody] ReporteDatasetCreateDto dto, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            var result = await _service.ActualizarAsync(companyId, codigo, dto, ResolveActor(), AllowSql(), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible actualizar el dataset", ex.Message));
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
            return BadRequest(CrearProblem("No fue posible eliminar el dataset", ex.Message));
        }
    }

    [HttpPost("{codigo}/probar")]
    public async Task<IActionResult> Probar(string codigo, [FromBody] ReporteDatasetPreviewRequestDto request, CancellationToken ct)
    {
        try
        {
            var companyId = _currentCompany.GetCompanyId();
            var result = await _service.ProbarAsync(companyId, codigo, request, AllowSql(), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblem("Solicitud inválida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblem("No fue posible probar el dataset", ex.Message));
        }
    }

    private bool AllowSql()
        => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.SuperAdministrador);

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
