using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Almacen;

namespace apc.Client.Services.Almacen;

/// <summary>Cliente HTTP del corte de carga inicial de existencias.</summary>
public sealed class CargaInicialClient
{
    private readonly HttpClient _http;

    public CargaInicialClient(HttpClient http) => _http = http;

    public async Task<List<CargaInicialPendienteDto>> GetPendientesAsync(int? bodegaId = null, CancellationToken ct = default)
    {
        var url = bodegaId.HasValue
            ? $"api/almacen/carga-inicial/pendientes?bodegaId={bodegaId.Value}"
            : "api/almacen/carga-inicial/pendientes";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<CargaInicialPendienteDto>>(ct) ?? new();
    }

    public async Task<CargaInicialSimulacionDto> SimularAsync(int? bodegaId = null, CancellationToken ct = default)
    {
        var url = bodegaId.HasValue
            ? $"api/almacen/carga-inicial/simular?bodegaId={bodegaId.Value}"
            : "api/almacen/carga-inicial/simular";
        var r = await _http.GetAsync(url, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<CargaInicialSimulacionDto>(ct) ?? new();
    }

    public async Task<CargaInicialConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/almacen/carga-inicial/config", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<CargaInicialConfigDto>(ct) ?? new();
    }

    public async Task<CargaInicialResultadoDto> EjecutarAsync(
        DateOnly fechaCorte, int tamanoLote = 200, int? bodegaId = null, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/carga-inicial/ejecutar",
            new { FechaCorte = fechaCorte, TamanoLote = tamanoLote, BodegaId = bodegaId }, ct);

        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo ejecutar el corte.");
        }

        return await r.ReadFromJsonAsyncWithAuthCheck<CargaInicialResultadoDto>(ct) ?? new();
    }

    public async Task<CargaInicialResultadoDto> PostearCostoManualAsync(
        DateOnly fechaCorte, List<CargaInicialCostoManualDto> items, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/carga-inicial/costo-manual",
            new { FechaCorte = fechaCorte, Items = items }, ct);

        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudieron postear los costos.");
        }

        return await r.ReadFromJsonAsyncWithAuthCheck<CargaInicialResultadoDto>(ct) ?? new();
    }

    /// <summary>Requiere permiso de Configuración.</summary>
    public async Task CerrarAsync(CancellationToken ct = default)
    {
        var r = await _http.PostAsync("api/almacen/carga-inicial/cerrar", content: null, ct);
        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo cerrar el corte.");
        }
    }

    /// <summary>Reversa + nueva apertura con el costo correcto. Requiere permiso de Configuración.</summary>
    public async Task<PosteoResultDto> ReabrirAsync(
        int articuloId, int bodegaId, decimal nuevoCosto, string motivo, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync("api/almacen/carga-inicial/reabrir",
            new { ArticuloId = articuloId, BodegaId = bodegaId, NuevoCosto = nuevoCosto, Motivo = motivo }, ct);

        if (!r.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? "No se pudo reabrir la carga inicial.");
        }

        return await r.ReadFromJsonAsyncWithAuthCheck<PosteoResultDto>(ct)
               ?? throw new InvalidOperationException("El servicio devolvió una respuesta vacía.");
    }
}
