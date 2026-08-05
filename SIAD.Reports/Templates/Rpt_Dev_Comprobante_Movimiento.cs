using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante de un movimiento de almacén genérico (entrada / salida / ajuste de valor). El
/// movimiento POSTEA al kardex; el vale lo respalda con su concepto, la bodega, los renglones y
/// —cuando hay valor— el total y su expresión en letras. En la columna de costo muestra el costo
/// real con el que el motor valorizó cada renglón (en salida, el promedio ponderado vigente).
/// </summary>
public sealed class Rpt_Dev_Comprobante_Movimiento : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_Movimiento(MovimientoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var doc = datos.Documento;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento MOV-{doc.Numero:00000} - SIAD", datos.ImpresoPor));

        if (doc.Estado == EstadoMovimientoAlmacen.Anulado)
        {
            AplicarMarcaAgua("ANULADO");
        }
    }

    private float BuildDocumento(Band band, MovimientoImpresionDto datos)
    {
        var doc = datos.Documento;

        var meta = new List<string>();
        if (doc.Fecha.HasValue)
        {
            meta.Add($"Fecha: {doc.Fecha.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        if (!string.IsNullOrWhiteSpace(doc.DocumentoReferencia))
        {
            meta.Add($"Doc: {doc.DocumentoReferencia}");
        }

        var y = BuildEncabezado(band, datos, "COMPROBANTE DE MOVIMIENTO", doc.Numero.ToString("00000"), meta, EstadoTexto(doc.Estado));

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildDatos(band, doc, y);
        y = BuildTabla(band, doc, y);

        if (doc.Total > 0m)
        {
            y = AddBloqueEnmarcado(band, y, $"TOTAL: L {Money(doc.Total)}   -   SON: {datos.MontoEnLetras} LEMPIRAS");
        }

        y = BuildObservaciones(band, doc, y);
        y = BuildFirmas(band, y,
        [
            ("ELABORADO POR", doc.UsuarioCreacion),
            ("AUTORIZADO POR", null)
        ]);

        return y;
    }

    private static float BuildDatos(Band band, MovimientoAlmacenDto doc, float y)
    {
        AddLabel(band, "Concepto:", 0f, y, 70f, 15f, 10f, bold: true);
        AddLabel(band, ConceptoTexto(doc), 72f, y, 310f, 15f, 10f);
        AddLabel(band, "Bodega:", 400f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, doc.BodegaNombre ?? "-", 462f, y, 288f, 15f, 10f);
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

    private static float BuildTabla(Band band, MovimientoAlmacenDto doc, float y)
    {
        float[] anchos = [90f, 330f, 100f, 115f, 115f];

        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Codigo", TextAlignment.MiddleLeft),
            ("Articulo", TextAlignment.MiddleLeft),
            ("Cantidad", TextAlignment.MiddleRight),
            ("Costo unit.", TextAlignment.MiddleRight),
            ("Total", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        if (doc.Detalles.Count == 0)
        {
            y = AddGridRow(band, y, 18f, anchos,
            [
                ("-", TextAlignment.TopLeft), ("Sin renglones", TextAlignment.TopLeft),
                (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight)
            ]);
        }
        else
        {
            foreach (var d in doc.Detalles)
            {
                var descripcion = WrapForWidth(FirstNonEmpty(d.NombreArticulo, d.CodigoArticulo) ?? "-", anchos[1]);
                var costo = d.CostoReal ?? d.CostoUnitario;

                y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
                [
                    (string.IsNullOrWhiteSpace(d.CodigoArticulo) ? d.ArticuloId.ToString() : d.CodigoArticulo!, TextAlignment.TopLeft),
                    (descripcion, TextAlignment.TopLeft),
                    (Cantidad(d.Cantidad), TextAlignment.TopRight),
                    (costo.ToString("N4", EsHn), TextAlignment.TopRight),
                    (Money(d.Total), TextAlignment.TopRight)
                ]);
            }
        }

        y = AddGridRow(band, y, 19f, anchos,
        [
            (string.Empty, TextAlignment.MiddleLeft), (string.Empty, TextAlignment.MiddleLeft),
            (string.Empty, TextAlignment.MiddleRight), ("TOTAL", TextAlignment.MiddleRight),
            (Money(doc.Total), TextAlignment.MiddleRight)
        ], bold: true, total: true);

        return y + 12f;
    }

    private static float BuildObservaciones(Band band, MovimientoAlmacenDto doc, float y)
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

    private static string ConceptoTexto(MovimientoAlmacenDto doc)
    {
        var nombre = string.IsNullOrWhiteSpace(doc.TipoNombre) ? (doc.TipoCodigo ?? "-") : doc.TipoNombre!;
        return string.IsNullOrWhiteSpace(doc.Clase) ? nombre : $"{nombre} ({doc.Clase})";
    }

    private static string EstadoTexto(short estado) => estado switch
    {
        EstadoMovimientoAlmacen.Registrado => "REGISTRADO",
        EstadoMovimientoAlmacen.Anulado => "ANULADO",
        _ => "-"
    };
}
