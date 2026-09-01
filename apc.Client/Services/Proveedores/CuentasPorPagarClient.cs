using apc.Client.Services;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>
/// Cliente HTTP de las cuentas por pagar unificadas (facturas de compra + compromisos) y del
/// pago en lote. Usa las extensiones auth-aware, que lanzan
/// <see cref="UnauthorizedAccessException"/> ante 401 o redirección a login.
/// </summary>
public sealed class CuentasPorPagarClient
{
    private const string BaseUrl = "api/proveedores/cuentas-por-pagar";

    private readonly HttpClient _http;

    public CuentasPorPagarClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<CxpDocumentoDto>> ListarAsync(
        CxpUnificadaFilterDto? filtro, CancellationToken ct = default)
    {
        var r = await _http.GetAsync(BaseUrl + Query(filtro), ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CxpDocumentoDto>>(ct)
               ?? new List<CxpDocumentoDto>();
    }

    public async Task<CxpResumenDto> ObtenerResumenAsync(
        CxpUnificadaFilterDto? filtro, CancellationToken ct = default)
    {
        var r = await _http.GetAsync(BaseUrl + "/resumen" + Query(filtro), ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<CxpResumenDto>(ct) ?? new CxpResumenDto();
    }

    /// <summary>
    /// Paga varios documentos de una vez. Ante un rechazo del servidor lanza
    /// <see cref="HttpRequestException"/> con el mensaje de negocio, para mostrarlo tal cual.
    /// </summary>
    public async Task<CxpLoteResultadoDto> PagarLoteAsync(
        CxpLoteUpsertDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsyncWithAuthCheck(BaseUrl + "/lote", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await r.ObtenerMensajeErrorAsync(ct);
            throw new HttpRequestException(mensaje ?? "No se pudo registrar el pago en lote.");
        }

        return await r.ReadFromJsonAsyncWithAuthCheck<CxpLoteResultadoDto>(ct)
               ?? new CxpLoteResultadoDto();
    }

    private static string Query(CxpUnificadaFilterDto? filtro)
    {
        if (filtro is null) return string.Empty;

        var p = new List<string>();
        void Add(string nombre, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                p.Add($"{nombre}={Uri.EscapeDataString(valor)}");
            }
        }

        Add("search", filtro.Search);
        Add("origen", filtro.Origen?.ToString());
        Add("estadoId", filtro.EstadoId?.ToString());
        Add("codProveedor", filtro.CodProveedor);
        if (filtro.SoloVencidos) Add("soloVencidos", "true");
        if (filtro.IncluirPagados) Add("incluirPagados", "true");

        return p.Count == 0 ? string.Empty : "?" + string.Join("&", p);
    }
}
