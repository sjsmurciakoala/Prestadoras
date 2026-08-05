using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante (vale de salida) de un descargo. El descargo es la ENTREGA real: la mercadería salió del
/// kardex a su costo promedio, por eso el vale muestra el costo unitario, el total y su expresión en
/// letras. Sirve de respaldo firmado de la salida (entregó / recibió). Contra una requisición muestra su
/// número; directo, el motivo.
/// </summary>
public sealed class Rpt_Dev_Comprobante_Descargo : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_Descargo(DescargoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var doc = datos.Documento;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento DESC-{doc.Numero:00000} - SIAD", datos.ImpresoPor));

        if (doc.Estado == EstadoDescargoHdr.Anulado)
        {
            AplicarMarcaAgua("ANULADO");
        }
    }

    private float BuildDocumento(Band band, DescargoImpresionDto datos)
    {
        var doc = datos.Documento;

        var meta = new List<string>();
        if (doc.Fecha.HasValue)
        {
            meta.Add($"Fecha: {doc.Fecha.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        if (doc.RequisicionNumero.HasValue)
        {
            meta.Add($"Requisicion: {doc.RequisicionNumero.Value:00000}");
        }

        var y = BuildEncabezado(band, datos, "VALE DE SALIDA", doc.Numero.ToString("00000"), meta, EstadoTexto(doc.Estado));

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
            ("ENTREGADO POR", doc.EntregadoPor),
            ("RECIBIDO POR", doc.RecibidoPor)
        ]);

        return y;
    }

    private static float BuildDatos(Band band, DescargoDocumentoDto doc, float y)
    {
        AddLabel(band, "Bodega:", 0f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, doc.BodegaNombre ?? "-", 62f, y, 320f, 15f, 10f);
        AddLabel(band, "Departamento:", 400f, y, 100f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.Departamento) ? "-" : doc.Departamento!, 502f, y, 248f, 15f, 10f);
        y += 18f;

        AddLabel(band, "Entrego:", 0f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.EntregadoPor) ? "-" : doc.EntregadoPor!, 62f, y, 320f, 15f, 10f);
        AddLabel(band, "Recibio:", 400f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.RecibidoPor) ? "-" : doc.RecibidoPor!, 462f, y, 288f, 15f, 10f);
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

    private static float BuildTabla(Band band, DescargoDocumentoDto doc, float y)
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

                y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
                [
                    (string.IsNullOrWhiteSpace(d.CodigoArticulo) ? d.ArticuloId.ToString() : d.CodigoArticulo!, TextAlignment.TopLeft),
                    (descripcion, TextAlignment.TopLeft),
                    (Cantidad(d.Cantidad), TextAlignment.TopRight),
                    (d.PrecioUnitario.ToString("N4", EsHn), TextAlignment.TopRight),
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

    private static float BuildObservaciones(Band band, DescargoDocumentoDto doc, float y)
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

    private static string EstadoTexto(short estado) => estado switch
    {
        EstadoDescargoHdr.Registrado => "REGISTRADO",
        EstadoDescargoHdr.Anulado => "ANULADO",
        _ => "-"
    };
}
