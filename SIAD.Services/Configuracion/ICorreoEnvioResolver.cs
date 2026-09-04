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

    /// <summary>
    /// Resuelve el TRANSPORTE (API key descifrada + remitente efectivo) de una empresa concreta, sin
    /// destinatarios de configuración — para correos de sistema (Identity) que se envían a una
    /// dirección externa. Lectura <b>cross-tenant</b> (por eso recibe el <paramref name="companyId"/>):
    /// los flujos de Identity ocurren sin sesión. Devuelve <c>null</c> si la conexión está inactiva,
    /// sin API key descifrable, sin remitente, o si el área <paramref name="tipoRemitente"/> existe y
    /// está inactiva (apagado explícito de los correos de ese tipo).
    /// </summary>
    Task<EnvioCorreoResueltoDto?> ResolverTransporteAsync(long companyId, string tipoRemitente, CancellationToken ct = default);

    /// <summary>
    /// Resuelve el transporte de la <b>empresa actual</b> para una PRUEBA de conexión: descifra la API
    /// key y toma el remitente por defecto, <b>sin</b> la compuerta del interruptor global
    /// <c>activo</c> (probar es una acción explícita, aunque el envío esté apagado). Devuelve
    /// <c>null</c> si no hay conexión, no hay API key descifrable, o falta el remitente por defecto.
    /// </summary>
    Task<EnvioCorreoResueltoDto?> ResolverPruebaAsync(CancellationToken ct = default);
}
