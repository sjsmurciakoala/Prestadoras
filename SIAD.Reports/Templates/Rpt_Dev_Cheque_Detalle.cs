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
/// Formato LARGO: cheque + comprobante en una sola hoja preimpresa (formato de Aguas de
/// Puerto Cortes / Banco Lafise). El papel ya trae los cuadros y las etiquetas; el SIAD
/// solo imprime los datos variables: arriba el cheque (lugar y fecha, beneficiario, valor,
/// cantidad en letras) y abajo el comprobante (concepto, valor, la partida contable con su
/// total y "Orden de Pago No.", y bajo ELABORADO POR el usuario que imprime).
///
/// Unidades = centesimas de pulgada (1/100"). 1 cm = 39.37. Las posiciones del COMPROBANTE
/// vienen de las medidas fisicas dadas (exactas). Las del CHEQUE son aproximadas y se
/// afinan con una impresion de prueba: todas estan en el bloque CALIBRACION de abajo.
/// El formato CORTO (solo el cheque) es Rpt_Dev_Cheque.
/// </summary>
public sealed class Rpt_Dev_Cheque_Detalle : XtraReport
{
    private const string FuenteCheque = "Book Antiqua";
    private const string FuenteCmp = "Arial";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    // =================== CALIBRACION (1/100") ===================
    // Hoja: 19.0 x 21.5 cm.
    private const int HojaAncho = 748;   // 19.0 cm
    private const int HojaAlto = 846;    // 21.5 cm

    // ---- CHEQUE (arriba, 0 - 10 cm) — posiciones aproximadas, calibrables ----
    private const float ChkFechaX = 430f, ChkFechaY = 82f, ChkFechaW = 310f;
    private const float ChkBenefX = 40f, ChkBenefY = 155f, ChkBenefW = 430f;
    private const float ChkValorX = 585f, ChkValorY = 153f, ChkValorW = 155f;
    private const float ChkLetrasX = 40f, ChkLetrasY = 220f, ChkLetrasW = 600f;
    private const float ChkFuente = 9.5f;

    // ---- COMPROBANTE (abajo, pegado al fondo) — MEDIDAS EXACTAS ----
    // Margen izq 0.3 cm = 12. Bloque a 10 cm (394); area Concepto a 10.9 cm (429).
    private const float ConceptoX = 12f, ConceptoW = 591f;   // 15.02 cm
    private const float ValorX = 603f, ValorW = 130f;        // 3.3 cm
    private const float FilaSuperiorY = 429f;                // 10.9 cm
    private const float FilaSuperiorH = 211f;                // 5.35 cm
    // Partida (16.25 - 20.15 cm).
    private const float PartidaY = 640f;                     // 16.25 cm
    private const float PartidaH = 154f;                     // 3.9 cm
    private const float CodigoX = 12f, CodigoW = 126f;       // 3.2 cm
    private const float DescX = 138f, DescW = 402f;          // 10.2 cm
    private const float DebeX = 540f, DebeW = 98f;           // 2.5 cm
    private const float HaberX = 638f, HaberW = 98f;         // 2.5 cm
    // Firmas (20.15 - 21.4 cm): 4 columnas iguales de 4.6 cm (181).
    private const float FirmasY = 793f;                      // 20.15 cm
    private const float FirmaElaboradoX = 193f, FirmaColW = 181f; // 2a columna
    private const float CmpFuente = 7.5f;
    // ============================================================

