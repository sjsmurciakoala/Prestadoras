using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Cobranza;
using SIAD.Tests.Infrastructure;
using Xunit;

namespace SIAD.Tests;

[Collection("Postgres")]
public class CobranzaHistorialTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ICobranzaService? _service;

    public CobranzaHistorialTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var companyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, companyService);
            _context.Database.UseTransaction(Transaction);
            _service = new CobranzaService(_context, companyService, new Fakes.FakeDocumentoCobranzaGenerator());
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task SembrarAccionesAsync()
    {
        // Catálogo temporal para satisfacer el FK de cod_accion (rollback al final)
        await Connection.ExecuteAsync(
            "INSERT INTO axl_accion_cobranza (cod_accion, nombre, activo) " +
            "VALUES (9901, 'TEST LLAMADA', true), (9902, 'TEST VISITA', true);",
            transaction: Transaction);

        // Resultado de catálogo (id generado por identidad) para verificar la subconsulta de proyección
        var codObservacion = await Connection.ExecuteScalarAsync<int>(
            "INSERT INTO axl_observacion_cobranza (observacion, activo) " +
            "VALUES ('TEST RESULTADO', true) RETURNING id;",
            transaction: Transaction);

        await Connection.ExecuteAsync(
            """
            INSERT INTO cln_accion_cobranza
                (company_id, codigocliente, fecha, accion, cod_accion, cod_observacion, observacion, ejecutado_por)
            VALUES
                (@cid, 'TEST-HIST-001', @enRango1,   'TEST LLAMADA', 9901, @codObs, 'en rango',    'tester-hist'),
                (@cid, 'TEST-HIST-001', @enRango2,   'TEST VISITA',  9902, NULL,    'otro tipo',   'tester-hist'),
                (@cid, 'TEST-HIST-001', @bordeHasta, 'TEST LLAMADA', 9901, NULL,    'borde hasta', 'tester-hist'),
                (@cid, 'TEST-HIST-001', @fueraRango, 'TEST LLAMADA', 9901, NULL,    'fuera',       'otro-usuario');
            """,
            new
            {
                cid = CompanyId,
                codObs = codObservacion,
                enRango1 = new DateTime(2099, 6, 5, 10, 0, 0),
                enRango2 = new DateTime(2099, 6, 6, 11, 0, 0),
                bordeHasta = new DateTime(2099, 6, 30, 23, 59, 0), // tarde en el día límite 'hasta'
                fueraRango = new DateTime(2099, 1, 1, 9, 0, 0)
            },
            Transaction);
    }

    [SkippableFact]
    public async Task Historial_FiltraPorRangoDeFechasYTipoDeAccion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await SembrarAccionesAsync();

        var todas = await _service!.ListarHistorialAccionesAsync(
            new DateTime(2099, 6, 1), new DateTime(2099, 6, 30),
            codAccion: null, clienteClave: "TEST-HIST-001", ejecutadoPor: null);

        var soloLlamadas = await _service.ListarHistorialAccionesAsync(
            new DateTime(2099, 6, 1), new DateTime(2099, 6, 30),
            codAccion: 9901, clienteClave: "TEST-HIST-001", ejecutadoPor: null);

        Assert.Equal(3, todas.Count);                    // la de enero queda fuera
        Assert.True(todas[0].Fecha >= todas[1].Fecha);   // orden descendente
        Assert.Equal("borde hasta", todas[0].Observacion); // 23:59 del día 'hasta' sí entra (límite inclusivo)

        Assert.Equal(2, soloLlamadas.Count);
        Assert.Contains(soloLlamadas, r => r.Observacion == "en rango");
        Assert.Contains(soloLlamadas, r => r.Observacion == "borde hasta");

        // La subconsulta de proyección resuelve el texto del catálogo axl_observacion_cobranza
        var enRango = Assert.Single(soloLlamadas, r => r.Observacion == "en rango");
        Assert.Equal("TEST RESULTADO", enRango.NombreObservacion);
    }

    [SkippableFact]
    public async Task Historial_FiltraPorEjecutadoPorParcial()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await SembrarAccionesAsync();

        var resultado = await _service!.ListarHistorialAccionesAsync(
            new DateTime(2099, 1, 1), new DateTime(2099, 12, 31),
            codAccion: null, clienteClave: "TEST-HIST-001", ejecutadoPor: "TESTER");

        Assert.Equal(3, resultado.Count);                // match parcial e insensible a mayúsculas
        Assert.All(resultado, r => Assert.Equal("tester-hist", r.EjecutadoPor));
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
