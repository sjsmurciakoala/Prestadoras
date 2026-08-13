using System.Net.Http.Json;
using apc.Client.Services;
using SIAD.Core.DTOs.Configuracion;

namespace apc.Client.Services.Configuracion;

/// <summary>Cliente HTTP del mantenimiento de correo (conexión + áreas de notificación).</summary>
public sealed class CorreoConfigClient
{
    private readonly HttpClient _http;
    public CorreoConfigClient(HttpClient http) => _http = http;

    // conexión

    public async Task<ConexionCorreoDto> ObtenerConexionAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/configuracion/correo/conexion", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ConexionCorreoDto>(ct) ?? new ConexionCorreoDto();
    }

    public async Task<ConexionCorreoDto> GuardarConexionAsync(ConexionCorreoUpsertDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync("api/configuracion/correo/conexion", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<ConexionCorreoDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }

    // notificaciones

    public async Task<List<NotificacionCorreoDto>> ListarNotificacionesAsync(CancellationToken ct = default)
    {
        var r = await _http.GetAsync("api/configuracion/correo/notificaciones", ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<List<NotificacionCorreoDto>>(ct) ?? new();
    }

    public async Task<NotificacionCorreoDto> GuardarNotificacionAsync(NotificacionCorreoDto dto, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsync("api/configuracion/correo/notificaciones", dto, ct);
        return await r.ReadFromJsonAsyncWithAuthCheck<NotificacionCorreoDto>(ct)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }
}
