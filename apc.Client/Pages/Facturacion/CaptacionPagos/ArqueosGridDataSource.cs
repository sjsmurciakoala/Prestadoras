using System.Collections;
using System.Collections.Generic;
using DevExpress.Blazor;
using apc.Client.Services.CaptacionPagos;
using SIAD.Core.DTOs.CaptacionPagos;

namespace apc.Client.Pages.Facturacion.CaptacionPagos;

public sealed class ArqueosGridDataSource : GridCustomDataSource
{
    private readonly CaptacionPagosClient _client;
    private readonly Action<int>? _onTotalCountChanged;

    public ArqueosGridDataSource(CaptacionPagosClient client, Action<int>? onTotalCountChanged = null)
    {
        _client = client;
        _onTotalCountChanged = onTotalCountChanged;
    }

    public int? CajaId { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    public int TotalCount { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var filtro = BuildFiltro();
            var result = await _client.GetArqueosPagedAsync(
                filtro,
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
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);
            var filtro = BuildFiltro();
            var result = await _client.GetArqueosPagedAsync(
                filtro,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetTotal(result.TotalCount);
            return new List<ArqueoDto>(result.Items);
        }
        catch
        {
            return new List<ArqueoDto>();
        }
    }

    private CaptacionArqueoFilterDto BuildFiltro() =>
        new()
        {
            CajaId = CajaId,
            FechaInicio = FechaInicio,
            FechaFin = FechaFin
        };

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
