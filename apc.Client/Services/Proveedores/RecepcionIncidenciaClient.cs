using apc.Client.Services;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>Cliente HTTP de la bitácora de incidencias de recepción (F4).</summary>
public sealed class RecepcionIncidenciaClient
{
    private const string BaseUrl = "api/proveedores/incidencias";

    private readonly HttpClient _http;

    public RecepcionIncidenciaClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<RecepcionIncidenciaDto>> ObtenerAsync(
        string? codProveedor = null, DateOnly? desde = null, DateOnly? hasta = null,
        short? tipo = null, string? search = null, CancellationToken ct = default)
    {
        var url = BaseUrl + Query(
            ("codProveedor", codProveedor),
            ("desde", Fecha(desde)),
            ("hasta", Fecha(hasta)),
            ("tipo", tipo?.ToString()),
            ("search", search));

        return await _http.GetFromJsonAsyncWithAuthCheck<List<RecepcionIncidenciaDto>>(url, ct)
               ?? new List<RecepcionIncidenciaDto>();
    }

    public async Task<IReadOnlyList<RecepcionIncidenciaLookupDto>> BuscarRecepcionesAsync(
        string codProveedor, string? search = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codProveedor)) return Array.Empty<RecepcionIncidenciaLookupDto>();

        var url = $"{BaseUrl}/recepciones/{Uri.EscapeDataString(codProveedor)}" + Query(("search", search));
        return await _http.GetFromJsonAsyncWithAuthCheck<List<RecepcionIncidenciaLookupDto>>(url, ct)
               ?? new List<RecepcionIncidenciaLookupDto>();
    }

    public async Task<RecepcionIncidenciaDto> CrearAsync(
        RecepcionIncidenciaUpsertDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl, dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo registrar la incidencia.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<RecepcionIncidenciaDto>(ct))!;
    }

    public async Task<RecepcionIncidenciaDto> ActualizarAsync(
        int id, RecepcionIncidenciaUpsertDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsyncWithAuthCheck($"{BaseUrl}/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo actualizar la incidencia.");
        }

        return (await r.ReadFromJsonAsyncWithAuthCheck<RecepcionIncidenciaDto>(ct))!;
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"{BaseUrl}/{id}", ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo eliminar la incidencia.");
        }
    }

    private static string? Fecha(DateOnly? valor) => valor?.ToString("yyyy-MM-dd");

    private static string Query(params (string Nombre, string? Valor)[] partes)
    {
        var p = new List<string>();
        foreach (var (nombre, valor) in partes)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                p.Add($"{nombre}={Uri.EscapeDataString(valor)}");
            }
        }

        return p.Count == 0 ? string.Empty : "?" + string.Join("&", p);
    }
}
