using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Scorecard de proveedores (<c>Database/2026-08-14_prv_evaluacion.sql</c>, F1).
/// <para>
/// Lo que se prueba aquí son las tres reglas que no viven en la BD: el <b>snapshot</b> del peso,
/// la <b>redistribución</b> del peso de los criterios sin datos y que recalcular <b>respete lo
/// capturado a mano</b>. Todo corre dentro de la transacción del test, que hace ROLLBACK.
/// </para>
/// </summary>
[Collection("Postgres")]
public class EvaluacionProveedorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private EvaluacionProveedorService? _service;

    private const string CodigoPeriodo = "TEST-EVAL";

    public EvaluacionProveedorTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _service = new EvaluacionProveedorService(_context, new TestCurrentCompanyService(CompanyId));
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Períodos ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task CrearPeriodo_QuedaAbiertoYSinEvaluaciones()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();

        Assert.True(periodo.Id > 0);
        Assert.Equal(EstadoEvaluacionPeriodo.Abierto, periodo.Estado);
        Assert.False(periodo.Cerrado);
        Assert.Equal(0, periodo.Evaluaciones);
    }

    [SkippableFact]
    public async Task CrearPeriodo_CodigoRepetido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        await CrearPeriodoAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearPeriodoAsync());
        Assert.Contains("Ya existe un período", ex.Message);
    }

    [SkippableFact]
    public async Task CrearPeriodo_FechaFinalAnterior_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearPeriodoAsync(new EvaluacionPeriodoUpsertDto
            {
                Codigo = CodigoPeriodo,
                Nombre = "Rango inválido",
                FechaDesde = hoy,
                FechaHasta = hoy.AddDays(-1)
            }, "tester"));

        Assert.Contains("no puede ser anterior", ex.Message);
    }

    // ── Cálculo ──────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Calcular_GeneraUnaEvaluacionPorProveedorConCompras()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");

        Skip.If(resultado.Evaluados == 0,
            "La base de pruebas no tiene recepciones de compra en el rango: nada que evaluar.");

        var ranking = await _service.GetRankingAsync(periodo.Id);
        Assert.Equal(resultado.Evaluados, ranking.Count);

        // El período quedó sellado con la fecha del cálculo.
        var recargado = await _service.GetPeriodoAsync(periodo.Id);
        Assert.NotNull(recargado!.FechaCalculo);
        Assert.Equal(resultado.Evaluados, recargado.Evaluaciones);
    }

    [SkippableFact]
    public async Task Calcular_ElDetalleGuardaSnapshotDelPeso()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var criterios = await _service.GetCriteriosAsync();
        var ranking = await _service.GetRankingAsync(periodo.Id);
        var fila = ranking[0];

        Assert.Equal(criterios.Count, fila.Criterios.Count);
        foreach (var r in fila.Criterios)
        {
            var origen = criterios.Single(c => c.Codigo == r.CriterioCodigo);
            Assert.Equal(origen.Peso, r.Peso);          // snapshot idéntico al catálogo
            Assert.Equal(origen.Nombre, r.CriterioNombre);
        }
    }

    [SkippableFact]
    public async Task Calcular_CriterioSinDatos_NoPuntuaCeroYRedistribuyeSuPeso()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ficha = await PrimeraFichaAsync(periodo.Id);
        var sinDatos = ficha.Criterios.Where(c => c.SinDatos).ToList();
        Skip.If(sinDatos.Count == 0, "En esta base ningún criterio quedó sin datos.");

        // Un criterio sin datos no aporta puntos ni peso: se excluye, no vale cero.
        foreach (var c in sinDatos)
        {
            Assert.Null(c.Logro);
            Assert.Null(c.Puntos);
            Assert.Null(c.PesoEfectivo);
        }

        // Y el peso de los que sí puntuaron suma exactamente 100.
        var pesoEfectivo = ficha.Criterios.Where(c => c.PesoEfectivo.HasValue).Sum(c => c.PesoEfectivo!.Value);
        Assert.InRange(pesoEfectivo, 99.9m, 100.1m);

        // El puntaje es la suma de los puntos, no un promedio sobre los criterios totales.
        var puntos = ficha.Criterios.Where(c => c.Puntos.HasValue).Sum(c => c.Puntos!.Value);
        Assert.Equal(Math.Round(puntos, 2), ficha.Puntaje);
    }

    [SkippableFact]
    public async Task Calcular_LaClaseCorrespondeAlPuntaje()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var clases = await _service.GetClasesAsync();
        var ficha = await PrimeraFichaAsync(periodo.Id);
        Skip.If(ficha.Puntaje is null, "El proveedor no obtuvo puntaje.");

        var clase = clases.Single(c => c.Codigo == ficha.ClaseCodigo);
        Assert.True(ficha.Puntaje >= clase.PuntajeDesde,
            $"El puntaje {ficha.Puntaje} no cae en la clase {clase.Codigo} ({clase.PuntajeDesde}–{clase.PuntajeHasta}).");
    }

    [SkippableFact]
    public async Task Calcular_DosVeces_NoDuplicaEvaluaciones()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var primera = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(primera.Evaluados == 0, "Sin recepciones en el rango.");

        var segunda = await _service.CalcularAsync(periodo.Id, "tester");

        Assert.Equal(primera.Evaluados, segunda.Evaluados);
        var ranking = await _service.GetRankingAsync(periodo.Id);
        Assert.Equal(primera.Evaluados, ranking.Count);

        // Y tampoco se duplican los renglones del detalle.
        var criterios = await _service.GetCriteriosAsync();
        Assert.Equal(criterios.Count, ranking[0].Criterios.Count);
    }

    // ── Captura manual ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Capturar_CriterioManual_CambiaElPuntajeYSobreviveAlRecalculo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ficha = await PrimeraFichaAsync(periodo.Id);
        var manual = ficha.Criterios.FirstOrDefault(c => c.EsManual);
        Skip.If(manual is null, "El catálogo no tiene criterios manuales.");
        Assert.True(manual!.SinDatos);   // nace pendiente de calificar

        var conCaptura = await _service.CapturarAsync(periodo.Id, ficha.CodProveedor,
            new EvaluacionCapturaDto { CriterioCodigo = manual.CriterioCodigo, Logro = 80m },
            "comprador");

        var capturado = conCaptura.Criterios.Single(c => c.CriterioCodigo == manual.CriterioCodigo);
        Assert.Equal(80m, capturado.Logro);
        Assert.Equal("comprador", capturado.UsuarioCaptura);
        Assert.NotNull(capturado.Puntos);

        // Recalcular el período NO borra lo que se calificó a mano.
        await _service.CalcularAsync(periodo.Id, "tester");
        var recalculada = await _service.GetFichaAsync(periodo.Id, ficha.CodProveedor);
        var tras = recalculada!.Criterios.Single(c => c.CriterioCodigo == manual.CriterioCodigo);

        Assert.Equal(80m, tras.Logro);
        Assert.Equal("comprador", tras.UsuarioCaptura);
    }

    [SkippableFact]
    public async Task Capturar_CriterioAutomatico_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ficha = await PrimeraFichaAsync(periodo.Id);
        var automatico = ficha.Criterios.First(c => !c.EsManual);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CapturarAsync(periodo.Id, ficha.CodProveedor,
                new EvaluacionCapturaDto { CriterioCodigo = automatico.CriterioCodigo, Logro = 100m },
                "comprador"));

        Assert.Contains("se calcula automáticamente", ex.Message);
    }

    [SkippableFact]
    public async Task Capturar_GuardaElPlanDeAccion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ficha = await PrimeraFichaAsync(periodo.Id);
        var actualizada = await _service.CapturarAsync(periodo.Id, ficha.CodProveedor,
            new EvaluacionCapturaDto { Observaciones = "Se solicita plan de mejora en entregas." },
            "comprador");

        Assert.Equal("Se solicita plan de mejora en entregas.", actualizada.Observaciones);
    }

    // ── Cierre ───────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Cerrar_CongelaElPeriodoYBloqueaRecalculoYCaptura()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ficha = await PrimeraFichaAsync(periodo.Id);

        Assert.True(await _service.CerrarPeriodoAsync(periodo.Id, "jefe"));

        var cerrado = await _service.GetPeriodoAsync(periodo.Id);
        Assert.True(cerrado!.Cerrado);

        var exCalculo = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CalcularAsync(periodo.Id, "tester"));
        Assert.Contains("cerrado", exCalculo.Message);

        var exCaptura = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CapturarAsync(periodo.Id, ficha.CodProveedor,
                new EvaluacionCapturaDto { Observaciones = "tarde" }, "comprador"));
        Assert.Contains("cerrado", exCaptura.Message);
    }

    [SkippableFact]
    public async Task Cerrar_SinEvaluaciones_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CerrarPeriodoAsync(periodo.Id, "jefe"));
        Assert.Contains("sin evaluaciones", ex.Message);
    }

    // ── Catálogo ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Criterios_LaSemillaSuma100()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var criterios = await _service!.GetCriteriosAsync();
        var suma = criterios.Sum(c => c.Peso);

        Assert.Equal(100m, suma);
    }

    [SkippableFact]
    public async Task Clases_CubrenTodoElRangoDePuntaje()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var clases = await _service!.GetClasesAsync();
        Skip.If(clases.Count == 0, "La empresa de pruebas no tiene escala de clases sembrada.");

        // Ningún puntaje entre 0 y 100 puede quedar sin clase: siempre hay una con desde <= puntaje.
        for (var p = 0m; p <= 100m; p += 5m)
        {
            Assert.Contains(clases, c => c.PuntajeDesde <= p);
        }
    }

    // ── Catálogo de criterios y clases (F3) ──────────────────────────────────

    [SkippableFact]
    public async Task CrearCriterio_ManualIgnoraLaMetrica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var creado = await _service!.CrearCriterioAsync(new EvaluacionCriterioUpsertDto
        {
            Codigo = "test-serv",              // se normaliza a mayúsculas
            Nombre = "Criterio manual de prueba",
            Peso = 5m,
            Origen = OrigenCriterioEvaluacion.Manual,
            Metrica = MetricaEvaluacion.Entrega,   // se ignora: es manual
            Activo = true
        }, "tester");

        Assert.Equal("TEST-SERV", creado.Codigo);
        Assert.True(creado.EsManual);
        Assert.Null(creado.Metrica);
    }

    [SkippableFact]
    public async Task CrearCriterio_AutomaticoSinMetrica_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearCriterioAsync(new EvaluacionCriterioUpsertDto
            {
                Codigo = "TEST-AUTO",
                Nombre = "Automático sin métrica",
                Peso = 5m,
                Origen = OrigenCriterioEvaluacion.Automatico
            }, "tester"));

        Assert.Contains("necesita una métrica", ex.Message);
    }

    [SkippableFact]
    public async Task CrearCriterio_MetricaDuplicada_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        // La semilla ya trae un criterio activo con la métrica ENTREGA.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearCriterioAsync(new EvaluacionCriterioUpsertDto
            {
                Codigo = "TEST-DUP",
                Nombre = "Otra entrega",
                Peso = 5m,
                Origen = OrigenCriterioEvaluacion.Automatico,
                Metrica = MetricaEvaluacion.Entrega,
                Activo = true
            }, "tester"));

        Assert.Contains("ya la usa", ex.Message);
    }

    [SkippableFact]
    public async Task CrearCriterio_CodigoRepetido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearCriterioAsync(new EvaluacionCriterioUpsertDto
            {
                Codigo = "SERVICIO",           // ya está en la semilla
                Nombre = "Duplicado",
                Peso = 5m,
                Origen = OrigenCriterioEvaluacion.Manual
            }, "tester"));

        Assert.Contains("Ya existe un criterio", ex.Message);
    }

    [SkippableFact]
    public async Task ActualizarCriterio_CambiaElPesoYSeVeEnElCalculo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var criterios = await _service!.GetCriteriosAsync();
        var documento = criterios.Single(c =>
            string.Equals(c.Metrica, MetricaEvaluacion.Documento, StringComparison.OrdinalIgnoreCase));

        await _service.ActualizarCriterioAsync(documento.Id, new EvaluacionCriterioUpsertDto
        {
            Codigo = documento.Codigo,
            Nombre = documento.Nombre,
            Peso = 30m,                       // era 10
            Origen = documento.Origen,
            Metrica = documento.Metrica,
            Orden = documento.Orden,
            Activo = true
        }, "tester");

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ficha = await PrimeraFichaAsync(periodo.Id);
        var renglon = ficha.Criterios.Single(c => c.CriterioCodigo == documento.Codigo);

        // El snapshot del detalle guarda el peso NUEVO, no el de la semilla.
        Assert.Equal(30m, renglon.Peso);
    }

    [SkippableFact]
    public async Task EliminarCriterio_YaUsado_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var criterios = await _service.GetCriteriosAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.EliminarCriterioAsync(criterios[0].Id));

        Assert.Contains("desactívelo en vez de borrarlo", ex.Message);
    }

    [SkippableFact]
    public async Task CrearClase_RangoSolapado_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var clases = await _service!.GetClasesAsync();
        Skip.If(clases.Count == 0, "Sin escala sembrada.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CrearClaseAsync(new EvaluacionClaseUpsertDto
            {
                Codigo = "TEST-X",
                Nombre = "Solapada",
                PuntajeDesde = 95m,   // cae dentro de A (90–100)
                PuntajeHasta = 99m,
                Activo = true
            }));

        Assert.Contains("se solapa", ex.Message);
    }

    [SkippableFact]
    public async Task CrearClase_RangoInvertido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearClaseAsync(new EvaluacionClaseUpsertDto
            {
                Codigo = "TEST-Y",
                Nombre = "Invertida",
                PuntajeDesde = 50m,
                PuntajeHasta = 20m,
                Activo = true
            }));

        Assert.Contains("no puede ser menor", ex.Message);
    }

    [SkippableFact]
    public async Task Catalogo_TraeLosInactivosYGetCriteriosNo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var inactivo = await _service!.CrearCriterioAsync(new EvaluacionCriterioUpsertDto
        {
            Codigo = "TEST-OFF",
            Nombre = "Criterio apagado",
            Peso = 0m,
            Origen = OrigenCriterioEvaluacion.Manual,
            Activo = false
        }, "tester");

        var catalogo = await _service.GetCriteriosCatalogoAsync();
        var activos = await _service.GetCriteriosAsync();

        Assert.Contains(catalogo, c => c.Id == inactivo.Id);
        Assert.DoesNotContain(activos, c => c.Id == inactivo.Id);
    }

    // ── Datos de impresión (F5) ──────────────────────────────────────────────

    [SkippableFact]
    public async Task DatosFichaImpresion_TraenEmpresaFichaYNota()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var ranking = await _service.GetRankingAsync(periodo.Id);
        var datos = await _service.GetDatosFichaImpresionAsync(
            periodo.Id, ranking[0].CodProveedor, "tester");

        Assert.NotNull(datos);
        Assert.False(string.IsNullOrWhiteSpace(datos!.EmpresaNombre));   // membrete resuelto
        Assert.Equal(ranking[0].CodProveedor, datos.Ficha.CodProveedor);
        Assert.Equal("tester", datos.ImpresoPor);

        // La nota explica los criterios que no puntuaron; sólo aparece si los hay.
        if (datos.Ficha.CriteriosSinDatos > 0)
        {
            Assert.False(string.IsNullOrWhiteSpace(datos.NotaCriteriosSinDatos));
        }
        else
        {
            Assert.Null(datos.NotaCriteriosSinDatos);
        }
    }

    [SkippableFact]
    public async Task DatosFichaImpresion_ProveedorSinEvaluacion_DevuelveNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        Assert.Null(await _service!.GetDatosFichaImpresionAsync(periodo.Id, "NO-EXISTE", "tester"));
    }

    [SkippableFact]
    public async Task DatosComparativoImpresion_TraenItemsPromedioYCriterios()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        var resultado = await _service!.CalcularAsync(periodo.Id, "tester");
        Skip.If(resultado.Evaluados == 0, "Sin recepciones en el rango.");

        var datos = await _service.GetDatosComparativoImpresionAsync(periodo.Id, null, "tester");

        Assert.NotNull(datos);
        Assert.Equal(periodo.Codigo, datos!.PeriodoCodigo);
        Assert.Equal(resultado.Evaluados, datos.Evaluados);
        Assert.Equal(resultado.PromedioPuntaje, datos.PromedioPuntaje);
        Assert.NotEmpty(datos.Criterios);

        // El desglose que imprime el cuadro se arma en el DTO, no en el reporte.
        Assert.All(datos.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.DesgloseTexto)));
        Assert.All(datos.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.PuntajeTexto)));
    }

    [SkippableFact]
    public async Task DatosComparativoImpresion_ConFiltro_DescribeElFiltroEnElPapel()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        var periodo = await CrearPeriodoAsync();
        await _service!.CalcularAsync(periodo.Id, "tester");

        var datos = await _service.GetDatosComparativoImpresionAsync(
            periodo.Id, new EvaluacionFilterDto { ClaseCodigo = "A", ComprasMinimas = 1000m }, "tester");

        Assert.NotNull(datos!.FiltroTexto);
        Assert.Contains("clase A", datos.FiltroTexto!);
        Assert.Contains("compras desde", datos.FiltroTexto!);
    }

    [SkippableFact]
    public async Task DatosComparativoImpresion_PeriodoInexistente_DevuelveNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ExigirCatalogoAsync();

        Assert.Null(await _service!.GetDatosComparativoImpresionAsync(int.MaxValue, null, "tester"));
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    /// <summary>
    /// El catálogo lo siembra el script de F0. Sin él no hay nada que probar, así que el test se
    /// omite con un mensaje claro en vez de fallar por una base sin migrar.
    /// </summary>
    private async Task ExigirCatalogoAsync()
    {
        var criterios = await Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM prv_evaluacion_criterio WHERE company_id = @c",
            new { c = CompanyId }, Transaction);

        Skip.If(criterios == 0,
            "Falta aplicar Database/2026-08-14_prv_evaluacion.sql (catálogo de criterios vacío).");
    }

    /// <summary>Período que abarca un año hacia atrás: agarra las compras que haya en la base.</summary>
    private async Task<EvaluacionPeriodoDto> CrearPeriodoAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        return await _service!.CrearPeriodoAsync(new EvaluacionPeriodoUpsertDto
        {
            Codigo = CodigoPeriodo,
            Nombre = "Período de prueba",
            FechaDesde = hoy.AddYears(-1),
            FechaHasta = hoy
        }, "tester");
    }

    private async Task<EvaluacionFichaDto> PrimeraFichaAsync(int periodoId)
    {
        var ranking = await _service!.GetRankingAsync(periodoId);
        var ficha = await _service.GetFichaAsync(periodoId, ranking[0].CodProveedor);
        Assert.NotNull(ficha);
        return ficha!;
    }

    /// <summary>Tenant fijo de la prueba (mismo patrón que el resto de la suite).</summary>
    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
