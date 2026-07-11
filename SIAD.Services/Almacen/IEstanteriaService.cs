using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IEstanteriaService
{
    Task<IReadOnlyList<EstanteriaListItemDto>> GetAsync(UbicacionFilterDto? filtro, CancellationToken ct = default);
    Task<EstanteriaEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<EstanteriaLookupDto>> GetLookupAsync(int bodegaId, CancellationToken ct = default);
    Task<EstanteriaEditDto> CreateAsync(EstanteriaEditDto dto, string user, CancellationToken ct = default);
    Task<EstanteriaEditDto> UpdateAsync(int id, EstanteriaEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
