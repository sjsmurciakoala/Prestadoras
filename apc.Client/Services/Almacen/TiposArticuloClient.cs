using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Almacen;

public sealed class TiposArticuloClient
{
    private readonly HttpClient _http;
    public TiposArticuloClient(HttpClient http) => _http = http;

    public async Task<List<TipoArticuloListItemDto>> GetAsync(ClasificacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var f = filtro ?? new ClasificacionFilterDto();
        var p = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Search)) p.Add($"search={Uri.EscapeDataString(f.Search)}");
        if (f.Activo.HasValue) p.Add($"activo={(f.Activo.Value ? "true" : "false")}");
        var url = p.Count > 0 ? $"api/almacen/tipos-articulo?{string.Join("&", p)}" : "api/almacen/tipos-articulo";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TipoArticuloListItemDto>>(ct) ?? new();
    }

    public async Task<List<TipoArticuloLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/tipos-articulo/lookup", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TipoArticuloLookupDto>>(ct) ?? new();
    }

    public async Task<TipoArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/tipos-articulo/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoArticuloEditDto>(ct);
    }

    public async Task<TipoArticuloEditDto> CreateAsync(TipoArticuloEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/tipos-articulo", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoArticuloEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<TipoArticuloEditDto> UpdateAsync(int id, TipoArticuloEditDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/tipos-articulo/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoArticuloEditDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/tipos-articulo/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }

    /// <summary>
    /// Cuentas contables del catálogo (plan de cuentas) para los desplegables del tipo:
    /// sólo cuentas de detalle (imputables) y activas, ordenadas por código.
    /// </summary>
    public async Task<List<CuentaContableLookupDto>> GetCuentasContablesAsync(CancellationToken ct = default)
    {
        var cuentas = await _http.GetFromJsonAsyncWithAuthCheck<PlanCuentaDto[]>(
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
}
