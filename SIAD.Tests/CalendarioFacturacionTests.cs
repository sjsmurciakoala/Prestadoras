using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Facturacion;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Facturacion;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase A del plan apertura-ciclo-único (2026-07-14): calendario de facturación
// (calendariopro multitenant). Verifica el ABM por año del servicio, la copia
// de año con desplazamiento de fechas, el scope por empresa y que los SPs del
// motor V3 quedaron repuntados con el filtro company_id.
[Collection("Postgres")]
public sealed class CalendarioFacturacionTests : IntegrationTestBase, IDisposable
{
    // Año improbable en datos reales para no chocar con calendario cargado.
    private const int Anio = 2091;

    private SiadDbContext? _context;

    public CalendarioFacturacionTests(PostgresFixture fixture) : base(fixture) { }

    public void Dispose() => _context?.Dispose();

    private CalendarioFacturacionService CrearServicio(long? companyId = null)
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new CompanyFija(companyId ?? CompanyId));
        _context.Database.UseTransaction(Transaction);
        return new CalendarioFacturacionService(_context);
    }

    private static CalendarioCicloDto Fila(int mes, string ciclo, int diaLectura, int? diasVence = 5) => new()
    {
        Mes = mes,
        Ciclo = ciclo,
        FechaLectura = new DateTime(Anio, mes, diaLectura),
        FechaFacturacion = new DateTime(Anio, mes, diaLectura),
        FechaVencimiento = new DateTime(Anio, mes, diaLectura).AddDays(diasVence ?? 5),
        DiasVencimiento = diasVence,
    };

    [SkippableFact]
    public async Task Guardar_y_obtener_normaliza_ciclo_y_hace_roundtrip()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        // '1' entra sin cero (formato SIMAFI) y debe guardarse normalizado '01'.
        var resultado = await servicio.GuardarAnioAsync(CompanyId, Anio,
            new List<CalendarioCicloDto> { Fila(7, "1", 15), Fila(7, "02", 16) }, "test-cal");

        Assert.Equal(2, resultado.Filas.Count);
        var c1 = resultado.Filas.Single(f => f.Ciclo == "01");
        Assert.Equal(new DateTime(Anio, 7, 15), c1.FechaLectura);
        Assert.Equal(new DateTime(Anio, 7, 20), c1.FechaVencimiento);
        Assert.Equal(5, c1.DiasVencimiento);
        Assert.True(c1.Ide > 0, "La fila nueva debe recibir su ide real.");

        // Reedición: cambia una, borra la otra.
        c1.FechaVencimiento = new DateTime(Anio, 7, 25);
        var editado = await servicio.GuardarAnioAsync(CompanyId, Anio,
            new List<CalendarioCicloDto> { c1 }, "test-cal");

        var unica = Assert.Single(editado.Filas);
        Assert.Equal("01", unica.Ciclo);
        Assert.Equal(new DateTime(Anio, 7, 25), unica.FechaVencimiento);
    }

    [SkippableFact]
    public async Task Guardar_rechaza_ciclo_duplicado_en_el_mes()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        // '1' y '01' normalizan al mismo ciclo → duplicado.
        var filas = new List<CalendarioCicloDto> { Fila(3, "1", 10), Fila(3, "01", 11) };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GuardarAnioAsync(CompanyId, Anio, filas, "test-cal"));
    }

    [SkippableFact]
    public async Task Guardar_rechaza_fila_sin_ninguna_fecha()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        // Sin fechas la fila no genera eventos y quedaría invisible e
        // ineditable en el calendario (única superficie de edición).
        var fila = new CalendarioCicloDto { Mes = 5, Ciclo = "01", DiasVencimiento = 5 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GuardarAnioAsync(CompanyId, Anio, new List<CalendarioCicloDto> { fila }, "test-cal"));
    }

    [SkippableFact]
    public async Task Guardar_permite_borrar_un_ciclo_y_renombrar_otro_a_su_codigo()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        // Escenario del índice único: existe '01' y '02'; se borra '01' y se
        // renombra '02'→'01' en un solo guardado. Sin el guardado en dos fases
        // (borrados primero) EF podía emitir el UPDATE antes del DELETE y
        // chocar con ux_calendariopro_company_periodo.
        var inicial = await servicio.GuardarAnioAsync(CompanyId, Anio,
            new List<CalendarioCicloDto> { Fila(8, "01", 4), Fila(8, "02", 5) }, "test-cal");

        var renombrada = inicial.Filas.Single(f => f.Ciclo == "02");
        renombrada.Ciclo = "01";

        var resultado = await servicio.GuardarAnioAsync(CompanyId, Anio,
            new List<CalendarioCicloDto> { renombrada }, "test-cal");

        var unica = Assert.Single(resultado.Filas);
        Assert.Equal("01", unica.Ciclo);
        Assert.Equal(renombrada.Ide, unica.Ide);
    }

    [SkippableFact]
    public async Task Guardar_rechaza_vencimiento_anterior_a_facturacion()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var fila = Fila(4, "01", 10);
        fila.FechaVencimiento = fila.FechaFacturacion!.Value.AddDays(-1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GuardarAnioAsync(CompanyId, Anio, new List<CalendarioCicloDto> { fila }, "test-cal"));
    }

    [SkippableFact]
    public async Task Copiar_anio_desplaza_fechas_y_protege_destino_con_datos()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        await servicio.GuardarAnioAsync(CompanyId, Anio,
            new List<CalendarioCicloDto> { Fila(2, "01", 27) }, "test-cal");

        var copia = await servicio.CopiarAnioAsync(CompanyId, Anio, Anio + 1, "test-cal");

        var fila = Assert.Single(copia.Filas);
        Assert.Equal(Anio + 1, fila.Anio);
        Assert.Equal(new DateTime(Anio + 1, 2, 27), fila.FechaLectura);
        // 27-feb + 5 días cruza a marzo y debe conservar día/mes en el destino.
        Assert.Equal(new DateTime(Anio + 1, 3, 4), fila.FechaVencimiento);

        // El destino ya tiene filas → segunda copia rechazada.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.CopiarAnioAsync(CompanyId, Anio, Anio + 1, "test-cal"));
    }

    [Fact]
    public void Desplazar_ajusta_29_febrero_a_28_en_anio_no_bisiesto()
    {
        // 2092 es bisiesto; 2093 no → 29-feb cae a 28-feb.
        var resultado = CalendarioFacturacionService.Desplazar(new DateOnly(2092, 2, 29), 2092, 2093);
        Assert.Equal(new DateOnly(2093, 2, 28), resultado);
    }

    [Fact]
    public void Desplazar_preserva_el_arrastre_de_anio_del_vencimiento_de_diciembre()
    {
        // Ciclo de diciembre 2091 que vence en enero 2092: al copiar 2091→2092
        // el vencimiento debe caer en enero 2093 (destino + 1), no en enero 2092.
        var resultado = CalendarioFacturacionService.Desplazar(new DateOnly(2092, 1, 3), 2091, 2092);
        Assert.Equal(new DateOnly(2093, 1, 3), resultado);
    }

    [SkippableFact]
    public async Task Calendario_es_multitenant_otra_empresa_no_ve_las_filas()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        await servicio.GuardarAnioAsync(CompanyId, Anio,
            new List<CalendarioCicloDto> { Fila(6, "01", 5) }, "test-cal");
        _context!.Dispose();

        // Otra empresa (id inexistente basta: el filtro global es por claim).
        var otraCompany = CompanyId + 991;
        var servicioOtra = CrearServicio(otraCompany);

        var ajeno = await servicioOtra.ObtenerAnioAsync(otraCompany, Anio);
        Assert.Empty(ajeno.Filas);
    }

    [SkippableFact]
    public async Task SPs_del_motor_leen_calendariopro_con_filtro_company_id()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        const string sql = @"
            SELECT p.proname, pg_get_functiondef(p.oid) AS def
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public'
              AND p.proname IN ('sp_lectura_v3', 'sp_adm_calcular_factura_lectura')";

        var defs = (await Connection.QueryAsync<(string proname, string def)>(
            new CommandDefinition(sql, transaction: Transaction))).ToList();

        Assert.Equal(2, defs.Count);
        foreach (var (proname, def) in defs)
        {
            var idx = def.IndexOf("FROM public.calendariopro", StringComparison.OrdinalIgnoreCase);
            Assert.True(idx >= 0, $"{proname} ya no lee calendariopro — revisar el repunte de la Fase A.");

            var bloque = def.Substring(idx, Math.Min(400, def.Length - idx));
            Assert.Contains("cp.company_id = p_company_id", bloque);
            // Match tolerante '1' vs '01' (SIMAFI sin cero a la izquierda).
            Assert.Contains("cp.ciclo::int", bloque);
        }
    }

    [SkippableFact]
    public async Task Calendariopro_tiene_company_id_not_null_y_unique_tenant_safe()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        const string sql = @"
            SELECT
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'calendariopro'
                      AND column_name = 'company_id' AND is_nullable = 'NO'
                ) AS company_not_null,
                EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE schemaname = 'public' AND tablename = 'calendariopro'
                      AND indexname = 'ux_calendariopro_company_periodo'
                ) AS unique_periodo";

        var row = await Connection.QueryFirstAsync<(bool company_not_null, bool unique_periodo)>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.True(row.company_not_null, "calendariopro.company_id debe ser NOT NULL (regla multitenant).");
        Assert.True(row.unique_periodo, "Falta el UNIQUE (company_id, ano, mes, ciclo) del calendario.");
    }

    private sealed class CompanyFija : ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
