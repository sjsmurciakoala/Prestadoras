using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Tenant;
using SIAD.Data;

namespace SIAD.Services.Tenancy;

public sealed class TenantCompanyService : ITenantCompanyService
{
    private readonly SiadDbContext _context;

    public TenantCompanyService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TenantCompanyDto>> ObtenerEmpresasAsync(CancellationToken ct = default)
    {
        return await _context.cfg_companies
            .AsNoTracking()
            .OrderBy(c => c.commercial_name)
            .Select(c => new TenantCompanyDto
            {
                CompanyId = c.company_id,
                Code = c.code,
                Name = c.commercial_name
            })
            .ToListAsync(ct);
    }

    public Task<bool> ExisteEmpresaAsync(long companyId, CancellationToken ct = default)
    {
        return _context.cfg_companies
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId, ct);
    }
}
