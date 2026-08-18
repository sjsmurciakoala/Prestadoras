using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante de una ORDEN DE COMPRA a un proveedor: el documento con que se formaliza el pedido
/// (proveedor, términos de pago, destino/uso y fecha de entrega pactada) y sus renglones valorizados
/// con ISV. No mueve inventario; se imprime para autorizar y enviar al proveedor. La factura de compra
/// (recepción) se imprime aparte con <see cref="Rpt_Dev_Comprobante_RecepcionCompra"/>.
/// </summary>
public sealed class Rpt_Dev_Comprobante_OrdenCompra : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_OrdenCompra(OrdenCompraImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var doc = datos.Documento;

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Documento O/C-{doc.Numero:00000} - SIAD", datos.ImpresoPor));

        // La orden en borrador todavía no está autorizada: se marca para que no se confunda con una
        // orden en firme ya enviada al proveedor. La anulada, con la marca roja habitual.
        if (doc.Estado == EstadoOrdenCompra.Anulada)
        {
            AplicarMarcaAgua("ANULADA");
        }
        else if (doc.Estado == EstadoOrdenCompra.Borrador)
        {
            AplicarMarcaAgua("BORRADOR");
        }
    }

    private float BuildDocumento(Band band, OrdenCompraImpresionDto datos)
    {
        var doc = datos.Documento;

        var meta = new List<string>();
        if (doc.Fecha.HasValue)
        {
            meta.Add($"Fecha: {doc.Fecha.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        if (doc.FechaEntregaPactada.HasValue)
        {
            meta.Add($"Entrega: {doc.FechaEntregaPactada.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        var y = BuildEncabezado(band, datos, "ORDEN DE COMPRA", doc.Numero.ToString("00000"), meta,
            doc.EstadoDescripcion.ToUpperInvariant());

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildDatos(band, doc, y);
        y = BuildTabla(band, doc, y);
        y = BuildTotales(band, datos, y);
        y = BuildObservacion(band, doc, y);
        y = BuildFirmas(band, y,
        [
            ("ELABORADO POR", null),
            ("APROBADO POR", doc.AprobadoPor),
            ("RECIBIDO POR (PROVEEDOR)", null)
        ]);

        return y;
    }

    private static float BuildDatos(Band band, OrdenCompraDto doc, float y)
    {
        var proveedor = string.IsNullOrWhiteSpace(doc.ProveedorNombre)
            ? doc.CodProveedor
            : $"{doc.CodProveedor} — {doc.ProveedorNombre}";
        AddLabel(band, "Proveedor:", 0f, y, 72f, 15f, 10f, bold: true);
        AddLabel(band, proveedor, 74f, y, 676f, 15f, 10f);
        y += 18f;

        AddLabel(band, "Términos de pago:", 0f, y, 120f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(doc.TerminosPago) ? "-" : doc.TerminosPago!, 122f, y, 260f, 15f, 10f);
        AddLabel(band, "Calcula ISV:", 400f, y, 90f, 15f, 10f, bold: true);
        AddLabel(band, doc.CalculaIsv ? "Sí" : "No", 492f, y, 258f, 15f, 10f);
        y += 18f;

        if (!string.IsNullOrWhiteSpace(doc.DestinoUso))
        {
            var texto = WrapForWidth(doc.DestinoUso, 668f, 10f);
            var alto = 4f + CountLines(texto) * 15f;
            AddLabel(band, "Destino / uso:", 0f, y, 96f, 15f, 10f, bold: true);
            AddLabel(band, texto, 98f, y, 652f, alto, 10f, align: TextAlignment.TopLeft, multiline: true);
            y += alto + 2f;
        }

        return y + 6f;
    }

    private static float BuildTabla(Band band, OrdenCompraDto doc, float y)
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
                    (Cantidad(d.CantidadPedida), TextAlignment.TopRight),
                    (Money(d.CostoUnitario), TextAlignment.TopRight),
                    (Money(d.Impuesto), TextAlignment.TopRight),
                    (Money(d.Total), TextAlignment.TopRight)
                ]);
            }
        }

        return y + 10f;
    }

    private static float BuildTotales(Band band, OrdenCompraImpresionDto datos, float y)
    {
        var doc = datos.Documento;

        y = AddTotalLinea(band, y, "Subtotal:", Money(doc.SubTotal));
        if (doc.Descuento > 0m)
        {
            var montoDescuento = Math.Round(doc.SubTotal * doc.Descuento / 100m, 2, MidpointRounding.AwayFromZero);
            y = AddTotalLinea(band, y, $"Descuento ({doc.Descuento.ToString("0.##", EsHn)}%):", $"-{Money(montoDescuento)}");
        }
        if (doc.OtrosGastos > 0m)
        {
            y = AddTotalLinea(band, y, "Otros gastos:", Money(doc.OtrosGastos));
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

    private static float BuildObservacion(Band band, OrdenCompraDto doc, float y)
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
