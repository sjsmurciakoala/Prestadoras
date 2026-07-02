using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Cobranza;
using apc.Client.Services;

namespace apc.Client.Services.Cobranza;

public class ClientesCobroClient
{
    private readonly HttpClient _http;

    public ClientesCobroClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<ClienteCobroDto>?> ListarAsync(ClienteCobroFiltroDto filtro, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (filtro.CicloId is not null)
            Append(query, "cicloId", filtro.CicloId.Value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(filtro.BarrioCodigo))
            Append(query, "barrioCodigo", filtro.BarrioCodigo);
        if (filtro.CategoriaId is not null)
            Append(query, "categoriaId", filtro.CategoriaId.Value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(filtro.Ruta))
            Append(query, "ruta", filtro.Ruta);
        if (filtro.ValorMinimo is not null)
            Append(query, "valorMinimo", filtro.ValorMinimo.Value.ToString(CultureInfo.InvariantCulture));
        if (filtro.DiasMoraMin is not null)
            Append(query, "diasMoraMin", filtro.DiasMoraMin.Value.ToString(CultureInfo.InvariantCulture));
        if (filtro.ExcluirBloqueados)
            Append(query, "excluirBloqueados", "true");
        if (filtro.ExcluirNoCortables)
            Append(query, "excluirNoCortables", "true");
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            Append(query, "busqueda", filtro.Busqueda);

        var url = "api/Cobranza/clientes-cobro";
        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<ClienteCobroDto>>(url, ct);
    }

    public Task<IReadOnlyList<CarteraVencidaClienteDto>?> ListarCarteraVencidaAsync(
        CarteraVencidaFiltroDto filtro, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (filtro.FechaCorte is not null)
            Append(query, "fechaCorte", filtro.FechaCorte.Value.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            Append(query, "busqueda", filtro.Busqueda);
        if (filtro.Tramo is not null)
            Append(query, "tramo", filtro.Tramo.Value.ToString(CultureInfo.InvariantCulture));
        if (filtro.CicloId is not null)
            Append(query, "cicloId", filtro.CicloId.Value.ToString(CultureInfo.InvariantCulture));

        var url = "api/Cobranza/cartera-vencida";
        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CarteraVencidaClienteDto>>(url, ct);
    }

    public async Task<int> RegistrarAccionLoteAsync(RegistrarAccionLoteRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsyncWithAuthCheck("api/Cobranza/acciones/lote", request, ct);
        var result = await resp.ReadFromJsonAsyncWithAuthCheck<RegistrarAccionLoteResponse>(ct);
        return result?.Registradas ?? 0;
    }

    public async Task<CartaCobroHdrDto?> GenerarCartasAsync(GenerarCartasCobroRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsyncWithAuthCheck("api/Cobranza/cartas-cobro", request, ct);
        return await resp.ReadFromJsonAsyncWithAuthCheck<CartaCobroHdrDto>(ct);
    }

    public string GetImprimirCartasUrl(int id) => $"api/Cobranza/cartas-cobro/{id}/imprimir";

    public string GetCartasPdfUrl(int id) => $"api/Cobranza/cartas-cobro/{id}/pdf";

    private static void Append(List<string> query, string key, string value)
        => query.Add($"{key}={Uri.EscapeDataString(value)}");

    private sealed class RegistrarAccionLoteResponse
    {
        public int Registradas { get; set; }
    }
}
