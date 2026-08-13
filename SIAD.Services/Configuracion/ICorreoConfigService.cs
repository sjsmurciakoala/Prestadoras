using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Mantenimiento de la configuración de correo por empresa: la conexión (SendGrid) y las áreas
/// de notificación con sus destinatarios. <b>Ningún método expone la API key</b> — el descifrado
/// vive solo en <see cref="ICorreoEnvioResolver"/>, que consume el sender, no la pantalla.
/// </summary>
public interface ICorreoConfigService
{
    /// <summary>Conexión de la empresa actual, sin la API key. Si no hay fila, devuelve valores por defecto.</summary>
    Task<ConexionCorreoDto> ObtenerConexionAsync(CancellationToken ct = default);

    /// <summary>
    /// Guarda la conexión (crea la fila si no existe). Cifra <c>NuevaApiKey</c> si viene con valor;
    /// si viene vacía, conserva la key existente. Devuelve la conexión sin la key.
    /// </summary>
    Task<ConexionCorreoDto> GuardarConexionAsync(ConexionCorreoUpsertDto dto, string user, CancellationToken ct = default);

    /// <summary>Áreas de notificación de la empresa actual, con sus destinatarios.</summary>
    Task<IReadOnlyList<NotificacionCorreoDto>> ListarNotificacionesAsync(CancellationToken ct = default);

    /// <summary>
    /// Upsert de un área por su tipo: crea o actualiza el remitente y <b>reemplaza</b> la lista de
    /// destinatarios en la misma operación. Devuelve el área guardada.
    /// </summary>
    Task<NotificacionCorreoDto> GuardarNotificacionAsync(NotificacionCorreoDto dto, string user, CancellationToken ct = default);
}
