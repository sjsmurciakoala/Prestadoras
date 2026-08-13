using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Resuelve el envío de un tipo de notificación para el sender: descifra la API key y calcula el
/// remitente efectivo y los destinatarios activos. Interfaz <b>aparte</b> de
/// <see cref="ICorreoConfigService"/> a propósito: el controller/pantalla depende solo de ese, que
/// nunca expone la clave; el descifrado queda restringido a quien envía correo.
/// </summary>
public interface ICorreoEnvioResolver
{
    /// <summary>
    /// Devuelve los datos de envío para <paramref name="tipo"/>, o <c>null</c> si el envío global
    /// está apagado, no hay conexión, o el área no existe o está inactiva.
    /// </summary>
    Task<EnvioCorreoResueltoDto?> ResolverEnvioAsync(string tipo, CancellationToken ct = default);
}
