using System;
using System.ComponentModel;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;

namespace SIAD.Reports;

public partial class Rpt_Dev_Saldo_Clientes_Categoria : XtraReport
{
    private static readonly DXFont DetailFont = new("Times New Roman", 8F);
    private static readonly DXFont BoldFont = new("Times New Roman", 8F, DXFontStyle.Bold);

    public Rpt_Dev_Saldo_Clientes_Categoria()
    {
        InitializeComponent();
    }

    private void TblDetail_BeforePrint(object sender, CancelEventArgs e)
    {
        var rowKind = Convert.ToString(GetCurrentColumnValue("row_kind")) ?? string.Empty;
        var isGroupHeader = string.Equals(rowKind, "group_header", StringComparison.OrdinalIgnoreCase);
        var isSubtotal = string.Equals(rowKind, "subtotal", StringComparison.OrdinalIgnoreCase);
        var isGrandTotal = string.Equals(rowKind, "grand_total", StringComparison.OrdinalIgnoreCase);

        tblDetail.Font = isGroupHeader || isSubtotal || isGrandTotal ? BoldFont : DetailFont;
        tblDetail.Borders = isGrandTotal
            ? BorderSide.Top | BorderSide.Bottom
            : isSubtotal
                ? BorderSide.Top
                : BorderSide.None;

        cellCodigo.Padding = new PaddingInfo(2, 2, 0, 0, 100F);
        cellCategoria.Padding = isGroupHeader
            ? new PaddingInfo(0, 2, 0, 0, 100F)
            : new PaddingInfo(2, 2, 0, 0, 100F);
    }
}
