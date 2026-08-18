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
    // El reporte va APAISADO (Letter horizontal): el ancho útil es 1000 (1100 − 100 de márgenes),
    // no los 750 de vertical. Con once columnas y una tabla de costeo, la horizontal es la única
    // orientación en la que "Descripción" y los importes caben sin truncarse.
    private const float AnchoContenido = 1000f;

    // Libro por bodega — columnas tabulares (x, ancho) que suman AnchoContenido (1000).
    private const float ColFechaX = 0f, ColFechaW = 70f;
    private const float ColDocX = 70f, ColDocW = 46f;
    private const float ColTipoX = 116f, ColTipoW = 74f;
    private const float ColCtxX = 190f, ColCtxW = 95f;
    private const float ColDescX = 285f, ColDescW = 205f;
    private const float ColEntX = 490f, ColEntW = 78f;
    private const float ColSalX = 568f, ColSalW = 78f;
    private const float ColVUX = 646f, ColVUW = 74f;
    private const float ColCostoX = 720f, ColCostoW = 74f;
    private const float ColValorX = 794f, ColValorW = 96f;
    private const float ColSaldoX = 890f, ColSaldoW = 110f;

    // Estado de cuenta por artículo — ocho columnas (x, ancho) que suman AnchoContenido (1000):
    // el concepto va fundido en Descripción, una sola columna de Cantidad con signo, y el costo
    // promedio al final.
    private const float EcFechaX = 0f, EcFechaW = 80f;
    private const float EcDescX = 80f, EcDescW = 300f;
    private const float EcCantX = 380f, EcCantW = 95f;
    private const float EcVUX = 475f, EcVUW = 100f;
    private const float EcValorX = 575f, EcValorW = 115f;
    private const float EcSaldoX = 690f, EcSaldoW = 100f;
    private const float EcSaldoValX = 790f, EcSaldoValW = 110f;
    private const float EcCostoX = 900f, EcCostoW = 100f;

    private const float RowHeightF = 15f;

    public Rpt_Dev_Movimientos_Kardex() : this(new MovimientosKardexImpresionDto()) { }

    public Rpt_Dev_Movimientos_Kardex(MovimientosKardexImpresionDto datos)
    {
        datos ??= new MovimientosKardexImpresionDto();

        // Letter apaisado. Se fijan las dimensiones explícitas (1100×850) además del flag para que
        // la base (que en su constructor puso 850×1100 en vertical) quede sobrescrita sin ambigüedad.
        Landscape = true;
        PageWidth = 1100;
        PageHeight = 850;

        DataSource = datos.Filas;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader(datos));
        Bands.Add(BuildDetail(datos));
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            string.IsNullOrWhiteSpace(datos.Subtitulo) ? datos.Titulo : datos.Subtitulo,
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor,
            AnchoContenido));
    }

    private ReportHeaderBand BuildReportHeader(MovimientosKardexImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "MOVIMIENTOS DE KARDEX" : datos.Titulo;
        AddLabel(band, titulo, 0f, y, AnchoContenido, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        if (!string.IsNullOrWhiteSpace(datos.Subtitulo))
        {
            AddLabel(band, datos.Subtitulo, 0f, y, AnchoContenido, 15f, 10f, bold: true, TextAlignment.MiddleCenter);
            y += 16f;
        }

        if (!string.IsNullOrWhiteSpace(datos.FiltroTexto))
        {
            AddLabel(band, datos.FiltroTexto, 0f, y, AnchoContenido, 13f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
            y += 14f;
        }

        AddLabel(band, $"Generado: {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}",
            0f, y, AnchoContenido, 13f, 8.5f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 18f;

        // Arrastre: con qué cantidad, valor y costo promedio arranca el período. Sin esta línea
        // la primera fila muestra un saldo que el reporte no explica.
        if (datos.TieneArrastre)
        {
            var costo = datos.CostoPromedioAnterior.HasValue
                ? datos.CostoPromedioAnterior.Value.ToString("N4", EsHn)
                : "—";

            AddLabel(band,
                $"Saldo anterior:  cantidad {datos.CantidadAnterior!.Value.ToString("N2", EsHn)}"
                + $"   ·   valor {Money(datos.ValorAnterior ?? 0m)}"
                + $"   ·   costo promedio {costo}",
                0f, y, AnchoContenido, 14f, 9f, bold: true, TextAlignment.MiddleCenter);
            y += 16f;
        }

        AddLine(band, y, 0f, AnchoContenido, 1.5f);
        y += 4f;

        band.HeightF = y;
        return band;
    }

    private PageHeaderBand BuildPageHeader(MovimientosKardexImpresionDto datos)
    {
        var band = new PageHeaderBand { HeightF = 20f };

        if (datos.ModoEstadoCuenta)
        {
            // Nombres completos: en apaisado hay ancho de sobra, no hacen falta abreviaturas.
            AddHeaderCell(band, "Fecha", EcFechaX, EcFechaW, TextAlignment.MiddleLeft);
            AddHeaderCell(band, "Descripción", EcDescX, EcDescW, TextAlignment.MiddleLeft);
            AddHeaderCell(band, "Cantidad", EcCantX, EcCantW, TextAlignment.MiddleRight);
            AddHeaderCell(band, "Valor unitario", EcVUX, EcVUW, TextAlignment.MiddleRight);
            AddHeaderCell(band, "Valor movimiento", EcValorX, EcValorW, TextAlignment.MiddleRight);
            AddHeaderCell(band, "Saldo", EcSaldoX, EcSaldoW, TextAlignment.MiddleRight);
            AddHeaderCell(band, "Saldo valorizado", EcSaldoValX, EcSaldoValW, TextAlignment.MiddleRight);
            AddHeaderCell(band, "Costo promedio", EcCostoX, EcCostoW, TextAlignment.MiddleRight);
            return band;
        }

        AddHeaderCell(band, "Fecha", ColFechaX, ColFechaW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Doc.", ColDocX, ColDocW, TextAlignment.MiddleCenter);
        AddHeaderCell(band, "Tipo", ColTipoX, ColTipoW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, string.IsNullOrWhiteSpace(datos.ColumnaContexto) ? "Contexto" : datos.ColumnaContexto, ColCtxX, ColCtxW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Descripción", ColDescX, ColDescW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Entradas", ColEntX, ColEntW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Salidas", ColSalX, ColSalW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "V. unit.", ColVUX, ColVUW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "C. prom.", ColCostoX, ColCostoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Valor mov.", ColValorX, ColValorW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Saldo", ColSaldoX, ColSaldoW, TextAlignment.MiddleRight);
        return band;
    }

    private DetailBand BuildDetail(MovimientosKardexImpresionDto datos)
    {
        var band = new DetailBand { HeightF = RowHeightF };

        if (datos.ModoEstadoCuenta)
        {
            AddCell(band, "[Fecha]", EcFechaX, EcFechaW, TextAlignment.MiddleLeft);
            AddCell(band, "[Descripcion]", EcDescX, EcDescW, TextAlignment.MiddleLeft);
            // Cantidad con signo: entradas positivas, salidas negativas, "—" cuando es 0.
            AddCell(band, "Iif([CantidadFirmada] = 0, '-', FormatString('{0:N2}', [CantidadFirmada]))", EcCantX, EcCantW, TextAlignment.MiddleRight);
            AddCell(band, "FormatString('{0:N4}', [ValorUnitario])", EcVUX, EcVUW, TextAlignment.MiddleRight);
            AddCell(band, "FormatString('{0:N2}', [ValorMovimiento])", EcValorX, EcValorW, TextAlignment.MiddleRight);
            AddCell(band, "Iif(IsNull([Saldo]), '-', FormatString('{0:N2}', [Saldo]))", EcSaldoX, EcSaldoW, TextAlignment.MiddleRight);
            AddCell(band, "Iif(IsNull([SaldoValorizado]), '-', FormatString('{0:N2}', [SaldoValorizado]))", EcSaldoValX, EcSaldoValW, TextAlignment.MiddleRight);
            AddCell(band, "Iif(IsNull([CostoPromedio]), '-', FormatString('{0:N4}', [CostoPromedio]))", EcCostoX, EcCostoW, TextAlignment.MiddleRight);
            return band;
        }

        AddCell(band, "[Fecha]", ColFechaX, ColFechaW, TextAlignment.MiddleLeft);
        AddCell(band, "[Documento]", ColDocX, ColDocW, TextAlignment.MiddleCenter);
        AddCell(band, "[Tipo]", ColTipoX, ColTipoW, TextAlignment.MiddleLeft);
        AddCell(band, "[Contexto]", ColCtxX, ColCtxW, TextAlignment.MiddleLeft);
        AddCell(band, "[Descripcion]", ColDescX, ColDescW, TextAlignment.MiddleLeft);
        AddCell(band, "Iif([Entradas] > 0, FormatString('{0:N2}', [Entradas]), '')", ColEntX, ColEntW, TextAlignment.MiddleRight);
        AddCell(band, "Iif([Salidas] > 0, FormatString('{0:N2}', [Salidas]), '')", ColSalX, ColSalW, TextAlignment.MiddleRight);
        AddCell(band, "FormatString('{0:N4}', [ValorUnitario])", ColVUX, ColVUW, TextAlignment.MiddleRight);
        AddCell(band, "Iif(IsNull([CostoPromedio]), '-', FormatString('{0:N4}', [CostoPromedio]))", ColCostoX, ColCostoW, TextAlignment.MiddleRight);
        AddCell(band, "FormatString('{0:N2}', [ValorMovimiento])", ColValorX, ColValorW, TextAlignment.MiddleRight);
        AddCell(band, "Iif(IsNull([Saldo]), '-', FormatString('{0:N2}', [Saldo]))", ColSaldoX, ColSaldoW, TextAlignment.MiddleRight);
        return band;
    }

    private ReportFooterBand BuildReportFooter(MovimientosKardexImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 44f };

        if (datos.ModoEstadoCuenta)
        {
            // Una sola columna de cantidad: el total es la cantidad neta (entradas − salidas).
            AddLabel(band, "TOTALES:", EcDescX, 6f, EcCantX - EcDescX - 6f, 16f, 9f, bold: true, TextAlignment.MiddleRight);
            band.Controls.Add(new XRLabel
            {
                BoundsF = new RectangleF(EcCantX, 6f, EcCantW, 16f),
                Text = (datos.TotalEntradas - datos.TotalSalidas).ToString("N2", EsHn),
                Font = new DXFont(FontFamily, 9f, DXFontStyle.Bold),
                TextAlignment = TextAlignment.MiddleRight,
                Borders = BorderSide.Top,
                BorderWidth = 1f,
                BorderColor = Color.Black,
                Padding = new PaddingInfo(3, 3, 0, 0, 100f)
            });
        }
        else
        {
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
        }

        // Cierre valorizado: es el "valores acumulados a la fecha" del kardex legacy. Va como
        // una línea propia y no como celdas bajo las columnas porque el costo promedio final no
        // es la suma de la columna, sino el cociente del acumulado.
        var resumen = $"Valor de entradas {Money(datos.ValorEntradas)}"
            + $"   ·   Valor de salidas {Money(datos.ValorSalidas)}";

        if (datos.MuestraSaldoValorizado)
        {
            var costoFinal = datos.CostoPromedioFinal.HasValue
                ? datos.CostoPromedioFinal.Value.ToString("N4", EsHn)
                : "—";

            resumen += $"   ·   Saldo valorizado {Money(datos.SaldoValorizado)}"
                + $"   ·   Costo promedio {costoFinal}";
        }

        AddLabel(band, resumen, 0f, 26f, AnchoContenido, 14f, 9f, bold: true, TextAlignment.MiddleRight);

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
