namespace SIAD.Services.Branding;
public interface IBrandingService
{
    Task<Core.DTOs.Branding.BrandingDto?> GetBrandingAsync(CancellationToken ct = default);

    Task<Core.DTOs.Branding.BrandingDto> UpsertBrandingAsync(string companyName, string companyShortName,
        CancellationToken ct = default);

    Task<Core.DTOs.Branding.BrandingDto> UpdateLogoAsync(string companyName, string companyShortName, string logoMime,
        byte[] logoBytes, CancellationToken ct = default);
}
