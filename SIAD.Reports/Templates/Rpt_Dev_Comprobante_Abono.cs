using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Reports;

public sealed class Rpt_Dev_Comprobante_Abono : XtraReport
{
    private const float ContentWidth = 750f;
    private const string FontFamily = "Times New Roman";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    public Rpt_Dev_Comprobante_Abono(CompromisoAbonoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        PaperKind = DXPaperKind.Letter;
        PageWidth = 850;
        PageHeight = 1100;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;
        Font = new DXFont(FontFamily, 11f);

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        if (string.Equals(datos.Estado, "A", StringComparison.OrdinalIgnoreCase))
        {
            Watermarks.Add(new XRWatermark
            {
                Id = "MarcaAnulado",
                Text = "ANULADO",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont(FontFamily, 90f, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 190,
                TextPosition = WatermarkPosition.InFront
            });
        }
    }

    private static float BuildDocumento(Band band, CompromisoAbonoImpresionDto datos)
    {
        var b = datos.Base;
        var compromiso = b.Compromiso;
        var y = 0f;

        // Encabezado empresa.
        AddLabel(band, b.EmpresaNombre, 0f, y, 520f, 20f, 14f, bold: true);
        y += 22f;
        if (!string.IsNullOrWhiteSpace(b.EmpresaRtn))
        {
            AddLabel(band, $"R.T.N. {b.EmpresaRtn!.Trim()}", 0f, y, 520f, 13f, 8.5f, color: Color.DimGray);
            y += 14f;
        }
        if (!string.IsNullOrWhiteSpace(b.EmpresaDireccion))
        {
            AddLabel(band, b.EmpresaDireccion!.Trim(), 0f, y, 520f, 13f, 8.5f, color: Color.DimGray);
            y += 14f;
        }

        // Caja de titulo del comprobante.
        var panel = new XRPanel { BoundsF = new RectangleF(520f, 0f, 230f, 72f), Borders = BorderSide.All, BorderWidth = 2f };
        band.Controls.Add(panel);
        AddLabel(panel, "COMPROBANTE DE ABONO", 0f, 6f, 230f, 13f, 8.5f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(panel, $"Compromiso OPD-{compromiso.NumeroOrden}", 0f, 21f, 230f, 16f, 10f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(panel, $"Abono No. {datos.NumeroAbono}", 0f, 39f, 230f, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(panel, $"Fecha: {datos.FechaAbono.ToString("dd/MM/yyyy", EsHn)}", 0f, 57f, 230f, 12f, 8f, align: TextAlignment.MiddleCenter);

        y = Math.Max(y, 78f) + 8f;
        band.Controls.Add(new XRLine { BoundsF = new RectangleF(0f, y, ContentWidth, 3f), LineWidth = 3f });
        y += 12f;

        // Proveedor / concepto.
        AddLabel(band, "Proveedor:", 0f, y, 78f, 15f, 10f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(compromiso.CodigoProveedor)
            ? compromiso.Proveedor
            : $"{compromiso.CodigoProveedor!.Trim()} - {compromiso.Proveedor}", 80f, y, 670f, 15f, 10f);
        y += 18f;

        AddLabel(band, "Concepto:", 0f, y, 72f, 15f, 10f, bold: true);
        AddLabel(band, compromiso.Concepto, 74f, y, 676f, 15f, 10f);
        y += 20f;

        AddLabel(band, "Metodo de pago:", 0f, y, 110f, 15f, 10f, bold: true);
        AddLabel(band, datos.MetodoPago, 112f, y, 250f, 15f, 10f);
        if (!string.IsNullOrWhiteSpace(datos.NumeroPartida))
        {
            AddLabel(band, "Partida:", 390f, y, 68f, 15f, 10f, bold: true);
            AddLabel(band, datos.NumeroPartida!, 460f, y, 290f, 15f, 10f);
        }
        y += 26f;

        // Cuadro de montos: compromiso, abono, saldo anterior, saldo restante.
        y = AddMontoRow(band, y, "Monto del compromiso", compromiso.Monto, bold: false);
        y = AddMontoRow(band, y, "Saldo anterior", datos.SaldoAnterior, bold: false);
        y = AddMontoRow(band, y, "MONTO ABONADO", datos.MontoAbono, bold: true);
        y = AddMontoRow(band, y, "SALDO RESTANTE", datos.SaldoRestante, bold: true);
        y += 10f;

        var enLetras = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Text = $"SON (abono): {b.MontoEnLetras}",
            Font = new DXFont(FontFamily, 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(8, 8, 0, 0, 100f)
        };
        band.Controls.Add(enLetras);
        y += 30f;

        // Firmas.
        y += 40f;
        string[] titulos = ["ENTREGADO POR", "RECIBIDO CONFORME"];
        const float anchoColumna = 240f;
        const float paso = 380f;
        for (var i = 0; i < titulos.Length; i++)
        {
            var x = i * paso;
            band.Controls.Add(new XRLine { BoundsF = new RectangleF(x, y, anchoColumna, 1.5f), LineWidth = 1f });
            AddLabel(band, titulos[i], x, y + 4f, anchoColumna, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
        }
        AddLabel(band, b.ImpresoPor, 0f, y + 17f, anchoColumna, 11f, 8f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 40f;

        band.Controls.Add(new XRLine { BoundsF = new RectangleF(0f, y, ContentWidth, 2f), LineStyle = DXDashStyle.Dash, LineWidth = 1f, ForeColor = Color.LightGray });
        AddLabel(band, $"Comprobante de abono {datos.NumeroAbono} - Documento OPD-{compromiso.NumeroOrden} - SIAD", 0f, y + 6f, ContentWidth, 12f, 7.5f, color: Color.DimGray);
        AddLabel(band, $"Impreso por {b.ImpresoPor} el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}", 0f, y + 18f, ContentWidth, 12f, 7.5f, color: Color.DimGray);

        return y + 34f;
    }

    private static float AddMontoRow(Band band, float y, string etiqueta, decimal valor, bool bold)
    {
        var panel = new XRPanel { BoundsF = new RectangleF(390f, y, 360f, 22f), Borders = BorderSide.All, BorderWidth = bold ? 2f : 1f };
        band.Controls.Add(panel);
        AddLabel(panel, etiqueta, 8f, 0f, 210f, 22f, bold ? 10f : 9f, bold: bold, TextAlignment.MiddleLeft, color: bold ? Color.Black : Color.DimGray);
        AddLabel(panel, $"L {Money(valor)}", 8f, 0f, 344f, 22f, bold ? 12f : 10f, bold: bold, TextAlignment.MiddleRight);
        return y + 26f;
    }

    private static void AddLabel(
        XRControl parent, string text, float x, float y, float width, float height, float fontSize,
        bool bold = false, TextAlignment align = TextAlignment.MiddleLeft, Color? color = null)
    {
        var estilo = bold ? DXFontStyle.Bold : DXFontStyle.Regular;
        parent.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, y, width, height),
            Text = text,
            Font = new DXFont(FontFamily, fontSize, estilo),
            TextAlignment = align,
            Multiline = false,
            WordWrap = false,
            CanGrow = false,
            ForeColor = color ?? Color.Black,
            Borders = BorderSide.None,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });
    }

    private static string Money(decimal value) => value.ToString("N2", EsHn);
}
