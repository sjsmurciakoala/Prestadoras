using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Mantenimientos;

namespace apc.Client.Services.Mantenimientos;

public class MantenimientosClient
{
    private readonly HttpClient _http;

    public MantenimientosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<RecargoMoraDto> ObtenerRecargoMoraAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<RecargoMoraDto>("api/mantenimientos/recargo-mora", ct);
        return result ?? new RecargoMoraDto();
    }

    public async Task<ResponseModelDto> GuardarRecargoMoraAsync(RecargoMoraDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/mantenimientos/recargo-mora", dto, ct);
        return await ReadResponseAsync(response, ct);
    }

    public async Task<IReadOnlyList<AjusteTarifarioDto>> ListarAjustesTarifariosAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<List<AjusteTarifarioDto>>("api/mantenimientos/ajustes-tarifarios", ct);
        return result ?? new List<AjusteTarifarioDto>();
    }

    public async Task<ResponseModelDto> GuardarAjusteTarifarioAsync(AjusteTarifarioSaveRequestDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/mantenimientos/ajustes-tarifarios", dto, ct);
        return await ReadResponseAsync(response, ct);
    }

    private static async Task<ResponseModelDto> ReadResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ResponseModelDto? payload = null;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
        }
        catch
        {
            // Contenido no parseable.
        }

        payload ??= new ResponseModelDto
        {
            Success = response.IsSuccessStatusCode,
            Message = response.ReasonPhrase ?? string.Empty
        };

        payload.Success = payload.Success && response.IsSuccessStatusCode;
        if (!payload.Success && string.IsNullOrWhiteSpace(payload.Message))
        {
            payload.Message = "La operación no pudo completarse.";
        }

        return payload;
    }
}
