using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Bancos;

namespace SIAD.Reports;

/// <summary>
/// Formato CORTO: cheque para el cliente (formato COMPAGOLG de la APP Espanola). Se imprime
/// SOBRE el cheque preimpreso del banco: solo posiciona los datos variables (lugar y fecha,
/// beneficiario, monto en letras y monto en numero). El resto (nombre del banco, lineas,
/// etiquetas) ya viene impreso en el papel.
///
/// Las posiciones son especificas del cheque fisico: se centralizan en las constantes de
/// CALIBRACION de abajo (unidades = centesimas de pulgada, 1/100"). Ajustar contra una
/// impresion de prueba sobre el cheque real. El formato LARGO (cheque + comprobante en una
/// sola hoja) es Rpt_Dev_Cheque_Detalle.
/// </summary>
public sealed class Rpt_Dev_Cheque : XtraReport
{
    private const string FontFamily = "Book Antiqua";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    // ----------------------------- CALIBRACION -----------------------------
    // Tamano del cheque (papel suelto por defecto: 8" x 3.5"). Si el cheque va en
    // hoja carta con talon, cambiar a 850 x 1100 y recolocar los campos.
    private const int ChequeAncho = 800;
    private const int ChequeAlto = 350;

    // Lugar y fecha ("CIUDAD, dd de Mes de aaaa").
    private const float FechaX = 430f, FechaY = 44f, FechaW = 350f;
    // Beneficiario (tras "PAGUESE A LA ORDEN DE").
    private const float BeneficiarioX = 150f, BeneficiarioY = 120f, BeneficiarioW = 470f;
    // Monto en numero (dentro del recuadro "L.").
    private const float MontoNumX = 628f, MontoNumY = 118f, MontoNumW = 150f;
    // Monto en letras (tras "LA SUMA DE"), puede ocupar dos lineas.
    private const float LetrasX = 95f, LetrasY = 160f, LetrasW = 655f, LetrasH = 34f;

    private const float FuenteDatos = 11f;
    // -----------------------------------------------------------------------

    public Rpt_Dev_Cheque(ChequeImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        PaperKind = DXPaperKind.Custom;
        PageWidth = ChequeAncho;
        PageHeight = ChequeAlto;
        Margins = new DXMargins(0, 0, 0, 0);
        RequestParameters = false;
        Font = new DXFont(FontFamily, FuenteDatos);

        var detail = new DetailBand { HeightF = ChequeAlto };
        Bands.Add(detail);

        // Lugar y fecha
        var lugarFecha = string.IsNullOrWhiteSpace(datos.Ciudad)
            ? FechaLarga(datos.FechaEmision)
            : $"{datos.Ciudad.Trim()}, {FechaLarga(datos.FechaEmision)}";
        AddLabel(detail, lugarFecha,
            FechaX, FechaY, FechaW, 18f, FuenteDatos, TextAlignment.MiddleLeft);

        // Beneficiario
        AddLabel(detail, string.IsNullOrWhiteSpace(datos.Beneficiario) ? string.Empty : datos.Beneficiario.Trim(),
            BeneficiarioX, BeneficiarioY, BeneficiarioW, 18f, FuenteDatos, TextAlignment.MiddleLeft, bold: true);

        // Monto en numero
        AddLabel(detail, $"**{Money(datos.Monto)}",
            MontoNumX, MontoNumY, MontoNumW, 18f, FuenteDatos, TextAlignment.MiddleRight, bold: true);

        // Monto en letras entre asteriscos (anti-alteracion), como el COMPAGOLG original
        AddLabel(detail, $"*** {datos.MontoEnLetras} ***",
            LetrasX, LetrasY, LetrasW, LetrasH, FuenteDatos, TextAlignment.TopLeft, bold: true, multiline: true);

        if (datos.Anulado)
        {
            Watermarks.Add(new XRWatermark
            {
                Id = "MarcaAnulado",
                Text = "ANULADO",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont(FontFamily, 60f, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 200,
                TextPosition = WatermarkPosition.InFront
            });
        }
    }

    private static void AddLabel(
        Band band,
        string text,
        float x,
        float y,
        float width,
        float height,
        float fontSize,
        TextAlignment align,
        bool bold = false,
        bool multiline = false)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, y, width, height),
            Text = text,
            Font = new DXFont(FontFamily, fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = align,
            Multiline = multiline,
            WordWrap = multiline,
            CanGrow = false,
            ForeColor = Color.Black,
            Borders = BorderSide.None,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });
    }

    private static string Money(decimal value) => value.ToString("N2", EsHn);

    private static string FechaLarga(DateTime fecha)
    {
        var mes = fecha.ToString("MMMM", EsHn);
        if (mes.Length > 0)
        {
            mes = char.ToUpper(mes[0], EsHn) + mes[1..];
        }

        return $"{fecha:dd} de {mes} de {fecha:yyyy}";
    }
}
