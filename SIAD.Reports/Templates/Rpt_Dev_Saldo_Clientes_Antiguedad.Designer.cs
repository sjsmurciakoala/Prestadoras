namespace SIAD.Reports
{
    partial class Rpt_Dev_Saldo_Clientes_Antiguedad
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Saldo_Clientes_Antiguedad));
            DevExpress.XtraReports.UI.XRSummary xrSummary1 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary2 = new DevExpress.XtraReports.UI.XRSummary();
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
            this.cellHeaderTelefono = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.GroupHeaderCiclo = new DevExpress.XtraReports.UI.GroupHeaderBand();
            this.lblCiclo = new DevExpress.XtraReports.UI.XRLabel();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCuenta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDireccion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTelefono = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.GroupFooterCiclo = new DevExpress.XtraReports.UI.GroupFooterBand();
            this.tblSubtotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblSubtotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellSubtotalCuenta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSubtotalCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSubtotalDireccion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSubtotalTelefono = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSubtotalSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalCuenta = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCliente = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDireccion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalTelefono = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Corte = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Dias_Minimos = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Estado_Cliente = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Ciclo_ID = new DevExpress.XtraReports.Parameters.Parameter();
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
            this.topLine.LocationFloat = new DevExpress.Utils.PointFloat(18F, 34F);
            this.topLine.Name = "topLine";
            this.topLine.SizeF = new System.Drawing.SizeF(982F, 8F);
            this.topLine.LineWidth = 3F;
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
            this.PageHeader.HeightF = 40F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
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
            this.cellHeaderCuenta,
            this.cellHeaderCliente,
            this.cellHeaderDireccion,
            this.cellHeaderTelefono,
            this.cellHeaderSaldo});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderCuenta
            // 
            this.cellHeaderCuenta.Name = "cellHeaderCuenta";
            this.cellHeaderCuenta.Text = "Cuenta";
            this.cellHeaderCuenta.Weight = 1.1D;
            // 
            // cellHeaderCliente
            // 
            this.cellHeaderCliente.Name = "cellHeaderCliente";
            this.cellHeaderCliente.Text = "Nombre del Cliente";
            this.cellHeaderCliente.Weight = 3.4D;
            // 
            // cellHeaderDireccion
            // 
            this.cellHeaderDireccion.Name = "cellHeaderDireccion";
            this.cellHeaderDireccion.Text = "Direccion";
            this.cellHeaderDireccion.Weight = 4D;
            // 
            // cellHeaderTelefono
            // 
            this.cellHeaderTelefono.Name = "cellHeaderTelefono";
            this.cellHeaderTelefono.Text = "Telefono";
            this.cellHeaderTelefono.Weight = 1.7D;
            // 
            // cellHeaderSaldo
            // 
            this.cellHeaderSaldo.Name = "cellHeaderSaldo";
            this.cellHeaderSaldo.Text = "Saldo";
            this.cellHeaderSaldo.Weight = 1.3D;
            // 
            // GroupHeaderCiclo
            // 
            this.GroupHeaderCiclo.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblCiclo});
            this.GroupHeaderCiclo.GroupFields.AddRange(new DevExpress.XtraReports.UI.GroupField[] {
            new DevExpress.XtraReports.UI.GroupField("ciclo_orden", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending),
            new DevExpress.XtraReports.UI.GroupField("ciclo_codigo", DevExpress.XtraReports.UI.XRColumnSortOrder.Ascending)});
            this.GroupHeaderCiclo.HeightF = 30F;
            this.GroupHeaderCiclo.Name = "GroupHeaderCiclo";
            this.GroupHeaderCiclo.RepeatEveryPage = true;
            // 
            // lblCiclo
            // 
            this.lblCiclo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ciclo_titulo]")});
            this.lblCiclo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblCiclo.LocationFloat = new DevExpress.Utils.PointFloat(20F, 4F);
            this.lblCiclo.Name = "lblCiclo";
            this.lblCiclo.SizeF = new System.Drawing.SizeF(300F, 24F);
            this.lblCiclo.StylePriority.UseFont = false;
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
            this.cellTelefono,
            this.cellSaldo});
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
            this.cellCuenta.Weight = 1.1D;
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
            this.cellCliente.Weight = 3.4D;
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
            this.cellDireccion.Weight = 4D;
            // 
            // cellTelefono
            // 
            this.cellTelefono.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[telefono]")});
            this.cellTelefono.Name = "cellTelefono";
            this.cellTelefono.StyleName = "DetailData";
            this.cellTelefono.StylePriority.UseTextAlignment = false;
            this.cellTelefono.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellTelefono.Weight = 1.7D;
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
            this.cellSaldo.Weight = 1.3D;
            // 
            // GroupFooterCiclo
            // 
            this.GroupFooterCiclo.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblSubtotal});
            this.GroupFooterCiclo.HeightF = 28F;
            this.GroupFooterCiclo.Name = "GroupFooterCiclo";
            // 
            // tblSubtotal
            // 
            this.tblSubtotal.Borders = DevExpress.XtraPrinting.BorderSide.Top;
            this.tblSubtotal.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblSubtotal.LocationFloat = new DevExpress.Utils.PointFloat(18F, 2F);
            this.tblSubtotal.Name = "tblSubtotal";
            this.tblSubtotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblSubtotalRow});
            this.tblSubtotal.SizeF = new System.Drawing.SizeF(982F, 24F);
            this.tblSubtotal.StyleName = "TotalData";
            this.tblSubtotal.StylePriority.UseBorders = false;
            this.tblSubtotal.StylePriority.UseFont = false;
            this.tblSubtotal.StylePriority.UseTextAlignment = false;
            this.tblSubtotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblSubtotalRow
            // 
            this.tblSubtotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellSubtotalCuenta,
            this.cellSubtotalCliente,
            this.cellSubtotalDireccion,
            this.cellSubtotalTelefono,
            this.cellSubtotalSaldo});
            this.tblSubtotalRow.Name = "tblSubtotalRow";
            this.tblSubtotalRow.Weight = 1D;
            // 
            // cellSubtotalCuenta
            // 
            this.cellSubtotalCuenta.Name = "cellSubtotalCuenta";
            this.cellSubtotalCuenta.Weight = 1.1D;
            // 
            // cellSubtotalCliente
            // 
            this.cellSubtotalCliente.Name = "cellSubtotalCliente";
            this.cellSubtotalCliente.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 4, 0, 0, 100F);
            this.cellSubtotalCliente.StylePriority.UsePadding = false;
            this.cellSubtotalCliente.StylePriority.UseTextAlignment = false;
            this.cellSubtotalCliente.Text = "Subtotal ciclo:";
            this.cellSubtotalCliente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSubtotalCliente.Weight = 3.4D;
            // 
            // cellSubtotalDireccion
            // 
            this.cellSubtotalDireccion.Name = "cellSubtotalDireccion";
            this.cellSubtotalDireccion.Weight = 4D;
            // 
            // cellSubtotalTelefono
            // 
            this.cellSubtotalTelefono.Name = "cellSubtotalTelefono";
            this.cellSubtotalTelefono.Weight = 1.7D;
            // 
            // cellSubtotalSaldo
            // 
            this.cellSubtotalSaldo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo])")});
            this.cellSubtotalSaldo.Name = "cellSubtotalSaldo";
            this.cellSubtotalSaldo.StylePriority.UseTextAlignment = false;
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Group;
            this.cellSubtotalSaldo.Summary = xrSummary1;
            this.cellSubtotalSaldo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSubtotalSaldo.TextFormatString = "{0:n2}";
            this.cellSubtotalSaldo.Weight = 1.3D;
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
            this.cellTotalTelefono,
            this.cellTotalSaldo});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalCuenta
            // 
            this.cellTotalCuenta.Name = "cellTotalCuenta";
            this.cellTotalCuenta.Weight = 1.1D;
            // 
            // cellTotalCliente
            // 
            this.cellTotalCliente.Name = "cellTotalCliente";
            this.cellTotalCliente.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 4, 0, 0, 100F);
            this.cellTotalCliente.StylePriority.UsePadding = false;
            this.cellTotalCliente.StylePriority.UseTextAlignment = false;
            this.cellTotalCliente.Text = "Total general:";
            this.cellTotalCliente.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCliente.Weight = 3.4D;
            // 
            // cellTotalDireccion
            // 
            this.cellTotalDireccion.Name = "cellTotalDireccion";
            this.cellTotalDireccion.Weight = 4D;
            // 
            // cellTotalTelefono
            // 
            this.cellTotalTelefono.Name = "cellTotalTelefono";
            this.cellTotalTelefono.Weight = 1.7D;
            // 
            // cellTotalSaldo
            // 
            this.cellTotalSaldo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo])")});
            this.cellTotalSaldo.Name = "cellTotalSaldo";
            this.cellTotalSaldo.StylePriority.UseTextAlignment = false;
            xrSummary2.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldo.Summary = xrSummary2;
            this.cellTotalSaldo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldo.TextFormatString = "{0:n2}";
            this.cellTotalSaldo.Weight = 1.3D;
            // 
            // sqlDataSource1
            // 
            this.sqlDataSource1.ConnectionName = "DefaultConnection";
            this.sqlDataSource1.Name = "sqlDataSource1";
            customSqlQuery1.Name = "Query";
            queryParameter1.Name = "CompaniaID";
            queryParameter1.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter1.Value = new DevExpress.DataAccess.Expression("?p_Compania_ID", typeof(int));
            queryParameter2.Name = "FechaCorte";
            queryParameter2.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter2.Value = new DevExpress.DataAccess.Expression("?p_Fecha_Corte", typeof(System.DateOnly));
            queryParameter3.Name = "DiasMinimos";
            queryParameter3.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Dias_Minimos", typeof(int));
            queryParameter4.Name = "EstadoCliente";
            queryParameter4.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Estado_Cliente", typeof(int));
            queryParameter5.Name = "CicloID";
            queryParameter5.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter5.Value = new DevExpress.DataAccess.Expression("?p_Ciclo_ID", typeof(int));
            customSqlQuery1.Parameters.AddRange(new DevExpress.DataAccess.Sql.QueryParameter[] {
            queryParameter1,
            queryParameter2,
            queryParameter3,
            queryParameter4,
            queryParameter5});
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
            // p_Fecha_Corte
            // 
            this.p_Fecha_Corte.Name = "p_Fecha_Corte";
            this.p_Fecha_Corte.Type = typeof(global::System.DateOnly);
            // 
            // p_Dias_Minimos
            // 
            this.p_Dias_Minimos.Name = "p_Dias_Minimos";
            this.p_Dias_Minimos.Type = typeof(int);
            this.p_Dias_Minimos.ValueInfo = "60";
            // 
            // p_Estado_Cliente
            // 
            this.p_Estado_Cliente.Name = "p_Estado_Cliente";
            this.p_Estado_Cliente.Type = typeof(int);
            this.p_Estado_Cliente.ValueInfo = "0";
            // 
            // p_Ciclo_ID
            // 
            this.p_Ciclo_ID.Name = "p_Ciclo_ID";
            this.p_Ciclo_ID.Type = typeof(int);
            this.p_Ciclo_ID.ValueInfo = "0";
            // 
            // Rpt_Dev_Saldo_Clientes_Antiguedad
            // 
            this.Bands.AddRange(new DevExpress.XtraReports.UI.Band[] {
            this.TopMargin,
            this.BottomMargin,
            this.ReportHeader,
            this.PageHeader,
            this.GroupHeaderCiclo,
            this.Detail,
            this.GroupFooterCiclo,
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
            this.p_Fecha_Corte,
            this.p_Dias_Minimos,
            this.p_Estado_Cliente,
            this.p_Ciclo_ID});
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
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTelefono;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSaldo;
        private DevExpress.XtraReports.UI.GroupHeaderBand GroupHeaderCiclo;
        private DevExpress.XtraReports.UI.XRLabel lblCiclo;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCuenta;
        private DevExpress.XtraReports.UI.XRTableCell cellCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellDireccion;
        private DevExpress.XtraReports.UI.XRTableCell cellTelefono;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldo;
        private DevExpress.XtraReports.UI.GroupFooterBand GroupFooterCiclo;
        private DevExpress.XtraReports.UI.XRTable tblSubtotal;
        private DevExpress.XtraReports.UI.XRTableRow tblSubtotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalCuenta;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalDireccion;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalTelefono;
        private DevExpress.XtraReports.UI.XRTableCell cellSubtotalSaldo;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCuenta;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCliente;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDireccion;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalTelefono;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldo;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Corte;
        private DevExpress.XtraReports.Parameters.Parameter p_Dias_Minimos;
        private DevExpress.XtraReports.Parameters.Parameter p_Estado_Cliente;
        private DevExpress.XtraReports.Parameters.Parameter p_Ciclo_ID;
    }
}
