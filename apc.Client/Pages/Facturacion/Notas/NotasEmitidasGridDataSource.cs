using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;
using apc.Client.Services.Facturacion;

namespace apc.Client.Pages.Facturacion.Notas;

public sealed class NotasEmitidasGridDataSource : GridCustomDataSource
{
    private readonly NotasCreditoDebitoClient _client;
    private readonly Action? _onStateChanged;

    public NotasEmitidasGridDataSource(NotasCreditoDebitoClient client, Action? onStateChanged = null)
    {
        _client = client;
        _onStateChanged = onStateChanged;
    }

    public string? SearchText { get; set; }
    public string? TipoNota { get; set; }   // "NC" | "ND" | null
    public short? EstadoId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }

    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }
    public bool HasLoaded { get; private set; }

    public void Invalidate() => HasLoaded = false;

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _client.ListarNotasEmitidasPagedAsync(BuildFilter(), 0, 1, null, true, cancellationToken);
            return SetState(page, null);
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception ex)
        {
            SetState(null, $"No se pudieron cargar las notas: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);
            var page = await _client.ListarNotasEmitidasPagedAsync(
                BuildFilter(), options.StartIndex, options.Count, sortField, sortDesc, cancellationToken);
            SetState(page, null);
            return page?.Items?.ToList() ?? new List<NotaEmitidaListDto>();
        }
        catch (OperationCanceledException) { return new List<NotaEmitidaListDto>(); }
        catch
        {
            SetState(null, "No se pudieron cargar las notas emitidas.");
            return new List<NotaEmitidaListDto>();
        }
    }

    private NotaEmitidaFilterDto BuildFilter() => new()
    {
        Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
        TipoNota = string.IsNullOrWhiteSpace(TipoNota) ? null : TipoNota,
        EstadoId = EstadoId,
        FechaDesde = FechaDesde,
        FechaHasta = FechaHasta
    };

    private int SetState(PagedResult<NotaEmitidaListDto>? page, string? error)
    {
        LastTotalCount = page?.TotalCount ?? 0;
        LastError = error;
        HasLoaded = true;
        _onStateChanged?.Invoke();
        return LastTotalCount;
    }

    private static (string? FieldName, bool Descending) GetSortInfo(IReadOnlyList<GridCustomDataSourceSortInfo>? sortInfo)
    {
        if (sortInfo is null || sortInfo.Count == 0) return (null, true);
        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
