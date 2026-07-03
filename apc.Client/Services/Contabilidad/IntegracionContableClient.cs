using SIAD.Core.DTOs.Contabilidad;

namespace apc.Client.Services.Contabilidad;

/// <summary>
/// Cliente HTTP de la Configuración de Integración Contable ↔ Comercial
/// (plan 2026-07-02, Fase 2).
/// </summary>
public sealed class IntegracionContableClient
{
    private readonly HttpClient http;

    public IntegracionContableClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<IntegracionContableDto> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IntegracionContableDto>(
            $"api/contabilidad/integracion/{companyId}", ct);
        return resultado ?? new IntegracionContableDto();
    }

    public async Task<IntegracionGuardarResultDto> GuardarAsync(long companyId, IntegracionContableDto dto,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(dto);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/contabilidad/integracion/{companyId}", dto, ct);
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<IntegracionGuardarResultDto>(ct);
        return resultado ?? throw new InvalidOperationException("El servidor devolvió una respuesta vacía.");
    }

    public async Task<IntegracionPerfilResultDto> AplicarPerfilAsync(long companyId, string perfil,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(perfil);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/contabilidad/integracion/{companyId}/perfil/{Uri.EscapeDataString(perfil)}", new { }, ct);
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<IntegracionPerfilResultDto>(ct);
        return resultado ?? throw new InvalidOperationException("El servidor devolvió una respuesta vacía.");
    }

    public async Task<IntegracionValidacionDto> ValidarAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IntegracionValidacionDto>(
            $"api/contabilidad/integracion/{companyId}/validacion", ct);
        return resultado ?? new IntegracionValidacionDto();
    }

    public async Task<IReadOnlyList<ServicioIntegracionLookupDto>> ListarServiciosAsync(long companyId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<ServicioIntegracionLookupDto>>(
            $"api/contabilidad/integracion/{companyId}/servicios", ct);
        return resultado ?? Array.Empty<ServicioIntegracionLookupDto>();
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasPosteablesAsync(long companyId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CuentaContableLookupDto>>(
            $"api/contabilidad/integracion/{companyId}/cuentas-posteables", ct);
        return resultado ?? Array.Empty<CuentaContableLookupDto>();
    }

    public async Task<IReadOnlyList<CategoriaServicioLookupDto>> ListarCategoriasAsync(CancellationToken ct = default)
    {
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CategoriaServicioLookupDto>>(
            "api/contabilidad/integracion/categorias", ct);
        return resultado ?? Array.Empty<CategoriaServicioLookupDto>();
    }

    // Catálogos contables existentes (pestaña Asientos).

    public async Task<IReadOnlyList<DiarioDto>> ListarDiariosAsync(CancellationToken ct = default)
    {
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<DiarioDto>>(
            "api/contabilidad/catalogos/diarios", ct);
        return resultado ?? Array.Empty<DiarioDto>();
    }

    public async Task<IReadOnlyList<TipoPartidaDto>> ListarTiposPartidaAsync(CancellationToken ct = default)
    {
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<TipoPartidaDto>>(
            "api/contabilidad/catalogos/tipos-partida", ct);
        return resultado ?? Array.Empty<TipoPartidaDto>();
    }
}
