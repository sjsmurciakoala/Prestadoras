using System;
using System.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Reports;

/// <summary>
/// Reporte "Ficha de evaluación de proveedor": la calificación del período con el desglose que la
/// sustenta —peso configurado, peso aplicado, evidencia y puntos por criterio— y firmas.
/// <para>
/// Es un documento de UNA página por proveedor, no un listado paginado: se arma todo en el
/// ReportHeader con posiciones calculadas, igual que los comprobantes de almacén. Los criterios
/// son pocos (media docena), así que no hace falta banda de detalle.
/// </para>
/// </summary>
public sealed class Rpt_Dev_Evaluacion_Proveedor : ComprobanteAlmacenReportBase
{
    // Columnas del desglose (x, ancho) — suman ContentWidth (750).
    private const float ColCriterioX = 0f, ColCriterioW = 250f;
    private const float ColEvidenciaX = 250f, ColEvidenciaW = 290f;
    private const float ColPesoX = 540f, ColPesoW = 70f;
    private const float ColAplicadoX = 610f, ColAplicadoW = 70f;
    private const float ColPuntosX = 680f, ColPuntosW = 70f;

    private const float FilaAlto = 30f;

    public Rpt_Dev_Evaluacion_Proveedor() : this(new EvaluacionFichaImpresionDto()) { }

    public Rpt_Dev_Evaluacion_Proveedor(EvaluacionFichaImpresionDto datos)
    {
        datos ??= new EvaluacionFichaImpresionDto();

        Bands.Add(BuildReportHeader(datos));
        Bands.Add(BuildPie(
            $"Evaluación {datos.Ficha.PeriodoCodigo} · {datos.Ficha.CodProveedor}",
            string.IsNullOrWhiteSpace(datos.ImpresoPor) ? "sistema" : datos.ImpresoPor));
    }

    private ReportHeaderBand BuildReportHeader(EvaluacionFichaImpresionDto datos)
    {
        var band = new ReportHeaderBand();
        var ficha = datos.Ficha;

        var y = BuildEncabezadoEmpresa(band, datos);
        y += 8f;

        AddLabel(band, "FICHA DE EVALUACIÓN DE PROVEEDOR", 0f, y, ContentWidth, 20f, 13f,
            bold: true, TextAlignment.MiddleCenter);
        y += 22f;

        AddLabel(band,
            $"{ficha.PeriodoCodigo} — {ficha.PeriodoNombre}  ·  del {ficha.FechaDesde:dd/MM/yyyy} al {ficha.FechaHasta:dd/MM/yyyy}",
            0f, y, ContentWidth, 13f, 9f, align: TextAlignment.MiddleCenter, color: Color.DimGray);
        y += 18f;

        y = BuildDatosProveedor(band, ficha, y);
        y += 8f;

        y = BuildCalificacion(band, ficha, y);
        y += 10f;

        y = BuildCabeceraTabla(band, y);
        y = BuildCriterios(band, ficha, y);
        y = BuildTotal(band, ficha, y);

        if (!string.IsNullOrWhiteSpace(datos.NotaCriteriosSinDatos))
        {
            y += 8f;
            AddLabel(band, datos.NotaCriteriosSinDatos, 0f, y, ContentWidth, 24f, 8.5f,
                align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);
            y += 26f;
        }

        if (!string.IsNullOrWhiteSpace(ficha.Observaciones))
        {
            y += 6f;
            AddLabel(band, "Observaciones y plan de acción:", 0f, y, ContentWidth, 14f, 9f, bold: true);
            y += 15f;
            y = AddBloqueEnmarcado(band, y, ficha.Observaciones!, 9f);
        }

        y = BuildFirmas(band, y, new (string, string?)[]
        {
            ("Elaborado por · Compras", string.IsNullOrWhiteSpace(datos.ImpresoPor) ? null : datos.ImpresoPor),
            ("Revisado por · Gerencia", null)
        });

        band.HeightF = y;
        return band;
    }

