using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

/// <summary>
/// Cliente HTTP del lote manual de partidas de facturación
/// (plan 2026-07-02, Fase 3).
/// </summary>
public sealed class LoteFacturacionClient
{
    private readonly HttpClient http;

    public LoteFacturacionClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<IReadOnlyList<LotePreviewLineaDto>> PreviewAsync(long companyId, DateOnly desde, DateOnly hasta,
        string modo, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modo);

        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<LotePreviewLineaDto>>(
            $"api/contabilidad/lote-facturacion/{companyId}/preview?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}&modo={Uri.EscapeDataString(modo)}",
            ct);
        return resultado ?? Array.Empty<LotePreviewLineaDto>();
    }

    public async Task<LoteGenerarResultDto> GenerarAsync(long companyId, LoteGenerarRequestDto request,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/contabilidad/lote-facturacion/{companyId}/generar", request, ct);
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<LoteGenerarResultDto>(ct);
        return resultado ?? throw new InvalidOperationException("El servidor devolvió una respuesta vacía.");
    }

    public async Task<IReadOnlyList<LoteFacturacionDto>> HistorialAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<LoteFacturacionDto>>(
            $"api/contabilidad/lote-facturacion/{companyId}/historial", ct);
        return resultado ?? Array.Empty<LoteFacturacionDto>();
    }

    public async Task<IReadOnlyList<PartidaPendienteDto>> PendientesAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<PartidaPendienteDto>>(
            $"api/contabilidad/lote-facturacion/{companyId}/pendientes", ct);
        return resultado ?? Array.Empty<PartidaPendienteDto>();
    }
}
