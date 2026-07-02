using System.Globalization;
using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.Facturacion;

public class CobranzaClient
{
    private readonly HttpClient _http;

    public CobranzaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CobranzaSaldoDetalleDto>> ObtenerSaldosAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<CobranzaSaldoDetalleDto>();
        }

        var url = $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/saldos";
        var result = await _http.GetFromJsonAsync<List<CobranzaSaldoDetalleDto>>(url, ct);
        return result ?? new List<CobranzaSaldoDetalleDto>();
    }

    public async Task<bool> EstaBloqueadoAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return false;
        }

        var url = $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/bloqueo";
        var result = await _http.GetFromJsonAsync<BloqueoResponse>(url, ct);
        return result?.Bloqueado ?? false;
    }

    public async Task<string?> NumeroALetrasAsync(decimal valor, CancellationToken ct = default)
    {
        var url = $"api/cobranza/numero-letras?valor={valor.ToString(CultureInfo.InvariantCulture)}";
        var result = await _http.GetFromJsonAsync<NumeroLetrasResponse>(url, ct);
        return result?.Texto;
    }

    public async Task<CobranzaPlanPreviewDto> CalcularPlanAsync(CobranzaPlanPreviewRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/planes/calcular", dto, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CobranzaPlanPreviewDto>(cancellationToken: ct) ?? new CobranzaPlanPreviewDto();
    }

    public async Task<ResponseModelDto> GuardarPlanAsync(CobranzaPlanGuardarDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/planes", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<IReadOnlyList<CobranzaPlanResumenDto>> ListarPlanesAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<CobranzaPlanResumenDto>>("api/cobranza/planes", ct);
        return result ?? new List<CobranzaPlanResumenDto>();
    }

    public Task<CobranzaPlanDetalleDto?> ObtenerPlanAsync(string correlativo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correlativo))
        {
            return Task.FromResult<CobranzaPlanDetalleDto?>(null);
        }

        return _http.GetFromJsonAsync<CobranzaPlanDetalleDto>($"api/cobranza/planes/{Uri.EscapeDataString(correlativo)}", ct);
    }

    private static async Task<ResponseModelDto> ReadResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ResponseModelDto? payload = null;

        if (response.Content is not null)
        {
            try
            {
                payload = await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
            }
            catch
            {
                // Ignorar contenido no parseable
            }
        }

        payload ??= new ResponseModelDto
        {
            Success = response.IsSuccessStatusCode,
            Message = response.ReasonPhrase ?? string.Empty
        };

        payload.Success = payload.Success && response.IsSuccessStatusCode;

        if (!payload.Success && string.IsNullOrWhiteSpace(payload.Message))
        {
            payload.Message = "La operación no pudo completarse.";
        }

        return payload;
    }

    public Task<IReadOnlyList<ObservacionCobranzaCatalogoDto>?> GetCatalogoObservacionesAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<ObservacionCobranzaCatalogoDto>>("api/cobranza/acciones/observaciones", ct);

    public Task<IReadOnlyList<AbogadoCobranzaLookupDto>?> GetAbogadosCobranzaAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<AbogadoCobranzaLookupDto>>("api/cobranza/acciones/abogados", ct);

    public Task<IReadOnlyList<AccionCobranzaDto>?> GetAccionesAsync(string clienteClave, CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<AccionCobranzaDto>>(
               $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/acciones", ct);

    public Task<IReadOnlyList<AccionCobranzaCatalogoDto>?> GetCatalogoAccionesAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<AccionCobranzaCatalogoDto>>("api/cobranza/acciones/catalogo", ct);

    public async Task<IReadOnlyList<AccionCobranzaHistorialDto>> GetHistorialAccionesAsync(
        DateTime desde, DateTime hasta, int? codAccion = null, string? clienteClave = null,
        string? ejecutadoPor = null, CancellationToken ct = default)
    {
        var url = $"api/cobranza/acciones/historial?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        if (codAccion is not null)
            url += $"&codAccion={codAccion}";
        if (!string.IsNullOrWhiteSpace(clienteClave))
            url += $"&cliente={Uri.EscapeDataString(clienteClave)}";
        if (!string.IsNullOrWhiteSpace(ejecutadoPor))
            url += $"&ejecutadoPor={Uri.EscapeDataString(ejecutadoPor)}";

        var result = await _http.GetFromJsonAsync<List<AccionCobranzaHistorialDto>>(url, ct);
        return result ?? [];
    }

    public async Task<RegistrarAccionResultadoDto> RegistrarAccionAsync(RegistrarAccionCobranzaRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/acciones", request, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RegistrarAccionResultadoDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Respuesta inválida al registrar la acción.");
    }

    /// <summary>URL del PDF snapshot archivado (para abrir/descargar en el navegador).</summary>
    public static string DocumentoAccionUrl(int documentoId)
        => $"api/cobranza/acciones/documentos/{documentoId}";

    public async Task<int?> RegenerarDocumentoAccionAsync(int accionId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/cobranza/acciones/{accionId}/documento", content: null, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RegenerarDocumentoResponse>(cancellationToken: ct);
        return payload?.DocumentoId;
    }

    private sealed class RegenerarDocumentoResponse
    {
        public int? DocumentoId { get; set; }
    }

    public Task<IReadOnlyList<LlamadaCobranzaDto>?> GetLlamadasAsync(string clienteClave, CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<LlamadaCobranzaDto>>(
               $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/llamadas", ct);

    public async Task RegistrarLlamadaAsync(RegistrarLlamadaRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/llamadas", request, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<NotaCobroDto>?> GetNotasCobroAsync(string clienteClave, CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<NotaCobroDto>>(
               $"api/cobranza/clientes/{Uri.EscapeDataString(clienteClave)}/notas-cobro", ct);

    public async Task<NotaCobroDto?> EmitirNotaCobroAsync(EmitirNotaCobroRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/cobranza/notas-cobro", request, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotaCobroDto>(cancellationToken: ct);
    }

    public async Task AnularNotaCobroAsync(int id, AnularNotaCobroRequest request, CancellationToken ct = default)
    {
        var response = await _http.PatchAsJsonAsync($"api/cobranza/notas-cobro/{id}/anular", request, cancellationToken: ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<BloqueoClienteEstadoDto?> GetEstadoBloqueoAsync(string clienteClave, CancellationToken ct = default)
        => _http.GetFromJsonAsyncWithAuthCheck<BloqueoClienteEstadoDto>(
               $"api/cobranza/bloqueo/{Uri.EscapeDataString(clienteClave)}", ct);

    public async Task<bool> BloquearDesbloquearAsync(BloquearClienteRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsyncWithAuthCheck("api/cobranza/bloqueo", request);
        resp.EnsureSuccessStatusCode();
        return true;
    }

    // ── CRUD Catálogos ────────────────────────────────────────────────────────

    public Task<IReadOnlyList<AccionCobranzaCrudDto>?> ListarAccionesCrudAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<AccionCobranzaCrudDto>>("api/cobranza/catalogos/acciones", ct);

    public async Task GuardarAccionCrudAsync(AccionCobranzaSaveDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/cobranza/catalogos/acciones", dto, ct);
        resp.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<ObservacionCobranzaCrudDto>?> ListarObservacionesCrudAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<ObservacionCobranzaCrudDto>>("api/cobranza/catalogos/observaciones", ct);

    public async Task GuardarObservacionCrudAsync(ObservacionCobranzaSaveDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/cobranza/catalogos/observaciones", dto, ct);
        resp.EnsureSuccessStatusCode();
    }

    private sealed class BloqueoResponse
    {
        public bool Bloqueado { get; set; }
    }

    private sealed class NumeroLetrasResponse
    {
        public decimal Valor { get; set; }
        public string? Texto { get; set; }
    }
}
