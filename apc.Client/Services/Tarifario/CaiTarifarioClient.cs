using System.Net.Http.Json;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace apc.Client.Services.Tarifario;

public sealed class CaiTarifarioClient
{
    private readonly HttpClient http;

    public CaiTarifarioClient(HttpClient http) => this.http = http;

    public async Task<CaiFacturacionListDto[]> ObtenerCaisAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<CaiFacturacionListDto[]>("api/tarifario/cai-offline/cais", ct)
           ?? Array.Empty<CaiFacturacionListDto>();

    public async Task<PagedResult<CaiFacturacionListDto>> GetCaisPagedAsync(
        CaiFacturacionFilterDto filter,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var parameters = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };
        if (!string.IsNullOrWhiteSpace(filter?.Search))
            parameters.Add($"search={Uri.EscapeDataString(filter.Search)}");
        if (filter?.Activo.HasValue == true)
            parameters.Add($"activo={(filter.Activo.Value ? "true" : "false")}");
        if (filter?.EstadoId is short estId && estId > 0)
            parameters.Add($"estadoId={estId}");
        if (!string.IsNullOrWhiteSpace(sortField))
        {
            parameters.Add($"sortField={Uri.EscapeDataString(sortField)}");
            if (sortDesc) parameters.Add("sortDesc=true");
        }

        var url = $"api/tarifario/cai-offline/cais/paged?{string.Join("&", parameters)}";
        return await http.GetFromJsonAsync<PagedResult<CaiFacturacionListDto>>(url, ct)
               ?? new PagedResult<CaiFacturacionListDto>();
    }

    public async Task<CaiBloqueReservadoListDto[]> ObtenerBloquesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<CaiBloqueReservadoListDto[]>("api/tarifario/cai-offline/bloques", ct)
           ?? Array.Empty<CaiBloqueReservadoListDto>();

    public async Task<ResponseModelDto?> GuardarCaiAsync(CaiFacturacionSaveRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/tarifario/cai-offline/cais", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    public async Task<ResponseModelDto?> ReservarBloqueAsync(CaiBloqueReservadoSaveRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/tarifario/cai-offline/bloques/reservar", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }

    public async Task<TipoDocumentoFiscalLookupDto[]> ObtenerTiposDocumentoLookupAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<TipoDocumentoFiscalLookupDto[]>("api/tarifario/cai-offline/tipos-documento-lookup", ct)
           ?? Array.Empty<TipoDocumentoFiscalLookupDto>();

    public async Task<CaiEstadoLookupDto[]> ObtenerEstadosLookupAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<CaiEstadoLookupDto[]>("api/tarifario/cai-offline/estados-lookup", ct)
           ?? Array.Empty<CaiEstadoLookupDto>();

    public async Task<ResponseModelDto?> CambiarEstadoAsync(long caiId, short estadoId, CancellationToken ct = default)
    {
        var response = await http.PatchAsJsonAsync(
            $"api/tarifario/cai-offline/cais/{caiId}/estado",
            new { EstadoId = estadoId }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResponseModelDto>(cancellationToken: ct);
    }
}
