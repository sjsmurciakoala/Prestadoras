using System.Net.Http.Json;
using SIAD.Core.DTOs.Catalogos;

namespace apc.Client.Services.Catalogos;

/// <summary>
/// Cliente HTTP para catalogos generales del sistema.
/// </summary>
public sealed class CatalogosClient
{
    private readonly HttpClient http;

    public CatalogosClient(HttpClient http) => this.http = http;

    public async Task<List<AbogadoLookupDto>> ObtenerAbogadosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<AbogadoLookupDto>>("api/catalogos/abogados", ct)
           ?? new List<AbogadoLookupDto>();

    public async Task<List<BarrioLookupDto>> ObtenerBarriosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<BarrioLookupDto>>("api/catalogos/barrios", ct)
           ?? new List<BarrioLookupDto>();

    public async Task<List<ServicioLookupDto>> ObtenerServiciosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ServicioLookupDto>>("api/catalogos/servicios", ct)
           ?? new List<ServicioLookupDto>();

    public async Task<List<TipoUsoLookupDto>> ObtenerTiposUsoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<TipoUsoLookupDto>>("api/catalogos/tipos-uso", ct)
           ?? new List<TipoUsoLookupDto>();

    public async Task<List<int>> ObtenerCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<int>>($"api/catalogos/categorias-por-tipo?tipoUsoCodigo={tipoUsoCodigo}", ct)
           ?? new List<int>();
}
