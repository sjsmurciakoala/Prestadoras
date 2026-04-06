using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Tenancy;
using SIAD.Services.Contabilidad;
using apc.Security;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/empresas")]
[ModuleAuthorize(PermissionModules.Contabilidad)]
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

    [HttpPost("{companyId}/logo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirLogo(long companyId, [FromForm(Name = "logoUpload")] IFormFile? archivo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            var detalle = errores.Count == 0 ? "Carga de logo inválida." : string.Join("; ", errores);
            return BadRequest(CrearProblemDetalle("Solicitud inválida", detalle));
        }

        // Aceptar tanto "logoUpload" como el primer archivo disponible por si el cliente usa otro nombre
        var archivoCargado = archivo ?? Request.Form.Files.FirstOrDefault();

        if (archivoCargado is null || archivoCargado.Length == 0)
        {
            return BadRequest(CrearProblemDetalle("Archivo requerido", "Debe proporcionar un archivo de imagen."));
        }

        // Validaciones básicas
        const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
        if (archivoCargado.Length > MaxFileSize)
        {
            return BadRequest(CrearProblemDetalle("Archivo demasiado grande", "El logo no puede superar los 5MB."));
        }

        // Validar extensión del archivo
        var extension = Path.GetExtension(archivoCargado.FileName)?.ToLowerInvariant();
        var extensionesPermitidas = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
        if (string.IsNullOrEmpty(extension) || !extensionesPermitidas.Contains(extension))
        {
            return BadRequest(CrearProblemDetalle("Tipo de archivo no válido", 
                "Solo se permiten imágenes: PNG, JPG, JPEG, WEBP, SVG."));
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            using var memoryStream = new MemoryStream();
            await archivoCargado.CopyToAsync(memoryStream, ct);
            var logoBytes = memoryStream.ToArray();

            await companyManagementService.GuardarLogoAsync(companyId, logoBytes, usuario, ct);
            return Ok(new { mensaje = "Logo guardado correctamente" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Validación fallida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(CrearProblemDetalle("Empresa no encontrada", ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                CrearProblemDetalle("Error al guardar el logo", ex.Message));
        }
    }

    [HttpGet("{companyId}/logo")]
    public async Task<IActionResult> ObtenerLogo(long companyId, CancellationToken ct)
    {
        try
        {
            var resultado = await companyManagementService.ObtenerLogoAsync(companyId, ct);
            if (resultado is null)
            {
                return NotFound();
            }

            var (logoBytes, contentType) = resultado.Value;
            return File(logoBytes, contentType ?? "image/png");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(CrearProblemDetalle("ID inválido", ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                CrearProblemDetalle("Error al obtener el logo", ex.Message));
        }
    }
}



