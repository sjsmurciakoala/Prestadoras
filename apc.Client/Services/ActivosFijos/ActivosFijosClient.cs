using System.Net;
using System.Net.Http.Json;
using SIAD.Core.DTOs.ActivosFijos;

namespace apc.Client.Services.ActivosFijos;

public sealed class ActivosFijosClient
{
    private const string BaseUrl = "api/activosfijos/activos";

    private readonly HttpClient _http;
    public ActivosFijosClient(HttpClient http) => _http = http;

    public async Task<List<ActivoFijoListItemDto>> GetAsync(ActivoFijoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ActivoFijoFilterDto();
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) partes.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.TipoActivoId.HasValue) partes.Add($"tipoActivoId={f.TipoActivoId.Value}");
        if (f.EstadoActivoId.HasValue) partes.Add($"estadoActivoId={f.EstadoActivoId.Value}");
        if (f.UbicacionId.HasValue) partes.Add($"ubicacionId={f.UbicacionId.Value}");
        if (f.EmpleadoId.HasValue) partes.Add($"empleadoId={f.EmpleadoId.Value}");
        if (f.SoloPendientes == true) partes.Add("soloPendientes=true");

        var url = partes.Count > 0 ? $"{BaseUrl}?{string.Join("&", partes)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ActivoFijoListItemDto>>(ct) ?? new();
    }

    public async Task<ActivoFijoResumenDto> GetResumenAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/resumen", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ActivoFijoResumenDto>(ct) ?? new();
    }

    public async Task<ActivoFijoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<ActivoFijoEditDto>(ct);
    }

    public async Task<ActivoFijoEditDto> GuardarAsync(ActivoFijoEditDto dto, CancellationToken ct = default)
    {
        var r = dto.Id.HasValue
            ? await _http.PutAsJsonAsync($"{BaseUrl}/{dto.Id.Value}", dto, ct)
            : await _http.PostAsJsonAsync(BaseUrl, dto, ct);

        await GarantizarExitoAsync(r, "No se pudo guardar el activo.", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ActivoFijoEditDto>(ct) ?? dto;
    }

    public async Task<List<ActivoAsignacionDto>> GetAsignacionesAsync(int activoId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/{activoId}/asignaciones", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ActivoAsignacionDto>>(ct) ?? new();
    }

    public async Task AsignarAsync(int activoId, ActivoAsignacionRequestDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"{BaseUrl}/{activoId}/asignaciones", dto, ct);
        await GarantizarExitoAsync(r, "No se pudo registrar la asignación.", ct);
    }

    public async Task<List<ActivoComponenteDto>> GetComponentesAsync(int activoId, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/{activoId}/componentes", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ActivoComponenteDto>>(ct) ?? new();
    }

    public async Task<ActivoComponenteDto> GuardarComponenteAsync(int activoId, ActivoComponenteDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync($"{BaseUrl}/{activoId}/componentes", dto, ct);
        await GarantizarExitoAsync(r, "No se pudo guardar el componente.", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ActivoComponenteDto>(ct) ?? dto;
    }

    public async Task EliminarComponenteAsync(int componenteId, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"{BaseUrl}/componentes/{componenteId}", ct);
        await GarantizarExitoAsync(r, "No se pudo eliminar el componente.", ct);
    }

    private static async Task GarantizarExitoAsync(HttpResponseMessage r, string mensajePorDefecto, CancellationToken ct)
    {
        if (r.IsSuccessStatusCode) return;
        var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
        throw new InvalidOperationException(mensaje ?? mensajePorDefecto);
    }
}
