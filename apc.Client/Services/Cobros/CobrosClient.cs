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

    public Task<IReadOnlyList<CobroDelDiaDto>?> DelDiaAsync(DateTime? fecha = null, string? usuario = null, int? cajaId = null)
    {
        var query = new List<string>();
        if (fecha.HasValue) query.Add($"fecha={fecha.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(usuario)) query.Add($"usuario={Uri.EscapeDataString(usuario)}");
        if (cajaId is > 0) query.Add($"cajaId={cajaId}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CobroDelDiaDto>>($"api/cobros/del-dia{qs}");
    }

    // F7 H5: catálogos de apoyo, mudados del CaptacionPagosClient retirado.
    public async Task<IReadOnlyList<SIAD.Core.DTOs.CaptacionPagos.ClienteComboDto>> GetClientesAsync(string? query = null, int? take = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query)}");
        if (take is > 0) qs.Add($"take={take}");
        var suffix = qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty;
        return await _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<SIAD.Core.DTOs.CaptacionPagos.ClienteComboDto>>($"api/cobros/clientes{suffix}") ?? [];
    }

    public async Task<IReadOnlyList<SIAD.Core.DTOs.CaptacionPagos.BancoDto>> GetBancosAsync() =>
        await _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<SIAD.Core.DTOs.CaptacionPagos.BancoDto>>("api/cobros/bancos") ?? [];
}
