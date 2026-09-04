using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Ejecución presupuestaria": una fila por partida con presupuesto, comprometido,
/// ejecutado, pagado y disponible, y los totales al pie. Es un LISTADO que pagina, así que se
/// enlaza por <see cref="XtraReport.DataSource"/> y usa <c>ExpressionBindings</c> — mismo patrón
/// que <see cref="Rpt_Dev_AntiguedadSaldos_Proveedor"/>.
/// <para>
/// El mismo reporte sirve para PDF y para Excel: el controlador llama <c>ExportToPdf</c> o
/// <c>ExportToXlsx</c> sobre la misma instancia (precedente: la antigüedad de saldos de proveedor).
/// </para>
/// </summary>
public sealed class Rpt_Dev_EjecucionPresupuestaria : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColPresX = 0f, ColPresW = 70f;
    private const float ColCtaX = 70f, ColCtaW = 110f;
    private const float ColDescX = 180f, ColDescW = 170f;
    private const float ColMontoX = 350f, ColMontoW = 95f;    // Presupuesto
    private const float ColCompX = 445f, ColCompW = 95f;      // Comprometido
    private const float ColEjecX = 540f, ColEjecW = 90f;      // Ejecutado
    private const float ColDispX = 630f, ColDispW = 90f;      // Disponible
    private const float ColPctX = 720f, ColPctW = 30f;        // % utilizado

    private const float RowHeightF = 16f;

    public Rpt_Dev_EjecucionPresupuestaria() : this(new PresupuestoEjecucionImpresionDto()) { }

    public Rpt_Dev_EjecucionPresupuestaria(PresupuestoEjecucionImpresionDto datos)
    {
        datos ??= new PresupuestoEjecucionImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildDetail());
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            "Ejecución presupuestaria",
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    private ReportHeaderBand BuildReportHeader(PresupuestoEjecucionImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "EJECUCIÓN PRESUPUESTARIA" : datos.Titulo;
        AddLabel(band, titulo, 0f, y, ContentWidth, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        AddLabel(band, $"Al {datos.Corte:dd/MM/yyyy}", 0f, y, ContentWidth, 13f, 9f,
            align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 16f;

        if (!string.IsNullOrWhiteSpace(datos.FiltroTexto))
        {
            AddLabel(band, datos.FiltroTexto, 0f, y, ContentWidth, 13f, 8.5f,
                align: TextAlignment.MiddleCenter, color: Color.DimGray);
            y += 15f;
        }

        y += 3f;
        AddLine(band, y, 0f, ContentWidth, 1.5f);
        y += 4f;

        band.HeightF = y;
        return band;
    }

    private PageHeaderBand BuildPageHeader()
    {
        var band = new PageHeaderBand { HeightF = 22f };
        AddHeaderCell(band, "Presupuesto", ColPresX, ColPresW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Partida", ColCtaX, ColCtaW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Descripción", ColDescX, ColDescW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Presupuesto", ColMontoX, ColMontoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Comprometido", ColCompX, ColCompW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Ejecutado", ColEjecX, ColEjecW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Disponible", ColDispX, ColDispW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "%", ColPctX, ColPctW, TextAlignment.MiddleRight);
        return band;
    }

    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };

        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.IdPresupuesto), ColPresX, ColPresW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.ConCuentaCode), ColCtaX, ColCtaW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.CuentaNombre), ColDescX, ColDescW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.Presupuesto), ColMontoX, ColMontoW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.Comprometido), ColCompX, ColCompW, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.Ejecutado), ColEjecX, ColEjecW, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(PresupuestoEjecucionItemDto.Disponible), ColDispX, ColDispW, TextAlignment.MiddleRight, numeric: true);

        // El porcentaje puede venir nulo (partida sin presupuesto): se imprime vacío, no "0".
        var pct = new XRLabel
        {
            BoundsF = new RectangleF(ColPctX, 0f, ColPctW, RowHeightF),
            Font = new DXFont(FontFamily, 8f),
            TextAlignment = TextAlignment.MiddleRight,
            Multiline = false,
            WordWrap = false,
            CanGrow = false,
            Borders = BorderSide.Bottom,
            BorderWidth = 0.5f,
            BorderColor = Color.Gainsboro,
            Padding = new PaddingInfo(2, 2, 0, 0, 100f)
        };
        pct.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"Iif(IsNull([{nameof(PresupuestoEjecucionItemDto.PctUtilizado)}]), '', " +
            $"FormatString('{{0:N0}}', [{nameof(PresupuestoEjecucionItemDto.PctUtilizado)}]))"));
        band.Controls.Add(pct);

        return band;
    }

    private ReportFooterBand BuildReportFooter(PresupuestoEjecucionImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 74f };

        AddLabel(band, "TOTALES", ColPresX, 8f, ColDescX + ColDescW - 6f, 18f, 9.5f,
            bold: true, TextAlignment.MiddleRight);

        AddTotalCell(band, datos.TotalPresupuesto, ColMontoX, ColMontoW);
        AddTotalCell(band, datos.TotalComprometido, ColCompX, ColCompW);
        AddTotalCell(band, datos.TotalEjecutado, ColEjecX, ColEjecW);
        AddTotalCell(band, datos.TotalDisponible, ColDispX, ColDispW);

        if (datos.Items.Count == 0)
        {
            AddLabel(band, "No hay partidas presupuestarias para los filtros seleccionados.",
                0f, 34f, ContentWidth, 14f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        }

        // La misma aclaración que necesita el contador para leer el cuadro sin confundirse.
        AddLabel(band,
            "Disponible = presupuesto − comprometido − ejecutado. El comprometido corresponde a órdenes de " +
            "compra aprobadas que todavía no se han recibido; devengar una factura mueve el importe de " +
            "comprometido a ejecutado y no altera el disponible. El pagado es informativo y no resta.",
            0f, 44f, ContentWidth, 26f, 7.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);

        return band;
    }

    private static void AddHeaderCell(Band band, string texto, float x, float w, TextAlignment align)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, 2f, w, 18f),
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

    private static void AddDetailCell(
        Band band, string campo, float x, float w, TextAlignment align, bool numeric, bool ocultarCero = false)
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

        string expr;
        if (!numeric)
        {
            expr = $"[{campo}]";
        }
        else if (ocultarCero)
        {
            expr = $"Iif([{campo}] > 0, FormatString('{{0:N2}}', [{campo}]), '')";
        }
        else
        {
            expr = $"FormatString('{{0:N2}}', [{campo}])";
        }

        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expr));
        band.Controls.Add(lbl);
    }

    /// <summary>
    /// Total ya calculado por el servicio. No se usa <c>sumSum</c> porque el disponible y el
    /// porcentaje son derivados: sumarlos por expresión daría cifras distintas a las de la pantalla.
    /// </summary>
    private static void AddTotalCell(Band band, decimal valor, float x, float w)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, 8f, w, 18f),
            Text = valor.ToString("N2", EsHn),
            Font = new DXFont(FontFamily, 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = 1.5f,
            BorderColor = Color.Black,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        });
    }
}
