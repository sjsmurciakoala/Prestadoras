using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Services.Cobranza;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class ClientesCobroTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CobranzaService? _service;

    public ClientesCobroTests(PostgresFixture fixture) : base(fixture)
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

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);
            _context.Database.UseTransaction(Transaction);

            _service = new CobranzaService(_context, mockCompanyService, new Fakes.FakeDocumentoCobranzaGenerator());
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task ListarClientesCobro_ConValorMinimo_FiltraDeudoresBajos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var result = await _service!.ListarClientesCobroAsync(
            new ClienteCobroFiltroDto { ValorMinimo = 1m });

        Assert.All(result, c => Assert.True(c.SaldoAdeudado >= 1m));
    }

    [SkippableFact]
    public async Task ListarClientesCobro_ExcluirBloqueados_NoIncluyeBloqueados()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var result = await _service!.ListarClientesCobroAsync(
            new ClienteCobroFiltroDto { ExcluirBloqueados = true });

        Assert.All(result, c => Assert.False(c.Bloqueado));
    }

    [SkippableFact]
    public async Task RegistrarAccionLote_CreaUnaAccionPorCliente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var claves = await ObtenerClavesConDeudaAsync(2);
        Skip.If(claves.Count == 0, "No hay clientes con deuda en la BD de prueba");

        var request = new RegistrarAccionLoteRequest(
            Claves: claves,
            CodAccion: 1,
            CodObservacion: null,
            AbogadoId: null,
            Observacion: "Prueba lote",
            EjecutadoPor: "tester");

        var creadas = await _service!.RegistrarAccionLoteAsync(request, "tester");

        Assert.Equal(claves.Count, creadas);

        var registradas = await _context!.cln_accion_cobranzas
            .CountAsync(a => claves.Contains(a.codigocliente) && a.observacion == "Prueba lote");
        Assert.Equal(claves.Count, registradas);
    }

    [SkippableFact]
    public async Task GenerarCartas_PersisteLoteConCorrelativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var claves = await ObtenerClavesConDeudaAsync(1);
        Skip.If(claves.Count == 0, "No hay clientes con deuda en la BD de prueba");

        var hdr = await _service!.GenerarCartasCobroAsync(
            new GenerarCartasCobroRequest(claves), "tester");

        Assert.True(hdr.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(hdr.Correlativo));
        Assert.Equal(1, hdr.TotalClientes);

        var lote = await _service.ObtenerCartaLoteAsync(hdr.Id);
        Assert.NotNull(lote);
        Assert.Single(lote!.Clientes);
        Assert.False(string.IsNullOrWhiteSpace(lote.Empresa.NombreComercial));
    }

    [SkippableFact]
    public async Task GenerarRequerimiento_PersistePlazoHoras()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var claves = await ObtenerClavesConDeudaAsync(1);
        Skip.If(claves.Count == 0, "No hay clientes con deuda en la BD de prueba");

        var hdr = await _service!.GenerarCartasCobroAsync(
            new GenerarCartasCobroRequest(claves, PlazoHoras: 48), "tester");

        Assert.Equal(48, hdr.PlazoHoras);

        var lote = await _service.ObtenerCartaLoteAsync(hdr.Id);
        Assert.NotNull(lote);
        Assert.Equal(48, lote!.Encabezado.PlazoHoras);
    }

    private async Task<System.Collections.Generic.List<string>> ObtenerClavesConDeudaAsync(int cantidad)
    {
        var candidatos = await _service!.ListarClientesCobroAsync(
            new ClienteCobroFiltroDto { ValorMinimo = 1m });

        return candidatos
            .Select(c => c.Clave)
            .Distinct()
            .Take(cantidad)
            .ToList();
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
