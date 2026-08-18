using apc.Client.Services;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>
/// Cliente HTTP de la antigüedad de saldos de proveedores. Solo lectura; usa las extensiones
/// auth-aware (lanzan <see cref="UnauthorizedAccessException"/> ante 401 o redirección a login).
/// </summary>
public sealed class AntiguedadSaldosProveedorClient
{
    private readonly HttpClient _http;

    public AntiguedadSaldosProveedorClient(HttpClient http) => _http = http;

    /// <summary>Matriz de antigüedad: filas por proveedor con saldo + totales por tramo.</summary>
    /// <param name="origen">0 = compras + compromisos, 1 = solo compras, 2 = solo compromisos.</param>
    public async Task<AntiguedadSaldosProveedorDto> ObtenerAsync(
        DateOnly? corte = null,
        bool incluirPorVencer = true,
        int origen = 0,
        int? tipoProveedor = null,
        string? codProveedor = null,
        CancellationToken ct = default)
    {
        var url = "api/proveedores/antiguedad-saldos" + BuildQuery(corte, incluirPorVencer, origen, tipoProveedor, codProveedor);

        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<AntiguedadSaldosProveedorDto>(ct)
               ?? new AntiguedadSaldosProveedorDto { Corte = corte ?? DateOnly.FromDateTime(DateTime.Today) };
    }

    /// <summary>URL (relativa a la app) del PDF; la página la abre en pestaña nueva con JS.</summary>
    public static string GetPdfUrl(DateOnly? corte = null, bool incluirPorVencer = true, int origen = 0, int? tipoProveedor = null, string? codProveedor = null)
        => "api/proveedores/antiguedad-saldos/pdf" + BuildQuery(corte, incluirPorVencer, origen, tipoProveedor, codProveedor);

    /// <summary>URL (relativa a la app) del Excel; la página la abre en pestaña nueva con JS.</summary>
    public static string GetExcelUrl(DateOnly? corte = null, bool incluirPorVencer = true, int origen = 0, int? tipoProveedor = null, string? codProveedor = null)
        => "api/proveedores/antiguedad-saldos/excel" + BuildQuery(corte, incluirPorVencer, origen, tipoProveedor, codProveedor);

    private static string BuildQuery(DateOnly? corte, bool incluirPorVencer, int origen, int? tipoProveedor, string? codProveedor)
        => Query(
            ("corte", Fecha(corte)),
            ("incluirPorVencer", incluirPorVencer ? "true" : "false"),
            ("origen", origen is 1 or 2 ? origen.ToString() : null),
            ("tipoProveedor", tipoProveedor?.ToString()),
            ("proveedor", string.IsNullOrWhiteSpace(codProveedor) ? null : codProveedor.Trim()));

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
