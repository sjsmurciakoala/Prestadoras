using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Aprobaciones;

namespace apc.Client.Services.Configuracion;

/// <summary>
/// Cliente HTTP de la configuración de aprobación por niveles: interruptor por documento,
/// escalera de montos y aprobadores.
/// </summary>
public sealed class AprobacionesConfigClient
{
    private readonly HttpClient _http;
    public AprobacionesConfigClient(HttpClient http) => _http = http;

    public async Task<AprobacionConfiguracionDto> ObtenerAsync(string documento, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"api/configuracion/aprobaciones/{documento}", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<AprobacionConfiguracionDto>(ct)
               ?? new AprobacionConfiguracionDto { Documento = documento };
    }

    public async Task<AprobacionConfiguracionDto> GuardarControlAsync(
        string documento, short modo, bool permiteAutoaprobacion, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync(
            $"api/configuracion/aprobaciones/{documento}/control",
            new { Modo = modo, PermiteAutoaprobacion = permiteAutoaprobacion }, ct);

        await LanzarSiFallaAsync(r, "No se pudo guardar la configuración.", ct);

        return await r.ReadFromJsonAsyncWithAuthCheck<AprobacionConfiguracionDto>(ct)
               ?? new AprobacionConfiguracionDto { Documento = documento };
    }

    public async Task<AprobacionNivelConfigDto> GuardarNivelAsync(
        string documento, AprobacionNivelConfigDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync($"api/configuracion/aprobaciones/{documento}/niveles", dto, ct);
        await LanzarSiFallaAsync(r, "No se pudo guardar el nivel.", ct);

        return await r.ReadFromJsonAsyncWithAuthCheck<AprobacionNivelConfigDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }

    public async Task EliminarNivelAsync(int nivelId, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"api/configuracion/aprobaciones/niveles/{nivelId}", ct);
        await LanzarSiFallaAsync(r, "No se pudo eliminar el nivel.", ct);
    }

    public async Task<AprobacionAprobadorConfigDto> AgregarAprobadorAsync(
        int nivelId, AprobacionAprobadorConfigDto dto, CancellationToken ct = default)
    {
        var r = await _http.PostAsJsonAsync(
            $"api/configuracion/aprobaciones/niveles/{nivelId}/aprobadores", dto, ct);

        await LanzarSiFallaAsync(r, "No se pudo agregar el aprobador.", ct);

        return await r.ReadFromJsonAsyncWithAuthCheck<AprobacionAprobadorConfigDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }

    public async Task EliminarAprobadorAsync(int aprobadorId, CancellationToken ct = default)
    {
        var r = await _http.DeleteAsync($"api/configuracion/aprobaciones/aprobadores/{aprobadorId}", ct);
        await LanzarSiFallaAsync(r, "No se pudo quitar el aprobador.", ct);
    }

    public async Task<List<AprobadorUsuarioLookupDto>> ListarUsuariosAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/configuracion/aprobaciones/usuarios", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<AprobadorUsuarioLookupDto>>(ct) ?? new();
    }

    public async Task<List<string>> ListarRolesAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/configuracion/aprobaciones/roles", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<string>>(ct) ?? new();
    }

    public async Task<List<DocumentoAprobacionOpcion>> ListarDocumentosAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/configuracion/aprobaciones/documentos", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<DocumentoAprobacionOpcion>>(ct) ?? new();
    }

    private static async Task LanzarSiFallaAsync(HttpResponseMessage r, string porDefecto, CancellationToken ct)
    {
        if (r.IsSuccessStatusCode) return;

        throw new InvalidOperationException(
            await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct) ?? porDefecto);
    }

    /// <summary>Documento del selector. <c>Disponible</c> false = catalogado pero aún sin enganche.</summary>
    public sealed class DocumentoAprobacionOpcion
    {
        public string Codigo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool Disponible { get; set; }
    }
}
