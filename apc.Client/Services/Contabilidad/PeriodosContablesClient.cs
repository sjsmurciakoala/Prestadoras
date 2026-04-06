using System.Net.Http.Json;
using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

/// <summary>
/// Cliente para interactuar con el API de periodos contables.
/// </summary>
public sealed class PeriodosContablesClient
{
    private readonly HttpClient _httpClient;

    public PeriodosContablesClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Obtiene el periodo activo de una empresa.
    /// </summary>
    public async Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(long companyId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/contabilidad/periodos/{companyId}/activo", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var resultado = await response.Content.ReadFromJsonAsync<PeriodoContableDto>(cancellationToken: ct);
            return resultado;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Valida que exista un periodo abierto.
    /// </summary>
    public async Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/contabilidad/periodos/{companyId}/existe-abierto", ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var resultado = await response.Content.ReadFromJsonAsync<ExistePeriodoAbiertoResponse>(cancellationToken: ct);
            return resultado?.Existe ?? false;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ExistePeriodoAbiertoResponse
    {
        public bool Existe { get; set; }
    }
}