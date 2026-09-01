using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Antigüedad de saldos de proveedores": el cuadro de aging de cuentas por pagar, una fila
/// por proveedor con su deuda repartida en seis tramos y el total general al pie. Es un LISTADO que
/// pagina, así que se enlaza por <see cref="XtraReport.DataSource"/> y usa <c>ExpressionBindings</c>
/// —mismo patrón que <see cref="Rpt_Dev_EstadoCuenta_Proveedor"/>—.
/// </summary>
public sealed class Rpt_Dev_AntiguedadSaldos_Proveedor : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColCodX = 0f, ColCodW = 58f;
    private const float ColProvX = 58f, ColProvW = 132f;
    private const float ColPvX = 190f, ColPvW = 80f;
    private const float Col30X = 270f, Col30W = 78f;
    private const float Col60X = 348f, Col60W = 78f;
    private const float Col90X = 426f, Col90W = 78f;
    private const float Col120X = 504f, Col120W = 78f;
    private const float ColMas120X = 582f, ColMas120W = 80f;
    private const float ColTotalX = 662f, ColTotalW = 88f;

    private const float RowHeightF = 16f;

    // Constructor sin parámetros para el diseñador / instanciación por reflexión.
    public Rpt_Dev_AntiguedadSaldos_Proveedor() : this(new AntiguedadSaldosImpresionDto()) { }

    public Rpt_Dev_AntiguedadSaldos_Proveedor(AntiguedadSaldosImpresionDto datos)
    {
        datos ??= new AntiguedadSaldosImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildDetail());
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            "Antigüedad de saldos de proveedores",
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    // ── Encabezado: empresa, título y corte (una sola vez) ──
    private ReportHeaderBand BuildReportHeader(AntiguedadSaldosImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "ANTIGÜEDAD DE SALDOS DE PROVEEDORES" : datos.Titulo;
        AddLabel(band, titulo, 0f, y, ContentWidth, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        AddLabel(band, $"Cuentas por pagar al corte {datos.Corte:dd/MM/yyyy}", 0f, y, ContentWidth, 13f, 9f,
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

    // ── Títulos de columna (se repiten en cada página) ──
    private PageHeaderBand BuildPageHeader()
    {
        var band = new PageHeaderBand { HeightF = 22f };
        AddHeaderCell(band, "Código", ColCodX, ColCodW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Proveedor", ColProvX, ColProvW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Por vencer", ColPvX, ColPvW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "1 – 30", Col30X, Col30W, TextAlignment.MiddleRight);
        AddHeaderCell(band, "31 – 60", Col60X, Col60W, TextAlignment.MiddleRight);
        AddHeaderCell(band, "61 – 90", Col90X, Col90W, TextAlignment.MiddleRight);
        AddHeaderCell(band, "91 – 120", Col120X, Col120W, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Más de 120", ColMas120X, ColMas120W, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Total", ColTotalX, ColTotalW, TextAlignment.MiddleRight);
        return band;
    }

    // ── Fila de detalle (proveedor) ──
    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };

        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.CodProveedor), ColCodX, ColCodW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Nombre), ColProvX, ColProvW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.PorVencer), ColPvX, ColPvW, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo30), Col30X, Col30W, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo60), Col60X, Col60W, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo90), Col90X, Col90W, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo120), Col120X, Col120W, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.TramoMas120), ColMas120X, ColMas120W, TextAlignment.MiddleRight, numeric: true, ocultarCero: true);
        AddDetailCell(band, nameof(AntiguedadSaldosProveedorFilaDto.SaldoTotal), ColTotalX, ColTotalW, TextAlignment.MiddleRight, numeric: true);

        return band;
    }

    // ── Total general por columna + nota de alcance ──
    private ReportFooterBand BuildReportFooter(AntiguedadSaldosImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 82f };

        AddLabel(band, "TOTAL GENERAL", ColCodX, 8f, ColProvX + ColProvW - 6f, 18f, 9.5f,
            bold: true, TextAlignment.MiddleRight);

        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.PorVencer), ColPvX, ColPvW);
        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo30), Col30X, Col30W);
        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo60), Col60X, Col60W);
        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo90), Col90X, Col90W);
        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.Tramo120), Col120X, Col120W);
        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.TramoMas120), ColMas120X, ColMas120W);
        AddTotalCell(band, nameof(AntiguedadSaldosProveedorFilaDto.SaldoTotal), ColTotalX, ColTotalW);

        if (datos.Items.Count == 0)
        {
            AddLabel(band, "Ningún proveedor tiene saldo al corte seleccionado.",
                0f, 34f, ContentWidth, 14f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        }

        // La misma advertencia que lleva la pantalla: sin ella este saldo se reporta como descuadre.
        AddLabel(band,
            "Alcance: reparte por antigüedad los documentos registrados en el portal (facturas de compra y " +
            "compromisos de pago directo) menos sus abonos vigentes. No incluye la cartera histórica migrada, " +
            "que vive en la cuenta contable del proveedor, por lo que este saldo puede diferir del mayor.",
            0f, 46f, ContentWidth, 32f, 7.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);

        return band;
    }

    // ── Helpers de celda con binding ──
    private static void AddHeaderCell(Band band, string texto, float x, float w, TextAlignment align)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, 2f, w, 18f),
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
        Band band, string campo, float x, float w, TextAlignment align, bool numeric, bool ocultarCero = false)
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

        string expr;
        if (!numeric)
        {
            expr = $"[{campo}]";
        }
        else if (ocultarCero)
        {
            // El cuadro es más legible sin un 0.00 en cada celda vacía de tramo.
            expr = $"Iif([{campo}] > 0, FormatString('{{0:N2}}', [{campo}]), '')";
        }
        else
        {
            expr = $"FormatString('{{0:N2}}', [{campo}])";
        }

        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expr));
        band.Controls.Add(lbl);
    }

    private static void AddTotalCell(Band band, string campo, float x, float w)
    {
        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(x, 8f, w, 18f),
            Font = new DXFont(FontFamily, 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Borders = BorderSide.Top,
            BorderWidth = 1.5f,
            BorderColor = Color.Black,
            TextFormatString = "{0:N2}",
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };
        lbl.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum([{campo}])"));
        lbl.Summary = new XRSummary(SummaryRunning.Report, SummaryFunc.Sum);
        band.Controls.Add(lbl);
    }
}
