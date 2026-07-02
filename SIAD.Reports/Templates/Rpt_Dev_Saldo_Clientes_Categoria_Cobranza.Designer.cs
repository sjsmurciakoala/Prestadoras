namespace SIAD.Reports
{
    partial class Rpt_Dev_Saldo_Clientes_Categoria_Cobranza
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Saldo_Clientes_Categoria_Cobranza));
            
            // Summaries for footer
            DevExpress.XtraReports.UI.XRSummary xrSummaryCantConMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryFactConMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummarySaldoConMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryConsumoConMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryCantSinMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryFactSinMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummarySaldoSinMedidor = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryCantTotal = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummaryFactTotal = new DevExpress.XtraReports.UI.XRSummary();
            DevExpress.XtraReports.UI.XRSummary xrSummarySaldoTotal = new DevExpress.XtraReports.UI.XRSummary();

            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.pageInfoFecha = new DevExpress.XtraReports.UI.XRPageInfo();
            this.pageInfoPagina = new DevExpress.XtraReports.UI.XRPageInfo();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.lblFechaReporte = new DevExpress.XtraReports.UI.XRLabel();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            
            // Header table
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow1 = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderRow1_CateGoria = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow1_Descripcion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow1_ConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow1_SinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow1_TotalAcueducto = new DevExpress.XtraReports.UI.XRTableCell();
            
            this.tblHeaderRow2 = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderRow2_CateGoria = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_Descripcion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_ConMedidorCant = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_ConMedidorFact = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_ConMedidorSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_ConMedidorConsumo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_SinMedidorCant = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_SinMedidorFact = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_SinMedidorSaldo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_TotalCant = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_TotalFact = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderRow2_TotalSaldo = new DevExpress.XtraReports.UI.XRTableCell();

            // Detail table
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCategoriaOrden = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCategoriaDescripcion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCantConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFacturacionConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldoConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellConsumoConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCantSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFacturacionSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldoSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCantTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFacturacionTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellSaldoTotal = new DevExpress.XtraReports.UI.XRTableCell();

            // Footer table
            this.ReportFooter = new DevExpress.XtraReports.UI.ReportFooterBand();
            this.tblTotal = new DevExpress.XtraReports.UI.XRTable();
            this.tblTotalRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellTotalCaption = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCantConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalFacturacionConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldoConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalConsumoConMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCantSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalFacturacionSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldoSinMedidor = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalCantTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalFacturacionTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotalSaldoTotal = new DevExpress.XtraReports.UI.XRTableCell();

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
            this.PageHeader.HeightF = 72F; // Expanded height for two rows
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(0F, 6F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow1,
            this.tblHeaderRow2});
            this.tblHeader.SizeF = new System.Drawing.SizeF(1290F, 60F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblHeaderRow1
            // 
            this.tblHeaderRow1.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderRow1_CateGoria,
            this.cellHeaderRow1_Descripcion,
            this.cellHeaderRow1_ConMedidor,
            this.cellHeaderRow1_SinMedidor,
            this.cellHeaderRow1_TotalAcueducto});
            this.tblHeaderRow1.Name = "tblHeaderRow1";
            this.tblHeaderRow1.Weight = 1D;
            // 
            // cellHeaderRow1_CateGoria
            // 
            this.cellHeaderRow1_CateGoria.Name = "cellHeaderRow1_CateGoria";
            this.cellHeaderRow1_CateGoria.Text = "";
            this.cellHeaderRow1_CateGoria.Weight = 0.5D;
            // 
            // cellHeaderRow1_Descripcion
            // 
            this.cellHeaderRow1_Descripcion.Name = "cellHeaderRow1_Descripcion";
            this.cellHeaderRow1_Descripcion.Text = "";
            this.cellHeaderRow1_Descripcion.Weight = 1.6D;
            // 
            // cellHeaderRow1_ConMedidor
            // 
            this.cellHeaderRow1_ConMedidor.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.cellHeaderRow1_ConMedidor.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellHeaderRow1_ConMedidor.Name = "cellHeaderRow1_ConMedidor";
            this.cellHeaderRow1_ConMedidor.Text = "CLIENTES CON MEDIDOR";
            this.cellHeaderRow1_ConMedidor.Weight = 3.6D;
            this.cellHeaderRow1_ConMedidor.StylePriority.UseBorders = false;
            this.cellHeaderRow1_ConMedidor.StylePriority.UseFont = false;
            // 
            // cellHeaderRow1_SinMedidor
            // 
            this.cellHeaderRow1_SinMedidor.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.cellHeaderRow1_SinMedidor.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellHeaderRow1_SinMedidor.Name = "cellHeaderRow1_SinMedidor";
            this.cellHeaderRow1_SinMedidor.Text = "CLIENTES SIN MEDIDOR";
            this.cellHeaderRow1_SinMedidor.Weight = 2.7D;
            this.cellHeaderRow1_SinMedidor.StylePriority.UseBorders = false;
            this.cellHeaderRow1_SinMedidor.StylePriority.UseFont = false;
            // 
            // cellHeaderRow1_TotalAcueducto
            // 
            this.cellHeaderRow1_TotalAcueducto.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.cellHeaderRow1_TotalAcueducto.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.cellHeaderRow1_TotalAcueducto.Name = "cellHeaderRow1_TotalAcueducto";
            this.cellHeaderRow1_TotalAcueducto.Text = "TOTAL ACUEDUCTO";
            this.cellHeaderRow1_TotalAcueducto.Weight = 2.7D;
            this.cellHeaderRow1_TotalAcueducto.StylePriority.UseBorders = false;
            this.cellHeaderRow1_TotalAcueducto.StylePriority.UseFont = false;
            
            // 
            // tblHeaderRow2
            // 
            this.tblHeaderRow2.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderRow2_CateGoria,
            this.cellHeaderRow2_Descripcion,
            this.cellHeaderRow2_ConMedidorCant,
            this.cellHeaderRow2_ConMedidorFact,
            this.cellHeaderRow2_ConMedidorSaldo,
            this.cellHeaderRow2_ConMedidorConsumo,
            this.cellHeaderRow2_SinMedidorCant,
            this.cellHeaderRow2_SinMedidorFact,
            this.cellHeaderRow2_SinMedidorSaldo,
            this.cellHeaderRow2_TotalCant,
            this.cellHeaderRow2_TotalFact,
            this.cellHeaderRow2_TotalSaldo});
            this.tblHeaderRow2.Name = "tblHeaderRow2";
            this.tblHeaderRow2.Weight = 1D;
            // 
            // cellHeaderRow2_CateGoria
            // 
            this.cellHeaderRow2_CateGoria.Name = "cellHeaderRow2_CateGoria";
            this.cellHeaderRow2_CateGoria.Text = "Cate\r\ngoria";
            this.cellHeaderRow2_CateGoria.Weight = 0.5D;
            // 
            // cellHeaderRow2_Descripcion
            // 
            this.cellHeaderRow2_Descripcion.Name = "cellHeaderRow2_Descripcion";
            this.cellHeaderRow2_Descripcion.Text = "Descripcion Categoria";
            this.cellHeaderRow2_Descripcion.Weight = 1.6D;
            // 
            // cellHeaderRow2_ConMedidorCant
            // 
            this.cellHeaderRow2_ConMedidorCant.Name = "cellHeaderRow2_ConMedidorCant";
            this.cellHeaderRow2_ConMedidorCant.Text = "Cantidad";
            this.cellHeaderRow2_ConMedidorCant.Weight = 0.8D;
            // 
            // cellHeaderRow2_ConMedidorFact
            // 
            this.cellHeaderRow2_ConMedidorFact.Multiline = true;
            this.cellHeaderRow2_ConMedidorFact.Name = "cellHeaderRow2_ConMedidorFact";
            this.cellHeaderRow2_ConMedidorFact.Text = "Facturacion\r\nMes";
            this.cellHeaderRow2_ConMedidorFact.Weight = 0.9D;
            // 
            // cellHeaderRow2_ConMedidorSaldo
            // 
            this.cellHeaderRow2_ConMedidorSaldo.Multiline = true;
            this.cellHeaderRow2_ConMedidorSaldo.Name = "cellHeaderRow2_ConMedidorSaldo";
            this.cellHeaderRow2_ConMedidorSaldo.Text = "Saldo\r\nAcumulado";
            this.cellHeaderRow2_ConMedidorSaldo.Weight = 1.0D;
            // 
            // cellHeaderRow2_ConMedidorConsumo
            // 
            this.cellHeaderRow2_ConMedidorConsumo.Multiline = true;
            this.cellHeaderRow2_ConMedidorConsumo.Name = "cellHeaderRow2_ConMedidorConsumo";
            this.cellHeaderRow2_ConMedidorConsumo.Text = "Consumo\r\nM3";
            this.cellHeaderRow2_ConMedidorConsumo.Weight = 0.9D;
            // 
            // cellHeaderRow2_SinMedidorCant
            // 
            this.cellHeaderRow2_SinMedidorCant.Name = "cellHeaderRow2_SinMedidorCant";
            this.cellHeaderRow2_SinMedidorCant.Text = "Cantidad";
            this.cellHeaderRow2_SinMedidorCant.Weight = 0.8D;
            // 
            // cellHeaderRow2_SinMedidorFact
            // 
            this.cellHeaderRow2_SinMedidorFact.Multiline = true;
            this.cellHeaderRow2_SinMedidorFact.Name = "cellHeaderRow2_SinMedidorFact";
            this.cellHeaderRow2_SinMedidorFact.Text = "Facturacion\r\nMes";
            this.cellHeaderRow2_SinMedidorFact.Weight = 0.9D;
            // 
            // cellHeaderRow2_SinMedidorSaldo
            // 
            this.cellHeaderRow2_SinMedidorSaldo.Multiline = true;
            this.cellHeaderRow2_SinMedidorSaldo.Name = "cellHeaderRow2_SinMedidorSaldo";
            this.cellHeaderRow2_SinMedidorSaldo.Text = "Saldo\r\nAcumulado";
            this.cellHeaderRow2_SinMedidorSaldo.Weight = 1.0D;
            // 
            // cellHeaderRow2_TotalCant
            // 
            this.cellHeaderRow2_TotalCant.Name = "cellHeaderRow2_TotalCant";
            this.cellHeaderRow2_TotalCant.Text = "Cantidad";
            this.cellHeaderRow2_TotalCant.Weight = 0.8D;
            // 
            // cellHeaderRow2_TotalFact
            // 
            this.cellHeaderRow2_TotalFact.Multiline = true;
            this.cellHeaderRow2_TotalFact.Name = "cellHeaderRow2_TotalFact";
            this.cellHeaderRow2_TotalFact.Text = "Facturacion\r\nMes";
            this.cellHeaderRow2_TotalFact.Weight = 0.9D;
            // 
            // cellHeaderRow2_TotalSaldo
            // 
            this.cellHeaderRow2_TotalSaldo.Multiline = true;
            this.cellHeaderRow2_TotalSaldo.Name = "cellHeaderRow2_TotalSaldo";
            this.cellHeaderRow2_TotalSaldo.Text = "Saldo\r\nAcumulado";
            this.cellHeaderRow2_TotalSaldo.Weight = 1.0D;

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
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 10F);
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
            this.cellCategoriaOrden,
            this.cellCategoriaDescripcion,
            this.cellCantConMedidor,
            this.cellFacturacionConMedidor,
            this.cellSaldoConMedidor,
            this.cellConsumoConMedidor,
            this.cellCantSinMedidor,
            this.cellFacturacionSinMedidor,
            this.cellSaldoSinMedidor,
            this.cellCantTotal,
            this.cellFacturacionTotal,
            this.cellSaldoTotal});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellCategoriaOrden
            // 
            this.cellCategoriaOrden.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[categoria_orden]")});
            this.cellCategoriaOrden.Name = "cellCategoriaOrden";
            this.cellCategoriaOrden.StylePriority.UseTextAlignment = false;
            this.cellCategoriaOrden.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellCategoriaOrden.Weight = 0.5D;
            // 
            // cellCategoriaDescripcion
            // 
            this.cellCategoriaDescripcion.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[categoria]")});
            this.cellCategoriaDescripcion.Name = "cellCategoriaDescripcion";
            this.cellCategoriaDescripcion.StylePriority.UseTextAlignment = false;
            this.cellCategoriaDescripcion.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellCategoriaDescripcion.Weight = 1.6D;
            // 
            // cellCantConMedidor
            // 
            this.cellCantConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cant_con_medidor]")});
            this.cellCantConMedidor.Name = "cellCantConMedidor";
            this.cellCantConMedidor.StylePriority.UseTextAlignment = false;
            this.cellCantConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCantConMedidor.TextFormatString = "{0:n0}";
            this.cellCantConMedidor.Weight = 0.8D;
            // 
            // cellFacturacionConMedidor
            // 
            this.cellFacturacionConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[facturacion_con_medidor]")});
            this.cellFacturacionConMedidor.Name = "cellFacturacionConMedidor";
            this.cellFacturacionConMedidor.StylePriority.UseTextAlignment = false;
            this.cellFacturacionConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellFacturacionConMedidor.TextFormatString = "{0:n2}";
            this.cellFacturacionConMedidor.Weight = 0.9D;
            // 
            // cellSaldoConMedidor
            // 
            this.cellSaldoConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_con_medidor]")});
            this.cellSaldoConMedidor.Name = "cellSaldoConMedidor";
            this.cellSaldoConMedidor.StylePriority.UseTextAlignment = false;
            this.cellSaldoConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSaldoConMedidor.TextFormatString = "{0:n2}";
            this.cellSaldoConMedidor.Weight = 1.0D;
            // 
            // cellConsumoConMedidor
            // 
            this.cellConsumoConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[consumo_con_medidor]")});
            this.cellConsumoConMedidor.Name = "cellConsumoConMedidor";
            this.cellConsumoConMedidor.StylePriority.UseTextAlignment = false;
            this.cellConsumoConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellConsumoConMedidor.TextFormatString = "{0:n0}";
            this.cellConsumoConMedidor.Weight = 0.9D;
            // 
            // cellCantSinMedidor
            // 
            this.cellCantSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cant_sin_medidor]")});
            this.cellCantSinMedidor.Name = "cellCantSinMedidor";
            this.cellCantSinMedidor.StylePriority.UseTextAlignment = false;
            this.cellCantSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCantSinMedidor.TextFormatString = "{0:n0}";
            this.cellCantSinMedidor.Weight = 0.8D;
            // 
            // cellFacturacionSinMedidor
            // 
            this.cellFacturacionSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[facturacion_sin_medidor]")});
            this.cellFacturacionSinMedidor.Name = "cellFacturacionSinMedidor";
            this.cellFacturacionSinMedidor.StylePriority.UseTextAlignment = false;
            this.cellFacturacionSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellFacturacionSinMedidor.TextFormatString = "{0:n2}";
            this.cellFacturacionSinMedidor.Weight = 0.9D;
            // 
            // cellSaldoSinMedidor
            // 
            this.cellSaldoSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_sin_medidor]")});
            this.cellSaldoSinMedidor.Name = "cellSaldoSinMedidor";
            this.cellSaldoSinMedidor.StylePriority.UseTextAlignment = false;
            this.cellSaldoSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSaldoSinMedidor.TextFormatString = "{0:n2}";
            this.cellSaldoSinMedidor.Weight = 1.0D;
            // 
            // cellCantTotal
            // 
            this.cellCantTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[cant_total]")});
            this.cellCantTotal.Name = "cellCantTotal";
            this.cellCantTotal.StylePriority.UseTextAlignment = false;
            this.cellCantTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellCantTotal.TextFormatString = "{0:n0}";
            this.cellCantTotal.Weight = 0.8D;
            // 
            // cellFacturacionTotal
            // 
            this.cellFacturacionTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[facturacion_total]")});
            this.cellFacturacionTotal.Name = "cellFacturacionTotal";
            this.cellFacturacionTotal.StylePriority.UseTextAlignment = false;
            this.cellFacturacionTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellFacturacionTotal.TextFormatString = "{0:n2}";
            this.cellFacturacionTotal.Weight = 0.9D;
            // 
            // cellSaldoTotal
            // 
            this.cellSaldoTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[saldo_total]")});
            this.cellSaldoTotal.Name = "cellSaldoTotal";
            this.cellSaldoTotal.StylePriority.UseTextAlignment = false;
            this.cellSaldoTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellSaldoTotal.TextFormatString = "{0:n2}";
            this.cellSaldoTotal.Weight = 1.0D;

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
            this.cellTotalCantConMedidor,
            this.cellTotalFacturacionConMedidor,
            this.cellTotalSaldoConMedidor,
            this.cellTotalConsumoConMedidor,
            this.cellTotalCantSinMedidor,
            this.cellTotalFacturacionSinMedidor,
            this.cellTotalSaldoSinMedidor,
            this.cellTotalCantTotal,
            this.cellTotalFacturacionTotal,
            this.cellTotalSaldoTotal});
            this.tblTotalRow.Name = "tblTotalRow";
            this.tblTotalRow.Weight = 1D;
            // 
            // cellTotalCaption
            // 
            this.cellTotalCaption.Name = "cellTotalCaption";
            this.cellTotalCaption.StylePriority.UseTextAlignment = false;
            this.cellTotalCaption.Text = "TOTAL";
            this.cellTotalCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellTotalCaption.Weight = 2.1D; // Combines 0.5 + 1.6
            // 
            // cellTotalCantConMedidor
            // 
            this.cellTotalCantConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([cant_con_medidor])")});
            this.cellTotalCantConMedidor.Name = "cellTotalCantConMedidor";
            this.cellTotalCantConMedidor.StylePriority.UseTextAlignment = false;
            xrSummaryCantConMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCantConMedidor.Summary = xrSummaryCantConMedidor;
            this.cellTotalCantConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCantConMedidor.TextFormatString = "{0:n0}";
            this.cellTotalCantConMedidor.Weight = 0.8D;
            // 
            // cellTotalFacturacionConMedidor
            // 
            this.cellTotalFacturacionConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([facturacion_con_medidor])")});
            this.cellTotalFacturacionConMedidor.Name = "cellTotalFacturacionConMedidor";
            this.cellTotalFacturacionConMedidor.StylePriority.UseTextAlignment = false;
            xrSummaryFactConMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalFacturacionConMedidor.Summary = xrSummaryFactConMedidor;
            this.cellTotalFacturacionConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalFacturacionConMedidor.TextFormatString = "{0:n2}";
            this.cellTotalFacturacionConMedidor.Weight = 0.9D;
            // 
            // cellTotalSaldoConMedidor
            // 
            this.cellTotalSaldoConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_con_medidor])")});
            this.cellTotalSaldoConMedidor.Name = "cellTotalSaldoConMedidor";
            this.cellTotalSaldoConMedidor.StylePriority.UseTextAlignment = false;
            xrSummarySaldoConMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldoConMedidor.Summary = xrSummarySaldoConMedidor;
            this.cellTotalSaldoConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldoConMedidor.TextFormatString = "{0:n2}";
            this.cellTotalSaldoConMedidor.Weight = 1.0D;
            // 
            // cellTotalConsumoConMedidor
            // 
            this.cellTotalConsumoConMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([consumo_con_medidor])")});
            this.cellTotalConsumoConMedidor.Name = "cellTotalConsumoConMedidor";
            this.cellTotalConsumoConMedidor.StylePriority.UseTextAlignment = false;
            xrSummaryConsumoConMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalConsumoConMedidor.Summary = xrSummaryConsumoConMedidor;
            this.cellTotalConsumoConMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalConsumoConMedidor.TextFormatString = "{0:n0}";
            this.cellTotalConsumoConMedidor.Weight = 0.9D;
            // 
            // cellTotalCantSinMedidor
            // 
            this.cellTotalCantSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([cant_sin_medidor])")});
            this.cellTotalCantSinMedidor.Name = "cellTotalCantSinMedidor";
            this.cellTotalCantSinMedidor.StylePriority.UseTextAlignment = false;
            xrSummaryCantSinMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCantSinMedidor.Summary = xrSummaryCantSinMedidor;
            this.cellTotalCantSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCantSinMedidor.TextFormatString = "{0:n0}";
            this.cellTotalCantSinMedidor.Weight = 0.8D;
            // 
            // cellTotalFacturacionSinMedidor
            // 
            this.cellTotalFacturacionSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([facturacion_sin_medidor])")});
            this.cellTotalFacturacionSinMedidor.Name = "cellTotalFacturacionSinMedidor";
            this.cellTotalFacturacionSinMedidor.StylePriority.UseTextAlignment = false;
            xrSummaryFactSinMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalFacturacionSinMedidor.Summary = xrSummaryFactSinMedidor;
            this.cellTotalFacturacionSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalFacturacionSinMedidor.TextFormatString = "{0:n2}";
            this.cellTotalFacturacionSinMedidor.Weight = 0.9D;
            // 
            // cellTotalSaldoSinMedidor
            // 
            this.cellTotalSaldoSinMedidor.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_sin_medidor])")});
            this.cellTotalSaldoSinMedidor.Name = "cellTotalSaldoSinMedidor";
            this.cellTotalSaldoSinMedidor.StylePriority.UseTextAlignment = false;
            xrSummarySaldoSinMedidor.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldoSinMedidor.Summary = xrSummarySaldoSinMedidor;
            this.cellTotalSaldoSinMedidor.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldoSinMedidor.TextFormatString = "{0:n2}";
            this.cellTotalSaldoSinMedidor.Weight = 1.0D;
            // 
            // cellTotalCantTotal
            // 
            this.cellTotalCantTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([cant_total])")});
            this.cellTotalCantTotal.Name = "cellTotalCantTotal";
            this.cellTotalCantTotal.StylePriority.UseTextAlignment = false;
            xrSummaryCantTotal.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalCantTotal.Summary = xrSummaryCantTotal;
            this.cellTotalCantTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalCantTotal.TextFormatString = "{0:n0}";
            this.cellTotalCantTotal.Weight = 0.8D;
            // 
            // cellTotalFacturacionTotal
            // 
            this.cellTotalFacturacionTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([facturacion_total])")});
            this.cellTotalFacturacionTotal.Name = "cellTotalFacturacionTotal";
            this.cellTotalFacturacionTotal.StylePriority.UseTextAlignment = false;
            xrSummaryFactTotal.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalFacturacionTotal.Summary = xrSummaryFactTotal;
            this.cellTotalFacturacionTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalFacturacionTotal.TextFormatString = "{0:n2}";
            this.cellTotalFacturacionTotal.Weight = 0.9D;
            // 
            // cellTotalSaldoTotal
            // 
            this.cellTotalSaldoTotal.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "sumSum([saldo_total])")});
            this.cellTotalSaldoTotal.Name = "cellTotalSaldoTotal";
            this.cellTotalSaldoTotal.StylePriority.UseTextAlignment = false;
            xrSummarySaldoTotal.Running = DevExpress.XtraReports.UI.SummaryRunning.Report;
            this.cellTotalSaldoTotal.Summary = xrSummarySaldoTotal;
            this.cellTotalSaldoTotal.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellTotalSaldoTotal.TextFormatString = "{0:n2}";
            this.cellTotalSaldoTotal.Weight = 1.0D;

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
            // p_Categoria_Servicio_ID
            // 
            this.p_Categoria_Servicio_ID.Name = "p_Categoria_Servicio_ID";
            this.p_Categoria_Servicio_ID.Type = typeof(int);
            this.p_Categoria_Servicio_ID.ValueInfo = "0";
            // 
            // Rpt_Dev_Saldo_Clientes_Categoria_Cobranza
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
            this.p_Fecha_Hasta,
            this.p_Categoria_Servicio_ID});
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
        
        // Header
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow1;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow1_CateGoria;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow1_Descripcion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow1_ConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow1_SinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow1_TotalAcueducto;
        
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow2;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_CateGoria;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_Descripcion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_ConMedidorCant;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_ConMedidorFact;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_ConMedidorSaldo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_ConMedidorConsumo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_SinMedidorCant;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_SinMedidorFact;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_SinMedidorSaldo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_TotalCant;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_TotalFact;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderRow2_TotalSaldo;

        // Detail
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCategoriaOrden;
        private DevExpress.XtraReports.UI.XRTableCell cellCategoriaDescripcion;
        private DevExpress.XtraReports.UI.XRTableCell cellCantConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellFacturacionConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldoConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellConsumoConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellCantSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellFacturacionSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldoSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellCantTotal;
        private DevExpress.XtraReports.UI.XRTableCell cellFacturacionTotal;
        private DevExpress.XtraReports.UI.XRTableCell cellSaldoTotal;

        // Footer
        private DevExpress.XtraReports.UI.ReportFooterBand ReportFooter;
        private DevExpress.XtraReports.UI.XRTable tblTotal;
        private DevExpress.XtraReports.UI.XRTableRow tblTotalRow;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCaption;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCantConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalFacturacionConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldoConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalConsumoConMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCantSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalFacturacionSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldoSinMedidor;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalCantTotal;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalFacturacionTotal;
        private DevExpress.XtraReports.UI.XRTableCell cellTotalSaldoTotal;

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
