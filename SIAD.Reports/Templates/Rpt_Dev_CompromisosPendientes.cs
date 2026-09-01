using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Compromisos presupuestarios pendientes": las órdenes de compra aprobadas que todavía
/// retienen presupuesto sin ejecutar, ordenadas por antigüedad. Es la herramienta para depurar
/// órdenes viejas que nadie cerró y que están reteniendo disponible.
/// <para>
/// Mismo reporte para PDF y Excel: el controlador exporta la misma instancia con
/// <c>ExportToPdf</c> o <c>ExportToXlsx</c>.
/// </para>
/// </summary>
public sealed class Rpt_Dev_CompromisosPendientes : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColOcX = 0f, ColOcW = 55f;
    private const float ColFechaX = 55f, ColFechaW = 65f;
    private const float ColProvX = 120f, ColProvW = 165f;
    private const float ColCtaX = 285f, ColCtaW = 110f;
    private const float ColCompX = 395f, ColCompW = 95f;
    private const float ColDevX = 490f, ColDevW = 90f;
    private const float ColSaldoX = 580f, ColSaldoW = 95f;
    private const float ColDiasX = 675f, ColDiasW = 75f;

    private const float RowHeightF = 16f;

    public Rpt_Dev_CompromisosPendientes() : this(new PresupuestoCompromisosImpresionDto()) { }

    public Rpt_Dev_CompromisosPendientes(PresupuestoCompromisosImpresionDto datos)
    {
        datos ??= new PresupuestoCompromisosImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildDetail());
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            "Compromisos presupuestarios pendientes",
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    private ReportHeaderBand BuildReportHeader(PresupuestoCompromisosImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo)
            ? "COMPROMISOS PRESUPUESTARIOS PENDIENTES" : datos.Titulo;
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
        AddHeaderCell(band, "O/C", ColOcX, ColOcW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Fecha", ColFechaX, ColFechaW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Proveedor", ColProvX, ColProvW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Partida", ColCtaX, ColCtaW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Comprometido", ColCompX, ColCompW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Recibido", ColDevX, ColDevW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Saldo retenido", ColSaldoX, ColSaldoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Antigüedad", ColDiasX, ColDiasW, TextAlignment.MiddleRight);
        return band;
    }

    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };

        AddDetailCell(band, nameof(PresupuestoCompromisoPendienteDto.DocumentoNumero), ColOcX, ColOcW, TextAlignment.MiddleLeft, numeric: false);

        var fecha = new XRLabel
        {
            BoundsF = new RectangleF(ColFechaX, 0f, ColFechaW, RowHeightF),
            Font = new DXFont(FontFamily, 8f),
            TextAlignment = TextAlignment.MiddleLeft,
            Multiline = false, WordWrap = false, CanGrow = false,
            Borders = BorderSide.Bottom, BorderWidth = 0.5f, BorderColor = Color.Gainsboro,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        fecha.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"FormatString('{{0:dd/MM/yyyy}}', [{nameof(PresupuestoCompromisoPendienteDto.Fecha)}])"));
        band.Controls.Add(fecha);

        AddDetailCell(band, nameof(PresupuestoCompromisoPendienteDto.Proveedor), ColProvX, ColProvW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(PresupuestoCompromisoPendienteDto.ConCuentaCode), ColCtaX, ColCtaW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(PresupuestoCompromisoPendienteDto.MontoComprometido), ColCompX, ColCompW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(PresupuestoCompromisoPendienteDto.MontoDevengado), ColDevX, ColDevW, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(PresupuestoCompromisoPendienteDto.SaldoComprometido), ColSaldoX, ColSaldoW, TextAlignment.MiddleRight, numeric: true);

        var dias = new XRLabel
        {
            BoundsF = new RectangleF(ColDiasX, 0f, ColDiasW, RowHeightF),
            Font = new DXFont(FontFamily, 8f),
            TextAlignment = TextAlignment.MiddleRight,
            Multiline = false, WordWrap = false, CanGrow = false,
            Borders = BorderSide.Bottom, BorderWidth = 0.5f, BorderColor = Color.Gainsboro,
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        dias.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text",
            $"Concat([{nameof(PresupuestoCompromisoPendienteDto.DiasAntiguedad)}], ' días')"));
        band.Controls.Add(dias);

        return band;
    }

    private ReportFooterBand BuildReportFooter(PresupuestoCompromisosImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 70f };

        AddLabel(band, "TOTALES", ColOcX, 8f, ColCtaX + ColCtaW - 6f, 18f, 9.5f,
            bold: true, TextAlignment.MiddleRight);

        AddTotalCell(band, datos.TotalComprometido, ColCompX, ColCompW);
        AddTotalCell(band, datos.TotalDevengado, ColDevX, ColDevW);
        AddTotalCell(band, datos.TotalSaldo, ColSaldoX, ColSaldoW);

        if (datos.Items.Count == 0)
        {
            AddLabel(band, "No hay compromisos con saldo pendiente: todo lo aprobado ya se recibió o se liberó.",
                0f, 34f, ContentWidth, 14f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        }

        AddLabel(band,
            "El saldo retenido es presupuesto que ya no está disponible para nuevas órdenes. Cancelar o " +
            "cerrar la orden de compra lo libera; recibir la mercadería lo convierte en ejecutado.",
            0f, 44f, ContentWidth, 22f, 7.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);

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
