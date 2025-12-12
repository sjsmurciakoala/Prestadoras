using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Tenancy;
using SIAD.Services.Contabilidad;
using SIAD.Services.Files;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/empresas")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public sealed class ContabilidadEmpresaController : ControllerBase
{
    private readonly ICompanyManagementService companyManagementService;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly IFileStorageService fileStorageService;

    public ContabilidadEmpresaController(ICompanyManagementService companyManagementService,
        ICurrentCompanyService currentCompanyService,
        IFileStorageService fileStorageService)
    {
        this.companyManagementService = companyManagementService;
        this.currentCompanyService = currentCompanyService;
        this.fileStorageService = fileStorageService;
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CompanyCreationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            var detalle = string.Join("; ", errores);
            return BadRequest(CrearProblemDetalle("Validación fallida", detalle));
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
            return BadRequest(CrearProblemDetalle("Error al guardar", raiz));
        }
    }

    [HttpPost("{companyId}/logo")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> CargarLogo(long companyId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(CrearProblemDetalle("Archivo inválido", "Debes adjuntar un archivo de imagen."));
        }

        if (!fileStorageService.IsValidImageFile(file.FileName, file.ContentType))
        {
            return BadRequest(CrearProblemDetalle("Formato inválido", "Solo se permiten imágenes PNG, JPG, GIF o SVG."));
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            await using var stream = file.OpenReadStream();
            var relativePath = await fileStorageService.SaveCompanyLogoAsync(companyId, stream, file.FileName, file.ContentType, ct);
            await companyManagementService.ActualizarLogoAsync(companyId, relativePath, usuario, ct);

            return Ok(new { logoUrl = relativePath });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblemDetalle("No fue posible cargar el logo", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Datos invalidos", ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                CrearProblemDetalle("Error al cargar el logo", ex.Message));
        }
    }

    [HttpGet("{companyId}")]
    public async Task<IActionResult> Obtener(long companyId, CancellationToken ct)
    {
        try
        {
            var resultado = await companyManagementService.ObtenerAsync(companyId, ct);
            if (resultado is null)
            {
                return NotFound(CrearProblemDetalle("Empresa no encontrada",
                    $"No existe una empresa con el ID {companyId}."));
            }

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                CrearProblemDetalle("Error al obtener la empresa", ex.Message));
        }
    }

    [HttpPut("{companyId}")]
    public async Task<IActionResult> Actualizar(long companyId, [FromBody] CompanyCreationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await companyManagementService.ActualizarAsync(companyId, dto, usuario, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(CrearProblemDetalle("No fue posible actualizar la empresa", ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Datos invalidos", ex.Message));
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException()?.Message ?? ex.Message;
            return BadRequest($"Error al actualizar la empresa: {raiz}");
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

