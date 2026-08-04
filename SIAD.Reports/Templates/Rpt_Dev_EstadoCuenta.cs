using System.Drawing;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Clientes;

namespace SIAD.Reports;

/// <summary>
/// Estado de cuenta imprimible del cliente (pruebas operativas ago-2026:
/// solo existía en pantalla). Carta vertical; mismos movimientos y saldo
/// corrido de la pestaña Movimientos, incluidas NC/ND. Mismo patrón en
/// código que <see cref="Rpt_Dev_Nota"/>.
/// </summary>
public sealed class Rpt_Dev_EstadoCuenta : XtraReport
{
    private const float ContentWidth = 750f;

    public Rpt_Dev_EstadoCuenta(EstadoCuentaImpresionDto estado)
    {
        PaperKind = DXPaperKind.Letter;
        Margins = new DXMargins(50, 50, 40, 40);
        DataSource = estado.Movimientos;
        RequestParameters = false;

        // ============================================================
        // REPORT HEADER — membrete y datos del cliente
        // ============================================================
        var rh = new ReportHeaderBand();
        float y = 0f;

        if (!string.IsNullOrWhiteSpace(estado.EmpresaNombre))
        {
            rh.Controls.Add(Label(estado.EmpresaNombre!, 0f, y, ContentWidth, 20f, 12f, bold: true, TextAlignment.MiddleCenter));
            y += 22f;
        }
        if (!string.IsNullOrWhiteSpace(estado.EmpresaRtn))
        {
            rh.Controls.Add(Label($"RTN: {estado.EmpresaRtn}", 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }
        if (!string.IsNullOrWhiteSpace(estado.EmpresaDireccion))
        {
            rh.Controls.Add(Label(estado.EmpresaDireccion!, 0f, y, ContentWidth, 14f, 8f, false, TextAlignment.MiddleCenter, Color.DimGray));
            y += 14f;
        }

        y += 10f;
        rh.Controls.Add(Label("ESTADO DE CUENTA", 0f, y, ContentWidth, 22f, 14f, bold: true, TextAlignment.MiddleCenter));
        y += 26f;

        var rango = (estado.Desde, estado.Hasta) switch
        {
            (null, null) => "Histórico completo",
            ({ } d, null) => $"Desde el {d:dd/MM/yyyy}",
            (null, { } h) => $"Hasta el {h:dd/MM/yyyy}",
            ({ } d, { } h) => $"Del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}"
        };
        rh.Controls.Add(Label(rango, 0f, y, ContentWidth, 16f, 9f, false, TextAlignment.MiddleCenter, Color.DimGray));
        y += 20f;

        rh.Controls.Add(Line(y)); y += 8f;

        float col2 = 390f;
        rh.Controls.Add(Label($"Cuenta No.: {estado.ClienteClave}", 0f, y, 380f, 16f, 9f, bold: true));
        rh.Controls.Add(Label($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", col2, y, 360f, 16f, 9f));
        y += 16f;
        rh.Controls.Add(Label($"Cliente: {estado.ClienteNombre}", 0f, y, ContentWidth, 16f, 9f));
        y += 16f;
        if (!string.IsNullOrWhiteSpace(estado.ClienteDireccion))
        {
            rh.Controls.Add(Label($"Dirección: {estado.ClienteDireccion}", 0f, y, ContentWidth, 16f, 9f));
            y += 16f;
        }

        y += 6f;

        // Encabezado de la tabla de movimientos
        var head = new XRTable
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            BackColor = Color.Gainsboro
        };
        head.BeginInit();
        var hrow = new XRTableRow();
        hrow.Cells.Add(HeadCell(90f, "Fecha", TextAlignment.MiddleCenter));
        hrow.Cells.Add(HeadCell(120f, "Tipo", TextAlignment.MiddleLeft));
        hrow.Cells.Add(HeadCell(280f, "Descripción", TextAlignment.MiddleLeft));
        hrow.Cells.Add(HeadCell(125f, "Monto", TextAlignment.MiddleRight));
        hrow.Cells.Add(HeadCell(135f, "Saldo", TextAlignment.MiddleRight));
        head.Rows.Add(hrow);
        head.EndInit();
        rh.Controls.Add(head);
        y += 20f;

        rh.HeightF = y;

        // ============================================================
        // DETAIL — una fila por movimiento
        // ============================================================
        var detail = new DetailBand { HeightF = 16f };
        var tbl = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, ContentWidth, 16f),
            Font = new DXFont("Arial", 8f)
        };
        tbl.BeginInit();
        var row = new XRTableRow();
        row.Cells.Add(BoundCell(90f, "[Fecha]", TextAlignment.MiddleCenter, "{0:dd/MM/yyyy}"));
        row.Cells.Add(BoundCell(120f, "[Tipo]", TextAlignment.MiddleLeft));
        row.Cells.Add(BoundCell(280f, "[Descripcion]", TextAlignment.MiddleLeft));
        row.Cells.Add(BoundCell(125f, "[Monto]", TextAlignment.MiddleRight, "{0:n2}"));
        row.Cells.Add(BoundCell(135f, "[SaldoInline]", TextAlignment.MiddleRight, "{0:n2}"));
        tbl.Rows.Add(row);
        tbl.EndInit();
        detail.Controls.Add(tbl);

        // ============================================================
        // REPORT FOOTER — saldo al corte
        // ============================================================
        var rf = new ReportFooterBand();
        float fy = 8f;

        rf.Controls.Add(Line(fy)); fy += 8f;
        rf.Controls.Add(Label("SALDO AL CORTE DEL REPORTE:", 330f, fy, 250f, 18f, 10f, bold: true, TextAlignment.MiddleRight));
        rf.Controls.Add(Label($"L. {estado.SaldoFinal:N2}", 580f, fy, 170f, 18f, 10f, bold: true, TextAlignment.MiddleRight));
        fy += 26f;

        rf.Controls.Add(Label(
            "El saldo corrido de cada fila es el histórico real acumulado; el filtro de fechas solo acota las filas mostradas.",
            0f, fy, ContentWidth, 14f, 7f, false, TextAlignment.MiddleLeft, Color.DimGray));
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
}
