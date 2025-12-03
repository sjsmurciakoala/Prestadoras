using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Tenancy;
using SIAD.Services.Contabilidad;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/empresas")]
[Authorize(Roles = RoleNames.AdminContabilidad + "," + RoleNames.SuperAdministrador)]
public sealed class ContabilidadEmpresaController : ControllerBase
{
    private readonly ICompanyManagementService companyManagementService;
    private readonly ICurrentCompanyService currentCompanyService;

    public ContabilidadEmpresaController(ICompanyManagementService companyManagementService,
        ICurrentCompanyService currentCompanyService)
    {
        this.companyManagementService = companyManagementService;
        this.currentCompanyService = currentCompanyService;
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CompanyCreationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tenantCompanyId = currentCompanyService.GetCompanyId();
        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await companyManagementService.CrearAsync(tenantCompanyId, dto, usuario, ct);
            return Created($"api/contabilidad/empresas/{resultado.CompanyId}", resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblemDetalle("No fue posible crear la empresa", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Datos invalidos", ex.Message));
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException()?.Message ?? ex.Message;
            return BadRequest($"Error al guardar la empresa: {raiz}");
        }
    }

    private static ProblemDetails CrearProblemDetalle(string titulo, string detalle)
    {
        return new ProblemDetails
        {
            Title = titulo,
            Detail = detalle,
            Status = StatusCodes.Status400BadRequest
        };
    }
}
