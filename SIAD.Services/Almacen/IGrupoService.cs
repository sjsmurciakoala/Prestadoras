using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IGrupoService
{
    Task<IReadOnlyList<GrupoListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default);
    Task<GrupoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<GrupoLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<GrupoEditDto> CreateAsync(GrupoEditDto dto, string user, CancellationToken ct = default);
    Task<GrupoEditDto> UpdateAsync(int id, GrupoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
