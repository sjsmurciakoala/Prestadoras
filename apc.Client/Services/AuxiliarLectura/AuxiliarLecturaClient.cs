using System.Net.Http.Json;
using SIAD.Core.DTOs.AuxiliarLectura;

namespace apc.Client.Services.AuxiliarLectura;

/// <summary>
/// Cliente HTTP para operaciones relacionadas con el auxiliar de lectura.
/// Patrón: Cliente HTTP sellado con métodos async reutilizables.
/// </summary>
public sealed class AuxiliarLecturaClient
{
    private readonly HttpClient http;

    public AuxiliarLecturaClient(HttpClient http) => this.http = http;

    /// <summary>
    /// Obtiene el período de lectura actualmente abierto.
    /// </summary>
    public async Task<AuxiliarLecturaPeriodoDto?> ObtenerPeriodoActualAsync(CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<AuxiliarLecturaPeriodoDto?>("api/auxiliarlectura/periodo-actual", ct);
    }

    /// <summary>
    /// Obtiene la lista de auxiliares de lectura con filtros opcionales.
    /// </summary>
    public async Task<List<AuxiliarLecturaDto>> ObtenerListaAsync(
        int? anio = null,
        int? mes = null,
        string? ciclo = null,
        bool soloPendientes = false,
        CancellationToken ct = default)
    {
        var queryParts = new List<string>();

        if (anio.HasValue)
            queryParts.Add($"anio={anio.Value}");

        if (mes.HasValue)
            queryParts.Add($"mes={mes.Value}");

        if (!string.IsNullOrWhiteSpace(ciclo))
            queryParts.Add($"ciclo={Uri.EscapeDataString(ciclo)}");

        if (soloPendientes)
            queryParts.Add("soloPendientes=true");

        var query = queryParts.Count > 0 ? $"?{string.Join("&", queryParts)}" : string.Empty;

        return await http.GetFromJsonAsync<List<AuxiliarLecturaDto>>($"api/auxiliarlectura{query}", ct)
               ?? new List<AuxiliarLecturaDto>();
    }

    /// <summary>
    /// Obtiene la lista de auxiliares de lectura con paginación remota.
    /// </summary>
    public async Task<AuxiliarLecturaPagedResponseDto?> ObtenerListaPaginadaAsync(
        int? anio,
        int? mes,
        string? ciclo,
        bool soloPendientes,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        skip = Math.Max(skip, 0);
        take = take <= 0 ? 100 : take;

        var queryParts = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };

        if (anio.HasValue)
            queryParts.Add($"anio={anio.Value}");

        if (mes.HasValue)
            queryParts.Add($"mes={mes.Value}");

        if (!string.IsNullOrWhiteSpace(ciclo))
            queryParts.Add($"ciclo={Uri.EscapeDataString(ciclo)}");

        if (soloPendientes)
            queryParts.Add("soloPendientes=true");

        if (!string.IsNullOrWhiteSpace(sortField))
        {
            queryParts.Add($"sortField={Uri.EscapeDataString(sortField)}");
            if (sortDesc)
                queryParts.Add("sortDesc=true");
        }

        var query = $"?{string.Join("&", queryParts)}";
        return await http.GetFromJsonAsync<AuxiliarLecturaPagedResponseDto?>($"api/auxiliarlectura/paged{query}", ct);
    }

    /// <summary>
    /// Genera un nuevo período de lectura.
    /// </summary>
    public async Task<HttpResponseMessage> GenerarPeriodoAsync(
        int anio,
        int mes,
        string ciclo,
        string usuario = "sistema",
        CancellationToken ct = default)
    {
        var payload = new { Anio = anio, Mes = mes, Ciclo = ciclo, Usuario = usuario };
        return await http.PostAsJsonAsync("api/auxiliarlectura", payload, cancellationToken: ct);
    }

    /// <summary>
    /// Cierra un período de lectura.
    /// </summary>
    public async Task<HttpResponseMessage> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var payload = new { Anio = anio, Mes = mes };
        return await http.PostAsJsonAsync("api/auxiliarlectura/cierre", payload, cancellationToken: ct);
    }

    /// <summary>
    /// Elimina un período de lectura.
    /// </summary>
    public async Task<HttpResponseMessage> EliminarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var queryParts = new List<string>();
        queryParts.Add($"anio={anio}");
        queryParts.Add($"mes={mes}");

        var query = $"?{string.Join("&", queryParts)}";
        return await http.DeleteAsync($"api/auxiliarlectura{query}", cancellationToken: ct);
    }

    /// <summary>
    /// Carga múltiples lecturas de forma masiva.
    /// </summary>
    public async Task<HttpResponseMessage> CargarLecturasMasivasAsync(
        CargaMasivaModel cargaMasiva,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cargaMasiva);
        return await http.PostAsJsonAsync("api/auxiliarlectura/masivo", cargaMasiva, cancellationToken: ct);
    }

    /// <summary>
    /// Modelo para carga masiva de lecturas.
    /// </summary>
    public class CargaMasivaModel
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string? Ciclo { get; set; }
        public CargaMasivaItemModel[] Lecturas { get; set; } = Array.Empty<CargaMasivaItemModel>();
    }

    /// <summary>
    /// Elemento individual en carga masiva de lecturas.
    /// </summary>
    public class CargaMasivaItemModel
    {
        public string Clave { get; set; } = string.Empty;
        public string Contador { get; set; } = string.Empty;
        public decimal LecturaAnterior { get; set; }
        public decimal LecturaActual { get; set; }
        public string Usuario { get; set; } = string.Empty;
    }
}
