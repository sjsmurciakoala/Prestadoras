namespace SIAD.Reports
{
    partial class Rpt_Dev_Saldo_Clientes_Ciclo
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Saldo_Clientes_Ciclo));
            DevExpress.XtraReports.UI.XRSummary xrSummary1 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary2 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary3 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary4 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary5 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary6 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary7 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary8 = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummary9 = new DevExpress.XtraReports.UI.XRSummary();
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
            this.cellHeaderCiclo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTotalUsuarios = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderActivos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderInactivos = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCiclo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellUsuarios = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellActivos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellInactivos = new DevExpress.XtraReports.UI.XRTableCell();
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalCaption = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalAnterior = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalDebitos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCreditos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldoActual = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalUsuarios = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalActivos = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalInactivos = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.Title = new DevExpress.XtraReports.UI.XRControlStyle();
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.TotalData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Desde = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Hasta = new DevExpress.XtraReports.Parameters.Parameter();
            ((System.ComponentModel.ISupportInitialize)(this.tblHeader)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tblDetail)).BeginInit();
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
            this.pageInfoFecha.LocationFloat = new DevExpress.Utils.PointFloat(0F, 6F);
            this.pageInfoFecha.Name = "pageInfoFecha";
            this.pageInfoFecha.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            this.pageInfoFecha.SizeF = new System.Drawing.SizeF(300F, 18F);
            this.pageInfoFecha.StyleName = "PageInfoStyle";
            this.pageInfoFecha.TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}";
            // 
            // pageInfoPagina
            // 
            this.pageInfoPagina.LocationFloat = new DevExpress.Utils.PointFloat(1030F, 6F);
            this.pageInfoPagina.Name = "pageInfoPagina";
            this.pageInfoPagina.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal;
            this.pageInfoPagina.SizeF = new System.Drawing.SizeF(260F, 18F);
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
            this.lblFechaReporte});
            this.ReportHeader.HeightF = 88F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([empresa_nombre])")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 16F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(175F, 8F);
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(930F, 26F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(90F, 40F);
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(1090F, 26F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblFechaReporte
            // 
            this.lblFechaReporte.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]")});
            this.lblFechaReporte.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblFechaReporte.LocationFloat = new DevExpress.Utils.PointFloat(1115F, 12F);
            this.lblFechaReporte.Name = "lblFechaReporte";
            this.lblFechaReporte.SizeF = new System.Drawing.SizeF(175F, 22F);
            this.lblFechaReporte.StylePriority.UseFont = false;
            this.lblFechaReporte.StylePriority.UseTextAlignment = false;
            this.lblFechaReporte.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
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
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
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
            this.cellHeaderAnterior,
            this.cellHeaderDebitos,
            this.cellHeaderCreditos,
            this.cellHeaderSaldoActual,
            this.cellHeaderTotalUsuarios,
            this.cellHeaderConMedidor,
            this.cellHeaderSinMedidor,
            this.cellHeaderActivos,
            this.cellHeaderInactivos});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderCiclo
            // 
            this.cellHeaderCiclo.Name = "cellHeaderCiclo";
            this.cellHeaderCiclo.Text = "Ciclo";
            this.cellHeaderCiclo.Weight = 0.75D;
            // 
            // cellHeaderAnterior
            // 
            this.cellHeaderAnterior.Name = "cellHeaderAnterior";
            this.cellHeaderAnterior.Text = "Anterior";
            this.cellHeaderAnterior.Weight = 1.55D;
            // 
            // cellHeaderDebitos
            // 
            this.cellHeaderDebitos.Name = "cellHeaderDebitos";
            this.cellHeaderDebitos.Text = "Debitos";
            this.cellHeaderDebitos.Weight = 1.25D;
            // 
            // cellHeaderCreditos
            // 
            this.cellHeaderCreditos.Name = "cellHeaderCreditos";
            this.cellHeaderCreditos.Text = "Creditos";
            this.cellHeaderCreditos.Weight = 1.25D;
            // 
            // cellHeaderSaldoActual
            // 
            this.cellHeaderSaldoActual.Multiline = true;
            this.cellHeaderSaldoActual.Name = "cellHeaderSaldoActual";
            this.cellHeaderSaldoActual.Text = "Saldo\r\nActual";
            this.cellHeaderSaldoActual.Weight = 1.45D;
            // 
            // cellHeaderTotalUsuarios
            // 
            this.cellHeaderTotalUsuarios.Multiline = true;
            this.cellHeaderTotalUsuarios.Name = "cellHeaderTotalUsuarios";
            this.cellHeaderTotalUsuarios.Text = "Total\r\nUsuarios";
            this.cellHeaderTotalUsuarios.Weight = 1D;
            // 
            // cellHeaderConMedidor
            // 
            this.cellHeaderConMedidor.Multiline = true;
            this.cellHeaderConMedidor.Name = "cellHeaderConMedidor";
            this.cellHeaderConMedidor.Text = "Con\r\nMedidor";
            this.cellHeaderConMedidor.Weight = 1D;
            // 
            // cellHeaderSinMedidor
            // 
            this.cellHeaderSinMedidor.Multiline = true;
            this.cellHeaderSinMedidor.Name = "cellHeaderSinMedidor";
            this.cellHeaderSinMedidor.Text = "Sin\r\nMedidor";
            this.cellHeaderSinMedidor.Weight = 1D;
            // 
            // cellHeaderActivos
            // 
            this.cellHeaderActivos.Name = "cellHeaderActivos";
            this.cellHeaderActivos.Text = "Activos";
            this.cellHeaderActivos.Weight = 0.9D;
            // 
            // cellHeaderInactivos
            // 
            this.cellHeaderInactivos.Name = "cellHeaderInactivos";
            this.cellHeaderInactivos.Text = "Inactivos";
            this.cellHeaderInactivos.Weight = 0.95D;
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
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F);
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(0F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(1290F, 26F);
            this.tblDetail.StylePriority.UseFont = false;
            this.tblDetail.StylePriority.UseTextAlignment = false;
            this.tblDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellCiclo,
            this.cellAnterior,
            this.cellDebitos,
            this.cellCreditos,
            this.cellSaldoActual,
            this.cellUsuarios,
            this.cellConMedidor,
            this.cellSinMedidor,
            this.cellActivos,
            this.cellInactivos});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellCiclo
            // 
            this.cellCiclo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[ciclo]")});
            this.cellCiclo.Name = "cellCiclo";
            this.cellCiclo.StylePriority.UseTextAlignment = false;
            this.cellCiclo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.cellCiclo.Weight = 0.75D;
            // 
            // cellAnterior
            // 
            this.cellAnterior.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_anterior]")});
            this.cellAnterior.Name = "cellAnterior";
            this.cellAnterior.StylePriority.UseTextAlignment = false;
            this.cellAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAnterior.TextFormatString = "{0:n2}";
            this.cellAnterior.Weight = 1.55D;
            // 
            // cellDebitos
            // 
            this.cellDebitos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[debitos]")});
            this.cellDebitos.Name = "cellDebitos";
            this.cellDebitos.StylePriority.UseTextAlignment = false;
            this.cellDebitos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellDebitos.TextFormatString = "{0:n2}";
            this.cellDebitos.Weight = 1.25D;
            // 
            // cellCreditos
            // 
            this.cellCreditos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[creditos]")});
            this.cellCreditos.Name = "cellCreditos";
            this.cellCreditos.StylePriority.UseTextAlignment = false;
            this.cellCreditos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCreditos.TextFormatString = "{0:n2}";
            this.cellCreditos.Weight = 1.25D;
            // 
            // cellSaldoActual
            // 
            this.cellSaldoActual.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_actual]")});
            this.cellSaldoActual.Name = "cellSaldoActual";
            this.cellSaldoActual.StylePriority.UseTextAlignment = false;
            this.cellSaldoActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSaldoActual.TextFormatString = "{0:n2}";
            this.cellSaldoActual.Weight = 1.45D;
            // 
            // cellUsuarios
            // 
            this.cellUsuarios.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[total_usuarios]")});
            this.cellUsuarios.Name = "cellUsuarios";
            this.cellUsuarios.StylePriority.UseTextAlignment = false;
            this.cellUsuarios.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellUsuarios.TextFormatString = "{0:n0}";
            this.cellUsuarios.Weight = 1D;
            // 
            // cellConMedidor
            // 
            this.cellConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[con_medidor]")});
            this.cellConMedidor.Name = "cellConMedidor";
            this.cellConMedidor.StylePriority.UseTextAlignment = false;
            this.cellConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellConMedidor.TextFormatString = "{0:n0}";
            this.cellConMedidor.Weight = 1D;
            // 
            // cellSinMedidor
            // 
            this.cellSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[sin_medidor]")});
            this.cellSinMedidor.Name = "cellSinMedidor";
            this.cellSinMedidor.StylePriority.UseTextAlignment = false;
            this.cellSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSinMedidor.TextFormatString = "{0:n0}";
            this.cellSinMedidor.Weight = 1D;
            // 
            // cellActivos
            // 
            this.cellActivos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[activos]")});
            this.cellActivos.Name = "cellActivos";
            this.cellActivos.StylePriority.UseTextAlignment = false;
            this.cellActivos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellActivos.TextFormatString = "{0:n0}";
            this.cellActivos.Weight = 0.9D;
            // 
            // cellInactivos
            // 
            this.cellInactivos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[inactivos]")});
            this.cellInactivos.Name = "cellInactivos";
            this.cellInactivos.StylePriority.UseTextAlignment = false;
            this.cellInactivos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellInactivos.TextFormatString = "{0:n0}";
            this.cellInactivos.Weight = 0.95D;
            // 
            // ReportFooter
            // 
            this.ReportFooter.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.tblTotal});
            this.ReportFooter.HeightF = 36F;
            this.ReportFooter.Name = "ReportFooter";
            // 
            // tblTotal
            // 
            this.tblTotal.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblTotal.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblTotal.LocationFloat = new DevExpress.Utils.PointFloat(0F, 4F);
            this.tblTotal.Name = "tblTotal";
            this.tblTotal.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblTotalRow});
            this.tblTotal.SizeF = new System.Drawing.SizeF(1290F, 30F);
            this.tblTotal.StylePriority.UseBorders = false;
            this.tblTotal.StylePriority.UseFont = false;
            this.tblTotal.StylePriority.UseTextAlignment = false;
            this.tblTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblTotalRow
            // 
            this.tblTotalRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellTotalCaption,
            this.cellTotalAnterior,
            this.cellTotalDebitos,
            this.cellTotalCreditos,
            this.cellTotalSaldoActual,
            this.cellTotalUsuarios,
            this.cellTotalConMedidor,
            this.cellTotalSinMedidor,
            this.cellTotalActivos,
            this.cellTotalInactivos});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalCaption
            // 
            this.cellTotalCaption.Name = "cellTotalCaption";
            this.cellTotalCaption.StylePriority.UseTextAlignment = false;
            this.cellTotalCaption.Text = "Total";
            this.cellTotalCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellTotalCaption.Weight = 0.75D;
            // 
            // cellTotalAnterior
            // 
            this.cellTotalAnterior.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_anterior])")});
            this.cellTotalAnterior.Name = "cellTotalAnterior";
            this.cellTotalAnterior.StylePriority.UseTextAlignment = false;
            xrSummary1.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalAnterior.Summary = xrSummary1;
            this.cellTotalAnterior.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalAnterior.TextFormatString = "{0:n2}";
            this.cellTotalAnterior.Weight = 1.55D;
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
            this.cellTotalDebitos.Weight = 1.25D;
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
            this.cellTotalCreditos.Weight = 1.25D;
            // 
            // cellTotalSaldoActual
            // 
            this.cellTotalSaldoActual.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_actual])")});
            this.cellTotalSaldoActual.Name = "cellTotalSaldoActual";
            this.cellTotalSaldoActual.StylePriority.UseTextAlignment = false;
            xrSummary4.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldoActual.Summary = xrSummary4;
            this.cellTotalSaldoActual.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldoActual.TextFormatString = "{0:n2}";
            this.cellTotalSaldoActual.Weight = 1.45D;
            // 
            // cellTotalUsuarios
            // 
            this.cellTotalUsuarios.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([total_usuarios])")});
            this.cellTotalUsuarios.Name = "cellTotalUsuarios";
            this.cellTotalUsuarios.StylePriority.UseTextAlignment = false;
            xrSummary5.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalUsuarios.Summary = xrSummary5;
            this.cellTotalUsuarios.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalUsuarios.TextFormatString = "{0:n0}";
            this.cellTotalUsuarios.Weight = 1D;
            // 
            // cellTotalConMedidor
            // 
            this.cellTotalConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([con_medidor])")});
            this.cellTotalConMedidor.Name = "cellTotalConMedidor";
            this.cellTotalConMedidor.StylePriority.UseTextAlignment = false;
            xrSummary6.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalConMedidor.Summary = xrSummary6;
            this.cellTotalConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalConMedidor.TextFormatString = "{0:n0}";
            this.cellTotalConMedidor.Weight = 1D;
            // 
            // cellTotalSinMedidor
            // 
            this.cellTotalSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([sin_medidor])")});
            this.cellTotalSinMedidor.Name = "cellTotalSinMedidor";
            this.cellTotalSinMedidor.StylePriority.UseTextAlignment = false;
            xrSummary7.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSinMedidor.Summary = xrSummary7;
            this.cellTotalSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSinMedidor.TextFormatString = "{0:n0}";
            this.cellTotalSinMedidor.Weight = 1D;
            // 
            // cellTotalActivos
            // 
            this.cellTotalActivos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([activos])")});
            this.cellTotalActivos.Name = "cellTotalActivos";
            this.cellTotalActivos.StylePriority.UseTextAlignment = false;
            xrSummary8.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalActivos.Summary = xrSummary8;
            this.cellTotalActivos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalActivos.TextFormatString = "{0:n0}";
            this.cellTotalActivos.Weight = 0.9D;
            // 
            // cellTotalInactivos
            // 
            this.cellTotalInactivos.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([inactivos])")});
            this.cellTotalInactivos.Name = "cellTotalInactivos";
            this.cellTotalInactivos.StylePriority.UseTextAlignment = false;
            xrSummary9.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalInactivos.Summary = xrSummary9;
            this.cellTotalInactivos.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalInactivos.TextFormatString = "{0:n0}";
            this.cellTotalInactivos.Weight = 0.95D;
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
            customSqlQuery1.Parameters.AddRange(new DevExpress.DataAccess.Sql.QueryParameter[] {
            queryParameter1,
            queryParameter2,
            queryParameter3});
            customSqlQuery1.Sql = resources.GetString("customSqlQuery1.Sql");
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
            // Rpt_Dev_Saldo_Clientes_Ciclo
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
            this.Margins = new DevExpress.Drawing.DXMargins(40, 40, 20, 32);
            this.PageHeight = 850;
            this.PageWidth = 1400;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Legal;
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Desde,
            this.p_Fecha_Hasta});
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
        private DevExpress.XtraReports.UI.XRLabel lblFechaReporte;
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCiclo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSaldoActual;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTotalUsuarios;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderActivos;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderInactivos;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCiclo;
        private DevExpress.XtraReports.UI.XRTableCell cellAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldoActual;
        private DevExpress.XtraReports.UI.XRTableCell cellUsuarios;
        private DevExpress.XtraReports.UI.XRTableCell cellConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellActivos;
        private DevExpress.XtraReports.UI.XRTableCell cellInactivos;
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCaption;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalAnterior;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalDebitos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCreditos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldoActual;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalUsuarios;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalActivos;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalInactivos;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle Title;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.UI.XRControlStyle TotalData;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Desde;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Hasta;
    }
}
