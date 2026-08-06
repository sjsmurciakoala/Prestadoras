using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Existencias por bodega (valorado)": listado de existencias por artículo agrupado por
/// bodega, con subtotal de valor por bodega y gran total. A diferencia de los comprobantes de
/// almacén (dibujo estático de una transacción), es un LISTADO que pagina: se enlaza por
/// <see cref="XtraReport.DataSource"/> a la lista y usa bandas de grupo/detalle con
/// <c>ExpressionBindings</c>, para que N páginas de filas se rendericen y sumen correctamente.
/// El encabezado de empresa se reutiliza del patrón de los comprobantes.
/// </summary>
public sealed class Rpt_Dev_Existencias_Bodega : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColCodigoX = 0f, ColCodigoW = 85f;
    private const float ColDescX = 85f, ColDescW = 300f;
    private const float ColUnidadX = 385f, ColUnidadW = 55f;
    private const float ColExistX = 440f, ColExistW = 90f;
    private const float ColCostoX = 530f, ColCostoW = 100f;
    private const float ColValorX = 630f, ColValorW = 120f;

    private const float RowHeightF = 17f;

    // Constructor sin parámetros para el diseñador / instanciación por reflexión.
    public Rpt_Dev_Existencias_Bodega() : this(new ExistenciasBodegaImpresionDto()) { }

    public Rpt_Dev_Existencias_Bodega(ExistenciasBodegaImpresionDto datos)
    {
        datos ??= new ExistenciasBodegaImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildGroupHeader());
        Bands.Add(BuildDetail());
        Bands.Add(BuildGroupFooter());
        Bands.Add(BuildReportFooter());
        Bands.Add(BuildPie(
            string.IsNullOrWhiteSpace(datos.FiltroTexto) ? datos.Titulo : datos.FiltroTexto,
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    // ── Encabezado del reporte: empresa + título + filtros + fecha (una sola vez) ──
    private ReportHeaderBand BuildReportHeader(ExistenciasBodegaImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "EXISTENCIAS POR BODEGA (VALORADO)" : datos.Titulo;
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
        AddHeaderCell(band, "Código", ColCodigoX, ColCodigoW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Descripción", ColDescX, ColDescW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Unidad", ColUnidadX, ColUnidadW, TextAlignment.MiddleCenter);
        AddHeaderCell(band, "Existencia", ColExistX, ColExistW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Costo prom.", ColCostoX, ColCostoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Valor", ColValorX, ColValorW, TextAlignment.MiddleRight);
        return band;
    }

    // ── Cabecera de grupo (bodega); se repite arriba si el grupo cruza de página ──
    private GroupHeaderBand BuildGroupHeader()
    {
        var band = new GroupHeaderBand { HeightF = 20f, RepeatEveryPage = true };
        band.GroupFields.Add(new GroupField(nameof(ExistenciaBodegaItemDto.BodegaDisplay)));

        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(0f, 2f, ContentWidth, 16f),
            Font = new DXFont(FontFamily, 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft,
            BackColor = Color.FromArgb(230, 230, 230),
            Borders = BorderSide.None,
            Padding = new PaddingInfo(4, 0, 0, 0, 100f)
        };
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"'Bodega:  ' + [{nameof(ExistenciaBodegaItemDto.BodegaDisplay)}]"));
        band.Controls.Add(lbl);

        return band;
    }

    // ── Fila de detalle (artículo) ──
    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };
        AddDetailCell(band, nameof(ExistenciaBodegaItemDto.ArticuloCodigo), ColCodigoX, ColCodigoW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(ExistenciaBodegaItemDto.ArticuloDescripcion), ColDescX, ColDescW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(ExistenciaBodegaItemDto.UnidadMedida), ColUnidadX, ColUnidadW, TextAlignment.MiddleCenter, numeric: false);
        AddDetailCell(band, nameof(ExistenciaBodegaItemDto.Existencia), ColExistX, ColExistW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(ExistenciaBodegaItemDto.CostoPromedio), ColCostoX, ColCostoW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(ExistenciaBodegaItemDto.Valor), ColValorX, ColValorW, TextAlignment.MiddleRight, numeric: true);
        return band;
    }

    // ── Subtotal por bodega ──
    private GroupFooterBand BuildGroupFooter()
    {
        var band = new GroupFooterBand { HeightF = 22f };

        AddLabel(band, "Subtotal bodega:", ColExistX, 4f, ColValorX - ColExistX - 6f, 16f, 9f, bold: true, TextAlignment.MiddleRight);

        var lblSum = new XRLabel
        {
            BoundsF = new RectangleF(ColValorX, 4f, ColValorW, 16f),
            Font = new DXFont(FontFamily, 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            BorderColor = Color.Black,
            TextFormatString = "{0:N2}",
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        lblSum.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum([{nameof(ExistenciaBodegaItemDto.Valor)}])"));
        lblSum.Summary = new XRSummary(SummaryRunning.Group, SummaryFunc.Sum);
        band.Controls.Add(lblSum);

        return band;
    }

    // ── Gran total del reporte ──
    private ReportFooterBand BuildReportFooter()
    {
        var band = new ReportFooterBand { HeightF = 28f };

        AddLabel(band, "TOTAL GENERAL:", ColExistX, 8f, ColValorX - ColExistX - 6f, 18f, 10f, bold: true, TextAlignment.MiddleRight);

        var lblTotal = new XRLabel
        {
            BoundsF = new RectangleF(ColValorX, 8f, ColValorW, 18f),
            Font = new DXFont(FontFamily, 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = 1.5f,
            BorderColor = Color.Black,
            TextFormatString = "{0:N2}",
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        lblTotal.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum([{nameof(ExistenciaBodegaItemDto.Valor)}])"));
        lblTotal.Summary = new XRSummary(SummaryRunning.Report, SummaryFunc.Sum);
        band.Controls.Add(lblTotal);

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
}
