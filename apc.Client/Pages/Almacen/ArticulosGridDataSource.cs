using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Almacen;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Pages.Almacen;

/// <summary>
/// Fuente de datos remota del grid de artículos: pagina, ordena y filtra en el servidor
/// (<see cref="ArticulosClient.SearchPagedAsync"/>). Reemplaza la carga completa del
/// catálogo en memoria. Patrón espejo de ClientesGridDataSource.
/// </summary>
public sealed class ArticulosGridDataSource : GridCustomDataSource
{
    private readonly ArticulosClient _client;
    private readonly Action<int>? _onTotalCountChanged;

    public ArticulosGridDataSource(ArticulosClient client, Action<int>? onTotalCountChanged = null)
    {
        _client = client;
        _onTotalCountChanged = onTotalCountChanged;
    }

    /// <summary>
    /// Filtro activo (búsqueda + tipo/categoría/bodega/estado/unidad). La página lo actualiza
    /// y llama a <c>grid.Reload()</c> para repaginar en el servidor.
    /// </summary>
    public ArticuloFilterDto Filtro { get; set; } = new();

    public int TotalCount { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.SearchPagedAsync(
                Filtro, skip: 0, take: 1, sortField: null, sortDesc: false, ct: cancellationToken);

            SetTotal(result.TotalCount);
            return result.TotalCount;
        }
        catch
        {
            SetTotal(0);
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _client.SearchPagedAsync(
                Filtro,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetTotal(result.TotalCount);
            return new List<ArticuloListItemDto>(result.Items);
        }
        catch
        {
            return new List<ArticuloListItemDto>();
        }
    }

    private void SetTotal(int total)
    {
        if (TotalCount == total) return;
        TotalCount = total;
        _onTotalCountChanged?.Invoke(total);
    }

    private static (string? FieldName, bool Descending) GetSortInfo(IReadOnlyList<GridCustomDataSourceSortInfo>? sortInfo)
    {
        if (sortInfo is null || sortInfo.Count == 0)
            return (null, false);

        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
