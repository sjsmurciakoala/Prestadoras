using System.Collections;
using DevExpress.Blazor;
using apc.Client.Services.Presupuesto;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Presupuesto;

namespace apc.Client.Pages.Presupuesto;

public sealed class PresupuestoConfiguracionesGridDataSource : GridCustomDataSource
{
    private readonly ConfiguracionPresupuestoClient _client;
    private readonly Action? _onStateChanged;

    public PresupuestoConfiguracionesGridDataSource(
        ConfiguracionPresupuestoClient client,
        Action? onStateChanged = null)
    {
        _client = client;
        _onStateChanged = onStateChanged;
    }

    public string? SearchText { get; set; }

    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }
    public bool HasLoaded { get; private set; }

    public void Invalidate()
    {
        HasLoaded = false;
    }

    public override async Task<int> GetItemCountAsync(
        GridCustomDataSourceCountOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.GetPagedAsync(
                BuildFilter(),
                skip: 0,
                take: 1,
                sortField: null,
                sortDesc: false,
                ct: cancellationToken);

            return SetState(result, null);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            SetState(null, $"No se pudo cargar configuraciones de presupuesto: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(
        GridCustomDataSourceItemsOptions options,
        CancellationToken cancellationToken)
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
            return result?.Items?.ToList() ?? new List<ConfiguracionPresupuestoListItemDto>();
        }
        catch (OperationCanceledException)
        {
            return new List<ConfiguracionPresupuestoListItemDto>();
        }
        catch
        {
            SetState(null, "No se pudo cargar configuraciones de presupuesto.");
            return new List<ConfiguracionPresupuestoListItemDto>();
        }
    }

    private ConfiguracionPresupuestoFilterDto BuildFilter() => new()
    {
        Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText
    };

    private int SetState(PagedResult<ConfiguracionPresupuestoListItemDto>? result, string? error)
    {
        LastTotalCount = result?.TotalCount ?? 0;
        LastError = error;
        HasLoaded = true;
        _onStateChanged?.Invoke();
        return LastTotalCount;
    }

    private static (string? FieldName, bool Descending) GetSortInfo(
        IReadOnlyList<GridCustomDataSourceSortInfo>? sortInfo)
    {
        if (sortInfo is null || sortInfo.Count == 0)
        {
            return (nameof(ConfiguracionPresupuestoListItemDto.AnioPresupuesto), true);
        }

        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
