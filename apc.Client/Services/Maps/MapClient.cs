using SIAD.Core.DTOs.Maps;

namespace apc.Client.Services.Maps;

public sealed class MapClient
{
    private readonly HttpClient _http;

    public MapClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<MapBootstrapDto?> GetConfigurationAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("api/map/config", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<MapBootstrapDto>(ct);
    }
}
