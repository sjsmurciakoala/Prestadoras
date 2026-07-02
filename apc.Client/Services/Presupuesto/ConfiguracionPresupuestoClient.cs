using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Presupuesto;

namespace apc.Client.Services.Presupuesto;

public sealed class ConfiguracionPresupuestoClient
{
    private readonly HttpClient _http;

    public ConfiguracionPresupuestoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ConfiguracionPresupuestoListItemDto>> GetAsync(
        ConfiguracionPresupuestoFilterDto? filtro = null,
        CancellationToken ct = default)
    {
        var parameters = BuildFilterParameters(filtro);
        var url = parameters.Count > 0
            ? $"api/presupuesto/configuraciones?{string.Join("&", parameters)}"
            : "api/presupuesto/configuraciones";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ConfiguracionPresupuestoListItemDto>>(ct)
            ?? new List<ConfiguracionPresupuestoListItemDto>();
    }

    public async Task<PagedResult<ConfiguracionPresupuestoListItemDto>?> GetPagedAsync(
        ConfiguracionPresupuestoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var parameters = BuildFilterParameters(filtro);
        parameters.Insert(0, $"take={Math.Max(1, take)}");
        parameters.Insert(0, $"skip={Math.Max(0, skip)}");

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/presupuesto/configuraciones/paged?{string.Join("&", parameters)}";
        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<ConfiguracionPresupuestoListItemDto>>(ct);
    }

