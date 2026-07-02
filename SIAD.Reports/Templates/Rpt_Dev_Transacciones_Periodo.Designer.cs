namespace SIAD.Reports
{
    partial class Rpt_Dev_Transacciones_Periodo
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Transacciones_Periodo));
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
            this.cellHeaderConcepto = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAgua = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAlc = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAmbiental = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderErsap = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderConvenio = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderGestion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderOtros = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellConcepto = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAgua = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAlc = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAmbiental = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellErsap = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellConvenio = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGestion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellOtros = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailDataOdd = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Inicio = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Fin = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
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
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([periodo_titulo])")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(65F, 42F);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(1150F, 30F);
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
            this.tblHeader.SizeF = new System.Drawing.SizeF(1290F, 32F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderConcepto,
            this.cellHeaderAgua,
            this.cellHeaderAlc,
            this.cellHeaderAmbiental,
            this.cellHeaderErsap,
            this.cellHeaderConvenio,
            this.cellHeaderGestion,
            this.cellHeaderOtros,
            this.cellHeaderTotal});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderConcepto
            // 
            this.cellHeaderConcepto.Name = "cellHeaderConcepto";
            this.cellHeaderConcepto.Text = "CONCEPTO";
            this.cellHeaderConcepto.Weight = 1.85D;
            // 
            // cellHeaderAgua
            // 
            this.cellHeaderAgua.Name = "cellHeaderAgua";
            this.cellHeaderAgua.Text = "AGUA POTABLE";
            this.cellHeaderAgua.Weight = 1.1D;
            // 
            // cellHeaderAlc
            // 
            this.cellHeaderAlc.Name = "cellHeaderAlc";
            this.cellHeaderAlc.Text = "ALCANTARILLADO SANITARIO";
            this.cellHeaderAlc.Weight = 1.15D;
            // 
            // cellHeaderAmbiental
            // 
            this.cellHeaderAmbiental.Name = "cellHeaderAmbiental";
            this.cellHeaderAmbiental.Text = "AMBIENTAL";
            this.cellHeaderAmbiental.Weight = 0.95D;
            // 
            // cellHeaderErsap
            // 
            this.cellHeaderErsap.Name = "cellHeaderErsap";
            this.cellHeaderErsap.Text = "TASA ERSAP";
            this.cellHeaderErsap.Weight = 0.95D;
            // 
            // cellHeaderConvenio
            // 
            this.cellHeaderConvenio.Name = "cellHeaderConvenio";
            this.cellHeaderConvenio.Text = "CONVENIO";
            this.cellHeaderConvenio.Weight = 0.95D;
            // 
            // cellHeaderGestion
            // 
            this.cellHeaderGestion.Name = "cellHeaderGestion";
            this.cellHeaderGestion.Text = "GESTION LEGAL";
            this.cellHeaderGestion.Weight = 1D;
            // 
            // cellHeaderOtros
            // 
            this.cellHeaderOtros.Name = "cellHeaderOtros";
            this.cellHeaderOtros.Text = "OTROS CARGOS";
            this.cellHeaderOtros.Weight = 1D;
            // 
            // cellHeaderTotal
            // 
            this.cellHeaderTotal.Name = "cellHeaderTotal";
            this.cellHeaderTotal.Text = "TOTAL";
            this.cellHeaderTotal.Weight = 1D;
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
            this.cellConcepto,
            this.cellAgua,
            this.cellAlc,
            this.cellAmbiental,
            this.cellErsap,
            this.cellConvenio,
            this.cellGestion,
            this.cellOtros,
            this.cellTotal});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellConcepto
            // 
            this.cellConcepto.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[concepto]")});
            this.cellConcepto.Name = "cellConcepto";
            this.cellConcepto.Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 2, 0, 0, 100F);
            this.cellConcepto.StyleName = "DetailData";
            this.cellConcepto.StylePriority.UsePadding = false;
            this.cellConcepto.StylePriority.UseTextAlignment = false;
            this.cellConcepto.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellConcepto.Weight = 1.85D;
            // 
            // cellAgua
            // 
            this.cellAgua.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[agua_potable]")});
            this.cellAgua.Name = "cellAgua";
            this.cellAgua.StyleName = "DetailData";
            this.cellAgua.StylePriority.UseTextAlignment = false;
            this.cellAgua.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAgua.TextFormatString = "{0:n2}";
            this.cellAgua.Weight = 1.1D;
            // 
            // cellAlc
            // 
            this.cellAlc.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[alcantarillado_sanitario]")});
            this.cellAlc.Name = "cellAlc";
            this.cellAlc.StyleName = "DetailData";
            this.cellAlc.StylePriority.UseTextAlignment = false;
            this.cellAlc.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAlc.TextFormatString = "{0:n2}";
            this.cellAlc.Weight = 1.15D;
            // 
            // cellAmbiental
            // 
            this.cellAmbiental.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ambiental]")});
            this.cellAmbiental.Name = "cellAmbiental";
            this.cellAmbiental.StyleName = "DetailData";
            this.cellAmbiental.StylePriority.UseTextAlignment = false;
            this.cellAmbiental.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAmbiental.TextFormatString = "{0:n2}";
            this.cellAmbiental.Weight = 0.95D;
            // 
            // cellErsap
            // 
            this.cellErsap.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[tasa_ersap]")});
            this.cellErsap.Name = "cellErsap";
            this.cellErsap.StyleName = "DetailData";
            this.cellErsap.StylePriority.UseTextAlignment = false;
            this.cellErsap.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellErsap.TextFormatString = "{0:n2}";
            this.cellErsap.Weight = 0.95D;
            // 
            // cellConvenio
            // 
            this.cellConvenio.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[convenio]")});
            this.cellConvenio.Name = "cellConvenio";
            this.cellConvenio.StyleName = "DetailData";
            this.cellConvenio.StylePriority.UseTextAlignment = false;
            this.cellConvenio.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellConvenio.TextFormatString = "{0:n2}";
            this.cellConvenio.Weight = 0.95D;
            // 
            // cellGestion
            // 
            this.cellGestion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[gestion_legal]")});
            this.cellGestion.Name = "cellGestion";
            this.cellGestion.StyleName = "DetailData";
            this.cellGestion.StylePriority.UseTextAlignment = false;
            this.cellGestion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellGestion.TextFormatString = "{0:n2}";
            this.cellGestion.Weight = 1D;
            // 
            // cellOtros
            // 
            this.cellOtros.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[otros_cargos]")});
            this.cellOtros.Name = "cellOtros";
            this.cellOtros.StyleName = "DetailData";
            this.cellOtros.StylePriority.UseTextAlignment = false;
            this.cellOtros.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellOtros.TextFormatString = "{0:n2}";
            this.cellOtros.Weight = 1D;
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
            this.cellTotal.Weight = 1D;
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
            // Rpt_Dev_Transacciones_Periodo
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.ReportHeader,
            this.PageHeader,
            this.Detail});
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
            this.PageInfoStyle});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).EndInit();
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
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderConcepto;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAgua;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAlc;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAmbiental;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderErsap;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderConvenio;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderGestion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderOtros;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTotal;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellConcepto;
        private DevExpress.XtraReports.UI.XRTableCell cellAgua;
        private DevExpress.XtraReports.UI.XRTableCell cellAlc;
        private DevExpress.XtraReports.UI.XRTableCell cellAmbiental;
        private DevExpress.XtraReports.UI.XRTableCell cellErsap;
        private DevExpress.XtraReports.UI.XRTableCell cellConvenio;
        private DevExpress.XtraReports.UI.XRTableCell cellGestion;
        private DevExpress.XtraReports.UI.XRTableCell cellOtros;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle DetailDataOdd;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Inicio;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Fin;
    }
}
