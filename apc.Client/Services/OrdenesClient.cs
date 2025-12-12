using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Ordenes;

namespace apc.Client.Services.Ordenes;

public class OrdenesClient
{
    private readonly HttpClient _httpClient;

    public OrdenesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<OrdenTrabajoListItemDto>> GetOrdenesAsync(OrdenTrabajoFilterDto filtro, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder("api/ordenes");
        var query = BuildQueryString(filtro);
        if (query.Length > 0)
        {
            urlBuilder.Append('?').Append(query);
        }

        var resultado = await GetJsonAsync<OrdenTrabajoListItemDto[]>(urlBuilder.ToString(), cancellationToken);
        return resultado ?? Array.Empty<OrdenTrabajoListItemDto>();
    }

    public async Task<OrdenTrabajoDetailDto?> GetOrdenAsync(int id, CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync<OrdenTrabajoDetailDto>($"api/ordenes/{id}", cancellationToken);
    }

    public async Task<OrdenTrabajoOperacionResultadoDto> CrearOrdenAsync(CrearOrdenTrabajoDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/ordenes", dto, cancellationToken);
            await EnsureSuccessJsonResponseAsync(response, cancellationToken);
            var ok = await response.Content.ReadFromJsonAsync<OrdenTrabajoOperacionResultadoDto>(cancellationToken: cancellationToken);
            return ok ?? new OrdenTrabajoOperacionResultadoDto(true, "Orden creada.");
        }
        catch (Exception ex)
        {
            return new OrdenTrabajoOperacionResultadoDto(false, ex.Message);
        }
    }

    public async Task<OrdenTrabajoOperacionResultadoDto> AsignarOrdenesAsync(OrdenTrabajoAsignacionDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/ordenes/asignaciones", dto, cancellationToken);
            await EnsureSuccessJsonResponseAsync(response, cancellationToken);
            var ok = await response.Content.ReadFromJsonAsync<OrdenTrabajoOperacionResultadoDto>(cancellationToken: cancellationToken);
            return ok ?? new OrdenTrabajoOperacionResultadoDto(true, "Órdenes asignadas.");
        }
        catch (Exception ex)
        {
            return new OrdenTrabajoOperacionResultadoDto(false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<UsuarioMiOrdenDto>> GetUsuariosAsync(int? tipo, CancellationToken cancellationToken = default)
    {
        var url = tipo.HasValue ? $"api/ordenes/usuarios?tipo={tipo.Value}" : "api/ordenes/usuarios";
        var usuarios = await GetJsonAsync<UsuarioMiOrdenDto[]>(url, cancellationToken);
        return usuarios ?? Array.Empty<UsuarioMiOrdenDto>();
    }

    public async Task<IReadOnlyList<OrdenTrabajoTipoDto>> BuscarTiposAsync(string departamento, string? texto, int take = 20, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder($"api/ordenes/tipos?departamento={Uri.EscapeDataString(departamento)}&take={take}");
        if (!string.IsNullOrWhiteSpace(texto))
        {
            urlBuilder.Append("&q=").Append(Uri.EscapeDataString(texto));
        }

        var tipos = await GetJsonAsync<OrdenTrabajoTipoDto[]>(urlBuilder.ToString(), cancellationToken);
        return tipos ?? Array.Empty<OrdenTrabajoTipoDto>();
    }

    public async Task<IReadOnlyList<OrdenTrabajoPropietarioDto>> BuscarPropietariosAsync(string? texto, int take = 20, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder($"api/ordenes/propietarios?take={take}");
        if (!string.IsNullOrWhiteSpace(texto))
        {
            urlBuilder.Append("&q=").Append(Uri.EscapeDataString(texto));
        }

        var propietarios = await GetJsonAsync<OrdenTrabajoPropietarioDto[]>(urlBuilder.ToString(), cancellationToken);
        return propietarios ?? Array.Empty<OrdenTrabajoPropietarioDto>();
    }

    public async Task<IReadOnlyList<OrdenTrabajoEstadoDto>> BuscarEstadosAsync(string? texto, int take = 20, CancellationToken cancellationToken = default)
    {
        var urlBuilder = new StringBuilder($"api/ordenes/estados?take={take}");
        if (!string.IsNullOrWhiteSpace(texto))
        {
            urlBuilder.Append("&q=").Append(Uri.EscapeDataString(texto));
        }

        var estados = await GetJsonAsync<OrdenTrabajoEstadoDto[]>(urlBuilder.ToString(), cancellationToken);
        return estados ?? Array.Empty<OrdenTrabajoEstadoDto>();
    }

    public async Task<IReadOnlyList<CoordenadaOrdenDto>> GetCoordenadasAsync(CancellationToken cancellationToken = default)
    {
        var coordenadas = await GetJsonAsync<CoordenadaOrdenDto[]>("api/ordenes/coordenadas", cancellationToken);
        return coordenadas ?? Array.Empty<CoordenadaOrdenDto>();
    }

    private static string BuildQueryString(OrdenTrabajoFilterDto filtro)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(filtro.Departamento))
        {
            parameters.Add($"departamento={Uri.EscapeDataString(filtro.Departamento)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            parameters.Add($"tipo={Uri.EscapeDataString(filtro.Tipo)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            parameters.Add($"estado={Uri.EscapeDataString(filtro.Estado)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.ClienteClave))
        {
            parameters.Add($"clienteClave={Uri.EscapeDataString(filtro.ClienteClave)}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            parameters.Add($"busqueda={Uri.EscapeDataString(filtro.Busqueda)}");
        }

        if (filtro.FechaDesde.HasValue)
        {
            parameters.Add($"fechaDesde={filtro.FechaDesde.Value:yyyy-MM-dd}");
        }

        if (filtro.FechaHasta.HasValue)
        {
            parameters.Add($"fechaHasta={filtro.FechaHasta.Value:yyyy-MM-dd}");
        }

        if (filtro.Anio.HasValue)
        {
            parameters.Add($"anio={filtro.Anio.Value}");
        }

        if (filtro.Mes.HasValue)
        {
            parameters.Add($"mes={filtro.Mes.Value}");
        }

        return string.Join("&", parameters);
    }

    private async Task<TResponse?> GetJsonAsync<TResponse>(string url, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[OrdenesClient] GET {url}");
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
            Console.WriteLine($"[OrdenesClient] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            Console.WriteLine($"[OrdenesClient] Content-Type: {response.Content.Headers.ContentType?.MediaType}");
            await EnsureSuccessJsonResponseAsync(response, cancellationToken);
            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            var ct = response?.Content.Headers.ContentType?.ToString() ?? "<none>";
            Console.WriteLine($"[OrdenesClient] ERROR GET {url} Content-Type={ct}: {ex.Message}");
            throw;
        }
    }

    private static async Task EnsureSuccessJsonResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Tu sesión ha expirado. Vuelve a iniciar sesión.");
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = string.IsNullOrWhiteSpace(payload) ? response.ReasonPhrase : payload;
            throw new InvalidOperationException(message ?? $"Error HTTP {(int)response.StatusCode}");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("El servidor devolvió un contenido no válido (posible sesión expirada).");
        }
    }
}
