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
/// Compromiso de pago: el documento que firma el cliente al suscribir el
/// convenio. Sigue el formato de la unidad de cobranza — encabezado con código,
/// nombre y fecha, desglose de la mora por concepto frente a las condiciones del
/// financiamiento, texto legal y plan de cuotas con saldo corrido.
///
/// El texto legal vive en las constantes de esta clase; si el área legal lo
/// cambia, se cambia aquí.
/// </summary>
public sealed class Rpt_Dev_Convenio : XtraReport
{
    private const float ContentWidth = 750f;
    private const float FontSize = 9.5f;                      // encabezado, condiciones y plan de cuotas
    private const float RowH = 16f;

    // El texto legal se lee como el del pagaré: mismo cuerpo e interlineado 1,5.
    private const float TextoSize = 12f;
    private const float TextoLineH = 31.5f;

    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");
    private const string Fuente = "Times New Roman";

    // ---- Texto legal (revisable por el área legal de la empresa) ----
    private const string Identificacion =
        "Yo <b>{FIRMANTE}</b>, mayor de edad, con tarjeta de identidad No. <b>{IDENTIDAD}</b>, " +
        "{CALIDAD} de la Empresa <b>{EMPRESA}</b>, bajo la cuenta <b>{CUENTA}</b>.";

    private const string Constancia =
        "<b>HAGO CONSTAR:</b> que se debe a la Empresa <b>{EMPRESA}</b> la cantidad abajo señalada por " +
        "concepto de agua potable consumida y no pagada, que se hará efectiva en el número de cuotas y " +
        "en las fechas abajo estipuladas, con la finalidad de que la deuda quede cancelada al terminar " +
        "de efectuar dichos pagos de agua potable recibida por parte de {EMPRESA}. Entiendo que el " +
        "atraso en el pago de dos de estas cuotas completas y consecutivas, o de los cargos corrientes " +
        "de agua potable, dará derecho a {EMPRESA} para dar por vencida toda la obligación, para que " +
        "proceda judicialmente contra mi persona ante la autoridad judicial de su elección y para " +
        "suspender el servicio, renunciando expresamente al fuero de mi domicilio.";

