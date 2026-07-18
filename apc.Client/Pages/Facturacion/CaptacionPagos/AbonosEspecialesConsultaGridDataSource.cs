using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Caja;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Caja;

namespace apc.Client.Pages.Facturacion.CaptacionPagos;

/// <summary>
/// Fuente de datos remota (paginada + ordenada en servidor) para el grid de
/// consulta de abonos especiales. Sigue el patrón de <c>ClientesGridDataSource</c>.
/// </summary>
public sealed class AbonosEspecialesConsultaGridDataSource : GridCustomDataSource
{
    private readonly AbonoClient _abonoClient;
    private readonly Action<int>? _onTotalCountChanged;

    public AbonosEspecialesConsultaGridDataSource(AbonoClient abonoClient, Action<int>? onTotalCountChanged = null)
    {
        _abonoClient = abonoClient;
        _onTotalCountChanged = onTotalCountChanged;
    }

    /// <summary>Código de estado a filtrar: "C" | "P" | "A" | null (todos).</summary>
    public string? Estado { get; set; }
    public string? Search { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }

    public int TotalCount { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _abonoClient.ListarAbonosEspecialesAsync(
                Estado, Search, Desde, Hasta,
                skip: 0, take: 1, sortField: null, sortDesc: false);

            var total = result?.TotalCount ?? 0;
            SetTotal(total);
            return total;
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

            var result = await _abonoClient.ListarAbonosEspecialesAsync(
                Estado, Search, Desde, Hasta,
                skip: options.StartIndex, take: options.Count,
                sortField: sortField, sortDesc: sortDesc);

            SetTotal(result?.TotalCount ?? 0);
            return new List<AbonoEspecialListItemDto>(result?.Items ?? Array.Empty<AbonoEspecialListItemDto>());
        }
        catch
        {
            return new List<AbonoEspecialListItemDto>();
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
