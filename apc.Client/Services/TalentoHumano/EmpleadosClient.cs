using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using apc.Client.Services;
using Microsoft.AspNetCore.Components.Forms;
using SIAD.Core.DTOs.TalentoHumano;

namespace apc.Client.Services.TalentoHumano;

public sealed class EmpleadosClient
{
    private const string BaseUrl = "api/talentohumano/empleados";
    private const long TamanoMaximoImportBytes = 10 * 1024 * 1024;

    private readonly HttpClient _http;
    public EmpleadosClient(HttpClient http) => _http = http;

    public async Task<List<EmpleadoListItemDto>> GetAsync(EmpleadoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new EmpleadoFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"{BaseUrl}?{string.Join("&", p)}" : BaseUrl;
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<EmpleadoListItemDto>>(ct) ?? new();
    }

    public async Task<List<EmpleadoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"{BaseUrl}/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<EmpleadoLookupDto>>(ct) ?? new();
    }

    public async Task<EmpleadoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"{BaseUrl}/{id}", ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<EmpleadoEditDto>(ct);
    }

    public async Task<EmpleadoEditDto> CreateAsync(EmpleadoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync(BaseUrl, dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo guardar el empleado.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<EmpleadoEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<EmpleadoEditDto> UpdateAsync(int id, EmpleadoEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"{BaseUrl}/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar el empleado.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<EmpleadoEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"{BaseUrl}/{id}/desactivar", null, ct);
        if (r.StatusCode == HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }

    /// <summary>URL relativa de la plantilla Excel; la página navega a ella con forceLoad para descargarla.</summary>
    public static string PlantillaExcelUrl => $"{BaseUrl}/plantilla-excel";

    public async Task<EmpleadoImportResultDto> ImportarExcelAsync(IBrowserFile archivo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(archivo);

        using var content = new MultipartFormDataContent();
        await using var stream = archivo.OpenReadStream(TamanoMaximoImportBytes, ct);
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType);
        content.Add(streamContent, "archivo", archivo.Name);

        var r = await _http.PostAsync($"{BaseUrl}/importar-excel", content, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo importar el archivo.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<EmpleadoImportResultDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }
}
