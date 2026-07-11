using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IBodegaService
{
    Task<IReadOnlyList<BodegaListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default);
    Task<BodegaEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<BodegaLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<BodegaEditDto> CreateAsync(BodegaEditDto dto, string user, CancellationToken ct = default);
    Task<BodegaEditDto> UpdateAsync(int id, BodegaEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
