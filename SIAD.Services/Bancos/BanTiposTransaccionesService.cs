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

        // emite_cheque es texto libre ('S'/'Y'/'1'/...): se agrupa en memoria (catalogo
        // pequeno) para poder evaluarlo con ChequeEmisionFlag sin traducirlo a SQL.
        var filas = await context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId)
            .Select(t => new { t.tipo_transaccion, t.nombre, t.entra_sale, t.emite_cheque })
            .ToListAsync(ct);

        return filas
            .GroupBy(t => new { t.tipo_transaccion, t.nombre, t.entra_sale })
            .Select(g => new BanTipoTransaccionListDto
            {
                TipoTransaccion = g.Key.tipo_transaccion,
                Nombre = g.Key.nombre,
                EntraSale = g.Key.entra_sale,
                EmiteCheque = g.Any(x => ChequeEmisionFlag.EsAfirmativo(x.emite_cheque))
            })
            .OrderBy(t => t.TipoTransaccion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
