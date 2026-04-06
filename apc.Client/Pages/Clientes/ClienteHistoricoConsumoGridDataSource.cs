using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Clientes;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Clientes;

namespace apc.Client.Pages.Clientes;

public sealed class ClienteHistoricoConsumoGridDataSource : GridCustomDataSource
{
    private readonly ClientesClient _clientesClient;
    private readonly Action? _onDataChanged;

    public ClienteHistoricoConsumoGridDataSource(ClientesClient clientesClient, Action? onDataChanged = null)
    {
        _clientesClient = clientesClient;
        _onDataChanged = onDataChanged;
    }

    public int ClienteId { get; set; }
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }

    public decimal TotalConsumo { get; private set; }
    public ClienteHistoricoConsumoHeaderDto? Header { get; private set; }
    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        if (ClienteId <= 0)
        {
            SetState(null, null);
            return 0;
        }

        NormalizeRange();

        try
        {
            // Traemos 1 item para obtener header/totalCount/totalConsumo sin bajar todo
            var result = await _clientesClient.ObtenerHistoricoConsumoPagedAsync(
                ClienteId, Desde, Hasta,
                skip: 0, take: 1,
                sortField: null, sortDesc: false,
                ct: cancellationToken);

            SetState(result, null);
            return result?.TotalCount ?? 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            SetState(null, $"Error al cargar el histórico: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        if (ClienteId <= 0)
            return new List<ClienteHistoricoConsumoItemDto>();

        NormalizeRange();

        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _clientesClient.ObtenerHistoricoConsumoPagedAsync(
                ClienteId, Desde, Hasta,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetState(result, null);

            return result is null
                ? new List<ClienteHistoricoConsumoItemDto>()
                : new List<ClienteHistoricoConsumoItemDto>(result.Items);
        }
        catch (OperationCanceledException)
        {
            return new List<ClienteHistoricoConsumoItemDto>();
        }
        catch
        {
            SetState(null, "Error al cargar el histórico.");
            return new List<ClienteHistoricoConsumoItemDto>();
        }
    }

    private void NormalizeRange()
    {
        if (Hasta < Desde)
            (Desde, Hasta) = (Hasta, Desde);

        Desde = Desde.Date;
        Hasta = Hasta.Date;
    }

    private void SetState(ClienteHistoricoConsumoPagedResponseDto? response, string? error)
    {
        LastTotalCount = response?.TotalCount ?? 0;
        TotalConsumo = response?.TotalConsumo ?? 0m;
        Header = response?.Header;
        LastError = error;
        _onDataChanged?.Invoke();
    }

    private static (string? FieldName, bool Descending) GetSortInfo(IReadOnlyList<GridCustomDataSourceSortInfo>? sortInfo)
    {
        if (sortInfo is null || sortInfo.Count == 0)
            return (null, false);

        var sort = sortInfo[0];
        return (sort.FieldName, sort.DescendingSortOrder);
    }
}
