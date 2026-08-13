using System;
using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Drawing;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Retenciones;

namespace SIAD.Reports;

/// <summary>
/// Constancia de retención (F5): documento que el agente retenedor entrega al proveedor por la suma
/// retenida. Dibujo "por código" (patrón <c>Rpt_Dev_Comprobante_Abono</c>): una <see cref="DetailBand"/>
/// cuya altura devuelve <c>BuildDocumento</c>, todo con <see cref="XRLabel"/>/<see cref="XRPanel"/>/
/// <see cref="XRLine"/> y la grilla de líneas como filas de <c>XRLabel</c> (reusa
/// <see cref="ComprobanteAlmacenReportBase"/>). Marca de agua "ANULADA" si el hdr está anulado.
///
/// F5b (CAI): hoy se numera con el FOLIO INTERNO. Si se confirma D1 (constancia formal con CAI del
/// Acuerdo 481-2017), <see cref="ConstanciaRetencionImpresionDto.CaiCorrelativo"/> / CaiLeyenda se
/// poblarían y el bloque bajo el folio imprimiría el correlativo de 16 dígitos + la leyenda. Mientras
/// esos campos sean NULL, nada de numeración autorizada se imprime.
/// </summary>
public sealed class Rpt_Dev_Constancia_Retencion : ComprobanteAlmacenReportBase
{
    // Columnas de la grilla de líneas (suman ContentWidth = 750).
    private static readonly float[] ColAnchos = [90f, 330f, 70f, 130f, 130f];

    public Rpt_Dev_Constancia_Retencion(ConstanciaRetencionImpresionDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var detail = new DetailBand();
        Bands.Add(detail);
        detail.HeightF = BuildDocumento(detail, datos);

        Bands.Add(BuildPie(
            $"Constancia de retención folio {datos.Folio} — OPD-{datos.NumeroOrden}/abono {datos.NumeroAbono}",
            datos.ImpresoPor));

        if (datos.Anulada)
        {
            AplicarMarcaAgua("ANULADA");
        }
    }

