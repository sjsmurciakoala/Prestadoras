using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Utilities;

namespace SIAD.Reports;

public sealed class Rpt_Dev_Compromiso_Proveedor : XtraReport
{
    private const float ContentWidth = 750f;
    private const string FontFamily = "Times New Roman";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    public Rpt_Dev_Compromiso_Proveedor(OrdenPagoDirectoImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        PaperKind = DXPaperKind.Letter;
        PageWidth = 850;
        PageHeight = 1100;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;
        Font = new DXFont(FontFamily, 11f);

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie(datos));

        if (datos.Compromiso.Anulada)
        {
            Watermarks.Add(new XRWatermark
            {
                Id = "MarcaAnulado",
                Text = "ANULADO",
                TextDirection = DirectionMode.ForwardDiagonal,
                Font = new DXFont(FontFamily, 90f, DXFontStyle.Bold),
                ForeColor = Color.Firebrick,
                TextTransparency = 190,
                TextPosition = WatermarkPosition.InFront
            });
        }
    }

    private static float BuildDocumento(Band band, OrdenPagoDirectoImpresionDto datos)
    {
        var compromiso = datos.Compromiso;
        var y = BuildEncabezado(band, datos);

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        y = BuildDatosProveedor(band, datos, y);
        y = BuildConceptoYMonto(band, datos, y);
        y = BuildDetalle(band, compromiso, y, datos.FormatoCuentas, datos.SeparadorCodigo);
        y = BuildPartidaContable(band, compromiso, y, datos.FormatoCuentas, datos.SeparadorCodigo);
        y = BuildFirmas(band, datos, y);

        return y;
    }

    private static float BuildEncabezado(Band band, OrdenPagoDirectoImpresionDto datos)
    {
        var compromiso = datos.Compromiso;
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

        var altoCaja = BuildCajaDocumento(band, compromiso);

        return Math.Max(Math.Max(yEmpresa, 50f), altoCaja) + 10f;
    }

    private static float BuildCajaDocumento(Band band, OrdenPagoDirectoDetalleDto compromiso)
    {
        var metaLineas = new List<string>();
        if (compromiso.CorrelativoProveedor.HasValue)
        {
            metaLineas.Add($"Correlativo prov.: {compromiso.CorrelativoProveedor.Value}");
        }

        if (compromiso.FechaCompromiso.HasValue)
        {
            metaLineas.Add($"Fecha: {compromiso.FechaCompromiso.Value.ToString("dd/MM/yyyy", EsHn)}");
        }

        var altoCaja = 6f + 13f + 24f + metaLineas.Count * 13f + 4f + 17f + 7f;
        var panel = new XRPanel
        {
            BoundsF = new RectangleF(520f, 0f, 230f, altoCaja),
            Borders = BorderSide.All,
            BorderWidth = 2f
        };
        band.Controls.Add(panel);

        var yCaja = 6f;
        AddLabel(panel, "COMPROMISO DE PROVEEDOR", 0f, yCaja, 230f, 13f, 8.5f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 13f;
        AddLabel(panel, $"No. {compromiso.NumeroOrden}", 0f, yCaja, 230f, 24f, 16f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 24f;

        foreach (var linea in metaLineas)
        {
            AddLabel(panel, linea, 0f, yCaja, 230f, 13f, 8.5f, align: TextAlignment.MiddleCenter);
            yCaja += 13f;
        }

        yCaja += 4f;
        var estado = new XRLabel
        {
            BoundsF = new RectangleF(12f, yCaja, 206f, 17f),
            Text = GetEstadoTexto(compromiso),
            Font = new DXFont(FontFamily, 8f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        };
        panel.Controls.Add(estado);

        return altoCaja;
    }

    private static float BuildDatosProveedor(Band band, OrdenPagoDirectoImpresionDto datos, float y)
    {
        var compromiso = datos.Compromiso;
        var yIzq = y;
        var yDer = y;

        var proveedorTexto = WrapForWidth(
            string.IsNullOrWhiteSpace(compromiso.CodigoProveedor)
                ? compromiso.Proveedor
                : $"{compromiso.CodigoProveedor.Trim()} - {compromiso.Proveedor}",
            290f,
            10f);
        var altoProveedor = 4f + CountLines(proveedorTexto) * 16f;
        AddLabel(band, "Proveedor:", 0f, yIzq, 78f, 15f, 10f, bold: true);
        AddLabel(band, proveedorTexto, 80f, yIzq, 290f, altoProveedor, 10f, align: TextAlignment.TopLeft, multiline: true);
        yIzq += altoProveedor + 1f;

        var rtn = compromiso.Rtn?.Trim();
        if (!string.IsNullOrWhiteSpace(rtn) || datos.ProveedorGenerico)
        {
            AddLabel(band, "R.T.N.:", 0f, yIzq, 78f, 15f, 10f, bold: true);
            AddLabel(band, string.IsNullOrWhiteSpace(rtn) ? "____________________" : rtn, 80f, yIzq, 290f, 15f, 10f);
            yIzq += 16f;
        }

        if (datos.ProveedorGenerico)
        {
            AddLabel(band, "Proveedor no catalogado - datos capturados manualmente", 0f, yIzq, 370f, 13f, 8f,
                color: Color.DimGray, italic: true);
            yIzq += 14f;
        }

        var pagarA = compromiso.PagarA?.Trim();
        if (!string.IsNullOrWhiteSpace(pagarA) &&
            (datos.ProveedorGenerico || !string.Equals(pagarA, compromiso.Proveedor?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            var pagarATexto = WrapForWidth(pagarA, 290f, 10f);
            var altoPagarA = 4f + CountLines(pagarATexto) * 16f;
            AddLabel(band, "Pagar a:", 390f, yDer, 68f, 15f, 10f, bold: true);
            AddLabel(band, pagarATexto, 460f, yDer, 290f, altoPagarA, 10f, bold: true, TextAlignment.TopLeft, multiline: true);
            yDer += altoPagarA + 1f;
        }

        var cuentaProveedor = FirstNonEmpty(compromiso.CuentaContableProveedor, compromiso.CuentaContable);
        if (!string.IsNullOrWhiteSpace(cuentaProveedor))
        {
            AddLabel(band, "Cuenta contable proveedor:", 390f, yDer, 178f, 15f, 10f, bold: true);
            AddLabel(band, AccountCodeFormatter.Format(cuentaProveedor, datos.FormatoCuentas, datos.SeparadorCodigo), 570f, yDer, 180f, 15f, 10f);
            yDer += 16f;
        }

        return Math.Max(yIzq, yDer) + 4f;
    }

    private static float BuildConceptoYMonto(Band band, OrdenPagoDirectoImpresionDto datos, float y)
    {
        var compromiso = datos.Compromiso;

        var altoConcepto = 4f + EstimateLines(compromiso.Concepto, 676f, 10f) * 16f;
        AddLabel(band, "Concepto:", 0f, y, 72f, 15f, 10f, bold: true);
        AddLabel(band, compromiso.Concepto, 74f, y, 676f, altoConcepto, 10f, align: TextAlignment.TopLeft, multiline: true);
        y += altoConcepto + 6f;

        var panelMonto = new XRPanel
        {
            BoundsF = new RectangleF(510f, y, 240f, 42f),
            Borders = BorderSide.All,
            BorderWidth = 2f
        };
        band.Controls.Add(panelMonto);
        AddLabel(panelMonto, "MONTO DEL COMPROMISO", 8f, 5f, 224f, 11f, 7.5f, bold: true,
            TextAlignment.MiddleRight, color: Color.DimGray);
        AddLabel(panelMonto, $"L {Money(compromiso.Monto)}", 8f, 17f, 224f, 21f, 15f, bold: true, TextAlignment.MiddleRight);
        y += 48f;

        var enLetras = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 20f),
            Text = $"SON: {datos.MontoEnLetras}",
            Font = new DXFont(FontFamily, 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(8, 8, 0, 0, 100f)
        };
        band.Controls.Add(enLetras);

        return y + 28f;
    }

    private static float BuildDetalle(Band band, OrdenPagoDirectoDetalleDto compromiso, float y, string mask, string separator)
    {
        AddLabel(band, "DETALLE DEL COMPROMISO", 0f, y, ContentWidth, 15f, 10f, bold: true);
        y += 18f;

        float[] anchos = [200f, 430f, 120f];
        y = AddGridRow(band, y, 18f, anchos,
            [("Cuenta de gasto", TextAlignment.MiddleLeft), ("Descripcion", TextAlignment.MiddleLeft), ("Monto", TextAlignment.MiddleRight)],
            bold: true, header: true);

        if (compromiso.Detalles.Count == 0)
        {
            y = AddGridRow(band, y, 18f, anchos,
                [("-", TextAlignment.TopLeft), ("Sin lineas de detalle", TextAlignment.TopLeft), (string.Empty, TextAlignment.TopRight)]);
        }
        else
        {
            foreach (var linea in compromiso.Detalles)
            {
                var cuenta = WrapForWidth(
                    AccountCodeFormatter.FormatDisplay(linea.CuentaContable, linea.ObjetoGasto, mask, separator), anchos[0]);
                var descripcion = WrapForWidth(
                    FirstNonEmpty(linea.Descripcion, linea.ConceptoDetalle), anchos[1]);

                y = AddGridRow(band, y, RowHeight(cuenta, descripcion), anchos,
                    [(string.IsNullOrWhiteSpace(cuenta) ? "-" : cuenta, TextAlignment.TopLeft),
                     (descripcion, TextAlignment.TopLeft),
                     (Money(linea.Monto), TextAlignment.TopRight)]);
            }
        }

        y = AddGridRow(band, y, 19f, anchos,
            [(string.Empty, TextAlignment.MiddleLeft), ("Monto total", TextAlignment.MiddleRight), (Money(compromiso.Monto), TextAlignment.MiddleRight)],
            bold: true, total: true);

        return y + 14f;
    }

    private static float BuildPartidaContable(Band band, OrdenPagoDirectoDetalleDto compromiso, float y, string mask, string separator)
    {
        var partida = compromiso.PartidaContable;
        if (partida is null)
        {
            AddLabel(band,
                "Compromiso registrado sin partida contable - se generara antes de procesar.",
                0f, y, ContentWidth, 15f, 9.5f, color: Color.DimGray, italic: true);
            return y + 22f;
        }

        var titulo = $"PARTIDA CONTABLE No. {partida.NumeroPartida}";
        if (partida.FechaPartida != DateTime.MinValue)
        {
            titulo += $"  -  Fecha: {partida.FechaPartida.ToString("dd/MM/yyyy", EsHn)}";
        }

        AddLabel(band, titulo, 0f, y, ContentWidth, 15f, 10f, bold: true);
        y += 16f;

        if (!string.IsNullOrWhiteSpace(partida.Descripcion))
        {
            AddLabel(band, partida.Descripcion.Trim(), 0f, y, ContentWidth, 13f, 8.5f, color: Color.DimGray);
            y += 15f;
        }

        y += 3f;

        float[] anchos = [230f, 120f, 240f, 80f, 80f];
        y = AddGridRow(band, y, 18f, anchos,
            [("Cuenta contable", TextAlignment.MiddleLeft), ("Centro costo", TextAlignment.MiddleLeft),
             ("Descripcion", TextAlignment.MiddleLeft), ("Debito", TextAlignment.MiddleRight), ("Credito", TextAlignment.MiddleRight)],
            bold: true, header: true);

        foreach (var linea in partida.Lineas)
        {
            var cuenta = WrapForWidth(
                AccountCodeFormatter.FormatDisplay(linea.CuentaContable, linea.NombreCuenta, mask, separator), anchos[0]);
            var centroCosto = WrapForWidth(linea.CentroCosto, anchos[1]);
            var descripcion = WrapForWidth(linea.Descripcion, anchos[2]);
            var alto = Math.Max(RowHeight(cuenta, descripcion), RowHeight(centroCosto, string.Empty));

            y = AddGridRow(band, y, alto, anchos,
                [(cuenta, TextAlignment.TopLeft),
                 (centroCosto, TextAlignment.TopLeft),
                 (descripcion, TextAlignment.TopLeft),
                 (linea.Debito == 0m ? string.Empty : Money(linea.Debito), TextAlignment.TopRight),
                 (linea.Credito == 0m ? string.Empty : Money(linea.Credito), TextAlignment.TopRight)]);
        }

        y = AddGridRow(band, y, 19f, anchos,
            [(string.Empty, TextAlignment.MiddleLeft), (string.Empty, TextAlignment.MiddleLeft),
             ("SUMAS IGUALES", TextAlignment.MiddleRight),
             (Money(partida.Lineas.Sum(l => l.Debito)), TextAlignment.MiddleRight),
             (Money(partida.Lineas.Sum(l => l.Credito)), TextAlignment.MiddleRight)],
            bold: true, total: true);

        return y + 14f;
    }

    private static float BuildFirmas(Band band, OrdenPagoDirectoImpresionDto datos, float y)
    {
        y += 46f;

        string[] titulos = ["ELABORADO POR", "REVISADO POR", "AUTORIZADO POR", "RECIBIDO CONFORME"];
        const float anchoColumna = 165f;
        const float paso = 195f;

        for (var i = 0; i < titulos.Length; i++)
        {
            var x = i * paso;
            AddLine(band, y, x, anchoColumna, 1f);
            AddLabel(band, titulos[i], x, y + 4f, anchoColumna, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
        }

        AddLabel(band, datos.ImpresoPor, 0f, y + 17f, anchoColumna, 11f, 8f,
            align: TextAlignment.MiddleCenter, color: Color.DimGray);

        var xRecibido = 3 * paso;
        string[] campos = ["Nombre:", "Identidad / R.T.N.:", "Fecha:"];
        var yCampo = y + 22f;
        foreach (var campo in campos)
        {
            AddLabel(band, campo, xRecibido, yCampo, 82f, 12f, 7.5f, color: Color.DimGray);
            AddLine(band, yCampo + 11f, xRecibido + 84f, anchoColumna - 84f, 0.5f);
            yCampo += 15f;
        }

        return yCampo + 10f;
    }

    private BottomMarginBand BuildPie(OrdenPagoDirectoImpresionDto datos)
    {
        var pie = new BottomMarginBand { HeightF = 50f };

        pie.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(0f, 4f, ContentWidth, 2f),
            LineStyle = DXDashStyle.Dash,
            LineWidth = 1f,
            ForeColor = Color.LightGray
        });

        AddLabel(pie, $"Documento OPD-{datos.Compromiso.NumeroOrden} - SIAD", 0f, 10f, 240f, 12f, 7.5f,
            color: Color.DimGray);
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

    private static string GetEstadoTexto(OrdenPagoDirectoDetalleDto compromiso)
    {
        if (compromiso.Anulada)
        {
            return "ANULADO";
        }

        return compromiso.Procesada
            ? "PROCESADO"
            : "REGISTRADO - PENDIENTE DE PROCESAR";
    }

    private static string BuildLineaLegal(OrdenPagoDirectoImpresionDto datos)
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

    // Las tablas se dibujan como grillas de XRLabel: el XRTableCell construido por codigo
    // no renderiza texto de varias lineas, mientras que el XRLabel si lo hace.
    private static float AddGridRow(
        Band band,
        float y,
        float alto,
        float[] anchos,
        (string Texto, TextAlignment Alineacion)[] celdas,
        bool bold = false,
        bool header = false,
        bool total = false)
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

    private static float RowHeight(string textoWrapA, string textoWrapB)
    {
        var lineas = Math.Max(CountLines(textoWrapA), CountLines(textoWrapB));
        return 8f + lineas * 15f;
    }

    private static int CountLines(string? texto)
        => string.IsNullOrEmpty(texto) ? 1 : texto.Count(c => c == '\n') + 1;

    private static int EstimateLines(string? texto, float ancho, float fontSize = 9.5f)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return 1;
        }

        return CountLines(WrapForWidth(texto, ancho, fontSize));
    }

    // El WordWrap de XRTableCell no envuelve de forma fiable el texto construido por codigo,
    // asi que el salto de linea se hace explicito y la altura de la fila se calcula con el.
    private static string WrapForWidth(string? texto, float ancho, float fontSize = 9.5f)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        // Ancho promedio observado en Times New Roman: ~0.61 unidades por punto de fuente;
        // se usa 0.64 para quebrar un poco antes y que ninguna linea exceda el ancho real.
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

        // El renderizador solo respeta saltos de linea CRLF; con "\n" a secas
        // la segunda linea no se imprime.
        return string.Join("\r\n", lineas);
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

    private static string? FirstNonEmpty(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string JoinNonEmpty(string separador, params string?[] valores)
        => string.Join(separador, valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
}
