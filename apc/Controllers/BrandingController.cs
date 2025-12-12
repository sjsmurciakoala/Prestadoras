using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIAD.Services.Branding;
using SIAD.Core.DTOs.Branding;

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
    public async Task<IActionResult> Upsert([FromBody] BrandingUpdateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _branding.UpsertBrandingAsync(dto.CompanyName, dto.CompanyShortName, ct);
        return Ok(result);
    }

    [HttpPost("logo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo([FromQuery] string? companyName, [FromQuery] string? companyShortName, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Debes adjuntar un archivo de imagen." });
        }

        companyName = companyName?.Trim() ?? "Portal";
        companyShortName = companyShortName?.Trim() ?? string.Empty;

        var allowed = new[] { "image/png", "image/jpeg", "image/gif", "image/svg+xml" };
        if (string.IsNullOrWhiteSpace(file.ContentType) || !allowed.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest(new { message = "Solo se permiten PNG, JPG, GIF o SVG." });
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var result = await _branding.UpdateLogoAsync(
            companyName,
            companyShortName,
            file.ContentType,
            ms.ToArray(),
            ct);

        return Ok(new
        {
            result.CompanyName,
            result.CompanyShortName,
            LogoBase64 = Convert.ToBase64String(result.LogoBytes),
            result.LogoMime
        });
    }
}
