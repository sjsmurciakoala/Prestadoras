using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Cobranza;
using apc.Client.Services;

namespace apc.Client.Services.Cobranza;

public class CorteMasivoClient
{
    private readonly HttpClient _http;

    public CorteMasivoClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<CorteMasivoHdrDto>?> ListarAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CorteMasivoHdrDto>>("api/corte-masivo", ct);

    public async Task<CorteMasivoHdrDto?> GenerarAsync(
        GenerarCorteMasivoRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsyncWithAuthCheck("api/corte-masivo", request, ct);
        return await resp.ReadFromJsonAsyncWithAuthCheck<CorteMasivoHdrDto>();
    }

    public Task<CorteMasivoDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default)
        => _http.GetFromJsonAsyncWithAuthCheck<CorteMasivoDetalleDto>($"api/corte-masivo/{id}", ct);

    public Task<CorteMasivoDetalleDto?> ObtenerParaReimpresionAsync(int id, CancellationToken ct = default)
        => _http.GetFromJsonAsyncWithAuthCheck<CorteMasivoDetalleDto>($"api/corte-masivo/{id}/reimprimir", ct);

    public string GetImprimirUrl(int id, bool soloSinPago = false)
        => soloSinPago
           ? $"api/corte-masivo/{id}/imprimir?soloSinPago=true"
           : $"api/corte-masivo/{id}/imprimir";

    public string GetExcelUrl(int id, bool soloSinPago = false)
        => soloSinPago
           ? $"api/corte-masivo/{id}/excel?soloSinPago=true"
           : $"api/corte-masivo/{id}/excel";

    public string GetComparativoExcelUrl(int id)
        => $"api/corte-masivo/{id}/comparativo-excel";
}
