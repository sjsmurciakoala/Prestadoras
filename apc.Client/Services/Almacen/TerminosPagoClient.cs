using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class TerminosPagoClient
{
    private readonly HttpClient _http;
    public TerminosPagoClient(HttpClient http) => _http = http;

    public async Task<List<TerminoPagoListItemDto>> GetAsync(ClasificacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ClasificacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"api/almacen/terminos-pago?{string.Join("&", p)}" : "api/almacen/terminos-pago";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TerminoPagoListItemDto>>(ct) ?? new();
    }

    public async Task<List<TerminoPagoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/terminos-pago/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TerminoPagoLookupDto>>(ct) ?? new();
    }

    public async Task<TerminoPagoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/terminos-pago/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<TerminoPagoEditDto>(ct);
    }

    public async Task<TerminoPagoEditDto> CreateAsync(TerminoPagoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/terminos-pago", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo guardar el término de pago.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<TerminoPagoEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<TerminoPagoEditDto> UpdateAsync(int id, TerminoPagoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/terminos-pago/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar el término de pago.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<TerminoPagoEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/terminos-pago/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }
}
