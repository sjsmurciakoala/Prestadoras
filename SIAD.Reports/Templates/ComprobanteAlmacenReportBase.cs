using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Base común de los comprobantes de almacén (requisición y descargo). Reúne el encabezado de la
/// empresa, la caja del documento, el bloque de firmas, el pie de página, la marca de agua y los
/// helpers de dibujo por código. Se toma el patrón del comprobante de compromiso: las "tablas" se
/// dibujan como grillas de <see cref="XRLabel"/> porque un <c>XRTableCell</c> construido por código no
/// renderiza texto multilínea, y los saltos de línea se hacen explícitos (CRLF) calculando la altura.
/// Cada comprobante concreto solo arma su cuerpo.
/// </summary>
public abstract class ComprobanteAlmacenReportBase : XtraReport
{
    protected const float ContentWidth = 750f;
    protected const string FontFamily = "Times New Roman";
    protected static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    protected ComprobanteAlmacenReportBase()
    {
        PaperKind = DXPaperKind.Letter;
        PageWidth = 850;
        PageHeight = 1100;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;
        Font = new DXFont(FontFamily, 11f);
    }

    // ── Encabezado: logo + datos de empresa a la izquierda, caja del documento a la derecha ──
    protected static float BuildEncabezado(
        Band band, ComprobanteAlmacenImpresionBase datos, string tituloCaja, string numero,
        IReadOnlyList<string> metaLineas, string estadoTexto)
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

        var altoCaja = BuildCajaDocumento(band, tituloCaja, numero, metaLineas, estadoTexto);

