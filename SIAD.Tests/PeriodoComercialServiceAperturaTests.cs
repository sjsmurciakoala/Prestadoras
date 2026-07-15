using Microsoft.EntityFrameworkCore;
using Dapper;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.PeriodosComerciales;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase B: atraviesa el SERVICIO C# completo (Dapper → SP → jsonb snake_case →
// AperturaCicloResumenDto), no solo el SP. Cubre la deserialización real del
// resumen (DateOnly, listas anidadas, avisos) que la UI consume.
[Collection("Postgres")]
public sealed class PeriodoComercialServiceAperturaTests : IntegrationTestBase, IDisposable
{
    private const int Anio = 2096;
    private const string Ciclo = "78";

    private SiadDbContext? _context;

    public PeriodoComercialServiceAperturaTests(PostgresFixture fixture) : base(fixture) { }

    public void Dispose() => _context?.Dispose();

    private PeriodoComercialService CrearServicio()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new CompanyFija(CompanyId));
        _context.Database.UseTransaction(Transaction);
        return new PeriodoComercialService(_context);
    }

    [SkippableFact]
    public async Task Servicio_abre_deserializa_resumen_y_deshace()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        // Calendario para que fecha_limite venga del calendario (DateOnly real).
        await Connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO public.calendariopro (company_id, ano, mes, ciclo, fechalec, fechafac, fechavence, diasvence)
              VALUES (@CompanyId, @Anio, 6, @Ciclo, make_date(@Anio, 6, 10), make_date(@Anio, 6, 10), make_date(@Anio, 6, 15), 5)",
            new { CompanyId, Anio, Ciclo }, Transaction));

        // Planilla previa para que la apertura haga roll-over.
        await Connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO public.historicomedicion
                  (company_id, ano, mes, ciclo, ruta, secuencia, clave, propietario, fecha, usuario, lect_ant, lect_act, consumo, consumoant)
              VALUES (@CompanyId, @Anio, 5, @Ciclo, '00T2', '001', 'T-SVC-1', 'CLIENTE SVC', current_date, 'lector-test', 1, 42, 41, 0)",
            new { CompanyId, Anio, Ciclo }, Transaction));

        // Preview (fn, sin escribir): DTO completo deserializado.
        var preview = await servicio.PreviewAperturaAsync(CompanyId, Anio, 6, Ciclo);
        Assert.Null(preview.Bloqueo);
        Assert.Equal("ROLL_OVER", preview.OrigenPlanilla);
        Assert.Equal(1, preview.ClientesPlanilla);
        Assert.Equal(new DateOnly(Anio, 6, 10), preview.FechaLimite);
        Assert.Null(preview.PeriodoCicloId);

        // Apertura real por el servicio.
        var resumen = await servicio.AbrirAsync(CompanyId, Anio, 6, Ciclo, "test-servicio");
        Assert.Equal(Ciclo, resumen.Ciclo);
        Assert.Equal("ROLL_OVER", resumen.OrigenPlanilla);
        Assert.Equal(1, resumen.ClientesPlanilla);
        Assert.Equal(new DateOnly(Anio, 6, 10), resumen.FechaLimite);
        Assert.NotNull(resumen.PeriodoCicloId);
        Assert.DoesNotContain("SIN_CALENDARIO", resumen.Avisos);

        // Deshacer por el servicio.
        var deshecho = await servicio.DeshacerAperturaAsync(CompanyId, resumen.PeriodoCicloId!.Value, "test-servicio");
        Assert.Equal(1, deshecho.PlanillaEliminada);
        Assert.True(deshecho.CicloEliminado);
        Assert.True(deshecho.PeriodoEliminado);
    }

    [SkippableFact]
    public async Task Servicio_sugerencia_devuelve_el_proximo_ciclo_del_calendario()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var sugerencia = await servicio.SugerenciaAperturaAsync(CompanyId);

        // La BD local tiene el calendario 2016-2026 cargado (Fase A): siempre
        // hay sugerencia y su ciclo viene normalizado a 2 dígitos.
        Assert.NotNull(sugerencia);
        Assert.InRange(sugerencia!.Mes, 1, 12);
        Assert.Equal(2, sugerencia.Ciclo.Length);
        Assert.NotNull(sugerencia.FechaLectura);
    }

    private sealed class CompanyFija : ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
