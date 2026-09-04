using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Reports;

/// <summary>
/// Partida contable (asiento de doble entrada) generada por un movimiento de almacén. Reúne el
/// encabezado de la empresa, el número/fecha de la póliza y la referencia al movimiento origen, y
/// dibuja las líneas Cuenta / Descripción / Debe / Haber con su fila de totales. Si la partida está
/// revertida (movimiento anulado), se imprime con la marca de agua "ANULADA".
/// </summary>
public sealed class Rpt_Dev_Partida_Contable : ComprobanteAlmacenReportBase
{
    public Rpt_Dev_Partida_Contable(PartidaContableImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie($"Partida {datos.Numero} - {datos.DocumentoReferencia} - SIAD", datos.ImpresoPor));

        if (datos.Anulada)
        {
            AplicarMarcaAgua("ANULADA");
        }
    }

    private float BuildDocumento(Band band, PartidaContableImpresionDto datos)
    {
        var meta = new List<string> { $"Fecha: {datos.Fecha.ToString("dd/MM/yyyy", EsHn)}" };
        if (!string.IsNullOrWhiteSpace(datos.DocumentoReferencia))
        {
            meta.Add(datos.DocumentoReferencia);
        }

        var y = BuildEncabezado(band, datos, "PARTIDA CONTABLE", datos.Numero, meta, datos.EstadoTexto);

        AddLine(band, y, lineWidth: 3f);
        y += 14f;

        if (!string.IsNullOrWhiteSpace(datos.Descripcion))
        {
            var texto = WrapForWidth(datos.Descripcion, 678f, 10f);
            var alto = 4f + CountLines(texto) * 15f;
            AddLabel(band, "Concepto:", 0f, y, 70f, 15f, 10f, bold: true);
            AddLabel(band, texto, 72f, y, 678f, alto, 10f, align: TextAlignment.TopLeft, multiline: true);
            y += alto + 6f;
        }

        y = BuildTabla(band, datos, y);

        if (datos.TotalDebe > 0m)
        {
            y = AddBloqueEnmarcado(band, y, $"TOTAL: L {Money(datos.TotalDebe)}   -   SON: {datos.TotalEnLetras} LEMPIRAS");
        }

        y = BuildFirmas(band, y,
        [
            ("ELABORADO POR", datos.ImpresoPor),
            ("REVISADO POR", null),
            ("AUTORIZADO POR", null)
        ]);

        return y;
    }

    private static float BuildTabla(Band band, PartidaContableImpresionDto datos, float y)
    {
        float[] anchos = [110f, 360f, 140f, 140f];

        y = AddGridRow(band, y, 18f, anchos,
        [
            ("Cuenta", TextAlignment.MiddleLeft),
            ("Descripcion", TextAlignment.MiddleLeft),
            ("Debe", TextAlignment.MiddleRight),
            ("Haber", TextAlignment.MiddleRight)
        ], bold: true, header: true);

        if (datos.Lineas.Count == 0)
        {
            y = AddGridRow(band, y, 18f, anchos,
            [
                ("-", TextAlignment.TopLeft), ("Sin lineas", TextAlignment.TopLeft),
                (string.Empty, TextAlignment.TopRight), (string.Empty, TextAlignment.TopRight)
            ]);
        }
        else
        {
            foreach (var l in datos.Lineas)
            {
                var nombre = WrapForWidth(FirstNonEmpty(l.CuentaNombre, l.Descripcion) ?? "-", anchos[1]);
                y = AddGridRow(band, y, RowHeight(nombre, string.Empty), anchos,
                [
                    (l.CuentaCodigo, TextAlignment.TopLeft),
                    (nombre, TextAlignment.TopLeft),
                    (l.Debe > 0m ? Money(l.Debe) : string.Empty, TextAlignment.TopRight),
                    (l.Haber > 0m ? Money(l.Haber) : string.Empty, TextAlignment.TopRight)
                ]);
            }
        }

        y = AddGridRow(band, y, 19f, anchos,
        [
            (string.Empty, TextAlignment.MiddleLeft), ("TOTALES", TextAlignment.MiddleRight),
            (Money(datos.TotalDebe), TextAlignment.MiddleRight),
            (Money(datos.TotalHaber), TextAlignment.MiddleRight)
        ], bold: true, total: true);

        return y + 12f;
    }
}
