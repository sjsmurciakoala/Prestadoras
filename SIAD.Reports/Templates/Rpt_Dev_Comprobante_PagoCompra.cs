using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Comprobante de un PAGO (abono) a una cuenta por pagar de compra: el documento operativo del egreso
/// —proveedor, factura pagada, método (efectivo / cheque / transferencia), banco/cheque y el cuadro de
/// saldos (total de la factura, saldo antes, pago aplicado y saldo restante)—, más el valor pagado en
/// letras y el bloque de firmas. El asiento contable del pago se imprime aparte con
/// <see cref="Rpt_Dev_Partida_Contable"/>. Si el pago está anulado, se pinta la marca de agua "ANULADO".
/// </summary>
public sealed class Rpt_Dev_Comprobante_PagoCompra : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Comprobante_PagoCompra(PagoCompraImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Pago No. {datos.NumeroAbono:00000} - Factura {datos.NumeroFactura} - SIAD", datos.ImpresoPor));

        if (datos.Anulada)
        {
            AplicarMarcaAgua("ANULADO");
        }
    }

    private float BuildDocumento(Band band, PagoCompraImpresionDto datos)
    {
        var meta = new List<string>
        {
            $"Fecha: {datos.Fecha.ToString("dd/MM/yyyy", EsHn)}",
            $"Metodo: {datos.MetodoPago}"
        };

        var y = BuildEncabezado(band, datos, "COMPROBANTE DE PAGO", datos.NumeroAbono.ToString("00000"), meta, datos.EstadoTexto);

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        // Proveedor + factura
        AddLabel(band, "Proveedor:", 0f, y, 75f, 15f, 10f, bold: true);
        AddLabel(band, FirstNonEmpty(datos.Proveedor, datos.CodProveedor) ?? "-", 77f, y, 435f, 15f, 10f);
        AddLabel(band, "Factura No.:", 520f, y, 95f, 15f, 10f, bold: true);
        AddLabel(band, datos.NumeroFactura, 615f, y, 135f, 15f, 10f, align: TextAlignment.MiddleRight);
        y += 20f;

        // Banco / cuenta / cheque (solo pagos bancarios)
        if (!string.IsNullOrWhiteSpace(datos.Banco) || !string.IsNullOrWhiteSpace(datos.CuentaBancaria)
            || !string.IsNullOrWhiteSpace(datos.NumCheque))
        {
            AddLabel(band, "Banco:", 0f, y, 55f, 15f, 10f, bold: true);
            AddLabel(band, JoinNonEmpty(" - ", datos.Banco, datos.CuentaBancaria), 57f, y, 455f, 15f, 10f);
            if (!string.IsNullOrWhiteSpace(datos.NumCheque))
            {
                AddLabel(band, "Cheque No.:", 520f, y, 95f, 15f, 10f, bold: true);
                AddLabel(band, datos.NumCheque, 615f, y, 135f, 15f, 10f, align: TextAlignment.MiddleRight);
            }
            y += 20f;
        }

        y += 4f;
        y = BuildTablaImportes(band, datos, y);

        y = AddBloqueEnmarcado(band, y, $"VALOR PAGADO: L {Money(datos.Monto)}   -   SON: {datos.MontoEnLetras} LEMPIRAS");

        if (!string.IsNullOrWhiteSpace(datos.NumeroPartida))
        {
            AddLabel(band, $"Partida contable No. {datos.NumeroPartida}", 0f, y, ContentWidth, 14f, 9f,
                italic: true, color: Color.DimGray);
            y += 18f;
        }

        y = BuildObservaciones(band, datos, y);

        y = BuildFirmas(band, y,
        [
            ("ELABORADO POR", datos.ImpresoPor),
            ("AUTORIZADO POR", null),
            ("RECIBI CONFORME", null)
        ]);

        return y;
    }

    private static float BuildTablaImportes(Band band, PagoCompraImpresionDto datos, float y)
    {
        float[] anchos = [560f, 190f];

        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Concepto", TextAlignment.MiddleLeft),
            ("Valor (L)", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        y = AddGridRow(band, y, 17f, anchos,
        [
            ("Total de la factura", TextAlignment.TopLeft),
            (Money(datos.MontoFactura), TextAlignment.TopRight)
        ]);

        y = AddGridRow(band, y, 17f, anchos,
        [
            ("Saldo antes del pago", TextAlignment.TopLeft),
            (Money(datos.SaldoAnterior), TextAlignment.TopRight)
        ]);

        y = AddGridRow(band, y, 17f, anchos,
        [
            ("Pago aplicado", TextAlignment.TopLeft),
            (Money(datos.Monto), TextAlignment.TopRight)
        ], bold: true);

        // Retención (si la hubo): desglose del neto realmente pagado al banco/caja.
        if (datos.Retenido > 0m)
        {
            y = AddGridRow(band, y, 17f, anchos,
            [
                ("(−) Retención", TextAlignment.TopLeft),
                (Money(datos.Retenido), TextAlignment.TopRight)
            ]);

            y = AddGridRow(band, y, 17f, anchos,
            [
                ("Neto pagado", TextAlignment.TopLeft),
                (Money(datos.Monto - datos.Retenido), TextAlignment.TopRight)
            ], bold: true);
        }

        y = AddGridRow(band, y, 19f, anchos,
        [
            ("SALDO RESTANTE", TextAlignment.MiddleRight),
            (Money(datos.SaldoRestante), TextAlignment.MiddleRight)
        ], bold: true, total: true);

        return y + 12f;
    }

    private static float BuildObservaciones(Band band, PagoCompraImpresionDto datos, float y)
    {
        if (string.IsNullOrWhiteSpace(datos.Observaciones))
        {
            return y;
        }

        var texto = WrapForWidth(datos.Observaciones, 648f, 9.5f);
        var alto = 4f + CountLines(texto) * 15f;
        AddLabel(band, "Observaciones:", 0f, y, 100f, 15f, 9.5f, bold: true);
        AddLabel(band, texto, 102f, y, 648f, alto, 9.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
        return y + alto + 2f;
    }
}
