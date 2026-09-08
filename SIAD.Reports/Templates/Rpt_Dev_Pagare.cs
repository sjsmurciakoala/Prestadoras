using System.Globalization;
using System.Drawing;
using System.Text.RegularExpressions;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Cobranza;

namespace SIAD.Reports;

/// <summary>
/// Pagaré a la vista del convenio de pago. Conserva el texto legal del formato
/// que usa la unidad de cobranza, compuesto como documento formal: serif,
/// párrafos justificados a interlineado 1,5 y los datos integrados en la
/// redacción.
///
/// El cuerpo va en <see cref="XRRichText"/> y no en una etiqueta porque, con el
/// resaltado en línea activo, la etiqueta ignora el justificado y el
/// interlineado (limitación documentada de AllowMarkupText).
///
/// El texto legal vive en las constantes de esta clase; si el área legal lo
/// cambia, se cambia aquí.
/// </summary>
public sealed class Rpt_Dev_Pagare : XtraReport
{
    private const float ContentWidth = 750f;
    private const float Sangria = 25f;                       // el texto no llega al filo de la caja
    private const float TextoAncho = ContentWidth - Sangria * 2f;

    private const float FontSize = 12f;
    private const float LineH = 31.5f;                        // Times 12 pt a interlineado 1,5
    private const float RowH = 20f;

    // El bloque de firmas se ancla cerca del pie: así la hoja queda pareja
    // aunque el texto del convenio salga más corto o más largo.
    private const float FirmasY = 880f;
    private const float AireAntesDeFirmar = 130f;

    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");
    private const string Fuente = "Times New Roman";

    // El domicilio del pagaré es fijo: la unidad de cobranza siempre lo suscribe
    // en el mismo municipio.
    private const string Municipio = "Puerto Cortés";
    private const string Departamento = "Cortés";

    // ---- Texto legal (revisable por el área legal de la empresa) ----
    private const string Obligacion =
        "Yo, <b>{DEUDOR}</b>, con documento de identidad No. <b>{IDENTIDAD}</b>, mayor de edad, de " +
        "nacionalidad hondureña, con domicilio en el Municipio de <b>{MUNICIPIO}</b>, Departamento de " +
        "<b>{DEPARTAMENTO}</b>, en mi condición personal, por medio del presente documento denominado " +
        "<b>PAGARÉ A LA VISTA, HAGO CONSTAR:</b> que en mi calidad antes dicha debo y pagaré " +
        "incondicionalmente a la Empresa <b>{ACREEDOR}</b>, la suma del valor real más intereses y " +
        "recargos, siendo un total de <b>L. {MONTO}</b>, el cual será pagado en <b>{MESES}</b> meses, " +
        "desde la fecha <b>{DESDE}</b> hasta la fecha <b>{HASTA}</b>, en las oficinas de la unidad de " +
        "Recuperación por Morosidad de esta Institución.";

    private const string Constancia =
        "Y para constancia y efectos legales firmo el presente pagaré en el Municipio de " +
        "<b>{MUNICIPIO}</b>, Departamento de <b>{DEPARTAMENTO}</b>, a los <b>{DIA}</b> días del mes de " +
        "<b>{MES}</b> del año <b>{ANIO}</b>.";

