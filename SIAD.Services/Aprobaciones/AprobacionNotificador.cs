using System.Net;
using System.Text;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Services.Configuracion;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Implementación de <see cref="IAprobacionNotificador"/>: arma el HTML del aviso y lo manda por
/// <see cref="ICorreoNotificador"/> con el remitente del área ALMACÉN.
/// <para>
/// <b>Traga sus errores a propósito.</b> Estos avisos se disparan después de que la firma ya está
/// confirmada en la base: si SendGrid está caído o la empresa no tiene correo configurado, el
/// documento igual avanzó y nadie debe ver un error por eso. Lo que se pierde es el aviso, no el
/// trabajo — y la bandeja "Mis aprobaciones" sigue mostrando lo pendiente.
/// </para>
/// </summary>
public sealed class AprobacionNotificador : IAprobacionNotificador
{
    private readonly IAprobacionService _aprobacion;
    private readonly ICorreoNotificador _correo;

    public AprobacionNotificador(IAprobacionService aprobacion, ICorreoNotificador correo)
    {
        _aprobacion = aprobacion;
        _correo = correo;
    }

    public async Task<CorreoEnvioResultado> NotificarPendienteOrdenCompraAsync(
        int ordenCompraId, string numero, string proveedor, decimal total, string nivel,
        CancellationToken ct = default)
    {
        try
        {
            var destinatarios = await _aprobacion.CorreosNivelPendienteAsync(
                DocumentosAprobacion.OrdenCompra, ordenCompraId, ct);

            if (destinatarios.Count == 0)
            {
                // Pasa cuando el nivel autoriza por ROL: los miembros viven en Identity y no se
                // resuelven desde aquí. El área queda enterada por su propia copia.
                return CorreoEnvioResultado.Skip("El nivel pendiente no tiene aprobadores con correo.");
            }

            var asunto = $"Orden de compra {numero} espera su aprobación";
            var html = ConstruirHtmlPendiente(numero, proveedor, total, nivel);

            return await _correo.NotificarDestinatariosAsync(
                TipoNotificacion.Almacen, destinatarios, asunto, html, ct);
        }
        catch (Exception ex)
        {
            return CorreoEnvioResultado.Skip($"No se pudo enviar el aviso: {ex.Message}");
        }
    }

    public async Task<CorreoEnvioResultado> NotificarResueltaOrdenCompraAsync(
        string? creador, string numero, string proveedor, decimal total, string desenlace,
        string? motivo, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(creador))
                return CorreoEnvioResultado.Skip("La orden no registra quién la creó.");

            var asunto = $"Orden de compra {numero}: {desenlace}";
            var html = ConstruirHtmlResuelta(numero, proveedor, total, desenlace, motivo);

            return await _correo.NotificarDestinatariosAsync(
                TipoNotificacion.Almacen, [creador.Trim()], asunto, html, ct);
        }
        catch (Exception ex)
        {
            return CorreoEnvioResultado.Skip($"No se pudo enviar el aviso: {ex.Message}");
        }
    }

    private static string ConstruirHtmlPendiente(string numero, string proveedor, decimal total, string nivel)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#333;\">");
        sb.Append("<p>Tiene una orden de compra esperando su firma:</p>");

        AbrirTabla(sb);
        Fila(sb, "Orden", numero);
        Fila(sb, "Proveedor", proveedor);
        Fila(sb, "Total", total.ToString("N2"));
        Fila(sb, "Nivel que le toca", nivel);
        sb.Append("</table>");

        sb.Append("<p style=\"margin-top:14px;\">Puede firmarla desde <strong>Mis aprobaciones</strong> en el portal.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string ConstruirHtmlResuelta(
        string numero, string proveedor, decimal total, string desenlace, string? motivo)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#333;\">");
        sb.Append("<p>Su orden de compra fue <strong>")
          .Append(WebUtility.HtmlEncode(desenlace))
          .Append("</strong>.</p>");

        AbrirTabla(sb);
        Fila(sb, "Orden", numero);
        Fila(sb, "Proveedor", proveedor);
        Fila(sb, "Total", total.ToString("N2"));
        if (!string.IsNullOrWhiteSpace(motivo)) Fila(sb, "Motivo", motivo!);
        sb.Append("</table>");

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AbrirTabla(StringBuilder sb)
        => sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" border=\"1\" ")
             .Append("style=\"border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px;\">");

    private static void Fila(StringBuilder sb, string etiqueta, string valor)
        => sb.Append("<tr><td style=\"background:#f5f5f5;font-weight:bold;\">")
             .Append(WebUtility.HtmlEncode(etiqueta))
             .Append("</td><td>")
             .Append(WebUtility.HtmlEncode(valor))
             .Append("</td></tr>");
}
