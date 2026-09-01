using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Reports;
using Xunit;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// Humo de los dos reportes de presupuesto (<see cref="Rpt_Dev_EjecucionPresupuestaria"/> y
/// <see cref="Rpt_Dev_CompromisosPendientes"/>): que se generen en PDF y en Excel sin reventar.
/// <para>
/// No tocan la BD —los DTO se arman en memoria—, así que ejercitan el layout, las expresiones
/// (incluida la <c>Iif</c> que oculta ceros y la que deja el porcentaje vacío cuando viene nulo) y
/// los totales de verdad, no sólo su compilación. Mismo patrón que
/// <c>AntiguedadSaldosReporteTests</c>.
/// </para>
/// </summary>
public class PresupuestoReportesTests
{
    private static PresupuestoEjecucionImpresionDto DatosEjecucion() => new()
    {
        EmpresaNombre = "Aguas de Puerto Cortés",
        EmpresaRtn = "05069999182490",
        Corte = new DateOnly(2026, 8, 28),
        FiltroTexto = "Solo con movimiento",
        Items = new List<PresupuestoEjecucionItemDto>
        {
            // Partida sobregirada: el disponible queda en 0 y el % pasa de 100.
            new() { IdPresupuesto = "PRE-2026", ConCuentaCode = "11401010101",
                    CuentaNombre = "Inv. Tubería y accesorios Agua Potable", CuentaPresupuestable = true,
                    Presupuesto = 10000m, Comprometido = 15000m, Ejecutado = 0m, Disponible = 0m, PctUtilizado = 150m },
            // Partida normal, con comprometido y ejecutado.
            new() { IdPresupuesto = "PRE-2026", ConCuentaCode = "11401010201",
                    CuentaNombre = "Inv. Tubería y accesorios Alc. Sanitario", CuentaPresupuestable = true,
                    Presupuesto = 5000m, Comprometido = 500m, Ejecutado = 300m, Disponible = 4200m, PctUtilizado = 16m },
            // Sin movimiento y SIN porcentaje: ejercita la rama que deja la celda vacía.
            new() { IdPresupuesto = "PRE-2026", ConCuentaCode = "11401010301",
                    CuentaNombre = "Materiales y Útiles de oficina", CuentaPresupuestable = true,
                    Presupuesto = 1000m, Comprometido = 0m, Ejecutado = 0m, Disponible = 1000m, PctUtilizado = null }
        },
        TotalPresupuesto = 16000m,
        TotalComprometido = 15500m,
        TotalEjecutado = 300m,
        TotalDisponible = 5200m
    };

    private static PresupuestoCompromisosImpresionDto DatosCompromisos() => new()
    {
        EmpresaNombre = "Aguas de Puerto Cortés",
        Corte = new DateOnly(2026, 8, 28),
        FiltroTexto = "Con 30 días o más",
        Items = new List<PresupuestoCompromisoPendienteDto>
        {
            new() { DocumentoTipo = "ORDEN_COMPRA", DocumentoId = 2027, DocumentoNumero = "00048",
                    Fecha = new DateOnly(2026, 8, 28), Proveedor = "AGENCIA LA MUNDIAL",
                    ConCuentaCode = "11401010101", MontoComprometido = 12000m, MontoDevengado = 0m,
                    SaldoComprometido = 12000m, DiasAntiguedad = 0 },
            // Con parte recibida: ejercita la columna "Recibido" y el saldo parcial.
            new() { DocumentoTipo = "ORDEN_COMPRA", DocumentoId = 2022, DocumentoNumero = "00043",
                    Fecha = new DateOnly(2026, 7, 15), Proveedor = "LARACH Y CIA S. DE R.L. DE C.V.",
                    ConCuentaCode = "11401010101", MontoComprometido = 2000m, MontoDevengado = 800m,
                    SaldoComprometido = 1200m, DiasAntiguedad = 44 }
        },
        TotalComprometido = 14000m,
        TotalDevengado = 800m,
        TotalSaldo = 13200m
    };

    [Fact]
    public void Ejecucion_genera_pdf_valido()
    {
        using var report = new Rpt_Dev_EjecucionPresupuestaria(DatosEjecucion());
        using var ms = new MemoryStream();

        report.ExportToPdf(ms);

        Assert.True(ms.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(ms.GetBuffer(), 0, 4));
    }

    [Fact]
    public void Ejecucion_genera_excel()
    {
        using var report = new Rpt_Dev_EjecucionPresupuestaria(DatosEjecucion());
        using var ms = new MemoryStream();

        report.ExportToXlsx(ms);

        Assert.True(ms.Length > 0);
        // Un .xlsx es un contenedor ZIP: empieza con la firma "PK".
        Assert.Equal((byte)'P', ms.GetBuffer()[0]);
        Assert.Equal((byte)'K', ms.GetBuffer()[1]);
    }

    [Fact]
    public void Compromisos_genera_pdf_valido()
    {
        using var report = new Rpt_Dev_CompromisosPendientes(DatosCompromisos());
        using var ms = new MemoryStream();

        report.ExportToPdf(ms);

        Assert.True(ms.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(ms.GetBuffer(), 0, 4));
    }

    [Fact]
    public void Compromisos_genera_excel()
    {
        using var report = new Rpt_Dev_CompromisosPendientes(DatosCompromisos());
        using var ms = new MemoryStream();

        report.ExportToXlsx(ms);

        Assert.True(ms.Length > 0);
        Assert.Equal((byte)'P', ms.GetBuffer()[0]);
        Assert.Equal((byte)'K', ms.GetBuffer()[1]);
    }

    /// <summary>
    /// Sin filas los reportes siguen saliendo: llevan su propio mensaje de "no hay nada" en el pie,
    /// que es justo la rama que más fácil se rompe si el DataSource viene vacío.
    /// </summary>
    [Fact]
    public void Reportes_sin_filas_no_revientan()
    {
        using (var ejecucion = new Rpt_Dev_EjecucionPresupuestaria(new PresupuestoEjecucionImpresionDto()))
        using (var ms = new MemoryStream())
        {
            ejecucion.ExportToPdf(ms);
            Assert.True(ms.Length > 0);
        }

        using (var compromisos = new Rpt_Dev_CompromisosPendientes(new PresupuestoCompromisosImpresionDto()))
        using (var ms = new MemoryStream())
        {
            compromisos.ExportToPdf(ms);
            Assert.True(ms.Length > 0);
        }
    }

    /// <summary>El constructor sin parámetros lo usa el diseñador y la instanciación por reflexión.</summary>
    [Fact]
    public void Reportes_tienen_constructor_sin_parametros()
    {
        using var ejecucion = new Rpt_Dev_EjecucionPresupuestaria();
        using var compromisos = new Rpt_Dev_CompromisosPendientes();

        Assert.NotNull(ejecucion);
        Assert.NotNull(compromisos);
    }
}