    private static float BuildDocumento(Band band, ConstanciaRetencionImpresionDto d)
    {
        var y = 0f;

        // ── Encabezado: empresa (izquierda) + caja de la constancia (derecha) ──
        var textoX = 0f;
        if (d.EmpresaLogo is { Length: > 0 })
        {
            using var stream = new MemoryStream(d.EmpresaLogo);
            band.Controls.Add(new XRPictureBox
            {
                BoundsF = new RectangleF(0f, 0f, 110f, 46f),
                Sizing = ImageSizeMode.ZoomImage,
                Image = Image.FromStream(stream)
            });
            textoX = 122f;
        }

        var anchoEmpresa = 500f - textoX;
        var yEmp = 0f;

        AddLabel(band, d.EmpresaNombre, textoX, yEmp, anchoEmpresa, 20f, 14f, bold: true);
        yEmp += 21f;

        var razonSocial = string.Equals(d.EmpresaRazonSocial?.Trim(), d.EmpresaNombre?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null : d.EmpresaRazonSocial;
        var legal = JoinNonEmpty(" - ",
            razonSocial,
            string.IsNullOrWhiteSpace(d.EmpresaRtn) ? null : $"R.T.N. {d.EmpresaRtn!.Trim()}");
        if (!string.IsNullOrWhiteSpace(legal))
        {
            AddLabel(band, legal, textoX, yEmp, anchoEmpresa, 13f, 8.5f, color: Color.DimGray);
            yEmp += 13f;
        }

        if (!string.IsNullOrWhiteSpace(d.EmpresaDireccion))
        {
            AddLabel(band, d.EmpresaDireccion!.Trim(), textoX, yEmp, anchoEmpresa, 13f, 8.5f, color: Color.DimGray);
            yEmp += 13f;
        }

        var contacto = JoinNonEmpty(" - ",
            string.IsNullOrWhiteSpace(d.EmpresaTelefono) ? null : $"Tel. {d.EmpresaTelefono!.Trim()}",
            d.EmpresaEmail);
        if (!string.IsNullOrWhiteSpace(contacto))
        {
            AddLabel(band, contacto, textoX, yEmp, anchoEmpresa, 13f, 8.5f, color: Color.DimGray);
            yEmp += 13f;
        }

        // Caja de la constancia (derecha): título + folio interno + fecha.
        var caja = new XRPanel { BoundsF = new RectangleF(520f, 0f, 230f, 72f), Borders = BorderSide.All, BorderWidth = 2f };
        band.Controls.Add(caja);
        AddLabel(caja, "CONSTANCIA DE RETENCIÓN", 0f, 6f, 230f, 14f, 9f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(caja, $"Folio No. {d.Folio}", 0f, 24f, 230f, 22f, 14f, bold: true, TextAlignment.MiddleCenter);
        AddLabel(caja, $"Fecha: {d.FechaEmision.ToString("dd/MM/yyyy", EsHn)}", 0f, 50f, 230f, 14f, 8.5f, align: TextAlignment.MiddleCenter);

        // Hook CAI (F5b): sólo se dibuja si el correlativo autorizado ya está poblado (hoy NULL).
        if (!string.IsNullOrWhiteSpace(d.CaiCorrelativo))
        {
            AddLabel(caja, $"CAI: {d.CaiCorrelativo}", 0f, 64f, 230f, 12f, 7.5f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        }

        y = Math.Max(yEmp, 78f) + 8f;
        AddLine(band, y, 0f, ContentWidth, 2f);
        y += 10f;

        // ── Proveedor (sujeto retenido) ──
        AddLabel(band, "Retenido a:", 0f, y, 90f, 15f, 10f, bold: true);
        AddLabel(band, d.ProveedorNombre, 92f, y, 658f, 15f, 10f);
        y += 18f;

        var provLinea = JoinNonEmpty("   ·   ",
            string.IsNullOrWhiteSpace(d.ProveedorCodigo) ? null : $"Código: {d.ProveedorCodigo!.Trim()}",
            string.IsNullOrWhiteSpace(d.ProveedorRtn) ? null : $"R.T.N.: {d.ProveedorRtn!.Trim()}");
        if (!string.IsNullOrWhiteSpace(provLinea))
        {
            AddLabel(band, provLinea, 92f, y, 658f, 15f, 9.5f, color: Color.DimGray);
            y += 18f;
        }

        // ── Documento origen ──
        AddLabel(band, "Documento:", 0f, y, 90f, 15f, 10f, bold: true);
        AddLabel(band, $"Compromiso/Orden No. {d.NumeroOrden} — Abono No. {d.NumeroAbono}", 92f, y, 658f, 15f, 10f);
        y += 18f;

        if (!string.IsNullOrWhiteSpace(d.Concepto))
        {
            var conceptoWrap = WrapForWidth(d.Concepto, 658f, 9.5f);
            var altoConcepto = CountLines(conceptoWrap) * 13f;
            AddLabel(band, "Concepto:", 0f, y, 90f, 15f, 10f, bold: true);
            AddLabel(band, conceptoWrap, 92f, y, 658f, altoConcepto, 9.5f, multiline: true, color: Color.DimGray);
            y += Math.Max(18f, altoConcepto + 3f);
        }

        // ── Base del pago ──
        AddLabel(band, "Base del pago:", 0f, y, 110f, 15f, 10f, bold: true);
        AddLabel(band, $"L {Money(d.BaseTotal)}", 112f, y, 200f, 15f, 10f);
        y += 22f;

        // ── Grilla de retenciones aplicadas ──
        (string, TextAlignment)[] encabezados =
        [
            ("Código", TextAlignment.MiddleLeft),
            ("Concepto de retención", TextAlignment.MiddleLeft),
            ("%", TextAlignment.MiddleRight),
            ("Base", TextAlignment.MiddleRight),
            ("Retenido", TextAlignment.MiddleRight)
        ];
        y = AddGridRow(band, y, 18f, ColAnchos, encabezados, bold: true, header: true);

        foreach (var l in d.Lineas)
        {
            var nombreWrap = WrapForWidth(l.Nombre, ColAnchos[1], 9.5f);
            var alto = RowHeight(nombreWrap, l.Codigo);
            (string, TextAlignment)[] celdas =
            [
                (l.Codigo, TextAlignment.MiddleLeft),
                (nombreWrap, TextAlignment.MiddleLeft),
                (l.Porcentaje.ToString("N2", EsHn), TextAlignment.MiddleRight),
                (Money(l.BaseLinea), TextAlignment.MiddleRight),
                (Money(l.MontoRetenido), TextAlignment.MiddleRight)
            ];
            y = AddGridRow(band, y, alto, ColAnchos, celdas);
        }

        // Total retenido (fila de cierre con borde superior).
        (string, TextAlignment)[] total =
        [
            (string.Empty, TextAlignment.MiddleLeft),
            (string.Empty, TextAlignment.MiddleLeft),
            (string.Empty, TextAlignment.MiddleRight),
            ("TOTAL RETENIDO", TextAlignment.MiddleRight),
            (Money(d.TotalRetenido), TextAlignment.MiddleRight)
        ];
        y = AddGridRow(band, y, 20f, ColAnchos, total, bold: true, total: true);
        y += 8f;

        // ── Total en letras ──
        y = AddBloqueEnmarcado(band, y, $"SON: {d.MontoEnLetras}");

        if (d.Anulada && !string.IsNullOrWhiteSpace(d.MotivoAnulacion))
        {
            AddLabel(band, $"Motivo de anulación: {d.MotivoAnulacion}", 0f, y, ContentWidth, 14f, 8.5f, color: Color.Firebrick);
            y += 16f;
        }

        // ── Leyenda + firma ──
        y += 8f;
        AddLabel(band,
            "El agente retenedor certifica haber practicado la retención detallada y que enterará al fisco la suma retenida conforme a la ley.",
            0f, y, ContentWidth, 26f, 8f, multiline: true, color: Color.DimGray);
        y += 30f;

        y += 40f;
        AddLine(band, y, 255f, 240f, 1f);
        AddLabel(band, "Firma y sello del agente retenedor", 255f, y + 4f, 240f, 12f, 8f, bold: true, TextAlignment.MiddleCenter);
        y += 30f;

        return y;
    }
}
