using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Reports;

/// <summary>
/// Presupuesto (encabezado + cuentas presupuestadas). A diferencia de los documentos de una
/// sola hoja (compromiso, cheque), aqui el numero de cuentas es variable: el detalle va en un
/// DetailBand enlazado a <see cref="PresupuestoImpresionDto.Detalles"/> y los titulos de columna
/// en un PageHeaderBand para que se repitan en cada pagina.
/// </summary>
public sealed class Rpt_Dev_Presupuesto : XtraReport
{
    private const float ContentWidth = 750f;
    private const string FontFamily = "Times New Roman";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    // Anchos de la grilla de detalle; la suma es ContentWidth.
    private const float ColCuenta = 330f;
    private const float ColProyeccion = 115f;
    private const float ColReal = 115f;
    private const float ColDisponible = 115f;
    private const float ColPorcentaje = 75f;

    private const float FilaAlto = 20f;

    public Rpt_Dev_Presupuesto(PresupuestoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        PaperKind = DXPaperKind.Letter;
        PageWidth = 850;
        PageHeight = 1100;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;
        Font = new DXFont(FontFamily, 11f);

        DataSource = datos.Detalles.Count > 0 ? datos.Detalles : null;

        Bands.Add(BuildEncabezado(datos));
        Bands.Add(BuildTitulosColumna(datos));
        Bands.Add(BuildDetalle(datos));
        Bands.Add(BuildTotales(datos));
        Bands.Add(BuildPie(datos));

        if (!datos.EstadoAprobado)
        {
            Watermarks.Add(new XRWatermark
            {
                Id = "MarcaNoAprobado",
                Text = "NO APROBADO",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont(FontFamily, 72f, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 215,
                TextPosition = WatermarkPosition.Behind
            });
        }
    }

    private static ReportHeaderBand BuildEncabezado(PresupuestoImpresionDto datos)
    {
        var band = new ReportHeaderBand();
        var y = BuildEmpresa(band, datos);

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildResumen(band, datos, y);

        band.HeightF = y;
        return band;
    }

    private static float BuildEmpresa(Band band, PresupuestoImpresionDto datos)
    {
        var textoX = 0f;

        if (datos.EmpresaLogo is { Length: > 0 })
        {
            using var stream = new MemoryStream(datos.EmpresaLogo);
            band.Controls.Add(new XRPictureBox
            {
                BoundsF = new RectangleF(0f, 0f, 110f, 46f),
                Sizing = ImageSizeMode.ZoomImage,
                Image = Image.FromStream(stream)
            });
            textoX = 122f;
        }

        var anchoTexto = 508f - textoX;
        var yEmpresa = 0f;

        AddLabel(band, datos.EmpresaNombre, textoX, yEmpresa, anchoTexto, 20f, 14f, bold: true);
        yEmpresa += 21f;

        var lineaLegal = BuildLineaLegal(datos);
        if (!string.IsNullOrWhiteSpace(lineaLegal))
        {
            AddLabel(band, lineaLegal, textoX, yEmpresa, anchoTexto, 13f, 8.5f, color: Color.DimGray);
            yEmpresa += 13f;
        }

        if (!string.IsNullOrWhiteSpace(datos.EmpresaDireccion))
        {
            AddLabel(band, datos.EmpresaDireccion.Trim(), textoX, yEmpresa, anchoTexto, 13f, 8.5f, color: Color.DimGray);
            yEmpresa += 13f;
        }

        var contacto = JoinNonEmpty(" - ",
            string.IsNullOrWhiteSpace(datos.EmpresaTelefono) ? null : $"Tel. {datos.EmpresaTelefono.Trim()}",
            datos.EmpresaEmail);
        if (!string.IsNullOrWhiteSpace(contacto))
        {
            AddLabel(band, contacto, textoX, yEmpresa, anchoTexto, 13f, 8.5f, color: Color.DimGray);
            yEmpresa += 13f;
        }

        var altoCaja = BuildCajaDocumento(band, datos);

        return Math.Max(Math.Max(yEmpresa, 50f), altoCaja) + 10f;
    }

    private static float BuildCajaDocumento(Band band, PresupuestoImpresionDto datos)
    {
        var metaLineas = new List<string>
        {
            $"Del {datos.FechaInicia:dd/MM/yyyy} al {datos.FechaFinaliza:dd/MM/yyyy}",
            $"Periodo: {datos.RangoPeriodo} mes(es)"
        };

        var altoCaja = 6f + 13f + 24f + metaLineas.Count * 13f + 4f + 17f + 7f;
        var panel = new XRPanel
        {
            BoundsF = new RectangleF(520f, 0f, 230f, altoCaja),
            Borders = BorderSide.All,
            BorderWidth = 2f
        };
        band.Controls.Add(panel);

        var yCaja = 6f;
        AddLabel(panel, "PRESUPUESTO", 0f, yCaja, 230f, 13f, 8.5f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 13f;
        AddLabel(panel, $"No. {datos.IdPresupuesto}", 0f, yCaja, 230f, 24f, 16f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 24f;

        foreach (var linea in metaLineas)
        {
            AddLabel(panel, linea, 0f, yCaja, 230f, 13f, 8.5f, align: TextAlignment.MiddleCenter);
            yCaja += 13f;
        }

        yCaja += 4f;
        panel.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(12f, yCaja, 206f, 17f),
            Text = datos.EstadoAprobado ? "APROBADO" : "PENDIENTE DE APROBACION",
            Font = new DXFont(FontFamily, 8f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });

        return altoCaja;
    }

    private static float BuildResumen(Band band, PresupuestoImpresionDto datos, float y)
    {
        AddLabel(band, "RESUMEN DEL PRESUPUESTO", 0f, y, ContentWidth, 15f, 10f, bold: true);
        y += 18f;

        (string Titulo, decimal Valor)[] cajas =
        [
            ("PRESUPUESTO GLOBAL", datos.ValorGlobal),
            ("DISTRIBUIDO EN CUENTAS", datos.TotalProyeccion),
            ("SIN DISTRIBUIR", datos.SinDistribuir),
            ("EJECUTADO", datos.TotalReal),
            ("DISPONIBLE", datos.TotalDisponible)
        ];

        const float anchoCaja = 146f;
        const float paso = 151f;
        const float altoCaja = 38f;

        for (var i = 0; i < cajas.Length; i++)
        {
            var panel = new XRPanel
            {
                BoundsF = new RectangleF(i * paso, y, anchoCaja, altoCaja),
                Borders = BorderSide.All,
                BorderWidth = 1f,
                BorderColor = Color.LightGray
            };
            band.Controls.Add(panel);

            AddLabel(panel, cajas[i].Titulo, 4f, 4f, anchoCaja - 8f, 11f, 7f, bold: true,
                TextAlignment.MiddleCenter, color: Color.DimGray);
            AddLabel(panel, $"L {Money(cajas[i].Valor)}", 4f, 16f, anchoCaja - 8f, 18f, 11f, bold: true,
                TextAlignment.MiddleCenter);
        }

        y += altoCaja + 6f;

        AddLabel(band,
            "Distribuido = suma de la proyeccion de las cuentas. Ejecutado = valor real cargado contra el presupuesto.",
            0f, y, ContentWidth, 12f, 7.5f, color: Color.DimGray, italic: true);

        return y + 18f;
    }

    private static PageHeaderBand BuildTitulosColumna(PresupuestoImpresionDto datos)
    {
        var band = new PageHeaderBand { HeightF = FilaAlto + 2f };

        AddCelda(band, "Cuenta contable", 0f, 0f, ColCuenta, FilaAlto, TextAlignment.MiddleLeft,
            bold: true, header: true);
        AddCelda(band, "Proyeccion", ColCuenta, 0f, ColProyeccion, FilaAlto, TextAlignment.MiddleRight,
            bold: true, header: true);
        AddCelda(band, "Ejecutado", ColCuenta + ColProyeccion, 0f, ColReal, FilaAlto, TextAlignment.MiddleRight,
            bold: true, header: true);
        AddCelda(band, "Disponible", ColCuenta + ColProyeccion + ColReal, 0f, ColDisponible, FilaAlto,
            TextAlignment.MiddleRight, bold: true, header: true);
        AddCelda(band, "% Ejec.", ColCuenta + ColProyeccion + ColReal + ColDisponible, 0f, ColPorcentaje, FilaAlto,
            TextAlignment.MiddleRight, bold: true, header: true);

        // Sin cuentas no hay grilla que encabezar.
        band.Visible = datos.Detalles.Count > 0;
        return band;
    }

    private static DetailBand BuildDetalle(PresupuestoImpresionDto datos)
    {
        var band = new DetailBand { HeightF = FilaAlto, KeepTogether = true };

        if (datos.Detalles.Count == 0)
        {
            band.HeightF = 40f;
            AddLabel(band, "Este presupuesto aun no tiene cuentas de detalle registradas.",
                0f, 10f, ContentWidth, 16f, 10f, align: TextAlignment.MiddleCenter, color: Color.DimGray, italic: true);
            return band;
        }

        // La cuenta puede ocupar dos lineas: crece ella y las demas celdas se estiran con la fila
        // (AnchorVertical.Both anula CanGrow, que es justo lo que se necesita en las columnas fijas).
        var cuenta = AddCelda(band, string.Empty, 0f, 0f, ColCuenta, FilaAlto, TextAlignment.TopLeft);
        cuenta.Multiline = true;
        cuenta.WordWrap = true;
        cuenta.CanGrow = true;
        cuenta.ExpressionBindings.Add(new ExpressionBinding(
            "BeforePrint", "Text", $"[{nameof(PresupuestoImpresionLineaDto.CuentaContable)}]"));

        AddCeldaValor(band, ColCuenta, ColProyeccion, nameof(PresupuestoImpresionLineaDto.ValorProyeccion), "{0:N2}");
        AddCeldaValor(band, ColCuenta + ColProyeccion, ColReal, nameof(PresupuestoImpresionLineaDto.ValorReal), "{0:N2}");
        AddCeldaValor(band, ColCuenta + ColProyeccion + ColReal, ColDisponible,
            nameof(PresupuestoImpresionLineaDto.ValorDisponible), "{0:N2}");
        AddCeldaValor(band, ColCuenta + ColProyeccion + ColReal + ColDisponible, ColPorcentaje,
            nameof(PresupuestoImpresionLineaDto.PorcentajeEjecucion), "{0:N1} %");

        return band;
    }

    private static ReportFooterBand BuildTotales(PresupuestoImpresionDto datos)
    {
        var band = new ReportFooterBand();
        var y = 0f;

        if (datos.Detalles.Count > 0)
        {
            AddCelda(band, "TOTALES", 0f, y, ColCuenta, FilaAlto, TextAlignment.MiddleRight, bold: true, total: true);
            AddCelda(band, Money(datos.TotalProyeccion), ColCuenta, y, ColProyeccion, FilaAlto,
                TextAlignment.MiddleRight, bold: true, total: true);
            AddCelda(band, Money(datos.TotalReal), ColCuenta + ColProyeccion, y, ColReal, FilaAlto,
                TextAlignment.MiddleRight, bold: true, total: true);
            AddCelda(band, Money(datos.TotalDisponible), ColCuenta + ColProyeccion + ColReal, y, ColDisponible,
                FilaAlto, TextAlignment.MiddleRight, bold: true, total: true);
            AddCelda(band, $"{PorcentajeGlobal(datos):N1} %",
                ColCuenta + ColProyeccion + ColReal + ColDisponible, y, ColPorcentaje, FilaAlto,
                TextAlignment.MiddleRight, bold: true, total: true);
            y += FilaAlto + 6f;

            AddLabel(band, $"Cuentas presupuestadas: {datos.Detalles.Count:N0}", 0f, y, 300f, 13f, 8.5f,
                color: Color.DimGray);
            AddLabel(band, $"Sin distribuir: L {Money(datos.SinDistribuir)}", ContentWidth - 300f, y, 300f, 13f, 8.5f,
                align: TextAlignment.MiddleRight, color: Color.DimGray);
            y += 20f;
        }

        y = BuildFirmas(band, datos, y);
        band.HeightF = y;
        return band;
    }

    private static float BuildFirmas(Band band, PresupuestoImpresionDto datos, float y)
    {
        y += 50f;

        string[] titulos = ["ELABORADO POR", "REVISADO POR", "APROBADO POR"];
        const float anchoColumna = 210f;
        const float paso = 270f;

        for (var i = 0; i < titulos.Length; i++)
        {
            var x = i * paso;
            AddLine(band, y, x, anchoColumna, 1f);
            AddLabel(band, titulos[i], x, y + 4f, anchoColumna, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
        }

        AddLabel(band, datos.ImpresoPor, 0f, y + 17f, anchoColumna, 11f, 8f,
            align: TextAlignment.MiddleCenter, color: Color.DimGray);

        return y + 32f;
    }

    private static BottomMarginBand BuildPie(PresupuestoImpresionDto datos)
    {
        var pie = new BottomMarginBand { HeightF = 50f };

        pie.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(0f, 4f, ContentWidth, 2f),
            LineStyle = DXDashStyle.Dash,
            LineWidth = 1f,
            ForeColor = Color.LightGray
        });

        AddLabel(pie, $"Presupuesto {datos.IdPresupuesto} - SIAD", 0f, 10f, 240f, 12f, 7.5f, color: Color.DimGray);
        AddLabel(pie,
            $"Impreso por {datos.ImpresoPor} el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}",
            240f, 10f, 270f, 12f, 7.5f, align: TextAlignment.MiddleCenter, color: Color.DimGray);

        pie.Controls.Add(new XRPageInfo
        {
            BoundsF = new RectangleF(510f, 10f, 240f, 12f),
            PageInfo = PageInfo.NumberOfTotal,
            TextFormatString = "Pagina {0} de {1}",
            TextAlignment = TextAlignment.MiddleRight,
            Font = new DXFont(FontFamily, 7.5f),
            ForeColor = Color.DimGray,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });

        return pie;
    }

    private static decimal PorcentajeGlobal(PresupuestoImpresionDto datos)
        => datos.TotalProyeccion == 0m
            ? 0m
            : Math.Round(datos.TotalReal / datos.TotalProyeccion * 100m, 1, MidpointRounding.AwayFromZero);

    private static void AddCeldaValor(Band band, float x, float ancho, string propiedad, string formato)
    {
        var celda = AddCelda(band, string.Empty, x, 0f, ancho, FilaAlto, TextAlignment.MiddleRight);
        celda.AnchorVertical = VerticalAnchorStyles.Both;
        celda.TextFormatString = formato;
        celda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"[{propiedad}]"));
    }

    // Las filas se dibujan como grillas de XRLabel: el XRTableCell construido por codigo
    // no renderiza texto de varias lineas, mientras que el XRLabel si lo hace.
    private static XRLabel AddCelda(
        Band band,
        string texto,
        float x,
        float y,
        float ancho,
        float alto,
        TextAlignment align,
        bool bold = false,
        bool header = false,
        bool total = false)
    {
        var celda = new XRLabel
        {
            BoundsF = new RectangleF(x, y, ancho, alto),
            Text = texto,
            Font = new DXFont(FontFamily, 9.5f, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = align,
            CanGrow = false,
            ForeColor = Color.Black,
            Borders = total ? BorderSide.Top : BorderSide.All,
            BorderWidth = total ? 1.5f : 0.5f,
            BorderColor = total ? Color.Black : Color.LightGray,
            Padding = new PaddingInfo(4, 4, 2, 2, 100f)
        };

        if (header)
        {
            celda.BackColor = Color.WhiteSmoke;
        }

        band.Controls.Add(celda);
        return celda;
    }

    private static string BuildLineaLegal(PresupuestoImpresionDto datos)
    {
        var razonSocial = datos.EmpresaRazonSocial?.Trim();
        if (string.Equals(razonSocial, datos.EmpresaNombre?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            razonSocial = null;
        }

        return JoinNonEmpty(" - ",
            razonSocial,
            string.IsNullOrWhiteSpace(datos.EmpresaRtn) ? null : $"R.T.N. {datos.EmpresaRtn.Trim()}");
    }

    private static void AddLabel(
        XRControl parent,
        string text,
        float x,
        float y,
        float width,
        float height,
        float fontSize,
        bool bold = false,
        TextAlignment align = TextAlignment.MiddleLeft,
        bool multiline = false,
        Color? color = null,
        bool italic = false)
    {
        var estilo = DXFontStyle.Regular;
        if (bold)
        {
            estilo |= DXFontStyle.Bold;
        }

        if (italic)
        {
            estilo |= DXFontStyle.Italic;
        }

        parent.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, y, width, height),
            Text = text,
            Font = new DXFont(FontFamily, fontSize, estilo),
            TextAlignment = align,
            Multiline = multiline,
            WordWrap = multiline,
            CanGrow = false,
            ForeColor = color ?? Color.Black,
            // Sin esto, los labels dentro de un XRPanel heredan el borde del panel.
            Borders = BorderSide.None,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });
    }

    private static void AddLine(Band band, float y, float x = 0f, float width = ContentWidth, float lineWidth = 1f)
    {
        band.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(x, y, width, lineWidth + 1f),
            LineWidth = lineWidth
        });
    }

    private static string Money(decimal value) => value.ToString("N2", EsHn);

    private static string JoinNonEmpty(string separador, params string?[] valores)
        => string.Join(separador, valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
}
