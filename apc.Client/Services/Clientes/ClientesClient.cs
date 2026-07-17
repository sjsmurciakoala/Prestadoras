using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.DTOs.Common;

namespace apc.Client.Services.Clientes;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con clientes.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class ClientesClient
{
    private readonly HttpClient http;

    public ClientesClient(HttpClient http) => this.http = http;

    // ── Código de cliente automático y secuencia sugerida (2026-07-16) ────────

    public async Task<CodigoClienteConfigDto?> ObtenerCodigoConfigAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("api/clientes/codigo-config", ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<CodigoClienteConfigDto>(ct);
    }

    public async Task<CodigoClienteConfigDto?> GuardarCodigoConfigAsync(CodigoClienteConfigDto dto, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("api/clientes/codigo-config", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct) ?? "No se pudo guardar la configuración.");
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<CodigoClienteConfigDto>(ct);
    }

    public async Task<string?> SugerirSecuenciaAsync(int cicloId, string libreta, CancellationToken ct = default)
    {
        var response = await http.GetAsync(
            $"api/clientes/siguiente-secuencia?cicloId={cicloId}&libreta={Uri.EscapeDataString(libreta)}", ct);
        var payload = await response.ReadFromJsonAsyncWithAuthCheck<SecuenciaSugeridaResponse>(ct);
        return payload?.Secuencia;
    }

    private sealed record SecuenciaSugeridaResponse(string? Secuencia);

    public async Task<ClienteCreateResponseDto> CrearAsync(ClienteCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsync("api/clientes", dto, cancellationToken: ct);

        try
        {
            var created = await response.ReadFromJsonAsyncWithAuthCheck<ClienteCreateResponseDto>(ct);
            if (created is null || created.Id <= 0)
            {
                throw new HttpRequestException("El servicio devolvió una respuesta vacía al crear el cliente.");
            }

            return created;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HttpRequestException("No fue posible crear el cliente.", ex);
        }
    }

    public async Task<List<ClienteListItemDto>> SearchAsync(string? texto, bool soloActivos, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return await SearchAsync(new ClienteFilterDto { SoloActivos = soloActivos }, ct);
        }

        var filtroNombre = new ClienteFilterDto { Nombre = texto, SoloActivos = soloActivos };
        var filtroCodigo = new ClienteFilterDto { Codigo = texto, SoloActivos = soloActivos };

        var nombreTask = SearchAsync(filtroNombre, ct);
        var codigoTask = SearchAsync(filtroCodigo, ct);

        await Task.WhenAll(nombreTask, codigoTask);

        return nombreTask.Result
            .Concat(codigoTask.Result)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<List<ClienteListItemDto>> SearchAsync(ClienteFilterDto filtro, CancellationToken ct = default)
    {
        var url = BuildSearchUrl(filtro);
        return await http.GetFromJsonAsync<List<ClienteListItemDto>>(url, ct) ?? new List<ClienteListItemDto>();
    }

    public async Task<PagedResult<ClienteListItemDto>> SearchPagedAsync(string? search, bool soloActivos, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : take;

        var parameters = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (soloActivos)
        {
            parameters.Add("soloActivos=true");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
            if (sortDesc)
            {
                parameters.Add("sortDesc=true");
            }
        }

        var url = $"api/clientes/search-paged?{string.Join("&", parameters)}";
        return await http.GetFromJsonAsync<PagedResult<ClienteListItemDto>>(url, ct)
            ?? new PagedResult<ClienteListItemDto>();
    }

    /// <summary>
    /// Obtiene los datos completos de un cliente por su ID.
    /// </summary>
    public async Task<ClienteDetailDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteDetailDto?>($"api/clientes/{id}", ct);

    public async Task<ClienteFotoMedidorHeaderDto?> ObtenerFotoMedidorHeaderAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteFotoMedidorHeaderDto?>($"api/clientes/{clienteId}/foto-medidor/header", ct);

    public async Task<ClienteFotoMedidorItemDto[]> ObtenerFotoMedidorAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
    {
        var query = $"?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        return await http.GetFromJsonAsync<ClienteFotoMedidorItemDto[]>($"api/clientes/{clienteId}/foto-medidor{query}", ct)
               ?? Array.Empty<ClienteFotoMedidorItemDto>();
    }

    public async Task<byte[]?> ObtenerFotoMedidorImagenAsync(int ide, CancellationToken ct = default)
        => await http.GetByteArrayAsync($"api/clientes/foto-medidor/{ide}/imagen", ct);

    /// <summary>
    /// Obtiene el estado de cuenta (resumen financiero) de un cliente.
    /// </summary>
    public async Task<ClienteEstadoCuentaDto?> ObtenerEstadoCuentaAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteEstadoCuentaDto?>($"api/clientes/{clienteId}/estado-cuenta", ct);

    /// <summary>
    /// Obtiene todos los movimientos (transacciones) de un cliente.
    /// </summary>
    public async Task<ClienteMovimientoDto[]> ObtenerMovimientosAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<ClienteMovimientoDto[]>($"api/clientes/{clienteId}/movimientos", ct)
           ?? Array.Empty<ClienteMovimientoDto>();

    /// <summary>
    /// Obtiene movimientos paginados de un cliente (carga remota).
    /// </summary>
    public async Task<PagedResult<ClienteMovimientoDto>> ObtenerMovimientosPagedAsync(
        int clienteId,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var url = $"api/clientes/{clienteId}/movimientos/paged?skip={skip}&take={take}&sortField={sortField}&sortDesc={sortDesc}";
        return await http.GetFromJsonAsync<PagedResult<ClienteMovimientoDto>>(url, ct)
               ?? new PagedResult<ClienteMovimientoDto>();
    }

    /// <summary>
    /// Obtiene el historico de consumo de un cliente.
    /// </summary>
    public async Task<ClienteHistoricoConsumoResponseDto?> ObtenerHistoricoConsumoAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
    {
        var query = $"?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        return await http.GetFromJsonAsync<ClienteHistoricoConsumoResponseDto?>($"api/clientes/{clienteId}/historico-consumo{query}", ct);
    }

    /// <summary>
    /// Obtiene el historico de consumo de un cliente con paging/sorting remoto.
    /// </summary>
    public async Task<ClienteHistoricoConsumoPagedResponseDto?> ObtenerHistoricoConsumoPagedAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        // NOTE: Added paged call to support virtual scrolling on historico consumo.
        skip = Math.Max(skip, 0);
        take = take <= 0 ? 100 : take;

        var parameters = new List<string>
        {
            $"desde={desde:yyyy-MM-dd}",
            $"hasta={hasta:yyyy-MM-dd}",
            $"skip={skip}",
            $"take={take}"
        };

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
            if (sortDesc)
            {
                parameters.Add("sortDesc=true");
            }
        }

        var url = $"api/clientes/{clienteId}/historico-consumo/paged?{string.Join("&", parameters)}";
        return await http.GetFromJsonAsync<ClienteHistoricoConsumoPagedResponseDto?>(url, ct);
    }

    public async Task<ClienteDetailDto?> ActualizarAsync(int id, ClienteUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PutAsJsonAsyncWithAuthCheck($"api/clientes/{id}", dto, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ClienteDetailDto>(ct);
    }

    public async Task SetNoCortableAsync(string clave, bool noCortable, string password, string? motivo = null, CancellationToken ct = default)
    {
        var request = new SetNoCortableRequest(clave, noCortable, password, motivo);
        var response = await http.PostAsJsonAsync($"api/clientes/{Uri.EscapeDataString(clave)}/no-cortable", request, ct);
        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
    }

    public async Task<List<ClienteEstadoLogItemDto>> ObtenerEstadoLogAsync(int clienteId, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ClienteEstadoLogItemDto>>($"api/clientes/{clienteId}/estado-log", ct)
           ?? new List<ClienteEstadoLogItemDto>();

    private sealed record ClienteCreadoDto(int Id);

    private static string BuildSearchUrl(ClienteFilterDto filtro)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            parameters.Add($"codigo={Uri.EscapeDataString(filtro.Codigo)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Nombre))
        {
            parameters.Add($"nombre={Uri.EscapeDataString(filtro.Nombre)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Barrio))
        {
            parameters.Add($"barrio={Uri.EscapeDataString(filtro.Barrio)}");
        }

        if (filtro.SoloActivos)
        {
            parameters.Add("soloActivos=true");
        }

        return parameters.Count > 0
            ? $"api/clientes/search?{string.Join("&", parameters)}"
            : "api/clientes/search";
    }
}
