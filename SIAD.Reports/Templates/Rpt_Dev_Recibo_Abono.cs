using System.Drawing;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Caja;

namespace SIAD.Reports;

public sealed class Rpt_Dev_Recibo_Abono : XtraReport
{
    private const float ContentWidth = 275f;

    public Rpt_Dev_Recibo_Abono(ReciboAbonoDto datos)
    {
        PaperKind = DXPaperKind.Custom;
        PageWidth = 315;
        PageHeight = 2000;
        Margins = new DXMargins(20, 20, 10, 10);
        DataSource = datos.Lineas;
        RequestParameters = false;

        // ============================================================
        // REPORT HEADER
        // ============================================================
        var rh = new ReportHeaderBand();

        float y = 4f;

        if (datos.EmpresaLogo is { Length: > 0 })
        {
            using var ms = new System.IO.MemoryStream(datos.EmpresaLogo);
            var logo = new XRPictureBox
            {
                BoundsF = new RectangleF(87.5f, y, 100f, 60f),
                Sizing = ImageSizeMode.ZoomImage,
                Image = System.Drawing.Image.FromStream(ms)
            };
            rh.Controls.Add(logo);
            y += 68f;
        }

        rh.Controls.Add(CenteredLabel(datos.EmpresaNombre, y, 10f, bold: true));
        y += 16f;

        rh.Controls.Add(Dashed(y)); y += 10f;

        rh.Controls.Add(CenteredLabel($"No. Recibo   {datos.NumRecibo}", y, 11f, bold: true));
        y += 18f;

        rh.Controls.Add(Dashed(y)); y += 10f;

        y = AddRow(rh, "Periodo     :", datos.Periodo, y);
        y = AddRow(rh, "Fecha Emision:", datos.FechaEmision, y);
        y = AddRow(rh, "RTN Cliente  :", datos.RtnCliente, y);
        y = AddRow(rh, "Cuenta No.  :", datos.CuentaNo, y);
        y = AddRow(rh, "Propietario :", datos.Propietario, y);

        rh.Controls.Add(Dashed(y)); y += 10f;

        var dirLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 28f),
            Font = new DXFont("Courier New", 8f),
            Text = $"Direccion: {datos.Direccion}",
            WordWrap = true,
            Multiline = true
        };
        rh.Controls.Add(dirLabel);
        y += 32f;

        rh.Controls.Add(Dashed(y));
        rh.HeightF = y + 12f;

        // ============================================================
        // DETAIL BAND
        // ============================================================
        var detail = new DetailBand { HeightF = 20f };

        var tbl = new XRTable { BoundsF = new RectangleF(0f, 0f, ContentWidth, 20f) };
        tbl.BeginInit();
        var row = new XRTableRow();
        row.Cells.Add(CreateCell(150f, "[Descripcion]", TextAlignment.MiddleLeft));
        row.Cells.Add(CreateCell(30f, "[Moneda]", TextAlignment.MiddleCenter));
        row.Cells.Add(CreateCell(95f, "[Monto]", TextAlignment.MiddleRight, "{0:N2}"));
        tbl.Rows.Add(row);
        tbl.EndInit();
        detail.Controls.Add(tbl);

        // ============================================================
        // REPORT FOOTER
        // ============================================================
        var rf = new ReportFooterBand();
        float fy = 0f;

        // Fila Total
        var totalTable = new XRTable { BoundsF = new RectangleF(0f, fy, ContentWidth, 22f) };
        totalTable.BeginInit();
        var totalRow = new XRTableRow();

        var labelCell = new XRTableCell
        {
            WidthF = 180f,
            TextAlignment = TextAlignment.MiddleRight,
            Font = new DXFont("Courier New", 9f, DXFontStyle.Bold),
            Text = "Total L.:"
        };
        totalRow.Cells.Add(labelCell);

        var totalCell = new XRTableCell
        {
            WidthF = 95f,
            TextAlignment = TextAlignment.MiddleRight,
            Font = new DXFont("Courier New", 9f, DXFontStyle.Bold),
            Text = datos.Total.ToString("N2")
        };
        totalRow.Cells.Add(totalCell);
        totalTable.Rows.Add(totalRow);
        totalTable.EndInit();
        rf.Controls.Add(totalTable);
        fy += 24f;

        rf.Controls.Add(CenteredLabel($".... {datos.TotalEnLetras} ....", fy, 8f));
        fy += 18f;

        rf.Controls.Add(Dashed(fy)); fy += 10f;

        var sello = new XRLabel
        {
            BoundsF = new RectangleF(20f, fy, ContentWidth - 40f, 28f),
            Font = new DXFont("Courier New", 8f),
            Text = "Sello electronico Sustituye\nFirma y sello Manual del Cajero",
            TextAlignment = TextAlignment.MiddleCenter,
            Multiline = true,
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        rf.Controls.Add(sello);
        fy += 36f;

        fy = AddRowFooter(rf, "Cajero      :", datos.Cajero, fy);
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        fy = AddRowFooter(rf, "Fecha Pago  :", datos.FechaPago, fy);
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        fy = AddRowFooter(rf, "Num. Trans. :", datos.NumeroTransaccion.ToString(), fy);
        rf.Controls.Add(Dashed(fy)); fy += 10f;
        fy = AddRowFooter(rf, "Generado por:", datos.GeneradoPor, fy);

        rf.Controls.Add(Dashed(fy)); fy += 10f;
        rf.Controls.Add(CenteredLabel("Cliente        Copia Caja", fy, 8f));
        fy += 16f;

        rf.HeightF = fy + 4f;

        Bands.AddRange([rh, detail, rf]);
    }

    private static XRLabel CenteredLabel(string text, float y, float fontSize, bool bold = false)
        => new()
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 14f),
            Font = new DXFont("Courier New", fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            Text = text,
            TextAlignment = TextAlignment.MiddleCenter
        };

    private static XRLine Dashed(float y)
        => new()
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 4f),
            LineStyle = DevExpress.Drawing.DXDashStyle.Dash,
            ForeColor = Color.Gray
        };

    private static float AddRow(Band band, string label, string value, float y)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, y, 130f, 14f),
            Font = new DXFont("Courier New", 8f),
            Text = label,
            ForeColor = Color.DimGray
        });
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(132f, y, 143f, 14f),
            Font = new DXFont("Courier New", 8f, DXFontStyle.Bold),
            Text = value,
            TextAlignment = TextAlignment.MiddleRight
        });
        return y + 14f;
    }

    private static float AddRowFooter(Band band, string label, string value, float y)
        => AddRow(band, label, value, y) + 2f;

    private static XRTableCell CreateCell(float width, string bindingExpr, TextAlignment align,
        string? format = null, bool bold = false)
    {
        var cell = new XRTableCell
        {
            WidthF = width,
            TextAlignment = align,
            Font = new DXFont("Courier New", 8f, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        if (format is not null) cell.TextFormatString = format;
        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", bindingExpr));
        return cell;
    }
}
