using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;

namespace apc.Client.Services.Facturacion;

public class NotasCreditoDebitoClient
{
    private readonly HttpClient _http;

    public NotasCreditoDebitoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<NotaClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/facturacion/notas/clientes"
            : $"api/facturacion/notas/clientes?query={Uri.EscapeDataString(query)}";
        var result = await _http.GetFromJsonAsync<List<NotaClienteLookupDto>>(url, ct);
        return result ?? new List<NotaClienteLookupDto>();
    }

    public async Task<IReadOnlyList<FacturaOrigenLookupDto>> BuscarFacturasClienteAsync(string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return new List<FacturaOrigenLookupDto>();
        }

        var result = await _http.GetFromJsonAsync<List<FacturaOrigenLookupDto>>(
            $"api/facturacion/notas/clientes/{Uri.EscapeDataString(clave)}/facturas", ct);
        return result ?? new List<FacturaOrigenLookupDto>();
    }

    public async Task<IReadOnlyList<MotivoLookupDto>> ListarMotivosAnulacionAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MotivoLookupDto>>("api/facturacion/notas/motivos/anulacion", ct);
        return result ?? new List<MotivoLookupDto>();
    }

    public async Task<IReadOnlyList<MotivoLookupDto>> ListarMotivosAumentoAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MotivoLookupDto>>("api/facturacion/notas/motivos/aumento", ct);
        return result ?? new List<MotivoLookupDto>();
    }

    public async Task<IReadOnlyList<CaiNotaLookupDto>> ListarCaisNotaAsync(short tipoDocumentoFiscalId, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<CaiNotaLookupDto>>(
            $"api/facturacion/notas/cais?tipoDocumentoFiscalId={tipoDocumentoFiscalId}", ct);
        return result ?? new List<CaiNotaLookupDto>();
    }

    public async Task<EmitirNotaResponseDto> EmitirNotaCreditoAsync(EmitirNotaCreditoRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/notas/credito", dto, ct);
        return await ReadNotaResponseAsync(response, ct);
    }

    public async Task<EmitirNotaResponseDto> EmitirNotaDebitoAsync(EmitirNotaDebitoRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/notas/debito", dto, ct);
        return await ReadNotaResponseAsync(response, ct);
    }

    public async Task<PagedResult<NotaEmitidaListDto>> ListarNotasEmitidasPagedAsync(
        NotaEmitidaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"skip={skip}",
            $"take={take}",
            $"sortDesc={sortDesc.ToString().ToLowerInvariant()}"
        };

        if (!string.IsNullOrWhiteSpace(filtro.Search))
            query.Add($"search={Uri.EscapeDataString(filtro.Search)}");
        if (!string.IsNullOrWhiteSpace(filtro.TipoNota))
            query.Add($"tipoNota={Uri.EscapeDataString(filtro.TipoNota)}");
        if (filtro.EstadoId.HasValue)
            query.Add($"estadoId={filtro.EstadoId.Value}");
        if (filtro.FechaDesde.HasValue)
            query.Add($"fechaDesde={Uri.EscapeDataString(filtro.FechaDesde.Value.ToString("o"))}");
        if (filtro.FechaHasta.HasValue)
            query.Add($"fechaHasta={Uri.EscapeDataString(filtro.FechaHasta.Value.ToString("o"))}");
        if (!string.IsNullOrWhiteSpace(sortField))
            query.Add($"sortField={Uri.EscapeDataString(sortField)}");

        var url = $"api/facturacion/notas/emitidas?{string.Join("&", query)}";
        var result = await _http.GetFromJsonAsync<PagedResult<NotaEmitidaListDto>>(url, ct);
        return result ?? new PagedResult<NotaEmitidaListDto>(new List<NotaEmitidaListDto>(), 0);
    }

    // ── Mantenimiento de catálogos de motivos ──

    public async Task<IReadOnlyList<MotivoCrudDto>> ListarMotivosAnulacionCrudAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MotivoCrudDto>>("api/facturacion/notas/motivos/anulacion/crud", ct);
        return result ?? new List<MotivoCrudDto>();
    }

    public async Task<IReadOnlyList<MotivoCrudDto>> ListarMotivosAumentoCrudAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MotivoCrudDto>>("api/facturacion/notas/motivos/aumento/crud", ct);
        return result ?? new List<MotivoCrudDto>();
    }

    public async Task<ResponseModelDto> GuardarMotivoAnulacionAsync(MotivoSaveRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/notas/motivos/anulacion", dto, ct);
        return await ReadResponseModelAsync(response, ct);
    }

    public async Task<ResponseModelDto> GuardarMotivoAumentoAsync(MotivoSaveRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/notas/motivos/aumento", dto, ct);
        return await ReadResponseModelAsync(response, ct);
    }

    private static async Task<ResponseModelDto> ReadResponseModelAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ResponseModelDto? payload = null;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
        }
        catch
        {
            // Contenido no parseable.
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

    private static async Task<EmitirNotaResponseDto> ReadNotaResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        EmitirNotaResponseDto? payload = null;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<EmitirNotaResponseDto>(cancellationToken: ct);
        }
        catch
        {
            // Contenido no parseable.
        }

        payload ??= new EmitirNotaResponseDto
        {
            Success = response.IsSuccessStatusCode,
            Codigo = response.IsSuccessStatusCode ? "OK" : "ERROR",
            Mensaje = response.ReasonPhrase ?? string.Empty
        };

        if (!response.IsSuccessStatusCode)
        {
            payload.Success = false;
            if (string.IsNullOrWhiteSpace(payload.Mensaje))
            {
                payload.Mensaje = "La operación no pudo completarse.";
            }
        }

        return payload;
    }
}
