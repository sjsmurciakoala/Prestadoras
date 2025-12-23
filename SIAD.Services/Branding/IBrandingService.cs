namespace SIAD.Services.Branding;
public interface IBrandingService
{
    Task<Core.DTOs.Branding.BrandingDto?> GetBrandingAsync(CancellationToken ct = default);
}
