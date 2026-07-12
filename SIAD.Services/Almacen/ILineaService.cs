using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface ILineaService
{
    Task<IReadOnlyList<LineaListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default);
    Task<LineaEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<LineaLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<LineaEditDto> CreateAsync(LineaEditDto dto, string user, CancellationToken ct = default);
    Task<LineaEditDto> UpdateAsync(int id, LineaEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
