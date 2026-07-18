using System.Net.Http.Json;
using System.Text.Json;
using apc.Client.Services.Contabilidad;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Proveedores;

namespace apc.Client.Services.Proveedores;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con proveedores.
/// </summary>
public sealed class ProveedoresClient
{
    private readonly HttpClient http;
    private readonly AccountFormatState accountFormat;

    public ProveedoresClient(HttpClient http, AccountFormatState accountFormat)
    {
        this.http = http;
        this.accountFormat = accountFormat;
    }

    public async Task<ProveedorListItemDto[]> BuscarAsync(ProveedorFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new ProveedorFilterDto();

        var query =
            $"api/proveedores/search?codigo={Uri.EscapeDataString(filtro.Codigo ?? string.Empty)}" +
            $"&nombre={Uri.EscapeDataString(filtro.Nombre ?? string.Empty)}" +
            $"&rtn={Uri.EscapeDataString(filtro.Rtn ?? string.Empty)}" +
            $"&soloActivos={filtro.SoloActivos}";

        return await http.GetFromJsonAsyncWithAuthCheck<ProveedorListItemDto[]>(query, ct)
            ?? Array.Empty<ProveedorListItemDto>();
    }

    public async Task<ProveedorDetailDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var safeCodigo = Uri.EscapeDataString(codigo.Trim());
        return await http.GetFromJsonAsyncWithAuthCheck<ProveedorDetailDto?>($"api/proveedores/{safeCodigo}", ct);
    }

    public async Task<ProveedorTipoLookupDto[]> ObtenerTiposAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<ProveedorTipoLookupDto[]>("api/proveedores/tipos", ct)
           ?? Array.Empty<ProveedorTipoLookupDto>();

    public async Task<ProveedorBancoLookupDto[]> ObtenerBancosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<ProveedorBancoLookupDto[]>("api/proveedores/bancos", ct)
           ?? Array.Empty<ProveedorBancoLookupDto>();

    public async Task<ProveedorBancoListItemDto[]> ObtenerBancosCatalogoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<ProveedorBancoListItemDto[]>("api/proveedores/bancos/catalogo", ct)
           ?? Array.Empty<ProveedorBancoListItemDto>();

    public async Task<ProveedorBancoDetailDto?> ObtenerBancoAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await http.GetFromJsonAsyncWithAuthCheck<ProveedorBancoDetailDto?>($"api/proveedores/bancos/{id}", ct);
    }

    public async Task CrearBancoAsync(ProveedorBancoUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsyncWithAuthCheck("api/proveedores/bancos", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task ActualizarBancoAsync(long id, ProveedorBancoUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsyncWithAuthCheck($"api/proveedores/bancos/{id}", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task EliminarBancoAsync(long id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/proveedores/bancos/{id}", ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task<CuentaContableLookupDto[]> ObtenerCuentasContablesAsync(CancellationToken ct = default)
    {
        var cuentas = await http.GetFromJsonAsyncWithAuthCheck<PlanCuentaDto[]>("api/contabilidad/catalogos/plan-cuentas", ct)
            ?? Array.Empty<PlanCuentaDto>();

        await accountFormat.EnsureLoadedAsync(ct);

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
                Description = c.Name ?? string.Empty,
                DisplayText = accountFormat.FormatDisplay(c.Code, c.Name)
            })
            .ToArray();
    }

    public async Task<TipoProveedorListItemDto[]> ObtenerTiposCatalogoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<TipoProveedorListItemDto[]>("api/proveedores/tipos/catalogo", ct)
           ?? Array.Empty<TipoProveedorListItemDto>();

    public async Task<TipoProveedorDetailDto?> ObtenerTipoAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await http.GetFromJsonAsyncWithAuthCheck<TipoProveedorDetailDto?>($"api/proveedores/tipos/{id}", ct);
    }

    public async Task CrearAsync(ProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsyncWithAuthCheck("api/proveedores", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task ActualizarAsync(string codigo, ProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var safeCodigo = Uri.EscapeDataString(codigo.Trim());
        var response = await http.PutAsJsonAsyncWithAuthCheck($"api/proveedores/{safeCodigo}", dto, ct);
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
        var response = await http.PostAsJsonAsyncWithAuthCheck("api/proveedores/tipos", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task ActualizarTipoAsync(int id, TipoProveedorUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsyncWithAuthCheck($"api/proveedores/tipos/{id}", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task EliminarTipoAsync(int id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/proveedores/tipos/{id}", ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    private static async Task EnsureSuccessWithDetailsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.RequestMessage?.RequestUri?.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new UnauthorizedAccessException("Su sesión ha expirado. Por favor, inicie sesión nuevamente.");
        }

        if (response.IsSuccessStatusCode)
        {
            var rawSuccess = await response.Content.ReadAsStringAsync(ct);
            if (IsHtmlErrorResponse(response, rawSuccess))
            {
                throw new HttpRequestException("El servidor devolvio HTML en lugar de JSON. Revise la autenticacion o el endpoint solicitado.");
            }

            return;
        }

        string? detail = null;
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!string.IsNullOrWhiteSpace(raw) && !IsHtmlErrorResponse(response, raw))
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

        detail ??= response.StatusCode == System.Net.HttpStatusCode.InternalServerError
            ? "Ocurrio un error interno en el servidor."
            : $"Error HTTP {(int)response.StatusCode} ({response.StatusCode}).";
        throw new HttpRequestException(detail, null, response.StatusCode);
    }

    private static bool IsHtmlErrorResponse(HttpResponseMessage response, string raw)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = raw.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }
}

