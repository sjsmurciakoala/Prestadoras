using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Almacen;

public sealed class ArticulosClient
{
    private readonly HttpClient _http;

    public ArticulosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ArticuloListItemDto>> GetAsync(ArticuloFilterDto? filtro = null, CancellationToken ct = default)
    {
        var parameters = BuildFilterParams(filtro ?? new ArticuloFilterDto());
        var url = parameters.Count > 0
            ? $"api/almacen/articulos?{string.Join("&", parameters)}"
            : "api/almacen/articulos";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<ArticuloListItemDto>>(ct) ?? new List<ArticuloListItemDto>();
    }

    /// <summary>Página del catálogo para el grid remoto (server-side paging/sort sobre el filtro).</summary>
    public async Task<PagedResult<ArticuloListItemDto>> SearchPagedAsync(
        ArticuloFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : take;

        var parameters = BuildFilterParams(filtro ?? new ArticuloFilterDto());
        parameters.Add($"skip={skip}");
        parameters.Add($"take={take}");

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
            if (sortDesc)
            {
                parameters.Add("sortDesc=true");
            }
        }

        var url = $"api/almacen/articulos/search-paged?{string.Join("&", parameters)}";
        return await _http.GetFromJsonAsyncWithAuthCheck<PagedResult<ArticuloListItemDto>>(url, ct)
               ?? new PagedResult<ArticuloListItemDto>();
    }

    /// <summary>KPIs del catálogo (totales) calculados en el servidor con el mismo filtro del grid.</summary>
    public async Task<ArticulosResumenDto> GetResumenAsync(ArticuloFilterDto? filtro = null, CancellationToken ct = default)
    {
        var parameters = BuildFilterParams(filtro ?? new ArticuloFilterDto());
        var url = parameters.Count > 0
            ? $"api/almacen/articulos/resumen?{string.Join("&", parameters)}"
            : "api/almacen/articulos/resumen";

        return await _http.GetFromJsonAsyncWithAuthCheck<ArticulosResumenDto>(url, ct) ?? new ArticulosResumenDto();
    }

    private static List<string> BuildFilterParams(ArticuloFilterDto filter)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.TipoArticuloId.HasValue)
        {
            parameters.Add($"tipoArticuloId={filter.TipoArticuloId.Value}");
        }

        if (filter.GrupoId.HasValue)
        {
            parameters.Add($"grupoId={filter.GrupoId.Value}");
        }

        if (filter.BodegaId.HasValue)
        {
            parameters.Add($"bodegaId={filter.BodegaId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filter.EstadoStock))
        {
            parameters.Add($"estadoStock={Uri.EscapeDataString(filter.EstadoStock)}");
        }

        if (filter.SinUnidad == true)
        {
            parameters.Add("sinUnidad=true");
        }

        if (filter.IncluirDescontinuados == true)
        {
            parameters.Add("incluirDescontinuados=true");
        }

        if (filter.SoloDescontinuados == true)
        {
            parameters.Add("soloDescontinuados=true");
        }

        if (filter.ConDescuadre == true)
        {
            parameters.Add("conDescuadre=true");
        }

        return parameters;
    }

    public async Task<List<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro = null, CancellationToken ct = default)
    {
        var filter = filtro ?? new AlertaStockFilterDto();
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        }

        if (filter.TipoArticuloId.HasValue)
        {
            parameters.Add($"tipoArticuloId={filter.TipoArticuloId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Severidad))
        {
            parameters.Add($"severidad={Uri.EscapeDataString(filter.Severidad)}");
        }

        if (filter.SoloConMinimo == true)
        {
            parameters.Add("soloConMinimo=true");
        }

        var url = parameters.Count > 0
            ? $"api/almacen/articulos/alertas?{string.Join("&", parameters)}"
            : "api/almacen/articulos/alertas";

        var response = await _http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<List<AlertaStockDto>>(ct) ?? new List<AlertaStockDto>();
    }

    /// <summary>
    /// Cuentas contables del catálogo (plan de cuentas) para el desplegable del artículo:
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

    public async Task<ArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await _http.GetAsync($"api/almacen/articulos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<ArticuloEditDto>(ct);
    }

    public async Task<ArticuloEditDto> CreateAsync(ArticuloEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsync("api/almacen/articulos", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<ArticuloEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    public async Task<ArticuloEditDto> UpdateAsync(int id, ArticuloEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID del artículo debe ser válido.", nameof(id));
        }

        var response = await _http.PutAsJsonAsync($"api/almacen/articulos/{id}", dto, ct);

        // 409 = otro usuario guardó primero (concurrencia optimista); 400 = regla de negocio.
        // Se extrae el mensaje del ProblemDetails: intentar deserializarlo como artículo daría
        // un error sin sentido para el usuario.
        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo guardar el artículo.");
        }

        var result = await response.ReadFromJsonAsyncWithAuthCheck<ArticuloEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
        }

        return result;
    }

    /// <summary>Descontinúa el artículo (soft-delete): se conserva y su kardex sigue consultable.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del artículo debe ser válido.", nameof(id));
        }

        var response = await _http.DeleteAsync($"api/almacen/articulos/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo descontinuar el artículo.");
        }

        return true;
    }

    /// <summary>Reactiva un artículo descontinuado.</summary>
    public async Task<bool> ReactivarAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del artículo debe ser válido.", nameof(id));
        }

        var response = await _http.PostAsync($"api/almacen/articulos/{id}/reactivar", content: null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo reactivar el artículo.");
        }

        return true;
    }

    // ── Ubicaciones del artículo por bodega ──────────────────────────────────

    public async Task<List<ArticuloUbicacionDto>> GetUbicacionesAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        if (articuloId <= 0) return new();
        var url = $"api/almacen/articulos/{articuloId}/ubicaciones";
        if (incluirInactivas) url += "?incluirInactivas=true";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ArticuloUbicacionDto>>(ct) ?? new();
    }

    public async Task<ArticuloUbicacionDto> AddUbicacionAsync(int articuloId, ArticuloUbicacionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var r = await _http.PostAsJsonAsync($"api/almacen/articulos/{articuloId}/ubicaciones", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo agregar la ubicación.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<ArticuloUbicacionDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }

    public async Task<ArticuloUbicacionDto> UpdateUbicacionAsync(int articuloId, int id, ArticuloUbicacionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var r = await _http.PutAsJsonAsync($"api/almacen/articulos/{articuloId}/ubicaciones/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar la ubicación.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<ArticuloUbicacionDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }

    /// <summary>Deshabilita (soft-delete) la ubicación; se conserva para histórico.</summary>
    public async Task<bool> DeshabilitarUbicacionAsync(int articuloId, int id, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"api/almacen/articulos/{articuloId}/ubicaciones/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo deshabilitar la ubicación.");
        }
        return true;
    }

    /// <summary>Reactiva una ubicación previamente deshabilitada.</summary>
    public async Task<bool> ReactivarUbicacionAsync(int articuloId, int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/articulos/{articuloId}/ubicaciones/{id}/reactivar", content: null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo reactivar la ubicación.");
        }
        return true;
    }

    // ── Proveedores del artículo ("UPC") ─────────────────────────────────────

    public async Task<List<ArticuloProveedorDto>> GetProveedoresAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        if (articuloId <= 0) return new();
        var url = $"api/almacen/articulos/{articuloId}/proveedores";
        if (incluirInactivas) url += "?incluirInactivas=true";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<ArticuloProveedorDto>>(ct) ?? new();
    }

    public async Task<ArticuloProveedorDto> AddProveedorAsync(int articuloId, ArticuloProveedorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var r = await _http.PostAsJsonAsync($"api/almacen/articulos/{articuloId}/proveedores", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo agregar el proveedor.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<ArticuloProveedorDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }

    public async Task<ArticuloProveedorDto> UpdateProveedorAsync(int articuloId, int id, ArticuloProveedorDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var r = await _http.PutAsJsonAsync($"api/almacen/articulos/{articuloId}/proveedores/{id}", dto, ct);
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo actualizar el proveedor.");
        }
        return await r.ReadFromJsonAsyncWithAuthCheck<ArticuloProveedorDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }

    /// <summary>Deshabilita (soft-delete) la relación con el proveedor; se conserva para histórico.</summary>
    public async Task<bool> DeshabilitarProveedorAsync(int articuloId, int id, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"api/almacen/articulos/{articuloId}/proveedores/{id}", ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo deshabilitar el proveedor.");
        }
        return true;
    }

    /// <summary>Reactiva una relación con proveedor previamente deshabilitada.</summary>
    public async Task<bool> ReactivarProveedorAsync(int articuloId, int id, CancellationToken ct = default)
    {
        var r = await _http.PostAsync($"api/almacen/articulos/{articuloId}/proveedores/{id}/reactivar", content: null, ct);
        if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        if (!r.IsSuccessStatusCode)
        {
            var mensaje = await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct);
            throw new InvalidOperationException(mensaje ?? "No se pudo reactivar el proveedor.");
        }
        return true;
    }
}
