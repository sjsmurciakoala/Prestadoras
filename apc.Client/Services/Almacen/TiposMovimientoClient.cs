using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Almacen;

/// <summary>
/// Cliente del catálogo de tipos de movimiento de almacén
/// (<c>api/almacen/conceptos-movimiento</c>). Es el mantenimiento del vocabulario de negocio
/// ("Merma", "Donación") que el usuario da de alta sin recompilar.
/// </summary>
public sealed class TiposMovimientoClient
{
    private readonly HttpClient _http;
    public TiposMovimientoClient(HttpClient http) => _http = http;

    /// <param name="soloActivos">true = sólo los que se pueden usar para capturar hoy.</param>
    public async Task<List<TipoMovimientoAlmacenListItemDto>> GetAsync(bool soloActivos = false, CancellationToken ct = default)
    {
        var url = $"api/almacen/conceptos-movimiento?soloActivos={(soloActivos ? "true" : "false")}";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<TipoMovimientoAlmacenListItemDto>>(ct) ?? new();
    }

    public async Task<TipoMovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var r = await _http.GetAsync($"api/almacen/conceptos-movimiento/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoMovimientoAlmacenDto>(ct);
    }

    public async Task<TipoMovimientoAlmacenDto> CreateAsync(TipoMovimientoAlmacenDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/conceptos-movimiento", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoMovimientoAlmacenDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    public async Task<TipoMovimientoAlmacenDto> UpdateAsync(int id, TipoMovimientoAlmacenDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/almacen/conceptos-movimiento/{id}", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<TipoMovimientoAlmacenDto>(ct) ?? throw new InvalidOperationException("Respuesta vacía.");
    }

    /// <summary>Desactiva el tipo. NO existe borrado: un tipo con histórico debe seguir resolviéndose.</summary>
    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/conceptos-movimiento/{id}/desactivar", null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await r.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return r.IsSuccessStatusCode;
    }

    /// <summary>
    /// Cuentas contables imputables y activas, para el override de cuenta del tipo.
    /// Mismo criterio y misma fuente que <c>TiposArticuloClient.GetCuentasContablesAsync</c>.
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
