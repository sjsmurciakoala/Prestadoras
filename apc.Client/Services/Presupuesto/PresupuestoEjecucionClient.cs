using apc.Client.Services;
using SIAD.Core.DTOs.Presupuesto;

namespace apc.Client.Services.Presupuesto;

/// <summary>Cliente HTTP del control presupuestario (api/presupuesto/ejecucion).</summary>
public sealed class PresupuestoEjecucionClient
{
    private const string BaseUrl = "api/presupuesto/ejecucion";

    private readonly HttpClient _http;

    public PresupuestoEjecucionClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Ejecución por partida.</summary>
    public async Task<List<PresupuestoEjecucionItemDto>> ListarEjecucionAsync(
        PresupuestoEjecucionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new PresupuestoEjecucionFilterDto();
        var parametros = new List<string>();

        if (!string.IsNullOrWhiteSpace(f.IdPresupuesto))
        {
            parametros.Add($"idPresupuesto={Uri.EscapeDataString(f.IdPresupuesto)}");
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            parametros.Add($"search={Uri.EscapeDataString(f.Search)}");
        }
        if (f.SoloPresupuestables)
        {
            parametros.Add("soloPresupuestables=true");
        }
        if (f.SoloConMovimiento)
        {
            parametros.Add("soloConMovimiento=true");
        }

        var url = parametros.Count > 0 ? $"{BaseUrl}?{string.Join("&", parametros)}" : BaseUrl;
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<PresupuestoEjecucionItemDto>>(ct)
            ?? new List<PresupuestoEjecucionItemDto>();
    }

    // ── URLs de exportación ──────────────────────────────────────────────────
    // Son estáticas y devuelven la URL, no el archivo: el PDF lo abre PdfPreviewPopup en un iframe
    // y el Excel se descarga con JS.open. Es el patrón del resto del portal.

    /// <summary>URL del PDF de ejecución presupuestaria (inline), con los filtros de la pantalla.</summary>
    public static string GetEjecucionPdfUrl(PresupuestoEjecucionFilterDto? filtro)
        => $"/{BaseUrl}/pdf{QueryEjecucion(filtro)}";

    /// <summary>URL del Excel de ejecución presupuestaria (descarga).</summary>
    public static string GetEjecucionExcelUrl(PresupuestoEjecucionFilterDto? filtro)
        => $"/{BaseUrl}/excel{QueryEjecucion(filtro)}";

    /// <summary>URL del PDF de compromisos pendientes (inline).</summary>
    public static string GetCompromisosPdfUrl(PresupuestoCompromisoFilterDto? filtro)
        => $"/{BaseUrl}/compromisos/pdf{QueryCompromisos(filtro)}";

    /// <summary>URL del Excel de compromisos pendientes (descarga).</summary>
    public static string GetCompromisosExcelUrl(PresupuestoCompromisoFilterDto? filtro)
        => $"/{BaseUrl}/compromisos/excel{QueryCompromisos(filtro)}";

    private static string QueryEjecucion(PresupuestoEjecucionFilterDto? f)
    {
        if (f is null) return string.Empty;
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.IdPresupuesto)) p.Add($"idPresupuesto={Uri.EscapeDataString(f.IdPresupuesto)}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.SoloPresupuestables) p.Add("soloPresupuestables=true");
        if (f.SoloConMovimiento) p.Add("soloConMovimiento=true");
        return p.Count > 0 ? $"?{string.Join("&", p)}" : string.Empty;
    }

    private static string QueryCompromisos(PresupuestoCompromisoFilterDto? f)
    {
        if (f is null) return string.Empty;
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.IdPresupuesto)) p.Add($"idPresupuesto={Uri.EscapeDataString(f.IdPresupuesto)}");
        if (!string.IsNullOrWhiteSpace(f.ConCuentaCode)) p.Add($"conCuentaCode={Uri.EscapeDataString(f.ConCuentaCode)}");
        if (!string.IsNullOrWhiteSpace(f.CodProveedor)) p.Add($"codProveedor={Uri.EscapeDataString(f.CodProveedor)}");
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.DiasMinimos.HasValue) p.Add($"diasMinimos={f.DiasMinimos.Value}");
        return p.Count > 0 ? $"?{string.Join("&", p)}" : string.Empty;
    }

    /// <summary>Compromisos con saldo pendiente.</summary>
    public async Task<List<PresupuestoCompromisoPendienteDto>> ListarCompromisosAsync(
        PresupuestoCompromisoFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new PresupuestoCompromisoFilterDto();
        var parametros = new List<string>();

        if (!string.IsNullOrWhiteSpace(f.IdPresupuesto))
        {
            parametros.Add($"idPresupuesto={Uri.EscapeDataString(f.IdPresupuesto)}");
        }
        if (!string.IsNullOrWhiteSpace(f.ConCuentaCode))
        {
            parametros.Add($"conCuentaCode={Uri.EscapeDataString(f.ConCuentaCode)}");
        }
        if (!string.IsNullOrWhiteSpace(f.CodProveedor))
        {
            parametros.Add($"codProveedor={Uri.EscapeDataString(f.CodProveedor)}");
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            parametros.Add($"search={Uri.EscapeDataString(f.Search)}");
        }
        if (f.DiasMinimos.HasValue)
        {
            parametros.Add($"diasMinimos={f.DiasMinimos.Value}");
        }

        var url = $"{BaseUrl}/compromisos";
        if (parametros.Count > 0) url += $"?{string.Join("&", parametros)}";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<PresupuestoCompromisoPendienteDto>>(ct)
            ?? new List<PresupuestoCompromisoPendienteDto>();
    }

    /// <summary>Kardex de una partida.</summary>
    public async Task<List<PresupuestoMovimientoDto>> ListarMovimientosAsync(
        string idPresupuesto, string cuenta, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/movimientos?idPresupuesto={Uri.EscapeDataString(idPresupuesto)}"
                + $"&cuenta={Uri.EscapeDataString(cuenta)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<PresupuestoMovimientoDto>>(ct)
            ?? new List<PresupuestoMovimientoDto>();
    }

    public async Task<List<PresupuestoControlConfigDto>> ListarConfiguracionAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/configuracion", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<PresupuestoControlConfigDto>>(ct)
            ?? new List<PresupuestoControlConfigDto>();
    }

    public async Task GuardarConfiguracionAsync(PresupuestoControlConfigDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsyncWithAuthCheck($"{BaseUrl}/configuracion", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct)
                ?? "No se pudo guardar la configuración del control presupuestario.");
        }
    }
}
