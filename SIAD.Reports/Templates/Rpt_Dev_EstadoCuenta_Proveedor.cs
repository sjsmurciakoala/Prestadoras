using System;
using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Estado de cuenta de proveedor": identidad del proveedor, resumen con la antigüedad del
/// saldo y el detalle de los documentos por pagar (facturas de compra + compromisos), con su gran
/// total. Es un LISTADO que pagina, así que se enlaza por <see cref="XtraReport.DataSource"/> y usa
/// bandas con <c>ExpressionBindings</c> — mismo patrón que <see cref="Rpt_Dev_Existencias_Bodega"/>.
/// </summary>
public sealed class Rpt_Dev_EstadoCuenta_Proveedor : ComprobanteAlmacenReportBase
{
    // Columnas (x, ancho) — suman ContentWidth (750).
    private const float ColFechaX = 0f, ColFechaW = 68f;
    private const float ColVenceX = 68f, ColVenceW = 68f;
    private const float ColDiasX = 136f, ColDiasW = 42f;
    private const float ColOrigenX = 178f, ColOrigenW = 78f;
    private const float ColDocX = 256f, ColDocW = 122f;
    private const float ColConceptoX = 378f, ColConceptoW = 152f;
    private const float ColMontoX = 530f, ColMontoW = 74f;
    private const float ColAbonadoX = 604f, ColAbonadoW = 70f;
    private const float ColSaldoX = 674f, ColSaldoW = 76f;

    private const float RowHeightF = 17f;

    // Constructor sin parámetros para el diseñador / instanciación por reflexión.
    public Rpt_Dev_EstadoCuenta_Proveedor() : this(new ProveedorEstadoCuentaImpresionDto()) { }

