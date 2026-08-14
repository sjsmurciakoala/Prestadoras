using Microsoft.Extensions.Configuration;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Une la resolución de configuración (<see cref="ICorreoEnvioResolver"/>) con el transporte
/// (<see cref="ISendGridCorreoTransport"/>). Los correos de sistema salen de la empresa fija
/// <c>Correo:CompanyIdSistema</c> (los flujos de Identity no tienen tenant en contexto).
/// </summary>
public sealed class CorreoNotificador : ICorreoNotificador
{
    private readonly ICorreoEnvioResolver _resolver;
    private readonly ISendGridCorreoTransport _transport;
    private readonly long _companyIdSistema;

    public CorreoNotificador(ICorreoEnvioResolver resolver, ISendGridCorreoTransport transport, IConfiguration config)
    {
        _resolver = resolver;
        _transport = transport;
        _companyIdSistema = config.GetValue<long?>("Correo:CompanyIdSistema") ?? 0;
    }

    public async Task<CorreoEnvioResultado> NotificarAreaAsync(string tipo, string asunto, string htmlBody, CancellationToken ct = default)
    {
        var envio = await _resolver.ResolverEnvioAsync(tipo, ct);
        if (envio is null)
            return CorreoEnvioResultado.Skip("Envío apagado o área no configurada/activa.");
        if (string.IsNullOrEmpty(envio.ApiKey))
            return CorreoEnvioResultado.Skip("La conexión no tiene API key.");
        if (envio.Para.Count == 0 && envio.ConCopia.Count == 0)
            return CorreoEnvioResultado.Skip("El área no tiene destinatarios.");

        return await _transport.EnviarAsync(new CorreoMensaje
        {
            ApiKey = envio.ApiKey!,
            FromEmail = envio.RemitenteEmail,
            FromName = envio.RemitenteNombre,
            Para = envio.Para,
            ConCopia = envio.ConCopia,
            Asunto = asunto,
            HtmlBody = htmlBody
        }, ct);
    }

    public async Task<CorreoEnvioResultado> EnviarSistemaAsync(string destinatario, string asunto, string htmlBody, CancellationToken ct = default)
    {
        if (_companyIdSistema <= 0)
            return CorreoEnvioResultado.Skip("Correo:CompanyIdSistema no configurado.");
        if (string.IsNullOrWhiteSpace(destinatario))
            return CorreoEnvioResultado.Skip("Sin destinatario.");

        var transporte = await _resolver.ResolverTransporteAsync(_companyIdSistema, TipoNotificacion.Sistema, ct);
        if (transporte is null || string.IsNullOrEmpty(transporte.ApiKey))
            return CorreoEnvioResultado.Skip("La empresa de sistema no tiene conexión activa con API key.");

        return await _transport.EnviarAsync(new CorreoMensaje
        {
            ApiKey = transporte.ApiKey!,
            FromEmail = transporte.RemitenteEmail,
            FromName = transporte.RemitenteNombre,
            Para = [destinatario.Trim()],
            Asunto = asunto,
            HtmlBody = htmlBody
        }, ct);
    }

    public async Task<CorreoEnvioResultado> ProbarConexionAsync(string destinatario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
            return CorreoEnvioResultado.Skip("Indica un correo de prueba.");

        var transporte = await _resolver.ResolverPruebaAsync(ct);
        if (transporte is null || string.IsNullOrEmpty(transporte.ApiKey))
            return CorreoEnvioResultado.Skip("Guarda una API key y un remitente por defecto antes de probar.");

        return await _transport.EnviarAsync(new CorreoMensaje
        {
            ApiKey = transporte.ApiKey!,
            FromEmail = transporte.RemitenteEmail,
            FromName = transporte.RemitenteNombre,
            Para = [destinatario.Trim()],
            Asunto = "Prueba de conexión — SIAD",
            HtmlBody = "<p>Este es un correo de <b>prueba</b> de la configuración de SendGrid en SIAD. " +
                       "Si lo recibiste, la conexión funciona correctamente.</p>"
        }, ct);
    }
}
