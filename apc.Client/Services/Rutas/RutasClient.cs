using System.Net.Http.Json;
using SIAD.Core.DTOs.Rutas;

namespace apc.Client.Services.Rutas;

public class RutasClient
{
    private readonly HttpClient _http;

    public RutasClient(HttpClient http)
    {
        _http = http;
    }

    public Task<List<RutaListItemDto>?> GetAsync(RutaFilterDto? filtro = null)
    {
        var filter = filtro ?? new RutaFilterDto();

        var parameters = new List<string>();

        if (filter.CodCiclo.HasValue)
        {
            parameters.Add($"codCiclo={filter.CodCiclo.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filter.CodRuta))
        {
            parameters.Add($"codRuta={Uri.EscapeDataString(filter.CodRuta)}");
        }

        var url = parameters.Count > 0
            ? $"api/rutas?{string.Join("&", parameters)}"
            : "api/rutas";
        return _http.GetFromJsonAsync<List<RutaListItemDto>>(url);
    }

    public Task<RutaDetailDto?> GetAsync(int id) =>
        _http.GetFromJsonAsync<RutaDetailDto>($"api/rutas/{id}");

    public async Task CreateAsync(RutaUpsertDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/rutas", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(int id, RutaUpsertDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/rutas/{id}", dto);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<CicloLookupDto>?> GetCiclosAsync() =>
        _http.GetFromJsonAsync<List<CicloLookupDto>>("api/rutas/ciclos");
}
