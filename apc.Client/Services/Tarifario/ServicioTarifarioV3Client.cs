using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace apc.Client.Services.Tarifario;

public sealed class ServicioTarifarioV3Client
{
    private readonly HttpClient http;

    public ServicioTarifarioV3Client(HttpClient http) => this.http = http;

    public async Task<ServicioTarifarioV3ListDto[]> ObtenerAsync(
        string? search,
        bool? activo,
        bool? facturableApp,
        long? tipoServicioId,
        CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            qs.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (activo.HasValue)
        {
            qs.Add($"activo={(activo.Value ? "true" : "false")}");
        }

        if (facturableApp.HasValue)
        {
            qs.Add($"facturableApp={(facturableApp.Value ? "true" : "false")}");
        }

        if (tipoServicioId.HasValue)
        {
            qs.Add($"tipoServicioId={tipoServicioId.Value}");
        }

        var url = qs.Count > 0
            ? $"api/tarifario/servicios-v3?{string.Join("&", qs)}"
            : "api/tarifario/servicios-v3";

        return await http.GetFromJsonAsync<ServicioTarifarioV3ListDto[]>(url, ct)
               ?? Array.Empty<ServicioTarifarioV3ListDto>();
    }

    public async Task<ServicioTarifarioV3EditDto?> ObtenerPorIdAsync(long servicioId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ServicioTarifarioV3EditDto>($"api/tarifario/servicios-v3/{servicioId}", ct);

    public async Task<ServicioTarifarioV3CatalogosDto?> ObtenerCatalogosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<ServicioTarifarioV3CatalogosDto>("api/tarifario/servicios-v3/catalogos", ct);

    public async Task<ResponseModelDto?> GuardarAsync(ServicioTarifarioV3EditDto request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/tarifario/servicios-v3", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    public async Task<ResponseModelDto?> DesactivarAsync(long servicioId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"api/tarifario/servicios-v3/{servicioId}/desactivar", new { }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }
}
