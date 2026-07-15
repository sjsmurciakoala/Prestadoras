using SIAD.Core.DTOs.Facturacion;

namespace apc.Client.Services.Facturacion;

/// <summary>
/// Cliente HTTP del calendario de facturación (Fase A apertura-ciclo-único,
/// 2026-07-14): fechas de lectura/facturación/vencimiento por año/mes/ciclo.
/// </summary>
public sealed class CalendarioFacturacionClient
{
    private readonly HttpClient http;

    public CalendarioFacturacionClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<IReadOnlyList<int>> ListarAniosAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<int>>(
            $"api/ventas/calendario-facturacion/{companyId}/anios", ct);
        return resultado ?? Array.Empty<int>();
    }

    public async Task<CalendarioAnioDto> ObtenerAnioAsync(long companyId, int anio, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<CalendarioAnioDto>(
            $"api/ventas/calendario-facturacion/{companyId}/{anio}", ct);
        return resultado ?? new CalendarioAnioDto { Anio = anio };
    }

    public async Task<CalendarioAnioDto> GuardarAnioAsync(long companyId, int anio,
        List<CalendarioCicloDto> filas, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(filas);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/calendario-facturacion/{companyId}/{anio}", filas, ct);
        // La extensión auth-aware cubre 401/redirección, errores HTTP y HTML inesperado.
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<CalendarioAnioDto>(ct);
        return resultado ?? new CalendarioAnioDto { Anio = anio };
    }

    public async Task<CalendarioAnioDto> CopiarAnioAsync(long companyId, int anioOrigen, int anioDestino,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/calendario-facturacion/{companyId}/copiar",
            new CopiarCalendarioAnioRequest { AnioOrigen = anioOrigen, AnioDestino = anioDestino }, ct);
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<CalendarioAnioDto>(ct);
        return resultado ?? new CalendarioAnioDto { Anio = anioDestino };
    }
}
