using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante (vale) de un traslado entre bodegas: la mercadería que SALE de la bodega de origen hacia
/// la de destino. Cubre los dos modos: <b>directo</b> (entra a destino de una, un solo acto) y <b>con
/// recepción</b> (queda en tránsito; el destino confirma en una o más recepciones parciales, cada una
/// con su propio comprobante <c>Rpt_Dev_Comprobante_Traslado_Recepcion</c>). En un traslado con recepción
/// la tabla muestra por renglón lo enviado, lo ya recibido y lo pendiente.
/// </summary>
public sealed class Rpt_Dev_Comprobante_Traslado : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_Traslado(TrasladoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var doc = datos.Documento;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento TRF-{doc.Numero:00000} - SIAD", datos.ImpresoPor));

        if (doc.Estado == EstadoMovimientoAlmacen.Anulado)
        {
            AplicarMarcaAgua("ANULADO");
        }
        else if (doc.Estado == EstadoMovimientoAlmacen.EnTransito)
        {
            AplicarMarcaAgua("EN TRANSITO");
        }
    }

    private float BuildDocumento(Band band, TrasladoImpresionDto datos)
    {
        var doc = datos.Documento;

        var meta = new List<string>();
        if (doc.Fecha.HasValue)
        {
            meta.Add($"Fecha: {doc.Fecha.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        meta.Add(doc.RequiereRecepcion ? "Con recepcion" : "Directo");
        if (!string.IsNullOrWhiteSpace(doc.DocumentoReferencia))
        {
            meta.Add($"Doc: {doc.DocumentoReferencia}");
        }

        var y = BuildEncabezado(band, datos, "VALE DE TRASLADO", doc.Numero.ToString("00000"), meta, EstadoTexto(doc.Estado));

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildBodegas(band, doc, y);
        y = BuildTabla(band, doc, y);

        if (doc.Total > 0m)
        {
            y = AddBloqueEnmarcado(band, y, $"VALOR TRASLADADO: L {Money(doc.Total)}   -   SON: {datos.MontoEnLetras} LEMPIRAS");
        }

        y = BuildObservaciones(band, doc, y);
        y = BuildFirmas(band, y,
        [
            ("ENTREGA (ORIGEN)", doc.UsuarioCreacion),
            ("AUTORIZADO POR", null),
            ("RECIBE (DESTINO)", doc.RecibidoPor)
        ]);

        return y;
    }

    private static float BuildBodegas(Band band, TrasladoDto doc, float y)
    {
        AddLabel(band, "De (origen):", 0f, y, 90f, 15f, 10f, bold: true);
        AddLabel(band, doc.BodegaOrigenNombre ?? "-", 92f, y, 290f, 15f, 10f);
        AddLabel(band, "A (destino):", 400f, y, 88f, 15f, 10f, bold: true);
        AddLabel(band, doc.BodegaDestinoNombre ?? "-", 490f, y, 260f, 15f, 10f);
        y += 18f;

        if (!string.IsNullOrWhiteSpace(doc.Motivo))
        {
            var texto = WrapForWidth(doc.Motivo, 668f, 10f);
            var alto = 4f + CountLines(texto) * 15f;
            AddLabel(band, "Motivo:", 0f, y, 60f, 15f, 10f, bold: true);
            AddLabel(band, texto, 62f, y, 688f, alto, 10f, align: TextAlignment.TopLeft, multiline: true);
            y += alto + 2f;
        }

        return y + 6f;
    }

    private static float BuildTabla(Band band, TrasladoDto doc, float y)
    {
        // Con recepción: se muestra el seguimiento (enviada / recibida / pendiente). Directo: tabla simple.
        return doc.RequiereRecepcion ? BuildTablaConRecepcion(band, doc, y) : BuildTablaDirecta(band, doc, y);
    }

    private static float BuildTablaDirecta(Band band, TrasladoDto doc, float y)
    {
        float[] anchos = [90f, 330f, 100f, 115f, 115f];
        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Codigo", TextAlignment.MiddleLeft), ("Articulo", TextAlignment.MiddleLeft),
            ("Cantidad", TextAlignment.MiddleRight), ("Costo unit.", TextAlignment.MiddleRight), ("Total", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        foreach (var d in doc.Detalles)
        {
            var descripcion = WrapForWidth(FirstNonEmpty(d.NombreArticulo, d.CodigoArticulo) ?? "-", anchos[1]);
            y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
            [
                (CodigoDe(d), TextAlignment.TopLeft), (descripcion, TextAlignment.TopLeft),
                (Cantidad(d.Cantidad), TextAlignment.TopRight),
                ((d.CostoReal ?? 0m).ToString("N4", EsHn), TextAlignment.TopRight),
                (Money(d.Total), TextAlignment.TopRight)
            ]);
        }

        return AddFilaTotal(band, y, anchos, doc.Total);
    }

    private static float BuildTablaConRecepcion(Band band, TrasladoDto doc, float y)
    {
        float[] anchos = [70f, 230f, 75f, 75f, 70f, 110f, 120f];
        y = AddGridRow(band, y, 26f, anchos,
        [
            ("Codigo", TextAlignment.MiddleLeft), ("Articulo", TextAlignment.MiddleLeft),
            ("Enviada", TextAlignment.MiddleRight), ("Recibida", TextAlignment.MiddleRight), ("Pend.", TextAlignment.MiddleRight),
            ("Costo unit.", TextAlignment.MiddleRight), ("Total", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        foreach (var d in doc.Detalles)
        {
            var descripcion = WrapForWidth(FirstNonEmpty(d.NombreArticulo, d.CodigoArticulo) ?? "-", anchos[1]);
            var pendiente = d.Cantidad - d.CantidadRecibida;
            y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
            [
                (CodigoDe(d), TextAlignment.TopLeft), (descripcion, TextAlignment.TopLeft),
                (Cantidad(d.Cantidad), TextAlignment.TopRight),
                (Cantidad(d.CantidadRecibida), TextAlignment.TopRight),
                (Cantidad(pendiente), TextAlignment.TopRight),
                ((d.CostoReal ?? 0m).ToString("N4", EsHn), TextAlignment.TopRight),
                (Money(d.Total), TextAlignment.TopRight)
            ]);
        }

        y = AddGridRow(band, y, 19f, anchos,
        [
            (string.Empty, TextAlignment.MiddleLeft), (string.Empty, TextAlignment.MiddleLeft),
            (string.Empty, TextAlignment.MiddleRight), (string.Empty, TextAlignment.MiddleRight), (string.Empty, TextAlignment.MiddleRight),
            ("TOTAL", TextAlignment.MiddleRight), (Money(doc.Total), TextAlignment.MiddleRight)
        ], bold: true, total: true);

        return y + 12f;
    }

    private static float AddFilaTotal(Band band, float y, float[] anchos, decimal total)
    {
        y = AddGridRow(band, y, 19f, anchos,
        [
            (string.Empty, TextAlignment.MiddleLeft), (string.Empty, TextAlignment.MiddleLeft),
            (string.Empty, TextAlignment.MiddleRight), ("TOTAL", TextAlignment.MiddleRight), (Money(total), TextAlignment.MiddleRight)
        ], bold: true, total: true);
        return y + 12f;
    }

    private static float BuildObservaciones(Band band, TrasladoDto doc, float y)
    {
        if (string.IsNullOrWhiteSpace(doc.Observaciones))
        {
            return y;
        }

        var texto = WrapForWidth(doc.Observaciones, 668f, 9.5f);
        var alto = 4f + CountLines(texto) * 15f;
        AddLabel(band, "Observaciones:", 0f, y, 100f, 15f, 9.5f, bold: true);
        AddLabel(band, texto, 102f, y, 648f, alto, 9.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
        return y + alto + 2f;
    }

    private static string CodigoDe(TrasladoDetalleDto d)
        => string.IsNullOrWhiteSpace(d.CodigoArticulo) ? d.ArticuloId.ToString() : d.CodigoArticulo!;

    private static string EstadoTexto(short estado) => estado switch
    {
        EstadoMovimientoAlmacen.EnTransito => "EN TRANSITO",
        EstadoMovimientoAlmacen.Recibido => "RECIBIDO",
        EstadoMovimientoAlmacen.Anulado => "ANULADO",
        _ => "REGISTRADO"
    };
}
