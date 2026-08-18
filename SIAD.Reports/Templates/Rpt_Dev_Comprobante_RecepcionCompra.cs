using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante de una FACTURA DE COMPRA (recepción de mercadería del proveedor): el documento que
/// ingresa la compra al inventario —proveedor, No. de factura del proveedor (SAR/CAI), O/C origen si
/// la hay, bodega— y sus renglones valorizados con ISV. La orden de compra que la origina se imprime
/// aparte con <see cref="Rpt_Dev_Comprobante_OrdenCompra"/>.
/// </summary>
public sealed class Rpt_Dev_Comprobante_RecepcionCompra : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_RecepcionCompra(RecepcionCompraImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var doc = datos.Documento;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento FC-{doc.Numero:00000} - SIAD", datos.ImpresoPor));

        if (doc.Estado == EstadoRecepcionCompra.Anulada)
        {
            AplicarMarcaAgua("ANULADA");
        }
    }

    private float BuildDocumento(Band band, RecepcionCompraImpresionDto datos)
    {
        var doc = datos.Documento;

        var meta = new List<string>();
        if (doc.Fecha.HasValue)
        {
            meta.Add($"Fecha: {doc.Fecha.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        if (!string.IsNullOrWhiteSpace(doc.NumeroFacturaSar))
        {
            meta.Add($"Factura: {doc.NumeroFacturaSar}");
        }

        var y = BuildEncabezado(band, datos, "FACTURA DE COMPRA", doc.Numero.ToString("00000"), meta,
            doc.EstadoDescripcion.ToUpperInvariant());

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildDatos(band, doc, y);
        y = BuildTabla(band, doc, y);
        y = BuildTotales(band, datos, y);
        y = BuildObservacion(band, doc, y);
        y = BuildFirmas(band, y,
        [
            ("RECIBIDO POR", null),
            ("REVISADO POR", null),
            ("AUTORIZADO POR", null)
        ]);

        return y;
    }

    private static float BuildDatos(Band band, RecepcionCompraDto doc, float y)
    {
        var proveedor = string.IsNullOrWhiteSpace(doc.ProveedorNombre)
            ? doc.CodProveedor
            : $"{doc.CodProveedor} — {doc.ProveedorNombre}";
        AddLabel(band, "Proveedor:", 0f, y, 72f, 15f, 10f, bold: true);
        AddLabel(band, proveedor, 74f, y, 676f, 15f, 10f);
        y += 18f;

        AddLabel(band, "No. factura:", 0f, y, 82f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.NumeroFacturaSar) ? "-" : doc.NumeroFacturaSar!, 84f, y, 298f, 15f, 10f);
        AddLabel(band, "C.A.I.:", 400f, y, 50f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.Cai) ? "-" : doc.Cai!, 452f, y, 298f, 15f, 10f);
        y += 18f;

        var origen = doc.OrdenCompraNumero.HasValue ? $"O/C No. {doc.OrdenCompraNumero.Value:00000}" : "Compra directa";
        AddLabel(band, "Origen:", 0f, y, 82f, 15f, 10f, bold: true);
        AddLabel(band, origen, 84f, y, 298f, 15f, 10f);
        AddLabel(band, "Bodega:", 400f, y, 60f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.BodegaNombre) ? "-" : doc.BodegaNombre!, 462f, y, 288f, 15f, 10f);
        y += 18f;

        var pago = string.IsNullOrWhiteSpace(doc.TerminosPago)
            ? doc.CondicionPagoDescripcion
            : $"{doc.TerminosPago} ({doc.CondicionPagoDescripcion})";
        AddLabel(band, "Cond. de pago:", 0f, y, 100f, 15f, 10f, bold: true);
        AddLabel(band, pago, 102f, y, 280f, 15f, 10f);
        if (doc.FechaVencimiento.HasValue)
        {
            AddLabel(band, "Vencimiento:", 400f, y, 90f, 15f, 10f, bold: true);
            AddLabel(band, doc.FechaVencimiento.Value.ToString("dd/MM/yyyy", EsHn), 492f, y, 258f, 15f, 10f);
        }
        y += 18f;

        return y + 6f;
    }

    private static float BuildTabla(Band band, RecepcionCompraDto doc, float y)
    {
        float[] anchos = [80f, 300f, 80f, 100f, 80f, 110f];

        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Codigo", TextAlignment.MiddleLeft),
            ("Articulo", TextAlignment.MiddleLeft),
            ("Cantidad", TextAlignment.MiddleRight),
            ("Costo unit.", TextAlignment.MiddleRight),
            ("ISV", TextAlignment.MiddleRight),
            ("Total", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        if (doc.Detalles.Count == 0)
        {
            y = AddGridRow(band, y, 18f, anchos,
            [
                ("-", TextAlignment.TopLeft), ("Sin renglones", TextAlignment.TopLeft),
                (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight),
                (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight)
            ]);
        }
        else
        {
            foreach (var d in doc.Detalles)
            {
                var descripcion = WrapForWidth(FirstNonEmpty(d.Descripcion, d.CodigoArticulo) ?? "-", anchos[1]);
                var codigo = FirstNonEmpty(d.CodigoArticulo, d.CodigoUpc) ?? d.ArticuloId.ToString();

                y = AddGridRow(band, y, RowHeight(descripcion, string.Empty), anchos,
                [
                    (codigo, TextAlignment.TopLeft),
                    (descripcion, TextAlignment.TopLeft),
                    (Cantidad(d.Cantidad), TextAlignment.TopRight),
                    (Money(d.CostoUnitario), TextAlignment.TopRight),
                    (Money(d.Impuesto), TextAlignment.TopRight),
                    (Money(d.Total), TextAlignment.TopRight)
                ]);
            }
        }

        return y + 10f;
    }

    private static float BuildTotales(Band band, RecepcionCompraImpresionDto datos, float y)
    {
        var doc = datos.Documento;

        y = AddTotalLinea(band, y, "Subtotal:", Money(doc.SubTotal));
        if (doc.Descuento > 0m)
        {
            y = AddTotalLinea(band, y, $"Descuento ({Cantidad(doc.Descuento)}%):", string.Empty);
        }
        if (doc.OtrosGastos > 0m)
        {
            y = AddTotalLinea(band, y, "Otros gastos:", Money(doc.OtrosGastos));
        }
        if (doc.FleteSeguro > 0m)
        {
            y = AddTotalLinea(band, y, "Flete / seguro:", Money(doc.FleteSeguro));
        }
        y = AddTotalLinea(band, y, "ISV:", Money(doc.Impuesto));
        y = AddTotalLinea(band, y, "TOTAL:", Money(doc.Total), destacado: true);

        y += 6f;
        if (doc.Total > 0m)
        {
            y = AddBloqueEnmarcado(band, y, $"SON: {datos.MontoEnLetras} LEMPIRAS");
        }

        return y;
    }

    /// <summary>Una fila de la columna de totales (etiqueta + importe, alineados a la derecha).</summary>
    private static float AddTotalLinea(Band band, float y, string etiqueta, string valor, bool destacado = false)
    {
        var fontSize = destacado ? 11f : 9.5f;
        if (destacado)
        {
            AddLine(band, y, 470f, 280f, 1f);
            y += 3f;
        }

        AddLabel(band, etiqueta, 470f, y, 160f, 16f, fontSize, bold: destacado, align: TextAlignment.MiddleRight);
        AddLabel(band, valor, 632f, y, 118f, 16f, fontSize, bold: destacado, align: TextAlignment.MiddleRight);
        return y + 17f;
    }

    private static float BuildObservacion(Band band, RecepcionCompraDto doc, float y)
    {
        if (string.IsNullOrWhiteSpace(doc.Observaciones))
        {
            return y;
        }

        var texto = WrapForWidth(doc.Observaciones, 658f, 9.5f);
        var alto = 4f + CountLines(texto) * 15f;
        AddLabel(band, "Observaciones:", 0f, y, 100f, 15f, 9.5f, bold: true);
        AddLabel(band, texto, 102f, y, 648f, alto, 9.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
        return y + alto + 2f;
    }
}
