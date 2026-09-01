using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Envío de correo de alto nivel: resuelve la configuración (conexión + área) y delega en el
/// transporte. Es lo que consume el código de negocio y el sender de Identity.
/// </summary>
public interface ICorreoNotificador
{
    /// <summary>
    /// Envía una notificación del área <paramref name="tipo"/> a sus destinatarios configurados
    /// (empresa actual). Si el envío está apagado o el área no tiene destinatarios, devuelve un
    /// resultado <c>Omitido</c> (no lanza). Para notificaciones de negocio con tenant en contexto.
    /// </summary>
    Task<CorreoEnvioResultado> NotificarAreaAsync(string tipo, string asunto, string htmlBody, CancellationToken ct = default);

    /// <summary>
    /// Envía a <b>destinatarios concretos</b> usando la conexión y el remitente del área
    /// <paramref name="tipo"/> de la empresa actual, y dejando en copia a los destinatarios fijos
    /// del área.
    /// <para>
    /// Existe para las notificaciones dirigidas a una persona por su papel en un documento —el
    /// aprobador al que le toca firmar, el comprador cuya orden fue rechazada—, que
    /// <see cref="NotificarAreaAsync"/> no cubre: aquel reparte a una lista fija de configuración.
    /// </para>
    /// <para>Sin destinatarios devuelve <c>Omitido</c>; nunca lanza por falta de configuración.</para>
    /// </summary>
    Task<CorreoEnvioResultado> NotificarDestinatariosAsync(
        string tipo, IReadOnlyCollection<string> destinatarios, string asunto, string htmlBody,
        CancellationToken ct = default);

    /// <summary>
    /// Envía un correo de SISTEMA (Identity: confirmación, reseteo) a una dirección concreta, usando
    /// la conexión de la empresa configurada en <c>Correo:CompanyIdSistema</c>. Devuelve <c>Omitido</c>
    /// si esa empresa no tiene conexión activa con API key y remitente.
    /// </summary>
    Task<CorreoEnvioResultado> EnviarSistemaAsync(string destinatario, string asunto, string htmlBody, CancellationToken ct = default);

    /// <summary>
    /// Envía un correo de PRUEBA a <paramref name="destinatario"/> usando la conexión <b>guardada</b>
    /// de la empresa actual (sin importar el interruptor global). Sirve para el botón "Probar conexión".
    /// </summary>
    Task<CorreoEnvioResultado> ProbarConexionAsync(string destinatario, CancellationToken ct = default);
}
