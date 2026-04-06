using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.Clientes;
using DevExpress.Blazor;
using SIAD.Core.DTOs.Clientes;

namespace apc.Client.Pages.Clientes;

public sealed class ClienteMovimientosGridDataSource : GridCustomDataSource
{
    private readonly ClientesClient _clientesClient;
    private readonly Action? _onDataChanged;

    public ClienteMovimientosGridDataSource(ClientesClient clientesClient, Action? onDataChanged = null)
    {
        _clientesClient = clientesClient;
        _onDataChanged = onDataChanged;
    }

    public int ClienteId { get; set; }
    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }
    public bool HasLoaded { get; private set; }

    public void Reset()
    {
        LastTotalCount = 0;
        LastError = null;
        HasLoaded = false;
    }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        if (ClienteId <= 0)
        {
            SetState(0, null);
            return 0;
        }

        try
        {
            var result = await _clientesClient.ObtenerMovimientosPagedAsync(
                ClienteId,
                skip: 0,
                take: 1,
                sortField: null,
                sortDesc: false,
                ct: cancellationToken);

            SetState(result.TotalCount, null);
            return result.TotalCount;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            SetState(0, $"Error al cargar movimientos: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        if (ClienteId <= 0)
        {
            return new List<ClienteMovimientoDto>();
        }

        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _clientesClient.ObtenerMovimientosPagedAsync(
                ClienteId,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetState(result.TotalCount, null);
            return new List<ClienteMovimientoDto>(result.Items);
        }
        catch (OperationCanceledException)
        {
            return new List<ClienteMovimientoDto>();
        }
        catch
        {
            SetState(0, "Error al cargar movimientos.");
            return new List<ClienteMovimientoDto>();
        }
    }

    private void SetState(int totalCount, string? error)
    {
        LastTotalCount = totalCount;
        LastError = error;
        HasLoaded = true;
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
