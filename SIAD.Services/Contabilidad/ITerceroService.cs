using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

public interface ITerceroService
{
    Task<IReadOnlyList<TerceroDto>> GetTercerosAsync(CancellationToken ct = default);
    Task<long> SaveTerceroAsync(TerceroUpsertDto request, CancellationToken ct = default);
    Task<bool> DeleteTerceroAsync(long thirdPartyId, CancellationToken ct = default);
    Task<SincronizarTercerosResultDto> SincronizarDesdeProveedoresYClientesAsync(string userId, CancellationToken ct = default);
}
