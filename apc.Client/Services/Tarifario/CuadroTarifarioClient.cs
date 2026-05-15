using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace apc.Client.Services.Tarifario;

public sealed class CuadroTarifarioClient
{
    private readonly HttpClient http;

    public CuadroTarifarioClient(HttpClient http) => this.http = http;

    public async Task<CuadroTarifarioListDto[]> ObtenerCuadrosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<CuadroTarifarioListDto[]>("api/cuadros-tarifarios", ct)
           ?? Array.Empty<CuadroTarifarioListDto>();

    public async Task<CuadroTarifarioCatalogosDto?> ObtenerCatalogosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<CuadroTarifarioCatalogosDto>("api/cuadros-tarifarios/catalogos", ct);

    public async Task<ResponseModelDto?> GuardarCuadroAsync(CuadroTarifarioSaveRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/cuadros-tarifarios", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    public async Task<ResponseModelDto?> DesactivarCuadroAsync(long cuadroTarifarioId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/cuadros-tarifarios/{cuadroTarifarioId}/desactivar", new { }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    // ── Reglas ──

    public async Task<ReglaTarifariaListDto[]> ObtenerReglasAsync(long cuadroTarifarioId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ReglaTarifariaListDto[]>(
               $"api/cuadros-tarifarios/{cuadroTarifarioId}/reglas", ct)
           ?? Array.Empty<ReglaTarifariaListDto>();

    public async Task<ResponseModelDto?> GuardarReglaAsync(ReglaTarifariaSaveRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/cuadros-tarifarios/reglas", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    public async Task<ResponseModelDto?> EliminarReglaAsync(long reglaTarifariaId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/cuadros-tarifarios/reglas/{reglaTarifariaId}/eliminar", new { }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }
}