    public Rpt_Dev_Convenio(ConvenioImpresionDto convenio)
    {
        PaperKind = DXPaperKind.Letter;
        Margins = new DXMargins(50, 50, 45, 45);
        RequestParameters = false;
        DataSource = ArmarFilas(convenio);

        var rh = new ReportHeaderBand();
        float y = 0f;

        // ---------- Membrete ----------
        rh.Controls.Add(Etiqueta(Mayus(convenio.EmpresaNombre), 0f, y, ContentWidth, 22f,
            12f, bold: true, TextAlignment.MiddleCenter));
        y += 26f;

        // ---------- Título y clave ----------
        var marco = new XRPanel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 30f),
            Borders = BorderSide.All,
            BorderWidth = 1f
        };
        var titulo = Etiqueta("COMPROMISO DE PAGO", 10f, 0f, 340f, 30f, 12f, bold: true);
        titulo.Borders = BorderSide.None;
        marco.Controls.Add(titulo);

        var clave = Etiqueta($"Clave: {convenio.ClienteClave}", 380f, 0f, 360f, 30f, 11f,
            bold: true, TextAlignment.MiddleRight);
        clave.Borders = BorderSide.None;
        marco.Controls.Add(clave);
        rh.Controls.Add(marco);
        y += 36f;

        // ---------- Código, nombre y fecha ----------
        rh.Controls.Add(Etiqueta("CÓDIGO", 0f, y, 180f, RowH, FontSize, bold: true, TextAlignment.MiddleCenter));
        rh.Controls.Add(Etiqueta("NOMBRE", 180f, y, 420f, RowH, FontSize, bold: true, TextAlignment.MiddleCenter));
        rh.Controls.Add(Etiqueta("FECHA", 600f, y, 150f, RowH, FontSize, bold: true, TextAlignment.MiddleCenter));
        y += RowH;

        rh.Controls.Add(Raya(0f, y, ContentWidth, Color.Gray));
        y += 4f;

        rh.Controls.Add(Etiqueta(convenio.Codigo, 0f, y, 180f, RowH, FontSize, bold: true, TextAlignment.MiddleCenter));
        rh.Controls.Add(Etiqueta(Mayus(convenio.ClienteNombre), 180f, y, 420f, RowH, FontSize, bold: true));
        rh.Controls.Add(Etiqueta(Corta(convenio.FechaCreacion), 600f, y, 150f, RowH, FontSize,
            bold: true, TextAlignment.MiddleCenter));
        y += RowH + 2f;

        rh.Controls.Add(Etiqueta($"DIRECCIÓN: {Dato(convenio.ClienteDireccion)}", 0f, y, ContentWidth, RowH, FontSize));
        y += RowH + 10f;

        // ---------- Deuda por concepto | condiciones del financiamiento ----------
        var yBloque = y;

        rh.Controls.Add(Etiqueta("CONCEPTO", 0f, y, 240f, RowH, FontSize, bold: true));
        rh.Controls.Add(Etiqueta("VALOR L.", 240f, y, 120f, RowH, FontSize, bold: true, TextAlignment.MiddleRight));
        y += RowH + 2f;

        if (convenio.Conceptos.Count == 0)
        {
            rh.Controls.Add(Etiqueta("Saldo trasladado al convenio", 0f, y, 240f, RowH, FontSize));
            rh.Controls.Add(Etiqueta(Money(convenio.MontoTotal), 240f, y, 120f, RowH, FontSize, false, TextAlignment.MiddleRight));
            y += RowH;
        }
        else
        {
            foreach (var concepto in convenio.Conceptos)
            {
                rh.Controls.Add(Etiqueta(concepto.Descripcion, 0f, y, 240f, RowH, FontSize));
                rh.Controls.Add(Etiqueta(Money(concepto.Valor), 240f, y, 120f, RowH, FontSize, false, TextAlignment.MiddleRight));
                y += RowH;
            }
        }

        y += 4f;
        rh.Controls.Add(Etiqueta("TOTAL MORA L.", 0f, y, 240f, RowH, FontSize, bold: true, TextAlignment.MiddleRight));
        rh.Controls.Add(Etiqueta(Money(convenio.MontoTotal), 240f, y, 120f, RowH, FontSize, bold: true, TextAlignment.MiddleRight));
        y += RowH;

        // Columna derecha: las condiciones pactadas.
        var yDer = yBloque;
        yDer = Condicion(rh, yDer, "PRIMA L.", Money(convenio.Prima));
        yDer = Condicion(rh, yDer, "MONTO A FINANCIAR L.", Money(convenio.MontoFinanciado));
        yDer = Condicion(rh, yDer, "TASA %", Money(convenio.Tasa));
        yDer = Condicion(rh, yDer, "MESES", convenio.Meses.ToString(EsHn));
        yDer = Condicion(rh, yDer, "CUOTA L.", Money(convenio.ValorCuota));

        y = Math.Max(y, yDer) + 14f;

        // ---------- Texto legal ----------
        var conRepresentante = !string.IsNullOrWhiteSpace(convenio.Representante);
        var firmante = conRepresentante ? convenio.Representante! : convenio.ClienteNombre;
        var calidad = conRepresentante
            ? $"actuando en mi condición de representante de <b>{Mayus(convenio.ClienteNombre)}</b>, cliente"
            : "cliente";

        y = Parrafo(rh, y, Identificacion
            .Replace("{FIRMANTE}", Mayus(firmante))
            .Replace("{IDENTIDAD}", Dato(convenio.DocRepresentante))
            .Replace("{CALIDAD}", calidad)
            .Replace("{EMPRESA}", Dato(convenio.EmpresaNombre))
            .Replace("{CUENTA}", convenio.ClienteClave));

        y += 8f;
        y = Parrafo(rh, y, Constancia.Replace("{EMPRESA}", Dato(convenio.EmpresaNombre)));
        y += 12f;

        // ---------- Encabezado del plan de cuotas ----------
        var head = new XRTable
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Font = new DXFont(Fuente, FontSize, DXFontStyle.Bold),
            BackColor = Color.Gainsboro,
            Borders = BorderSide.Top | BorderSide.Bottom
        };
        head.BeginInit();
        var hrow = new XRTableRow();
        hrow.Cells.Add(Celda(70f, "CUOTA", TextAlignment.MiddleCenter));
        hrow.Cells.Add(Celda(150f, "VENCIMIENTO", TextAlignment.MiddleCenter));
        hrow.Cells.Add(Celda(130f, "CAPITAL", TextAlignment.MiddleRight));
        hrow.Cells.Add(Celda(130f, "INTERESES", TextAlignment.MiddleRight));
        hrow.Cells.Add(Celda(130f, "CUOTA L.", TextAlignment.MiddleRight));
        hrow.Cells.Add(Celda(140f, "SALDO L.", TextAlignment.MiddleRight));
        head.Rows.Add(hrow);
        head.EndInit();
        rh.Controls.Add(head);
        y += 20f;

        rh.HeightF = y;

        // ---------- Una fila por cuota ----------
        var detail = new DetailBand { HeightF = 17f };
        var tbl = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, ContentWidth, 17f),
            Font = new DXFont(Fuente, FontSize)
        };
        tbl.BeginInit();
        var row = new XRTableRow();
        row.Cells.Add(CeldaEnlazada(70f, "[Numero]", TextAlignment.MiddleCenter));
        row.Cells.Add(CeldaEnlazada(150f, "[Vencimiento]", TextAlignment.MiddleCenter));
        row.Cells.Add(CeldaEnlazada(130f, "[Capital]", TextAlignment.MiddleRight));
        row.Cells.Add(CeldaEnlazada(130f, "[Intereses]", TextAlignment.MiddleRight));
        row.Cells.Add(CeldaEnlazada(130f, "[Cuota]", TextAlignment.MiddleRight));
        row.Cells.Add(CeldaEnlazada(140f, "[Saldo]", TextAlignment.MiddleRight));
        tbl.Rows.Add(row);
        tbl.EndInit();
        detail.Controls.Add(tbl);

        // ---------- Observación, firmas y pie ----------
        // Al fondo de la última hoja: si no, con pocas cuotas las firmas quedan
        // colgando a media página.
        var rf = new ReportFooterBand { PrintAtBottom = true };
        float fy = 6f;

        rf.Controls.Add(Raya(0f, fy, ContentWidth, Color.Gray));
        fy += 10f;

        if (!string.IsNullOrWhiteSpace(convenio.Comentario))
        {
            rf.Controls.Add(Etiqueta(Mayus(convenio.Comentario), 0f, fy, ContentWidth, RowH, FontSize));
            fy += RowH;
        }

        fy += 70f;

        const float colAncho = 320f;
        const float colDerecha = ContentWidth - colAncho;

        rf.Controls.Add(Raya(0f, fy, colAncho, Color.Black));
        rf.Controls.Add(Raya(colDerecha, fy, colAncho, Color.Black));
        fy += 6f;

        // Quién firma por la empresa: su nombre cuando está configurado, y si no
        // el rótulo genérico de siempre.
        var firmaEmpresa = string.IsNullOrWhiteSpace(convenio.FirmanteCobranza)
            ? "Representante"
            : Mayus(convenio.FirmanteCobranza);

        rf.Controls.Add(Etiqueta(firmaEmpresa, 0f, fy, colAncho, RowH,
            FontSize, bold: !string.IsNullOrWhiteSpace(convenio.FirmanteCobranza), TextAlignment.MiddleCenter));
        rf.Controls.Add(Etiqueta(Mayus(firmante), colDerecha, fy, colAncho, RowH,
            FontSize, bold: true, TextAlignment.MiddleCenter));
        fy += RowH;

        rf.Controls.Add(Etiqueta("UNIDAD DE COBRANZA", 0f, fy, colAncho, RowH,
            FontSize, bold: false, TextAlignment.MiddleCenter));
        rf.Controls.Add(Etiqueta(Dato(convenio.DocRepresentante), colDerecha, fy, colAncho, RowH,
            FontSize, bold: false, TextAlignment.MiddleCenter));
        fy += RowH + 16f;

        var elaborado = $"Elaborado por: {Dato(convenio.ElaboradoPor)}";
        if (convenio.FechaElaboracion is { } cuando)
        {
            elaborado += $"    {cuando.ToString("dd/MM/yy hh:mm:ss tt", EsHn)}";
        }
        rf.Controls.Add(Etiqueta(elaborado, 0f, fy, ContentWidth, RowH, 8f));
        fy += RowH;

        rf.HeightF = fy;

        Bands.AddRange(new Band[] { rh, detail, rf });
    }

    // ---------------- Filas del plan ----------------

    /// <summary>Fila del plan de cuotas, con el saldo corrido que deja cada pago.</summary>
    private sealed class FilaCuota
    {
        public string Numero { get; set; } = string.Empty;
        public string Vencimiento { get; set; } = string.Empty;
        public string Capital { get; set; } = string.Empty;
        public string Intereses { get; set; } = string.Empty;
        public string Cuota { get; set; } = string.Empty;
        public string Saldo { get; set; } = string.Empty;
    }

    /// <summary>
    /// La primera fila es el arranque (cuota 00): no cobra nada y muestra la
    /// deuda financiada completa, como en el formato de la unidad de cobranza.
    /// </summary>
    private static List<FilaCuota> ArmarFilas(ConvenioImpresionDto convenio)
    {
        var filas = new List<FilaCuota>();
        var saldo = convenio.MontoFinanciado > 0m ? convenio.MontoFinanciado : convenio.MontoTotal;

        var primerVencimiento = convenio.Cuotas.Count > 0
            ? convenio.Cuotas[0].FechaVencimiento
            : convenio.FechaPrimerPago;

        filas.Add(new FilaCuota
        {
            Numero = "00",
            Vencimiento = Corta(primerVencimiento),
            Capital = Money(0m),
            Intereses = Money(0m),
            Cuota = Money(0m),
            Saldo = Money(saldo)
        });

        foreach (var cuota in convenio.Cuotas)
        {
            saldo -= cuota.Monto;

            filas.Add(new FilaCuota
            {
                Numero = cuota.Numero.ToString("00", EsHn),
                Vencimiento = Corta(cuota.FechaVencimiento),
                Capital = Money(cuota.Monto),
                Intereses = Money(0m),
                Cuota = Money(cuota.Monto),
                Saldo = Money(saldo)
            });
        }

        return filas;
    }

    // ---------------- Bloques ----------------

    /// <summary>Una condición del financiamiento: rótulo a la izquierda y valor a la derecha.</summary>
    private static float Condicion(Band band, float y, string rotulo, string valor)
    {
        band.Controls.Add(Etiqueta(rotulo, 420f, y, 210f, RowH, FontSize, bold: true));
        band.Controls.Add(Etiqueta(valor, 630f, y, 120f, RowH, FontSize, bold: false, TextAlignment.MiddleRight));
        return y + RowH + 2f;
    }

    /// <summary>
    /// Párrafo justificado con los datos resaltados. Va en texto enriquecido
    /// porque una etiqueta con resaltado en línea ignora el justificado.
    /// </summary>
    private static float Parrafo(Band band, float y, string textoConMarcas)
    {
        // El nombre de la empresa suele terminar en punto ("S.A. de C.V."), así
        // que al cerrar la frase queda doble; se colapsa.
        textoConMarcas = Regex.Replace(textoConMarcas, @"\.\.+", ".");

        var visible = Regex.Replace(textoConMarcas, "</?b>", string.Empty);

        var charsPorLinea = Math.Max(20, (int)(ContentWidth / (TextoSize * 0.70f)));
        var lineas = Math.Max(1, (int)Math.Ceiling(visible.Length / (double)charsPorLinea));
        var alto = lineas * TextoLineH + TextoLineH * 0.5f;

        band.Controls.Add(new XRRichText
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, alto),
            CanGrow = false,
            CanShrink = false,
            Html = "<p style=\"margin:0; text-align:justify; line-height:150%; " +
                   $"font-family:'{Fuente}'; font-size:{TextoSize}pt\">{textoConMarcas}</p>"
        });

        return y + alto;
    }

    // ---------------- Texto ----------------

    private static string Money(decimal value) => value.ToString("N2", EsHn);

    private static string Mayus(string? texto) => (texto ?? string.Empty).Trim().ToUpper(EsHn);

    private static string Corta(DateTime? fecha) => fecha?.ToString("dd/MM/yy", EsHn) ?? string.Empty;

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
            WordWrap = false
        };

    private static XRLine Raya(float x, float y, float w, Color color)
        => new() { BoundsF = new RectangleF(x, y, w, 2f), ForeColor = color };

    private static XRTableCell Celda(float width, string text, TextAlignment align)
        => new() { WidthF = width, Text = text, TextAlignment = align, Padding = new PaddingInfo(4, 4, 0, 0) };

    private static XRTableCell CeldaEnlazada(float width, string expression, TextAlignment align)
    {
        var cell = new XRTableCell
        {
            WidthF = width,
            TextAlignment = align,
            Padding = new PaddingInfo(4, 4, 0, 0)
        };
        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expression));
        return cell;
    }
}