    public Rpt_Dev_Cheque_Detalle(ChequeImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        PaperKind = DXPaperKind.Custom;
        PageWidth = HojaAncho;
        PageHeight = HojaAlto;
        Margins = new DXMargins(0, 0, 0, 0);
        RequestParameters = false;
        Font = new DXFont(FuenteCmp, CmpFuente);

        var detail = new DetailBand { HeightF = HojaAlto };
        Bands.Add(detail);

        BuildCheque(detail, datos);
        BuildComprobante(detail, datos);

        if (datos.Anulado)
        {
            Watermarks.Add(new XRWatermark
            {
                Id = "MarcaAnulado",
                Text = "ANULADO",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont(FuenteCheque, 72f, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 200,
                TextPosition = WatermarkPosition.InFront
            });
        }
    }

    private static void BuildCheque(Band band, ChequeImpresionDto datos)
    {
        AddLabel(band, FechaCheque(datos.FechaEmision, datos.Ciudad),
            ChkFechaX, ChkFechaY, ChkFechaW, 16f, FuenteCheque, ChkFuente, TextAlignment.MiddleLeft);

        AddLabel(band, string.IsNullOrWhiteSpace(datos.Beneficiario) ? string.Empty : datos.Beneficiario.Trim(),
            ChkBenefX, ChkBenefY, ChkBenefW, 16f, FuenteCheque, ChkFuente + 0.5f, TextAlignment.MiddleLeft, bold: true);

        AddLabel(band, $"**{Money(datos.Monto)}",
            ChkValorX, ChkValorY, ChkValorW, 16f, FuenteCheque, ChkFuente + 0.5f, TextAlignment.MiddleRight, bold: true);

        AddLabel(band, $"**** {LetrasSinMoneda(datos.MontoEnLetras)} ****",
            ChkLetrasX, ChkLetrasY, ChkLetrasW, 16f, FuenteCheque, ChkFuente, TextAlignment.MiddleLeft, bold: true);
    }

    private static void BuildComprobante(Band band, ChequeImpresionDto datos)
    {
        // Concepto (multilinea, dentro del area) + valor a la derecha.
        var concepto = string.IsNullOrWhiteSpace(datos.Concepto) ? string.Empty : datos.Concepto.Trim();
        AddLabel(band, WrapForWidth(concepto, ConceptoW - 12f, CmpFuente),
            ConceptoX + 4f, FilaSuperiorY + 4f, ConceptoW - 8f, FilaSuperiorH - 8f, FuenteCmp, CmpFuente,
            TextAlignment.TopLeft, multiline: true);
        AddLabel(band, $"**{Money(datos.Monto)}",
            ValorX + 2f, FilaSuperiorY + 4f, ValorW - 6f, 12f, FuenteCmp, CmpFuente, TextAlignment.TopRight);

        // Partida contable: filas de la distribucion + total.
        BuildPartida(band, datos);

        // Firma: solo ELABORADO POR (usuario que imprime).
        AddLabel(band, datos.ImpresoPor,
            FirmaElaboradoX, FirmasY + 6f, FirmaColW, 12f, FuenteCmp, CmpFuente + 0.5f, TextAlignment.MiddleCenter);
    }

    private static void BuildPartida(Band band, ChequeImpresionDto datos)
    {
        var lineas = datos.Distribucion;
        const float topDatos = PartidaY + 6f;      // debajo del encabezado preimpreso
        const float altoTotal = 12f;               // fila de "TOTAL / Orden de Pago"
        var espacio = PartidaH - 6f - altoTotal;
        var filas = Math.Max(1, lineas.Count);
        var rowH = Math.Min(22f, espacio / filas);

        var y = topDatos;
        foreach (var l in lineas)
        {
            AddLabel(band, l.CodigoCuenta, CodigoX + 2f, y, CodigoW - 4f, rowH, FuenteCmp, CmpFuente, TextAlignment.MiddleLeft);
            AddLabel(band, l.NombreCuenta, DescX + 2f, y, DescW - 4f, rowH, FuenteCmp, CmpFuente, TextAlignment.MiddleLeft);
            AddLabel(band, l.Cargo == 0m ? string.Empty : Money(l.Cargo), DebeX, y, DebeW - 4f, rowH, FuenteCmp, CmpFuente, TextAlignment.MiddleRight);
            AddLabel(band, l.Credito == 0m ? string.Empty : Money(l.Credito), HaberX, y, HaberW - 4f, rowH, FuenteCmp, CmpFuente, TextAlignment.MiddleRight);
            y += rowH;
        }

        // Fila de totales, al fondo del area de la partida.
        var yTotal = PartidaY + PartidaH - altoTotal - 1f;
        if (!string.IsNullOrWhiteSpace(datos.OrigenDocumento))
        {
            AddLabel(band, $"Orden de Pago No. {datos.OrigenDocumento.Trim()}",
                CodigoX + 2f, yTotal, DescX - CodigoX + 60f, altoTotal, FuenteCmp, CmpFuente - 0.5f, TextAlignment.MiddleLeft);
        }

        AddLabel(band, Money(datos.Distribucion.Sum(l => l.Cargo)), DebeX, yTotal, DebeW - 4f, altoTotal, FuenteCmp, CmpFuente, TextAlignment.MiddleRight, bold: true);
        AddLabel(band, Money(datos.Distribucion.Sum(l => l.Credito)), HaberX, yTotal, HaberW - 4f, altoTotal, FuenteCmp, CmpFuente, TextAlignment.MiddleRight, bold: true);
    }

    private static void AddLabel(
        Band band,
        string text,
        float x,
        float y,
        float width,
        float height,
        string fontFamily,
        float fontSize,
        TextAlignment align,
        bool bold = false,
        bool multiline = false)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, y, width, height),
            Text = text,
            Font = new DXFont(fontFamily, fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = align,
            Multiline = multiline,
            WordWrap = multiline,
            CanGrow = false,
            ForeColor = Color.Black,
            Borders = BorderSide.None,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });
    }

    // El renderizador solo respeta CRLF; se quiebra el texto por ancho a mano.
    private static string WrapForWidth(string? texto, float ancho, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var maxCaracteres = Math.Max(10, (int)((ancho - 6f) / (0.58f * fontSize)));
        var palabras = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lineas = new List<string>();
        var actual = string.Empty;

        foreach (var palabra in palabras)
        {
            var candidata = actual.Length == 0 ? palabra : $"{actual} {palabra}";
            if (candidata.Length > maxCaracteres && actual.Length > 0)
            {
                lineas.Add(actual);
                actual = palabra;
            }
            else
            {
                actual = candidata;
            }
        }

        if (actual.Length > 0)
        {
            lineas.Add(actual);
        }

        return string.Join("\r\n", lineas);
    }

    private static string Money(decimal value) => value.ToString("N2", EsHn);

    // "Puerto Cortes, 13  JULIO  2026" (mes en mayusculas, como el cheque preimpreso).
    private static string FechaCheque(DateTime fecha, string? ciudad)
    {
        var mes = fecha.ToString("MMMM", EsHn).ToUpper(EsHn);
        var lugar = string.IsNullOrWhiteSpace(ciudad) ? string.Empty : $"{ciudad.Trim()}, ";
        return $"{lugar}{fecha:dd}  {mes}  {fecha:yyyy}";
    }

    // El monto en letras del cheque va sin "LEMPIRAS" (esa palabra ya viene preimpresa).
    private static string LetrasSinMoneda(string? montoEnLetras)
    {
        var s = (montoEnLetras ?? string.Empty).Trim();
        return s.EndsWith(" LEMPIRAS", StringComparison.OrdinalIgnoreCase) ? s[..^9].TrimEnd() : s;
    }
}
