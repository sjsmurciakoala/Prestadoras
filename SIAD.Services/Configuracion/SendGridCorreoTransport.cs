using System.Net.Http.Headers;
using System.Net.Http.Json;
using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Envía correo por la API v3 de SendGrid (<c>POST /v3/mail/send</c>, respuesta 202). El
/// <c>HttpClient</c> tipado trae la BaseAddress; la API key va por mensaje en el header
/// <c>Authorization: Bearer</c> (cada empresa tiene la suya, descifrada en el momento).
/// </summary>
public sealed class SendGridCorreoTransport : ISendGridCorreoTransport
{
    private readonly HttpClient _http;

    public SendGridCorreoTransport(HttpClient http) => _http = http;

    public async Task<CorreoEnvioResultado> EnviarAsync(CorreoMensaje mensaje, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (string.IsNullOrWhiteSpace(mensaje.ApiKey))
            return CorreoEnvioResultado.Skip("Sin API key.");

        var to = mensaje.Para.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new { email = x.Trim() }).ToList();
        var cc = mensaje.ConCopia.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new { email = x.Trim() }).ToList();
        if (to.Count == 0)
            return CorreoEnvioResultado.Skip("Sin destinatarios.");
        if (string.IsNullOrWhiteSpace(mensaje.FromEmail))
            return CorreoEnvioResultado.Skip("Sin remitente.");

        var personalization = new Dictionary<string, object> { ["to"] = to };
        if (cc.Count > 0) personalization["cc"] = cc;

        var from = new Dictionary<string, object> { ["email"] = mensaje.FromEmail!.Trim() };
        if (!string.IsNullOrWhiteSpace(mensaje.FromName)) from["name"] = mensaje.FromName!.Trim();

        var payload = new
        {
            personalizations = new[] { personalization },
            from,
            subject = mensaje.Asunto ?? string.Empty,
            content = new[] { new { type = "text/html", value = mensaje.HtmlBody ?? string.Empty } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v3/mail/send")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mensaje.ApiKey);

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return CorreoEnvioResultado.Ok((int)resp.StatusCode);

            var body = await resp.Content.ReadAsStringAsync(ct);
            return CorreoEnvioResultado.Fallo((int)resp.StatusCode, Truncar(body));
        }
        catch (Exception ex)
        {
            return CorreoEnvioResultado.Fallo(null, ex.Message);
        }
    }

    private static string Truncar(string? s)
    {
        s ??= string.Empty;
        return s.Length > 500 ? s[..500] : s;
    }
}
