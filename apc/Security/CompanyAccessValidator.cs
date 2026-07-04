using Microsoft.EntityFrameworkCore;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace apc.Security;

/// <summary>
/// Validación única de acceso a empresa para endpoints que reciben
/// {companyId} en la ruta: el id pedido debe coincidir con el tenant del
/// usuario (resuelto de claims vía <see cref="ICurrentCompanyService"/> —
/// nunca se confía en el companyId del request) y existir en cfg_companies.
/// Reemplaza las copias privadas ValidarAccesoEmpresaAsync por controller,
/// una de las cuales había divergido sin la comparación contra el tenant.
/// </summary>
public interface ICompanyAccessValidator
{
    Task<bool> ValidarAccesoAsync(long companyId, CancellationToken ct = default);
}

public sealed class CompanyAccessValidator : ICompanyAccessValidator
{
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;

    public CompanyAccessValidator(SiadDbContext dbContext, ICurrentCompanyService currentCompanyService)
    {
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
    }

    public async Task<bool> ValidarAccesoAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return false;
        }

        // Claim primero (gratis): no ir a la BD por un id que no es el del
        // tenant actual.
        var companyIdActual = currentCompanyService.GetCompanyId();
        if (companyIdActual <= 0 || companyIdActual != companyId)
        {
            return false;
        }

        return await dbContext.cfg_companies
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId, ct);
    }
}