    public Rpt_Dev_Pagare(PagareImpresionDto pagare)
    {
        PaperKind = DXPaperKind.Letter;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;

        var band = new DetailBand();
        float y = 0f;

        // ---------- Membrete ----------
        band.Controls.Add(Etiqueta(Mayus(pagare.EmpresaNombre), 0f, y, ContentWidth, 26f,
            13f, bold: true, TextAlignment.MiddleCenter));
        y += 30f;

        band.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 2f),
            ForeColor = Color.Black,
            LineWidth = 1f
        });
        y += 44f;

        // ---------- Título y monto, enmarcados de margen a margen ----------
        var marco = new XRPanel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 44f),
            Borders = BorderSide.All,
            BorderWidth = 1.5f
        };

        // Los hijos de un panel heredan sus bordes: hay que apagarlos o el
        // recuadro sale partido.
        var titulo = Etiqueta("PAGARÉ", 16f, 0f, 300f, 44f, 20f, bold: true);
        titulo.Borders = BorderSide.None;
        marco.Controls.Add(titulo);

        var cifra = Etiqueta($"POR L. {Money(pagare.MontoTotal)}", 316f, 0f, ContentWidth - 332f, 44f,
            15f, bold: true, TextAlignment.MiddleRight);
        cifra.Borders = BorderSide.None;
        marco.Controls.Add(cifra);

        band.Controls.Add(marco);
        y += 62f;

        // ---------- Cuerpo ----------
        y = Parrafo(band, y, Obligacion
            .Replace("{DEUDOR}", Mayus(pagare.DeudorNombre))
            .Replace("{IDENTIDAD}", Dato(pagare.DeudorIdentidad))
            .Replace("{MUNICIPIO}", Municipio)
            .Replace("{DEPARTAMENTO}", Departamento)
            .Replace("{ACREEDOR}", Dato(pagare.EmpresaNombre))
            .Replace("{MONTO}", Money(pagare.MontoTotal))
            .Replace("{MESES}", pagare.CantidadMeses.ToString(EsHn))
            .Replace("{DESDE}", Larga(pagare.FechaDesde))
            .Replace("{HASTA}", Larga(pagare.FechaHasta)));

        y += 16f;

        y = Parrafo(band, y, Constancia
            .Replace("{MUNICIPIO}", Municipio)
            .Replace("{DEPARTAMENTO}", Departamento)
            .Replace("{DIA}", pagare.FechaSuscripcion.ToString("dd", EsHn))
            .Replace("{MES}", pagare.FechaSuscripcion.ToString("MMMM", EsHn))
            .Replace("{ANIO}", pagare.FechaSuscripcion.ToString("yyyy", EsHn)));

        // ---------- Firmas ----------
        // Dos columnas del mismo ancho, separadas por el centro de la hoja. El
        // espacio de arriba es para firmar sobre la línea.
        y = Math.Max(y + AireAntesDeFirmar, FirmasY);

        const float colAncho = 330f;
        const float colDerecha = ContentWidth - colAncho;

        band.Controls.Add(Raya(0f, y, colAncho));
        band.Controls.Add(Raya(colDerecha, y, colAncho));
        y += 8f;

        // Quién firma por la empresa: su nombre cuando está configurado, y si no
        // el rótulo genérico de siempre.
        var yIzq = y;
        if (!string.IsNullOrWhiteSpace(pagare.FirmanteCobranza))
        {
            band.Controls.Add(Etiqueta(Mayus(pagare.FirmanteCobranza), 0f, yIzq, colAncho, RowH,
                FontSize, bold: true, TextAlignment.MiddleCenter));
            yIzq += RowH;
        }

        band.Controls.Add(Etiqueta("Representante Legal", 0f, yIzq, colAncho, RowH,
            FontSize, bold: false, TextAlignment.MiddleCenter));
        yIzq += RowH;
        band.Controls.Add(Etiqueta("Unidad de Cobranza", 0f, yIzq, colAncho, RowH,
            FontSize, bold: false, TextAlignment.MiddleCenter));

        // Quién firma como deudor, con sus datos en pares alineados debajo.
        var yDer = y;
        band.Controls.Add(Etiqueta(Mayus(pagare.DeudorNombre), colDerecha, yDer, colAncho, RowH,
            FontSize, bold: true, TextAlignment.MiddleCenter));
        yDer += RowH + 4f;
        yDer = DatoDeFirma(band, colDerecha, yDer, "Identidad No.", Dato(pagare.DeudorIdentidad));
        yDer = DatoDeFirma(band, colDerecha, yDer, "Cuenta No.", pagare.NumeroCuenta);

        band.HeightF = Math.Max(yIzq + RowH, yDer) + 16f;
        Bands.Add(band);
    }

    // ---------------- Párrafos ----------------

    /// <summary>
    /// Escribe un párrafo justificado a interlineado 1,5 con los datos
    /// resaltados, y devuelve la Y siguiente. La banda no reacomoda controles,
    /// así que el alto se estima por caracteres visibles y se sobreestima a
    /// propósito.
    /// </summary>
    private static float Parrafo(Band band, float y, string textoConMarcas)
    {
        var visible = Regex.Replace(textoConMarcas, "</?b>", string.Empty);

        // Medido sobre el render: Times a 12 pt entra ~81 caracteres por línea
        // en esta caja; media línea de colchón para no cortar.
        var charsPorLinea = Math.Max(20, (int)(TextoAncho / (FontSize * 0.70f)));
        var lineas = Math.Max(1, (int)Math.Ceiling(visible.Length / (double)charsPorLinea));
        var alto = lineas * LineH + LineH * 0.5f;

        var parrafo = new XRRichText
        {
            BoundsF = new RectangleF(Sangria, y, TextoAncho, alto),
            CanGrow = false,
            CanShrink = false,
            Html = "<p style=\"margin:0; text-align:justify; line-height:150%; " +
                   $"font-family:'{Fuente}'; font-size:{FontSize}pt\">{textoConMarcas}</p>"
        };
        band.Controls.Add(parrafo);

        return y + alto;
    }

    /// <summary>
    /// Un dato del deudor bajo su firma: rótulo y valor en columnas fijas, para
    /// que la identidad y la cuenta queden alineadas entre sí.
    /// </summary>
    private static float DatoDeFirma(Band band, float x, float y, string rotulo, string valor)
    {
        const float sangria = 52f;
        const float anchoRotulo = 120f;

        band.Controls.Add(Etiqueta(rotulo, x + sangria, y, anchoRotulo, RowH, FontSize - 1f));
        band.Controls.Add(Etiqueta(valor, x + sangria + anchoRotulo, y, 330f - sangria - anchoRotulo, RowH,
            FontSize - 1f, bold: true));

        return y + RowH;
    }

    // ---------------- Texto ----------------

    private static string Money(decimal value) => value.ToString("N2", EsHn);

    private static string Mayus(string? texto) => (texto ?? string.Empty).Trim().ToUpper(EsHn);

    /// <summary>"10 de septiembre de 2026": en un documento formal la fecha va en palabras.</summary>
    private static string Larga(DateTime? fecha)
        => fecha is { } f ? $"{f:dd} de {f.ToString("MMMM", EsHn)} de {f:yyyy}" : "____________";

    /// <summary>Lo que el sistema no tiene se deja como línea para llenar a mano.</summary>
    private static string Dato(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? "____________" : valor.Trim();

    // ---------------- Controles ----------------

    private static XRLabel Etiqueta(
        string text, float x, float y, float w, float h, float size,
        bool bold = false, TextAlignment align = TextAlignment.MiddleLeft)
        => new()
        {
            BoundsF = new RectangleF(x, y, w, h),
            Text = text,
            Font = new DXFont(Fuente, size, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = align,
            ForeColor = Color.Black,
            WordWrap = true
        };

    private static XRLine Raya(float x, float y, float w)
        => new() { BoundsF = new RectangleF(x, y, w, 2f), ForeColor = Color.Black };
}
