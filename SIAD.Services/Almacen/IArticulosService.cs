using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IArticulosService
{
    Task<IReadOnlyList<ArticuloListItemDto>> GetAsync(ArticuloFilterDto? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro, CancellationToken ct = default);
    Task<ArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ArticuloEditDto> CreateAsync(ArticuloEditDto dto, string user, CancellationToken ct = default);
    Task<ArticuloEditDto> UpdateAsync(int id, ArticuloEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
