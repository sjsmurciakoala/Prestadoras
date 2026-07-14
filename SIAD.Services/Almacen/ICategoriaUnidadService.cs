using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface ICategoriaUnidadService
{
    Task<IReadOnlyList<CategoriaUnidadListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default);
    Task<CategoriaUnidadEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoriaUnidadLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<CategoriaUnidadEditDto> CreateAsync(CategoriaUnidadEditDto dto, string user, CancellationToken ct = default);
    Task<CategoriaUnidadEditDto> UpdateAsync(int id, CategoriaUnidadEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
