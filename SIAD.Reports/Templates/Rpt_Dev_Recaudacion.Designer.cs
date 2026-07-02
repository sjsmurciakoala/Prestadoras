namespace SIAD.Reports
{
    partial class Rpt_Dev_Recaudacion
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            DevExpress.DataAccess.Sql.CustomSqlQuery customSqlQuery1 = new DevExpress.DataAccess.Sql.CustomSqlQuery();
            DevExpress.DataAccess.Sql.QueryParameter queryParameter1 = new DevExpress.DataAccess.Sql.QueryParameter();
            DevExpress.DataAccess.Sql.QueryParameter queryParameter2 = new DevExpress.DataAccess.Sql.QueryParameter();
            DevExpress.DataAccess.Sql.QueryParameter queryParameter3 = new DevExpress.DataAccess.Sql.QueryParameter();
            DevExpress.DataAccess.Sql.QueryParameter queryParameter4 = new DevExpress.DataAccess.Sql.QueryParameter();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Recaudacion));
            DevExpress.XtraReports.UI.XRSummary xrSummary1 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary2 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary3 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary4 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary5 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary6 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary7 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary8 = new DevExpress.XtraReports.UI.XRSummary();
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblFechaReporte = new DevExpress.XtraReports.UI.XRPageInfo();
            this.lblPaginaTexto = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaNumero = new DevExpress.XtraReports.UI.XRPageInfo();
            this.headerLine = new DevExpress.XtraReports.UI.XRLine();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRecibo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderNombre = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRecuperacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderIngresosMes = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellRecibo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellNombre = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellRecuperacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellIngresosMes = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.GroupHeaderMedioPago = new DevExpress.XtraReports.UI.GroupHeaderBand();
            this.lblMedioPagoHeader = new DevExpress.XtraReports.UI.XRLabel();
            this.GroupFooterMedioPago = new DevExpress.XtraReports.UI.GroupFooterBand();
            this.tblGroupFooter = new DevExpress.XtraReports.UI.XRTable();
            this.tblGroupFooterRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellGroupFooterRecibosLabel = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupFooterRecibosCount = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupFooterMedioPagoLabel = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupFooterRecuperacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupFooterIngresosMes = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupFooterTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.lblTotalAbonadosLabel = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalAbonadosCount = new DevExpress.XtraReports.UI.XRLabel();
            this.tblReportFooter = new DevExpress.XtraReports.UI.XRTable();
            this.tblReportFooterRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellReportFooterLabel = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellReportFooterRecuperacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellReportFooterIngresosMes = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellReportFooterTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Desde = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Hasta = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Medio_Pago_Codigo = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblGroupFooter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblReportFooter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // TopMargin
            // 
            this.TopMargin.HeightF = 18F;
            this.TopMargin.Name = "TopMargin";
            // 
            // BottomMargin
            // 
            this.BottomMargin.HeightF = 18F;
            this.BottomMargin.Name = "BottomMargin";
            // 
            // ReportHeader
            // 
            this.ReportHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblFechaReporte,
            this.lblPaginaTexto,
            this.lblPaginaNumero,
            this.headerLine,
            this.lblEmpresa,
            this.lblTitulo});
            this.ReportHeader.HeightF = 92F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblFechaReporte
            // 
            this.lblFechaReporte.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F);
            this.lblFechaReporte.LocationFloat = new DevExpress.Utils.PointFloat(610F, 2F);
            this.lblFechaReporte.Name = "lblFechaReporte";
            this.lblFechaReporte.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            this.lblFechaReporte.SizeF = new System.Drawing.SizeF(120F, 20F);
            this.lblFechaReporte.StylePriority.UseFont = false;
            this.lblFechaReporte.StylePriority.UseTextAlignment = false;
            this.lblFechaReporte.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblFechaReporte.TextFormatString = "{0:dd/MM/yy}";
            // 
            // lblPaginaTexto
            // 
            this.lblPaginaTexto.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F);
            this.lblPaginaTexto.LocationFloat = new DevExpress.Utils.PointFloat(610F, 22F);
            this.lblPaginaTexto.Name = "lblPaginaTexto";
            this.lblPaginaTexto.SizeF = new System.Drawing.SizeF(64F, 20F);
            this.lblPaginaTexto.StylePriority.UseFont = false;
            this.lblPaginaTexto.StylePriority.UseTextAlignment = false;
            this.lblPaginaTexto.Text = "PAG.";
            this.lblPaginaTexto.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // lblPaginaNumero
            // 
            this.lblPaginaNumero.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F);
            this.lblPaginaNumero.LocationFloat = new DevExpress.Utils.PointFloat(682F, 22F);
            this.lblPaginaNumero.Name = "lblPaginaNumero";
            this.lblPaginaNumero.PageInfo = DevExpress.XtraPrinting.PageInfo.Number;
            this.lblPaginaNumero.SizeF = new System.Drawing.SizeF(48F, 20F);
            this.lblPaginaNumero.StylePriority.UseFont = false;
            this.lblPaginaNumero.StylePriority.UseTextAlignment = false;
            this.lblPaginaNumero.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblPaginaNumero.TextFormatString = "{0}";
            // 
            // headerLine
            // 
            this.headerLine.LineWidth = 2F;
            this.headerLine.LocationFloat = new DevExpress.Utils.PointFloat(8F, 44F);
            this.headerLine.Name = "headerLine";
            this.headerLine.SizeF = new System.Drawing.SizeF(722F, 6F);
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([empresa_nombre])")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(115F, 54F);
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(470F, 24F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[titulo_reporte]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(44F, 78F);
            this.lblTitulo.Multiline = true;
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(642F, 14F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // PageHeader
            // 
            this.PageHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblHeader});
            this.PageHeader.HeightF = 35F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.BorderWidth = 2F;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(8F, 5F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(722F, 25F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseBorderWidth = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderFecha,
            this.cellHeaderRecibo,
            this.cellHeaderCliente,
            this.cellHeaderNombre,
            this.cellHeaderRecuperacion,
            this.cellHeaderIngresosMes,
            this.cellHeaderTotal});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderFecha
            // 
            this.cellHeaderFecha.Name = "cellHeaderFecha";
            this.cellHeaderFecha.StylePriority.UseTextAlignment = false;
            this.cellHeaderFecha.Text = "FECHA";
            this.cellHeaderFecha.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellHeaderFecha.Weight = 0.95D;
            // 
            // cellHeaderRecibo
            // 
            this.cellHeaderRecibo.Name = "cellHeaderRecibo";
            this.cellHeaderRecibo.StylePriority.UseTextAlignment = false;
            this.cellHeaderRecibo.Text = "RECIBO";
            this.cellHeaderRecibo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellHeaderRecibo.Weight = 1.05D;
            // 
            // cellHeaderCliente
            // 
            this.cellHeaderCliente.Name = "cellHeaderCliente";
            this.cellHeaderCliente.Text = "CLIENTE";
            this.cellHeaderCliente.Weight = 1.15D;
            // 
            // cellHeaderNombre
            // 
            this.cellHeaderNombre.Name = "cellHeaderNombre";
            this.cellHeaderNombre.Text = "NOMBRE DEL CLIENTE";
            this.cellHeaderNombre.Weight = 3.1D;
            // 
            // cellHeaderRecuperacion
            // 
            this.cellHeaderRecuperacion.Name = "cellHeaderRecuperacion";
            this.cellHeaderRecuperacion.StylePriority.UseTextAlignment = false;
            this.cellHeaderRecuperacion.Text = "RECUPERACION";
            this.cellHeaderRecuperacion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderRecuperacion.Weight = 1.1D;
            // 
            // cellHeaderIngresosMes
            // 
            this.cellHeaderIngresosMes.Name = "cellHeaderIngresosMes";
            this.cellHeaderIngresosMes.StylePriority.UseTextAlignment = false;
            this.cellHeaderIngresosMes.Text = "INGRESOS MES";
            this.cellHeaderIngresosMes.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderIngresosMes.Weight = 1.1D;
            // 
            // cellHeaderTotal
            // 
            this.cellHeaderTotal.Name = "cellHeaderTotal";
            this.cellHeaderTotal.StylePriority.UseTextAlignment = false;
            this.cellHeaderTotal.Text = "Total";
            this.cellHeaderTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderTotal.Weight = 1.1D;
            // 
            // Detail
            // 
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblDetail});
            this.Detail.HeightF = 18F;
            this.Detail.Name = "Detail";
            // 
            // tblDetail
            // 
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8.8F);
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(8F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(722F, 18F);
            this.tblDetail.StylePriority.UseFont = false;
            this.tblDetail.StylePriority.UseTextAlignment = false;
            this.tblDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellFecha,
            this.cellRecibo,
            this.cellCliente,
            this.cellNombre,
            this.cellRecuperacion,
            this.cellIngresosMes,
            this.cellTotal});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellFecha
            // 
            this.cellFecha.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha]")});
            this.cellFecha.Name = "cellFecha";
            this.cellFecha.StyleName = "DetailData";
            this.cellFecha.StylePriority.UseTextAlignment = false;
            this.cellFecha.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellFecha.TextFormatString = "{0:dd/MM/yyyy}";
            this.cellFecha.Weight = 0.95D;
            // 
            // cellRecibo
            // 
            this.cellRecibo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[recibo]")});
            this.cellRecibo.Name = "cellRecibo";
            this.cellRecibo.StyleName = "DetailData";
            this.cellRecibo.StylePriority.UseTextAlignment = false;
            this.cellRecibo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellRecibo.Weight = 1.05D;
            // 
            // cellCliente
            // 
            this.cellCliente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_codigo]")});
            this.cellCliente.Name = "cellCliente";
            this.cellCliente.StyleName = "DetailData";
            this.cellCliente.Weight = 1.15D;
            // 
            // cellNombre
            // 
            this.cellNombre.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_nombre]")});
            this.cellNombre.Name = "cellNombre";
            this.cellNombre.StyleName = "DetailData";
            this.cellNombre.Weight = 3.1D;
            // 
            // cellRecuperacion
            // 
            this.cellRecuperacion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[recuperacion]")});
            this.cellRecuperacion.Name = "cellRecuperacion";
            this.cellRecuperacion.StyleName = "DetailData";
            this.cellRecuperacion.StylePriority.UseTextAlignment = false;
            this.cellRecuperacion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellRecuperacion.TextFormatString = "{0:n2}";
            this.cellRecuperacion.Weight = 1.1D;
            // 
            // cellIngresosMes
            // 
            this.cellIngresosMes.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ingresos_mes]")});
            this.cellIngresosMes.Name = "cellIngresosMes";
            this.cellIngresosMes.StyleName = "DetailData";
            this.cellIngresosMes.StylePriority.UseTextAlignment = false;
            this.cellIngresosMes.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellIngresosMes.TextFormatString = "{0:n2}";
            this.cellIngresosMes.Weight = 1.1D;
            // 
            // cellTotal
            // 
            this.cellTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_fila]")});
            this.cellTotal.Name = "cellTotal";
            this.cellTotal.StyleName = "DetailData";
            this.cellTotal.StylePriority.UseTextAlignment = false;
            this.cellTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal.TextFormatString = "{0:n2}";
            this.cellTotal.Weight = 1.1D;
            // 
            // GroupHeaderMedioPago
            // 
            this.GroupHeaderMedioPago.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblMedioPagoHeader});
            this.GroupHeaderMedioPago.GroupFields.AddRange(new DevExpress.XtraReports.UI.GroupField[] {
            new DevExpress.XtraReports.UI.GroupField("medio_pago_codigo", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending)});
            this.GroupHeaderMedioPago.HeightF = 32F;
            this.GroupHeaderMedioPago.Name = "GroupHeaderMedioPago";
            // 
            // lblMedioPagoHeader
            // 
            this.lblMedioPagoHeader.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[medio_pago_nombre]")});
            this.lblMedioPagoHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblMedioPagoHeader.LocationFloat = new DevExpress.Utils.PointFloat(8F, 10F);
            this.lblMedioPagoHeader.Name = "lblMedioPagoHeader";
            this.lblMedioPagoHeader.SizeF = new System.Drawing.SizeF(400F, 18F);
            this.lblMedioPagoHeader.StylePriority.UseFont = false;
            // 
            // GroupFooterMedioPago
            // 
            this.GroupFooterMedioPago.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblGroupFooter});
            this.GroupFooterMedioPago.HeightF = 35F;
            this.GroupFooterMedioPago.Name = "GroupFooterMedioPago";
            // 
            // tblGroupFooter
            // 
            this.tblGroupFooter.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblGroupFooter.BorderWidth = 1F;
            this.tblGroupFooter.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblGroupFooter.LocationFloat = new DevExpress.Utils.PointFloat(8F, 4F);
            this.tblGroupFooter.Name = "tblGroupFooter";
            this.tblGroupFooter.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblGroupFooterRow});
            this.tblGroupFooter.SizeF = new System.Drawing.SizeF(722F, 24F);
            this.tblGroupFooter.StylePriority.UseBorders = false;
            this.tblGroupFooter.StylePriority.UseBorderWidth = false;
            this.tblGroupFooter.StylePriority.UseFont = false;
            this.tblGroupFooter.StylePriority.UseTextAlignment = false;
            this.tblGroupFooter.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // tblGroupFooterRow
            // 
            this.tblGroupFooterRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellGroupFooterRecibosLabel,
            this.cellGroupFooterRecibosCount,
            this.cellGroupFooterMedioPagoLabel,
            this.cellGroupFooterRecuperacion,
            this.cellGroupFooterIngresosMes,
            this.cellGroupFooterTotal});
            this.tblGroupFooterRow.Name = "tblGroupFooterRow";
            this.tblGroupFooterRow.Weight = 1D;
            // 
            // cellGroupFooterRecibosLabel
            // 
            this.cellGroupFooterRecibosLabel.Name = "cellGroupFooterRecibosLabel";
            this.cellGroupFooterRecibosLabel.Text = "No. Recibos";
            this.cellGroupFooterRecibosLabel.Weight = 0.95D;
            // 
            // cellGroupFooterRecibosCount
            // 
            this.cellGroupFooterRecibosCount.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumCount([recibo])")});
            this.cellGroupFooterRecibosCount.Name = "cellGroupFooterRecibosCount";
            this.cellGroupFooterRecibosCount.StylePriority.UseTextAlignment = false;
            this.cellGroupFooterRecibosCount.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupFooterRecibosCount.Summary = xrSummary1;
            this.cellGroupFooterRecibosCount.Weight = 0.55D;
            // 
            // cellGroupFooterMedioPagoLabel
            // 
            this.cellGroupFooterMedioPagoLabel.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "\'Total \' + [medio_pago_nombre]")});
            this.cellGroupFooterMedioPagoLabel.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.cellGroupFooterMedioPagoLabel.Name = "cellGroupFooterMedioPagoLabel";
            this.cellGroupFooterMedioPagoLabel.StylePriority.UseFont = false;
            this.cellGroupFooterMedioPagoLabel.StylePriority.UseTextAlignment = false;
            this.cellGroupFooterMedioPagoLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellGroupFooterMedioPagoLabel.Weight = 3.85D;
            // 
            // cellGroupFooterRecuperacion
            // 
            this.cellGroupFooterRecuperacion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([recuperacion])")});
            this.cellGroupFooterRecuperacion.Name = "cellGroupFooterRecuperacion";
            this.cellGroupFooterRecuperacion.StylePriority.UseTextAlignment = false;
            this.cellGroupFooterRecuperacion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            xrSummary2.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupFooterRecuperacion.Summary = xrSummary2;
            this.cellGroupFooterRecuperacion.TextFormatString = "{0:n2}";
            this.cellGroupFooterRecuperacion.Weight = 1.1D;
            // 
            // cellGroupFooterIngresosMes
            // 
            this.cellGroupFooterIngresosMes.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([ingresos_mes])")});
            this.cellGroupFooterIngresosMes.Name = "cellGroupFooterIngresosMes";
            this.cellGroupFooterIngresosMes.StylePriority.UseTextAlignment = false;
            this.cellGroupFooterIngresosMes.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            xrSummary3.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupFooterIngresosMes.Summary = xrSummary3;
            this.cellGroupFooterIngresosMes.TextFormatString = "{0:n2}";
            this.cellGroupFooterIngresosMes.Weight = 1.1D;
            // 
            // cellGroupFooterTotal
            // 
            this.cellGroupFooterTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([total_fila])")});
            this.cellGroupFooterTotal.Name = "cellGroupFooterTotal";
            this.cellGroupFooterTotal.StylePriority.UseTextAlignment = false;
            this.cellGroupFooterTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            xrSummary4.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupFooterTotal.Summary = xrSummary4;
            this.cellGroupFooterTotal.TextFormatString = "{0:n2}";
            this.cellGroupFooterTotal.Weight = 1.1D;
            // 
            // ReportFooter
            // 
            this.ReportFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblTotalAbonadosLabel,
            this.lblTotalAbonadosCount,
            this.tblReportFooter});
            this.ReportFooter.HeightF = 75F;
            this.ReportFooter.Name = "ReportFooter";
            // 
            // lblTotalAbonadosLabel
            // 
            this.lblTotalAbonadosLabel.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTotalAbonadosLabel.LocationFloat = new DevExpress.Utils.PointFloat(130F, 40F);
            this.lblTotalAbonadosLabel.Name = "lblTotalAbonadosLabel";
            this.lblTotalAbonadosLabel.SizeF = new System.Drawing.SizeF(200F, 22F);
            this.lblTotalAbonadosLabel.StylePriority.UseFont = false;
            this.lblTotalAbonadosLabel.StylePriority.UseTextAlignment = false;
            this.lblTotalAbonadosLabel.Text = "TOTAL ABONADOS :";
            this.lblTotalAbonadosLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // lblTotalAbonadosCount
            // 
            this.lblTotalAbonadosCount.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumDistinctCount([cliente_codigo])")});
            this.lblTotalAbonadosCount.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTotalAbonadosCount.LocationFloat = new DevExpress.Utils.PointFloat(340F, 40F);
            this.lblTotalAbonadosCount.Name = "lblTotalAbonadosCount";
            this.lblTotalAbonadosCount.SizeF = new System.Drawing.SizeF(100F, 22F);
            this.lblTotalAbonadosCount.StylePriority.UseFont = false;
            this.lblTotalAbonadosCount.StylePriority.UseTextAlignment = false;
            this.lblTotalAbonadosCount.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            xrSummary5.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.lblTotalAbonadosCount.Summary = xrSummary5;
            // 
            // tblReportFooter
            // 
            this.tblReportFooter.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblReportFooter.BorderWidth = 1F;
            this.tblReportFooter.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblReportFooter.LocationFloat = new DevExpress.Utils.PointFloat(8F, 5F);
            this.tblReportFooter.Name = "tblReportFooter";
            this.tblReportFooter.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblReportFooterRow});
            this.tblReportFooter.SizeF = new System.Drawing.SizeF(722F, 25F);
            this.tblReportFooter.StylePriority.UseBorders = false;
            this.tblReportFooter.StylePriority.UseBorderWidth = false;
            this.tblReportFooter.StylePriority.UseFont = false;
            this.tblReportFooter.StylePriority.UseTextAlignment = false;
            this.tblReportFooter.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // tblReportFooterRow
            // 
            this.tblReportFooterRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellReportFooterLabel,
            this.cellReportFooterRecuperacion,
            this.cellReportFooterIngresosMes,
            this.cellReportFooterTotal});
            this.tblReportFooterRow.Name = "tblReportFooterRow";
            this.tblReportFooterRow.Weight = 1D;
            // 
            // cellReportFooterLabel
            // 
            this.cellReportFooterLabel.Borders = DevExpress.XtraPrinting.BorderSide.All;
            this.cellReportFooterLabel.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellReportFooterLabel.Name = "cellReportFooterLabel";
            this.cellReportFooterLabel.StylePriority.UseBorders = false;
            this.cellReportFooterLabel.StylePriority.UseFont = false;
            this.cellReportFooterLabel.StylePriority.UseTextAlignment = false;
            this.cellReportFooterLabel.Text = "Total:";
            this.cellReportFooterLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellReportFooterLabel.Weight = 5.35D;
            // 
            // cellReportFooterRecuperacion
            // 
            this.cellReportFooterRecuperacion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([recuperacion])")});
            this.cellReportFooterRecuperacion.Name = "cellReportFooterRecuperacion";
            this.cellReportFooterRecuperacion.StylePriority.UseTextAlignment = false;
            this.cellReportFooterRecuperacion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            xrSummary6.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellReportFooterRecuperacion.Summary = xrSummary6;
            this.cellReportFooterRecuperacion.TextFormatString = "{0:n2}";
            this.cellReportFooterRecuperacion.Weight = 1.1D;
            // 
            // cellReportFooterIngresosMes
            // 
            this.cellReportFooterIngresosMes.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([ingresos_mes])")});
            this.cellReportFooterIngresosMes.Name = "cellReportFooterIngresosMes";
            this.cellReportFooterIngresosMes.StylePriority.UseTextAlignment = false;
            this.cellReportFooterIngresosMes.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            xrSummary7.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellReportFooterIngresosMes.Summary = xrSummary7;
            this.cellReportFooterIngresosMes.TextFormatString = "{0:n2}";
            this.cellReportFooterIngresosMes.Weight = 1.1D;
            // 
            // cellReportFooterTotal
            // 
            this.cellReportFooterTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([total_fila])")});
            this.cellReportFooterTotal.Name = "cellReportFooterTotal";
            this.cellReportFooterTotal.StylePriority.UseTextAlignment = false;
            this.cellReportFooterTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            xrSummary8.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellReportFooterTotal.Summary = xrSummary8;
            this.cellReportFooterTotal.TextFormatString = "{0:n2}";
            this.cellReportFooterTotal.Weight = 1.1D;
            // 
            // sqlDataSource1
            // 
            this.sqlDataSource1.ConnectionName = "DefaultConnection";
            this.sqlDataSource1.Name = "sqlDataSource1";
            queryParameter1.Name = "CompaniaID";
            queryParameter1.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter1.Value = new DevExpress.DataAccess.Expression("?p_Compania_ID", typeof(int));
            queryParameter2.Name = "FechaDesde";
            queryParameter2.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter2.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Desde", typeof(System.DateTime));
            queryParameter3.Name = "FechaHasta";
            queryParameter3.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Hasta", typeof(System.DateTime));
            queryParameter4.Name = "MedioPagoCodigo";
            queryParameter4.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Medio_Pago_Codigo", typeof(string));
            customSqlQuery1.Name = "Query";
            customSqlQuery1.Parameters.Add(queryParameter1);
            customSqlQuery1.Parameters.Add(queryParameter2);
            customSqlQuery1.Parameters.Add(queryParameter3);
            customSqlQuery1.Parameters.Add(queryParameter4);
            customSqlQuery1.Sql = "SELECT * FROM public.rep_recaudacion(:CompaniaID, :FechaDesde, :FechaHasta, :MedioPagoCodigo)";
            this.sqlDataSource1.Queries.AddRange(new DevExpress.DataAccess.Sql.SqlQuery[] {
            customSqlQuery1});
            this.sqlDataSource1.ResultSchemaSerializable = resources.GetString("sqlDataSource1.ResultSchemaSerializable");
            // 
            // Title
            // 
            this.Title.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.Title.Name = "Title";
            this.Title.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // HeaderCaption
            // 
            this.HeaderCaption.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.HeaderCaption.BorderWidth = 1F;
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.HeaderCaption.Name = "HeaderCaption";
            this.HeaderCaption.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            // 
            // DetailData
            // 
            this.DetailData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8.8F);
            this.DetailData.Name = "DetailData";
            this.DetailData.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            // 
            // TotalData
            // 
            this.TotalData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.TotalData.Name = "TotalData";
            this.TotalData.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            // 
            // p_Compania_ID
            // 
            this.p_Compania_ID.Name = "p_Compania_ID";
            this.p_Compania_ID.Type = typeof(int);
            this.p_Compania_ID.ValueInfo = "0";
            // 
            // p_Fecha_Desde
            // 
            this.p_Fecha_Desde.Name = "p_Fecha_Desde";
            this.p_Fecha_Desde.Type = typeof(System.DateTime);
            // 
            // p_Fecha_Hasta
            // 
            this.p_Fecha_Hasta.Name = "p_Fecha_Hasta";
            this.p_Fecha_Hasta.Type = typeof(System.DateTime);
            // 
            // p_Medio_Pago_Codigo
            // 
            this.p_Medio_Pago_Codigo.AllowNull = true;
            this.p_Medio_Pago_Codigo.Name = "p_Medio_Pago_Codigo";
            // 
            // Rpt_Dev_Recaudacion
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.ReportHeader,
            this.PageHeader,
            this.Detail,
            this.GroupHeaderMedioPago,
            this.GroupFooterMedioPago,
            this.ReportFooter});
            this.ComponentStorage.AddRange(new System.ComponentModel.IComponent[] {
            this.sqlDataSource1});
            this.DataMember = "Query";
            this.DataSource = this.sqlDataSource1;
            this.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F);
            this.Margins = new DevExpress.Drawing.DXMargins(40, 40, 18, 18);
            this.PageHeight = 1100;
            this.PageWidth = 850;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Desde,
            this.p_Fecha_Hasta,
            this.p_Medio_Pago_Codigo});
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.Title,
            this.HeaderCaption,
            this.DetailData,
            this.TotalData});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblGroupFooter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblReportFooter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }

        #endregion

        private DevExpress.XtraReports.UI.TopMarginBand TopMargin;
        private DevExpress.XtraReports.UI.BottomMarginBand BottomMargin;
        private DevExpress.XtraReports.UI.ReportHeaderBand ReportHeader;
        private DevExpress.XtraReports.UI.XRPageInfo lblFechaReporte;
        private DevExpress.XtraReports.UI.XRLabel lblPaginaTexto;
        private DevExpress.XtraReports.UI.XRPageInfo lblPaginaNumero;
        private DevExpress.XtraReports.UI.XRLine headerLine;
        private DevExpress.XtraReports.UI.XRLabel lblEmpresa;
        private DevExpress.XtraReports.UI.XRLabel lblTitulo;
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRecibo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderNombre;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRecuperacion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderIngresosMes;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTotal;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellRecibo;
        private DevExpress.XtraReports.UI.XRTableCell cellCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellNombre;
        private DevExpress.XtraReports.UI.XRTableCell cellRecuperacion;
        private DevExpress.XtraReports.UI.XRTableCell cellIngresosMes;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal;
        private DevExpress.XtraReports.UI.GroupHeaderBand GroupHeaderMedioPago;
        private DevExpress.XtraReports.UI.XRLabel lblMedioPagoHeader;
        private DevExpress.XtraReports.UI.GroupFooterBand GroupFooterMedioPago;
        private DevExpress.XtraReports.UI.XRTable tblGroupFooter;
        private DevExpress.XtraReports.UI.XRTableRow tblGroupFooterRow;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupFooterRecibosLabel;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupFooterRecibosCount;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupFooterMedioPagoLabel;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupFooterRecuperacion;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupFooterIngresosMes;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupFooterTotal;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRLabel lblTotalAbonadosLabel;
        private DevExpress.XtraReports.UI.XRLabel lblTotalAbonadosCount;
        private DevExpress.XtraReports.UI.XRTable tblReportFooter;
        private DevExpress.XtraReports.UI.XRTableRow tblReportFooterRow;
        private DevExpress.XtraReports.UI.XRTableCell cellReportFooterLabel;
        private DevExpress.XtraReports.UI.XRTableCell cellReportFooterRecuperacion;
        private DevExpress.XtraReports.UI.XRTableCell cellReportFooterIngresosMes;
        private DevExpress.XtraReports.UI.XRTableCell cellReportFooterTotal;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Desde;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Hasta;
        private DevExpress.XtraReports.Parameters.Parameter p_Medio_Pago_Codigo;
    }
}
