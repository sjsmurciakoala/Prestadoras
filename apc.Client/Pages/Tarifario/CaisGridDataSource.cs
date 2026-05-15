using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using apc.Client.Services.Tarifario;

namespace apc.Client.Pages.Tarifario;

public sealed class CaisGridDataSource : GridCustomDataSource
{
    private readonly CaiTarifarioClient _client;
    private readonly Action? _onStateChanged;

    public CaisGridDataSource(CaiTarifarioClient client, Action? onStateChanged = null)
    {
        _client = client;
        _onStateChanged = onStateChanged;
    }

    public string? SearchText { get; set; }
    public bool? Activo { get; set; } // null = todos los estados (incluye anulados)
    public short? EstadoId { get; set; }

    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }
    public bool HasLoaded { get; private set; }

    public void Invalidate() => HasLoaded = false;

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.GetCaisPagedAsync(BuildFilter(), 0, 1, null, false, cancellationToken);
            return SetState(page, null);
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception ex)
        {
            SetState(null, $"No se pudo cargar el catálogo de CAI: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);
            var page = await _client.GetCaisPagedAsync(
                BuildFilter(), options.StartIndex, options.Count, sortField, sortDesc, cancellationToken);
            SetState(page, null);
            return page?.Items?.ToList() ?? new List<CaiFacturacionListDto>();
        }
        catch (OperationCanceledException) { return new List<CaiFacturacionListDto>(); }
        catch
        {
            SetState(null, "No se pudo cargar el catálogo de CAI.");
            return new List<CaiFacturacionListDto>();
        }
    }

    private CaiFacturacionFilterDto BuildFilter() => new()
    {
        Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
        Activo = Activo,
        EstadoId = EstadoId
    };

    private int SetState(PagedResult<CaiFacturacionListDto>? page, string? error)
    {
        LastTotalCount = page?.TotalCount ?? 0;
        LastError = error;
        HasLoaded = true;
        _onStateChanged?.Invoke();
        return LastTotalCount;
    }

    private static (string? FieldName, bool Descending) GetSortInfo(IReadOnlyList<GridCustomDataSourceSortInfo>? sortInfo)
    {
        if (sortInfo is null || sortInfo.Count == 0) return (null, false);
        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
