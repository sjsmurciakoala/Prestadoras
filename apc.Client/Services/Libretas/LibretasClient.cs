using System.Net.Http.Json;
using SIAD.Core.DTOs.Libretas;

namespace apc.Client.Services.Libretas;

/// <summary>
/// Catálogo global de libretas (libro del lector, sin ciclo — 2026-07-16).
/// Consumido por la pantalla de mantenimiento y por el combo Libreta del
/// formulario de cliente.
/// </summary>
public class LibretasClient
{
    private readonly HttpClient _http;

    public LibretasClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<LibretaDto>> GetAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/libretas", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<LibretaDto>>(ct) ?? new List<LibretaDto>();
    }

    public async Task<List<LibretaDto>> GetActivasAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/libretas/activas", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<LibretaDto>>(ct) ?? new List<LibretaDto>();
    }

    public async Task<LibretaDto> CreateAsync(LibretaUpsertDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/libretas", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "Error al guardar la libreta.");
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<LibretaDto>(ct)
            ?? throw new InvalidOperationException("La libreta devolvió una respuesta vacía.");
    }

    public async Task<LibretaDto> UpdateAsync(long id, LibretaUpsertDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/libretas/{id}", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "Error al guardar la libreta.");
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<LibretaDto>(ct)
            ?? throw new InvalidOperationException("La libreta devolvió una respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(long id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/libretas/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }
}
