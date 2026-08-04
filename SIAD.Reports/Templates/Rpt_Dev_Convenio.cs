using System.Drawing;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Cobranza;

namespace SIAD.Reports;

/// <summary>
/// Documento imprimible del convenio de pago (plan de cuotas) — pruebas
/// operativas ago-2026: no existía PDF del convenio. Carta vertical, membrete
/// de la empresa, resumen del plan, tabla de cuotas y espacio de firmas.
/// Mismo patrón en código que <see cref="Rpt_Dev_Nota"/>.
/// </summary>
public sealed class Rpt_Dev_Convenio : XtraReport
{
    private const float ContentWidth = 750f;

    public Rpt_Dev_Convenio(ConvenioImpresionDto convenio)
    {
        PaperKind = DXPaperKind.Letter;
        Margins = new DXMargins(50, 50, 40, 40);
        DataSource = convenio.Cuotas;
        RequestParameters = false;

        // ============================================================
        // REPORT HEADER — membrete, título y datos del convenio
        // ============================================================
        var rh = new ReportHeaderBand();
        float y = 0f;

        if (!string.IsNullOrWhiteSpace(convenio.EmpresaNombre))
        {
            rh.Controls.Add(Label(convenio.EmpresaNombre!, 0f, y, ContentWidth, 20f, 12f, bold: true, TextAlignment.MiddleCenter));
            y += 22f;
        }
        if (!string.IsNullOrWhiteSpace(convenio.EmpresaRtn))
        {
            rh.Controls.Add(Label($"RTN: {convenio.EmpresaRtn}", 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }
        if (!string.IsNullOrWhiteSpace(convenio.EmpresaDireccion))
        {
            rh.Controls.Add(Label(convenio.EmpresaDireccion!, 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }

        y += 10f;
        rh.Controls.Add(Label("CONVENIO DE PAGO", 0f, y, ContentWidth, 22f, 14f, bold: true, TextAlignment.MiddleCenter));
        y += 26f;
        rh.Controls.Add(Label($"No. {convenio.Correlativo ?? convenio.PlanId.ToString()}  ·  Estado: {convenio.EstadoTexto}", 0f, y, ContentWidth, 16f, 10f, bold: true, TextAlignment.MiddleCenter));
        y += 22f;

        rh.Controls.Add(Line(y)); y += 8f;

        float col2 = 390f;
        rh.Controls.Add(Label($"Cuenta No.: {convenio.ClienteClave}", 0f, y, 380f, 16f, 9f, bold: true));
        rh.Controls.Add(Label($"Fecha del convenio: {convenio.FechaCreacion:dd/MM/yyyy}", col2, y, 360f, 16f, 9f));
        y += 16f;
        rh.Controls.Add(Label($"Cliente: {convenio.ClienteNombre}", 0f, y, 380f, 16f, 9f));
        if (convenio.FechaPrimerPago is { } fpp)
        {
            rh.Controls.Add(Label($"Primer pago: {fpp:dd/MM/yyyy}", col2, y, 360f, 16f, 9f));
        }
        y += 16f;
        if (!string.IsNullOrWhiteSpace(convenio.ClienteDireccion))
        {
            rh.Controls.Add(Label($"Dirección: {convenio.ClienteDireccion}", 0f, y, ContentWidth, 16f, 9f));
            y += 16f;
        }
        if (!string.IsNullOrWhiteSpace(convenio.Representante))
        {
            var doc = string.IsNullOrWhiteSpace(convenio.DocRepresentante) ? string.Empty : $" (ID: {convenio.DocRepresentante})";
            rh.Controls.Add(Label($"Representante: {convenio.Representante}{doc}", 0f, y, ContentWidth, 16f, 9f));
            y += 16f;
        }

        y += 6f;

        // Resumen del plan
        rh.Controls.Add(Label($"Monto total: L. {convenio.MontoTotal:N2}", 0f, y, 250f, 16f, 9f, bold: true));
        rh.Controls.Add(Label($"Prima: L. {convenio.Prima:N2}", 250f, y, 240f, 16f, 9f));
        rh.Controls.Add(Label($"Financiado: L. {convenio.MontoFinanciado:N2} en {convenio.Meses} cuotas", 490f, y, 260f, 16f, 9f));
        y += 20f;

        if (!string.IsNullOrWhiteSpace(convenio.Comentario))
        {
            rh.Controls.Add(Label($"Observaciones: {convenio.Comentario}", 0f, y, ContentWidth, 28f, 8f, false, TextAlignment.TopLeft, Color.DimGray));
            y += 30f;
        }

        // Encabezado de la tabla de cuotas
        var head = new XRTable
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            BackColor = Color.Gainsboro
        };
        head.BeginInit();
        var hrow = new XRTableRow();
        hrow.Cells.Add(HeadCell(90f, "Cuota", TextAlignment.MiddleCenter));
        hrow.Cells.Add(HeadCell(170f, "Vence", TextAlignment.MiddleCenter));
        hrow.Cells.Add(HeadCell(170f, "Monto", TextAlignment.MiddleRight));
        hrow.Cells.Add(HeadCell(170f, "Saldo", TextAlignment.MiddleRight));
        hrow.Cells.Add(HeadCell(150f, "Estado", TextAlignment.MiddleCenter));
        head.Rows.Add(hrow);
        head.EndInit();
        rh.Controls.Add(head);
        y += 20f;

        rh.HeightF = y;

        // ============================================================
        // DETAIL — una fila por cuota
        // ============================================================
        var detail = new DetailBand { HeightF = 18f };
        var tbl = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, ContentWidth, 18f),
            Font = new DXFont("Arial", 9f)
        };
        tbl.BeginInit();
        var row = new XRTableRow();
        row.Cells.Add(BoundCell(90f, "[Numero]", TextAlignment.MiddleCenter));
        row.Cells.Add(BoundCell(170f, "[FechaVencimiento]", TextAlignment.MiddleCenter, "{0:dd/MM/yyyy}"));
        row.Cells.Add(BoundCell(170f, "[Monto]", TextAlignment.MiddleRight, "{0:n2}"));
        row.Cells.Add(BoundCell(170f, "[Saldo]", TextAlignment.MiddleRight, "{0:n2}"));
        row.Cells.Add(BoundCell(150f, "[EstadoTexto]", TextAlignment.MiddleCenter));
        tbl.Rows.Add(row);
        tbl.EndInit();
        detail.Controls.Add(tbl);

        // ============================================================
        // REPORT FOOTER — saldo pendiente y firmas
        // ============================================================
        var rf = new ReportFooterBand();
        float fy = 8f;

        rf.Controls.Add(Label("SALDO PENDIENTE:", 380f, fy, 200f, 18f, 10f, bold: true, TextAlignment.MiddleRight));
        rf.Controls.Add(Label($"L. {convenio.SaldoPendiente:N2}", 580f, fy, 170f, 18f, 10f, bold: true, TextAlignment.MiddleRight));
        fy += 30f;

        rf.Controls.Add(Label(
            "El cliente se compromete a pagar las cuotas en las fechas establecidas. " +
            "El incumplimiento del convenio habilita las gestiones de cobro y corte del servicio.",
            0f, fy, ContentWidth, 28f, 8f, false, TextAlignment.TopLeft, Color.DimGray));
        fy += 46f;

        // Firmas
        rf.Controls.Add(new XRLine { BoundsF = new RectangleF(40f, fy, 280f, 2f), ForeColor = Color.Black });
        rf.Controls.Add(new XRLine { BoundsF = new RectangleF(430f, fy, 280f, 2f), ForeColor = Color.Black });
        fy += 4f;
        var firmaCliente = string.IsNullOrWhiteSpace(convenio.Representante) ? convenio.ClienteNombre : convenio.Representante!;
        rf.Controls.Add(Label(firmaCliente, 40f, fy, 280f, 14f, 8f, false, TextAlignment.MiddleCenter));
        rf.Controls.Add(Label("Por la empresa", 430f, fy, 280f, 14f, 8f, false, TextAlignment.MiddleCenter));
        fy += 14f;
        rf.Controls.Add(Label("Cliente / Representante", 40f, fy, 280f, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
        fy += 20f;

        rf.Controls.Add(Label($"Impreso: {DateTime.Now:dd/MM/yyyy HH:mm}", 0f, fy, ContentWidth, 14f, 8f, false, TextAlignment.MiddleRight, Color.DimGray));
        fy += 16f;

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
}
