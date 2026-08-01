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

    // ── Carta de Cobranza Prejudicial (formato formal, pruebas operativas
    //    jul-2026: membrete real de la empresa + correlativo de avisos POR
    //    CLIENTE, alineado al estilo del requerimiento de pago en mora) ────────
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

        var detail = new DetailBand { HeightF = 740f };
        float w = 700f;

        var fontEmpresa = new DXFont("Times New Roman", 15f, DXFontStyle.Bold);
        var fontMembrete = new DXFont("Times New Roman", 9f);
        var fontTitulo = new DXFont("Times New Roman", 13f, DXFontStyle.Bold);
        var fontBody = new DXFont("Times New Roman", 11f);
        var fontBold = new DXFont("Times New Roman", 11f, DXFontStyle.Bold);

        var total = d.TotalAdeudado.ToString("N2", Hn);
        var fecha = d.FechaEmision.ToString("d 'de' MMMM 'de' yyyy", Hn);

        var controles = new List<XRControl>
        {
            // Membrete real de la empresa (mismo espíritu que el requerimiento)
            Label(0, 0, w, 22, d.EmpresaNombre ?? string.Empty, fontEmpresa, TextAlignment.MiddleCenter),
            Label(0, 24, w, 14, string.IsNullOrWhiteSpace(d.EmpresaRtn) ? string.Empty : $"RTN: {d.EmpresaRtn}",
                  fontMembrete, TextAlignment.MiddleCenter, Color.DimGray),
            Label(0, 38, w, 14, d.EmpresaDireccion ?? string.Empty, fontMembrete, TextAlignment.MiddleCenter, Color.DimGray),
            Label(0, 56, w, 4, "", fontMembrete, TextAlignment.MiddleCenter, Color.Black, BorderSide.Top),

            // Título + correlativo de avisos del cliente
            Label(0, 68, w, 22, "CARTA DE COBRANZA PREJUDICIAL", fontTitulo, TextAlignment.MiddleCenter),
            Label(0, 90, w, 18, $"AVISO N.º {Math.Max(d.NumeroAviso, 1)}", fontBold, TextAlignment.MiddleCenter),

            // Fecha
            Label(0, 120, w, 18, fecha, fontBody, TextAlignment.MiddleRight),

            // Destinatario
            Label(0, 150, w, 18, $"Señor(a): {d.ClienteNombre}", fontBold),
            Label(0, 168, w, 18, $"Cuenta No.: {d.ClienteClave}" +
                  (string.IsNullOrWhiteSpace(d.Medidor) ? string.Empty : $"     Medidor: {d.Medidor}"), fontBody),
            Label(0, 186, w, 18, $"Dirección: {d.Direccion ?? "—"}", fontBody),

            // Cuerpo
            Multiline(0, 222, w, 70,
                "Por este medio le comunicamos que, a la fecha de la presente, usted mantiene " +
                "una deuda pendiente con nuestra empresa por la suma de:", fontBody),

            Label(0, 292, w, 24, $"L. {total}", new DXFont("Times New Roman", 13f, DXFontStyle.Bold), TextAlignment.MiddleCenter),

            Multiline(0, 330, w, 110,
                $"Se le concede un plazo de {d.PlazoDias} días hábiles a partir de la presente " +
                "notificación para regularizar su situación y evitar el inicio de acciones " +
                "judiciales de cobro. De no atender este requerimiento, su caso será remitido " +
                "al departamento legal para los trámites correspondientes.", fontBody),

            Multiline(0, 445, w, 40,
                "Si a la fecha de recibo de la presente ya efectuó el pago, haga caso omiso de " +
                "este aviso y disculpe la molestia.", fontBody),

            // Cierre / firma
            Label(0, 520, w, 18, "Atentamente,", fontBody),
            Label(0, 600, 280, 1, "", fontBody, TextAlignment.TopLeft, Color.Black, BorderSide.Top),
            Label(0, 604, 280, 18, d.Firmante ?? "Departamento de Cobranzas", fontBody),
            Label(0, 622, 280, 18, "Departamento de Cobranzas", new DXFont("Times New Roman", 9f), TextAlignment.TopLeft, Color.DimGray),
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
