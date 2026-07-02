namespace SIAD.Reports
{
    partial class Rpt_Dev_Movimiento_Periodo
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Movimiento_Periodo));
            DevExpress.XtraReports.UI.XRSummary xrSummary1 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary2 = new DevExpress.XtraReports.UI.XRSummary();
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.pageInfoFecha = new DevExpress.XtraReports.UI.XRPageInfo();
            this.pageInfoPagina = new DevExpress.XtraReports.UI.XRPageInfo();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaTexto = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaNumero = new DevExpress.XtraReports.UI.XRPageInfo();
            this.lblFechaReporte = new DevExpress.XtraReports.UI.XRLabel();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTrans = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderDescripcion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTrans = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDescripcion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalTrans = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDescripcion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
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
            this.lblEmpresa,
            this.lblTitulo,
            this.lblPaginaTexto,
            this.lblPaginaNumero,
            this.lblFechaReporte});
            this.ReportHeader.HeightF = 102F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([empresa_nombre])")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 18F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(135F, 12F);
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(650F, 30F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 15F, DevExpress.Drawing.DXFontStyle.Italic);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(125F, 48F);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(680F, 28F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblPaginaTexto
            // 
            this.lblPaginaTexto.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.lblPaginaTexto.LocationFloat = new DevExpress.Utils.PointFloat(850F, 14F);
            this.lblPaginaTexto.Name = "lblPaginaTexto";
            this.lblPaginaTexto.SizeF = new System.Drawing.SizeF(90F, 24F);
            this.lblPaginaTexto.StylePriority.UseFont = false;
            this.lblPaginaTexto.StylePriority.UseTextAlignment = false;
            this.lblPaginaTexto.Text = "Pagina";
            this.lblPaginaTexto.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // lblPaginaNumero
            // 
            this.lblPaginaNumero.LocationFloat = new DevExpress.Utils.PointFloat(945F, 14F);
            this.lblPaginaNumero.Name = "lblPaginaNumero";
            this.lblPaginaNumero.PageInfo = DevExpress.XtraPrinting.PageInfo.Number;
            this.lblPaginaNumero.SizeF = new System.Drawing.SizeF(55F, 24F);
            this.lblPaginaNumero.StylePriority.UseTextAlignment = false;
            this.lblPaginaNumero.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.lblPaginaNumero.TextFormatString = "{0}";
            // 
            // lblFechaReporte
            // 
            this.lblFechaReporte.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]")});
            this.lblFechaReporte.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.lblFechaReporte.LocationFloat = new DevExpress.Utils.PointFloat(835F, 48F);
            this.lblFechaReporte.Name = "lblFechaReporte";
            this.lblFechaReporte.SizeF = new System.Drawing.SizeF(165F, 24F);
            this.lblFechaReporte.StylePriority.UseFont = false;
            this.lblFechaReporte.StylePriority.UseTextAlignment = false;
            this.lblFechaReporte.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
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
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(18F, 6F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(982F, 30F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderFecha,
            this.cellHeaderTrans,
            this.cellHeaderDescripcion,
            this.cellHeaderDebitos,
            this.cellHeaderCreditos,
            this.cellHeaderSaldo});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderFecha
            // 
            this.cellHeaderFecha.Name = "cellHeaderFecha";
            this.cellHeaderFecha.Text = "Fecha";
            this.cellHeaderFecha.Weight = 1.05D;
            // 
            // cellHeaderTrans
            // 
            this.cellHeaderTrans.Name = "cellHeaderTrans";
            this.cellHeaderTrans.Text = "Trans.";
            this.cellHeaderTrans.Weight = 0.9D;
            // 
            // cellHeaderDescripcion
            // 
            this.cellHeaderDescripcion.Name = "cellHeaderDescripcion";
            this.cellHeaderDescripcion.Text = "Descripcion";
            this.cellHeaderDescripcion.Weight = 4.2D;
            // 
            // cellHeaderDebitos
            // 
            this.cellHeaderDebitos.Name = "cellHeaderDebitos";
            this.cellHeaderDebitos.Text = "Debitos";
            this.cellHeaderDebitos.Weight = 1.35D;
            // 
            // cellHeaderCreditos
            // 
            this.cellHeaderCreditos.Name = "cellHeaderCreditos";
            this.cellHeaderCreditos.Text = "Creditos";
            this.cellHeaderCreditos.Weight = 1.35D;
            // 
            // cellHeaderSaldo
            // 
            this.cellHeaderSaldo.Name = "cellHeaderSaldo";
            this.cellHeaderSaldo.Text = "Saldo";
            this.cellHeaderSaldo.Weight = 1.45D;
            // 
            // Detail
            // 
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblDetail});
            this.Detail.HeightF = 26F;
            this.Detail.Name = "Detail";
            // 
            // tblDetail
            // 
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F);
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(18F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(982F, 26F);
            this.tblDetail.StylePriority.UseFont = false;
            this.tblDetail.StylePriority.UseTextAlignment = false;
            this.tblDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellFecha,
            this.cellTrans,
            this.cellDescripcion,
            this.cellDebitos,
            this.cellCreditos,
            this.cellSaldo});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellFecha
            // 
            this.cellFecha.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_texto]")});
            this.cellFecha.Name = "cellFecha";
            this.cellFecha.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellFecha.StyleName = "DetailData";
            this.cellFecha.StylePriority.UsePadding = false;
            this.cellFecha.StylePriority.UseTextAlignment = false;
            this.cellFecha.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellFecha.Weight = 1.05D;
            // 
            // cellTrans
            // 
            this.cellTrans.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[transaccion_codigo]")});
            this.cellTrans.Name = "cellTrans";
            this.cellTrans.StyleName = "DetailData";
            this.cellTrans.StylePriority.UseTextAlignment = false;
            this.cellTrans.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellTrans.Weight = 0.9D;
            // 
            // cellDescripcion
            // 
            this.cellDescripcion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[descripcion]")});
            this.cellDescripcion.Name = "cellDescripcion";
            this.cellDescripcion.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellDescripcion.StyleName = "DetailData";
            this.cellDescripcion.StylePriority.UsePadding = false;
            this.cellDescripcion.StylePriority.UseTextAlignment = false;
            this.cellDescripcion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellDescripcion.Weight = 4.2D;
            // 
            // cellDebitos
            // 
            this.cellDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Iif([debitos] = 0, '', FormatString('{0:n2}', [debitos]))")});
            this.cellDebitos.Name = "cellDebitos";
            this.cellDebitos.StyleName = "DetailData";
            this.cellDebitos.StylePriority.UseTextAlignment = false;
            this.cellDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellDebitos.Weight = 1.35D;
            // 
            // cellCreditos
            // 
            this.cellCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Iif([creditos] = 0, '', FormatString('{0:n2}', [creditos]))")});
            this.cellCreditos.Name = "cellCreditos";
            this.cellCreditos.StyleName = "DetailData";
            this.cellCreditos.StylePriority.UseTextAlignment = false;
            this.cellCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCreditos.Weight = 1.35D;
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
            this.cellSaldo.Weight = 1.45D;
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
            this.tblTotal.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblTotal.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblTotal.LocationFloat = new DevExpress.Utils.PointFloat(18F, 4F);
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
            this.cellTotalFecha,
            this.cellTotalTrans,
            this.cellTotalDescripcion,
            this.cellTotalDebitos,
            this.cellTotalCreditos,
            this.cellTotalSaldo});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalFecha
            // 
            this.cellTotalFecha.Name = "cellTotalFecha";
            this.cellTotalFecha.Weight = 1.05D;
            // 
            // cellTotalTrans
            // 
            this.cellTotalTrans.Name = "cellTotalTrans";
            this.cellTotalTrans.Weight = 0.9D;
            // 
            // cellTotalDescripcion
            // 
            this.cellTotalDescripcion.Name = "cellTotalDescripcion";
            this.cellTotalDescripcion.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 4, 0, 0, 100F);
            this.cellTotalDescripcion.StylePriority.UsePadding = false;
            this.cellTotalDescripcion.StylePriority.UseTextAlignment = false;
            this.cellTotalDescripcion.Text = "Total:";
            this.cellTotalDescripcion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalDescripcion.Weight = 4.2D;
            // 
            // cellTotalDebitos
            // 
            this.cellTotalDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([debitos])")});
            this.cellTotalDebitos.Name = "cellTotalDebitos";
            this.cellTotalDebitos.StylePriority.UseTextAlignment = false;
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalDebitos.Summary = xrSummary1;
            this.cellTotalDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalDebitos.TextFormatString = "{0:n2}";
            this.cellTotalDebitos.Weight = 1.35D;
            // 
            // cellTotalCreditos
            // 
            this.cellTotalCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([creditos])")});
            this.cellTotalCreditos.Name = "cellTotalCreditos";
            this.cellTotalCreditos.StylePriority.UseTextAlignment = false;
            xrSummary2.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCreditos.Summary = xrSummary2;
            this.cellTotalCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCreditos.TextFormatString = "{0:n2}";
            this.cellTotalCreditos.Weight = 1.35D;
            // 
            // cellTotalSaldo
            // 
            this.cellTotalSaldo.Name = "cellTotalSaldo";
            this.cellTotalSaldo.Weight = 1.45D;
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
            this.HeaderCaption.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.HeaderCaption.BorderWidth = 1F;
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.HeaderCaption.ForeColor = System.Drawing.Color.Black;
            this.HeaderCaption.Name = "HeaderCaption";
            this.HeaderCaption.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            this.HeaderCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // DetailData
            // 
            this.DetailData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F);
            this.DetailData.ForeColor = System.Drawing.Color.Black;
            this.DetailData.Name = "DetailData";
            this.DetailData.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            this.DetailData.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
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
            this.TotalData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
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
            // Rpt_Dev_Movimiento_Periodo
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
            this.p_Fecha_Inicio,
            this.p_Fecha_Fin});
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
        private DevExpress.XtraReports.UI.XRLabel lblEmpresa;
        private DevExpress.XtraReports.UI.XRLabel lblTitulo;
        private DevExpress.XtraReports.UI.XRLabel lblPaginaTexto;
        private DevExpress.XtraReports.UI.XRPageInfo lblPaginaNumero;
        private DevExpress.XtraReports.UI.XRLabel lblFechaReporte;
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTrans;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderDescripcion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSaldo;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellTrans;
        private DevExpress.XtraReports.UI.XRTableCell cellDescripcion;
        private DevExpress.XtraReports.UI.XRTableCell cellDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldo;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalTrans;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDescripcion;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldo;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Inicio;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Fin;
    }
}
