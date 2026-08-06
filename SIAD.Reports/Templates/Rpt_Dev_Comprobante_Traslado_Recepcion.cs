using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante de una RECEPCIÓN de traslado: la tanda que confirma la bodega de destino (puede ser
/// parcial). Es la contraparte del vale de envío (<c>Rpt_Dev_Comprobante_Traslado</c>): mientras aquél
/// respalda lo que sale de origen, éste respalda lo que entra a destino en un acto concreto. Un traslado
/// con recepción puede tener varias recepciones y por tanto varios de estos comprobantes.
/// </summary>
public sealed class Rpt_Dev_Comprobante_Traslado_Recepcion : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_Traslado_Recepcion(TrasladoRecepcionImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var traslado = datos.Traslado;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento TRF-{traslado.Numero:00000} · Recepcion - SIAD", datos.ImpresoPor));

        if (traslado.Estado == EstadoMovimientoAlmacen.Anulado)
        {
            AplicarMarcaAgua("ANULADO");
        }
    }

    private float BuildDocumento(Band band, TrasladoRecepcionImpresionDto datos)
    {
        var traslado = datos.Traslado;
        var rec = datos.Recepcion;

        var meta = new List<string>();
        meta.Add($"Recepcion: {rec.Fecha.ToString("dd/MM/yyyy", EsHn)}");
        if (!string.IsNullOrWhiteSpace(traslado.DocumentoReferencia))
        {
            meta.Add($"Doc: {traslado.DocumentoReferencia}");
        }

        var y = BuildEncabezado(band, datos, "RECEPCION DE TRASLADO", traslado.Numero.ToString("00000"), meta, EstadoTexto(traslado.Estado));

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildDatos(band, traslado, rec, y);
        y = BuildTabla(band, rec, y);

        var totalActo = rec.Lineas.Sum(l => l.Total);
        if (totalActo > 0m)
        {
            y = AddBloqueEnmarcado(band, y, $"VALOR RECIBIDO: L {Money(totalActo)}   -   SON: {datos.MontoEnLetras} LEMPIRAS");
        }

        if (!string.IsNullOrWhiteSpace(rec.Observaciones))
        {
            var texto = WrapForWidth(rec.Observaciones, 668f, 9.5f);
            var alto = 4f + CountLines(texto) * 15f;
            AddLabel(band, "Observaciones:", 0f, y, 100f, 15f, 9.5f, bold: true);
            AddLabel(band, texto, 102f, y, 648f, alto, 9.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
            y += alto + 2f;
        }

        y = BuildFirmas(band, y,
        [
            ("ENTREGA (ORIGEN)", null),
            ("RECIBE CONFORME (DESTINO)", rec.UsuarioCreacion)
        ]);

        return y;
    }

    private static float BuildDatos(Band band, TrasladoDto traslado, TrasladoRecepcionDto rec, float y)
    {
        AddLabel(band, "Traslado:", 0f, y, 70f, 15f, 10f, bold: true);
        AddLabel(band, $"TRF-{traslado.Numero:00000}", 72f, y, 200f, 15f, 10f);
        AddLabel(band, "Recibio:", 400f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(rec.UsuarioCreacion) ? "-" : rec.UsuarioCreacion!, 462f, y, 288f, 15f, 10f);
        y += 18f;

        AddLabel(band, "De (origen):", 0f, y, 90f, 15f, 10f, bold: true);
        AddLabel(band, traslado.BodegaOrigenNombre ?? "-", 92f, y, 290f, 15f, 10f);
        AddLabel(band, "A (destino):", 400f, y, 88f, 15f, 10f, bold: true);
        AddLabel(band, traslado.BodegaDestinoNombre ?? "-", 490f, y, 260f, 15f, 10f);
        y += 18f;

        if (!string.IsNullOrWhiteSpace(traslado.Motivo))
        {
            AddLabel(band, "Motivo:", 0f, y, 60f, 15f, 10f, bold: true);
            AddLabel(band, traslado.Motivo!, 62f, y, 688f, 15f, 10f);
            y += 18f;
        }

        return y + 6f;
    }

    private static float BuildTabla(Band band, TrasladoRecepcionDto rec, float y)
    {
        float[] anchos = [90f, 330f, 100f, 115f, 115f];
        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Codigo", TextAlignment.MiddleLeft), ("Articulo", TextAlignment.MiddleLeft),
            ("Recibida", TextAlignment.MiddleRight), ("Costo unit.", TextAlignment.MiddleRight), ("Total", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        if (rec.Lineas.Count == 0)
        {
            y = AddGridRow(band, y, 18f, anchos,
            [
                ("-", TextAlignment.TopLeft), ("Sin renglones", TextAlignment.TopLeft),
                (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight)
            ]);
        }
        else
        {
            foreach (var l in rec.Lineas)
            {
                var descripcion = WrapForWidth(FirstNonEmpty(l.NombreArticulo, l.CodigoArticulo) ?? "-", anchos[1]);
                var codigo = string.IsNullOrWhiteSpace(l.CodigoArticulo) ? l.ArticuloId.ToString() : l.CodigoArticulo!;
                y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
                [
                    (codigo, TextAlignment.TopLeft), (descripcion, TextAlignment.TopLeft),
                    (Cantidad(l.Cantidad), TextAlignment.TopRight),
                    (l.CostoReal.ToString("N4", EsHn), TextAlignment.TopRight),
                    (Money(l.Total), TextAlignment.TopRight)
                ]);
            }
        }

        y = AddGridRow(band, y, 19f, anchos,
        [
            (string.Empty, TextAlignment.MiddleLeft), (string.Empty, TextAlignment.MiddleLeft),
            (string.Empty, TextAlignment.MiddleRight), ("TOTAL", TextAlignment.MiddleRight),
            (Money(rec.Lineas.Sum(l => l.Total)), TextAlignment.MiddleRight)
        ], bold: true, total: true);

        return y + 12f;
    }

    private static string EstadoTexto(short estadoTraslado) => estadoTraslado switch
    {
        EstadoMovimientoAlmacen.EnTransito => "RECEPCION PARCIAL",
        EstadoMovimientoAlmacen.Recibido => "TRASLADO COMPLETO",
        EstadoMovimientoAlmacen.Anulado => "ANULADO",
        _ => "-"
    };
}