        return Math.Max(Math.Max(yEmpresa, 50f), altoCaja) + 10f;
    }

    // ── Encabezado de LISTADO: logo + datos de empresa a lo ancho, sin la caja de documento ──
    // (los reportes de listado —existencias, movimientos de kardex— no tienen "No. de documento").
    // Devuelve la Y siguiente para seguir dibujando el título del reporte debajo.
    protected static float BuildEncabezadoEmpresa(Band band, ComprobanteAlmacenImpresionBase datos)
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

        var anchoTexto = ContentWidth - textoX;
        var y = 0f;

        AddLabel(band, datos.EmpresaNombre, textoX, y, anchoTexto, 20f, 14f, bold: true);
        y += 21f;

        var razonSocial = string.Equals(datos.EmpresaRazonSocial?.Trim(), datos.EmpresaNombre?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null : datos.EmpresaRazonSocial;
        var legal = JoinNonEmpty(" - ",
            razonSocial,
            string.IsNullOrWhiteSpace(datos.EmpresaRtn) ? null : $"R.T.N. {datos.EmpresaRtn.Trim()}");
        if (!string.IsNullOrWhiteSpace(legal))
        {
            AddLabel(band, legal, textoX, y, anchoTexto, 13f, 8.5f, color: Color.DimGray);
            y += 13f;
        }

        if (!string.IsNullOrWhiteSpace(datos.EmpresaDireccion))
        {
            AddLabel(band, datos.EmpresaDireccion.Trim(), textoX, y, anchoTexto, 13f, 8.5f, color: Color.DimGray);
            y += 13f;
        }

        var contacto = JoinNonEmpty(" - ",
            string.IsNullOrWhiteSpace(datos.EmpresaTelefono) ? null : $"Tel. {datos.EmpresaTelefono.Trim()}",
            datos.EmpresaEmail);
        if (!string.IsNullOrWhiteSpace(contacto))
        {
            AddLabel(band, contacto, textoX, y, anchoTexto, 13f, 8.5f, color: Color.DimGray);
            y += 13f;
        }

        return Math.Max(y, 50f);
    }

    private static float BuildCajaDocumento(
        Band band, string titulo, string numero, IReadOnlyList<string> metaLineas, string estadoTexto)
    {
        var altoCaja = 6f + 13f + 24f + metaLineas.Count * 13f + 4f + 17f + 7f;
        var panel = new XRPanel
        {
            BoundsF = new RectangleF(520f, 0f, 230f, altoCaja),
            Borders = BorderSide.All,
            BorderWidth = 2f
        };
        band.Controls.Add(panel);

        var yCaja = 6f;
        AddLabel(panel, titulo, 0f, yCaja, 230f, 13f, 8.5f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 13f;
        AddLabel(panel, $"No. {numero}", 0f, yCaja, 230f, 24f, 16f, bold: true, TextAlignment.MiddleCenter);
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
            Text = estadoTexto,
            Font = new DXFont(FontFamily, 8f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });

        return altoCaja;
    }

    // ── Firmas: N columnas con línea, título y (opcional) nombre conocido debajo ──
    protected static float BuildFirmas(Band band, float y, (string Titulo, string? Nombre)[] columnas)
    {
        y += 46f;

        var n = Math.Max(1, columnas.Length);
        var anchoColumna = n >= 3 ? 220f : 240f;
        var paso = n >= 3 ? 250f : 380f;

        for (var i = 0; i < columnas.Length; i++)
        {
            var x = i * paso;
            AddLine(band, y, x, anchoColumna, 1f);
            AddLabel(band, columnas[i].Titulo, x, y + 4f, anchoColumna, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
            if (!string.IsNullOrWhiteSpace(columnas[i].Nombre))
            {
                AddLabel(band, columnas[i].Nombre!, x, y + 17f, anchoColumna, 11f, 8f,
                    align: TextAlignment.MiddleCenter, color: Color.DimGray);
            }
        }

        return y + 34f;
    }

    // ── Pie: identificación del documento, quién imprimió y paginación ──
    protected BottomMarginBand BuildPie(string documentoTexto, string impresoPor)
    {
        var pie = new BottomMarginBand { HeightF = 50f };

        pie.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(0f, 4f, ContentWidth, 2f),
            LineStyle = DXDashStyle.Dash,
            LineWidth = 1f,
            ForeColor = Color.LightGray
        });

        AddLabel(pie, documentoTexto, 0f, 10f, 260f, 12f, 7.5f, color: Color.DimGray);
        AddLabel(pie, $"Impreso por {impresoPor} el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}",
            260f, 10f, 250f, 12f, 7.5f, align: TextAlignment.MiddleCenter, color: Color.DimGray);

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

    protected void AplicarMarcaAgua(string texto)
    {
        Watermarks.Add(new XRWatermark
        {
            Id = "MarcaEstado",
            Text = texto,
            TextDirection = DirectionMode.ForwardDiagonal,
            Font = new DXFont(FontFamily, 90f, DXFontStyle.Bold),
            ForeColor = Color.Firebrick,
            TextTransparency = 190,
            TextPosition = WatermarkPosition.InFront
        });
    }

    private static string BuildLineaLegal(ComprobanteAlmacenImpresionBase datos)
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

    // ── Helpers de dibujo (idénticos al patrón del comprobante de compromiso) ──

    // Las tablas se dibujan como grillas de XRLabel: el XRTableCell construido por código no
    // renderiza texto de varias líneas, mientras que el XRLabel sí lo hace.
    protected static float AddGridRow(
        Band band, float y, float alto, float[] anchos,
        (string Texto, TextAlignment Alineacion)[] celdas,
        bool bold = false, bool header = false, bool total = false)
    {
        var x = 0f;
        for (var i = 0; i < celdas.Length; i++)
        {
            var celda = new XRLabel
            {
                BoundsF = new RectangleF(x, y, anchos[i], alto),
                Text = celdas[i].Texto,
                Font = new DXFont(FontFamily, 9.5f, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
                TextAlignment = celdas[i].Alineacion,
                Multiline = true,
                WordWrap = true,
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
            x += anchos[i];
        }

        return y + alto;
    }

    protected static float RowHeight(string textoWrapA, string textoWrapB)
    {
        var lineas = Math.Max(CountLines(textoWrapA), CountLines(textoWrapB));
        return 8f + lineas * 15f;
    }

    protected static int CountLines(string? texto)
        => string.IsNullOrEmpty(texto) ? 1 : texto.Count(c => c == '\n') + 1;

    protected static int EstimateLines(string? texto, float ancho, float fontSize = 9.5f)
        => string.IsNullOrWhiteSpace(texto) ? 1 : CountLines(WrapForWidth(texto, ancho, fontSize));

    // El WordWrap nativo no envuelve de forma fiable el texto construido por código, así que el
    // salto de línea se hace explícito y la altura de la fila se calcula con él.
    protected static string WrapForWidth(string? texto, float ancho, float fontSize = 9.5f)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        // Ancho promedio observado en Times New Roman: ~0.61 unidades por punto de fuente;
        // se usa 0.64 para quebrar un poco antes y que ninguna línea exceda el ancho real.
        var maxCaracteres = Math.Max(10, (int)((ancho - 10f) / (0.64f * fontSize)));
        var palabras = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lineas = new List<string>();
        var actual = string.Empty;

        foreach (var palabra in palabras)
        {
            var candidata = actual.Length == 0 ? palabra : $"{actual} {palabra}";
            if (candidata.Length > maxCaracteres && actual.Length > 0)
            {
                lineas.Add(actual);
                actual = palabra;
            }
            else
            {
                actual = candidata;
            }
        }

        if (actual.Length > 0)
        {
            lineas.Add(actual);
        }

        // El renderizador solo respeta saltos de línea CRLF; con "\n" a secas la segunda línea
        // no se imprime.
        return string.Join("\r\n", lineas);
    }

    /// <summary>Dibuja una caja enmarcada de ancho completo (p. ej. el total en letras) y devuelve la Y siguiente.</summary>
    protected static float AddBloqueEnmarcado(Band band, float y, string texto, float fontSize = 9.5f)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Text = texto,
            Font = new DXFont(FontFamily, fontSize, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(8, 8, 0, 0, 100f)
        });
        return y + 28f;
    }

    protected static void AddLabel(
        XRControl parent, string text, float x, float y, float width, float height, float fontSize,
        bool bold = false, TextAlignment align = TextAlignment.MiddleLeft, bool multiline = false,
        Color? color = null, bool italic = false)
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

    protected static void AddLine(Band band, float y, float x = 0f, float width = ContentWidth, float lineWidth = 1f)
    {
        band.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(x, y, width, lineWidth + 1f),
            LineWidth = lineWidth
        });
    }

    protected static string Money(decimal value) => value.ToString("N2", EsHn);

    protected static string Cantidad(decimal value) => value.ToString("N2", EsHn);

    protected static string? FirstNonEmpty(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    protected static string JoinNonEmpty(string separador, params string?[] valores)
        => string.Join(separador, valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
}