    public async Task<List<ConfiguracionPresupuestoDetalleListItemDto>> GetDetailsByPresupuestoAsync(
        string idPresupuesto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return new List<ConfiguracionPresupuestoDetalleListItemDto>();
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var response = await _http.GetAsync($"api/presupuesto/configuraciones/{id}/detalles", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ConfiguracionPresupuestoDetalleListItemDto>>(ct)
            ?? new List<ConfiguracionPresupuestoDetalleListItemDto>();
    }

    public async Task<ConfiguracionPresupuestoDetalleListItemDto?> GetDetailByIdAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto) || string.IsNullOrWhiteSpace(cuentaContable))
        {
            return null;
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.GetAsync($"api/presupuesto/configuraciones/{id}/detalles/{cuenta}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoDetalleListItemDto>(ct);
    }

    public async Task<List<CuentaContableLookupDto>> GetCuentasDestinoTrasladoAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto) || string.IsNullOrWhiteSpace(cuentaContable))
        {
            return new List<CuentaContableLookupDto>();
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.GetAsync(
            $"api/presupuesto/configuraciones/{id}/detalles/{cuenta}/cuentas-destino-traslado",
            ct);

        return await response.ReadFromJsonAsyncWithAuthCheck<List<CuentaContableLookupDto>>(ct)
            ?? new List<CuentaContableLookupDto>();
    }

    public async Task<ConfiguracionPresupuestoDetalleListItemDto> AddDetailAsync(
        string idPresupuesto,
        ConfiguracionPresupuestoDetalleEditDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var response = await _http.PostAsJsonAsync($"api/presupuesto/configuraciones/{id}/detalles", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoDetalleListItemDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El detalle de presupuesto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ConfiguracionPresupuestoDetalleListItemDto> UpdateDetailAsync(
        string idPresupuesto,
        string cuentaContable,
        ConfiguracionPresupuestoDetalleUpdateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.PutAsJsonAsync(
            $"api/presupuesto/configuraciones/{id}/detalles/{cuenta}",
            dto,
            ct);

        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoDetalleListItemDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El detalle de presupuesto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<List<PresupuestoActividadSolicitudListItemDto>> GetSolicitudesByDetalleAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto) || string.IsNullOrWhiteSpace(cuentaContable))
        {
            return new List<PresupuestoActividadSolicitudListItemDto>();
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.GetAsync(
            $"api/presupuesto/configuraciones/{id}/detalles/{cuenta}/solicitudes",
            ct);

        return await response.ReadFromJsonAsyncWithAuthCheck<List<PresupuestoActividadSolicitudListItemDto>>(ct)
            ?? new List<PresupuestoActividadSolicitudListItemDto>();
    }

    public async Task<PresupuestoActividadSolicitudListItemDto> CreateSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        PresupuestoActividadSolicitudCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/configuraciones/{id}/detalles/{cuenta}/solicitudes",
            dto,
            ct);

        var result = await response.ReadFromJsonAsyncWithAuthCheck<PresupuestoActividadSolicitudListItemDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La solicitud de actividad devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<PresupuestoActividadSolicitudListItemDto> ApproveSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        string? comentario = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var payload = new PresupuestoActividadSolicitudDecisionDto { Comentario = comentario };
        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/configuraciones/{id}/detalles/{cuenta}/solicitudes/{solicitudId}/aprobar",
            payload,
            ct);

        var result = await response.ReadFromJsonAsyncWithAuthCheck<PresupuestoActividadSolicitudListItemDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La aprobacion devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<PresupuestoActividadSolicitudListItemDto> RejectSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        string? comentario = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var payload = new PresupuestoActividadSolicitudDecisionDto { Comentario = comentario };
        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/configuraciones/{id}/detalles/{cuenta}/solicitudes/{solicitudId}/rechazar",
            payload,
            ct);

        var result = await response.ReadFromJsonAsyncWithAuthCheck<PresupuestoActividadSolicitudListItemDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El rechazo devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ConfiguracionPresupuestoEditDto?> GetByIdAsync(
        string idPresupuesto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return null;
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var response = await _http.GetAsync($"api/presupuesto/configuraciones/{id}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoEditDto>(ct);
    }

    public async Task<ConfiguracionPresupuestoEditDto?> GetByIdAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return null;
        }

        return await GetByIdAsync(idPresupuesto, ct);
    }

    public async Task<string> GetNextIdAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/presupuesto/configuraciones/next-id", ct);
        var payload = await response.ReadFromJsonAsyncWithAuthCheck<JsonElement>(ct);
        var result = ParseNextId(payload);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException("No se pudo generar el ID de presupuesto.");
        }

        return result.Trim();
    }

    public async Task<string> GetNextIdAsync(string cuentaContable, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var code = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.GetAsync($"api/presupuesto/configuraciones/next-id?cuentaContable={code}", ct);
        var payload = await response.ReadFromJsonAsyncWithAuthCheck<JsonElement>(ct);
        var result = ParseNextId(payload);
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException("No se pudo generar el ID de presupuesto.");
        }

        return result.Trim();
    }

    public async Task<DateOnly> GetServerDateAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/presupuesto/configuraciones/server-date", ct);
        var payload = await response.ReadFromJsonAsyncWithAuthCheck<JsonElement>(ct);

        if (payload.ValueKind == JsonValueKind.Object &&
            (payload.TryGetProperty("serverDate", out var serverDateElement) ||
             payload.TryGetProperty("ServerDate", out serverDateElement)) &&
            TryParseDateOnly(serverDateElement, out var serverDate))
        {
            return serverDate;
        }

        if (TryParseDateOnly(payload, out serverDate))
        {
            return serverDate;
        }

        throw new InvalidOperationException("No se pudo obtener la fecha actual del servidor.");
    }

    public async Task<ConfiguracionPresupuestoEditDto> CreateAsync(
        ConfiguracionPresupuestoEditDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/presupuesto/configuraciones", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La configuracion de presupuesto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ConfiguracionPresupuestoEditDto> UpdateAsync(
        string idPresupuesto,
        ConfiguracionPresupuestoEditDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var response = await _http.PutAsJsonAsync($"api/presupuesto/configuraciones/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La configuracion de presupuesto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<ConfiguracionPresupuestoEditDto> UpdateAsync(
        string idPresupuesto,
        string cuentaContable,
        ConfiguracionPresupuestoEditDto dto,
        CancellationToken ct = default)
    {
        return await UpdateAsync(idPresupuesto, dto, ct);
    }

    public async Task<ConfiguracionPresupuestoEditDto> ApprovePresupuestoAsync(
        string idPresupuesto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var response = await _http.PostAsync($"api/presupuesto/configuraciones/{id}/aprobar", content: null, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ConfiguracionPresupuestoEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("La aprobacion del presupuesto devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeleteAsync(string idPresupuesto, string cuentaContable, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = Uri.EscapeDataString(idPresupuesto.Trim());
        var cuenta = Uri.EscapeDataString(cuentaContable.Trim());
        var response = await _http.DeleteAsync($"api/presupuesto/configuraciones/{id}/{cuenta}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<CuentaContableLookupDto[]> ObtenerCuentasContablesAsync(CancellationToken ct = default)
    {
        var cuentas = await _http.GetFromJsonAsync<PlanCuentaDto[]>("api/contabilidad/catalogos/plan-cuentas", ct)
            ?? Array.Empty<PlanCuentaDto>();

        return cuentas
            .Where(c => c.AllowsBudget)
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.AccountId,
                Code = c.Code ?? string.Empty,
                Description = c.Name ?? string.Empty
            })
            .ToArray();
    }

    private static List<string> BuildFilterParameters(ConfiguracionPresupuestoFilterDto? filtro)
    {
        var filter = filtro ?? new ConfiguracionPresupuestoFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        return parameters;
    }

    private static string ParseNextId(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.String)
        {
            return payload.GetString() ?? string.Empty;
        }

        if (payload.ValueKind == JsonValueKind.Number)
        {
            return payload.GetRawText();
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            if (payload.TryGetProperty("nextId", out var nextId) ||
                payload.TryGetProperty("NextId", out nextId))
            {
                if (nextId.ValueKind == JsonValueKind.String)
                {
                    return nextId.GetString() ?? string.Empty;
                }

                if (nextId.ValueKind == JsonValueKind.Number)
                {
                    return nextId.GetRawText();
                }
            }
        }

        return string.Empty;
    }

    private static bool TryParseDateOnly(JsonElement element, out DateOnly result)
    {
        result = default;

        if (element.ValueKind == JsonValueKind.String)
        {
            var raw = element.GetString();
            return DateOnly.TryParseExact(
                raw,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }

        return false;
    }
}