    public Rpt_Dev_EstadoCuenta_Proveedor(ProveedorEstadoCuentaImpresionDto datos)
    {
        datos ??= new ProveedorEstadoCuentaImpresionDto();

        DataSource = datos.Items;

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPageHeader());
        Bands.Add(BuildDetail());
        Bands.Add(BuildReportFooter(datos));
        Bands.Add(BuildPie(
            $"Estado de cuenta · {datos.Codigo}",
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    // ── Encabezado: empresa, título, proveedor, resumen y antigüedad (una sola vez) ──
    private ReportHeaderBand BuildReportHeader(ProveedorEstadoCuentaImpresionDto datos)
    {
        var band = new ReportHeaderBand();

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        var titulo = string.IsNullOrWhiteSpace(datos.Titulo) ? "ESTADO DE CUENTA DE PROVEEDOR" : datos.Titulo;
        AddLabel(band, titulo, 0f, y, ContentWidth, 20f, 13f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        AddLabel(band, $"Corte al {datos.Corte:dd/MM/yyyy}", 0f, y, ContentWidth, 13f, 9f,
            align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 18f;

        y = BuildDatosProveedor(band, datos, y);
        y += 6f;

        y = BuildResumen(band, datos.Resumen, y);
        y += 6f;

        y = BuildAntiguedad(band, datos.Resumen, y);
        y += 6f;

        if (!string.IsNullOrWhiteSpace(datos.FiltroTexto))
        {
            AddLabel(band, datos.FiltroTexto, 0f, y, ContentWidth, 13f, 8.5f,
                align: TextAlignment.MiddleLeft, color: Color.DimGray);
            y += 16f;
        }

        AddLine(band, y, 0f, ContentWidth, 1.5f);
        y += 4f;

        band.HeightF = y;
        return band;
    }

    // Ficha del proveedor en dos columnas dentro de un marco.
    private static float BuildDatosProveedor(Band band, ProveedorEstadoCuentaImpresionDto datos, float y)
    {
        // 58f y no 52f: con tres líneas de 16f la última quedaba pegada al borde inferior.
        const float alto = 58f;

        band.Controls.Add(new XRPanel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, alto),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            BorderColor = Color.Black
        });

        var interiorY = y + 5f;
        const float etiquetaW = 78f;
        const float col2X = 390f;

        AddLabel(band, "Proveedor:", 8f, interiorY, etiquetaW, 14f, 9f, bold: true);
        AddLabel(band, $"{datos.Codigo}  —  {datos.Nombre}", 8f + etiquetaW, interiorY, col2X - etiquetaW - 16f, 14f, 9f);

        AddLabel(band, "RTN:", col2X, interiorY, 40f, 14f, 9f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(datos.Rtn) ? "—" : datos.Rtn, col2X + 42f, interiorY, 180f, 14f, 9f);

        interiorY += 16f;

        AddLabel(band, "Tipo:", 8f, interiorY, etiquetaW, 14f, 9f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(datos.TipoNombre) ? "—" : datos.TipoNombre,
            8f + etiquetaW, interiorY, col2X - etiquetaW - 16f, 14f, 9f);

        AddLabel(band, "Cuenta:", col2X, interiorY, 46f, 14f, 9f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(datos.CuentaContable) ? "—" : datos.CuentaContable,
            col2X + 48f, interiorY, 180f, 14f, 9f);

        interiorY += 16f;

        AddLabel(band, "Documentos:", 8f, interiorY, etiquetaW, 14f, 9f, bold: true);
        AddLabel(band, datos.Resumen.DocumentosPendientes.ToString("N0", EsHn),
            8f + etiquetaW, interiorY, 120f, 14f, 9f);

        if (datos.Resumen.UltimoPagoFecha.HasValue)
        {
            AddLabel(band, "Último pago:", col2X, interiorY, 68f, 14f, 9f, bold: true);
            AddLabel(band,
                $"{Money(datos.Resumen.UltimoPagoMonto ?? 0m)}  ·  {datos.Resumen.UltimoPagoFecha.Value:dd/MM/yyyy}",
                col2X + 70f, interiorY, 200f, 14f, 9f);
        }

        return y + alto;
    }

    // Tres cifras grandes: total, vencido y por vencer.
    private static float BuildResumen(Band band, ProveedorEstadoCuentaResumenDto r, float y)
    {
        const float alto = 40f;
        const float cajaW = ContentWidth / 3f;

        var cajas = new (string Titulo, decimal Valor, Color Color)[]
        {
            ("SALDO TOTAL", r.SaldoTotal, Color.Black),
            ("VENCIDO", r.SaldoVencido, Color.FromArgb(176, 42, 42)),
            ("POR VENCER", r.SaldoPorVencer, Color.FromArgb(0, 96, 60))
        };

        for (var i = 0; i < cajas.Length; i++)
        {
            // Los rótulos van DENTRO del panel (coordenadas relativas): como hermanos del panel
            // quedaban detrás de su fondo opaco y las cajas salían vacías en el PDF.
            var panel = new XRPanel
            {
                BoundsF = new RectangleF(i * cajaW, y, cajaW, alto),
                Borders = BorderSide.All,
                BorderWidth = 0.5f,
                BorderColor = Color.Silver,
                BackColor = Color.FromArgb(246, 246, 246)
            };
            band.Controls.Add(panel);

            AddLabel(panel, cajas[i].Titulo, 0f, 5f, cajaW, 12f, 8f,
                bold: true, TextAlignment.MiddleCenter, color: Color.DimGray);
            AddLabel(panel, Money(cajas[i].Valor), 0f, 18f, cajaW, 18f, 12f,
                bold: true, TextAlignment.MiddleCenter, color: cajas[i].Color);
        }

        return y + alto;
    }

    // Antigüedad en cinco tramos, en una sola fila de celdas.
    private static float BuildAntiguedad(Band band, ProveedorEstadoCuentaResumenDto r, float y)
    {
        const float altoTitulo = 14f;
        const float altoFila = 30f;
        var celdaW = ContentWidth / 5f;

        AddLabel(band, "Antigüedad del saldo (días desde el vencimiento)", 0f, y, ContentWidth, altoTitulo, 8.5f,
            bold: true, color: Color.DimGray);
        y += altoTitulo + 2f;

        var tramos = new (string Titulo, decimal Valor)[]
        {
            ("Corriente", r.AntiguedadCorriente),
            ("1 – 30", r.Antiguedad30),
            ("31 – 60", r.Antiguedad60),
            ("61 – 90", r.Antiguedad90),
            ("Más de 90", r.AntiguedadMas90)
        };

        for (var i = 0; i < tramos.Length; i++)
        {
            var x = i * celdaW;

            band.Controls.Add(new XRPanel
            {
                BoundsF = new RectangleF(x, y, celdaW, altoFila),
                Borders = BorderSide.All,
                BorderWidth = 0.5f,
                BorderColor = Color.Silver
            });

            AddLabel(band, tramos[i].Titulo, x, y + 3f, celdaW, 11f, 7.5f,
                align: TextAlignment.MiddleCenter, color: Color.DimGray);
            AddLabel(band, Money(tramos[i].Valor), x, y + 14f, celdaW, 13f, 9f,
                bold: true, TextAlignment.MiddleCenter);
        }

        return y + altoFila;
    }

    // ── Títulos de columna (se repiten en cada página) ──
    private PageHeaderBand BuildPageHeader()
    {
        var band = new PageHeaderBand { HeightF = 20f };
        AddHeaderCell(band, "Fecha", ColFechaX, ColFechaW, TextAlignment.MiddleCenter);
        AddHeaderCell(band, "Vence", ColVenceX, ColVenceW, TextAlignment.MiddleCenter);
        AddHeaderCell(band, "Días", ColDiasX, ColDiasW, TextAlignment.MiddleCenter);
        AddHeaderCell(band, "Origen", ColOrigenX, ColOrigenW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Documento", ColDocX, ColDocW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Concepto", ColConceptoX, ColConceptoW, TextAlignment.MiddleLeft);
        AddHeaderCell(band, "Monto", ColMontoX, ColMontoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Abonado", ColAbonadoX, ColAbonadoW, TextAlignment.MiddleRight);
        AddHeaderCell(band, "Saldo", ColSaldoX, ColSaldoW, TextAlignment.MiddleRight);
        return band;
    }

    // ── Fila de detalle (documento) ──
    private DetailBand BuildDetail()
    {
        var band = new DetailBand { HeightF = RowHeightF };

        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.FechaTexto), ColFechaX, ColFechaW, TextAlignment.MiddleCenter, numeric: false);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.VencimientoTexto), ColVenceX, ColVenceW, TextAlignment.MiddleCenter, numeric: false);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.DiasTexto), ColDiasX, ColDiasW, TextAlignment.MiddleCenter, numeric: false);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.OrigenDescripcion), ColOrigenX, ColOrigenW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.NumeroDocumento), ColDocX, ColDocW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Concepto), ColConceptoX, ColConceptoW, TextAlignment.MiddleLeft, numeric: false);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Monto), ColMontoX, ColMontoW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Abonado), ColAbonadoX, ColAbonadoW, TextAlignment.MiddleRight, numeric: true);
        AddDetailCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Saldo), ColSaldoX, ColSaldoW, TextAlignment.MiddleRight, numeric: true);

        return band;
    }

    // ── Gran total + nota de alcance ──
    private ReportFooterBand BuildReportFooter(ProveedorEstadoCuentaImpresionDto datos)
    {
        var band = new ReportFooterBand { HeightF = 92f };

        AddLabel(band, "TOTAL A PAGAR:", ColConceptoX, 8f, ColMontoX - ColConceptoX - 6f, 18f, 10f,
            bold: true, TextAlignment.MiddleRight);

        AddTotalCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Monto), ColMontoX, ColMontoW);
        AddTotalCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Abonado), ColAbonadoX, ColAbonadoW);
        AddTotalCell(band, nameof(ProveedorEstadoCuentaDocumentoDto.Saldo), ColSaldoX, ColSaldoW);

        if (datos.Items.Count == 0)
        {
            AddLabel(band, "Este proveedor no tiene documentos pendientes a la fecha de corte.",
                0f, 34f, ContentWidth, 14f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        }

        // La misma advertencia que lleva la pantalla: sin ella este saldo se reporta como un
        // descuadre contable.
        AddLabel(band,
            "Alcance: suma los documentos registrados en el portal (facturas de compra y compromisos de pago " +
            "directo) menos sus abonos vigentes. No incluye la cartera histórica migrada, que vive en la cuenta " +
            "contable del proveedor, por lo que este saldo puede diferir del mayor.",
            0f, 54f, ContentWidth, 32f, 7.5f, align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);

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

    private static void AddTotalCell(Band band, string campo, float x, float w)
    {
        var lbl = new XRLabel
        {
            BoundsF = new RectangleF(x, 8f, w, 18f),
            Font = new DXFont(FontFamily, 9.5f, DXFontStyle.Bold),
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
