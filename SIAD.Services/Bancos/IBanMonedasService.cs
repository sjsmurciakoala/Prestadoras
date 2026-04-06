using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IBanMonedasService
{
    Task<IReadOnlyList<BanMonedaLookupDto>> GetAsync(long companyId, CancellationToken ct = default);
}
