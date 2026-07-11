using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface ITipoArticuloService
{
    Task<IReadOnlyList<TipoArticuloListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default);
    Task<TipoArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TipoArticuloLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<TipoArticuloEditDto> CreateAsync(TipoArticuloEditDto dto, string user, CancellationToken ct = default);
    Task<TipoArticuloEditDto> UpdateAsync(int id, TipoArticuloEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
