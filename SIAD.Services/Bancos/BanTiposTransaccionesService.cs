using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class BanTiposTransaccionesService : IBanTiposTransaccionesService
{
    private readonly SiadDbContext context;
    private readonly ICurrentCompanyService currentCompanyService;

    public BanTiposTransaccionesService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService)
    {
        this.context = context;
        this.currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<BanTipoTransaccionListDto>> GetAsync(long companyId, CancellationToken ct = default)
    {
        var currentCompanyId = EnsureCompanyId();
        if (companyId <= 0 || companyId != currentCompanyId)
        {
            throw new InvalidOperationException("La empresa solicitada no es valida para el usuario actual.");
        }

        var items = await context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId)
            .GroupBy(t => new { t.tipo_transaccion, t.nombre, t.entra_sale })
            .Select(g => g.Key)
            .OrderBy(t => t.tipo_transaccion)
            .ThenBy(t => t.nombre)
            .Select(t => new BanTipoTransaccionListDto
            {
                TipoTransaccion = t.tipo_transaccion,
                Nombre = t.nombre,
                EntraSale = t.entra_sale
            })
            .ToListAsync(ct);

        return items;
    }

    private long EnsureCompanyId()
    {
        var companyId = currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual de la sesion.");
        }

        return companyId;
    }
}
