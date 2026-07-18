using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace apc.Client.Services.Tarifario;

public sealed class DesgloseAbonoConfigClient
{
    private readonly HttpClient http;

    public DesgloseAbonoConfigClient(HttpClient http) => this.http = http;

    public async Task<DesgloseAbonoItemDto[]> ObtenerAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<DesgloseAbonoItemDto[]>("api/tarifario/desglose-abonos", ct)
           ?? [];

    public async Task<ResponseModelDto?> GuardarAsync(DesgloseAbonoGuardarDto request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsyncWithAuthCheck("api/tarifario/desglose-abonos", request, ct);
        return await response.ReadFromJsonAsyncWithAuthCheck<ResponseModelDto>(ct);
    }
}
