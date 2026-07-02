namespace SIAD.Reports
{
    partial class Rpt_Dev_Desglose_Facturacion
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Desglose_Facturacion));
            DevExpress.XtraReports.UI.XRSummary xrSummary1 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary2 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary3 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary4 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary5 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary6 = new DevExpress.XtraReports.UI.XRSummary();
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.pageInfoFecha = new DevExpress.XtraReports.UI.XRPageInfo();
            this.pageInfoPagina = new DevExpress.XtraReports.UI.XRPageInfo();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.lblFechaReporte = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaHeader = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaNumero = new DevExpress.XtraReports.UI.XRPageInfo();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderCiclo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderFacturacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAdultoMayor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderPagosRegistrados = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCiclo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFacturacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAdultoMayor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellPagosRegistrados = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalCaption = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalFacturacion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalAdultoMayor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalPagosRegistrados = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailDataOdd = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Inicio = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Fin = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblTotal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // TopMargin
            // 
            this.TopMargin.HeightF = 24F;
            this.TopMargin.Name = "TopMargin";
            // 
            // BottomMargin
            // 
            this.BottomMargin.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.pageInfoFecha,
            this.pageInfoPagina});
            this.BottomMargin.HeightF = 32F;
            this.BottomMargin.Name = "BottomMargin";
            // 
            // pageInfoFecha
            // 
            this.pageInfoFecha.LocationFloat = new DevExpress.Utils.PointFloat(0F, 4F);
            this.pageInfoFecha.Name = "pageInfoFecha";
            this.pageInfoFecha.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            this.pageInfoFecha.SizeF = new System.Drawing.SizeF(300F, 20F);
            this.pageInfoFecha.StyleName = "PageInfoStyle";
            this.pageInfoFecha.TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}";
            // 
            // pageInfoPagina
            // 
            this.pageInfoPagina.LocationFloat = new DevExpress.Utils.PointFloat(1030F, 4F);
            this.pageInfoPagina.Name = "pageInfoPagina";
            this.pageInfoPagina.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal;
            this.pageInfoPagina.SizeF = new System.Drawing.SizeF(260F, 20F);
            this.pageInfoPagina.StyleName = "PageInfoStyle";
            this.pageInfoPagina.StylePriority.UseTextAlignment = false;
            this.pageInfoPagina.TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopRight;
            this.pageInfoPagina.TextFormatString = "Pagina {0} de {1}";
            // 
            // ReportHeader
            // 
            this.ReportHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblEmpresa,
            this.lblTitulo,
            this.lblFechaReporte,
            this.lblPaginaHeader,
            this.lblPaginaNumero});
            this.ReportHeader.HeightF = 108F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([empresa_nombre])")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 16F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(180F, 12F);
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(930F, 28F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(90F, 42F);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(1090F, 30F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblFechaReporte
            // 
            this.lblFechaReporte.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]")});
            this.lblFechaReporte.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblFechaReporte.LocationFloat = new DevExpress.Utils.PointFloat(520F, 74F);
            this.lblFechaReporte.Name = "lblFechaReporte";
            this.lblFechaReporte.SizeF = new System.Drawing.SizeF(250F, 22F);
            this.lblFechaReporte.StylePriority.UseFont = false;
            this.lblFechaReporte.StylePriority.UseTextAlignment = false;
            this.lblFechaReporte.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblPaginaHeader
            // 
            this.lblPaginaHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblPaginaHeader.LocationFloat = new DevExpress.Utils.PointFloat(1115F, 12F);
            this.lblPaginaHeader.Name = "lblPaginaHeader";
            this.lblPaginaHeader.SizeF = new System.Drawing.SizeF(80F, 22F);
            this.lblPaginaHeader.StylePriority.UseFont = false;
            this.lblPaginaHeader.Text = "PAG.";
            // 
            // lblPaginaNumero
            // 
            this.lblPaginaNumero.LocationFloat = new DevExpress.Utils.PointFloat(1210F, 12F);
            this.lblPaginaNumero.Name = "lblPaginaNumero";
            this.lblPaginaNumero.PageInfo = DevExpress.XtraPrinting.PageInfo.Number;
            this.lblPaginaNumero.SizeF = new System.Drawing.SizeF(80F, 22F);
            this.lblPaginaNumero.StylePriority.UseTextAlignment = false;
            this.lblPaginaNumero.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblPaginaNumero.TextFormatString = "{0}";
            // 
            // PageHeader
            // 
            this.PageHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblHeader});
            this.PageHeader.HeightF = 40F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(0F, 6F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(1290F, 36F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderCiclo,
            this.cellHeaderFacturacion,
            this.cellHeaderDebitos,
            this.cellHeaderCreditos,
            this.cellHeaderAdultoMayor,
            this.cellHeaderPagosRegistrados,
            this.cellHeaderSaldo});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderCiclo
            // 
            this.cellHeaderCiclo.Name = "cellHeaderCiclo";
            this.cellHeaderCiclo.Text = "Ciclo";
            this.cellHeaderCiclo.Weight = 0.7D;
            // 
            // cellHeaderFacturacion
            // 
            this.cellHeaderFacturacion.Name = "cellHeaderFacturacion";
            this.cellHeaderFacturacion.Text = "Facturacion";
            this.cellHeaderFacturacion.Weight = 1.35D;
            // 
            // cellHeaderDebitos
            // 
            this.cellHeaderDebitos.Name = "cellHeaderDebitos";
            this.cellHeaderDebitos.Text = "Debitos";
            this.cellHeaderDebitos.Weight = 1.1D;
            // 
            // cellHeaderCreditos
            // 
            this.cellHeaderCreditos.Name = "cellHeaderCreditos";
            this.cellHeaderCreditos.Text = "Creditos";
            this.cellHeaderCreditos.Weight = 1.1D;
            // 
            // cellHeaderAdultoMayor
            // 
            this.cellHeaderAdultoMayor.Multiline = true;
            this.cellHeaderAdultoMayor.Name = "cellHeaderAdultoMayor";
            this.cellHeaderAdultoMayor.Text = "Adulto\r\nMayor";
            this.cellHeaderAdultoMayor.Weight = 1.15D;
            // 
            // cellHeaderPagosRegistrados
            // 
            this.cellHeaderPagosRegistrados.Multiline = true;
            this.cellHeaderPagosRegistrados.Name = "cellHeaderPagosRegistrados";
            this.cellHeaderPagosRegistrados.Text = "Pagos\r\nRegistrados";
            this.cellHeaderPagosRegistrados.Weight = 1.35D;
            // 
            // cellHeaderSaldo
            // 
            this.cellHeaderSaldo.Name = "cellHeaderSaldo";
            this.cellHeaderSaldo.Text = "Saldo";
            this.cellHeaderSaldo.Weight = 1.35D;
            // 
            // Detail
            // 
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblDetail});
            this.Detail.HeightF = 28F;
            this.Detail.Name = "Detail";
            // 
            // tblDetail
            // 
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F);
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.OddStyleName = "DetailDataOdd";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(1290F, 28F);
            this.tblDetail.StylePriority.UseFont = false;
            this.tblDetail.StylePriority.UseTextAlignment = false;
            this.tblDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellCiclo,
            this.cellFacturacion,
            this.cellDebitos,
            this.cellCreditos,
            this.cellAdultoMayor,
            this.cellPagosRegistrados,
            this.cellSaldo});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellCiclo
            // 
            this.cellCiclo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ciclo]")});
            this.cellCiclo.Name = "cellCiclo";
            this.cellCiclo.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellCiclo.StyleName = "DetailData";
            this.cellCiclo.StylePriority.UsePadding = false;
            this.cellCiclo.StylePriority.UseTextAlignment = false;
            this.cellCiclo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellCiclo.Weight = 0.7D;
            // 
            // cellFacturacion
            // 
            this.cellFacturacion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[facturacion]")});
            this.cellFacturacion.Name = "cellFacturacion";
            this.cellFacturacion.StyleName = "DetailData";
            this.cellFacturacion.StylePriority.UseTextAlignment = false;
            this.cellFacturacion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellFacturacion.TextFormatString = "{0:n2}";
            this.cellFacturacion.Weight = 1.35D;
            // 
            // cellDebitos
            // 
            this.cellDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[debitos]")});
            this.cellDebitos.Name = "cellDebitos";
            this.cellDebitos.StyleName = "DetailData";
            this.cellDebitos.StylePriority.UseTextAlignment = false;
            this.cellDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellDebitos.TextFormatString = "{0:n2}";
            this.cellDebitos.Weight = 1.1D;
            // 
            // cellCreditos
            // 
            this.cellCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[creditos]")});
            this.cellCreditos.Name = "cellCreditos";
            this.cellCreditos.StyleName = "DetailData";
            this.cellCreditos.StylePriority.UseTextAlignment = false;
            this.cellCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCreditos.TextFormatString = "{0:n2}";
            this.cellCreditos.Weight = 1.1D;
            // 
            // cellAdultoMayor
            // 
            this.cellAdultoMayor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[adulto_mayor]")});
            this.cellAdultoMayor.Name = "cellAdultoMayor";
            this.cellAdultoMayor.StyleName = "DetailData";
            this.cellAdultoMayor.StylePriority.UseTextAlignment = false;
            this.cellAdultoMayor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAdultoMayor.TextFormatString = "{0:n2}";
            this.cellAdultoMayor.Weight = 1.15D;
            // 
            // cellPagosRegistrados
            // 
            this.cellPagosRegistrados.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[pagos_registrados]")});
            this.cellPagosRegistrados.Name = "cellPagosRegistrados";
            this.cellPagosRegistrados.StyleName = "DetailData";
            this.cellPagosRegistrados.StylePriority.UseTextAlignment = false;
            this.cellPagosRegistrados.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellPagosRegistrados.TextFormatString = "{0:n2}";
            this.cellPagosRegistrados.Weight = 1.35D;
            // 
            // cellSaldo
            // 
            this.cellSaldo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo]")});
            this.cellSaldo.Name = "cellSaldo";
            this.cellSaldo.StyleName = "DetailData";
            this.cellSaldo.StylePriority.UseTextAlignment = false;
            this.cellSaldo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSaldo.TextFormatString = "{0:n2}";
            this.cellSaldo.Weight = 1.35D;
            // 
            // ReportFooter
            // 
            this.ReportFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblTotal});
            this.ReportFooter.HeightF = 34F;
            this.ReportFooter.Name = "ReportFooter";
            // 
            // tblTotal
            // 
            this.tblTotal.Borders = DevExpress.XtraPrinting.BorderSide.Top;
            this.tblTotal.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblTotal.LocationFloat = new DevExpress.Utils.PointFloat(0F, 4F);
            this.tblTotal.Name = "tblTotal";
            this.tblTotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblTotalRow});
            this.tblTotal.SizeF = new System.Drawing.SizeF(1290F, 28F);
            this.tblTotal.StyleName = "TotalData";
            this.tblTotal.StylePriority.UseBorders = false;
            this.tblTotal.StylePriority.UseFont = false;
            this.tblTotal.StylePriority.UseTextAlignment = false;
            this.tblTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblTotalRow
            // 
            this.tblTotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellTotalCaption,
            this.cellTotalFacturacion,
            this.cellTotalDebitos,
            this.cellTotalCreditos,
            this.cellTotalAdultoMayor,
            this.cellTotalPagosRegistrados,
            this.cellTotalSaldo});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalCaption
            // 
            this.cellTotalCaption.Name = "cellTotalCaption";
            this.cellTotalCaption.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellTotalCaption.StylePriority.UsePadding = false;
            this.cellTotalCaption.StylePriority.UseTextAlignment = false;
            this.cellTotalCaption.Text = "Total";
            this.cellTotalCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellTotalCaption.Weight = 0.7D;
            // 
            // cellTotalFacturacion
            // 
            this.cellTotalFacturacion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([facturacion])")});
            this.cellTotalFacturacion.Name = "cellTotalFacturacion";
            this.cellTotalFacturacion.StylePriority.UseTextAlignment = false;
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalFacturacion.Summary = xrSummary1;
            this.cellTotalFacturacion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalFacturacion.TextFormatString = "{0:n2}";
            this.cellTotalFacturacion.Weight = 1.35D;
            // 
            // cellTotalDebitos
            // 
            this.cellTotalDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([debitos])")});
            this.cellTotalDebitos.Name = "cellTotalDebitos";
            this.cellTotalDebitos.StylePriority.UseTextAlignment = false;
            xrSummary2.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalDebitos.Summary = xrSummary2;
            this.cellTotalDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalDebitos.TextFormatString = "{0:n2}";
            this.cellTotalDebitos.Weight = 1.1D;
            // 
            // cellTotalCreditos
            // 
            this.cellTotalCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([creditos])")});
            this.cellTotalCreditos.Name = "cellTotalCreditos";
            this.cellTotalCreditos.StylePriority.UseTextAlignment = false;
            xrSummary3.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCreditos.Summary = xrSummary3;
            this.cellTotalCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCreditos.TextFormatString = "{0:n2}";
            this.cellTotalCreditos.Weight = 1.1D;
            // 
            // cellTotalAdultoMayor
            // 
            this.cellTotalAdultoMayor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([adulto_mayor])")});
            this.cellTotalAdultoMayor.Name = "cellTotalAdultoMayor";
            this.cellTotalAdultoMayor.StylePriority.UseTextAlignment = false;
            xrSummary4.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalAdultoMayor.Summary = xrSummary4;
            this.cellTotalAdultoMayor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalAdultoMayor.TextFormatString = "{0:n2}";
            this.cellTotalAdultoMayor.Weight = 1.15D;
            // 
            // cellTotalPagosRegistrados
            // 
            this.cellTotalPagosRegistrados.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([pagos_registrados])")});
            this.cellTotalPagosRegistrados.Name = "cellTotalPagosRegistrados";
            this.cellTotalPagosRegistrados.StylePriority.UseTextAlignment = false;
            xrSummary5.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalPagosRegistrados.Summary = xrSummary5;
            this.cellTotalPagosRegistrados.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalPagosRegistrados.TextFormatString = "{0:n2}";
            this.cellTotalPagosRegistrados.Weight = 1.35D;
            // 
            // cellTotalSaldo
            // 
            this.cellTotalSaldo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo])")});
            this.cellTotalSaldo.Name = "cellTotalSaldo";
            this.cellTotalSaldo.StylePriority.UseTextAlignment = false;
            xrSummary6.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldo.Summary = xrSummary6;
            this.cellTotalSaldo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldo.TextFormatString = "{0:n2}";
            this.cellTotalSaldo.Weight = 1.35D;
            // 
            // sqlDataSource1
            // 
            this.sqlDataSource1.ConnectionName = "DefaultConnection";
            this.sqlDataSource1.Name = "sqlDataSource1";
            customSqlQuery1.Name = "Query";
            queryParameter1.Name = "CompaniaID";
            queryParameter1.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter1.Value = new DevExpress.DataAccess.Expression("?p_Compania_ID", typeof(int));
            queryParameter2.Name = "FechaInicio";
            queryParameter2.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter2.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Inicio", typeof(System.DateOnly));
            queryParameter3.Name = "FechaFin";
            queryParameter3.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Fin", typeof(System.DateOnly));
            customSqlQuery1.Parameters.AddRange(new DevExpress.DataAccess.Sql.QueryParameter[] {
            queryParameter1,
            queryParameter2,
            queryParameter3});
            customSqlQuery1.Sql = resources.GetString("customSqlQuery1.Sql");
            this.sqlDataSource1.Queries.AddRange(new DevExpress.DataAccess.Sql.SqlQuery[] {
            customSqlQuery1});
            this.sqlDataSource1.ResultSchemaSerializable = resources.GetString("sqlDataSource1.ResultSchemaSerializable");
            // 
            // Title
            // 
            this.Title.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.Title.ForeColor = System.Drawing.Color.Black;
            this.Title.Name = "Title";
            this.Title.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // HeaderCaption
            // 
            this.HeaderCaption.BackColor = System.Drawing.Color.Transparent;
            this.HeaderCaption.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.HeaderCaption.BorderWidth = 1F;
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.HeaderCaption.ForeColor = System.Drawing.Color.Black;
            this.HeaderCaption.Name = "HeaderCaption";
            this.HeaderCaption.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            this.HeaderCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // DetailData
            // 
            this.DetailData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F);
            this.DetailData.ForeColor = System.Drawing.Color.Black;
            this.DetailData.Name = "DetailData";
            this.DetailData.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            this.DetailData.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // DetailDataOdd
            // 
            this.DetailDataOdd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this.DetailDataOdd.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F);
            this.DetailDataOdd.Name = "DetailDataOdd";
            this.DetailDataOdd.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            // 
            // PageInfoStyle
            // 
            this.PageInfoStyle.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.PageInfoStyle.ForeColor = System.Drawing.Color.Black;
            this.PageInfoStyle.Name = "PageInfoStyle";
            this.PageInfoStyle.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            // 
            // TotalData
            // 
            this.TotalData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.TotalData.ForeColor = System.Drawing.Color.Black;
            this.TotalData.Name = "TotalData";
            this.TotalData.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            this.TotalData.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // p_Compania_ID
            // 
            this.p_Compania_ID.Name = "p_Compania_ID";
            this.p_Compania_ID.Type = typeof(int);
            this.p_Compania_ID.ValueInfo = "0";
            // 
            // p_Fecha_Inicio
            // 
            this.p_Fecha_Inicio.Name = "p_Fecha_Inicio";
            this.p_Fecha_Inicio.Type = typeof(global::System.DateOnly);
            // 
            // p_Fecha_Fin
            // 
            this.p_Fecha_Fin.Name = "p_Fecha_Fin";
            this.p_Fecha_Fin.Type = typeof(global::System.DateOnly);
            // 
            // Rpt_Dev_Desglose_Facturacion
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
            this.Margins = new DevExpress.Drawing.DXMargins(40, 40, 24, 32);
            this.PageHeight = 850;
            this.PageWidth = 1400;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Legal;
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Inicio,
            this.p_Fecha_Fin});
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.Title,
            this.HeaderCaption,
            this.DetailData,
            this.DetailDataOdd,
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
        private DevExpress.XtraReports.UI.XRLabel lblEmpresa;
        private DevExpress.XtraReports.UI.XRLabel lblTitulo;
        private DevExpress.XtraReports.UI.XRLabel lblFechaReporte;
        private DevExpress.XtraReports.UI.XRLabel lblPaginaHeader;
        private DevExpress.XtraReports.UI.XRPageInfo lblPaginaNumero;
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCiclo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderFacturacion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAdultoMayor;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderPagosRegistrados;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSaldo;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCiclo;
        private DevExpress.XtraReports.UI.XRTableCell cellFacturacion;
        private DevExpress.XtraReports.UI.XRTableCell cellDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellAdultoMayor;
        private DevExpress.XtraReports.UI.XRTableCell cellPagosRegistrados;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldo;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCaption;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalFacturacion;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalAdultoMayor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalPagosRegistrados;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldo;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle DetailDataOdd;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Inicio;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Fin;
    }
}
