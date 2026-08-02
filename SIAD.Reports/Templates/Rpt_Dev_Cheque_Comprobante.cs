using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Utilities;

namespace SIAD.Reports;

/// <summary>
/// Comprobante interno de emision de cheque (formato COMPAGOL de la APP Espanola):
/// documento contable tamano carta con los datos del pago, la distribucion de la
/// partida (cargos/creditos por cuenta) y el bloque de firmas de autorizacion.
/// </summary>
public sealed class Rpt_Dev_Cheque_Comprobante : XtraReport
{
    private const float ContentWidth = 750f;
    private const string FontFamily = "Times New Roman";
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    public Rpt_Dev_Cheque_Comprobante(ChequeImpresionDto datos)
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

        if (datos.Anulado)
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

    private static float BuildDocumento(Band band, ChequeImpresionDto datos)
    {
        var y = BuildEncabezado(band, datos);

        AddLine(band, y, lineWidth: 3f);
        y += 10f;

        AddLabel(band, "SOLICITUD DE EMISIÓN DE CHEQUE", 0f, y, ContentWidth, 20f, 14f,
            bold: true, TextAlignment.MiddleCenter);
        y += 24f;
        AddLine(band, y, lineWidth: 1f);
        y += 12f;

        y = BuildDatosCheque(band, datos, y);
        y = BuildDistribucion(band, datos, y);
        y = BuildFirmas(band, y);

        return y;
    }

    private static float BuildEncabezado(Band band, ChequeImpresionDto datos)
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

        var altoCaja = BuildCajaDocumento(band, datos);

