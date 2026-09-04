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
            LogoMime = hasLogo ? TipoDeImagen(branding.LogoBytes, branding.LogoMime) : branding.LogoMime
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
            // IFormFile.ContentType devuelve cadena VACIA cuando el navegador no lo manda,
            // nunca null: por eso el "?? " de antes no se activaba y se guardaba vacio, y el
            // logo no se podia pintar. Se deduce de los bytes, que es lo fiable.
            var mime = TipoDeImagen(logoBytes, archivoCargado.ContentType);

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

    /// <summary>
    /// Devuelve el tipo de imagen del logo. Se prefiere el guardado, pero hay filas antiguas con
    /// <c>logo_mime</c> vacio: sin tipo, un data URI no se pinta como imagen, asi que se deduce
    /// de los bytes. Ultimo recurso PNG, que es el formato con el que se suben casi todos.
    /// </summary>
    private static string TipoDeImagen(byte[] bytes, string? guardado)
    {
        if (!string.IsNullOrWhiteSpace(guardado))
        {
            return guardado;
        }

        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
        {
            return "image/gif";
        }

        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        if (bytes.Length >= 1 && bytes[0] == (byte)'<')
        {
            return "image/svg+xml";
        }

        return "image/png";
    }
}
