using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Retenciones;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Retenciones;

namespace apc.Client.Pages.Proveedores;

/// <summary>
/// Data source remoto (paginación server-side) del registro fiscal de retenciones (F4). Calcado de
/// <c>ClientesGridDataSource</c>. Los filtros se setean desde la página antes de <c>Reload()</c>.
/// </summary>
public sealed class RetencionesConsultaGridDataSource : GridCustomDataSource
{
    private readonly RetencionRegistroClient _client;
    private readonly Action<int>? _onTotalCountChanged;

    public RetencionesConsultaGridDataSource(RetencionRegistroClient client, Action<int>? onTotalCountChanged = null)
    {
        _client = client;
        _onTotalCountChanged = onTotalCountChanged;
    }

    public string? Search { get; set; }
    public string? CodProveedor { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public short? EstadoId { get; set; }
    public int? Folio { get; set; }

    public int TotalCount { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.BuscarAsync(BuildFiltro(0, 1, null, false), cancellationToken);
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
            var result = await _client.BuscarAsync(
                BuildFiltro(options.StartIndex, options.Count, sortField, sortDesc), cancellationToken);

            SetTotal(result.TotalCount);
            return new List<RetencionRegistroListItemDto>(result.Items);
        }
        catch
        {
            return new List<RetencionRegistroListItemDto>();
        }
    }

    private RetencionRegistroFilterDto BuildFiltro(int skip, int take, string? sortField, bool sortDesc) => new()
    {
        Search = Search,
        CodProveedor = CodProveedor,
        Desde = Desde,
        Hasta = Hasta,
        EstadoId = EstadoId,
        Folio = Folio,
        Skip = skip,
        Take = take,
        SortField = sortField,
        SortDescending = sortDesc
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
            return (null, false);

        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
