using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using apc.Client.Services;

namespace apc.Client.Services.Tarifario;

public sealed class TarifarioConflictoClient
{
    private readonly HttpClient _http;

    public TarifarioConflictoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<TarifarioConflictoListDto>> ObtenerAsync(
        string? search,
        string? estadoCodigo,
        string? rutaCodigo,
        int? clienteId,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(estadoCodigo))
        {
            query.Add($"estadoCodigo={Uri.EscapeDataString(estadoCodigo.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(rutaCodigo))
        {
            query.Add($"rutaCodigo={Uri.EscapeDataString(rutaCodigo.Trim())}");
        }

        if (clienteId.HasValue && clienteId.Value > 0)
        {
            query.Add($"clienteId={clienteId.Value}");
        }

        var uri = "api/tarifario/conflictos-v3";
        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return await _http.GetFromJsonAsyncWithAuthCheck<TarifarioConflictoListDto[]>(uri, ct)
               ?? Array.Empty<TarifarioConflictoListDto>();
    }

    public async Task<ResponseModelDto?> ResolverAsync(
        TarifarioConflictoResolveRequest request,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsyncWithAuthCheck("api/tarifario/conflictos-v3/resolver", request, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>(ct);
    }
}
