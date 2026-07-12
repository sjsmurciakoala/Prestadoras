using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

public sealed class ArticulosClient
{
    private readonly HttpClient _http;

    public ArticulosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ArticuloListItemDto>> GetAsync(ArticuloFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new ArticuloFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Linea))
        {
            parameters.Add($"linea={Uri.EscapeDataString(filter.Linea)}");
        }

        if (filter.SoloBajoMinimo == true)
        {
            parameters.Add("soloBajoMinimo=true");
        }

        var url = parameters.Count > 0
            ? $"api/almacen/articulos?{string.Join("&", parameters)}"
            : "api/almacen/articulos";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ArticuloListItemDto>>(ct) ?? new List<ArticuloListItemDto>();
    }

    public async Task<List<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new AlertaStockFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Linea))
        {
            parameters.Add($"linea={Uri.EscapeDataString(filter.Linea)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Severidad))
        {
            parameters.Add($"severidad={Uri.EscapeDataString(filter.Severidad)}");
        }

        if (filter.SoloConMinimo == true)
        {
            parameters.Add("soloConMinimo=true");
        }

        var url = parameters.Count > 0
            ? $"api/almacen/articulos/alertas?{string.Join("&", parameters)}"
            : "api/almacen/articulos/alertas";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<AlertaStockDto>>(ct) ?? new List<AlertaStockDto>();
    }

    public async Task<List<string>> GetLineasAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/almacen/articulos/lineas", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<string>>(ct) ?? new List<string>();
    }

    public async Task<ArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/almacen/articulos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ArticuloEditDto>(ct);
    }

    public async Task<ArticuloEditDto> CreateAsync(ArticuloEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/almacen/articulos", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ArticuloEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    public async Task<ArticuloEditDto> UpdateAsync(int id, ArticuloEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID del artículo debe ser válido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/almacen/articulos/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ArticuloEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del artículo debe ser válido.", nameof(id));
        }

        var response = await _http.DeleteAsync($"api/almacen/articulos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo eliminar el artículo.");
        }

        return true;
    }

    // ── Ubicaciones del artículo por bodega ──────────────────────────────────

    public async Task<List<ArticuloUbicacionDto>> GetUbicacionesAsync(int articuloId, CancellationToken ct = default)
    {
        if (articuloId <= 0) return new();
        var r = await _http.GetAsync($"api/almacen/articulos/{articuloId}/ubicaciones", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ArticuloUbicacionDto>>(ct) ?? new();
    }

    public async Task<ArticuloUbicacionDto> AddUbicacionAsync(int articuloId, ArticuloUbicacionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var r = await _http.PostAsJsonAsync($"api/almacen/articulos/{articuloId}/ubicaciones", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo agregar la ubicación.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<ArticuloUbicacionDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }

    public async Task<ArticuloUbicacionDto> UpdateUbicacionAsync(int articuloId, int id, ArticuloUbicacionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var r = await _http.PutAsJsonAsync($"api/almacen/articulos/{articuloId}/ubicaciones/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar la ubicación.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<ArticuloUbicacionDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }

    public async Task<bool> DeleteUbicacionAsync(int articuloId, int id, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"api/almacen/articulos/{articuloId}/ubicaciones/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo eliminar la ubicación.");
        }
        return true;
    }
}
