using SIAD.Core.DTOs.Rutas;

namespace SIAD.Services.Rutas;

public interface IRutasService
{
    Task<IReadOnlyList<RutaListItemDto>> GetRutasAsync(RutaFilterDto filtro, CancellationToken ct = default);
    Task<RutaDetailDto?> GetRutaAsync(int id, CancellationToken ct = default);
    Task<int> CreateRutaAsync(RutaUpsertDto dto, CancellationToken ct = default);
    Task UpdateRutaAsync(int id, RutaUpsertDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<CicloLookupDto>> GetCiclosAsync(CancellationToken ct = default);
}
