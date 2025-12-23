using Microsoft.AspNetCore.Mvc;
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
}
