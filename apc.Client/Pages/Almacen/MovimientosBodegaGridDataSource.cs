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
/// Fuente de datos remota del grid del libro de movimientos de bodega: pagina y ordena en
/// el servidor (<see cref="KardexClient.GetMovimientosBodegaPagedAsync"/>). Sin bodega en el
/// filtro no consulta (devuelve vacío). Patrón espejo de ArticulosGridDataSource.
/// </summary>
public sealed class MovimientosBodegaGridDataSource : GridCustomDataSource
{
    private readonly KardexClient _client;
    private readonly Action<int>? _onTotalCountChanged;

    public MovimientosBodegaGridDataSource(KardexClient client, Action<int>? onTotalCountChanged = null)
    {
        _client = client;
        _onTotalCountChanged = onTotalCountChanged;
    }

    /// <summary>Filtro activo (bodega + período/tipo/artículo/búsqueda). La página lo actualiza y recarga el grid.</summary>
    public MovimientosBodegaFilterDto Filtro { get; set; } = new();

    public int TotalCount { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        if (!Filtro.BodegaId.HasValue)
        {
            SetTotal(0);
            return 0;
        }

        try
        {
            var result = await _client.GetMovimientosBodegaPagedAsync(
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
        if (!Filtro.BodegaId.HasValue)
        {
            return new List<MovimientoBodegaItemDto>();
        }

        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _client.GetMovimientosBodegaPagedAsync(
                Filtro,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetTotal(result.TotalCount);
            return new List<MovimientoBodegaItemDto>(result.Items);
        }
        catch
        {
            return new List<MovimientoBodegaItemDto>();
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
