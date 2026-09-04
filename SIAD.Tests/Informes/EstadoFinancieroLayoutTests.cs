using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.UI.CrossTab;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Reports;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Informes;

/// <summary>
/// El formato impreso de los estados financieros (2026-09-03).
///
/// Los cuatro estados se entregan con el mismo membrete y la misma estructura de columnas, y esa
/// presentación vive en <c>EstadoFinancieroLayout</c>. Estas pruebas fijan lo que se rompe en
/// silencio: que el reporte se arme, que el membrete quede en los márgenes y no empujando el
/// contenido, y que el pie lleve el número de página y NADA más —el juego impreso no lleva
/// "Generado:" ni "Página X de Y"—.
///
/// No comprueban el aspecto pixel a pixel; para eso está mirar el PDF. Comprueban la estructura,
/// que es lo que un cambio descuidado deshace sin que nadie lo note hasta que se imprime.
///
/// <para>
/// Con <c>SIAD_TEST_PDF_DIR</c> apuntando a una carpeta, cada prueba deja además el PDF generado
/// ahí para revisarlo a ojo contra el juego original.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class EstadoFinancieroLayoutTests : IntegrationTestBase
{
    public EstadoFinancieroLayoutTests(PostgresFixture fixture) : base(fixture) { }

    private sealed class EmpresaFija : ICurrentCompanyService
    {
        private readonly long _companyId;

        public EmpresaFija(long companyId) => _companyId = companyId;

        public long GetCompanyId() => _companyId;
    }

    private XtraReport ConstruirReporte(string codigo, string nombre)
    {
        var opciones = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        using var contexto = new SiadDbContext(opciones, new EmpresaFija(CompanyId));

        // La cadena que expone NpgsqlConnection viene SIN contraseña —Npgsql la retira— y el
        // origen de datos del reporte abre su propia conexión, así que se toma la original.
        var cadena = Environment.GetEnvironmentVariable(PostgresFixture.ConnectionStringEnvVar);

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = cadena,
            })
            .Build();

        var fabrica = new ReportTemplateFactory(contexto, new EmpresaFija(CompanyId), configuracion);
        return fabrica.CreateTemplateReport(codigo, nombre, null, codigo);
    }

    private static TBanda? Banda<TBanda>(XtraReport report) where TBanda : Band
    {
        foreach (Band banda in report.Bands)
        {
            if (banda is TBanda encontrada)
            {
                return encontrada;
            }
        }

        return null;
    }

    private static IEnumerable<XRControl> TodosLosControles(XRControl raiz)
    {
        foreach (XRControl hijo in raiz.Controls)
        {
            yield return hijo;
            foreach (var nieto in TodosLosControles(hijo))
            {
                yield return nieto;
            }
        }
    }

    /// <summary>Deja el PDF en disco cuando se pide, para revisarlo contra el juego impreso.</summary>
    private static void GuardarPdfSiSePide(XtraReport report, string nombreArchivo)
    {
        var carpeta = Environment.GetEnvironmentVariable("SIAD_TEST_PDF_DIR");
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            return;
        }

        // El origen de datos del reporte abre su PROPIA conexion; sin resolverla, DevExpress cae
        // a SqlClient y falla con TypeLoadException. En el portal esto lo hace el bootstrap de
        // reporteria; aqui hay que pedirlo a mano.
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    Environment.GetEnvironmentVariable(PostgresFixture.ConnectionStringEnvVar),
            })
            .Build();

        ReportingRuntimeBootstrap.ConfigureSqlDataSources(report, configuracion);

        Directory.CreateDirectory(carpeta);
        report.ExportToPdf(Path.Combine(carpeta, nombreArchivo));

        // Ademas del PDF, una imagen por pagina: es lo que permite comparar el diseno a ojo sin
        // depender de un visor.
        report.ExportToImage(
            Path.Combine(carpeta, Path.ChangeExtension(nombreArchivo, ".png")),
            new DevExpress.XtraPrinting.ImageExportOptions
            {
                Format = DevExpress.Drawing.DXImageFormat.Png,
                Resolution = 110,
                ExportMode = DevExpress.XtraPrinting.ImageExportMode.SingleFilePageByPage,
                PageBorderWidth = 0,
            });
    }

    [SkippableFact]
    public void El_flujo_de_efectivo_se_arma_con_el_membrete_en_los_margenes()
    {
        using var report = ConstruirReporte("estado-flujo-efectivo", "Estado de flujos de efectivo");

        var margenSuperior = Banda<TopMarginBand>(report);
        var margenInferior = Banda<BottomMarginBand>(report);

        Assert.NotNull(margenSuperior);
        Assert.NotNull(margenInferior);

        // El membrete va en los márgenes para repetirse en todas las hojas sin mover el contenido.
        Assert.True(margenSuperior!.Controls.Count > 0,
            "El margen superior quedó vacío: el membrete no se aplicó.");
        Assert.True(margenSuperior.HeightF >= 70f,
            $"El margen superior mide {margenSuperior.HeightF}: no cabe el membrete.");

        // El membrete lleva el logo de la empresa; si no aparece, o no se resolvio la empresa o
        // no se pudo decodificar la imagen, y el encabezado sale mudo.
        var conLogo = 0;
        foreach (var control in TodosLosControles(margenSuperior))
        {
            if (control is XRPictureBox)
            {
                conLogo++;
            }
        }

        Assert.True(conLogo == 1, $"Se esperaba el logo en el membrete; hay {conLogo} imagen(es).");

        GuardarPdfSiSePide(report, "estado-flujo-efectivo.pdf");
    }

    [SkippableFact]
    public void El_estado_de_resultados_compara_los_dos_ejercicios()
    {
        using var report = ConstruirReporte("estado-resultados", "Estado de resultados");

        var cabecera = Banda<PageHeaderBand>(report);
        Assert.NotNull(cabecera);

        var textos = new List<string>();
        var expresiones = new List<string>();

        foreach (var control in TodosLosControles(cabecera!))
        {
            if (control is not XRLabel etiqueta)
            {
                continue;
            }

            textos.Add(etiqueta.Text ?? string.Empty);
            foreach (ExpressionBinding enlace in etiqueta.ExpressionBindings)
            {
                expresiones.Add(enlace.Expression ?? string.Empty);
            }
        }

        // La cabecera va en dos niveles: el corte y la variación.
        Assert.Contains(textos, t => t.Contains("AL 31 DE DICIEMBRE", StringComparison.Ordinal));
        Assert.Contains(textos, t => t.Contains("VARIACION", StringComparison.Ordinal));
        Assert.Contains(expresiones, e => e.Contains("AddYears", StringComparison.Ordinal));

        GuardarPdfSiSePide(report, "estado-resultados.pdf");
    }

    [SkippableFact]
    public void El_balance_general_compara_los_dos_ejercicios()
    {
        using var report = ConstruirReporte("estado-situacion-financiera", "Estado de situacion financiera");

        var textos = new List<string>();
        foreach (Band banda in report.Bands)
        {
            foreach (var control in TodosLosControles(banda))
            {
                if (control is XRLabel etiqueta)
                {
                    textos.Add(etiqueta.Text ?? string.Empty);
                }
            }
        }

        Assert.Contains(textos, t => t.Contains("BALANCE GENERAL", StringComparison.Ordinal));
        Assert.Contains(textos, t => t.Contains("VARIACION", StringComparison.Ordinal));

        GuardarPdfSiSePide(report, "balance-general.pdf");
    }

    [SkippableFact]
    public void El_patrimonio_se_imprime_como_matriz()
    {
        using var report = ConstruirReporte("estado-cambios-patrimonio", "Estado de cambios en el patrimonio");

        XRCrossTab? matriz = null;
        foreach (Band banda in report.Bands)
        {
            foreach (var control in TodosLosControles(banda))
            {
                if (control is XRCrossTab encontrada)
                {
                    matriz = encontrada;
                }
            }
        }

        Assert.True(matriz is not null,
            "El patrimonio debe imprimirse como matriz: una fila por movimiento y una columna por componente.");

        // Las columnas son los componentes que configura cada empresa, no una lista fija.
        Assert.Contains(matriz!.ColumnFields.Cast<CrossTabColumnField>(),
            c => c.FieldName == "componente");

        GuardarPdfSiSePide(report, "estado-cambios-patrimonio.pdf");
    }

    [SkippableFact]
    public void El_pie_lleva_el_numero_de_pagina_y_nada_mas()
    {
        using var report = ConstruirReporte("estado-flujo-efectivo", "Estado de flujos de efectivo");

        var margenInferior = Banda<BottomMarginBand>(report);
        Assert.NotNull(margenInferior);

        var conNumero = 0;
        foreach (var control in TodosLosControles(margenInferior!))
        {
            if (control is not XRPageInfo info)
            {
                continue;
            }

            // El juego impreso numera la hoja y ya: nada de fecha de generación ni "de N".
            Assert.NotEqual(PageInfo.DateTime, info.PageInfo);
            Assert.NotEqual(PageInfo.NumberOfTotal, info.PageInfo);

            if (info.PageInfo == PageInfo.Number)
            {
                conNumero++;
            }
        }

        Assert.True(conNumero == 1,
            $"Se esperaba exactamente un número de página en el pie; hay {conNumero}.");
    }

    [SkippableFact]
    public void El_encabezado_identifica_la_empresa_y_declara_la_moneda()
    {
        using var report = ConstruirReporte("estado-flujo-efectivo", "Estado de flujos de efectivo");

        var encabezado = Banda<ReportHeaderBand>(report);
        Assert.NotNull(encabezado);

        var textos = new List<string>();
        var expresiones = new List<string>();

        foreach (var control in TodosLosControles(encabezado!))
        {
            if (control is not XRLabel etiqueta)
            {
                continue;
            }

            textos.Add(etiqueta.Text ?? string.Empty);
            foreach (ExpressionBinding enlace in etiqueta.ExpressionBindings)
            {
                expresiones.Add(enlace.Expression ?? string.Empty);
            }
        }

        Assert.Contains(textos, t => t.Contains("ESTADO DE FLUJO DE EFECTIVO", StringComparison.Ordinal));
        Assert.Contains(textos, t => t.Contains("Expresado en lempiras", StringComparison.OrdinalIgnoreCase));

        // Las tres líneas de identidad salen del dataset, no de constantes.
        Assert.Contains(expresiones, e => e.Contains("empresa_nombre", StringComparison.Ordinal));
        Assert.Contains(expresiones, e => e.Contains("empresa_nombre_legal", StringComparison.Ordinal));
    }

    [SkippableFact]
    public void Los_rotulos_de_anio_salen_de_la_fecha_del_reporte()
    {
        using var report = ConstruirReporte("estado-flujo-efectivo", "Estado de flujos de efectivo");

        var cabecera = Banda<PageHeaderBand>(report);
        Assert.NotNull(cabecera);

        var expresiones = new List<string>();
        foreach (var control in TodosLosControles(cabecera!))
        {
            if (control is XRLabel etiqueta)
            {
                foreach (ExpressionBinding enlace in etiqueta.ExpressionBindings)
                {
                    expresiones.Add(enlace.Expression ?? string.Empty);
                }
            }
        }

        // Si alguien deja el año escrito a mano, el reporte miente el 1 de enero siguiente.
        Assert.Contains(expresiones, e => e.Contains("FechaHasta", StringComparison.Ordinal));
        Assert.Contains(expresiones, e => e.Contains("AddYears", StringComparison.Ordinal));
    }
}
