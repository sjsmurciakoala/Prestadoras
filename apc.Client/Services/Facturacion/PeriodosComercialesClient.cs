using SIAD.Core.DTOs.PeriodosComerciales;

namespace apc.Client.Services.Facturacion;

/// <summary>
/// Cliente HTTP de períodos comerciales y avisos de períodos
/// (plan 2026-07-02, Fase 7).
/// </summary>
public sealed class PeriodosComercialesClient
{
    private readonly HttpClient http;

    public PeriodosComercialesClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<IReadOnlyList<PeriodoComercialDto>> ListarAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<PeriodoComercialDto>>(
            $"api/ventas/periodos-comerciales/{companyId}", ct);
        return resultado ?? Array.Empty<PeriodoComercialDto>();
    }

    public async Task<IReadOnlyList<RutaCicloDto>> RutasCicloAsync(long companyId, long periodoCicloId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<RutaCicloDto>>(
            $"api/ventas/periodos-comerciales/{companyId}/ciclos/{periodoCicloId}/rutas", ct);
        return resultado ?? Array.Empty<RutaCicloDto>();
    }

    public async Task<IReadOnlyList<ChecklistCierreItemDto>> ChecklistAsync(long companyId, long periodoComercialId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<ChecklistCierreItemDto>>(
            $"api/ventas/periodos-comerciales/{companyId}/{periodoComercialId}/checklist", ct);
        return resultado ?? Array.Empty<ChecklistCierreItemDto>();
    }

    /// <summary>Apertura integral (Fase B): devuelve el resumen con avisos.</summary>
    public async Task<AperturaCicloResumenDto> AbrirAsync(long companyId, AbrirPeriodoComercialRequest request,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(request);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/periodos-comerciales/{companyId}/abrir", request, ct);
        var resumen = await response.ReadFromJsonAsyncWithAuthCheck<AperturaCicloResumenDto>(ct);
        return resumen ?? new AperturaCicloResumenDto();
    }

    /// <summary>Preview de la apertura (sin escribir): bloqueos + avisos.</summary>
    public async Task<AperturaCicloResumenDto> PreviewAperturaAsync(long companyId, int anio, int mes,
        string? ciclo, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<AperturaCicloResumenDto>(
            $"api/ventas/periodos-comerciales/{companyId}/abrir/preview?anio={anio}&mes={mes}&ciclo={Uri.EscapeDataString(ciclo ?? string.Empty)}", ct);
        return resultado ?? new AperturaCicloResumenDto();
    }

    /// <summary>Próximo ciclo según el calendario de facturación (null si no hay).</summary>
    public async Task<SugerenciaAperturaDto?> SugerenciaAperturaAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        return await http.GetFromJsonAsyncWithAuthCheck<SugerenciaAperturaDto?>(
            $"api/ventas/periodos-comerciales/{companyId}/abrir/sugerencia", ct);
    }

    /// <summary>Deshace una apertura de ciclo (el servidor la rechaza si hay lecturas/facturas).</summary>
    public async Task<DeshacerAperturaResultadoDto> DeshacerAperturaAsync(long companyId, long periodoCicloId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/periodos-comerciales/{companyId}/ciclos/{periodoCicloId}/deshacer", new { }, ct);
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<DeshacerAperturaResultadoDto>(ct);
        return resultado ?? new DeshacerAperturaResultadoDto();
    }

    public async Task CerrarCicloAsync(long companyId, long periodoCicloId, bool forzar,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/periodos-comerciales/{companyId}/ciclos/{periodoCicloId}/cerrar",
            new CerrarCicloRequest(forzar), ct);
        await LanzarSiErrorAsync(response, ct);
    }

    public async Task CerrarMesAsync(long companyId, long periodoComercialId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/periodos-comerciales/{companyId}/{periodoComercialId}/cerrar",
            new { }, ct);
        await LanzarSiErrorAsync(response, ct);
    }

    public async Task<IReadOnlyList<AvisoPeriodoDto>> AvisosAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<AvisoPeriodoDto>>(
            $"api/periodos/avisos/{companyId}", ct);
        return resultado ?? Array.Empty<AvisoPeriodoDto>();
    }

    private static async Task LanzarSiErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
        throw new HttpRequestException(mensaje ?? "Error en la solicitud HTTP.");
    }
}
