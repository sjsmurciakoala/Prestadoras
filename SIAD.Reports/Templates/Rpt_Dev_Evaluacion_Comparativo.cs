using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Evaluación de proveedores — cuadro comparativo": un renglón por proveedor evaluado en
/// el período, con el logro de cada criterio, la calificación y la clase. Es el listado que revisa
/// gerencia para decidir con quién se sigue comprando.
/// <para>
/// Es un LISTADO que pagina, así que se enlaza por <see cref="XtraReport.DataSource"/> con bandas y
/// <c>ExpressionBindings</c>, igual que <see cref="Rpt_Dev_EstadoCuenta_Proveedor"/>. El desglose
/// por criterio va en UNA columna de texto (<c>DesgloseTexto</c>) y no en una columna por criterio:
/// el catálogo es configurable, así que el número de columnas no se conoce al compilar.
/// </para>
/// </summary>
public sealed class Rpt_Dev_Evaluacion_Comparativo : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    // El desglose lleva 310: con seis criterios el texto ronda los 65 caracteres y a 250 se
    // cortaba. El ancho salió de proveedor, compras y clase, que sobraban.
    private const float ColCodigoX = 0f, ColCodigoW = 56f;
    private const float ColProveedorX = 56f, ColProveedorW = 160f;
    private const float ColComprasX = 216f, ColComprasW = 80f;
    private const float ColDesgloseX = 296f, ColDesgloseW = 310f;
    private const float ColCalifX = 606f, ColCalifW = 66f;
    private const float ColClaseX = 672f, ColClaseW = 78f;

    private const float RowHeightF = 18f;

    public Rpt_Dev_Evaluacion_Comparativo() : this(new EvaluacionComparativoImpresionDto()) { }

    public Rpt_Dev_Evaluacion_Comparativo(EvaluacionComparativoImpresionDto datos)
    {
        datos ??= new EvaluacionComparativoImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildDetail());
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            $"Evaluación de proveedores · {datos.PeriodoCodigo}",
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    private ReportHeaderBand BuildReportHeader(EvaluacionComparativoImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        AddLabel(band, "EVALUACIÓN DE PROVEEDORES", 0f, y, ContentWidth, 20f, 13f,
            bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        AddLabel(band,
            $"{datos.PeriodoCodigo} — {datos.PeriodoNombre}  ·  del {datos.FechaDesde:dd/MM/yyyy} al {datos.FechaHasta:dd/MM/yyyy}"
            + (datos.PeriodoCerrado ? "  ·  período cerrado" : string.Empty),
            0f, y, ContentWidth, 13f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 18f;

        AddLabel(band,
            $"{datos.Evaluados:N0} proveedor(es) evaluado(s)"
            + (datos.PromedioPuntaje.HasValue
                ? $"  ·  calificación promedio {datos.PromedioPuntaje.Value:N2}"
                : string.Empty),
            0f, y, ContentWidth, 14f, 9.5f, align: TextAlignment.MiddleCenter);
        y += 18f;

        if (!string.IsNullOrWhiteSpace(datos.FiltroTexto))
        {
            AddLabel(band, datos.FiltroTexto!, 0f, y, ContentWidth, 13f, 8.5f, color: Color.DimGray);
            y += 15f;
        }

        // Leyenda de los criterios: en el cuadro sólo caben abreviados.
        var leyenda = BuildLeyenda(datos);
        if (!string.IsNullOrWhiteSpace(leyenda))
        {
            AddLabel(band, leyenda, 0f, y, ContentWidth, 24f, 8f,
                align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
            y += 26f;
        }

        AddLine(band, y, 0f, ContentWidth, 1.5f);
        band.HeightF = y + 4f;
        return band;
    }

    /// <summary>"Cumpl. = Cumplimiento de entrega (25%) · Compl. = Completitud del pedido (20%)…"</summary>
    private static string BuildLeyenda(EvaluacionComparativoImpresionDto datos)
    {
        if (datos.Criterios.Count == 0) return string.Empty;

        var partes = new System.Collections.Generic.List<string>(datos.Criterios.Count);
        foreach (var c in datos.Criterios)
        {
            var abrev = c.Nombre.Length <= 6 ? c.Nombre : c.Nombre[..5] + ".";
            partes.Add($"{abrev} = {c.Nombre} ({c.Peso:N0}%)");
        }

        return "Criterios:  " + string.Join("   ·   ", partes);
    }

    private PageHeaderBand BuildPageHeader()
    {
        var band = new PageHeaderBand { HeightF = 20f };

        AddHeaderCell(band, "CÓDIGO", ColCodigoX, ColCodigoW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "PROVEEDOR", ColProveedorX, ColProveedorW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "COMPRAS", ColComprasX, ColComprasW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "DESGLOSE POR CRITERIO", ColDesgloseX, ColDesgloseW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "CALIF.", ColCalifX, ColCalifW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "CLASE", ColClaseX, ColClaseW, TextAlignment.MiddleLeft);

        return band;
    }

    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };

        AddDetailCell(band, nameof(EvaluacionRankingItemDto.CodProveedor), ColCodigoX, ColCodigoW,
            TextAlignment.MiddleLeft, numeric: false);
        // 8pt (y no los 8.5 del resto): con 160 de ancho, un nombre largo como
        // "LARACH Y CIA S. DE R.L. DE C.V." no entraba y se cortaba al final.
        AddDetailCell(band, nameof(EvaluacionRankingItemDto.ProveedorNombre), ColProveedorX, ColProveedorW,
            TextAlignment.MiddleLeft, numeric: false, fontSize: 8f);
        AddDetailCell(band, nameof(EvaluacionRankingItemDto.ComprasPeriodo), ColComprasX, ColComprasW,
            TextAlignment.MiddleRight, numeric: true);
        // 7pt: es la celda más densa del cuadro (seis criterios con su valor en una línea).
        // Con 310 de ancho y este tamaño queda margen aunque se agregue un criterio más.
        AddDetailCell(band, nameof(EvaluacionRankingItemDto.DesgloseTexto), ColDesgloseX, ColDesgloseW,
            TextAlignment.MiddleLeft, numeric: false, fontSize: 7f);
        AddDetailCell(band, nameof(EvaluacionRankingItemDto.PuntajeTexto), ColCalifX, ColCalifW,
            TextAlignment.MiddleRight, numeric: false, bold: true);
        AddDetailCell(band, nameof(EvaluacionRankingItemDto.ClaseTexto), ColClaseX, ColClaseW,
            TextAlignment.MiddleLeft, numeric: false);

        return band;
    }

    private ReportFooterBand BuildReportFooter(EvaluacionComparativoImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 120f };

        AddLine(band, 4f, 0f, ContentWidth, 1.5f);

        AddLabel(band, "TOTAL COMPRADO EN EL PERÍODO:", ColProveedorX, 10f,
            ColComprasX - ColProveedorX - 6f, 18f, 9.5f, bold: true, TextAlignment.MiddleRight);

        var total = new XRLabel
        {
            BoundsF = new RectangleF(ColComprasX, 10f, ColComprasW, 18f),
            Font = new DXFont(FontFamily, 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            TextFormatString = "{0:N2}",
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        total.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"sumSum([{nameof(EvaluacionRankingItemDto.ComprasPeriodo)}])"));
        total.Summary = new XRSummary(SummaryRunning.Report, SummaryFunc.Sum);
        band.Controls.Add(total);

        if (datos.Items.Count == 0)
        {
            AddLabel(band, "El período no tiene proveedores evaluados. Ejecute «Recalcular» en la pantalla de evaluación.",
                0f, 34f, ContentWidth, 14f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        }

        // La misma advertencia que lleva la pantalla: sin ella, un criterio sin datos se lee como
        // un cero y el cuadro parece decir que el proveedor falló.
        AddLabel(band,
            "Cómo leerlo: cada criterio se califica de 0 a 100 y la calificación final es el promedio ponderado. "
            + "Un criterio marcado «—» no tuvo datos en el período: no cuenta como cero, su peso se reparte entre "
            + "los demás. Sólo se evalúan proveedores con facturas de compra registradas en el rango.",
            0f, 56f, ContentWidth, 34f, 7.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);

        BuildFirmas(band, 60f, new (string, string?)[]
        {
            ("Jefe de Compras", null),
            ("Gerencia General", null)
        });

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

    private static void AddDetailCell(
        Band band, string campo, float x, float w, TextAlignment align, bool numeric,
        bool bold = false, float fontSize = 8.5f)
    {
        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(x, 0f, w, RowHeightF),
            Font = new DXFont(FontFamily, fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
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
