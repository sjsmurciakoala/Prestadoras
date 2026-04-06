using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.FacturacionMiscelaneos;

namespace apc.Client.Services.Facturacion;

public class FacturacionMiscelaneosClient
{
    private readonly HttpClient _http;

    public FacturacionMiscelaneosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<ClienteLookupDto>> SearchClientesAsync(string? query, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? "api/facturacion/miscelaneos/clientes"
            : $"api/facturacion/miscelaneos/clientes?query={Uri.EscapeDataString(query)}";

        var result = await _http.GetFromJsonAsync<List<ClienteLookupDto>>(url, ct);
        return result ?? new List<ClienteLookupDto>();
    }

    public Task<ClienteMiscelaneoDto?> GetClienteAsync(string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Task.FromResult<ClienteMiscelaneoDto?>(null);
        }

        return _http.GetFromJsonAsync<ClienteMiscelaneoDto>($"api/facturacion/miscelaneos/clientes/{Uri.EscapeDataString(clave)}", ct);
    }

    public async Task<IReadOnlyList<MiscelaneoCatalogoDto>> GetCatalogoAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<MiscelaneoCatalogoDto>>("api/facturacion/miscelaneos/categorias", ct);
        return result ?? new List<MiscelaneoCatalogoDto>();
    }

    public async Task<ResponseModelDto> CrearReciboAsync(FacturaMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/miscelaneos/recibos", dto, cancellationToken: ct);
        return await ReadResponseAsync(response, ct);
    }

    public Task<FacturaMiscelaneoResponseDto?> GetReciboAsync(int numeroRecibo, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<FacturaMiscelaneoResponseDto>($"api/facturacion/miscelaneos/recibos/{numeroRecibo}", ct);

    // ── CRUD catálogo misceláneos ──

    public async Task<MiscelaneoCatalogoEditDto?> GetCatalogoItemAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/facturacion/miscelaneos/catalogo/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        return await response.ReadFromJsonAsyncWithAuthCheck<MiscelaneoCatalogoEditDto>(ct);
    }

    public async Task<MiscelaneoCatalogoEditDto> CrearCatalogoItemAsync(MiscelaneoCatalogoEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/facturacion/miscelaneos/catalogo", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<MiscelaneoCatalogoEditDto>(ct);
        return result ?? throw new InvalidOperationException("Respuesta vacia al crear concepto.");
    }

    public async Task<MiscelaneoCatalogoEditDto> ActualizarCatalogoItemAsync(int id, MiscelaneoCatalogoEditDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/facturacion/miscelaneos/catalogo/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<MiscelaneoCatalogoEditDto>(ct);
        return result ?? throw new InvalidOperationException("Respuesta vacia al actualizar concepto.");
    }

    public async Task<bool> EliminarCatalogoItemAsync(int id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/facturacion/miscelaneos/catalogo/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return true;
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasContablesAsync(CancellationToken ct = default)
    {
        var cuentas = await _http.GetFromJsonAsync<PlanCuentaDto[]>(
            "api/contabilidad/catalogos/plan-cuentas", ct) ?? Array.Empty<PlanCuentaDto>();

        return cuentas
            .Where(c => c.AllowsPosting
                && (string.IsNullOrWhiteSpace(c.Status)
                    || string.Equals(c.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Status, "ACTIVO", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.AccountId,
                Code = c.Code ?? string.Empty,
                Description = c.Name ?? string.Empty
            })
            .ToList();
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
                // Contenido no parseable, continuar con payload por defecto.
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
}
