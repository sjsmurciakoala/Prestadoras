using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using DevExpress.Drawing;
using DevExpress.Drawing.Printing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Cobranza;

namespace SIAD.Reports;

public sealed class Rpt_Dev_Carta_Cobro : XtraReport
{
    private const float ContentWidth = 750f;
    private static readonly CultureInfo EsHn = CultureInfo.GetCultureInfo("es-HN");

    public Rpt_Dev_Carta_Cobro(CartaCobroLoteDto lote)
    {
        PaperKind = DXPaperKind.Letter;
        PageWidth = 850;
        PageHeight = 1100;
        Margins = new DXMargins(50, 50, 50, 50);
        RequestParameters = false;
        Font = new DXFont("Times New Roman", 11f);
        DataSource = BuildPages(lote);

        var detail = new DetailBand
        {
            HeightF = 950f,
            PageBreak = PageBreak.AfterBand
        };
        detail.BeforePrint += DetailBeforePrint;

        Bands.Add(detail);
    }

    private void DetailBeforePrint(object? sender, CancelEventArgs e)
    {
        if (sender is not DetailBand band)
            return;

        band.Controls.Clear();
        if (GetCurrentRow() is not CartaPage page)
        {
            band.HeightF = 0f;
            return;
        }

        band.HeightF = BuildPage(band, page);
    }

    private static List<CartaPage> BuildPages(CartaCobroLoteDto lote)
    {
        var empresaNombre = !string.IsNullOrWhiteSpace(lote.Empresa.RazonSocial)
            ? lote.Empresa.RazonSocial!
            : lote.Empresa.NombreComercial;

        var logoBytes = TryDecodeBase64(lote.Empresa.LogoBase64);
        var plazo = lote.Encabezado.PlazoHoras ?? 24;

        return lote.Clientes
            .Select(cliente => new CartaPage(
                lote.Encabezado.Correlativo,
                empresaNombre,
                lote.Empresa.NombreComercial,
                logoBytes,
                plazo,
                Math.Max(cliente.NumeroRequerimiento, 1),
                cliente,
                BuildServicios(cliente)))
            .ToList();
    }

    private static IReadOnlyList<ServicioSaldo> BuildServicios(CartaCobroClienteDto cliente)
    {
        var movimientos = cliente.Detalle
            .Where(d => d.SaldoDetalle != 0m)
            .ToList();

        var periodoMaximo = movimientos.Count > 0
            ? movimientos.Max(d => PeriodKey(d.Periodo))
            : 0;

        return movimientos
            .GroupBy(d => string.IsNullOrWhiteSpace(d.TipoServicio)
                ? (string.IsNullOrWhiteSpace(d.Descripcion) ? "Servicio" : d.Descripcion)
                : d.TipoServicio!)
            .Select(g => new ServicioSaldo(
                g.Key,
                g.Where(d => PeriodKey(d.Periodo) != periodoMaximo).Sum(d => d.SaldoDetalle),
                g.Where(d => PeriodKey(d.Periodo) == periodoMaximo).Sum(d => d.SaldoDetalle)))
            .OrderBy(g => g.Descripcion)
            .ToList();
    }

