using apc.Client.Services;
using SIAD.Core.DTOs.CondicionesLectura;

namespace apc.Client.Services.CondicionesLectura;

/// <summary>
/// Cliente HTTP del ABM de condiciones de lectura por empresa (app_lectores,
/// 2026-07-06). El catálogo administrado lo consume la app de lectores vía
/// GET /api/condiciones (apc.MobileApi).
/// </summary>
public sealed class CondicionesLecturaClient
{
    private readonly HttpClient http;

    public CondicionesLecturaClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<CondicionesLecturaCatalogoDto> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        var resultado = await http.GetFromJsonAsyncWithAuthCheck<CondicionesLecturaCatalogoDto>(
            $"api/ventas/condiciones-lectura/{companyId}", ct);
        return resultado ?? new CondicionesLecturaCatalogoDto();
    }

    public async Task<CondicionesLecturaCatalogoDto> GuardarAsync(long companyId,
        List<CondicionLecturaAdminDto> condiciones, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);
        ArgumentNullException.ThrowIfNull(condiciones);

        var response = await http.PostAsJsonAsyncWithAuthCheck(
            $"api/ventas/condiciones-lectura/{companyId}", condiciones, ct);
        var resultado = await response.ReadFromJsonAsyncWithAuthCheck<CondicionesLecturaCatalogoDto>(ct);
        return resultado ?? throw new InvalidOperationException("El servidor devolvió una respuesta vacía.");
    }
}
