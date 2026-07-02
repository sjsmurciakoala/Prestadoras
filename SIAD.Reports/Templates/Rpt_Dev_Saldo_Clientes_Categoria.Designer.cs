namespace SIAD.Reports
{
    partial class Rpt_Dev_Saldo_Clientes_Categoria
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Rpt_Dev_Saldo_Clientes_Categoria));
            this.TopMargin = new DevExpress.XtraReports.UI.TopMarginBand();
            this.BottomMargin = new DevExpress.XtraReports.UI.BottomMarginBand();
            this.pageInfoFecha = new DevExpress.XtraReports.UI.XRPageInfo();
            this.pageInfoPagina = new DevExpress.XtraReports.UI.XRPageInfo();
            this.ReportHeader = new DevExpress.XtraReports.UI.ReportHeaderBand();
            this.lblFechaSistema = new DevExpress.XtraReports.UI.XRLabel();
            this.lblEmpresa = new DevExpress.XtraReports.UI.XRLabel();
            this.lblTitulo = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaHeader = new DevExpress.XtraReports.UI.XRLabel();
            this.lblPaginaNumero = new DevExpress.XtraReports.UI.XRPageInfo();
            this.PageHeader = new DevExpress.XtraReports.UI.PageHeaderBand();
            this.tblHeader = new DevExpress.XtraReports.UI.XRTable();
            this.tblHeaderRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellHeaderCodigo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderCategoria = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAgua = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderAlcantarillado = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderFondo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderErsaps = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderConvenio = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderOtros = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderGestion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellHeaderTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.Detail = new DevExpress.XtraReports.UI.DetailBand();
            this.tblDetail = new DevExpress.XtraReports.UI.XRTable();
            this.tblDetailRow = new DevExpress.XtraReports.UI.XRTableRow();
            this.cellCodigo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellCategoria = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAgua = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellAlcantarillado = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellFondo = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellErsaps = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellConvenio = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellOtros = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellGestion = new DevExpress.XtraReports.UI.XRTableCell();
            this.cellTotal = new DevExpress.XtraReports.UI.XRTableCell();
            this.sqlDataSource1 = new DevExpress.DataAccess.Sql.SqlDataSource(this.components);
            this.HeaderCaption = new DevExpress.XtraReports.UI.XRControlStyle();
            this.DetailData = new DevExpress.XtraReports.UI.XRControlStyle();
            this.PageInfoStyle = new DevExpress.XtraReports.UI.XRControlStyle();
            this.p_Compania_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Fecha_Corte = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Categoria_Servicio_ID = new DevExpress.XtraReports.Parameters.Parameter();
            this.p_Estado_Cliente = new DevExpress.XtraReports.Parameters.Parameter();
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
            this.BottomMargin.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.pageInfoFecha,
            this.pageInfoPagina});
            this.BottomMargin.HeightF = 30F;
            this.BottomMargin.Name = "BottomMargin";
            // 
            // pageInfoFecha
            // 
            this.pageInfoFecha.LocationFloat = new DevExpress.Utils.PointFloat(0F, 4F);
            this.pageInfoFecha.Name = "pageInfoFecha";
            this.pageInfoFecha.PageInfo = DevExpress.XtraPrinting.PageInfo.DateTime;
            this.pageInfoFecha.SizeF = new System.Drawing.SizeF(320F, 20F);
            this.pageInfoFecha.StyleName = "PageInfoStyle";
            this.pageInfoFecha.TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}";
            // 
            // pageInfoPagina
            // 
            this.pageInfoPagina.LocationFloat = new DevExpress.Utils.PointFloat(1080F, 4F);
            this.pageInfoPagina.Name = "pageInfoPagina";
            this.pageInfoPagina.PageInfo = DevExpress.XtraPrinting.PageInfo.NumberOfTotal;
            this.pageInfoPagina.SizeF = new System.Drawing.SizeF(300F, 20F);
            this.pageInfoPagina.StyleName = "PageInfoStyle";
            this.pageInfoPagina.StylePriority.UseTextAlignment = false;
            this.pageInfoPagina.TextAlignment = DevExpress.XtraPrinting.TextAlignment.TopRight;
            this.pageInfoPagina.TextFormatString = "Pagina {0} de {1}";
            // 
            // ReportHeader
            // 
            this.ReportHeader.Controls.AddRange(new DevExpress.XtraReports.UI.XRControl[] {
            this.lblFechaSistema,
            this.lblEmpresa,
            this.lblTitulo,
            this.lblPaginaHeader,
            this.lblPaginaNumero});
            this.ReportHeader.HeightF = 96F;
            this.ReportHeader.Name = "ReportHeader";
            // 
            // lblFechaSistema
            // 
            this.lblFechaSistema.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]")});
            this.lblFechaSistema.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.lblFechaSistema.LocationFloat = new DevExpress.Utils.PointFloat(600F, 0F);
            this.lblFechaSistema.Name = "lblFechaSistema";
            this.lblFechaSistema.SizeF = new System.Drawing.SizeF(180F, 18F);
            this.lblFechaSistema.StylePriority.UseFont = false;
            this.lblFechaSistema.StylePriority.UseTextAlignment = false;
            this.lblFechaSistema.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblEmpresa
            // 
            this.lblEmpresa.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "Upper([empresa_nombre])")});
            this.lblEmpresa.Font = new DevExpress.Drawing.DXFont("Times New Roman", 14F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblEmpresa.LocationFloat = new DevExpress.Utils.PointFloat(260F, 18F);
            this.lblEmpresa.Name = "lblEmpresa";
            this.lblEmpresa.SizeF = new System.Drawing.SizeF(860F, 28F);
            this.lblEmpresa.StylePriority.UseFont = false;
            this.lblEmpresa.StylePriority.UseTextAlignment = false;
            this.lblEmpresa.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblTitulo
            // 
            this.lblTitulo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[titulo_reporte]")});
            this.lblTitulo.Font = new DevExpress.Drawing.DXFont("Times New Roman", 12F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblTitulo.LocationFloat = new DevExpress.Utils.PointFloat(90F, 50F);
            this.lblTitulo.Multiline = true;
            this.lblTitulo.Name = "lblTitulo";
            this.lblTitulo.SizeF = new System.Drawing.SizeF(1180F, 36F);
            this.lblTitulo.StylePriority.UseFont = false;
            this.lblTitulo.StylePriority.UseTextAlignment = false;
            this.lblTitulo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // lblPaginaHeader
            // 
            this.lblPaginaHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 11F, DevExpress.Drawing.DXFontStyle.Bold);
            this.lblPaginaHeader.LocationFloat = new DevExpress.Utils.PointFloat(1145F, 18F);
            this.lblPaginaHeader.Name = "lblPaginaHeader";
            this.lblPaginaHeader.SizeF = new System.Drawing.SizeF(70F, 22F);
            this.lblPaginaHeader.StylePriority.UseFont = false;
            this.lblPaginaHeader.Text = "PAG.";
            // 
            // lblPaginaNumero
            // 
            this.lblPaginaNumero.LocationFloat = new DevExpress.Utils.PointFloat(1220F, 18F);
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
            this.PageHeader.HeightF = 42F;
            this.PageHeader.Name = "PageHeader";
            // 
            // tblHeader
            // 
            this.tblHeader.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.tblHeader.Font = new DevExpress.Drawing.DXFont("Times New Roman", 7.25F, DevExpress.Drawing.DXFontStyle.Bold);
            this.tblHeader.LocationFloat = new DevExpress.Utils.PointFloat(8F, 4F);
            this.tblHeader.Name = "tblHeader";
            this.tblHeader.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblHeaderRow});
            this.tblHeader.SizeF = new System.Drawing.SizeF(1230F, 34F);
            this.tblHeader.StylePriority.UseBorders = false;
            this.tblHeader.StylePriority.UseFont = false;
            this.tblHeader.StylePriority.UseTextAlignment = false;
            this.tblHeader.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // tblHeaderRow
            // 
            this.tblHeaderRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellHeaderCodigo,
            this.cellHeaderCategoria,
            this.cellHeaderAgua,
            this.cellHeaderAlcantarillado,
            this.cellHeaderFondo,
            this.cellHeaderErsaps,
            this.cellHeaderConvenio,
            this.cellHeaderOtros,
            this.cellHeaderGestion,
            this.cellHeaderTotal});
            this.tblHeaderRow.Name = "tblHeaderRow";
            this.tblHeaderRow.Weight = 1D;
            // 
            // cellHeaderCodigo
            // 
            this.cellHeaderCodigo.Multiline = true;
            this.cellHeaderCodigo.Name = "cellHeaderCodigo";
            this.cellHeaderCodigo.Text = "CODIGO";
            this.cellHeaderCodigo.Weight = 0.6D;
            // 
            // cellHeaderCategoria
            // 
            this.cellHeaderCategoria.Name = "cellHeaderCategoria";
            this.cellHeaderCategoria.Text = "CATEGORIA";
            this.cellHeaderCategoria.Weight = 0.85D;
            // 
            // cellHeaderAgua
            // 
            this.cellHeaderAgua.Name = "cellHeaderAgua";
            this.cellHeaderAgua.Text = "Agua Potable";
            this.cellHeaderAgua.Weight = 1.05D;
            // 
            // cellHeaderAlcantarillado
            // 
            this.cellHeaderAlcantarillado.Multiline = true;
            this.cellHeaderAlcantarillado.Name = "cellHeaderAlcantarillado";
            this.cellHeaderAlcantarillado.Text = "Alcantarillado\nSanitario";
            this.cellHeaderAlcantarillado.Weight = 1.15D;
            // 
            // cellHeaderFondo
            // 
            this.cellHeaderFondo.Multiline = true;
            this.cellHeaderFondo.Name = "cellHeaderFondo";
            this.cellHeaderFondo.Text = "Fondo Fuentes\nde Agua";
            this.cellHeaderFondo.Weight = 1.15D;
            // 
            // cellHeaderErsaps
            // 
            this.cellHeaderErsaps.Name = "cellHeaderErsaps";
            this.cellHeaderErsaps.Text = "Tasa ERSAPS";
            this.cellHeaderErsaps.Weight = 1D;
            // 
            // cellHeaderConvenio
            // 
            this.cellHeaderConvenio.Multiline = true;
            this.cellHeaderConvenio.Name = "cellHeaderConvenio";
            this.cellHeaderConvenio.Text = "Convenio\nde Pago";
            this.cellHeaderConvenio.Weight = 1.05D;
            // 
            // cellHeaderOtros
            // 
            this.cellHeaderOtros.Name = "cellHeaderOtros";
            this.cellHeaderOtros.Text = "Otros";
            this.cellHeaderOtros.Weight = 0.85D;
            // 
            // cellHeaderGestion
            // 
            this.cellHeaderGestion.Multiline = true;
            this.cellHeaderGestion.Name = "cellHeaderGestion";
            this.cellHeaderGestion.Text = "Gestion Legal";
            this.cellHeaderGestion.Weight = 0.95D;
            // 
            // cellHeaderTotal
            // 
            this.cellHeaderTotal.Name = "cellHeaderTotal";
            this.cellHeaderTotal.Text = "Total";
            this.cellHeaderTotal.Weight = 1.35D;
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
            this.tblDetail.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.tblDetail.LocationFloat = new DevExpress.Utils.PointFloat(8F, 0F);
            this.tblDetail.Name = "tblDetail";
            this.tblDetail.Rows.AddRange(new DevExpress.XtraReports.UI.XRTableRow[] {
            this.tblDetailRow});
            this.tblDetail.SizeF = new System.Drawing.SizeF(1230F, 21F);
            this.tblDetail.StylePriority.UseFont = false;
            this.tblDetail.StylePriority.UseTextAlignment = false;
            this.tblDetail.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            this.tblDetail.BeforePrint += new DevExpress.XtraReports.UI.BeforePrintEventHandler(this.TblDetail_BeforePrint);
            // 
            // tblDetailRow
            // 
            this.tblDetailRow.Cells.AddRange(new DevExpress.XtraReports.UI.XRTableCell[] {
            this.cellCodigo,
            this.cellCategoria,
            this.cellAgua,
            this.cellAlcantarillado,
            this.cellFondo,
            this.cellErsaps,
            this.cellConvenio,
            this.cellOtros,
            this.cellGestion,
            this.cellTotal});
            this.tblDetailRow.Name = "tblDetailRow";
            this.tblDetailRow.Weight = 1D;
            // 
            // cellCodigo
            // 
            this.cellCodigo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[codigo]")});
            this.cellCodigo.Name = "cellCodigo";
            this.cellCodigo.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            this.cellCodigo.StyleName = "DetailData";
            this.cellCodigo.StylePriority.UsePadding = false;
            this.cellCodigo.Weight = 0.6D;
            // 
            // cellCategoria
            // 
            this.cellCategoria.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[categoria]")});
            this.cellCategoria.Name = "cellCategoria";
            this.cellCategoria.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            this.cellCategoria.StyleName = "DetailData";
            this.cellCategoria.StylePriority.UsePadding = false;
            this.cellCategoria.StylePriority.UseTextAlignment = false;
            this.cellCategoria.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleLeft;
            this.cellCategoria.Weight = 0.85D;
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
            this.cellAgua.Weight = 1.05D;
            // 
            // cellAlcantarillado
            // 
            this.cellAlcantarillado.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[alcantarillado_sanitario]")});
            this.cellAlcantarillado.Name = "cellAlcantarillado";
            this.cellAlcantarillado.StyleName = "DetailData";
            this.cellAlcantarillado.StylePriority.UseTextAlignment = false;
            this.cellAlcantarillado.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellAlcantarillado.TextFormatString = "{0:n2}";
            this.cellAlcantarillado.Weight = 1.15D;
            // 
            // cellFondo
            // 
            this.cellFondo.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[fondo_fuentes_agua]")});
            this.cellFondo.Name = "cellFondo";
            this.cellFondo.StyleName = "DetailData";
            this.cellFondo.StylePriority.UseTextAlignment = false;
            this.cellFondo.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellFondo.TextFormatString = "{0:n2}";
            this.cellFondo.Weight = 1.15D;
            // 
            // cellErsaps
            // 
            this.cellErsaps.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[tasa_ersaps]")});
            this.cellErsaps.Name = "cellErsaps";
            this.cellErsaps.StyleName = "DetailData";
            this.cellErsaps.StylePriority.UseTextAlignment = false;
            this.cellErsaps.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellErsaps.TextFormatString = "{0:n2}";
            this.cellErsaps.Weight = 1D;
            // 
            // cellConvenio
            // 
            this.cellConvenio.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[convenio_pago]")});
            this.cellConvenio.Name = "cellConvenio";
            this.cellConvenio.StyleName = "DetailData";
            this.cellConvenio.StylePriority.UseTextAlignment = false;
            this.cellConvenio.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellConvenio.TextFormatString = "{0:n2}";
            this.cellConvenio.Weight = 1.05D;
            // 
            // cellOtros
            // 
            this.cellOtros.ExpressionBindings.AddRange(new DevExpress.XtraReports.UI.ExpressionBinding[] {
            new DevExpress.XtraReports.UI.ExpressionBinding("BeforePrint", "Text", "[otros]")});
            this.cellOtros.Name = "cellOtros";
            this.cellOtros.StyleName = "DetailData";
            this.cellOtros.StylePriority.UseTextAlignment = false;
            this.cellOtros.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleRight;
            this.cellOtros.TextFormatString = "{0:n2}";
            this.cellOtros.Weight = 0.85D;
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
            this.cellGestion.Weight = 0.95D;
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
            this.cellTotal.Weight = 1.35D;
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
            queryParameter3.Name = "CategoriaServicioId";
            queryParameter3.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter3.Value = new DevExpress.DataAccess.Expression("?p_Categoria_Servicio_ID", typeof(int));
            queryParameter4.Name = "EstadoCliente";
            queryParameter4.Type = typeof(global::DevExpress.DataAccess.Expression);
            queryParameter4.Value = new DevExpress.DataAccess.Expression("?p_Estado_Cliente", typeof(int));
            customSqlQuery1.Parameters.AddRange(new DevExpress.DataAccess.Sql.QueryParameter[] {
            queryParameter1,
            queryParameter2,
            queryParameter3,
            queryParameter4});
            customSqlQuery1.Sql = resources.GetString("customSqlQuery1.Sql");
            this.sqlDataSource1.Queries.AddRange(new DevExpress.DataAccess.Sql.SqlQuery[] {
            customSqlQuery1});
            this.sqlDataSource1.ResultSchemaSerializable = resources.GetString("sqlDataSource1.ResultSchemaSerializable");
            // 
            // HeaderCaption
            // 
            this.HeaderCaption.Borders = DevExpress.XtraPrinting.BorderSide.Top | DevExpress.XtraPrinting.BorderSide.Bottom;
            this.HeaderCaption.BorderWidth = 1F;
            this.HeaderCaption.Font = new DevExpress.Drawing.DXFont("Times New Roman", 7.25F, DevExpress.Drawing.DXFontStyle.Bold);
            this.HeaderCaption.Name = "HeaderCaption";
            this.HeaderCaption.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            this.HeaderCaption.TextAlignment = DevExpress.XtraPrinting.TextAlignment.MiddleCenter;
            // 
            // DetailData
            // 
            this.DetailData.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.DetailData.Name = "DetailData";
            this.DetailData.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
            // 
            // PageInfoStyle
            // 
            this.PageInfoStyle.Font = new DevExpress.Drawing.DXFont("Times New Roman", 8F);
            this.PageInfoStyle.Name = "PageInfoStyle";
            this.PageInfoStyle.Padding = new DevExpress.XtraPrinting.PaddingInfo(2, 2, 0, 0, 100F);
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
            // p_Categoria_Servicio_ID
            // 
            this.p_Categoria_Servicio_ID.Name = "p_Categoria_Servicio_ID";
            this.p_Categoria_Servicio_ID.Type = typeof(int);
            this.p_Categoria_Servicio_ID.ValueInfo = "0";
            // 
            // p_Estado_Cliente
            // 
            this.p_Estado_Cliente.Name = "p_Estado_Cliente";
            this.p_Estado_Cliente.Type = typeof(int);
            this.p_Estado_Cliente.ValueInfo = "0";
            // 
            // Rpt_Dev_Saldo_Clientes_Categoria
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
            this.Margins = new DevExpress.Drawing.DXMargins(35, 35, 18, 30);
            this.PageHeight = 850;
            this.PageWidth = 1450;
            this.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Legal;
            this.Parameters.AddRange(new DevExpress.XtraReports.Parameters.Parameter[] {
            this.p_Compania_ID,
            this.p_Fecha_Corte,
            this.p_Categoria_Servicio_ID,
            this.p_Estado_Cliente});
            this.StyleSheet.AddRange(new DevExpress.XtraReports.UI.XRControlStyle[] {
            this.HeaderCaption,
            this.DetailData,
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
        private DevExpress.XtraReports.UI.XRLabel lblFechaSistema;
        private DevExpress.XtraReports.UI.XRLabel lblEmpresa;
        private DevExpress.XtraReports.UI.XRLabel lblTitulo;
        private DevExpress.XtraReports.UI.XRLabel lblPaginaHeader;
        private DevExpress.XtraReports.UI.XRPageInfo lblPaginaNumero;
        private DevExpress.XtraReports.UI.PageHeaderBand PageHeader;
        private DevExpress.XtraReports.UI.XRTable tblHeader;
        private DevExpress.XtraReports.UI.XRTableRow tblHeaderRow;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCodigo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderCategoria;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAgua;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderAlcantarillado;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderFondo;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderErsaps;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderConvenio;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderOtros;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderGestion;
        private DevExpress.XtraReports.UI.XRTableCell cellHeaderTotal;
        private DevExpress.XtraReports.UI.DetailBand Detail;
        private DevExpress.XtraReports.UI.XRTable tblDetail;
        private DevExpress.XtraReports.UI.XRTableRow tblDetailRow;
        private DevExpress.XtraReports.UI.XRTableCell cellCodigo;
        private DevExpress.XtraReports.UI.XRTableCell cellCategoria;
        private DevExpress.XtraReports.UI.XRTableCell cellAgua;
        private DevExpress.XtraReports.UI.XRTableCell cellAlcantarillado;
        private DevExpress.XtraReports.UI.XRTableCell cellFondo;
        private DevExpress.XtraReports.UI.XRTableCell cellErsaps;
        private DevExpress.XtraReports.UI.XRTableCell cellConvenio;
        private DevExpress.XtraReports.UI.XRTableCell cellOtros;
        private DevExpress.XtraReports.UI.XRTableCell cellGestion;
        private DevExpress.XtraReports.UI.XRTableCell cellTotal;
        private DevExpress.DataAccess.Sql.SqlDataSource sqlDataSource1;
        private DevExpress.XtraReports.UI.XRControlStyle HeaderCaption;
        private DevExpress.XtraReports.UI.XRControlStyle DetailData;
        private DevExpress.XtraReports.UI.XRControlStyle PageInfoStyle;
        private DevExpress.XtraReports.Parameters.Parameter p_Compania_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Fecha_Corte;
        private DevExpress.XtraReports.Parameters.Parameter p_Categoria_Servicio_ID;
        private DevExpress.XtraReports.Parameters.Parameter p_Estado_Cliente;
    }
}