    private static float BuildPage(Band band, CartaPage page)
    {
        float y = 0f;

        if (page.LogoBytes is { Length: > 0 })
        {
            using var stream = new MemoryStream(page.LogoBytes);
            band.Controls.Add(new XRPictureBox
            {
                BoundsF = new RectangleF(300f, y, 150f, 60f),
                Sizing = ImageSizeMode.ZoomImage,
                Image = Image.FromStream(stream)
            });
            y += 64f;
        }

        AddLabel(band, page.EmpresaNombre, 0f, y, ContentWidth, 22f, 15f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;
        AddLabel(band, $"REQUERIMIENTO DE PAGO EN MORA  # {page.NumeroRequerimiento}", 0f, y, ContentWidth, 18f, 11f, bold: true, TextAlignment.MiddleCenter);
        y += 22f;
        AddLine(band, y, width: 3f);
        y += 12f;

        AddLabel(band, "Clave :", 0f, y, 70f, 15f, 10f, bold: true);
        AddLabel(band, page.Cliente.Clave, 72f, y, 250f, 15f, 10f);
        AddLabel(band, "Fecha Emision", 510f, y, 110f, 15f, 10f, bold: true, TextAlignment.MiddleRight);
        AddLabel(band, DateTime.Today.ToString("dd/MM/yy", EsHn), 625f, y, 125f, 15f, 10f, align: TextAlignment.MiddleRight);
        y += 16f;

        AddLabel(band, "Propietario :", 0f, y, 85f, 15f, 10f, bold: true);
        AddLabel(band, page.Cliente.Nombre ?? string.Empty, 88f, y, 370f, 15f, 10f);
        AddLabel(band, "No.Identidad :", 510f, y, 110f, 15f, 10f, bold: true, TextAlignment.MiddleRight);
        AddLabel(band, page.Cliente.Identidad ?? string.Empty, 625f, y, 125f, 15f, 10f, align: TextAlignment.MiddleRight);
        y += 16f;

        AddLabel(band, "Direccion :", 0f, y, 80f, 28f, 10f, bold: true, align: TextAlignment.TopLeft);
        AddLabel(band, page.Cliente.Direccion ?? string.Empty, 82f, y, 668f, 28f, 10f, align: TextAlignment.TopLeft, multiline: true);
        y += 32f;

        AddLabel(
            band,
            $"Medidor : {page.Cliente.Medidor ?? string.Empty}     Ciclo : {page.Cliente.CicloId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}     Libreta : {page.Cliente.Libreta ?? string.Empty}     Secuencia : {page.Cliente.Secuencia ?? string.Empty}",
            0f,
            y,
            ContentWidth,
            16f,
            10f);
        y += 30f;

        AddLabel(band, "Estimado Usuario :", 0f, y, ContentWidth, 17f, 11f);
        y += 24f;

        AddLabel(
            band,
            $"Hacemos de su conocimiento que a la fecha tiene una mora Pendiente de LPS. {Money(page.Cliente.SaldoTotal)}",
            0f,
            y,
            ContentWidth,
            18f,
            11f);
        y += 26f;

        y = AddSaldosTable(band, page, y);

        AddLabel(band, $"TOTAL mora:     {Money(page.Cliente.SaldoTotal)}", 470f, y, 280f, 18f, 10f, bold: true, TextAlignment.MiddleRight);
        y += 26f;

        AddLabel(
            band,
            $"Por antes expuesto se le brinda un plazo de {page.PlazoHoras} HORAS para realizar un plan de pago, lo cual debera presentarse a las oficinas de Atencion al Cliente; DEPARTAMENTO DE COBRANZAS.",
            0f,
            y,
            ContentWidth,
            36f,
            10f,
            multiline: true);
        y += 40f;

        AddLabel(
            band,
            $"En caso contrario {page.EmpresaCorta}, da por terminado el plazo que se le concedio y procede a la recuperacion total de su obligacion a traves de la via JUDICIAL, por medio de nuestro apoderado legal.",
            0f,
            y,
            ContentWidth,
            40f,
            10f,
            multiline: true);
        y += 44f;

        AddLabel(
            band,
            "Asi mismo hacemos de su conocimiento que al ser trasladado a esta instancia incurre en un cargo del 25% por concepto de honorarios de abogado por el valor en mora. Esperando una pronta respuesta a este llamado.",
            0f,
            y,
            ContentWidth,
            40f,
            10f,
            multiline: true);
        y += 48f;

        AddLabel(band, "NO SE APRECIA EL VALOR DEL AGUA HASTA QUE SE SECA EL POZO", 0f, y, ContentWidth, 18f, 10f, align: TextAlignment.MiddleCenter);
        y += 30f;

        y = AddFirmas(band, y);

        AddLabel(band, "Unidad de Cobranzas A.P.C.", 0f, y, 220f, 20f, 10f, bold: true);
        AddLine(band, y - 2f, 0f, 210f, 1f);
        y += 28f;

        AddLabel(band, "Observacion:", 0f, y, 85f, 18f, 10f, bold: true);
        AddLine(band, y + 14f, 88f, 662f, 1f);
        y += 30f;

        return Math.Max(y, 940f);
    }

    private static float AddSaldosTable(Band band, CartaPage page, float y)
    {
        float[] widths = [220f, 105f, 75f, 105f, 75f, 170f];
        var table = new XRTable
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, 40f + Math.Max(page.Servicios.Count, 1) * 18f),
            Borders = BorderSide.None,
            BorderWidth = 0.5f,
            Font = new DXFont("Times New Roman", 9.5f)
        };

