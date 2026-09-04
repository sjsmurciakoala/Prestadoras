using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Reports;
using Xunit;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Humo del reporte de antigüedad de saldos (<see cref="Rpt_Dev_AntiguedadSaldos_Proveedor"/>):
/// que el cuadro se genere en PDF y Excel sin reventar. No toca la BD —arma el DTO en memoria—, así
/// que ejercita el layout, las expresiones (incluida la <c>Iif</c> que oculta los ceros de tramo) y
/// los totales <c>sumSum</c> de verdad, no sólo su compilación.
/// </summary>
public class AntiguedadSaldosReporteTests
{
    private static AntiguedadSaldosImpresionDto DatosEjemplo() => new()
    {
        EmpresaNombre = "MERENDON",
        EmpresaRtn = "08019000123456",
        Corte = new DateOnly(2026, 8, 14),
        FiltroTexto = "Incluye por vencer y vencido · compras y compromisos",
        Items = new List<AntiguedadSaldosProveedorFilaDto>
        {
            // Sólo dos tramos: el resto llega en 0 y ejercita la rama vacía de la expresión Iif.
            new() { CodProveedor = "0142", Nombre = "Químicos del Valle, S. de R.L.",
                    PorVencer = 420000m, Tramo30 = 180000m, SaldoTotal = 600000m, DocumentosPendientes = 4 },
            // Los seis tramos con monto.
            new() { CodProveedor = "0088", Nombre = "Distribuidora Ferretera El Yunque",
                    PorVencer = 84300m, Tramo30 = 62000m, Tramo60 = 41700m, Tramo90 = 28900m,
                    Tramo120 = 9600m, TramoMas120 = 6000m, Vencido = 148200m, SaldoTotal = 232500m, DocumentosPendientes = 8 },
            // Sin nada por vencer: sólo tramos viejos.
            new() { CodProveedor = "0603", Nombre = "Servicios Industriales GEA",
                    Tramo90 = 9200m, Tramo120 = 12400m, TramoMas120 = 40000m, Vencido = 61600m, SaldoTotal = 61600m, DocumentosPendientes = 3 }
        },
        Totales = new AntiguedadSaldosTotalesDto
        {
            Proveedores = 3, PorVencer = 504300m, Tramo30 = 242000m, Tramo60 = 41700m,
            Tramo90 = 38100m, Tramo120 = 22000m, TramoMas120 = 46000m, Vencido = 209800m,
            SaldoTotal = 894100m, DocumentosPendientes = 15
        }
    };

    [Fact]
    public void Reporte_genera_pdf_valido()
    {
        using var report = new Rpt_Dev_AntiguedadSaldos_Proveedor(DatosEjemplo());
        using var ms = new MemoryStream();

        report.ExportToPdf(ms);

        Assert.True(ms.Length > 0);
        var head = Encoding.ASCII.GetString(ms.GetBuffer(), 0, 4);
        Assert.Equal("%PDF", head);   // firma de un PDF válido
    }

    [Fact]
    public void Reporte_genera_excel()
    {
        using var report = new Rpt_Dev_AntiguedadSaldos_Proveedor(DatosEjemplo());
        using var ms = new MemoryStream();

        report.ExportToXlsx(ms);

        Assert.True(ms.Length > 0);
        // Un .xlsx es un contenedor ZIP: empieza con la firma "PK".
        Assert.Equal((byte)'P', ms.GetBuffer()[0]);
        Assert.Equal((byte)'K', ms.GetBuffer()[1]);
    }

    [Fact]
    public void Reporte_sin_filas_no_revienta()
    {
        // El pie muestra "ningún proveedor..." y los sumSum corren sobre 0 filas.
        using var report = new Rpt_Dev_AntiguedadSaldos_Proveedor(new AntiguedadSaldosImpresionDto
        {
            EmpresaNombre = "MERENDON",
            Corte = new DateOnly(2026, 8, 14)
        });
        using var ms = new MemoryStream();

        report.ExportToPdf(ms);

        Assert.True(ms.Length > 0);
    }
}
