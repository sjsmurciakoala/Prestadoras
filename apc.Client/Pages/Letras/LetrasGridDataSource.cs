using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Letras;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Letras;

namespace apc.Client.Pages.Letras;

public sealed class LetrasGridDataSource : GridCustomDataSource
{
    private readonly LetrasClient _client;
    private readonly Action<int>? _onTotalCountChanged;

    public LetrasGridDataSource(LetrasClient client, Action<int>? onTotalCountChanged = null)
    {
        _client = client;
        _onTotalCountChanged = onTotalCountChanged;
    }

    public string? Search { get; set; }
    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }
    public bool HasLoaded { get; private set; }

    public void Invalidate()
    {
        HasLoaded = false;
    }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.GetPagedAsync(BuildFilter(), skip: 0, take: 1, sortField: null, sortDesc: false, ct: cancellationToken);
            return SetState(result, null);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            SetState(null, $"No se pudo cargar el catalogo de letras: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _client.GetPagedAsync(
                BuildFilter(),
                options.StartIndex,
                options.Count,
                sortField,
                sortDesc,
                cancellationToken);

            SetState(result, null);
            return result?.Items?.ToList() ?? new List<LetraListItemDto>();
        }
        catch (OperationCanceledException)
        {
            return new List<LetraListItemDto>();
        }
        catch
        {
            SetState(null, "No se pudo cargar el catalogo de letras.");
            return new List<LetraListItemDto>();
        }
    }

    private LetraFilterDto BuildFilter() => new()
    {
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search
    };

    private int SetState(PagedResult<LetraListItemDto>? result, string? error)
    {
        var total = result?.TotalCount ?? 0;
        if (LastTotalCount != total)
        {
            LastTotalCount = total;
            _onTotalCountChanged?.Invoke(total);
        }
        else
        {
            LastTotalCount = total;
        }
        LastError = error;
        HasLoaded = true;
        return LastTotalCount;
    }

    private static (string? FieldName, bool Descending) GetSortInfo(IReadOnlyList<GridCustomDataSourceSortInfo>? sortInfo)
    {
        if (sortInfo is null || sortInfo.Count == 0)
            return (null, false);

        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}

