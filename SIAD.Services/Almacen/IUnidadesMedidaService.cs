using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IUnidadesMedidaService
{
    Task<IReadOnlyList<UnidadMedidaListItemDto>> GetAsync(UnidadMedidaFilterDto? filtro, CancellationToken ct = default);
    Task<UnidadMedidaEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<UnidadMedidaLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCategoriasAsync(CancellationToken ct = default);
    Task<UnidadMedidaEditDto> CreateAsync(UnidadMedidaEditDto dto, string user, CancellationToken ct = default);
    Task<UnidadMedidaEditDto> UpdateAsync(int id, UnidadMedidaEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
