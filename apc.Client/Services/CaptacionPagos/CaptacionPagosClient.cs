using System.Net.Http.Json;
using System.Text.Json;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.CaptacionPagos;

public class CaptacionPagosClient
{
    private readonly HttpClient _http;

    public CaptacionPagosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<CajaDto>> GetCajasAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<CajaDto>>("api/captacionpagos/cajas", ct);
        return result ?? new List<CajaDto>();
    }

    public async Task<IReadOnlyList<ArqueoDto>> GetArqueosAsync(CaptacionArqueoFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildArqueoQuery(filtro);
        var url = string.IsNullOrWhiteSpace(query) ? "api/captacionpagos/arqueos" : $"api/captacionpagos/arqueos?{query}";
        var result = await _http.GetFromJsonAsync<List<ArqueoDto>>(url, ct);
        return result ?? new List<ArqueoDto>();
    }

    public async Task<PagedResult<ArqueoDto>> GetArqueosPagedAsync(
        CaptacionArqueoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var query = BuildArqueoPagedQuery(filtro, skip, take, sortField, sortDesc);
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/captacionpagos/arqueos/paged"
            : $"api/captacionpagos/arqueos/paged?{query}";

        var result = await _http.GetFromJsonAsync<PagedResult<ArqueoDto>>(url, ct);
        return result ?? new PagedResult<ArqueoDto>();
    }

    public Task<CaptacionPagoResponseDto?> GetPagoAsync(string numFactura, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<CaptacionPagoResponseDto>($"api/captacionpagos/{Uri.EscapeDataString(numFactura)}", ct);

    public async Task<ResponseModelDto> RegistrarPagoAsync(PagoCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/reverso", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<IReadOnlyList<ReciboMiscelaneoDto>> GetMiscelaneosAsync(string? clienteClave, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(clienteClave)
            ? "api/captacionpagos/miscelaneos"
            : $"api/captacionpagos/miscelaneos?clienteClave={Uri.EscapeDataString(clienteClave)}";

        var result = await _http.GetFromJsonAsync<List<ReciboMiscelaneoDto>>(url, ct);
        return result ?? new List<ReciboMiscelaneoDto>();
    }

    public async Task<PagedResult<ReciboMiscelaneoDto>> GetMiscelaneosPagedAsync(
        string? clienteClave,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var query = BuildMiscelaneosPagedQuery(clienteClave, skip, take, sortField, sortDesc);
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/captacionpagos/miscelaneos/paged"
            : $"api/captacionpagos/miscelaneos/paged?{query}";

        var result = await _http.GetFromJsonAsync<PagedResult<ReciboMiscelaneoDto>>(url, ct);
        return result ?? new PagedResult<ReciboMiscelaneoDto>();
    }

    // ==================== AUTOCOMPLETADO Y BÚSQUEDA ====================

    public async Task<IReadOnlyList<BusquedaFacturaDto>> BuscarFacturasAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<BusquedaFacturaDto>();
        }

        var result = await _http.GetFromJsonAsync<List<BusquedaFacturaDto>>(
            $"api/captacionpagos/search/{Uri.EscapeDataString(term)}", ct);
        return result ?? new List<BusquedaFacturaDto>();
    }

    public async Task<bool> ExisteRegistroPagoAsync(string numFactura, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(numFactura))
        {
            return false;
        }

        var result = await _http.GetFromJsonAsync<Dictionary<string, bool>>(
            $"api/captacionpagos/{Uri.EscapeDataString(numFactura)}/existe", ct);

        return result?.TryGetValue("existe", out var existe) == true && existe;
    }

    // ==================== POSTEO MANUAL ====================

    public async Task<IReadOnlyList<SaldoPosteoManualDto>> GetSaldosPosteoManualAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<SaldoPosteoManualDto>();
        }

        var result = await _http.GetFromJsonAsync<List<SaldoPosteoManualDto>>(
            $"api/captacionpagos/saldos-manual/{Uri.EscapeDataString(clienteClave)}", ct);
        return result ?? new List<SaldoPosteoManualDto>();
    }

    public async Task<ResponseModelDto> RegistrarPagoManualAsync(PagoManualCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/posteo-manual", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<ResponseModelDto> ReversarPagoManualAsync(ReversoManualRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/posteo-manual/reverso", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    // ==================== POSTEO MISCELÁNEOS ====================

    public async Task<IReadOnlyList<ReciboMiscelaneoDetalleDto>> GetDetalleReciboMiscelaneoAsync(long recibo, CancellationToken ct = default)
    {
        if (recibo <= 0)
        {
            return Array.Empty<ReciboMiscelaneoDetalleDto>();
        }

        var result = await _http.GetFromJsonAsync<List<ReciboMiscelaneoDetalleDto>>(
            $"api/captacionpagos/miscelaneos/{recibo}/detalle", ct);
        return result ?? new List<ReciboMiscelaneoDetalleDto>();
    }

    public async Task<ResponseModelDto> RegistrarPagoMiscelaneoAsync(PagoMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/miscelaneos/registrar", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<ResponseModelDto> ReversarPagoMiscelaneoAsync(ReversoMiscelaneoRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/captacionpagos/miscelaneos/reverso", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    // ==================== COMBOS Y AUXILIARES ====================

    public async Task<IReadOnlyList<ClienteComboDto>> GetClientesAsync(string? query = null, CancellationToken ct = default, int? take = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
            parts.Add($"query={Uri.EscapeDataString(query)}");
        if (take is not null)
            parts.Add($"take={take.Value}");

        var url = "api/captacionpagos/clientes";
        if (parts.Count > 0)
            url += "?" + string.Join("&", parts);

        var result = await _http.GetFromJsonAsync<List<ClienteComboDto>>(url, ct);
        return result ?? new List<ClienteComboDto>();
    }

    public async Task<IReadOnlyList<BancoDto>> GetBancosAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<BancoDto>>("api/captacionpagos/bancos", ct);
        return result ?? new List<BancoDto>();
    }

    public async Task<PeriodoActualDto?> GetPeriodoActualAsync(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<PeriodoActualDto>("api/captacionpagos/periodo-actual", ct);
    }

    private static async Task<ResponseModelDto> ReadResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ResponseModelDto? payload = null;
        string? rawContent = null;
        string? fallbackMessage = null;

        if (response.Content is not null)
        {
            rawContent = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(rawContent))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<ResponseModelDto>(
                        rawContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    // Ignored: intentar extraer mensaje de ProblemDetails u otro JSON.
                }

                if (payload is null)
                {
                    try
                    {
                        using var document = JsonDocument.Parse(rawContent);
                        var root = document.RootElement;
                        var title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                        var detail = root.TryGetProperty("detail", out var detailProp) ? detailProp.GetString() : null;
                        fallbackMessage = string.Join(" ", new[] { title, detail }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    }
                    catch
                    {
                        // Ignored: usar contenido crudo como fallback.
                    }
                }
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
            if (!string.IsNullOrWhiteSpace(fallbackMessage))
            {
                payload.Message = fallbackMessage!;
            }
            else if (!string.IsNullOrWhiteSpace(rawContent))
            {
                payload.Message = rawContent.Trim();
            }
            else
            {
                payload.Message = "La operación no pudo completarse.";
            }
        }
        // Log error to console for debugging
        if (!payload.Success)
        {
            Console.WriteLine($"[CaptacionPagosClient ERROR] Status: {response.StatusCode}");
            Console.WriteLine($"[CaptacionPagosClient ERROR] Success: {payload.Success}");
            Console.WriteLine($"[CaptacionPagosClient ERROR] Message: {payload.Message}");
            if (!string.IsNullOrWhiteSpace(rawContent))
            {
                Console.WriteLine($"[CaptacionPagosClient ERROR] Raw Response: {rawContent}");
            }
        }
        return payload;
    }

    private static string BuildArqueoQuery(CaptacionArqueoFilterDto? filtro)
    {
        if (filtro is null)
        {
            return string.Empty;
        }

        var parametros = new List<string>();

        if (filtro.CajaId.HasValue && filtro.CajaId > 0)
        {
            parametros.Add($"CajaId={filtro.CajaId}");
        }

        if (filtro.FechaInicio.HasValue)
        {
            parametros.Add($"FechaInicio={Uri.EscapeDataString(filtro.FechaInicio.Value.ToString("yyyy-MM-dd"))}");
        }

        if (filtro.FechaFin.HasValue)
        {
            parametros.Add($"FechaFin={Uri.EscapeDataString(filtro.FechaFin.Value.ToString("yyyy-MM-dd"))}");
        }

        return string.Join("&", parametros);
    }

    private static string BuildArqueoPagedQuery(
        CaptacionArqueoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc)
    {
        var parametros = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };

        if (filtro?.CajaId is int cajaId && cajaId > 0)
        {
            parametros.Add($"CajaId={cajaId}");
        }

        if (filtro?.FechaInicio.HasValue == true)
        {
            parametros.Add($"FechaInicio={Uri.EscapeDataString(filtro.FechaInicio.Value.ToString("yyyy-MM-dd"))}");
        }

        if (filtro?.FechaFin.HasValue == true)
        {
            parametros.Add($"FechaFin={Uri.EscapeDataString(filtro.FechaFin.Value.ToString("yyyy-MM-dd"))}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parametros.Add($"sortField={Uri.EscapeDataString(sortField)}");
            parametros.Add($"sortDesc={sortDesc.ToString().ToLowerInvariant()}");
        }

        return string.Join("&", parametros);
    }

    private static string BuildMiscelaneosPagedQuery(
        string? clienteClave,
        int skip,
        int take,
        string? sortField,
        bool sortDesc)
    {
        var parametros = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };

        if (!string.IsNullOrWhiteSpace(clienteClave))
        {
            parametros.Add($"clienteClave={Uri.EscapeDataString(clienteClave)}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parametros.Add($"sortField={Uri.EscapeDataString(sortField)}");
            parametros.Add($"sortDesc={sortDesc.ToString().ToLowerInvariant()}");
        }

        return string.Join("&", parametros);
    }
}
