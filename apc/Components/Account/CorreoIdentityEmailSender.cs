using apc.Data;
using Microsoft.AspNetCore.Identity;
using SIAD.Services.Configuracion;

namespace apc.Components.Account;

/// <summary>
/// Envío real de los correos de Identity (confirmación de cuenta y reseteo de contraseña) por
/// SendGrid, en reemplazo de <see cref="IdentityNoOpEmailSender"/>. Delega en
/// <see cref="ICorreoNotificador.EnviarSistemaAsync"/>, que usa la conexión de la empresa
/// <c>Correo:CompanyIdSistema</c> (estos flujos ocurren sin sesión).
/// <para>
/// Si el envío se omite por configuración (sin conexión/API key), no lanza: el flujo de Identity
/// continúa igual que con el No-Op, sin filtrar si la cuenta existe.
/// </para>
/// </summary>
internal sealed class CorreoIdentityEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ICorreoNotificador _notificador;

    public CorreoIdentityEmailSender(ICorreoNotificador notificador) => _notificador = notificador;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        _notificador.EnviarSistemaAsync(email, "Confirma tu correo",
            $"Confirma tu cuenta <a href='{confirmationLink}'>haciendo clic aquí</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        _notificador.EnviarSistemaAsync(email, "Restablece tu contraseña",
            $"Restablece tu contraseña <a href='{resetLink}'>haciendo clic aquí</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        _notificador.EnviarSistemaAsync(email, "Restablece tu contraseña",
            $"Tu código para restablecer la contraseña es: {resetCode}");
}
