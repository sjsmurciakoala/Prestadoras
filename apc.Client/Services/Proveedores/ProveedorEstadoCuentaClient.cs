using apc.Client.Services;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>
/// Cliente HTTP del estado de cuenta del proveedor. Solo lectura; usa las extensiones
/// auth-aware (lanzan <see cref="UnauthorizedAccessException"/> ante 401 o redirección a login).
/// </summary>
public sealed class ProveedorEstadoCuentaClient
{
    private readonly HttpClient _http;

    public ProveedorEstadoCuentaClient(HttpClient http) => _http = http;

    /// <summary>Identidad + resumen. Devuelve null si el proveedor no existe en la empresa actual.</summary>
    public async Task<ProveedorEstadoCuentaDto?> ObtenerResumenAsync(
        string codigo, DateOnly? corte = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var url = BaseUrl(codigo) + Query(("corte", Fecha(corte)));
        var r = await _http.GetAsync(url, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<ProveedorEstadoCuentaDto>(ct);
    }

    public async Task<IReadOnlyList<ProveedorEstadoCuentaDocumentoDto>> ObtenerDocumentosAsync(
        string codigo, DateOnly? corte = null, bool soloPendientes = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return Array.Empty<ProveedorEstadoCuentaDocumentoDto>();

        var url = BaseUrl(codigo) + "/documentos"
                + Query(("corte", Fecha(corte)), ("soloPendientes", soloPendientes ? "true" : "false"));
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ProveedorEstadoCuentaDocumentoDto>>(ct)
               ?? new List<ProveedorEstadoCuentaDocumentoDto>();
    }

    public async Task<IReadOnlyList<ProveedorEstadoCuentaMovimientoDto>> ObtenerMovimientosAsync(
        string codigo, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return Array.Empty<ProveedorEstadoCuentaMovimientoDto>();

        var url = BaseUrl(codigo) + "/movimientos"
                + Query(("desde", Fecha(desde)), ("hasta", Fecha(hasta)));
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ProveedorEstadoCuentaMovimientoDto>>(ct)
               ?? new List<ProveedorEstadoCuentaMovimientoDto>();
    }

    /// <summary>URL (relativa a la app) del PDF; la página la abre en pestaña nueva con JS.</summary>
    public static string GetPdfUrl(string codigo, DateOnly? corte = null, bool soloPendientes = true)
        => BaseUrl(codigo) + "/pdf"
         + Query(("corte", Fecha(corte)), ("soloPendientes", soloPendientes ? "true" : "false"));

    private static string BaseUrl(string codigo)
        => $"api/proveedores/{Uri.EscapeDataString(codigo)}/estado-cuenta";

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
