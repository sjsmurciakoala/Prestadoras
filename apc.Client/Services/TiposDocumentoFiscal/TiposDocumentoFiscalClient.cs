using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TiposDocumentoFiscal;

namespace apc.Client.Services.TiposDocumentoFiscal;

public sealed class TiposDocumentoFiscalClient
{
    private readonly HttpClient _http;

    public TiposDocumentoFiscalClient(HttpClient http) => _http = http;

    public async Task<List<TipoDocumentoFiscalDto>> ListarAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<TipoDocumentoFiscalDto>>("api/tipos-documento-fiscal", ct)
           ?? new List<TipoDocumentoFiscalDto>();

    public async Task<ResponseModelDto?> ActualizarAsync(short id, TipoDocumentoFiscalUpdateDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"api/tipos-documento-fiscal/{id}", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }
}
