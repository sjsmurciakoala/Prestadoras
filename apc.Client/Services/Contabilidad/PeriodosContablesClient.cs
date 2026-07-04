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

    /// <summary>
    /// Reconciliación caché de saldos (con_saldo_cuenta) vs libro (F6).
    /// Usa las extensiones auth-aware: lanza UnauthorizedAccessException en
    /// 401/403/redirect a login y HttpRequestException con el detalle en
    /// errores del servidor — la página decide cómo mostrarlos.
    /// </summary>
    public Task<SaldoVerificacionResultDto?> VerificarSaldosAsync(long companyId, long? periodId = null,
        CancellationToken ct = default)
    {
        var url = $"api/contabilidad/saldos/{companyId}/verificacion";
        if (periodId.HasValue)
        {
            url += $"?periodId={periodId.Value}";
        }

        return _httpClient.GetFromJsonAsyncWithAuthCheck<SaldoVerificacionResultDto>(url, ct);
    }

    private sealed class ExistePeriodoAbiertoResponse
    {
        public bool Existe { get; set; }
    }
}