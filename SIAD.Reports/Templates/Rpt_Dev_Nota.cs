using System.Drawing;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.NotasCreditoDebito;

namespace SIAD.Reports;

/// <summary>
/// Documento imprimible de una Nota de Crédito / Débito (pruebas operativas
/// jul-2026: reimpresión y vista previa antes de guardar). Carta vertical;
/// todos los datos fiscales vienen tal como quedaron grabados en la nota.
/// En vista previa lleva marca de agua "VISTA PREVIA — SIN VALOR FISCAL"
/// (XRWatermark, dxdocs: Display Watermarks in a Report).
/// </summary>
public sealed class Rpt_Dev_Nota : XtraReport
{
    private const float ContentWidth = 750f;

    public Rpt_Dev_Nota(NotaImpresionDto nota)
    {
        PaperKind = DXPaperKind.Letter;
        Margins = new DXMargins(50, 50, 40, 40);
        DataSource = nota.Lineas;
        RequestParameters = false;

        if (nota.EsVistaPrevia)
        {
            var marca = new XRWatermark
            {
                Id = "VistaPrevia",
                Text = "VISTA PREVIA — SIN VALOR FISCAL",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont("Arial", 34, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 160,
                TextPosition = WatermarkPosition.InFront
            };
            Watermarks.Add(marca);
            WatermarkId = marca.Id;
        }

        // ============================================================
        // REPORT HEADER — emisor, documento y datos fiscales
        // ============================================================
        var rh = new ReportHeaderBand();
        float y = 0f;

        rh.Controls.Add(Label(nota.EmisorNombre, 0f, y, ContentWidth, 20f, 12f, bold: true, TextAlignment.MiddleCenter));
        y += 22f;
        rh.Controls.Add(Label($"RTN: {nota.EmisorRtn}", 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
        y += 14f;
        if (!string.IsNullOrWhiteSpace(nota.EmisorDireccion))
        {
            rh.Controls.Add(Label(nota.EmisorDireccion!, 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }

        y += 8f;
        rh.Controls.Add(Label(nota.TituloDocumento, 0f, y, ContentWidth, 22f, 13f, bold: true, TextAlignment.MiddleCenter));
        y += 24f;

        var numero = nota.EsVistaPrevia
            ? $"No. {nota.NumeroDocumento}  (PROVISIONAL — NO EMITIDO)"
            : $"No. {nota.NumeroDocumento}";
        rh.Controls.Add(Label(numero, 0f, y, ContentWidth, 18f, 10f, bold: true, TextAlignment.MiddleCenter));
        y += 22f;

        // Bloque fiscal CAI
        if (!string.IsNullOrWhiteSpace(nota.CodigoCai))
        {
            rh.Controls.Add(Label($"CAI: {nota.CodigoCai}", 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }
        if (!string.IsNullOrWhiteSpace(nota.LeyendaCaiRango))
        {
            rh.Controls.Add(Label(nota.LeyendaCaiRango!, 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }
        if (nota.FechaLimiteCai is { } limite)
        {
            rh.Controls.Add(Label($"Fecha límite de emisión: {limite:dd/MM/yyyy}", 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }

        y += 6f;
        rh.Controls.Add(Line(y)); y += 8f;

        // Receptor + referencia (dos columnas)
        float col2 = 390f;
        rh.Controls.Add(Label($"Cuenta No.: {nota.ClienteClave}", 0f, y, 380f, 16f, 9f, bold: true));
        rh.Controls.Add(Label($"Fecha emisión: {nota.FechaEmision:dd/MM/yyyy HH:mm}", col2, y, 360f, 16f, 9f));
        y += 16f;
        rh.Controls.Add(Label($"Cliente: {nota.ReceptorNombre}", 0f, y, 380f, 16f, 9f));
        rh.Controls.Add(Label($"Factura origen: {nota.FacturaOrigenNumero}", col2, y, 360f, 16f, 9f));
        y += 16f;
        rh.Controls.Add(Label($"RTN: {(string.IsNullOrWhiteSpace(nota.ReceptorRtn) ? "—" : nota.ReceptorRtn)}", 0f, y, 380f, 16f, 9f));
        if (nota.FacturaOrigenFecha is { } fo)
        {
            rh.Controls.Add(Label($"Fecha factura origen: {fo:dd/MM/yyyy}", col2, y, 360f, 16f, 9f));
        }
        y += 16f;
        if (!string.IsNullOrWhiteSpace(nota.ReceptorDireccion))
        {
            rh.Controls.Add(Label($"Dirección: {nota.ReceptorDireccion}", 0f, y, ContentWidth, 16f, 9f));
            y += 16f;
        }

        var motivo = string.IsNullOrWhiteSpace(nota.MotivoDetalle)
            ? nota.MotivoDescripcion
            : $"{nota.MotivoDescripcion} — {nota.MotivoDetalle}";
        rh.Controls.Add(Label($"Motivo: {motivo}", 0f, y, ContentWidth, 16f, 9f));
        y += 16f;

        if (nota.AnulaFacturaOrigen)
        {
            rh.Controls.Add(Label("ANULA LA FACTURA ORIGEN", 0f, y, ContentWidth, 16f, 9f, bold: true, foreColor: Color.Firebrick));
            y += 16f;
        }

        y += 4f;

        // Encabezado de la tabla de detalle
        var head = new XRTable
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            BackColor = Color.Gainsboro
        };
        head.BeginInit();
        var hrow = new XRTableRow();
        hrow.Cells.Add(HeadCell(390f, "Descripción", TextAlignment.MiddleLeft));
        hrow.Cells.Add(HeadCell(90f, "Cantidad", TextAlignment.MiddleCenter));
        hrow.Cells.Add(HeadCell(130f, "Precio", TextAlignment.MiddleRight));
        hrow.Cells.Add(HeadCell(140f, "Total", TextAlignment.MiddleRight));
        head.Rows.Add(hrow);
        head.EndInit();
        rh.Controls.Add(head);
        y += 20f;

        rh.HeightF = y;

        // ============================================================
        // DETAIL — una fila por línea de la nota
        // ============================================================
        var detail = new DetailBand { HeightF = 18f };
        var tbl = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, ContentWidth, 18f),
            Font = new DXFont("Arial", 9f)
        };
        tbl.BeginInit();
        var row = new XRTableRow();
        row.Cells.Add(BoundCell(390f, "[Descripcion]", TextAlignment.MiddleLeft));
        row.Cells.Add(BoundCell(90f, "[Cantidad]", TextAlignment.MiddleCenter, "{0:n0}"));
        row.Cells.Add(BoundCell(130f, "[MontoUnitario]", TextAlignment.MiddleRight, "{0:n2}"));
        row.Cells.Add(BoundCell(140f, "[MontoTotal]", TextAlignment.MiddleRight, "{0:n2}"));
        tbl.Rows.Add(row);
        tbl.EndInit();
        detail.Controls.Add(tbl);

        // ============================================================
        // REPORT FOOTER — totales y pie
        // ============================================================
        var rf = new ReportFooterBand();
        float fy = 6f;

        fy = TotalRow(rf, "Subtotal:", nota.SubTotal, fy, bold: false);
        fy = TotalRow(rf, "ISV:", nota.Isv, fy, bold: false);
        fy = TotalRow(rf, "TOTAL:", nota.Total, fy, bold: true);

        fy += 14f;
        rf.Controls.Add(Label($"Emitido por: {nota.UsuarioEmisor}", 0f, fy, 380f, 14f, 8f, false, TextAlignment.MiddleLeft, Color.DimGray));
        rf.Controls.Add(Label($"Impreso: {DateTime.Now:dd/MM/yyyy HH:mm}", 390f, fy, 360f, 14f, 8f, false, TextAlignment.MiddleRight, Color.DimGray));
        fy += 18f;

        rf.HeightF = fy;

        Bands.AddRange(new Band[] { rh, detail, rf });
    }

    private static XRLabel Label(
        string text, float x, float y, float w, float h, float size,
        bool bold = false, TextAlignment align = TextAlignment.MiddleLeft, Color? foreColor = null)
        => new()
        {
            BoundsF = new RectangleF(x, y, w, h),
            Text = text,
            Font = new DXFont("Arial", size, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = align,
            ForeColor = foreColor ?? Color.Black,
            WordWrap = true
        };

    private static XRLine Line(float y)
        => new() { BoundsF = new RectangleF(0f, y, ContentWidth, 4f), ForeColor = Color.Gainsboro };

    private static XRTableCell HeadCell(float width, string text, TextAlignment align)
        => new() { WidthF = width, Text = text, TextAlignment = align, Padding = new PaddingInfo(4, 4, 0, 0) };

    private static XRTableCell BoundCell(float width, string expression, TextAlignment align, string? format = null)
    {
        var cell = new XRTableCell
        {
            WidthF = width,
            TextAlignment = align,
            Padding = new PaddingInfo(4, 4, 0, 0)
        };
        if (format is not null) cell.TextFormatString = format;
        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expression));
        return cell;
    }

    private float TotalRow(ReportFooterBand rf, string etiqueta, decimal valor, float y, bool bold)
    {
        var estilo = bold ? DXFontStyle.Bold : DXFontStyle.Regular;
        var tamano = bold ? 10f : 9f;
        rf.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(470f, y, 140f, 18f),
            Text = etiqueta,
            Font = new DXFont("Arial", tamano, estilo),
            TextAlignment = TextAlignment.MiddleRight
        });
        rf.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(610f, y, 140f, 18f),
            Text = valor.ToString("N2"),
            Font = new DXFont("Arial", tamano, estilo),
            TextAlignment = TextAlignment.MiddleRight
        });
        return y + 20f;
    }
}
