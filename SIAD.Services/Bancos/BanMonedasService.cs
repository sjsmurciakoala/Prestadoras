using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Bancos;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class BanMonedasService : IBanMonedasService
{
    private readonly SiadDbContext context;

    public BanMonedasService(SiadDbContext context)
    {
        this.context = context;
    }

    public async Task<IReadOnlyList<BanMonedaLookupDto>> GetAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return Array.Empty<BanMonedaLookupDto>();
        }

        return await context.ban_moneda
            .AsNoTracking()
            .Where(m => m.company_id == companyId)
            .OrderBy(m => m.descripcion)
            .Select(m => new BanMonedaLookupDto
            {
                BanMonedaId = m.ban_moneda_id,
                Codigo = m.codigo,
                Descripcion = m.descripcion
            })
            .ToListAsync(ct);
    }
}