        table.BeginInit();
        table.Rows.Add(CreateRow(18f,
            CreateCell("DESCRIPCION", widths[0], TextAlignment.MiddleLeft, bold: true),
            CreateCell("SALDOS ANT.", widths[1], TextAlignment.MiddleRight, bold: true),
            CreateCell("RECARGOS", widths[2], TextAlignment.MiddleRight, bold: true),
            CreateCell("MES ACTUAL", widths[3], TextAlignment.MiddleRight, bold: true),
            CreateCell("RECARGOS", widths[4], TextAlignment.MiddleRight, bold: true),
            CreateCell("TOTAL", widths[5], TextAlignment.MiddleRight, bold: true)));

        if (page.Servicios.Count == 0)
        {
            table.Rows.Add(CreateRow(18f,
                CreateCell("Sin saldos pendientes", widths[0], TextAlignment.MiddleLeft),
                CreateCell("0.00", widths[1], TextAlignment.MiddleRight),
                CreateCell("0.00", widths[2], TextAlignment.MiddleRight),
                CreateCell("0.00", widths[3], TextAlignment.MiddleRight),
                CreateCell("0.00", widths[4], TextAlignment.MiddleRight),
                CreateCell("0.00", widths[5], TextAlignment.MiddleRight)));
        }
        else
        {
            foreach (var servicio in page.Servicios)
            {
                table.Rows.Add(CreateRow(18f,
                    CreateCell(servicio.Descripcion, widths[0], TextAlignment.MiddleLeft),
                    CreateCell(Money(servicio.Anterior), widths[1], TextAlignment.MiddleRight),
                    CreateCell("0.00", widths[2], TextAlignment.MiddleRight),
                    CreateCell(Money(servicio.Actual), widths[3], TextAlignment.MiddleRight),
                    CreateCell("0.00", widths[4], TextAlignment.MiddleRight),
                    CreateCell(Money(servicio.Total), widths[5], TextAlignment.MiddleRight)));
            }
        }

        table.EndInit();
        band.Controls.Add(table);

        return y + table.HeightF + 10f;
    }

    private static float AddFirmas(Band band, float y)
    {
        const float x = 430f;
        string[] labels = ["Recibida por:", "Identidad:", "Telefono:", "Fecha Recibido:"];

        foreach (var label in labels)
        {
            AddLabel(band, label, x, y, 105f, 18f, 10f, bold: true, TextAlignment.MiddleRight);
            AddLine(band, y + 14f, x + 115f, 205f, 1f);
            y += 22f;
        }

        return y + 8f;
    }

    private static XRTableRow CreateRow(float height, params XRTableCell[] cells)
    {
        var row = new XRTableRow { HeightF = height };
        row.Cells.AddRange(cells);
        return row;
    }

    private static XRTableCell CreateCell(string text, float width, TextAlignment alignment, bool bold = false)
        => new()
        {
            Text = text,
            WidthF = width,
            TextAlignment = alignment,
            Font = new DXFont("Times New Roman", 9.5f, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            Padding = new PaddingInfo(3, 3, 0, 0, 100f)
        };

    private static void AddLabel(
        Band band,
        string text,
        float x,
        float y,
        float width,
        float height,
        float fontSize,
        bool bold = false,
        TextAlignment align = TextAlignment.MiddleLeft,
        bool multiline = false)
    {
        band.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(x, y, width, height),
            Text = text,
            Font = new DXFont("Times New Roman", fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
            TextAlignment = align,
            Multiline = multiline,
            WordWrap = multiline,
            CanGrow = false,
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

    private static int PeriodKey(string? periodo)
    {
        if (string.IsNullOrWhiteSpace(periodo))
            return 0;

        var parts = periodo.Split('/');
        return parts.Length == 2
               && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
               && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)
            ? year * 100 + month
            : 0;
    }

    private static byte[]? TryDecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private sealed record CartaPage(
        string Correlativo,
        string EmpresaNombre,
        string EmpresaCorta,
        byte[]? LogoBytes,
        int PlazoHoras,
        int NumeroRequerimiento,
        CartaCobroClienteDto Cliente,
        IReadOnlyList<ServicioSaldo> Servicios);

    private sealed record ServicioSaldo(string Descripcion, decimal Anterior, decimal Actual)
    {
        public decimal Total => Anterior + Actual;
    }
}
