using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.DTOs.Common;
using apc.Client.Services; // HttpClientExtensions

namespace apc.Client.Services.Cobros;

// Cliente HTTP del motor único de cobro (unificación cobranza F3).
public class CobrosClient
{
    private readonly HttpClient _http;

    public CobrosClient(HttpClient http) => _http = http;

    public async Task<ResponseModelDto?> RegistrarAsync(CobroCrearDto dto)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/cobros", dto);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>();
    }

    public async Task<ResponseModelDto?> ReversarAsync(CobroReversoDto dto)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/cobros/reverso", dto);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>();
    }

    public Task<IReadOnlyList<CobroDelDiaDto>?> DelDiaAsync(DateTime? fecha = null, string? usuario = null)
    {
        var query = new List<string>();
        if (fecha.HasValue) query.Add($"fecha={fecha.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(usuario)) query.Add($"usuario={Uri.EscapeDataString(usuario)}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CobroDelDiaDto>>($"api/cobros/del-dia{qs}");
    }
}
