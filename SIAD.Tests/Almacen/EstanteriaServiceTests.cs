using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

[Collection("Postgres")]
public class EstanteriaServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IEstanteriaService? _service;

    public EstanteriaServiceTests(PostgresFixture fixture) : base(fixture)
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

            _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
            _context.Database.UseTransaction(Transaction);
            _service = new EstanteriaService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // Inserta una bodega directamente y devuelve su id (company_id lo estampa el contexto).
    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();
        return bodega.id;
    }

    [SkippableFact]
    public async Task Create_BodegaInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CreateAsync(new EstanteriaEditDto { BodegaId = 999999, Codigo = "E1" }, "tester"));
    }

    [SkippableFact]
    public async Task Create_Valido_Persiste()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodegaId = await SeedBodegaAsync("SB1");
        var creado = await _service!.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaId, Codigo = "e1", Nombre = "Pasillo A" }, "tester");

        Assert.NotNull(creado.Id);

        var lista = await _service.GetAsync(new UbicacionFilterDto { BodegaId = bodegaId });
        var item = Assert.Single(lista);
        Assert.Equal("E1", item.Codigo);
        Assert.Equal(bodegaId, item.BodegaId);
    }

    [SkippableFact]
    public async Task Create_CodigoDuplicadoEnMismaBodega_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodegaId = await SeedBodegaAsync("SB2");
        await _service!.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaId, Codigo = "E1" }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaId, Codigo = "E1" }, "tester"));
    }

    [SkippableFact]
    public async Task Create_MismoCodigoEnOtraBodega_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodegaA = await SeedBodegaAsync("SB3");
        var bodegaB = await SeedBodegaAsync("SB4");

        await _service!.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaA, Codigo = "E1" }, "tester");
        var enOtra = await _service.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaB, Codigo = "E1" }, "tester");

        Assert.NotNull(enOtra.Id);
    }

    [SkippableFact]
    public async Task Lookup_FiltraPorBodega()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodegaA = await SeedBodegaAsync("SB5");
        var bodegaB = await SeedBodegaAsync("SB6");
        await _service!.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaA, Codigo = "EA" }, "tester");
        await _service.CreateAsync(new EstanteriaEditDto { BodegaId = bodegaB, Codigo = "EB" }, "tester");

        var lookupA = await _service.GetLookupAsync(bodegaA);
        var item = Assert.Single(lookupA);
        Assert.Equal("EA", item.Codigo);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
