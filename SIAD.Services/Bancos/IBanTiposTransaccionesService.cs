using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IBanTiposTransaccionesService
{
    Task<IReadOnlyList<BanTipoTransaccionListDto>> GetAsync(long companyId, CancellationToken ct = default);
}
