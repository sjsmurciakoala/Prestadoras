using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante (vale) de una requisición de materiales. La requisición es la SOLICITUD: el vale sirve
/// para firmarla y darle seguimiento al despacho; muestra por renglón lo solicitado, lo ya despachado
/// y lo pendiente. No refleja movimiento de inventario (eso lo hace el descargo).
/// </summary>
public sealed class Rpt_Dev_Comprobante_Requisicion : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_Requisicion(RequisicionImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var doc = datos.Documento;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento REQ-{doc.Numero:00000} - SIAD", datos.ImpresoPor));

        if (doc.Estado == EstadoRequisicionHdr.Anulada)
        {
            AplicarMarcaAgua("ANULADA");
        }
        else if (doc.Estado == EstadoRequisicionHdr.Rechazada)
        {
            AplicarMarcaAgua("RECHAZADA");
        }
    }

    private float BuildDocumento(Band band, RequisicionImpresionDto datos)
    {
        var doc = datos.Documento;

        var meta = new List<string>();
        if (doc.Fecha.HasValue)
        {
            meta.Add($"Fecha: {doc.Fecha.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        if (doc.FechaRequerida.HasValue)
        {
            meta.Add($"Requerida: {doc.FechaRequerida.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        var y = BuildEncabezado(band, datos, "REQUISICION DE MATERIALES", doc.Numero.ToString("00000"), meta, EstadoTexto(doc.Estado));

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildDatos(band, doc, y);
        y = BuildTabla(band, doc, y);
        y = BuildValorYObservacion(band, datos, y);
        y = BuildFirmas(band, y,
        [
            ("SOLICITADO POR", doc.Solicitante),
            ("APROBADO POR", doc.AprobadoPor),
            ("RECIBIDO POR", null)
        ]);

        return y;
    }

    private static float BuildDatos(Band band, RequisicionDocumentoDto doc, float y)
    {
        AddLabel(band, "Bodega:", 0f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, doc.BodegaNombre ?? "-", 62f, y, 320f, 15f, 10f);
        AddLabel(band, "Departamento:", 400f, y, 100f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.Departamento) ? "-" : doc.Departamento!, 502f, y, 248f, 15f, 10f);
        y += 18f;

        var solicitante = string.IsNullOrWhiteSpace(doc.CargoSolicitante)
            ? doc.Solicitante ?? "-"
            : $"{doc.Solicitante} ({doc.CargoSolicitante})";
        AddLabel(band, "Solicitante:", 0f, y, 78f, 15f, 10f, bold: true);
        AddLabel(band, solicitante, 80f, y, 670f, 15f, 10f);
        y += 18f;

        if (!string.IsNullOrWhiteSpace(doc.Aplicacion))
        {
            var texto = WrapForWidth(doc.Aplicacion, 668f, 10f);
            var alto = 4f + CountLines(texto) * 15f;
            AddLabel(band, "Aplicacion:", 0f, y, 80f, 15f, 10f, bold: true);
            AddLabel(band, texto, 82f, y, 668f, alto, 10f, align: TextAlignment.TopLeft, multiline: true);
            y += alto + 2f;
        }

        return y + 6f;
    }

    private static float BuildTabla(Band band, RequisicionDocumentoDto doc, float y)
    {
        float[] anchos = [90f, 330f, 100f, 115f, 115f];

        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Codigo", TextAlignment.MiddleLeft),
            ("Articulo", TextAlignment.MiddleLeft),
            ("Solicitada", TextAlignment.MiddleRight),
            ("Despachada", TextAlignment.MiddleRight),
            ("Pendiente", TextAlignment.MiddleRight)
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
                var descripcion = WrapForWidth(FirstNonEmpty(d.NombreArticulo, d.Descripcion, d.CodigoArticulo) ?? "-", anchos[1]);
                var pendiente = d.Cantidad - d.CantidadDespachada;

                y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
                [
                    (string.IsNullOrWhiteSpace(d.CodigoArticulo) ? d.ArticuloId.ToString() : d.CodigoArticulo!, TextAlignment.TopLeft),
                    (descripcion, TextAlignment.TopLeft),
                    (Cantidad(d.Cantidad), TextAlignment.TopRight),
                    (Cantidad(d.CantidadDespachada), TextAlignment.TopRight),
                    (Cantidad(pendiente), TextAlignment.TopRight)
                ]);
            }
        }

        return y + 12f;
    }

    private static float BuildValorYObservacion(Band band, RequisicionImpresionDto datos, float y)
    {
        var doc = datos.Documento;

        if (doc.Total > 0m)
        {
            y = AddBloqueEnmarcado(band, y, $"Valor estimado: L {Money(doc.Total)}   -   SON: {datos.MontoEnLetras} LEMPIRAS");
        }

        if (!string.IsNullOrWhiteSpace(doc.Observacion))
        {
            var texto = WrapForWidth(doc.Observacion, 668f, 9.5f);
            var alto = 4f + CountLines(texto) * 15f;
            AddLabel(band, "Observacion:", 0f, y, 90f, 15f, 9.5f, bold: true);
            AddLabel(band, texto, 92f, y, 658f, alto, 9.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
            y += alto + 2f;
        }

        return y;
    }

    private static string EstadoTexto(short estado) => estado switch
    {
        EstadoRequisicionHdr.Borrador => "BORRADOR",
        EstadoRequisicionHdr.EnRevision => "EN REVISION",
        EstadoRequisicionHdr.Aprobada => "APROBADA",
        EstadoRequisicionHdr.DespachadaParcial => "DESPACHO PARCIAL",
        EstadoRequisicionHdr.DespachadaTotal => "DESPACHO TOTAL",
        EstadoRequisicionHdr.CerradaEnOC => "CERRADA EN O/C",
        EstadoRequisicionHdr.Rechazada => "RECHAZADA",
        EstadoRequisicionHdr.Anulada => "ANULADA",
        _ => "-"
    };
}
