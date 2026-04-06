using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using apc.Client.Services.AuxiliarLectura;
using DevExpress.Blazor;
using SIAD.Core.DTOs.AuxiliarLectura;

namespace apc.Client.Pages.Auxiliares;

public sealed class AuxiliarLecturaGridDataSource : GridCustomDataSource
{
    private readonly AuxiliarLecturaClient _client;
    private readonly Action? _onDataChanged;

    public AuxiliarLecturaGridDataSource(AuxiliarLecturaClient client, Action? onDataChanged = null)
    {
        _client = client;
        _onDataChanged = onDataChanged;
    }

    public int? Anio { get; set; }
    public int? Mes { get; set; }
    public string? Ciclo { get; set; }
    public bool? SoloPendientes { get; set; }

    public int LastTotalCount { get; private set; }
    public string? LastError { get; private set; }
    public bool HasLoaded { get; private set; }

    public override async Task<int> GetItemCountAsync(GridCustomDataSourceCountOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client.ObtenerListaPaginadaAsync(
                anio: Anio,
                mes: Mes,
                ciclo: Ciclo,
                soloPendientes: SoloPendientes ?? false,
                skip: 0,
                take: 1,
                sortField: null,
                sortDesc: false,
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
            SetState(null, $"Error al cargar lecturas: {ex.Message}");
            return 0;
        }
    }

    public override async Task<IList> GetItemsAsync(GridCustomDataSourceItemsOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var (sortField, sortDesc) = GetSortInfo(options.SortInfo);

            var result = await _client.ObtenerListaPaginadaAsync(
                anio: Anio,
                mes: Mes,
                ciclo: Ciclo,
                soloPendientes: SoloPendientes ?? false,
                skip: options.StartIndex,
                take: options.Count,
                sortField: sortField,
                sortDesc: sortDesc,
                ct: cancellationToken);

            SetState(result, null);
            return result?.Items?.ToList() ?? new List<AuxiliarLecturaDto>();
        }
        catch (OperationCanceledException)
        {
            return new List<AuxiliarLecturaDto>();
        }
        catch
        {
            SetState(null, "Error al cargar lecturas.");
            return new List<AuxiliarLecturaDto>();
        }
    }

    private void SetState(AuxiliarLecturaPagedResponseDto? response, string? error)
    {
        LastTotalCount = response?.TotalCount ?? 0;
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
