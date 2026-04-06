namespace SIAD.Services.Branding;
public interface IBrandingService
{
    Task<Core.DTOs.Branding.BrandingDto?> GetBrandingAsync(CancellationToken ct = default);
    Task GuardarBrandingAsync(string companyName, string companyShortName, CancellationToken ct = default);
    Task GuardarLogoAsync(byte[] logoBytes, string logoMime, CancellationToken ct = default);
}
