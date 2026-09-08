using System.Net;
using System.Net.Http.Json;
using SIAD.Core.DTOs.ActivosFijos;

namespace apc.Client.Services.ActivosFijos;

public sealed class CatalogosActivosFijosClient
{
    private const string BaseUrl = "api/activosfijos/catalogos";

    private readonly HttpClient _http;
    public CatalogosActivosFijosClient(HttpClient http) => _http = http;

    // ── Catálogos de sistema ────────────────────────────────────────────────

    public async Task<List<CatalogoAfDto>> GetMetodosDepreciacionAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/metodos-depreciacion", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CatalogoAfDto>>(ct) ?? new();
    }

    public async Task<List<CatalogoAfDto>> GetEstadosAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/estados", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CatalogoAfDto>>(ct) ?? new();
    }

    // ── Tipos de activo ─────────────────────────────────────────────────────

    public async Task<List<TipoActivoListItemDto>> GetTiposAsync(bool? activo = null, string? search = null, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/tipos{Query(activo, search)}", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TipoActivoListItemDto>>(ct) ?? new();
    }

    public async Task<List<TipoActivoLookupDto>> GetTiposLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/tipos/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TipoActivoLookupDto>>(ct) ?? new();
    }

    public async Task<TipoActivoEditDto?> GetTipoByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/tipos/{id}", ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoActivoEditDto>(ct);
    }

    public async Task<TipoActivoEditDto> GuardarTipoAsync(TipoActivoEditDto dto, CancellationToken ct = default)
    {
        var r = dto.Id.HasValue
            ? await _http.PutAsJsonAsync($"{BaseUrl}/tipos/{dto.Id.Value}", dto, ct)
            : await _http.PostAsJsonAsync($"{BaseUrl}/tipos", dto, ct);

        await GarantizarExitoAsync(r, "No se pudo guardar el tipo de activo.", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoActivoEditDto>(ct) ?? dto;
    }

    public async Task DesactivarTipoAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/tipos/{id}/desactivar", null, ct);
        await GarantizarExitoAsync(r, "No se pudo desactivar el tipo de activo.", ct);
    }

    // ── Ubicaciones ─────────────────────────────────────────────────────────

    public async Task<List<UbicacionActivoListItemDto>> GetUbicacionesAsync(bool? activo = null, string? search = null, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/ubicaciones{Query(activo, search)}", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<UbicacionActivoListItemDto>>(ct) ?? new();
    }

    public async Task<List<UbicacionActivoLookupDto>> GetUbicacionesLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/ubicaciones/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<UbicacionActivoLookupDto>>(ct) ?? new();
    }

    public async Task<UbicacionActivoEditDto?> GetUbicacionByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/ubicaciones/{id}", ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<UbicacionActivoEditDto>(ct);
    }

    public async Task<UbicacionActivoEditDto> GuardarUbicacionAsync(UbicacionActivoEditDto dto, CancellationToken ct = default)
    {
        var r = dto.Id.HasValue
            ? await _http.PutAsJsonAsync($"{BaseUrl}/ubicaciones/{dto.Id.Value}", dto, ct)
            : await _http.PostAsJsonAsync($"{BaseUrl}/ubicaciones", dto, ct);

        await GarantizarExitoAsync(r, "No se pudo guardar la ubicación.", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<UbicacionActivoEditDto>(ct) ?? dto;
    }

    public async Task DesactivarUbicacionAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/ubicaciones/{id}/desactivar", null, ct);
        await GarantizarExitoAsync(r, "No se pudo desactivar la ubicación.", ct);
    }

    // ── Utilidades ──────────────────────────────────────────────────────────

    private static string Query(bool? activo, string? search)
    {
        var partes = new List<string>();
        if (activo.HasValue) partes.Add($"activo={(activo.Value ? "true" : "false")}");
        if (!string.IsNullOrWhiteSpace(search)) partes.Add($"search={Uri.EscapeDataString(search)}");
        return partes.Count > 0 ? $"?{string.Join("&", partes)}" : string.Empty;
    }

    private static async Task GarantizarExitoAsync(HttpResponseMessage r, string mensajePorDefecto, CancellationToken ct)
    {
        if (r.IsSuccessStatusCode) return;
        var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
        throw new InvalidOperationException(mensaje ?? mensajePorDefecto);
    }
}
