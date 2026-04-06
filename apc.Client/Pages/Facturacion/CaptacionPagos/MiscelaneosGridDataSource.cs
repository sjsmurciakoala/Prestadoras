using System.Collections;
using System.Collections.Generic;
using DevExpress.Blazor;
using apc.Client.Services.CaptacionPagos;
using SIAD.Core.DTOs.CaptacionPagos;

namespace apc.Client.Pages.Facturacion.CaptacionPagos;

public sealed class MiscelaneosGridDataSource : GridCustomDataSource
{
    private readonly CaptacionPagosClient _client;
    private readonly Action<int>? _onTotalCountChanged;

    public MiscelaneosGridDataSource(CaptacionPagosClient client, Action<int>? onTotalCountChanged = null)
    {
        _client = client;
        _onTotalCountChanged = onTotalCountChanged;
    }

    public string? ClienteClave { get; set; }

    public int TotalCount { get; private set; } = -1;

    public void PrepareForRefresh()
    {
        // Force the first total callback after a user-triggered reload,
        // even when the total count repeats (e.g., 0 -> 0).
        TotalCount = -1;
    }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ClienteClave))
        {
            SetTotal(0);
            return 0;
        }

        try
        {
            var result = await _client.GetMiscelaneosPagedAsync(
                ClienteClave,
                skip: 0,
                take: 1,
                sortField: null,
                sortDesc: false,
                ct: cancellationToken);

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
        if (string.IsNullOrWhiteSpace(ClienteClave))
        {
            return new List<ReciboMiscelaneoDto>();
        }

        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);
            var result = await _client.GetMiscelaneosPagedAsync(
                ClienteClave,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetTotal(result.TotalCount);
            return new List<ReciboMiscelaneoDto>(result.Items);
        }
        catch
        {
            return new List<ReciboMiscelaneoDto>();
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
        {
            return (null, false);
        }

        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
