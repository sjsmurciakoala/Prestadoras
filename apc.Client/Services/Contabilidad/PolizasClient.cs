using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Contabilidad;
using apc.Client.Services;

namespace apc.Client.Services.Contabilidad;

/// Cliente HTTP para la API de polizas.
/// Define DTOs locales que coinciden con el JSON del backend para evitar
/// dependencias del ensamblado del servidor en el cliente WASM.
public sealed class PolizasClient
{
    private readonly HttpClient _http;

    public PolizasClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<PolizaConLineasDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentException("id debe ser > 0", nameof(id));

        var response = await _http.GetAsync($"api/contabilidad/polizas/{id}", ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al obtener poliza {id}: {response.StatusCode}");

        return await response.Content.ReadFromJsonAsync<PolizaConLineasDto?>(cancellationToken: ct);
    }

    public async Task<List<PolizaListaDto>> ListarPorPeriodoAsync(long periodId, int skip = 0, int take = 100, CancellationToken ct = default)
    {
        if (periodId <= 0) throw new ArgumentException("periodId debe ser > 0", nameof(periodId));

        var url = $"api/contabilidad/polizas?periodId={periodId}&skip={skip}&take={take}";
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al listar polizas del periodo {periodId}: {response.StatusCode}");

        return await response.Content.ReadFromJsonAsync<List<PolizaListaDto>>(cancellationToken: ct) ?? new List<PolizaListaDto>();
    }

    public async Task<List<PolizaListaDto>> ListarPorDiarioAsync(long journalId, int skip = 0, int take = 100, CancellationToken ct = default)
    {
        if (journalId <= 0) throw new ArgumentException("journalId debe ser > 0", nameof(journalId));

        var url = $"api/contabilidad/polizas?journalId={journalId}&skip={skip}&take={take}";
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al listar polizas del diario {journalId}: {response.StatusCode}");

        return await response.Content.ReadFromJsonAsync<List<PolizaListaDto>>(cancellationToken: ct) ?? new List<PolizaListaDto>();
    }

    public async Task<long> CrearAsync(PolizaCrearRequest dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/contabilidad/polizas", dto, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await HttpClientExtensions.ObtenerMensajeErrorAsync(response, ct);
            throw new HttpRequestException(detail ?? $"Error al crear poliza: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<CreateResponseDto>(cancellationToken: ct);
        return result?.Id ?? throw new InvalidOperationException("La respuesta no contiene ID");
    }

    public async Task ActualizarAsync(long id, PolizaActualizarRequest dto, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentException("id debe ser > 0", nameof(id));
        ArgumentNullException.ThrowIfNull(dto);

        var response = await _http.PutAsJsonAsync($"api/contabilidad/polizas/{id}", dto, cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al actualizar poliza {id}: {response.StatusCode}");
    }

    public async Task RegistrarAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentException("id debe ser > 0", nameof(id));

        var response = await _http.PostAsync($"api/contabilidad/polizas/{id}/registrar", null, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al registrar poliza {id}: {response.StatusCode}");
    }

    public async Task RevertirAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentException("id debe ser > 0", nameof(id));

        var response = await _http.PostAsync($"api/contabilidad/polizas/{id}/revertir", null, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al revertir poliza {id}: {response.StatusCode}");
    }

    public async Task EliminarAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentException("id debe ser > 0", nameof(id));

        var response = await _http.DeleteAsync($"api/contabilidad/polizas/{id}", ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al eliminar poliza {id}: {response.StatusCode}");
    }

    public async Task<(bool Balanceado, decimal TotalDebito, decimal TotalCredito)> ValidarAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentException("id debe ser > 0", nameof(id));

        var response = await _http.GetAsync($"api/contabilidad/polizas/{id}/validar", ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Error al validar poliza {id}: {response.StatusCode}");

        var result = await response.Content.ReadFromJsonAsync<ValidacionResponseDto>(cancellationToken: ct);
        if (result is null) throw new InvalidOperationException("Sin respuesta de validacion");

        return (result.Balanceado, result.TotalDebito, result.TotalCredito);
    }

    private sealed record CreateResponseDto(long Id);
    private sealed record ValidacionResponseDto(bool Balanceado, decimal TotalDebito, decimal TotalCredito);
}
