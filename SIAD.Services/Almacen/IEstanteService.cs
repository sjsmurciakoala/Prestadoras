using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IEstanteService
{
    Task<IReadOnlyList<EstanteListItemDto>> GetAsync(UbicacionFilterDto? filtro, CancellationToken ct = default);
    Task<EstanteEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<EstanteLookupDto>> GetLookupAsync(int estanteriaId, CancellationToken ct = default);
    Task<EstanteEditDto> CreateAsync(EstanteEditDto dto, string user, CancellationToken ct = default);
    Task<EstanteEditDto> UpdateAsync(int id, EstanteEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
