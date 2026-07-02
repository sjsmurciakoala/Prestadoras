using System.Globalization;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Cobranza;

namespace SIAD.Reports.Documentos;

/// <summary>
/// Genera los documentos (PDF) de las acciones de cobranza usando DevExpress Reporting.
/// El layout actual de la Carta de Cobranza Prejudicial es un PLACEHOLDER; reemplazar
/// por el formato oficial cuando esté disponible (solo se toca BuildCartaPrejudicial).
/// </summary>
public sealed class DocumentoCobranzaGenerator : IDocumentoCobranzaGenerator
{
    private static readonly CultureInfo Hn = CultureInfo.GetCultureInfo("es-HN");

    public bool Soporta(string documentoCodigo) =>
        documentoCodigo == DocumentosCobranzaCodigos.CartaCobranzaPrejudicial;

    public DocumentoGenerado Generar(string documentoCodigo, DocumentoCobranzaDatos datos)
    {
        using var report = documentoCodigo switch
        {
            DocumentosCobranzaCodigos.CartaCobranzaPrejudicial => BuildCartaPrejudicial(datos),
            _ => throw new NotSupportedException($"Documento de cobranza no soportado: {documentoCodigo}")
        };

        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        var nombre = $"carta-prejudicial-{datos.ClienteClave}-{datos.FechaEmision:yyyyMMdd}.pdf";
        return new DocumentoGenerado(nombre, stream.ToArray(), "application/pdf");
    }

    // ── PLACEHOLDER: Carta de Cobranza Prejudicial ───────────────────────────────
    private static XtraReport BuildCartaPrejudicial(DocumentoCobranzaDatos d)
    {
        var report = new XtraReport
        {
            Name = "CartaCobranzaPrejudicial",
            DisplayName = "Carta de Cobranza Prejudicial",
            Margins = new DXMargins(50, 50, 50, 50)
        };
        report.Bands.Clear();
        report.Bands.AddRange([new TopMarginBand(), new BottomMarginBand()]);

        var detail = new DetailBand { HeightF = 720f };
        float w = 700f;

        var fontTitulo = new DXFont("Arial", 16f, DXFontStyle.Bold);
        var fontBody = new DXFont("Arial", 11f);
        var fontBold = new DXFont("Arial", 11f, DXFontStyle.Bold);

        var total = d.TotalAdeudado.ToString("N2", Hn);
        var fecha = d.FechaEmision.ToString("d 'de' MMMM 'de' yyyy", Hn);

        var controles = new List<XRControl>
        {
            // Membrete (placeholder)
            Label(0, 0, w, 22, "[ MEMBRETE DE LA EMPRESA ]", new DXFont("Arial", 10f, DXFontStyle.Bold),
                  TextAlignment.MiddleCenter, Color.Gray),

            // Lugar y fecha
            Label(0, 50, w, 18, $"Tegucigalpa, {fecha}", fontBody, TextAlignment.MiddleRight),

            // Destinatario
            Label(0, 95, w, 18, $"Señor(a): {d.ClienteNombre}", fontBody),
            Label(0, 115, w, 18, $"Clave: {d.ClienteClave}", fontBody),
            Label(0, 135, w, 18, $"Dirección: {d.Direccion ?? "—"}", fontBody),

            // Título
            Label(0, 180, w, 26, "CARTA DE COBRANZA PREJUDICIAL", fontTitulo, TextAlignment.MiddleCenter),

            // Cuerpo
            Multiline(0, 230, w, 90,
                "Por este medio le comunicamos que a la fecha mantiene una deuda pendiente con " +
                "nuestra empresa por la suma de:", fontBody),

            Label(0, 320, w, 24, $"L. {total}", fontBold, TextAlignment.MiddleCenter),

            Multiline(0, 360, w, 110,
                $"Se le concede un plazo de {d.PlazoDias} días hábiles a partir de la presente " +
                "notificación para regularizar su situación y evitar el inicio de acciones " +
                "judiciales de cobro. De no atender este requerimiento, su caso será remitido " +
                "al departamento legal.", fontBody),

            // Cierre / firma
            Label(0, 540, w, 18, "Atentamente,", fontBody),
            Label(0, 610, 280, 1, "", fontBody, TextAlignment.TopLeft, Color.Black, BorderSide.Top),
            Label(0, 614, 280, 18, d.Firmante ?? "Departamento de Cobranzas", fontBody),
            Label(0, 632, 280, 18, "Departamento de Cobranzas", new DXFont("Arial", 9f), TextAlignment.TopLeft, Color.DimGray),
        };

        detail.Controls.AddRange(controles.ToArray());
        report.Bands.Add(detail);
        return report;
    }

    private static XRLabel Label(
        float x, float y, float width, float height, string text, DXFont font,
        TextAlignment align = TextAlignment.MiddleLeft, Color? color = null, BorderSide borders = BorderSide.None)
        => new()
        {
            BoundsF = new RectangleF(x, y, width, height),
            Font = font,
            Text = text,
            TextAlignment = align,
            ForeColor = color ?? Color.Black,
            Borders = borders,
            BorderColor = Color.Black,
            BorderWidth = borders == BorderSide.None ? 0f : 1f
        };

    private static XRLabel Multiline(float x, float y, float width, float height, string text, DXFont font)
        => new()
        {
            BoundsF = new RectangleF(x, y, width, height),
            Font = font,
            Text = text,
            Multiline = true,
            TextAlignment = TextAlignment.TopJustify
        };
}
