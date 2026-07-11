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
public class EstanteServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IEstanteService? _service;

    public EstanteServiceTests(PostgresFixture fixture) : base(fixture)
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
            _service = new EstanteService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // Inserta bodega + estantería directamente y devuelve el id de la estantería.
    private async Task<int> SeedEstanteriaAsync(string bodegaCodigo, string estanteriaCodigo)
    {
        var bodega = new alm_bodega { codigo = bodegaCodigo, nombre = $"Bodega {bodegaCodigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();

        var estanteria = new alm_estanteria { bodega_id = bodega.id, codigo = estanteriaCodigo, activo = true };
        _context.alm_estanterias.Add(estanteria);
        await _context.SaveChangesAsync();
        return estanteria.id;
    }

    [SkippableFact]
    public async Task Create_EstanteriaInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CreateAsync(new EstanteEditDto { EstanteriaId = 999999, Codigo = "1" }, "tester"));
    }

    [SkippableFact]
    public async Task Create_Valido_Persiste()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var estanteriaId = await SeedEstanteriaAsync("SB1", "E1");
        var creado = await _service!.CreateAsync(new EstanteEditDto { EstanteriaId = estanteriaId, Codigo = "n1", Descripcion = "Nivel 1" }, "tester");

        Assert.NotNull(creado.Id);

        var lista = await _service.GetAsync(new UbicacionFilterDto { EstanteriaId = estanteriaId });
        var item = Assert.Single(lista);
        Assert.Equal("N1", item.Codigo);
        Assert.Equal(estanteriaId, item.EstanteriaId);
    }

    [SkippableFact]
    public async Task Create_CodigoDuplicadoEnMismaEstanteria_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var estanteriaId = await SeedEstanteriaAsync("SB2", "E1");
        await _service!.CreateAsync(new EstanteEditDto { EstanteriaId = estanteriaId, Codigo = "N1" }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new EstanteEditDto { EstanteriaId = estanteriaId, Codigo = "N1" }, "tester"));
    }

    [SkippableFact]
    public async Task Lookup_ArmaUbicacionCodigoCompuesto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var estanteriaId = await SeedEstanteriaAsync("BOD", "EST");
        await _service!.CreateAsync(new EstanteEditDto { EstanteriaId = estanteriaId, Codigo = "1" }, "tester");

        var lookup = await _service.GetLookupAsync(estanteriaId);
        var item = Assert.Single(lookup);
        Assert.Equal("BOD-EST-1", item.UbicacionCodigo);
        Assert.Equal("BOD-EST-1", item.Display);
    }

    [SkippableFact]
    public async Task Lookup_FiltraPorEstanteria()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var estA = await SeedEstanteriaAsync("SB3", "EA");
        var estB = await SeedEstanteriaAsync("SB4", "EB");
        await _service!.CreateAsync(new EstanteEditDto { EstanteriaId = estA, Codigo = "1" }, "tester");
        await _service.CreateAsync(new EstanteEditDto { EstanteriaId = estB, Codigo = "2" }, "tester");

        var lookupA = await _service.GetLookupAsync(estA);
        var item = Assert.Single(lookupA);
        Assert.Equal("1", item.Codigo);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
