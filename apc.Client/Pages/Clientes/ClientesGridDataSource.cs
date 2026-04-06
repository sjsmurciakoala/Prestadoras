using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Clientes;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Clientes;

namespace apc.Client.Pages.Clientes;

public sealed class ClientesGridDataSource : GridCustomDataSource
{
    private readonly ClientesClient _clientesClient;
    private readonly Action<int>? _onTotalCountChanged;

    public ClientesGridDataSource(ClientesClient clientesClient, Action<int>? onTotalCountChanged = null)
    {
        _clientesClient = clientesClient;
        _onTotalCountChanged = onTotalCountChanged;
    }

    public string? SearchText { get; set; }
    public bool SoloActivos { get; set; } = true;

    public int TotalCount { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _clientesClient.SearchPagedAsync(
                SearchText, SoloActivos,
                skip: 0, take: 1,
                sortField: null, sortDesc: false,
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
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _clientesClient.SearchPagedAsync(
                SearchText, SoloActivos,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetTotal(result.TotalCount);

            return new List<ClienteListItemDto>(result.Items);
        }
        catch
        {
            return new List<ClienteListItemDto>();
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
