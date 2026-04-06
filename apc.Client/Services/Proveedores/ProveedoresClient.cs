using System.Net.Http.Json;
using System.Text.Json;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con proveedores.
/// </summary>
public sealed class ProveedoresClient
{
    private readonly HttpClient http;

    public ProveedoresClient(HttpClient http) => this.http = http;

    public async Task<ProveedorListItemDto[]> BuscarAsync(ProveedorFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new ProveedorFilterDto();

        var query =
            $"api/proveedores/search?codigo={Uri.EscapeDataString(filtro.Codigo ?? string.Empty)}" +
            $"&nombre={Uri.EscapeDataString(filtro.Nombre ?? string.Empty)}" +
            $"&rtn={Uri.EscapeDataString(filtro.Rtn ?? string.Empty)}" +
            $"&soloActivos={filtro.SoloActivos}";

        return await http.GetFromJsonAsync<ProveedorListItemDto[]>(query, ct)
            ?? Array.Empty<ProveedorListItemDto>();
    }

    public async Task<ProveedorDetailDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var safeCodigo = Uri.EscapeDataString(codigo.Trim());
        return await http.GetFromJsonAsync<ProveedorDetailDto?>($"api/proveedores/{safeCodigo}", ct);
    }

    public async Task<ProveedorTipoLookupDto[]> ObtenerTiposAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<ProveedorTipoLookupDto[]>("api/proveedores/tipos", ct)
           ?? Array.Empty<ProveedorTipoLookupDto>();

    public async Task<CuentaContableLookupDto[]> ObtenerCuentasContablesAsync(CancellationToken ct = default)
    {
        var cuentas = await http.GetFromJsonAsync<PlanCuentaDto[]>("api/contabilidad/catalogos/plan-cuentas", ct)
            ?? Array.Empty<PlanCuentaDto>();

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
            .ToArray();
    }

    public async Task<TipoProveedorListItemDto[]> ObtenerTiposCatalogoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<TipoProveedorListItemDto[]>("api/proveedores/tipos/catalogo", ct)
           ?? Array.Empty<TipoProveedorListItemDto>();

    public async Task<TipoProveedorDetailDto?> ObtenerTipoAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await http.GetFromJsonAsync<TipoProveedorDetailDto?>($"api/proveedores/tipos/{id}", ct);
    }

    public async Task CrearAsync(ProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/proveedores", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task ActualizarAsync(string codigo, ProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var safeCodigo = Uri.EscapeDataString(codigo.Trim());
        var response = await http.PutAsJsonAsync($"api/proveedores/{safeCodigo}", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task EliminarAsync(string codigo, CancellationToken ct = default)
    {
        var safeCodigo = Uri.EscapeDataString(codigo.Trim());
        var response = await http.DeleteAsync($"api/proveedores/{safeCodigo}", ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task CrearTipoAsync(TipoProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/proveedores/tipos", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task ActualizarTipoAsync(int id, TipoProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"api/proveedores/tipos/{id}", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task EliminarTipoAsync(int id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/proveedores/tipos/{id}", ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }
    private static async Task EnsureSuccessWithDetailsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.String)
                    {
                        detail = detailProp.GetString();
                    }
                    else if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                    {
                        detail = titleProp.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                detail = raw;
            }
        }

        detail ??= $"Error HTTP {(int)response.StatusCode} ({response.StatusCode}).";
        throw new HttpRequestException(detail, null, response.StatusCode);
    }
}

