using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SIAD.Core.DTOs.Branding;
using SIAD.Data;

namespace SIAD.Services.Branding;

public class BrandingService : IBrandingService
{
    private readonly SiadDbContext _context;
    private readonly ILogger<BrandingService> _logger;

    public BrandingService(SiadDbContext context, ILogger<BrandingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BrandingDto?> GetBrandingAsync(CancellationToken ct = default)
    {
        try
        {
            var branding = await _context.portal_brandings
                .AsNoTracking()
                .OrderByDescending(b => b.updated_at)
                .FirstOrDefaultAsync(ct);

            return branding is null
                ? null
                : new BrandingDto(
                    branding.company_name ?? string.Empty,
                    branding.company_short_name ?? string.Empty,
                    branding.logo_mime ?? "image/png",
                    branding.logo_bytes ?? System.Array.Empty<byte>());
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning(ex, "La tabla portal_branding no existe en la base de datos. Se omitirá el branding.");
            return null;
        }
    }

    public async Task<BrandingDto> UpsertBrandingAsync(string companyName, string companyShortName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        companyName = companyName.Trim();
        companyShortName = companyShortName?.Trim() ?? string.Empty;

        var now = DateTime.UtcNow;

        var branding = await _context.portal_brandings
            .OrderByDescending(b => b.updated_at)
            .FirstOrDefaultAsync(ct);

        if (branding is null)
        {
            branding = new Core.Entities.portal_branding
            {
                company_name = companyName,
                company_short_name = companyShortName,
                logo_mime = "image/png",
                logo_bytes = Array.Empty<byte>(),
                updated_at = now
            };

            _context.portal_brandings.Add(branding);
        }
        else
        {
            branding.company_name = companyName;
            branding.company_short_name = companyShortName;
            branding.updated_at = now;
            _context.portal_brandings.Update(branding);
        }

        await _context.SaveChangesAsync(ct);

        return new BrandingDto(
            branding.company_name,
            branding.company_short_name,
            branding.logo_mime ?? "image/png",
            branding.logo_bytes ?? Array.Empty<byte>());
    }

    public async Task<BrandingDto> UpdateLogoAsync(string companyName, string companyShortName, string logoMime,
        byte[] logoBytes, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logoMime);
        ArgumentNullException.ThrowIfNull(logoBytes);

        var branding = await _context.portal_brandings
            .OrderByDescending(b => b.updated_at)
            .FirstOrDefaultAsync(ct);

        if (branding is null)
        {
            branding = new Core.Entities.portal_branding();
            _context.portal_brandings.Add(branding);
        }

        branding.company_name = companyName.Trim();
        branding.company_short_name = companyShortName?.Trim() ?? string.Empty;
        branding.logo_mime = logoMime.Trim();
        branding.logo_bytes = logoBytes;
        branding.updated_at = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return new BrandingDto(
            branding.company_name,
            branding.company_short_name,
            branding.logo_mime,
            branding.logo_bytes);
    }
}
