namespace SIAD.Reports
{
    partial class Rpt_Dev_Analisis_Antiguedad_Cobros
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Analisis_Antiguedad_Cobros));
            DevExpress.XtraReports.UI.XRSummary xrSummary1 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary2 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary3 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary4 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary5 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary6 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary7 = new DevExpress.XtraReports.UI.XRSummary();
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.pageInfoFecha = new DevExpress.XtraReports.UI.XRPageInfo();
            this.pageInfoPagina = new DevExpress.XtraReports.UI.XRPageInfo();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblFechaReporte = new DevExpress.XtraReports.UI.XRLabel();
            this.topLine = new DevExpress.XtraReports.UI.XRLine();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderCuenta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderDireccion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCorriente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeader30 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeader61 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeader91 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeader181 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeader361 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCuenta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDireccion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCorriente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cell30 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cell61 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cell91 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cell181 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cell361 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalCuenta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDireccion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCorriente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal30 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal61 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal91 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal181 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal361 = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGrandTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Base = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Retroceso_Valor = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Unidad_Tiempo = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblTotal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // TopMargin
            // 
            this.TopMargin.HeightF = 18F;
            this.TopMargin.Name = "TopMargin";
            // 
            // BottomMargin
            // 
            this.BottomMargin.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.pageInfoFecha,
            this.pageInfoPagina});
            this.BottomMargin.HeightF = 28F;
            this.BottomMargin.Name = "BottomMargin";
            // 
            // pageInfoFecha
            // 
            this.pageInfoFecha.LocationFloat = new DevExpress.Utils.PointFloat(0F, 4F);
            this.pageInfoFecha.Name = "pageInfoFecha";
            this.pageInfoFecha.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            this.pageInfoFecha.SizeF = new System.Drawing.SizeF(260F, 18F);
            this.pageInfoFecha.StyleName = "PageInfoStyle";
            this.pageInfoFecha.TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}";
            // 
            // pageInfoPagina
            // 
            this.pageInfoPagina.LocationFloat = new DevExpress.Utils.PointFloat(790F, 4F);
            this.pageInfoPagina.Name = "pageInfoPagina";
            this.pageInfoPagina.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal;
            this.pageInfoPagina.SizeF = new System.Drawing.SizeF(230F, 18F);
            this.pageInfoPagina.StyleName = "PageInfoStyle";
            this.pageInfoPagina.StylePriority.UseTextAlignment = false;
            this.pageInfoPagina.TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopRight;
            this.pageInfoPagina.TextFormatString = "Pagina {0} de {1}";
            // 
            // ReportHeader
            // 
            this.ReportHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblFechaReporte,
            this.topLine,
            this.lblEmpresa,
            this.lblTitulo});
            this.ReportHeader.HeightF = 104F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblFechaReporte
            // 
            this.lblFechaReporte.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]")});
            this.lblFechaReporte.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblFechaReporte.LocationFloat = new DevExpress.Utils.PointFloat(848F, 8F);
            this.lblFechaReporte.Name = "lblFechaReporte";
            this.lblFechaReporte.SizeF = new System.Drawing.SizeF(152F, 22F);
            this.lblFechaReporte.StylePriority.UseFont = false;
            this.lblFechaReporte.StylePriority.UseTextAlignment = false;
            this.lblFechaReporte.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // topLine
            // 
            this.topLine.LineWidth = 3F;
            this.topLine.LocationFloat = new DevExpress.Utils.PointFloat(18F, 34F);
            this.topLine.Name = "topLine";
            this.topLine.SizeF = new System.Drawing.SizeF(982F, 8F);
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([empresa_nombre])")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 17F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(165F, 46F);
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(690F, 28F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(110F, 76F);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(800F, 24F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // PageHeader
            // 
            this.PageHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblHeader});
            this.PageHeader.HeightF = 44F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.5F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(18F, 6F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(982F, 34F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderCuenta,
            this.cellHeaderCliente,
            this.cellHeaderDireccion,
            this.cellHeaderCorriente,
            this.cellHeader30,
            this.cellHeader61,
            this.cellHeader91,
            this.cellHeader181,
            this.cellHeader361,
            this.cellHeaderTotal});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderCuenta
            // 
            this.cellHeaderCuenta.Name = "cellHeaderCuenta";
            this.cellHeaderCuenta.Text = "CUENTA";
            this.cellHeaderCuenta.Weight = 1.35D;
            // 
            // cellHeaderCliente
            // 
            this.cellHeaderCliente.Name = "cellHeaderCliente";
            this.cellHeaderCliente.Text = "NOMBRE DEL CLIENTE";
            this.cellHeaderCliente.Weight = 3.55D;
            // 
            // cellHeaderDireccion
            // 
            this.cellHeaderDireccion.Name = "cellHeaderDireccion";
            this.cellHeaderDireccion.Text = "DIRECCION";
            this.cellHeaderDireccion.Weight = 4.75D;
            // 
            // cellHeaderCorriente
            // 
            this.cellHeaderCorriente.Name = "cellHeaderCorriente";
            this.cellHeaderCorriente.Text = "CORRIENTE";
            this.cellHeaderCorriente.Weight = 1.35D;
            // 
            // cellHeader30
            // 
            this.cellHeader30.Multiline = true;
            this.cellHeader30.Name = "cellHeader30";
            this.cellHeader30.Text = "30 - 60\r\nDIAS";
            this.cellHeader30.Weight = 1.05D;
            // 
            // cellHeader61
            // 
            this.cellHeader61.Multiline = true;
            this.cellHeader61.Name = "cellHeader61";
            this.cellHeader61.Text = "61 - 90\r\nDIAS";
            this.cellHeader61.Weight = 1.05D;
            // 
            // cellHeader91
            // 
            this.cellHeader91.Multiline = true;
            this.cellHeader91.Name = "cellHeader91";
            this.cellHeader91.Text = "91 - 180\r\nDIAS";
            this.cellHeader91.Weight = 1.15D;
            // 
            // cellHeader181
            // 
            this.cellHeader181.Multiline = true;
            this.cellHeader181.Name = "cellHeader181";
            this.cellHeader181.Text = "181 - 360\r\nDIAS";
            this.cellHeader181.Weight = 1.15D;
            // 
            // cellHeader361
            // 
            this.cellHeader361.Multiline = true;
            this.cellHeader361.Name = "cellHeader361";
            this.cellHeader361.Text = "MAS A 361\r\nDIAS";
            this.cellHeader361.Weight = 1.15D;
            // 
            // cellHeaderTotal
            // 
            this.cellHeaderTotal.Name = "cellHeaderTotal";
            this.cellHeaderTotal.Text = "TOTAL";
            this.cellHeaderTotal.Weight = 1.2D;
            // 
            // Detail
            // 
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblDetail});
            this.Detail.HeightF = 24F;
            this.Detail.Name = "Detail";
            // 
            // tblDetail
            // 
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F);
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(18F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(982F, 24F);
            this.tblDetail.StylePriority.UseFont = false;
            this.tblDetail.StylePriority.UseTextAlignment = false;
            this.tblDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellCuenta,
            this.cellCliente,
            this.cellDireccion,
            this.cellCorriente,
            this.cell30,
            this.cell61,
            this.cell91,
            this.cell181,
            this.cell361,
            this.cellTotal});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellCuenta
            // 
            this.cellCuenta.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cuenta]")});
            this.cellCuenta.Name = "cellCuenta";
            this.cellCuenta.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellCuenta.StyleName = "DetailData";
            this.cellCuenta.StylePriority.UsePadding = false;
            this.cellCuenta.StylePriority.UseTextAlignment = false;
            this.cellCuenta.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellCuenta.Weight = 1.35D;
            // 
            // cellCliente
            // 
            this.cellCliente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_nombre]")});
            this.cellCliente.Name = "cellCliente";
            this.cellCliente.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellCliente.StyleName = "DetailData";
            this.cellCliente.StylePriority.UsePadding = false;
            this.cellCliente.StylePriority.UseTextAlignment = false;
            this.cellCliente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellCliente.Weight = 3.55D;
            // 
            // cellDireccion
            // 
            this.cellDireccion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[direccion]")});
            this.cellDireccion.Name = "cellDireccion";
            this.cellDireccion.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellDireccion.StyleName = "DetailData";
            this.cellDireccion.StylePriority.UsePadding = false;
            this.cellDireccion.StylePriority.UseTextAlignment = false;
            this.cellDireccion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellDireccion.Weight = 4.75D;
            // 
            // cellCorriente
            // 
            this.cellCorriente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[corriente]")});
            this.cellCorriente.Name = "cellCorriente";
            this.cellCorriente.StyleName = "DetailData";
            this.cellCorriente.StylePriority.UseTextAlignment = false;
            this.cellCorriente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCorriente.TextFormatString = "{0:n2}";
            this.cellCorriente.Weight = 1.35D;
            // 
            // cell30
            // 
            this.cell30.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[dias_30_60]")});
            this.cell30.Name = "cell30";
            this.cell30.StyleName = "DetailData";
            this.cell30.StylePriority.UseTextAlignment = false;
            this.cell30.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cell30.TextFormatString = "{0:n2}";
            this.cell30.Weight = 1.05D;
            // 
            // cell61
            // 
            this.cell61.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[dias_61_90]")});
            this.cell61.Name = "cell61";
            this.cell61.StyleName = "DetailData";
            this.cell61.StylePriority.UseTextAlignment = false;
            this.cell61.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cell61.TextFormatString = "{0:n2}";
            this.cell61.Weight = 1.05D;
            // 
            // cell91
            // 
            this.cell91.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[dias_91_180]")});
            this.cell91.Name = "cell91";
            this.cell91.StyleName = "DetailData";
            this.cell91.StylePriority.UseTextAlignment = false;
            this.cell91.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cell91.TextFormatString = "{0:n2}";
            this.cell91.Weight = 1.15D;
            // 
            // cell181
            // 
            this.cell181.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[dias_181_360]")});
            this.cell181.Name = "cell181";
            this.cell181.StyleName = "DetailData";
            this.cell181.StylePriority.UseTextAlignment = false;
            this.cell181.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cell181.TextFormatString = "{0:n2}";
            this.cell181.Weight = 1.15D;
            // 
            // cell361
            // 
            this.cell361.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[mas_361]")});
            this.cell361.Name = "cell361";
            this.cell361.StyleName = "DetailData";
            this.cell361.StylePriority.UseTextAlignment = false;
            this.cell361.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cell361.TextFormatString = "{0:n2}";
            this.cell361.Weight = 1.15D;
            // 
            // cellTotal
            // 
            this.cellTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total]")});
            this.cellTotal.Name = "cellTotal";
            this.cellTotal.StyleName = "DetailData";
            this.cellTotal.StylePriority.UseTextAlignment = false;
            this.cellTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal.TextFormatString = "{0:n2}";
            this.cellTotal.Weight = 1.2D;
            // 
            // ReportFooter
            // 
            this.ReportFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblTotal});
            this.ReportFooter.HeightF = 30F;
            this.ReportFooter.Name = "ReportFooter";
            // 
            // tblTotal
            // 
            this.tblTotal.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblTotal.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblTotal.LocationFloat = new DevExpress.Utils.PointFloat(18F, 2F);
            this.tblTotal.Name = "tblTotal";
            this.tblTotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblTotalRow});
            this.tblTotal.SizeF = new System.Drawing.SizeF(982F, 26F);
            this.tblTotal.StyleName = "TotalData";
            this.tblTotal.StylePriority.UseBorders = false;
            this.tblTotal.StylePriority.UseFont = false;
            this.tblTotal.StylePriority.UseTextAlignment = false;
            this.tblTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblTotalRow
            // 
            this.tblTotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellTotalCuenta,
            this.cellTotalCliente,
            this.cellTotalDireccion,
            this.cellTotalCorriente,
            this.cellTotal30,
            this.cellTotal61,
            this.cellTotal91,
            this.cellTotal181,
            this.cellTotal361,
            this.cellGrandTotal});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalCuenta
            // 
            this.cellTotalCuenta.Name = "cellTotalCuenta";
            this.cellTotalCuenta.Weight = 1.35D;
            // 
            // cellTotalCliente
            // 
            this.cellTotalCliente.Name = "cellTotalCliente";
            this.cellTotalCliente.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 4, 0, 0, 100F);
            this.cellTotalCliente.StylePriority.UsePadding = false;
            this.cellTotalCliente.StylePriority.UseTextAlignment = false;
            this.cellTotalCliente.Text = "Total general:";
            this.cellTotalCliente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCliente.Weight = 3.55D;
            // 
            // cellTotalDireccion
            // 
            this.cellTotalDireccion.Name = "cellTotalDireccion";
            this.cellTotalDireccion.Weight = 4.75D;
            // 
            // cellTotalCorriente
            // 
            this.cellTotalCorriente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([corriente])")});
            this.cellTotalCorriente.Name = "cellTotalCorriente";
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCorriente.Summary = xrSummary1;
            this.cellTotalCorriente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCorriente.TextFormatString = "{0:n2}";
            this.cellTotalCorriente.Weight = 1.35D;
            // 
            // cellTotal30
            // 
            this.cellTotal30.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([dias_30_60])")});
            this.cellTotal30.Name = "cellTotal30";
            xrSummary2.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotal30.Summary = xrSummary2;
            this.cellTotal30.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal30.TextFormatString = "{0:n2}";
            this.cellTotal30.Weight = 1.05D;
            // 
            // cellTotal61
            // 
            this.cellTotal61.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([dias_61_90])")});
            this.cellTotal61.Name = "cellTotal61";
            xrSummary3.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotal61.Summary = xrSummary3;
            this.cellTotal61.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal61.TextFormatString = "{0:n2}";
            this.cellTotal61.Weight = 1.05D;
            // 
            // cellTotal91
            // 
            this.cellTotal91.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([dias_91_180])")});
            this.cellTotal91.Name = "cellTotal91";
            xrSummary4.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotal91.Summary = xrSummary4;
            this.cellTotal91.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal91.TextFormatString = "{0:n2}";
            this.cellTotal91.Weight = 1.15D;
            // 
            // cellTotal181
            // 
            this.cellTotal181.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([dias_181_360])")});
            this.cellTotal181.Name = "cellTotal181";
            xrSummary5.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotal181.Summary = xrSummary5;
            this.cellTotal181.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal181.TextFormatString = "{0:n2}";
            this.cellTotal181.Weight = 1.15D;
            // 
            // cellTotal361
            // 
            this.cellTotal361.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([mas_361])")});
            this.cellTotal361.Name = "cellTotal361";
            xrSummary6.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotal361.Summary = xrSummary6;
            this.cellTotal361.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotal361.TextFormatString = "{0:n2}";
            this.cellTotal361.Weight = 1.15D;
            // 
            // cellGrandTotal
            // 
            this.cellGrandTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([total])")});
            this.cellGrandTotal.Name = "cellGrandTotal";
            xrSummary7.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellGrandTotal.Summary = xrSummary7;
            this.cellGrandTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellGrandTotal.TextFormatString = "{0:n2}";
            this.cellGrandTotal.Weight = 1.2D;
            // 
            // sqlDataSource1
            // 
            this.sqlDataSource1.ConnectionName = "DefaultConnection";
            this.sqlDataSource1.Name = "sqlDataSource1";
            customSqlQuery1.Name = "Query";
            queryParameter1.Name = "CompaniaID";
            queryParameter1.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter1.Value = new DevExpress.DataAccess.Expression("?p_Compania_ID", typeof(int));
            queryParameter2.Name = "FechaBase";
            queryParameter2.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter2.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Base", typeof(System.DateOnly));
            queryParameter3.Name = "RetrocesoValor";
            queryParameter3.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Retroceso_Valor", typeof(int));
            queryParameter4.Name = "UnidadTiempo";
            queryParameter4.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Unidad_Tiempo", typeof(string));
            customSqlQuery1.Parameters.AddRange(new DevExpress.DataAccess.Sql.QueryParameter[] {
            queryParameter1,
            queryParameter2,
            queryParameter3,
            queryParameter4});
            customSqlQuery1.Sql = resources.GetString("customSqlQuery1.Sql");
            this.sqlDataSource1.Queries.AddRange(new DevExpress.DataAccess.Sql.SqlQuery[] {
            customSqlQuery1});
            // 
            // Title
            // 
            this.Title.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.Title.Name = "Title";
            this.Title.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // HeaderCaption
            // 
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.HeaderCaption.Name = "HeaderCaption";
            this.HeaderCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // DetailData
            // 
            this.DetailData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F);
            this.DetailData.Name = "DetailData";
            // 
            // PageInfoStyle
            // 
            this.PageInfoStyle.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.PageInfoStyle.Name = "PageInfoStyle";
            // 
            // TotalData
            // 
            this.TotalData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.TotalData.Name = "TotalData";
            // 
            // p_Compania_ID
            // 
            this.p_Compania_ID.Name = "p_Compania_ID";
            this.p_Compania_ID.Type = typeof(int);
            this.p_Compania_ID.ValueInfo = "0";
            // 
            // p_Fecha_Base
            // 
            this.p_Fecha_Base.Name = "p_Fecha_Base";
            this.p_Fecha_Base.Type = typeof(global::System.DateOnly);
            // 
            // p_Retroceso_Valor
            // 
            this.p_Retroceso_Valor.Name = "p_Retroceso_Valor";
            this.p_Retroceso_Valor.Type = typeof(int);
            this.p_Retroceso_Valor.ValueInfo = "12";
            // 
            // p_Unidad_Tiempo
            // 
            this.p_Unidad_Tiempo.Name = "p_Unidad_Tiempo";
            this.p_Unidad_Tiempo.ValueInfo = "MESES";
            // 
            // Rpt_Dev_Analisis_Antiguedad_Cobros
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.ReportHeader,
            this.PageHeader,
            this.Detail,
            this.ReportFooter});
            this.ComponentStorage.AddRange(new System.ComponentModel.IComponent[] {
            this.sqlDataSource1});
            this.DataMember = "Query";
            this.DataSource = this.sqlDataSource1;
            this.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.75F);
            this.Landscape = true;
            this.Margins = new DevExpress.Drawing.DXMargins(40, 40, 18, 28);
            this.PageHeight = 850;
            this.PageWidth = 1100;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Base,
            this.p_Retroceso_Valor,
            this.p_Unidad_Tiempo});
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.Title,
            this.HeaderCaption,
            this.DetailData,
            this.PageInfoStyle,
            this.TotalData});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblTotal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this)).EndInit();

        }

        #endregion

        private DevExpress.XtraReports.UI.TopMarginBand TopMargin;
        private DevExpress.XtraReports.UI.BottomMarginBand BottomMargin;
        private DevExpress.XtraReports.UI.XRPageInfo pageInfoFecha;
        private DevExpress.XtraReports.UI.XRPageInfo pageInfoPagina;
        private DevExpress.XtraReports.UI.ReportHeaderBand ReportHeader;
        private DevExpress.XtraReports.UI.XRLabel lblFechaReporte;
        private DevExpress.XtraReports.UI.XRLine topLine;
        private DevExpress.XtraReports.UI.XRLabel lblEmpresa;
        private DevExpress.XtraReports.UI.XRLabel lblTitulo;
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCuenta;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderDireccion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCorriente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeader30;
        private DevExpress.XtraReports.UI.XRTableCell cellHeader61;
        private DevExpress.XtraReports.UI.XRTableCell cellHeader91;
        private DevExpress.XtraReports.UI.XRTableCell cellHeader181;
        private DevExpress.XtraReports.UI.XRTableCell cellHeader361;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTotal;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCuenta;
        private DevExpress.XtraReports.UI.XRTableCell cellCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellDireccion;
        private DevExpress.XtraReports.UI.XRTableCell cellCorriente;
        private DevExpress.XtraReports.UI.XRTableCell cell30;
        private DevExpress.XtraReports.UI.XRTableCell cell61;
        private DevExpress.XtraReports.UI.XRTableCell cell91;
        private DevExpress.XtraReports.UI.XRTableCell cell181;
        private DevExpress.XtraReports.UI.XRTableCell cell361;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCuenta;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDireccion;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCorriente;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal30;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal61;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal91;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal181;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal361;
        private DevExpress.XtraReports.UI.XRTableCell cellGrandTotal;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Base;
        private DevExpress.XtraReports.Parameters.Parameter p_Retroceso_Valor;
        private DevExpress.XtraReports.Parameters.Parameter p_Unidad_Tiempo;
    }
}