        return Math.Max(Math.Max(yEmpresa, 50f), altoCaja) + 8f;
    }

    private static float BuildCajaDocumento(Band band, ChequeImpresionDto datos)
    {
        var metaLineas = new List<string>
        {
            $"Fecha: {FechaLarga(datos.FechaEmision)}"
        };
        if (!string.IsNullOrWhiteSpace(datos.ComprobanteNumero))
        {
            metaLineas.Add($"Comprobante contable: {datos.ComprobanteNumero.Trim()}");
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
        AddLabel(panel, "COMPROBANTE DE CHEQUE", 0f, yCaja, 230f, 13f, 8.5f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 13f;
        AddLabel(panel, $"No. {FormatNumeroCheque(datos.NumeroCheque)}", 0f, yCaja, 230f, 24f, 16f, bold: true, TextAlignment.MiddleCenter);
        yCaja += 24f;

        foreach (var linea in metaLineas)
        {
            AddLabel(panel, linea, 6f, yCaja, 218f, 13f, 8.5f, align: TextAlignment.MiddleCenter);
            yCaja += 13f;
        }

        yCaja += 4f;
        var estado = new XRLabel
        {
            BoundsF = new RectangleF(12f, yCaja, 206f, 17f),
            Text = datos.Anulado ? "ANULADO" : "EMITIDO",
            Font = new DXFont(FontFamily, 8f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter,
            ForeColor = datos.Anulado ? Color.Firebrick : Color.Black,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        };
        panel.Controls.Add(estado);

        return altoCaja;
    }

    private static float BuildDatosCheque(Band band, ChequeImpresionDto datos, float y)
    {
        // A favor de
        var beneficiario = string.IsNullOrWhiteSpace(datos.Beneficiario) ? "____________________" : datos.Beneficiario.Trim();
        var benefTexto = WrapForWidth(beneficiario, 625f, 11f);
        var altoBenef = 4f + CountLines(benefTexto) * 16f;
        AddLabel(band, "A favor de:", 0f, y, 118f, 15f, 10.5f, bold: true);
        AddLabel(band, benefTexto, 122f, y, 628f, altoBenef, 11f, bold: true, TextAlignment.TopLeft, multiline: true);
        y += altoBenef + 3f;

        // La cantidad de (en letras) + valor en numero a la derecha
        var letras = $"*** {datos.MontoEnLetras} ***";
        var letrasTexto = WrapForWidth(letras, 445f, 10.5f);
        var altoLetras = Math.Max(18f, 4f + CountLines(letrasTexto) * 15f);
        AddLabel(band, "La cantidad de:", 0f, y, 118f, 15f, 10.5f, bold: true);
        AddLabel(band, letrasTexto, 122f, y, 448f, altoLetras, 10.5f, bold: true, TextAlignment.TopLeft, multiline: true, italic: true);
        AddLabel(band, $"*** L {Money(datos.Monto)}", 574f, y, 176f, 18f, 12f, bold: true, TextAlignment.TopRight);
        y += altoLetras + 5f;

        // Pagado con cheque No. | Cuenta de cheques No.
        AddLabel(band, "Pagado con cheque No.:", 0f, y, 150f, 15f, 10.5f, bold: true);
        AddLabel(band, FormatNumeroCheque(datos.NumeroCheque), 154f, y, 200f, 15f, 10.5f);
        AddLabel(band, "Cuenta de cheques No.:", 388f, y, 158f, 15f, 10.5f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(datos.NumeroCuenta) ? "-" : datos.NumeroCuenta.Trim(), 548f, y, 202f, 15f, 10.5f);
        y += 17f;

        // A cargo del banco
        AddLabel(band, "A cargo del banco:", 0f, y, 150f, 15f, 10.5f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(datos.BancoNombre) ? "-" : datos.BancoNombre.Trim(), 154f, y, 596f, 15f, 10.5f);
        y += 17f;

        // Pago por concepto de
        var concepto = string.IsNullOrWhiteSpace(datos.Concepto) ? "-" : datos.Concepto.Trim();
        var conceptoTexto = WrapForWidth(concepto, 596f, 10.5f);
        var altoConcepto = 4f + CountLines(conceptoTexto) * 15f;
        AddLabel(band, "Pago por concepto de:", 0f, y, 150f, 15f, 10.5f, bold: true);
        AddLabel(band, conceptoTexto, 154f, y, 596f, altoConcepto, 10.5f, align: TextAlignment.TopLeft, multiline: true);
        y += altoConcepto + 8f;

        return y;
    }

    private static float BuildDistribucion(Band band, ChequeImpresionDto datos, float y)
    {
        AddLabel(band, "DISTRIBUCIÓN CONTABLE", 0f, y, ContentWidth, 15f, 10f, bold: true);
        y += 18f;

        if (!datos.TieneDistribucion)
        {
            AddLabel(band,
                "Cheque sin partida contable asociada - no hay distribución de cargos y créditos.",
                0f, y, ContentWidth, 15f, 9.5f, color: Color.DimGray, italic: true);
            return y + 24f;
        }

        float[] anchos = [130f, 300f, 70f, 125f, 125f];
        y = AddGridRow(band, y, 18f, anchos,
            [("Código", TextAlignment.MiddleLeft), ("Nombre de la cuenta", TextAlignment.MiddleLeft),
             ("C. Costo", TextAlignment.MiddleCenter), ("Cargos", TextAlignment.MiddleRight), ("Créditos", TextAlignment.MiddleRight)],
            bold: true, header: true);

        foreach (var linea in datos.Distribucion)
        {
            var codigo = AccountCodeFormatter.Format(linea.CodigoCuenta, datos.FormatoCuentas, datos.SeparadorCodigo);
            var nombre = WrapForWidth(linea.NombreCuenta, anchos[1], 9.5f);
            var alto = Math.Max(16f, RowHeight(nombre, string.Empty));

            y = AddGridRow(band, y, alto, anchos,
                [(codigo, TextAlignment.TopLeft),
                 (nombre, TextAlignment.TopLeft),
                 (string.IsNullOrWhiteSpace(linea.CentroCosto) ? "-" : linea.CentroCosto.Trim(), TextAlignment.TopCenter),
                 (linea.Cargo == 0m ? string.Empty : Money(linea.Cargo), TextAlignment.TopRight),
                 (linea.Credito == 0m ? string.Empty : Money(linea.Credito), TextAlignment.TopRight)]);
        }

        var totalCargos = datos.Distribucion.Sum(l => l.Cargo);
        var totalCreditos = datos.Distribucion.Sum(l => l.Credito);
        y = AddGridRow(band, y, 19f, anchos,
            [(string.Empty, TextAlignment.MiddleLeft), ("TOTALES", TextAlignment.MiddleRight),
             (string.Empty, TextAlignment.MiddleCenter),
             (Money(totalCargos), TextAlignment.MiddleRight), (Money(totalCreditos), TextAlignment.MiddleRight)],
            bold: true, total: true);

        return y + 14f;
    }

    private static float BuildFirmas(Band band, float y)
    {
        y += 40f;

        string[] titulos = ["ELABORADO POR", "REVISADO POR", "APROBADO POR", "Vo. Bo."];
        const float anchoColumna = 165f;
        const float paso = 195f;

        for (var i = 0; i < titulos.Length; i++)
        {
            var x = i * paso;
            AddLine(band, y, x, anchoColumna, 1f);
            AddLabel(band, titulos[i], x, y + 4f, anchoColumna, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
        }

        return y + 24f;
    }

    private BottomMarginBand BuildPie(ChequeImpresionDto datos)
    {
        var pie = new BottomMarginBand { HeightF = 50f };

        pie.Controls.Add(new XRLine
        {
            BoundsF = new RectangleF(0f, 4f, ContentWidth, 2f),
            LineStyle = DXDashStyle.Dash,
            LineWidth = 1f,
            ForeColor = Color.LightGray
        });

        AddLabel(pie, $"Cheque No. {FormatNumeroCheque(datos.NumeroCheque)} - SIAD", 0f, 10f, 240f, 12f, 7.5f,
            color: Color.DimGray);
        AddLabel(pie,
            $"Impreso por {datos.ImpresoPor} el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", EsHn)}",
            240f, 10f, 270f, 12f, 7.5f, align: TextAlignment.MiddleCenter, color: Color.DimGray);

        pie.Controls.Add(new XRPageInfo
        {
            BoundsF = new RectangleF(510f, 10f, 240f, 12f),
            PageInfo = PageInfo.NumberOfTotal,
            TextFormatString = "Página {0} de {1}",
            TextAlignment = TextAlignment.MiddleRight,
            Font = new DXFont(FontFamily, 7.5f),
            ForeColor = Color.DimGray,
            Padding = new PaddingInfo(0, 0, 0, 0, 100f)
        });

        return pie;
    }

    private static string BuildLineaLegal(ChequeImpresionDto datos)
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

    // El WordWrap construido por codigo no envuelve de forma fiable, asi que el salto de
    // linea se hace explicito (CRLF: el renderizador ignora "\n" a secas) y la altura se
    // calcula con el.
    private static string WrapForWidth(string? texto, float ancho, float fontSize = 9.5f)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

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

    private static string FormatNumeroCheque(decimal numero) => decimal.Truncate(numero).ToString("000000", EsHn);

    private static string FechaLarga(DateTime fecha)
    {
        var mes = fecha.ToString("MMMM", EsHn);
        if (mes.Length > 0)
        {
            mes = char.ToUpper(mes[0], EsHn) + mes[1..];
        }

        return $"{fecha:dd} de {mes} de {fecha:yyyy}";
    }

    private static string JoinNonEmpty(string separador, params string?[] valores)
        => string.Join(separador, valores.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()));
}
