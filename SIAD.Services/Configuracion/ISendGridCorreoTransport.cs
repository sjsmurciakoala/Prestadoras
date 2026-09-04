using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Transporte de correo por SendGrid (API v3 <c>/mail/send</c>). Pieza de bajo nivel: recibe un
/// mensaje ya resuelto (con la API key descifrada, remitente y destinatarios) y lo envía. No toca la
/// BD ni conoce el tenant. Se mockea en tests.
/// </summary>
public interface ISendGridCorreoTransport
{
    Task<CorreoEnvioResultado> EnviarAsync(CorreoMensaje mensaje, CancellationToken ct = default);
}
