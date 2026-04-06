using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Medidores;

namespace apc.Client.Services.Medidores;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con medidores.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class MedidoresClient
{
    private readonly HttpClient http;

    public MedidoresClient(HttpClient http) => this.http = http;

    /// <summary>
    /// Obtiene la lista de medidores con filtros opcionales.
    /// </summary>
    /// <param name="clienteClave">Filtro: código del cliente</param>
    /// <param name="asignado">Filtro: true=asignados, false=disponibles</param>
    public async Task<List<MedidorListDto>> ObtenerListaAsync(
        string? clienteClave = null,
        bool? asignado = null,
        CancellationToken ct = default)
    {
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(clienteClave))
            queryParts.Add($"ClienteClave={Uri.EscapeDataString(clienteClave)}");
        
        if (asignado.HasValue)
            queryParts.Add($"Asignado={asignado.Value.ToString().ToLowerInvariant()}");

        var query = queryParts.Count > 0 ? $"?{string.Join("&", queryParts)}" : string.Empty;
        
        return await http.GetFromJsonAsync<List<MedidorListDto>>($"api/medidores{query}", ct)
               ?? new List<MedidorListDto>();
    }

    /// <summary>
    /// Obtiene el detalle completo de un medidor por su ID.
    /// </summary>
    public async Task<MedidorDetailDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<MedidorDetailDto?>($"api/medidores/{id}", ct);

    public async Task<PagedResult<MedidorListItemDto>?> GetPagedAsync(
        MedidorFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var filter = filtro ?? new MedidorFilterDto(null, null, null, null, null);

        var parameters = new List<string>
        {
            $"skip={Math.Max(0, skip)}",
            $"take={Math.Max(1, take)}"
        };

        if (!string.IsNullOrWhiteSpace(filter.Numero))
        {
            parameters.Add($"numero={Uri.EscapeDataString(filter.Numero)}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Marca))
        {
            parameters.Add($"marca={Uri.EscapeDataString(filter.Marca)}");
        }

        if (filter.Estado.HasValue)
        {
            parameters.Add($"estado={(filter.Estado.Value ? "true" : "false")}");
        }

        if (filter.Asignado.HasValue)
        {
            parameters.Add($"asignado={(filter.Asignado.Value ? "true" : "false")}");
        }

        if (!string.IsNullOrWhiteSpace(filter.ClienteClave))
        {
            parameters.Add($"clienteClave={Uri.EscapeDataString(filter.ClienteClave)}");
        }

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
        }

        parameters.Add($"sortDesc={(sortDesc ? "true" : "false")}");

        var url = $"api/medidores/paged?{string.Join("&", parameters)}";
        var response = await http.GetAsync(url, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<PagedResult<MedidorListItemDto>>(ct);
    }

    public async Task<MedidorEditDto?> GetEditByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var response = await http.GetAsync($"api/medidores/{id}/edit", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        return await response.ReadFromJsonAsyncWithAuthCheck<MedidorEditDto>(ct);
    }

    public async Task<MedidorEditDto> CreateAsync(MedidorEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsync("api/medidores", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<MedidorEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El medidor devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<MedidorEditDto> UpdateAsync(int id, MedidorEditDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentException("El ID del medidor debe ser valido.", nameof(id));
        }

        var response = await http.PutAsJsonAsync($"api/medidores/{id}", dto, ct);
        var result = await response.ReadFromJsonAsyncWithAuthCheck<MedidorEditDto>(ct);
        if (result is null)
        {
            throw new InvalidOperationException("El medidor devolvio una respuesta vacia.");
        }

        return result;
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El ID del medidor debe ser valido.", nameof(id));
        }

        var response = await http.PostAsync($"api/medidores/{id}/desactivar", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        await response.ReadFromJsonAsyncWithAuthCheck<object>(ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Asigna un medidor a un cliente.
    /// </summary>
    public async Task<HttpResponseMessage> AsignarAsync(int medidorId, int clienteId, CancellationToken ct = default)
    {
        var payload = new { MedidorId = medidorId, ClienteId = clienteId };
        return await http.PostAsJsonAsync("api/medidores/asignar", payload, cancellationToken: ct);
    }

    /// <summary>
    /// Registra una lectura para un cliente sin medidor.
    /// </summary>
    public async Task<HttpResponseMessage> RegistrarLecturaSinMedidorAsync(
        LecturaSinMedidorModel lectura,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        return await http.PostAsJsonAsync("api/medidores/lecturas-sin-medidor", lectura, cancellationToken: ct);
    }

    /// <summary>
    /// Modelo para registrar lecturas sin medidor asociado.
    /// </summary>
    public class LecturaSinMedidorModel
    {
        public string Clave { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Today;
        public decimal Lectura { get; set; }
        public string Usuario { get; set; } = string.Empty;
    }
}
