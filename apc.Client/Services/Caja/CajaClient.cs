using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Caja;
using apc.Client.Services; // HttpClientExtensions

namespace apc.Client.Services.Caja;

public class CajaClient
{
    private readonly HttpClient _http;

    public CajaClient(HttpClient http) => _http = http;

    public Task<SesionCajaDto?> GetSesionActivaAsync(string usuario)
        => _http.GetFromJsonAsyncWithAuthCheck<SesionCajaDto>(
               $"api/caja/sesion-activa?usuario={Uri.EscapeDataString(usuario)}");

    public async Task<CajaResponseDto?> AbrirAsync(AbrirCajaRequestDto request)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/caja/abrir", request);
        return await response.ReadFromJsonAsyncWithAuthCheck<CajaResponseDto>();
    }

    public async Task<CajaResponseDto?> CerrarAsync(CerrarCajaRequestDto request)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/caja/cerrar", request);
        return await response.ReadFromJsonAsyncWithAuthCheck<CajaResponseDto>();
    }

    public Task<ResumenCajaDto?> GetResumenAsync(int sesionId)
        => _http.GetFromJsonAsyncWithAuthCheck<ResumenCajaDto>($"api/caja/sesion/{sesionId}/resumen");

    public Task<IReadOnlyList<HistorialCierreDto>?> GetHistorialAsync(string usuario)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<HistorialCierreDto>>(
               $"api/caja/historial?usuario={Uri.EscapeDataString(usuario)}");
}
