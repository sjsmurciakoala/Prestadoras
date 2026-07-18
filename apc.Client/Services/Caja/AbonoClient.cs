using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Caja;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.CaptacionPagos;
using apc.Client.Services; // HttpClientExtensions

namespace apc.Client.Services.Caja;

public class AbonoClient
{
    private readonly HttpClient _http;

    public AbonoClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<FacturaConSaldoDto>?> BuscarFacturasConSaldoAsync(string term)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<FacturaConSaldoDto>>(
            $"api/abono/buscar-facturas?term={Uri.EscapeDataString(term)}");

    public Task<IReadOnlyList<FacturaConSaldoDto>?> ListarFacturasPendientesPorClienteAsync(string clienteClave)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<FacturaConSaldoDto>>(
            $"api/abono/facturas-por-cliente?clienteClave={Uri.EscapeDataString(clienteClave)}");

    public async Task<ResponseModelDto?> RegistrarAbonoAsync(AbonoCrearDto request)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/abono/registrar", request);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>();
    }

    public async Task<ResponseModelDto?> ReversarAbonoAsync(ReversoAbonoRequestDto request)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/abono/reversar", request);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>();
    }

    public Task<IReadOnlyList<ArqueoDto>?> ListarAbonosDelDiaAsync(string? usuario = null, DateTime? fecha = null)
    {
        var url = "api/abono/arqueo?";
        if (!string.IsNullOrWhiteSpace(usuario))
        {
            url += $"usuario={Uri.EscapeDataString(usuario)}&";
        }
        if (fecha.HasValue)
        {
            url += $"fecha={fecha.Value:yyyy-MM-dd}&";
        }
        return _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<ArqueoDto>>(url);
    }

    public Task<IReadOnlyList<AbonoHistorialItemDto>?> ListarHistorialAsync(string clienteClave)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<AbonoHistorialItemDto>>(
            $"api/abono/historial/{Uri.EscapeDataString(clienteClave)}");

    public Task<ClienteSaldoDto?> ObtenerSaldoClienteAsync(string clienteClave)
        => _http.GetFromJsonAsyncWithAuthCheck<ClienteSaldoDto>(
            $"api/abono/saldo-cliente?clienteClave={Uri.EscapeDataString(clienteClave)}");

    public async Task<ResponseModelDto?> GenerarReciboPendienteAsync(GenerarReciboDto request)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/abono/generar-recibo", request);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>();
    }

    public Task<IReadOnlyList<ReciboPendienteDto>?> ListarRecibosPendientesAsync(string numFactura)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<ReciboPendienteDto>>(
            $"api/abono/recibos-pendientes?numFactura={Uri.EscapeDataString(numFactura)}");

    public Task<IReadOnlyList<AbonoHistorialItemDto>?> ListarHistorialPorFacturaAsync(string numFactura)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<AbonoHistorialItemDto>>(
            $"api/abono/historial-factura/{Uri.EscapeDataString(numFactura)}");

    public async Task<ResponseModelDto?> AnularReciboPendienteAsync(AnularReciboPendienteDto request)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/abono/anular-pendiente", request);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>();
    }
}
