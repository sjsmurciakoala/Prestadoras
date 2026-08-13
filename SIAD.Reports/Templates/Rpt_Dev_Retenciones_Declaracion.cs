using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Retenciones;

namespace SIAD.Reports;

/// <summary>
/// Reporte mensual de retenciones para la declaración (F5.1): listado que pagina, agrupado por TIPO
/// (grupo externo) y por PROVEEDOR (grupo interno) con subtotales por grupo y gran total. Calca el
/// patrón de <see cref="Rpt_Dev_Existencias_Bodega"/> (enlace por <see cref="XtraReport.DataSource"/>,
/// bandas de grupo con <c>ExpressionBindings</c> y <see cref="XRSummary"/>), agregando un segundo nivel
/// de agrupación vía <see cref="GroupBand.Level"/> (0 = pegado al Detail = proveedor; 1 = externo = tipo;
/// header y footer del mismo grupo comparten Level). El encabezado de empresa se reutiliza del base.
/// El total refleja EXACTAMENTE el filtro (por defecto Vigentes ⇒ total a declarar; el estado va en el
/// texto de filtro y en la columna Estado).
/// </summary>
public sealed class Rpt_Dev_Retenciones_Declaracion : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColFolioX = 0f, ColFolioW = 70f;
    private const float ColFechaX = 70f, ColFechaW = 80f;
    private const float ColOrdenX = 150f, ColOrdenW = 50f;
    private const float ColAbonoX = 200f, ColAbonoW = 50f;
    private const float ColRtnX = 250f, ColRtnW = 120f;
    private const float ColPctX = 370f, ColPctW = 50f;
    private const float ColBaseX = 420f, ColBaseW = 110f;
    private const float ColRetX = 530f, ColRetW = 110f;
    private const float ColEstadoX = 640f, ColEstadoW = 110f;

    private const float RowHeightF = 16f;

    // Constructor sin parámetros para el diseñador / instanciación por reflexión.
    public Rpt_Dev_Retenciones_Declaracion() : this(new RetencionesDeclaracionImpresionDto()) { }

    public Rpt_Dev_Retenciones_Declaracion(RetencionesDeclaracionImpresionDto datos)
    {
        datos ??= new RetencionesDeclaracionImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildGroupHeaderTipo());        // Level 1 (externo)
        Bands.Add(BuildGroupHeaderProveedor());   // Level 0 (interno, pegado al Detail)
        Bands.Add(BuildDetail());
        Bands.Add(BuildGroupFooterProveedor());   // Level 0
        Bands.Add(BuildGroupFooterTipo());        // Level 1
        Bands.Add(BuildReportFooter());
        Bands.Add(BuildPie(
            string.IsNullOrWhiteSpace(datos.FiltroTexto) ? datos.Titulo : datos.FiltroTexto,
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    // ── Encabezado del reporte: empresa + título + filtros + fecha (una sola vez) ──
    private ReportHeaderBand BuildReportHeader(RetencionesDeclaracionImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "RETENCIONES APLICADAS — DECLARACIÓN" : datos.Titulo;
        AddLabel(band, titulo, 0f, y, ContentWidth, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        if (!string.IsNullOrWhiteSpace(datos.FiltroTexto))
        {
            AddLabel(band, datos.FiltroTexto, 0f, y, ContentWidth, 13f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
            y += 14f;
        }

        AddLabel(band, $"Generado: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}",
            0f, y, ContentWidth, 13f, 8.5f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 18f;

        AddLine(band, y, 0f, ContentWidth, 1.5f);
        y += 4f;

        band.HeightF = y;
        return band;
    }

    // ── Títulos de columna (se repiten en cada página) ──
    private PageHeaderBand BuildPageHeader()
    {
        var band = new PageHeaderBand { HeightF = 20f };
        AddHeaderCell(band, "Folio", ColFolioX, ColFolioW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Fecha", ColFechaX, ColFechaW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Orden", ColOrdenX, ColOrdenW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Abono", ColAbonoX, ColAbonoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "RTN", ColRtnX, ColRtnW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "%", ColPctX, ColPctW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Base", ColBaseX, ColBaseW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Retenido", ColRetX, ColRetW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Estado", ColEstadoX, ColEstadoW, TextAlignment.MiddleLeft);
        return band;
    }

    // ── Cabecera de grupo TIPO (externo, Level 1); se repite arriba al cruzar de página ──
    private GroupHeaderBand BuildGroupHeaderTipo()
    {
        var band = new GroupHeaderBand { HeightF = 22f, RepeatEveryPage = true, Level = 1 };
        band.GroupFields.Add(new GroupField(nameof(RetencionDeclaracionLineaDto.TipoDisplay)));

        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(0f, 3f, ContentWidth, 16f),
            Font = new DXFont(FontFamily, 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft,
            BackColor = Color.FromArgb(225, 225, 225),
            Borders = BorderSide.None,
            Padding = new PaddingInfo(4, 0, 0, 0, 100f)
        };
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"'Tipo:  ' + [{nameof(RetencionDeclaracionLineaDto.TipoDisplay)}]"));
        band.Controls.Add(lbl);

        return band;
    }

    // ── Cabecera de grupo PROVEEDOR (interno, Level 0) ──
    private GroupHeaderBand BuildGroupHeaderProveedor()
    {
        var band = new GroupHeaderBand { HeightF = 18f, RepeatEveryPage = true, Level = 0 };
        band.GroupFields.Add(new GroupField(nameof(RetencionDeclaracionLineaDto.ProveedorDisplay)));

        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(12f, 1f, ContentWidth - 12f, 15f),
            Font = new DXFont(FontFamily, 9f, DXFontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            TextAlignment = TextAlignment.MiddleLeft,
            BackColor = Color.FromArgb(243, 243, 243),
            Borders = BorderSide.None,
            Padding = new PaddingInfo(4, 0, 0, 0, 100f)
        };
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"'Proveedor:  ' + [{nameof(RetencionDeclaracionLineaDto.ProveedorDisplay)}]"));
        band.Controls.Add(lbl);

        return band;
    }

    // ── Fila de detalle (una retención aplicada) ──
    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.Folio), ColFolioX, ColFolioW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.FechaTexto), ColFechaX, ColFechaW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.NumeroOrden), ColOrdenX, ColOrdenW, TextAlignment.MiddleRight, numeric: false);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.NumeroAbono), ColAbonoX, ColAbonoW, TextAlignment.MiddleRight, numeric: false);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.RtnProveedor), ColRtnX, ColRtnW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.Porcentaje), ColPctX, ColPctW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.BaseLinea), ColBaseX, ColBaseW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.MontoRetenido), ColRetX, ColRetW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(RetencionDeclaracionLineaDto.EstadoDescripcion), ColEstadoX, ColEstadoW, TextAlignment.MiddleLeft, numeric: false);
        return band;
    }

    // ── Subtotal por PROVEEDOR (Level 0) ──
    private GroupFooterBand BuildGroupFooterProveedor()
    {
        var band = new GroupFooterBand { HeightF = 20f, Level = 0 };
        AddLabel(band, "Subtotal proveedor:", ColOrdenX, 3f, ColBaseX - ColOrdenX - 4f, 15f, 8.5f, bold: true, TextAlignment.MiddleRight);
        AddSumLabel(band, nameof(RetencionDeclaracionLineaDto.BaseLinea), ColBaseX, 3f, ColBaseW, SummaryRunning.Group, bold: false);
        AddSumLabel(band, nameof(RetencionDeclaracionLineaDto.MontoRetenido), ColRetX, 3f, ColRetW, SummaryRunning.Group, bold: true);
        return band;
    }

    // ── Subtotal por TIPO (Level 1) ──
    private GroupFooterBand BuildGroupFooterTipo()
    {
        var band = new GroupFooterBand { HeightF = 24f, Level = 1 };
        AddLabel(band, "Subtotal tipo:", ColOrdenX, 5f, ColBaseX - ColOrdenX - 4f, 16f, 9f, bold: true, TextAlignment.MiddleRight);
        AddSumLabel(band, nameof(RetencionDeclaracionLineaDto.BaseLinea), ColBaseX, 5f, ColBaseW, SummaryRunning.Group, bold: true);
        AddSumLabel(band, nameof(RetencionDeclaracionLineaDto.MontoRetenido), ColRetX, 5f, ColRetW, SummaryRunning.Group, bold: true);
        return band;
    }

    // ── Gran total del reporte (= total a declarar cuando el filtro es Vigentes) ──
    private ReportFooterBand BuildReportFooter()
    {
        var band = new ReportFooterBand { HeightF = 30f };
        AddLabel(band, "TOTAL RETENIDO:", ColOrdenX, 8f, ColBaseX - ColOrdenX - 4f, 18f, 10f, bold: true, TextAlignment.MiddleRight);
        AddSumLabel(band, nameof(RetencionDeclaracionLineaDto.BaseLinea), ColBaseX, 8f, ColBaseW, SummaryRunning.Report, bold: true);
        AddSumLabel(band, nameof(RetencionDeclaracionLineaDto.MontoRetenido), ColRetX, 8f, ColRetW, SummaryRunning.Report, bold: true);
        return band;
    }

    // ── Helpers de celda con binding ──
    private static void AddHeaderCell(Band band, string texto, float x, float w, TextAlignment align)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, 2f, w, 16f),
            Text = texto,
            Font = new DXFont(FontFamily, 8.5f, DXFontStyle.Bold),
            TextAlignment = align,
            BackColor = Color.WhiteSmoke,
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.Silver,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        });
    }

    private static void AddDetailCell(Band band, string campo, float x, float w, TextAlignment align, bool numeric)
    {
        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(x, 0f, w, RowHeightF),
            Font = new DXFont(FontFamily, 8.5f),
            TextAlignment = align,
            Multiline = false,
            WordWrap = false,
            CanGrow = false,
            Borders = BorderSide.Bottom,
            BorderWidth = 0.5f,
            BorderColor = Color.Gainsboro,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };

        var expr = numeric ? $"FormatString('{{0:N2}}', [{campo}])" : $"[{campo}]";
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expr));
        band.Controls.Add(lbl);
    }

    // Subtotal/total: el XRLabel necesita a la vez el ExpressionBinding a sumSum([campo]) Y el Summary.
    private static void AddSumLabel(Band band, string campo, float x, float y, float w, SummaryRunning running, bool bold)
    {
        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(x, y, w, 16f),
            Font = new DXFont(FontFamily, bold ? 9f : 8.5f, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = running == SummaryRunning.Report ? 1.5f : 1f,
            BorderColor = Color.Black,
            TextFormatString = "{0:N2}",
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum([{campo}])"));
        lbl.Summary = new XRSummary(running, SummaryFunc.Sum);
        band.Controls.Add(lbl);
    }
}
