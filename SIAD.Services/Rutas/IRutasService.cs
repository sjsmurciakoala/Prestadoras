using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Rutas;

namespace SIAD.Services.Rutas;

public interface IRutasService
{
    Task<IReadOnlyList<RutaListItemDto>> GetRutasAsync(RutaFilterDto filtro, CancellationToken ct = default);
    Task<PagedResult<RutaListItemDto>> GetRutasPagedAsync(RutaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<RutaDetailDto?> GetRutaAsync(int id, CancellationToken ct = default);
    Task<int> CreateRutaAsync(RutaUpsertDto dto, string user, CancellationToken ct = default);
    Task UpdateRutaAsync(int id, RutaUpsertDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateRutaAsync(int id, string user, CancellationToken ct = default);
    Task<IReadOnlyList<CicloLookupDto>> GetCiclosAsync(CancellationToken ct = default);
}
