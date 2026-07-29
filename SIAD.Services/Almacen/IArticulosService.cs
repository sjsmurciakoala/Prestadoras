using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Almacen;

public interface IArticulosService
{
    Task<IReadOnlyList<ArticuloListItemDto>> GetAsync(ArticuloFilterDto? filtro, CancellationToken ct = default);

    /// <summary>Página del catálogo para el grid remoto (server-side paging/sort sobre el filtro).</summary>
    Task<PagedResult<ArticuloListItemDto>> SearchPagedAsync(
        ArticuloFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);

    /// <summary>KPIs (totales) del catálogo calculados en el servidor sobre el mismo filtro del grid.</summary>
    Task<ArticulosResumenDto> GetResumenAsync(ArticuloFilterDto? filtro, CancellationToken ct = default);

    Task<IReadOnlyList<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro, CancellationToken ct = default);
    Task<ArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ArticuloEditDto> CreateAsync(ArticuloEditDto dto, string user, CancellationToken ct = default);
    Task<ArticuloEditDto> UpdateAsync(int id, ArticuloEditDto dto, string user, CancellationToken ct = default);

    /// <summary>Descontinúa el artículo (soft-delete): se conserva y su kardex sigue consultable.</summary>
    Task<bool> DeleteAsync(int id, string user, CancellationToken ct = default);

    /// <summary>Reactiva un artículo descontinuado.</summary>
    Task<bool> ReactivarAsync(int id, string user, CancellationToken ct = default);
}
