using System.Net;
using System.Text;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Services.Configuracion;

namespace SIAD.Services.Almacen;

/// <summary>
/// Arma el resumen HTML de las alertas de stock y lo envía al área ALMACÉN vía
/// <see cref="ICorreoNotificador"/>. La detección la hace <see cref="IArticulosService.GetAlertasStockAsync"/>;
/// aquí solo se formatea y se envía.
/// </summary>
public sealed class AlertasStockNotificador : IAlertasStockNotificador
{
    private readonly IArticulosService _articulos;
    private readonly ICorreoNotificador _correo;

    public AlertasStockNotificador(IArticulosService articulos, ICorreoNotificador correo)
    {
        _articulos = articulos;
        _correo = correo;
    }

    public async Task<CorreoEnvioResultado> EnviarResumenAsync(string motivo, CancellationToken ct = default)
    {
        var alertas = await _articulos.GetAlertasStockAsync(null, ct);
        if (alertas.Count == 0)
            return CorreoEnvioResultado.Skip("No hay alertas de stock.");

        var asunto = $"Alertas de stock — {DateTime.Now:dd/MM/yyyy}";
        var html = ConstruirHtml(alertas, string.IsNullOrWhiteSpace(motivo)
            ? "Resumen de artículos que requieren atención:" : motivo);
        return await _correo.NotificarAreaAsync(TipoNotificacion.Almacen, asunto, html, ct);
    }

    public async Task<CorreoEnvioResultado> EnviarCrucesAsync(
        IReadOnlyCollection<(int articuloId, int bodegaId)> pares, string documento, CancellationToken ct = default)
    {
        if (pares.Count == 0)
            return CorreoEnvioResultado.Skip("Sin cruces que reportar.");

        // Estado ACTUAL de las alertas; se filtra a los pares que cruzaron y siguen en alerta.
        var todas = await _articulos.GetAlertasStockAsync(null, ct);
        var set = pares.ToHashSet();
        var focos = todas.Where(a => set.Contains((a.Id, a.BodegaId))).ToList();
        if (focos.Count == 0)
            return CorreoEnvioResultado.Skip("Los artículos ya no están en alerta.");

        var asunto = $"Stock bajo — {documento}";
        var html = ConstruirHtml(focos, $"Estos artículos quedaron en alerta tras {WebUtility.HtmlEncode(documento)}:");
        return await _correo.NotificarAreaAsync(TipoNotificacion.Almacen, asunto, html, ct);
    }

    private static string ConstruirHtml(IReadOnlyList<AlertaStockDto> alertas, string encabezado)
    {
        var sb = new StringBuilder();
        sb.Append("<p style=\"font-family:Arial,sans-serif;font-size:14px;\">").Append(encabezado).Append("</p>");
        sb.Append("<table cellpadding=\"6\" cellspacing=\"0\" border=\"1\" ")
          .Append("style=\"border-collapse:collapse;font-family:Arial,sans-serif;font-size:13px;\">");
        sb.Append("<tr style=\"background:#f2f2f2;\">")
          .Append("<th align=\"left\">Severidad</th><th align=\"left\">Código</th><th align=\"left\">Descripción</th>")
          .Append("<th align=\"left\">Bodega</th><th align=\"right\">Existencia</th><th align=\"right\">Mínimo</th></tr>");

        foreach (var a in alertas)
        {
            sb.Append("<tr>")
              .Append("<td>").Append(SeveridadLabel(a.Severidad)).Append("</td>")
              .Append("<td>").Append(WebUtility.HtmlEncode(a.Codigo)).Append("</td>")
              .Append("<td>").Append(WebUtility.HtmlEncode(a.Descripcion)).Append("</td>")
              .Append("<td>").Append(WebUtility.HtmlEncode(a.BodegaNombre ?? string.Empty)).Append("</td>")
              .Append("<td align=\"right\">").Append(a.Existencia.ToString("N2")).Append("</td>")
              .Append("<td align=\"right\">").Append(a.ExistenciaMinima > 0 ? a.ExistenciaMinima.ToString("N2") : "—").Append("</td>")
              .Append("</tr>");
        }

        sb.Append("</table>");
        sb.Append("<p style=\"color:#888;font-family:Arial,sans-serif;font-size:11px;\">SIAD — Alertas de inventario</p>");
        return sb.ToString();
    }

    private static string SeveridadLabel(string severidad) => severidad switch
    {
        StockSeveridad.Negativa => "Existencia negativa",
        StockSeveridad.SinStock => "Sin stock",
        StockSeveridad.BajoMinimo => "Bajo mínimo",
        _ => severidad
    };
}
