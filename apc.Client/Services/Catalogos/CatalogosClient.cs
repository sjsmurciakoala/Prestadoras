using System.Net.Http.Json;
using SIAD.Core.DTOs.Catalogos;

namespace apc.Client.Services.Catalogos;

/// <summary>
/// Cliente HTTP para catalogos generales del sistema.
/// </summary>
public sealed class CatalogosClient
{
    private readonly HttpClient http;

    public CatalogosClient(HttpClient http) => this.http = http;

    public async Task<List<AbogadoLookupDto>> ObtenerAbogadosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<AbogadoLookupDto>>("api/catalogos/abogados", ct)
           ?? new List<AbogadoLookupDto>();

    public async Task<List<BarrioLookupDto>> ObtenerBarriosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<BarrioLookupDto>>("api/catalogos/barrios", ct)
           ?? new List<BarrioLookupDto>();

    public async Task<List<ClaseMedidorLookupDto>> ObtenerClasesMedidorAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ClaseMedidorLookupDto>>("api/catalogos/clases-medidor", ct)
           ?? new List<ClaseMedidorLookupDto>();

    public async Task<List<ServicioLookupDto>> ObtenerServiciosAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ServicioLookupDto>>("api/catalogos/servicios", ct)
           ?? new List<ServicioLookupDto>();

    public async Task<List<TipoUsoLookupDto>> ObtenerTiposUsoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<TipoUsoLookupDto>>("api/catalogos/tipos-uso", ct)
           ?? new List<TipoUsoLookupDto>();

    public async Task<List<int>> ObtenerCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<int>>($"api/catalogos/categorias-por-tipo?tipoUsoCodigo={tipoUsoCodigo}", ct)
           ?? new List<int>();

    // ── CRUD Barrios (/api/mantenimientos/barrios) ──────────────────────────────

    public Task<IReadOnlyList<BarrioDto>?> GetBarriosCrudAsync(CancellationToken ct = default)
        => http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<BarrioDto>>("api/mantenimientos/barrios", ct);

    public async Task<BarrioDto?> CrearBarrioAsync(BarrioCreateDto dto, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsyncWithAuthCheck("api/mantenimientos/barrios", dto);
        return await resp.ReadFromJsonAsyncWithAuthCheck<BarrioDto>(ct);
    }

    public async Task<BarrioDto?> ActualizarBarrioAsync(string codigo, BarrioUpdateDto dto, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsyncWithAuthCheck($"api/mantenimientos/barrios/{Uri.EscapeDataString(codigo)}", dto);
        return await resp.ReadFromJsonAsyncWithAuthCheck<BarrioDto>(ct);
    }

    public async Task<bool> EliminarBarrioAsync(string codigo, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/mantenimientos/barrios/{Uri.EscapeDataString(codigo)}", ct);
        return resp.IsSuccessStatusCode;
    }

    // ── CRUD Clases de Medidor (/api/mantenimientos/clases-medidor) ─────────────

    public Task<IReadOnlyList<ClaseMedidorDto>?> GetClasesMedidorCrudAsync(CancellationToken ct = default)
        => http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<ClaseMedidorDto>>("api/mantenimientos/clases-medidor", ct);

    public async Task<ClaseMedidorDto?> CrearClaseMedidorAsync(ClaseMedidorCreateDto dto, CancellationToken ct = default)
    {
        var resp = await http.PostAsJsonAsyncWithAuthCheck("api/mantenimientos/clases-medidor", dto);
        return await resp.ReadFromJsonAsyncWithAuthCheck<ClaseMedidorDto>(ct);
    }

    public async Task<ClaseMedidorDto?> ActualizarClaseMedidorAsync(string codigo, ClaseMedidorUpdateDto dto, CancellationToken ct = default)
    {
        var resp = await http.PutAsJsonAsyncWithAuthCheck($"api/mantenimientos/clases-medidor/{Uri.EscapeDataString(codigo)}", dto);
        return await resp.ReadFromJsonAsyncWithAuthCheck<ClaseMedidorDto>(ct);
    }

    public async Task<bool> EliminarClaseMedidorAsync(string codigo, CancellationToken ct = default)
    {
        var resp = await http.DeleteAsync($"api/mantenimientos/clases-medidor/{Uri.EscapeDataString(codigo)}", ct);
        return resp.IsSuccessStatusCode;
    }
}
