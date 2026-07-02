namespace SIAD.Reports
{
    partial class Rpt_Dev_Historial_Recibos_Emitidos
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Historial_Recibos_Emitidos));
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
            this.cellHeaderRecibo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTipo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderContribuyente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderValor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderNulo = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellRecibo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTipo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFecha = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellContribuyente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellValor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellNulo = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.footerLineLeft = new DevExpress.XtraReports.UI.XRLine();
            this.footerLineRight = new DevExpress.XtraReports.UI.XRLine();
            this.lblTotalEmitidos = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalEmitidosCantidad = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalEmitidosValor = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalNulos = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalNulosCantidad = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalNulosValor = new DevExpress.XtraReports.UI.XRLabel();
            this.lblServiciosPublicos = new DevExpress.XtraReports.UI.XRLabel();
            this.lblServiciosPublicosValor = new DevExpress.XtraReports.UI.XRLabel();
            this.lblMiscelaneos = new DevExpress.XtraReports.UI.XRLabel();
            this.lblMiscelaneosValor = new DevExpress.XtraReports.UI.XRLabel();
            this.footerLineTotal = new DevExpress.XtraReports.UI.XRLine();
            this.lblTotalGeneral = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTotalGeneralValor = new DevExpress.XtraReports.UI.XRLabel();
            this.lblJefeAreaComercial = new DevExpress.XtraReports.UI.XRLabel();
            this.lblFirmaAreaComercial = new DevExpress.XtraReports.UI.XRLabel();
            this.lblAceptadoPor = new DevExpress.XtraReports.UI.XRLabel();
            this.lblFirmaRecibido = new DevExpress.XtraReports.UI.XRLabel();
            this.lineJefeAreaComercial = new DevExpress.XtraReports.UI.XRLine();
            this.lineFirmaAreaComercial = new DevExpress.XtraReports.UI.XRLine();
            this.lineAceptadoPor = new DevExpress.XtraReports.UI.XRLine();
            this.lineFirmaRecibido = new DevExpress.XtraReports.UI.XRLine();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Desde = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Hasta = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Usuario = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
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
            this.PageHeader.HeightF = 28F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(8F, 2F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(722F, 24F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderRecibo,
            this.cellHeaderTipo,
            this.cellHeaderFecha,
            this.cellHeaderCliente,
            this.cellHeaderContribuyente,
            this.cellHeaderValor,
            this.cellHeaderNulo});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderRecibo
            // 
            this.cellHeaderRecibo.Name = "cellHeaderRecibo";
            this.cellHeaderRecibo.Text = "RECIBO";
            this.cellHeaderRecibo.Weight = 1.1D;
            // 
            // cellHeaderTipo
            // 
            this.cellHeaderTipo.Name = "cellHeaderTipo";
            this.cellHeaderTipo.Text = "TIPO";
            this.cellHeaderTipo.Weight = 0.55D;
            // 
            // cellHeaderFecha
            // 
            this.cellHeaderFecha.Name = "cellHeaderFecha";
            this.cellHeaderFecha.Text = "FECHA";
            this.cellHeaderFecha.Weight = 0.95D;
            // 
            // cellHeaderCliente
            // 
            this.cellHeaderCliente.Name = "cellHeaderCliente";
            this.cellHeaderCliente.Text = "CLIENTE";
            this.cellHeaderCliente.Weight = 1.35D;
            // 
            // cellHeaderContribuyente
            // 
            this.cellHeaderContribuyente.Name = "cellHeaderContribuyente";
            this.cellHeaderContribuyente.Text = "CONTRIBUYENTE";
            this.cellHeaderContribuyente.Weight = 3.35D;
            // 
            // cellHeaderValor
            // 
            this.cellHeaderValor.Name = "cellHeaderValor";
            this.cellHeaderValor.StylePriority.UseTextAlignment = false;
            this.cellHeaderValor.Text = "VALOR L.";
            this.cellHeaderValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellHeaderValor.Weight = 1.25D;
            // 
            // cellHeaderNulo
            // 
            this.cellHeaderNulo.Name = "cellHeaderNulo";
            this.cellHeaderNulo.StylePriority.UseTextAlignment = false;
            this.cellHeaderNulo.Text = "Nulo";
            this.cellHeaderNulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellHeaderNulo.Weight = 0.85D;
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
            this.cellRecibo,
            this.cellTipo,
            this.cellFecha,
            this.cellCliente,
            this.cellContribuyente,
            this.cellValor,
            this.cellNulo});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellRecibo
            // 
            this.cellRecibo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[recibo]")});
            this.cellRecibo.Name = "cellRecibo";
            this.cellRecibo.StyleName = "DetailData";
            this.cellRecibo.Weight = 1.1D;
            // 
            // cellTipo
            // 
            this.cellTipo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[tipo]")});
            this.cellTipo.Name = "cellTipo";
            this.cellTipo.StyleName = "DetailData";
            this.cellTipo.StylePriority.UseTextAlignment = false;
            this.cellTipo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellTipo.Weight = 0.55D;
            // 
            // cellFecha
            // 
            this.cellFecha.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha]")});
            this.cellFecha.Name = "cellFecha";
            this.cellFecha.StyleName = "DetailData";
            this.cellFecha.StylePriority.UseTextAlignment = false;
            this.cellFecha.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellFecha.TextFormatString = "{0:dd/MM/yy}";
            this.cellFecha.Weight = 0.95D;
            // 
            // cellCliente
            // 
            this.cellCliente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cliente_codigo]")});
            this.cellCliente.Name = "cellCliente";
            this.cellCliente.StyleName = "DetailData";
            this.cellCliente.Weight = 1.35D;
            // 
            // cellContribuyente
            // 
            this.cellContribuyente.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[contribuyente]")});
            this.cellContribuyente.Name = "cellContribuyente";
            this.cellContribuyente.StyleName = "DetailData";
            this.cellContribuyente.Weight = 3.35D;
            // 
            // cellValor
            // 
            this.cellValor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[valor_lempiras]")});
            this.cellValor.Name = "cellValor";
            this.cellValor.StyleName = "DetailData";
            this.cellValor.StylePriority.UseTextAlignment = false;
            this.cellValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellValor.TextFormatString = "{0:n2}";
            this.cellValor.Weight = 1.25D;
            // 
            // cellNulo
            // 
            this.cellNulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[nulo]")});
            this.cellNulo.Name = "cellNulo";
            this.cellNulo.StyleName = "DetailData";
            this.cellNulo.StylePriority.UseTextAlignment = false;
            this.cellNulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellNulo.Weight = 0.85D;
            // 
            // ReportFooter
            // 
            this.ReportFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.footerLineLeft,
            this.footerLineRight,
            this.lblTotalEmitidos,
            this.lblTotalEmitidosCantidad,
            this.lblTotalEmitidosValor,
            this.lblTotalNulos,
            this.lblTotalNulosCantidad,
            this.lblTotalNulosValor,
            this.lblServiciosPublicos,
            this.lblServiciosPublicosValor,
            this.lblMiscelaneos,
            this.lblMiscelaneosValor,
            this.footerLineTotal,
            this.lblTotalGeneral,
            this.lblTotalGeneralValor,
            this.lblJefeAreaComercial,
            this.lblFirmaAreaComercial,
            this.lblAceptadoPor,
            this.lblFirmaRecibido,
            this.lineJefeAreaComercial,
            this.lineFirmaAreaComercial,
            this.lineAceptadoPor,
            this.lineFirmaRecibido});
            this.ReportFooter.HeightF = 150F;
            this.ReportFooter.Name = "ReportFooter";
            // 
            // footerLineLeft
            // 
            this.footerLineLeft.LocationFloat = new DevExpress.Utils.PointFloat(8F, 4F);
            this.footerLineLeft.Name = "footerLineLeft";
            this.footerLineLeft.SizeF = new System.Drawing.SizeF(420F, 6F);
            // 
            // footerLineRight
            // 
            this.footerLineRight.LocationFloat = new DevExpress.Utils.PointFloat(485F, 4F);
            this.footerLineRight.Name = "footerLineRight";
            this.footerLineRight.SizeF = new System.Drawing.SizeF(245F, 6F);
            // 
            // lblTotalEmitidos
            // 
            this.lblTotalEmitidos.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblTotalEmitidos.LocationFloat = new DevExpress.Utils.PointFloat(8F, 12F);
            this.lblTotalEmitidos.Name = "lblTotalEmitidos";
            this.lblTotalEmitidos.SizeF = new System.Drawing.SizeF(168F, 18F);
            this.lblTotalEmitidos.StylePriority.UseFont = false;
            this.lblTotalEmitidos.Text = "Total Recibos Emitidos";
            // 
            // lblTotalEmitidosCantidad
            // 
            this.lblTotalEmitidosCantidad.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_emitidos_cantidad]")});
            this.lblTotalEmitidosCantidad.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblTotalEmitidosCantidad.LocationFloat = new DevExpress.Utils.PointFloat(182F, 12F);
            this.lblTotalEmitidosCantidad.Name = "lblTotalEmitidosCantidad";
            this.lblTotalEmitidosCantidad.SizeF = new System.Drawing.SizeF(70F, 18F);
            this.lblTotalEmitidosCantidad.StylePriority.UseFont = false;
            this.lblTotalEmitidosCantidad.StylePriority.UseTextAlignment = false;
            this.lblTotalEmitidosCantidad.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // lblTotalEmitidosValor
            // 
            this.lblTotalEmitidosValor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_emitidos_valor]")});
            this.lblTotalEmitidosValor.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblTotalEmitidosValor.LocationFloat = new DevExpress.Utils.PointFloat(258F, 12F);
            this.lblTotalEmitidosValor.Name = "lblTotalEmitidosValor";
            this.lblTotalEmitidosValor.SizeF = new System.Drawing.SizeF(110F, 18F);
            this.lblTotalEmitidosValor.StylePriority.UseFont = false;
            this.lblTotalEmitidosValor.StylePriority.UseTextAlignment = false;
            this.lblTotalEmitidosValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblTotalEmitidosValor.TextFormatString = "{0:n2}";
            // 
            // lblTotalNulos
            // 
            this.lblTotalNulos.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblTotalNulos.LocationFloat = new DevExpress.Utils.PointFloat(8F, 34F);
            this.lblTotalNulos.Name = "lblTotalNulos";
            this.lblTotalNulos.SizeF = new System.Drawing.SizeF(168F, 18F);
            this.lblTotalNulos.StylePriority.UseFont = false;
            this.lblTotalNulos.Text = "Total Recibos Nulos";
            // 
            // lblTotalNulosCantidad
            // 
            this.lblTotalNulosCantidad.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_nulos_cantidad]")});
            this.lblTotalNulosCantidad.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblTotalNulosCantidad.LocationFloat = new DevExpress.Utils.PointFloat(182F, 34F);
            this.lblTotalNulosCantidad.Name = "lblTotalNulosCantidad";
            this.lblTotalNulosCantidad.SizeF = new System.Drawing.SizeF(70F, 18F);
            this.lblTotalNulosCantidad.StylePriority.UseFont = false;
            this.lblTotalNulosCantidad.StylePriority.UseTextAlignment = false;
            this.lblTotalNulosCantidad.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // lblTotalNulosValor
            // 
            this.lblTotalNulosValor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_nulos_valor]")});
            this.lblTotalNulosValor.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblTotalNulosValor.LocationFloat = new DevExpress.Utils.PointFloat(258F, 34F);
            this.lblTotalNulosValor.Name = "lblTotalNulosValor";
            this.lblTotalNulosValor.SizeF = new System.Drawing.SizeF(110F, 18F);
            this.lblTotalNulosValor.StylePriority.UseFont = false;
            this.lblTotalNulosValor.StylePriority.UseTextAlignment = false;
            this.lblTotalNulosValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblTotalNulosValor.TextFormatString = "{0:n2}";
            // 
            // lblServiciosPublicos
            // 
            this.lblServiciosPublicos.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblServiciosPublicos.LocationFloat = new DevExpress.Utils.PointFloat(485F, 12F);
            this.lblServiciosPublicos.Name = "lblServiciosPublicos";
            this.lblServiciosPublicos.SizeF = new System.Drawing.SizeF(150F, 18F);
            this.lblServiciosPublicos.StylePriority.UseFont = false;
            this.lblServiciosPublicos.StylePriority.UseTextAlignment = false;
            this.lblServiciosPublicos.Text = "Servicios Publicos:";
            this.lblServiciosPublicos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // lblServiciosPublicosValor
            // 
            this.lblServiciosPublicosValor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_servicios_publicos]")});
            this.lblServiciosPublicosValor.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblServiciosPublicosValor.LocationFloat = new DevExpress.Utils.PointFloat(640F, 12F);
            this.lblServiciosPublicosValor.Name = "lblServiciosPublicosValor";
            this.lblServiciosPublicosValor.SizeF = new System.Drawing.SizeF(90F, 18F);
            this.lblServiciosPublicosValor.StylePriority.UseFont = false;
            this.lblServiciosPublicosValor.StylePriority.UseTextAlignment = false;
            this.lblServiciosPublicosValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblServiciosPublicosValor.TextFormatString = "{0:n2}";
            // 
            // lblMiscelaneos
            // 
            this.lblMiscelaneos.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblMiscelaneos.LocationFloat = new DevExpress.Utils.PointFloat(485F, 32F);
            this.lblMiscelaneos.Name = "lblMiscelaneos";
            this.lblMiscelaneos.SizeF = new System.Drawing.SizeF(150F, 18F);
            this.lblMiscelaneos.StylePriority.UseFont = false;
            this.lblMiscelaneos.StylePriority.UseTextAlignment = false;
            this.lblMiscelaneos.Text = "Miscelaneos:";
            this.lblMiscelaneos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            // 
            // lblMiscelaneosValor
            // 
            this.lblMiscelaneosValor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_miscelaneos]")});
            this.lblMiscelaneosValor.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblMiscelaneosValor.LocationFloat = new DevExpress.Utils.PointFloat(640F, 32F);
            this.lblMiscelaneosValor.Name = "lblMiscelaneosValor";
            this.lblMiscelaneosValor.SizeF = new System.Drawing.SizeF(90F, 18F);
            this.lblMiscelaneosValor.StylePriority.UseFont = false;
            this.lblMiscelaneosValor.StylePriority.UseTextAlignment = false;
            this.lblMiscelaneosValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblMiscelaneosValor.TextFormatString = "{0:n2}";
            // 
            // footerLineTotal
            // 
            this.footerLineTotal.LocationFloat = new DevExpress.Utils.PointFloat(575F, 52F);
            this.footerLineTotal.Name = "footerLineTotal";
            this.footerLineTotal.SizeF = new System.Drawing.SizeF(155F, 6F);
            // 
            // lblTotalGeneral
            // 
            this.lblTotalGeneral.Font = new DevExpress.Drawing.DXFont("Courier New", 10F, DevExpress.Drawing.DXFontStyle.Bold | DevExpress.Drawing.DXFontStyle.Italic);
            this.lblTotalGeneral.LocationFloat = new DevExpress.Utils.PointFloat(575F, 58F);
            this.lblTotalGeneral.Name = "lblTotalGeneral";
            this.lblTotalGeneral.SizeF = new System.Drawing.SizeF(70F, 18F);
            this.lblTotalGeneral.StylePriority.UseFont = false;
            this.lblTotalGeneral.StylePriority.UseTextAlignment = false;
            this.lblTotalGeneral.Text = "TOTAL";
            this.lblTotalGeneral.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            // 
            // lblTotalGeneralValor
            // 
            this.lblTotalGeneralValor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_general]")});
            this.lblTotalGeneralValor.Font = new DevExpress.Drawing.DXFont("Courier New", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTotalGeneralValor.LocationFloat = new DevExpress.Utils.PointFloat(640F, 58F);
            this.lblTotalGeneralValor.Name = "lblTotalGeneralValor";
            this.lblTotalGeneralValor.SizeF = new System.Drawing.SizeF(90F, 18F);
            this.lblTotalGeneralValor.StylePriority.UseFont = false;
            this.lblTotalGeneralValor.StylePriority.UseTextAlignment = false;
            this.lblTotalGeneralValor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.lblTotalGeneralValor.TextFormatString = "{0:n2}";
            // 
            // lblJefeAreaComercial
            // 
            this.lblJefeAreaComercial.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblJefeAreaComercial.LocationFloat = new DevExpress.Utils.PointFloat(8F, 92F);
            this.lblJefeAreaComercial.Name = "lblJefeAreaComercial";
            this.lblJefeAreaComercial.SizeF = new System.Drawing.SizeF(150F, 18F);
            this.lblJefeAreaComercial.StylePriority.UseFont = false;
            this.lblJefeAreaComercial.Text = "Jefe Area Comercial :";
            // 
            // lblFirmaAreaComercial
            // 
            this.lblFirmaAreaComercial.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblFirmaAreaComercial.LocationFloat = new DevExpress.Utils.PointFloat(430F, 92F);
            this.lblFirmaAreaComercial.Name = "lblFirmaAreaComercial";
            this.lblFirmaAreaComercial.SizeF = new System.Drawing.SizeF(150F, 18F);
            this.lblFirmaAreaComercial.StylePriority.UseFont = false;
            this.lblFirmaAreaComercial.Text = "Firma Area Comercial :";
            // 
            // lblAceptadoPor
            // 
            this.lblAceptadoPor.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblAceptadoPor.LocationFloat = new DevExpress.Utils.PointFloat(8F, 124F);
            this.lblAceptadoPor.Name = "lblAceptadoPor";
            this.lblAceptadoPor.SizeF = new System.Drawing.SizeF(100F, 18F);
            this.lblAceptadoPor.StylePriority.UseFont = false;
            this.lblAceptadoPor.Text = "Aceptado Por :";
            // 
            // lblFirmaRecibido
            // 
            this.lblFirmaRecibido.Font = new DevExpress.Drawing.DXFont("Courier New", 9F);
            this.lblFirmaRecibido.LocationFloat = new DevExpress.Utils.PointFloat(486F, 124F);
            this.lblFirmaRecibido.Name = "lblFirmaRecibido";
            this.lblFirmaRecibido.SizeF = new System.Drawing.SizeF(110F, 18F);
            this.lblFirmaRecibido.StylePriority.UseFont = false;
            this.lblFirmaRecibido.Text = "Firma de Recibido :";
            // 
            // lineJefeAreaComercial
            // 
            this.lineJefeAreaComercial.LocationFloat = new DevExpress.Utils.PointFloat(160F, 107F);
            this.lineJefeAreaComercial.Name = "lineJefeAreaComercial";
            this.lineJefeAreaComercial.SizeF = new System.Drawing.SizeF(180F, 6F);
            // 
            // lineFirmaAreaComercial
            // 
            this.lineFirmaAreaComercial.LocationFloat = new DevExpress.Utils.PointFloat(582F, 107F);
            this.lineFirmaAreaComercial.Name = "lineFirmaAreaComercial";
            this.lineFirmaAreaComercial.SizeF = new System.Drawing.SizeF(148F, 6F);
            // 
            // lineAceptadoPor
            // 
            this.lineAceptadoPor.LocationFloat = new DevExpress.Utils.PointFloat(110F, 139F);
            this.lineAceptadoPor.Name = "lineAceptadoPor";
            this.lineAceptadoPor.SizeF = new System.Drawing.SizeF(180F, 6F);
            // 
            // lineFirmaRecibido
            // 
            this.lineFirmaRecibido.LocationFloat = new DevExpress.Utils.PointFloat(598F, 139F);
            this.lineFirmaRecibido.Name = "lineFirmaRecibido";
            this.lineFirmaRecibido.SizeF = new System.Drawing.SizeF(132F, 6F);
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
            queryParameter4.Name = "Usuario";
            queryParameter4.Type = typeof(DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Usuario", typeof(string));
            customSqlQuery1.Name = "Query";
            customSqlQuery1.Parameters.Add(queryParameter1);
            customSqlQuery1.Parameters.Add(queryParameter2);
            customSqlQuery1.Parameters.Add(queryParameter3);
            customSqlQuery1.Parameters.Add(queryParameter4);
            customSqlQuery1.Sql = resources.GetString("customSqlQuery1.Sql");
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
            // p_Usuario
            // 
            this.p_Usuario.AllowNull = true;
            this.p_Usuario.Name = "p_Usuario";
            // 
            // Rpt_Dev_Historial_Recibos_Emitidos
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
            this.Font = new DevExpress.Drawing.DXFont("Times New Roman", 9F);
            this.Margins = new DevExpress.Drawing.DXMargins(40, 40, 18, 18);
            this.PageHeight = 1100;
            this.PageWidth = 850;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Desde,
            this.p_Fecha_Hasta,
            this.p_Usuario});
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.Title,
            this.HeaderCaption,
            this.DetailData,
            this.TotalData});
            this.Version = "25.2";
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).EndInit();
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
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRecibo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTipo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderContribuyente;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderValor;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderNulo;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellRecibo;
        private DevExpress.XtraReports.UI.XRTableCell cellTipo;
        private DevExpress.XtraReports.UI.XRTableCell cellFecha;
        private DevExpress.XtraReports.UI.XRTableCell cellCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellContribuyente;
        private DevExpress.XtraReports.UI.XRTableCell cellValor;
        private DevExpress.XtraReports.UI.XRTableCell cellNulo;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRLine footerLineLeft;
        private DevExpress.XtraReports.UI.XRLine footerLineRight;
        private DevExpress.XtraReports.UI.XRLabel lblTotalEmitidos;
        private DevExpress.XtraReports.UI.XRLabel lblTotalEmitidosCantidad;
        private DevExpress.XtraReports.UI.XRLabel lblTotalEmitidosValor;
        private DevExpress.XtraReports.UI.XRLabel lblTotalNulos;
        private DevExpress.XtraReports.UI.XRLabel lblTotalNulosCantidad;
        private DevExpress.XtraReports.UI.XRLabel lblTotalNulosValor;
        private DevExpress.XtraReports.UI.XRLabel lblServiciosPublicos;
        private DevExpress.XtraReports.UI.XRLabel lblServiciosPublicosValor;
        private DevExpress.XtraReports.UI.XRLabel lblMiscelaneos;
        private DevExpress.XtraReports.UI.XRLabel lblMiscelaneosValor;
        private DevExpress.XtraReports.UI.XRLine footerLineTotal;
        private DevExpress.XtraReports.UI.XRLabel lblTotalGeneral;
        private DevExpress.XtraReports.UI.XRLabel lblTotalGeneralValor;
        private DevExpress.XtraReports.UI.XRLabel lblJefeAreaComercial;
        private DevExpress.XtraReports.UI.XRLabel lblFirmaAreaComercial;
        private DevExpress.XtraReports.UI.XRLabel lblAceptadoPor;
        private DevExpress.XtraReports.UI.XRLabel lblFirmaRecibido;
        private DevExpress.XtraReports.UI.XRLine lineJefeAreaComercial;
        private DevExpress.XtraReports.UI.XRLine lineFirmaAreaComercial;
        private DevExpress.XtraReports.UI.XRLine lineAceptadoPor;
        private DevExpress.XtraReports.UI.XRLine lineFirmaRecibido;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Desde;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Hasta;
        private DevExpress.XtraReports.Parameters.Parameter p_Usuario;
    }
}
