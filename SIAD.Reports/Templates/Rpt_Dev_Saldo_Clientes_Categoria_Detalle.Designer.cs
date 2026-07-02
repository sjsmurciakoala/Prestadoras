namespace SIAD.Reports
{
    partial class Rpt_Dev_Saldo_Clientes_Categoria_Detalle
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Saldo_Clientes_Categoria_Detalle));
            DevExpress.XtraReports.UI.XRSummary xrSummaryCount = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryAnt = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryDeb = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryCre = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryAct = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryTotalCount = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryTotalAnt = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryTotalDeb = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryTotalCre = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryTotalAct = new DevExpress.XtraReports.UI.XRSummary();
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.pageInfoFecha = new DevExpress.XtraReports.UI.XRPageInfo();
            this.pageInfoPagina = new DevExpress.XtraReports.UI.XRPageInfo();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.lblFechaReporte = new DevExpress.XtraReports.UI.XRLabel();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderNombre = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRuta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellNombre = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellRuta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.GroupHeader1 = new DevExpress.XtraReports.UI.GroupHeaderBand();
            this.lblGroupCiclo = new DevExpress.XtraReports.UI.XRLabel();
            this.GroupFooter1 = new DevExpress.XtraReports.UI.GroupFooterBand();
            this.tblGroupFooter = new DevExpress.XtraReports.UI.XRTable();
            this.tblGroupFooterRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellGroupCount = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupTotalCaption = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupTotalAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupTotalDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupTotalCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGroupTotalSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalCount = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCaption = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Desde = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Hasta = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Categoria_Servicio_ID = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblGroupFooter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblTotal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this)).BeginInit();
            // 
            // TopMargin
            // 
            this.TopMargin.HeightF = 20F;
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
            this.pageInfoFecha.LocationFloat = new DevExpress.Utils.PointFloat(10F, 5F);
            this.pageInfoFecha.Name = "pageInfoFecha";
            this.pageInfoFecha.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            this.pageInfoFecha.SizeF = new System.Drawing.SizeF(150F, 20F);
            this.pageInfoFecha.StyleName = "PageInfoStyle";
            this.pageInfoFecha.TextFormatString = "{0:dd/MM/yyyy HH:mm}";
            // 
            // pageInfoPagina
            // 
            this.pageInfoPagina.LocationFloat = new DevExpress.Utils.PointFloat(910F, 5F);
            this.pageInfoPagina.Name = "pageInfoPagina";
            this.pageInfoPagina.SizeF = new System.Drawing.SizeF(100F, 20F);
            this.pageInfoPagina.StyleName = "PageInfoStyle";
            this.pageInfoPagina.StylePriority.UseTextAlignment = false;
            this.pageInfoPagina.TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopRight;
            this.pageInfoPagina.TextFormatString = "Pág. {0} de {1}";
            // 
            // ReportHeader
            // 
            this.ReportHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblEmpresa,
            this.lblTitulo,
            this.lblFechaReporte});
            this.ReportHeader.HeightF = 85F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 13F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(0F, 5F);
            this.lblEmpresa.Multiline = true;
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(1020F, 25F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[titulo_reporte]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(0F, 32F);
            this.lblTitulo.Multiline = true;
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(1020F, 22F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblFechaReporte
            // 
            this.lblFechaReporte.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "\'EMISION: \' + [fecha_reporte_texto]")});
            this.lblFechaReporte.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.5F);
            this.lblFechaReporte.LocationFloat = new DevExpress.Utils.PointFloat(0F, 58F);
            this.lblFechaReporte.Multiline = true;
            this.lblFechaReporte.Name = "lblFechaReporte";
            this.lblFechaReporte.SizeF = new System.Drawing.SizeF(1020F, 18F);
            this.lblFechaReporte.StylePriority.UseFont = false;
            this.lblFechaReporte.StylePriority.UseTextAlignment = false;
            this.lblFechaReporte.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // PageHeader
            // 
            this.PageHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblHeader});
            this.PageHeader.HeightF = 28F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(1020F, 25F);
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderCliente,
            this.cellHeaderNombre,
            this.cellHeaderRuta,
            this.cellHeaderAnterior,
            this.cellHeaderDebitos,
            this.cellHeaderCreditos,
            this.cellHeaderSaldoActual});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderCliente
            // 
            this.cellHeaderCliente.Name = "cellHeaderCliente";
            this.cellHeaderCliente.StyleName = "HeaderCaption";
            this.cellHeaderCliente.StylePriority.UseTextAlignment = false;
            this.cellHeaderCliente.Text = "Cliente";
            this.cellHeaderCliente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellHeaderCliente.Weight = 0.9D;
            // 
            // cellHeaderNombre
            // 
            this.cellHeaderNombre.Name = "cellHeaderNombre";
            this.cellHeaderNombre.StyleName = "HeaderCaption";
            this.cellHeaderNombre.StylePriority.UseTextAlignment = false;
            this.cellHeaderNombre.Text = "Nombre del Abonado";
            this.cellHeaderNombre.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellHeaderNombre.Weight = 3.2D;
            // 
            // cellHeaderRuta
            // 
            this.cellHeaderRuta.Name = "cellHeaderRuta";
            this.cellHeaderRuta.StyleName = "HeaderCaption";
            this.cellHeaderRuta.StylePriority.UseTextAlignment = false;
            this.cellHeaderRuta.Text = "Ruta / Clave";
            this.cellHeaderRuta.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellHeaderRuta.Weight = 1.1D;
            // 
            // cellHeaderAnterior
            // 
            this.cellHeaderAnterior.Name = "cellHeaderAnterior";
            this.cellHeaderAnterior.StyleName = "HeaderCaption";
            this.cellHeaderAnterior.StylePriority.UseTextAlignment = false;
            this.cellHeaderAnterior.Text = "Saldo Anterior";
            this.cellHeaderAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderAnterior.Weight = 1.2D;
            // 
            // cellHeaderDebitos
            // 
            this.cellHeaderDebitos.Name = "cellHeaderDebitos";
            this.cellHeaderDebitos.StyleName = "HeaderCaption";
            this.cellHeaderDebitos.StylePriority.UseTextAlignment = false;
            this.cellHeaderDebitos.Text = "Debitos";
            this.cellHeaderDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderDebitos.Weight = 1.2D;
            // 
            // cellHeaderCreditos
            // 
            this.cellHeaderCreditos.Name = "cellHeaderCreditos";
            this.cellHeaderCreditos.StyleName = "HeaderCaption";
            this.cellHeaderCreditos.StylePriority.UseTextAlignment = false;
            this.cellHeaderCreditos.Text = "Creditos";
            this.cellHeaderCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderCreditos.Weight = 1.2D;
            // 
            // cellHeaderSaldoActual
            // 
            this.cellHeaderSaldoActual.Name = "cellHeaderSaldoActual";
            this.cellHeaderSaldoActual.StyleName = "HeaderCaption";
            this.cellHeaderSaldoActual.StylePriority.UseTextAlignment = false;
            this.cellHeaderSaldoActual.Text = "Saldo Actual";
            this.cellHeaderSaldoActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderSaldoActual.Weight = 1.4D;
            // 
            // Detail
            // 
            this.Detail.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblDetail});
            this.Detail.HeightF = 22F;
            this.Detail.Name = "Detail";
            // 
            // tblDetail
            // 
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(1020F, 20F);
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellCliente,
            this.cellNombre,
            this.cellRuta,
            this.cellAnterior,
            this.cellDebitos,
            this.cellCreditos,
            this.cellSaldoActual});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellCliente
            // 
            this.cellCliente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_codigo]")});
            this.cellCliente.Name = "cellCliente";
            this.cellCliente.StyleName = "DetailData";
            this.cellCliente.Weight = 0.9D;
            // 
            // cellNombre
            // 
            this.cellNombre.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_nombre]")});
            this.cellNombre.Name = "cellNombre";
            this.cellNombre.StyleName = "DetailData";
            this.cellNombre.Weight = 3.2D;
            // 
            // cellRuta
            // 
            this.cellRuta.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ruta]")});
            this.cellRuta.Name = "cellRuta";
            this.cellRuta.StyleName = "DetailData";
            this.cellRuta.Weight = 1.1D;
            // 
            // cellAnterior
            // 
            this.cellAnterior.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_anterior]")});
            this.cellAnterior.Name = "cellAnterior";
            this.cellAnterior.StyleName = "DetailData";
            this.cellAnterior.StylePriority.UseTextAlignment = false;
            this.cellAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAnterior.TextFormatString = "{0:n2}";
            this.cellAnterior.Weight = 1.2D;
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
            this.cellDebitos.Weight = 1.2D;
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
            this.cellCreditos.Weight = 1.2D;
            // 
            // cellSaldoActual
            // 
            this.cellSaldoActual.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_actual]")});
            this.cellSaldoActual.Name = "cellSaldoActual";
            this.cellSaldoActual.StyleName = "DetailData";
            this.cellSaldoActual.StylePriority.UseTextAlignment = false;
            this.cellSaldoActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSaldoActual.TextFormatString = "{0:n2}";
            this.cellSaldoActual.Weight = 1.4D;
            // 
            // GroupHeader1
            // 
            this.GroupHeader1.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblGroupCiclo});
            this.GroupHeader1.GroupFields.AddRange(new DevExpress.XtraReports.UI.GroupField[] {
            new DevExpress.XtraReports.UI.GroupField("ciclo", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending)});
            this.GroupHeader1.HeightF = 30F;
            this.GroupHeader1.Name = "GroupHeader1";
            // 
            // lblGroupCiclo
            // 
            this.lblGroupCiclo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "\'CICLO : \' + [ciclo]")});
            this.lblGroupCiclo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblGroupCiclo.LocationFloat = new DevExpress.Utils.PointFloat(0F, 5F);
            this.lblGroupCiclo.Multiline = true;
            this.lblGroupCiclo.Name = "lblGroupCiclo";
            this.lblGroupCiclo.SizeF = new System.Drawing.SizeF(500F, 22F);
            this.lblGroupCiclo.StylePriority.UseFont = false;
            this.lblGroupCiclo.StylePriority.UseTextAlignment = false;
            this.lblGroupCiclo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // GroupFooter1
            // 
            this.GroupFooter1.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblGroupFooter});
            this.GroupFooter1.HeightF = 32F;
            this.GroupFooter1.Name = "GroupFooter1";
            // 
            // tblGroupFooter
            // 
            this.tblGroupFooter.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblGroupFooter.LocationFloat = new DevExpress.Utils.PointFloat(0F, 2F);
            this.tblGroupFooter.Name = "tblGroupFooter";
            this.tblGroupFooter.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblGroupFooterRow});
            this.tblGroupFooter.SizeF = new System.Drawing.SizeF(1020F, 25F);
            this.tblGroupFooter.StylePriority.UseBorders = false;
            // 
            // tblGroupFooterRow
            // 
            this.tblGroupFooterRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellGroupCount,
            this.cellGroupTotalCaption,
            this.cellGroupTotalAnterior,
            this.cellGroupTotalDebitos,
            this.cellGroupTotalCreditos,
            this.cellGroupTotalSaldoActual});
            this.tblGroupFooterRow.Name = "tblGroupFooterRow";
            this.tblGroupFooterRow.Weight = 1D;
            // 
            // cellGroupCount
            // 
            this.cellGroupCount.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumCount([cliente_codigo])")});
            this.cellGroupCount.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellGroupCount.Name = "cellGroupCount";
            this.cellGroupCount.StylePriority.UseFont = false;
            this.cellGroupCount.StylePriority.UseTextAlignment = false;
            xrSummaryCount.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupCount.Summary = xrSummaryCount;
            this.cellGroupCount.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellGroupCount.TextFormatString = "{0:n0}";
            this.cellGroupCount.Weight = 0.9D;
            // 
            // cellGroupTotalCaption
            // 
            this.cellGroupTotalCaption.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "\'TOTAL CICLO : \' + [ciclo]")});
            this.cellGroupTotalCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellGroupTotalCaption.Name = "cellGroupTotalCaption";
            this.cellGroupTotalCaption.StylePriority.UseFont = false;
            this.cellGroupTotalCaption.StylePriority.UseTextAlignment = false;
            this.cellGroupTotalCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellGroupTotalCaption.Weight = 4.3D;
            // 
            // cellGroupTotalAnterior
            // 
            this.cellGroupTotalAnterior.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_anterior])")});
            this.cellGroupTotalAnterior.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellGroupTotalAnterior.Name = "cellGroupTotalAnterior";
            this.cellGroupTotalAnterior.StylePriority.UseFont = false;
            this.cellGroupTotalAnterior.StylePriority.UseTextAlignment = false;
            xrSummaryAnt.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupTotalAnterior.Summary = xrSummaryAnt;
            this.cellGroupTotalAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellGroupTotalAnterior.TextFormatString = "{0:n2}";
            this.cellGroupTotalAnterior.Weight = 1.2D;
            // 
            // cellGroupTotalDebitos
            // 
            this.cellGroupTotalDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([debitos])")});
            this.cellGroupTotalDebitos.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellGroupTotalDebitos.Name = "cellGroupTotalDebitos";
            this.cellGroupTotalDebitos.StylePriority.UseFont = false;
            this.cellGroupTotalDebitos.StylePriority.UseTextAlignment = false;
            xrSummaryDeb.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupTotalDebitos.Summary = xrSummaryDeb;
            this.cellGroupTotalDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellGroupTotalDebitos.TextFormatString = "{0:n2}";
            this.cellGroupTotalDebitos.Weight = 1.2D;
            // 
            // cellGroupTotalCreditos
            // 
            this.cellGroupTotalCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([creditos])")});
            this.cellGroupTotalCreditos.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellGroupTotalCreditos.Name = "cellGroupTotalCreditos";
            this.cellGroupTotalCreditos.StylePriority.UseFont = false;
            this.cellGroupTotalCreditos.StylePriority.UseTextAlignment = false;
            xrSummaryCre.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupTotalCreditos.Summary = xrSummaryCre;
            this.cellGroupTotalCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellGroupTotalCreditos.TextFormatString = "{0:n2}";
            this.cellGroupTotalCreditos.Weight = 1.2D;
            // 
            // cellGroupTotalSaldoActual
            // 
            this.cellGroupTotalSaldoActual.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_actual])")});
            this.cellGroupTotalSaldoActual.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellGroupTotalSaldoActual.Name = "cellGroupTotalSaldoActual";
            this.cellGroupTotalSaldoActual.StylePriority.UseFont = false;
            this.cellGroupTotalSaldoActual.StylePriority.UseTextAlignment = false;
            xrSummaryAct.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellGroupTotalSaldoActual.Summary = xrSummaryAct;
            this.cellGroupTotalSaldoActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellGroupTotalSaldoActual.TextFormatString = "{0:n2}";
            this.cellGroupTotalSaldoActual.Weight = 1.4D;
            // 
            // ReportFooter
            // 
            this.ReportFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblTotal});
            this.ReportFooter.HeightF = 35F;
            this.ReportFooter.Name = "ReportFooter";
            // 
            // tblTotal
            // 
            this.tblTotal.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblTotal.BorderWidth = 2F;
            this.tblTotal.LocationFloat = new DevExpress.Utils.PointFloat(0F, 5F);
            this.tblTotal.Name = "tblTotal";
            this.tblTotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblTotalRow});
            this.tblTotal.SizeF = new System.Drawing.SizeF(1020F, 25F);
            this.tblTotal.StylePriority.UseBorders = false;
            this.tblTotal.StylePriority.UseBorderWidth = false;
            // 
            // tblTotalRow
            // 
            this.tblTotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellTotalCount,
            this.cellTotalCaption,
            this.cellTotalAnterior,
            this.cellTotalDebitos,
            this.cellTotalCreditos,
            this.cellTotalSaldoActual});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalCount
            // 
            this.cellTotalCount.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumCount([cliente_codigo])")});
            this.cellTotalCount.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalCount.Name = "cellTotalCount";
            this.cellTotalCount.StylePriority.UseFont = false;
            this.cellTotalCount.StylePriority.UseTextAlignment = false;
            xrSummaryTotalCount.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCount.Summary = xrSummaryTotalCount;
            this.cellTotalCount.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellTotalCount.TextFormatString = "{0:n0}";
            this.cellTotalCount.Weight = 0.9D;
            // 
            // cellTotalCaption
            // 
            this.cellTotalCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalCaption.Name = "cellTotalCaption";
            this.cellTotalCaption.StylePriority.UseFont = false;
            this.cellTotalCaption.StylePriority.UseTextAlignment = false;
            this.cellTotalCaption.Text = "TOTAL GENERAL :";
            this.cellTotalCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellTotalCaption.Weight = 4.3D;
            // 
            // cellTotalAnterior
            // 
            this.cellTotalAnterior.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_anterior])")});
            this.cellTotalAnterior.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalAnterior.Name = "cellTotalAnterior";
            this.cellTotalAnterior.StylePriority.UseFont = false;
            this.cellTotalAnterior.StylePriority.UseTextAlignment = false;
            xrSummaryTotalAnt.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalAnterior.Summary = xrSummaryTotalAnt;
            this.cellTotalAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalAnterior.TextFormatString = "{0:n2}";
            this.cellTotalAnterior.Weight = 1.2D;
            // 
            // cellTotalDebitos
            // 
            this.cellTotalDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([debitos])")});
            this.cellTotalDebitos.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalDebitos.Name = "cellTotalDebitos";
            this.cellTotalDebitos.StylePriority.UseFont = false;
            this.cellTotalDebitos.StylePriority.UseTextAlignment = false;
            xrSummaryTotalDeb.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalDebitos.Summary = xrSummaryTotalDeb;
            this.cellTotalDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalDebitos.TextFormatString = "{0:n2}";
            this.cellTotalDebitos.Weight = 1.2D;
            // 
            // cellTotalCreditos
            // 
            this.cellTotalCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([creditos])")});
            this.cellTotalCreditos.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalCreditos.Name = "cellTotalCreditos";
            this.cellTotalCreditos.StylePriority.UseFont = false;
            this.cellTotalCreditos.StylePriority.UseTextAlignment = false;
            xrSummaryTotalCre.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCreditos.Summary = xrSummaryTotalCre;
            this.cellTotalCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCreditos.TextFormatString = "{0:n2}";
            this.cellTotalCreditos.Weight = 1.2D;
            // 
            // cellTotalSaldoActual
            // 
            this.cellTotalSaldoActual.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_actual])")});
            this.cellTotalSaldoActual.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalSaldoActual.Name = "cellTotalSaldoActual";
            this.cellTotalSaldoActual.StylePriority.UseFont = false;
            this.cellTotalSaldoActual.StylePriority.UseTextAlignment = false;
            xrSummaryTotalAct.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldoActual.Summary = xrSummaryTotalAct;
            this.cellTotalSaldoActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldoActual.TextFormatString = "{0:n2}";
            this.cellTotalSaldoActual.Weight = 1.4D;
            // 
            // sqlDataSource1
            // 
            this.sqlDataSource1.ConnectionName = "DefaultConnection";
            this.sqlDataSource1.Name = "sqlDataSource1";
            customSqlQuery1.Name = "Query";
            queryParameter1.Name = "CompaniaID";
            queryParameter1.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter1.Value = new DevExpress.DataAccess.Expression("?p_Compania_ID", typeof(int));
            queryParameter2.Name = "FechaDesde";
            queryParameter2.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter2.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Desde", typeof(System.DateOnly));
            queryParameter3.Name = "FechaHasta";
            queryParameter3.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Hasta", typeof(System.DateOnly));
            queryParameter4.Name = "CategoriaServicioId";
            queryParameter4.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Categoria_Servicio_ID", typeof(int));
            customSqlQuery1.Parameters.AddRange(new DevExpress.DataAccess.Sql.QueryParameter[] {
            queryParameter1,
            queryParameter2,
            queryParameter3,
            queryParameter4});
            customSqlQuery1.Sql = "SELECT * FROM public.rep_saldo_clientes_categoria_detalle(:CompaniaID, :FechaDesde, :FechaHasta, :CategoriaServicioId) ORDER BY ciclo_orden, ciclo, cliente_codigo;";
            this.sqlDataSource1.Queries.AddRange(new DevExpress.DataAccess.Sql.SqlQuery[] {
            customSqlQuery1});
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
            this.HeaderCaption.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.HeaderCaption.ForeColor = System.Drawing.Color.Black;
            this.HeaderCaption.Name = "HeaderCaption";
            this.HeaderCaption.Padding = new DevExpress.XtraPrinting.PaddingInfo(3, 3, 0, 0, 100F);
            this.HeaderCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // DetailData
            // 
            this.DetailData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F);
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
            this.TotalData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.5F, DevExpress.Drawing.DXFontStyle.Bold);
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
            // p_Fecha_Desde
            // 
            this.p_Fecha_Desde.Name = "p_Fecha_Desde";
            this.p_Fecha_Desde.Type = typeof(global::System.DateOnly);
            // 
            // p_Fecha_Hasta
            // 
            this.p_Fecha_Hasta.Name = "p_Fecha_Hasta";
            this.p_Fecha_Hasta.Type = typeof(global::System.DateOnly);
            // 
            // p_Categoria_Servicio_ID
            // 
            this.p_Categoria_Servicio_ID.Name = "p_Categoria_Servicio_ID";
            this.p_Categoria_Servicio_ID.Type = typeof(int);
            this.p_Categoria_Servicio_ID.ValueInfo = "0";
            // 
            // Rpt_Dev_Saldo_Clientes_Categoria_Detalle
            // 
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Desde,
            this.p_Fecha_Hasta,
            this.p_Categoria_Servicio_ID});
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.ReportHeader,
            this.PageHeader,
            this.Detail,
            this.GroupHeader1,
            this.GroupFooter1,
            this.ReportFooter});
            this.ComponentStorage.AddRange(new System.ComponentModel.IComponent[] {
            this.sqlDataSource1});
            this.DataMember = "Query";
            this.DataSource = this.sqlDataSource1;
            this.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9.75F);
            this.Landscape = true;
            this.Margins = new DevExpress.Drawing.DXMargins(40, 40, 20, 32);
            this.PageHeight = 850;
            this.PageWidth = 1100;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.Title,
            this.HeaderCaption,
            this.DetailData,
            this.PageInfoStyle,
            this.TotalData});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblGroupFooter)).EndInit();
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
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderNombre;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRuta;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSaldoActual;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellNombre;
        private DevExpress.XtraReports.UI.XRTableCell cellRuta;
        private DevExpress.XtraReports.UI.XRTableCell cellAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldoActual;
        private DevExpress.XtraReports.UI.GroupHeaderBand GroupHeader1;
        private DevExpress.XtraReports.UI.XRLabel lblGroupCiclo;
        private DevExpress.XtraReports.UI.GroupFooterBand GroupFooter1;
        private DevExpress.XtraReports.UI.XRTable tblGroupFooter;
        private DevExpress.XtraReports.UI.XRTableRow tblGroupFooterRow;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupCount;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupTotalCaption;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupTotalAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupTotalDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupTotalCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellGroupTotalSaldoActual;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCount;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCaption;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldoActual;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Desde;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Hasta;
        private DevExpress.XtraReports.Parameters.Parameter p_Categoria_Servicio_ID;
    }
}
