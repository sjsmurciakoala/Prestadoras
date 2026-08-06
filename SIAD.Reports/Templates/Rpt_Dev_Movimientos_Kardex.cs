using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Reporte imprimible de movimientos de kardex, tabular y cronológico. Un mismo reporte sirve para:
/// el <b>kardex de un artículo</b> (columna de contexto = Bodega, saldo = saldo corrido) y el
/// <b>libro de movimientos de una bodega</b> (columna de contexto = Artículo, saldo = existencia
/// resultante). El título, el subtítulo, el encabezado de la columna de contexto y los totales
/// vienen en <see cref="MovimientosKardexImpresionDto"/>. Se enlaza por DataSource a las filas y
/// pagina con bandas + ExpressionBindings.
/// </summary>
public sealed class Rpt_Dev_Movimientos_Kardex : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColFechaX = 0f, ColFechaW = 62f;
    private const float ColDocX = 62f, ColDocW = 48f;
    private const float ColTipoX = 110f, ColTipoW = 78f;
    private const float ColCtxX = 188f, ColCtxW = 105f;
    private const float ColDescX = 293f, ColDescW = 170f;
    private const float ColEntX = 463f, ColEntW = 72f;
    private const float ColSalX = 535f, ColSalW = 72f;
    private const float ColVUX = 607f, ColVUW = 68f;
    private const float ColSaldoX = 675f, ColSaldoW = 75f;

    private const float RowHeightF = 15f;

    public Rpt_Dev_Movimientos_Kardex() : this(new MovimientosKardexImpresionDto()) { }

    public Rpt_Dev_Movimientos_Kardex(MovimientosKardexImpresionDto datos)
    {
        datos ??= new MovimientosKardexImpresionDto();

        DataSource = datos.Filas;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader(datos));
        Bands.Add(BuildDetail());
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            string.IsNullOrWhiteSpace(datos.Subtitulo) ? datos.Titulo : datos.Subtitulo,
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    private ReportHeaderBand BuildReportHeader(MovimientosKardexImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "MOVIMIENTOS DE KARDEX" : datos.Titulo;
        AddLabel(band, titulo, 0f, y, ContentWidth, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        if (!string.IsNullOrWhiteSpace(datos.Subtitulo))
        {
            AddLabel(band, datos.Subtitulo, 0f, y, ContentWidth, 15f, 10f, bold: true, TextAlignment.MiddleCenter);
            y += 16f;
        }

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

    private PageHeaderBand BuildPageHeader(MovimientosKardexImpresionDto datos)
    {
        var band = new PageHeaderBand { HeightF = 20f };
        AddHeaderCell(band, "Fecha", ColFechaX, ColFechaW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Doc.", ColDocX, ColDocW, TextAlignment.MiddleCenter);
        AddHeaderCell(band, "Tipo", ColTipoX, ColTipoW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, string.IsNullOrWhiteSpace(datos.ColumnaContexto) ? "Contexto" : datos.ColumnaContexto, ColCtxX, ColCtxW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Descripción", ColDescX, ColDescW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Entradas", ColEntX, ColEntW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Salidas", ColSalX, ColSalW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "V. unit.", ColVUX, ColVUW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Saldo", ColSaldoX, ColSaldoW, TextAlignment.MiddleRight);
        return band;
    }

    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };
        AddCell(band, "[Fecha]", ColFechaX, ColFechaW, TextAlignment.MiddleLeft);
        AddCell(band, "[Documento]", ColDocX, ColDocW, TextAlignment.MiddleCenter);
        AddCell(band, "[Tipo]", ColTipoX, ColTipoW, TextAlignment.MiddleLeft);
        AddCell(band, "[Contexto]", ColCtxX, ColCtxW, TextAlignment.MiddleLeft);
        AddCell(band, "[Descripcion]", ColDescX, ColDescW, TextAlignment.MiddleLeft);
        AddCell(band, "Iif([Entradas] > 0, FormatString('{0:N2}', [Entradas]), '')", ColEntX, ColEntW, TextAlignment.MiddleRight);
        AddCell(band, "Iif([Salidas] > 0, FormatString('{0:N2}', [Salidas]), '')", ColSalX, ColSalW, TextAlignment.MiddleRight);
        AddCell(band, "FormatString('{0:N2}', [ValorUnitario])", ColVUX, ColVUW, TextAlignment.MiddleRight);
        AddCell(band, "Iif(IsNull([Saldo]), '-', FormatString('{0:N2}', [Saldo]))", ColSaldoX, ColSaldoW, TextAlignment.MiddleRight);
        return band;
    }

    private ReportFooterBand BuildReportFooter(MovimientosKardexImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 24f };

        AddLabel(band, "TOTALES:", ColTipoX, 6f, ColEntX - ColTipoX - 6f, 16f, 9f, bold: true, TextAlignment.MiddleRight);

        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(ColEntX, 6f, ColEntW, 16f),
            Text = Money(datos.TotalEntradas),
            Font = new DXFont(FontFamily, 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            BorderColor = Color.Black,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        });

        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(ColSalX, 6f, ColSalW, 16f),
            Text = Money(datos.TotalSalidas),
            Font = new DXFont(FontFamily, 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            BorderColor = Color.Black,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        });

        return band;
    }

    private static void AddHeaderCell(Band band, string texto, float x, float w, TextAlignment align)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, 2f, w, 16f),
            Text = texto,
            Font = new DXFont(FontFamily, 8f, DXFontStyle.Bold),
            TextAlignment = align,
            BackColor = Color.WhiteSmoke,
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.Silver,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        });
    }

    private static void AddCell(Band band, string expr, float x, float w, TextAlignment align)
    {
        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(x, 0f, w, RowHeightF),
            Font = new DXFont(FontFamily, 8f),
            TextAlignment = align,
            Multiline = false,
            WordWrap = false,
            CanGrow = false,
            Borders = BorderSide.Bottom,
            BorderWidth = 0.5f,
            BorderColor = Color.Gainsboro,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expr));
        band.Controls.Add(lbl);
    }
}
