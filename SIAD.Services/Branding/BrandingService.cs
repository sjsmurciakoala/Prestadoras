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
            _logger.LogWarning(ex, "La tabla portal_branding no existe en la base de datos. Se omitira el branding.");
            return null;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout consultando portal_brandings. Se omitira el branding.");
            return null;
        }
        catch (InvalidOperationException ex) when (ex.GetBaseException() is TimeoutException)
        {
            _logger.LogWarning(ex, "Timeout consultando portal_brandings. Se omitira el branding.");
            return null;
        }
    }

    public async Task GuardarBrandingAsync(string companyName, string companyShortName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);

        var branding = await _context.portal_brandings
            .OrderByDescending(b => b.updated_at)
            .FirstOrDefaultAsync(ct);

        if (branding is null)
        {
            branding = new Core.Entities.portal_branding
            {
                company_name = companyName.Trim(),
                company_short_name = (companyShortName ?? string.Empty).Trim(),
                logo_mime = "image/png",
                logo_bytes = Array.Empty<byte>(),
                updated_at = DateTime.UtcNow
            };
            _context.portal_brandings.Add(branding);
        }
        else
        {
            branding.company_name = companyName.Trim();
            branding.company_short_name = (companyShortName ?? string.Empty).Trim();
            branding.updated_at = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task GuardarLogoAsync(byte[] logoBytes, string logoMime, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(logoBytes);

        if (logoBytes.Length == 0)
        {
            throw new ArgumentException("El logo no puede estar vacío.", nameof(logoBytes));
        }

        if (logoBytes.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("El logo no puede superar los 5MB.", nameof(logoBytes));
        }

        var branding = await _context.portal_brandings
            .OrderByDescending(b => b.updated_at)
            .FirstOrDefaultAsync(ct);

        if (branding is null)
        {
            branding = new Core.Entities.portal_branding
            {
                company_name = "Mi Empresa",
                company_short_name = string.Empty,
                logo_mime = logoMime ?? "image/png",
                logo_bytes = logoBytes,
                updated_at = DateTime.UtcNow
            };
            _context.portal_brandings.Add(branding);
        }
        else
        {
            branding.logo_bytes = logoBytes;
            branding.logo_mime = logoMime ?? "image/png";
            branding.updated_at = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }
}