    // Identidad del proveedor, enmarcada en dos columnas.
    private static float BuildDatosProveedor(Band band, EvaluacionFichaDto ficha, float y)
    {
        const float alto = 58f;

        band.Controls.Add(new XRPanel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, alto),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            BorderColor = Color.Black
        });

        var interiorY = y + 5f;
        const float etiquetaW = 86f;
        const float col2X = 390f;

        AddLabel(band, "Proveedor:", 8f, interiorY, etiquetaW, 14f, 9f, bold: true);
        AddLabel(band, $"{ficha.CodProveedor}  —  {ficha.ProveedorNombre}",
            8f + etiquetaW, interiorY, col2X - etiquetaW - 16f, 14f, 9f);

        AddLabel(band, "RTN:", col2X, interiorY, 40f, 14f, 9f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(ficha.Rtn) ? "—" : ficha.Rtn,
            col2X + 42f, interiorY, 180f, 14f, 9f);

        interiorY += 16f;

        AddLabel(band, "Tipo:", 8f, interiorY, etiquetaW, 14f, 9f, bold: true);
        AddLabel(band, string.IsNullOrWhiteSpace(ficha.TipoNombre) ? "—" : ficha.TipoNombre,
            8f + etiquetaW, interiorY, col2X - etiquetaW - 16f, 14f, 9f);

        AddLabel(band, "Compras:", col2X, interiorY, 56f, 14f, 9f, bold: true);
        AddLabel(band, Money(ficha.ComprasPeriodo), col2X + 58f, interiorY, 180f, 14f, 9f);

        interiorY += 16f;

        AddLabel(band, "Facturas:", 8f, interiorY, etiquetaW, 14f, 9f, bold: true);
        AddLabel(band, ficha.Recepciones.ToString("N0", EsHn), 8f + etiquetaW, interiorY, 100f, 14f, 9f);

        AddLabel(band, "Órdenes:", col2X, interiorY, 56f, 14f, 9f, bold: true);
        AddLabel(band, ficha.Ordenes.ToString("N0", EsHn), col2X + 58f, interiorY, 100f, 14f, 9f);

        return y + alto;
    }

    // Caja grande con el puntaje y la clase.
    private static float BuildCalificacion(Band band, EvaluacionFichaDto ficha, float y)
    {
        const float alto = 52f;

        band.Controls.Add(new XRPanel
        {
            BoundsF = new RectangleF(0f, y, ContentWidth, alto),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            BorderColor = Color.Black,
            BackColor = Color.FromArgb(242, 244, 245)
        });

        AddLabel(band, "CALIFICACIÓN DEL PERÍODO", 10f, y + 8f, 300f, 14f, 9f, bold: true);
        AddLabel(band,
            ficha.Puntaje.HasValue ? ficha.Puntaje.Value.ToString("N2", EsHn) : "sin datos",
            10f, y + 22f, 300f, 24f, 18f, bold: true);

        var clase = string.IsNullOrWhiteSpace(ficha.ClaseCodigo)
            ? "SIN CLASE"
            : $"CLASE {ficha.ClaseCodigo}  —  {ficha.ClaseNombre}".ToUpperInvariant();

        AddLabel(band, clase, 330f, y + 16f, ContentWidth - 340f, 22f, 13f,
            bold: true, TextAlignment.MiddleRight);

        return y + alto;
    }

    private static float BuildCabeceraTabla(Band band, float y)
    {
        AddLabel(band, "CRITERIO", ColCriterioX + 4f, y, ColCriterioW, 15f, 8.5f, bold: true);
        AddLabel(band, "EVIDENCIA", ColEvidenciaX + 4f, y, ColEvidenciaW, 15f, 8.5f, bold: true);
        AddLabel(band, "PESO", ColPesoX, y, ColPesoW - 4f, 15f, 8.5f, bold: true, TextAlignment.MiddleRight);
        AddLabel(band, "APLICADO", ColAplicadoX, y, ColAplicadoW - 4f, 15f, 8.5f, bold: true, TextAlignment.MiddleRight);
        AddLabel(band, "PUNTOS", ColPuntosX, y, ColPuntosW - 4f, 15f, 8.5f, bold: true, TextAlignment.MiddleRight);

        y += 16f;
        AddLine(band, y, 0f, ContentWidth, 1.5f);
        return y + 3f;
    }

    // Un renglón por criterio: nombre + origen arriba, evidencia debajo en gris.
    private static float BuildCriterios(Band band, EvaluacionFichaDto ficha, float y)
    {
        foreach (var c in ficha.Criterios)
        {
            AddLabel(band, c.CriterioNombre, ColCriterioX + 4f, y, ColCriterioW - 8f, 14f, 9f, bold: true);
            AddLabel(band, c.EsManual ? "manual" : "automático",
                ColCriterioX + 4f, y + 14f, ColCriterioW - 8f, 12f, 7.5f, color: Color.DimGray);

            var evidencia = string.IsNullOrWhiteSpace(c.Detalle) ? "—" : c.Detalle!;
            AddLabel(band, WrapForWidth(evidencia, ColEvidenciaW - 8f, 8.5f),
                ColEvidenciaX + 4f, y, ColEvidenciaW - 8f, 26f, 8.5f,
                align: TextAlignment.TopLeft, multiline: true, color: Color.DimGray);

            AddLabel(band, $"{c.Peso:N2}%", ColPesoX, y, ColPesoW - 4f, 14f, 9f,
                align: TextAlignment.MiddleRight);

            // Sin datos: se marca explícito para que el lector no crea que sacó cero.
            AddLabel(band,
                c.PesoEfectivo.HasValue ? $"{c.PesoEfectivo.Value:N2}%" : "no puntúa",
                ColAplicadoX, y, ColAplicadoW - 4f, 14f, 9f,
                align: TextAlignment.MiddleRight,
                color: c.PesoEfectivo.HasValue ? Color.Black : Color.DimGray);

            AddLabel(band, c.Puntos.HasValue ? c.Puntos.Value.ToString("N2", EsHn) : "—",
                ColPuntosX, y, ColPuntosW - 4f, 14f, 9f, bold: true, TextAlignment.MiddleRight);

            y += FilaAlto;
            AddLine(band, y - 4f, 0f, ContentWidth, 0.5f);
        }

        return y;
    }

    private static float BuildTotal(Band band, EvaluacionFichaDto ficha, float y)
    {
        AddLine(band, y, 0f, ContentWidth, 1.5f);
        y += 6f;

        AddLabel(band, "CALIFICACIÓN FINAL", ColEvidenciaX, y, ColPuntosX - ColEvidenciaX - 8f, 18f, 10f,
            bold: true, TextAlignment.MiddleRight);
        AddLabel(band, ficha.Puntaje.HasValue ? ficha.Puntaje.Value.ToString("N2", EsHn) : "—",
            ColPuntosX, y, ColPuntosW - 4f, 18f, 11f, bold: true, TextAlignment.MiddleRight);

        return y + 20f;
    }
}
