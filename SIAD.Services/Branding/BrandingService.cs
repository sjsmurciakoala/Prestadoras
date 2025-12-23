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
}
