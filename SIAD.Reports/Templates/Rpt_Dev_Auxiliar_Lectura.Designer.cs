namespace SIAD.Reports
{
    partial class Rpt_Dev_Auxiliar_Lectura
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
            DevExpress.DataAccess.Sql.QueryParameter queryParameter5 = new DevExpress.DataAccess.Sql.QueryParameter();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Auxiliar_Lectura));
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
            this.cellHeaderSecuencia = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderClave = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderContador = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderLecturaAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderLecturaActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderConsumo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderUsuario = new DevExpress.XtraReports.UI.XRTableCell();
            this.GroupHeaderRuta = new DevExpress.XtraReports.UI.GroupHeaderBand();
            this.lblRuta = new DevExpress.XtraReports.UI.XRLabel();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellSecuencia = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellClave = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellContador = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellLecturaAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellLecturaActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellConsumo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellUsuario = new DevExpress.XtraReports.UI.XRTableCell();
            this.GroupFooterRuta = new DevExpress.XtraReports.UI.GroupFooterBand();
            this.tblSubtotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblSubtotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellSubtotalLabel = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSubtotalConsumo = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalLabel = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalConsumo = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Anio = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Mes = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Ciclo_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Solo_Pendientes = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblSubtotal)).BeginInit();
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
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
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
            this.cellHeaderSecuencia,
            this.cellHeaderClave,
            this.cellHeaderCliente,
            this.cellHeaderContador,
            this.cellHeaderLecturaAnterior,
            this.cellHeaderLecturaActual,
            this.cellHeaderConsumo,
            this.cellHeaderFecha,
            this.cellHeaderUsuario});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderSecuencia
            // 
            this.cellHeaderSecuencia.Name = "cellHeaderSecuencia";
            this.cellHeaderSecuencia.Text = "Sec.";
            this.cellHeaderSecuencia.Weight = 0.7D;
            // 
            // cellHeaderClave
            // 
            this.cellHeaderClave.Name = "cellHeaderClave";
            this.cellHeaderClave.Text = "Clave";
            this.cellHeaderClave.Weight = 1.15D;
            // 
            // cellHeaderCliente
            // 
            this.cellHeaderCliente.Name = "cellHeaderCliente";
            this.cellHeaderCliente.Text = "Cliente";
            this.cellHeaderCliente.Weight = 3.45D;
            // 
            // cellHeaderContador
            // 
            this.cellHeaderContador.Name = "cellHeaderContador";
            this.cellHeaderContador.Text = "Contador";
            this.cellHeaderContador.Weight = 1.2D;
            // 
            // cellHeaderLecturaAnterior
            // 
            this.cellHeaderLecturaAnterior.Name = "cellHeaderLecturaAnterior";
            this.cellHeaderLecturaAnterior.Text = "Lec. Ant.";
            this.cellHeaderLecturaAnterior.Weight = 1.05D;
            // 
            // cellHeaderLecturaActual
            // 
            this.cellHeaderLecturaActual.Name = "cellHeaderLecturaActual";
            this.cellHeaderLecturaActual.Text = "Lec. Act.";
            this.cellHeaderLecturaActual.Weight = 1.05D;
            // 
            // cellHeaderConsumo
            // 
            this.cellHeaderConsumo.Name = "cellHeaderConsumo";
            this.cellHeaderConsumo.Text = "Consumo";
            this.cellHeaderConsumo.Weight = 1.05D;
            // 
            // cellHeaderFecha
            // 
            this.cellHeaderFecha.Name = "cellHeaderFecha";
            this.cellHeaderFecha.Text = "Fecha";
            this.cellHeaderFecha.Weight = 1D;
            // 
            // cellHeaderUsuario
            // 
            this.cellHeaderUsuario.Name = "cellHeaderUsuario";
            this.cellHeaderUsuario.Text = "Usuario";
            this.cellHeaderUsuario.Weight = 1.35D;
            // 
            // GroupHeaderRuta
            // 
            this.GroupHeaderRuta.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblRuta});
            this.GroupHeaderRuta.GroupFields.AddRange(new DevExpress.XtraReports.UI.GroupField[] {
            new DevExpress.XtraReports.UI.GroupField("ruta_codigo", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending)});
            this.GroupHeaderRuta.HeightF = 28F;
            this.GroupHeaderRuta.Name = "GroupHeaderRuta";
            this.GroupHeaderRuta.RepeatEveryPage = true;
            // 
            // lblRuta
            // 
            this.lblRuta.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ruta_titulo]")});
            this.lblRuta.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblRuta.LocationFloat = new DevExpress.Utils.PointFloat(18F, 2F);
            this.lblRuta.Name = "lblRuta";
            this.lblRuta.SizeF = new System.Drawing.SizeF(320F, 22F);
            this.lblRuta.StylePriority.UseFont = false;
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
            this.cellSecuencia,
            this.cellClave,
            this.cellCliente,
            this.cellContador,
            this.cellLecturaAnterior,
            this.cellLecturaActual,
            this.cellConsumo,
            this.cellFecha,
            this.cellUsuario});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellSecuencia
            // 
            this.cellSecuencia.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[secuencia]")});
            this.cellSecuencia.Name = "cellSecuencia";
            this.cellSecuencia.StyleName = "DetailData";
            this.cellSecuencia.StylePriority.UseTextAlignment = false;
            this.cellSecuencia.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSecuencia.Weight = 0.7D;
            // 
            // cellClave
            // 
            this.cellClave.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[clave]")});
            this.cellClave.Name = "cellClave";
            this.cellClave.StyleName = "DetailData";
            this.cellClave.Weight = 1.15D;
            // 
            // cellCliente
            // 
            this.cellCliente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_nombre]")});
            this.cellCliente.Name = "cellCliente";
            this.cellCliente.StyleName = "DetailData";
            this.cellCliente.Weight = 3.45D;
            // 
            // cellContador
            // 
            this.cellContador.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[contador]")});
            this.cellContador.Name = "cellContador";
            this.cellContador.StyleName = "DetailData";
            this.cellContador.Weight = 1.2D;
            // 
            // cellLecturaAnterior
            // 
            this.cellLecturaAnterior.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[lectura_anterior]")});
            this.cellLecturaAnterior.Name = "cellLecturaAnterior";
            this.cellLecturaAnterior.StyleName = "DetailData";
            this.cellLecturaAnterior.StylePriority.UseTextAlignment = false;
            this.cellLecturaAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellLecturaAnterior.TextFormatString = "{0:n2}";
            this.cellLecturaAnterior.Weight = 1.05D;
            // 
            // cellLecturaActual
            // 
            this.cellLecturaActual.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[lectura_actual]")});
            this.cellLecturaActual.Name = "cellLecturaActual";
            this.cellLecturaActual.StyleName = "DetailData";
            this.cellLecturaActual.StylePriority.UseTextAlignment = false;
            this.cellLecturaActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellLecturaActual.TextFormatString = "{0:n2}";
            this.cellLecturaActual.Weight = 1.05D;
            // 
            // cellConsumo
            // 
            this.cellConsumo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[consumo]")});
            this.cellConsumo.Name = "cellConsumo";
            this.cellConsumo.StyleName = "DetailData";
            this.cellConsumo.StylePriority.UseTextAlignment = false;
            this.cellConsumo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellConsumo.TextFormatString = "{0:n2}";
            this.cellConsumo.Weight = 1.05D;
            // 
            // cellFecha
            // 
            this.cellFecha.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_lectura]")});
            this.cellFecha.Name = "cellFecha";
            this.cellFecha.StyleName = "DetailData";
            this.cellFecha.StylePriority.UseTextAlignment = false;
            this.cellFecha.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellFecha.TextFormatString = "{0:dd/MM/yyyy}";
            this.cellFecha.Weight = 1D;
            // 
            // cellUsuario
            // 
            this.cellUsuario.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[usuario]")});
            this.cellUsuario.Name = "cellUsuario";
            this.cellUsuario.StyleName = "DetailData";
            this.cellUsuario.Weight = 1.35D;
            // 
            // GroupFooterRuta
            // 
            this.GroupFooterRuta.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblSubtotal});
            this.GroupFooterRuta.HeightF = 28F;
            this.GroupFooterRuta.Name = "GroupFooterRuta";
            // 
            // tblSubtotal
            // 
            this.tblSubtotal.Borders = DevExpress.XtraPrinting.BorderSide.Top;
            this.tblSubtotal.LocationFloat = new DevExpress.Utils.PointFloat(18F, 2F);
            this.tblSubtotal.Name = "tblSubtotal";
            this.tblSubtotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblSubtotalRow});
            this.tblSubtotal.SizeF = new System.Drawing.SizeF(982F, 24F);
            this.tblSubtotal.StylePriority.UseBorders = false;
            this.tblSubtotal.StylePriority.UseTextAlignment = false;
            this.tblSubtotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblSubtotalRow
            // 
            this.tblSubtotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellSubtotalLabel,
            this.cellSubtotalConsumo});
            this.tblSubtotalRow.Name = "tblSubtotalRow";
            this.tblSubtotalRow.Weight = 1D;
            // 
            // cellSubtotalLabel
            // 
            this.cellSubtotalLabel.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellSubtotalLabel.Name = "cellSubtotalLabel";
            this.cellSubtotalLabel.StylePriority.UseFont = false;
            this.cellSubtotalLabel.StylePriority.UseTextAlignment = false;
            this.cellSubtotalLabel.Text = "Total ruta";
            this.cellSubtotalLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSubtotalLabel.Weight = 8.6D;
            // 
            // cellSubtotalConsumo
            // 
            this.cellSubtotalConsumo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([consumo])")});
            this.cellSubtotalConsumo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellSubtotalConsumo.Name = "cellSubtotalConsumo";
            this.cellSubtotalConsumo.StylePriority.UseFont = false;
            this.cellSubtotalConsumo.StylePriority.UseTextAlignment = false;
            this.cellSubtotalConsumo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSubtotalConsumo.TextFormatString = "{0:n2}";
            this.cellSubtotalConsumo.Weight = 1.35D;
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellSubtotalConsumo.Summary = xrSummary1;
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
            this.tblTotal.LocationFloat = new DevExpress.Utils.PointFloat(18F, 4F);
            this.tblTotal.Name = "tblTotal";
            this.tblTotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblTotalRow});
            this.tblTotal.SizeF = new System.Drawing.SizeF(982F, 26F);
            this.tblTotal.StylePriority.UseBorders = false;
            this.tblTotal.StylePriority.UseTextAlignment = false;
            this.tblTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblTotalRow
            // 
            this.tblTotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellTotalLabel,
            this.cellTotalConsumo});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalLabel
            // 
            this.cellTotalLabel.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalLabel.Name = "cellTotalLabel";
            this.cellTotalLabel.StylePriority.UseFont = false;
            this.cellTotalLabel.StylePriority.UseTextAlignment = false;
            this.cellTotalLabel.Text = "Total general";
            this.cellTotalLabel.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalLabel.Weight = 8.6D;
            // 
            // cellTotalConsumo
            // 
            this.cellTotalConsumo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([consumo])")});
            this.cellTotalConsumo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellTotalConsumo.Name = "cellTotalConsumo";
            this.cellTotalConsumo.StylePriority.UseFont = false;
            this.cellTotalConsumo.StylePriority.UseTextAlignment = false;
            this.cellTotalConsumo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalConsumo.TextFormatString = "{0:n2}";
            this.cellTotalConsumo.Weight = 1.35D;
            xrSummary2.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalConsumo.Summary = xrSummary2;
            // 
            // sqlDataSource1
            // 
            this.sqlDataSource1.ConnectionName = "DefaultConnection";
            this.sqlDataSource1.Name = "sqlDataSource1";
            queryParameter1.Name = "CompaniaID";
            queryParameter1.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter1.Value = new DevExpress.DataAccess.Expression("?p_Compania_ID", typeof(int));
            queryParameter2.Name = "Anio";
            queryParameter2.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter2.Value = new DevExpress.DataAccess.Expression("?p_Anio", typeof(int));
            queryParameter3.Name = "Mes";
            queryParameter3.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Mes", typeof(int));
            queryParameter4.Name = "CicloID";
            queryParameter4.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Ciclo_ID", typeof(int));
            queryParameter5.Name = "SoloPendientes";
            queryParameter5.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter5.Value = new DevExpress.DataAccess.Expression("?p_Solo_Pendientes", typeof(bool));
            customSqlQuery1.Name = "Query";
            customSqlQuery1.Parameters.Add(queryParameter1);
            customSqlQuery1.Parameters.Add(queryParameter2);
            customSqlQuery1.Parameters.Add(queryParameter3);
            customSqlQuery1.Parameters.Add(queryParameter4);
            customSqlQuery1.Parameters.Add(queryParameter5);
            customSqlQuery1.Sql = resources.GetString("customSqlQuery1.Sql");
            this.sqlDataSource1.Queries.AddRange(new DevExpress.DataAccess.Sql.SqlQuery[] {
            customSqlQuery1});
            this.sqlDataSource1.ResultSchemaSerializable = resources.GetString("sqlDataSource1.ResultSchemaSerializable");
            // 
            // Title
            // 
            this.Title.Font = new DevExpress.Drawing.DXFont("Times New Roman", 18F, DevExpress.Drawing.DXFontStyle.Bold);
            this.Title.ForeColor = System.Drawing.Color.Black;
            this.Title.Name = "Title";
            this.Title.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // HeaderCaption
            // 
            this.HeaderCaption.BackColor = System.Drawing.Color.Transparent;
            this.HeaderCaption.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.HeaderCaption.BorderWidth = 1F;
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
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
            // PageInfoStyle
            // 
            this.PageInfoStyle.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.PageInfoStyle.ForeColor = System.Drawing.Color.Black;
            this.PageInfoStyle.Name = "PageInfoStyle";
            this.PageInfoStyle.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            // 
            // TotalData
            // 
            this.TotalData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10.5F, DevExpress.Drawing.DXFontStyle.Bold);
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
            // p_Anio
            // 
            this.p_Anio.Name = "p_Anio";
            this.p_Anio.Type = typeof(int);
            this.p_Anio.ValueInfo = "0";
            // 
            // p_Mes
            // 
            this.p_Mes.Name = "p_Mes";
            this.p_Mes.Type = typeof(int);
            this.p_Mes.ValueInfo = "0";
            // 
            // p_Ciclo_ID
            // 
            this.p_Ciclo_ID.Name = "p_Ciclo_ID";
            this.p_Ciclo_ID.Type = typeof(int);
            this.p_Ciclo_ID.ValueInfo = "0";
            // 
            // p_Solo_Pendientes
            // 
            this.p_Solo_Pendientes.Name = "p_Solo_Pendientes";
            this.p_Solo_Pendientes.Type = typeof(bool);
            this.p_Solo_Pendientes.ValueInfo = "False";
            // 
            // Rpt_Dev_Auxiliar_Lectura
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.ReportHeader,
            this.PageHeader,
            this.GroupHeaderRuta,
            this.Detail,
            this.GroupFooterRuta,
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
            this.p_Anio,
            this.p_Mes,
            this.p_Ciclo_ID,
            this.p_Solo_Pendientes});
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.Title,
            this.HeaderCaption,
            this.DetailData,
            this.PageInfoStyle,
            this.TotalData});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblSubtotal)).EndInit();
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
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSecuencia;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderClave;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderContador;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderLecturaAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderLecturaActual;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderConsumo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderUsuario;
        private DevExpress.XtraReports.UI.GroupHeaderBand GroupHeaderRuta;
        private DevExpress.XtraReports.UI.XRLabel lblRuta;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellSecuencia;
        private DevExpress.XtraReports.UI.XRTableCell cellClave;
        private DevExpress.XtraReports.UI.XRTableCell cellCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellContador;
        private DevExpress.XtraReports.UI.XRTableCell cellLecturaAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellLecturaActual;
        private DevExpress.XtraReports.UI.XRTableCell cellConsumo;
        private DevExpress.XtraReports.UI.XRTableCell cellFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellUsuario;
        private DevExpress.XtraReports.UI.GroupFooterBand GroupFooterRuta;
        private DevExpress.XtraReports.UI.XRTable tblSubtotal;
        private DevExpress.XtraReports.UI.XRTableRow tblSubtotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalLabel;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalConsumo;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalLabel;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalConsumo;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Anio;
        private DevExpress.XtraReports.Parameters.Parameter p_Mes;
        private DevExpress.XtraReports.Parameters.Parameter p_Ciclo_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Solo_Pendientes;
    }
}
