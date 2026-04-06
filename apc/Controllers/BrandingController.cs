using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.Branding;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _branding;

    public BrandingController(IBrandingService branding) => _branding = branding;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var branding = await _branding.GetBrandingAsync(ct);
        if (branding is null)
        {
            return NoContent();
        }

        var hasLogo = branding.LogoBytes is { Length: > 0 };
        return Ok(new
        {
            branding.CompanyName,
            branding.CompanyShortName,
            LogoBase64 = hasLogo ? Convert.ToBase64String(branding.LogoBytes) : null,
            branding.LogoMime
        });
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public async Task<IActionResult> Actualizar([FromBody] BrandingUpdateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _branding.GuardarBrandingAsync(request.CompanyName, request.CompanyShortName, ct);
            return Ok(new { mensaje = "Branding actualizado correctamente." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Datos inválidos", Detail = ex.Message });
        }
    }

    [HttpPost("logo")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirLogo([FromForm(Name = "logoFile")] IFormFile? archivo, CancellationToken ct)
    {
        var archivoCargado = archivo ?? Request.Form.Files.FirstOrDefault();

        if (archivoCargado is null || archivoCargado.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Archivo requerido", Detail = "Debe proporcionar un archivo de imagen." });
        }

        if (archivoCargado.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new ProblemDetails { Title = "Archivo demasiado grande", Detail = "El logo no puede superar los 5MB." });
        }

        var extension = Path.GetExtension(archivoCargado.FileName)?.ToLowerInvariant();
        var extensionesPermitidas = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
        if (string.IsNullOrEmpty(extension) || !extensionesPermitidas.Contains(extension))
        {
            return BadRequest(new ProblemDetails { Title = "Tipo de archivo no válido", Detail = "Solo se permiten imágenes: PNG, JPG, JPEG, WEBP, SVG." });
        }

        try
        {
            using var memoryStream = new MemoryStream();
            await archivoCargado.CopyToAsync(memoryStream, ct);
            var logoBytes = memoryStream.ToArray();
            var mime = archivoCargado.ContentType ?? "image/png";

            await _branding.GuardarLogoAsync(logoBytes, mime, ct);
            return Ok(new { mensaje = "Logo guardado correctamente." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Validación fallida", Detail = ex.Message });
        }
    }

    public sealed class BrandingUpdateRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyShortName { get; set; } = string.Empty;
    }
}
