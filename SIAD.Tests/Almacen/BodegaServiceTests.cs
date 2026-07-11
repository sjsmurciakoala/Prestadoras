using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

[Collection("Postgres")]
public class BodegaServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IBodegaService? _service;

    public BodegaServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        // First run the base initialization to setup Connection and Transaction
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);

            // EF Core needs to use the transaction initiated by IntegrationTestBase
            _context.Database.UseTransaction(Transaction);

            _service = new BodegaService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Create_CodigoDuplicado_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.CreateAsync(new BodegaEditDto { Codigo = "B1", Nombre = "Central" }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateAsync(new BodegaEditDto { Codigo = "B1", Nombre = "Otra" }, "tester"));
    }

    [SkippableFact]
    public async Task Create_Valido_PersisteYNormalizaCodigo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CreateAsync(new BodegaEditDto { Codigo = "b2", Nombre = "Secundaria" }, "tester");

        Assert.NotNull(creado.Id);

        // El código se normaliza a mayúsculas al persistir; GetAsync lo lee de la BD ya normalizado.
        var lista = await _service.GetAsync(new ClasificacionFilterDto { Search = "B2" });
        var item = Assert.Single(lista);
        Assert.Equal("B2", item.Codigo);
        Assert.Equal("Secundaria", item.Nombre);

        // También verificable vía GetByIdAsync.
        var porId = await _service.GetByIdAsync(creado.Id!.Value);
        Assert.NotNull(porId);
        Assert.Equal("B2", porId!.Codigo);
    }

    [SkippableFact]
    public async Task Update_CambiaNombre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CreateAsync(new BodegaEditDto { Codigo = "B3", Nombre = "Nombre viejo" }, "tester");
        var id = creado.Id!.Value;

        await _service.UpdateAsync(id, new BodegaEditDto { Codigo = "B3", Nombre = "Nombre nuevo", Activo = true }, "tester");

        var actualizado = await _service.GetByIdAsync(id);
        Assert.NotNull(actualizado);
        Assert.Equal("Nombre nuevo", actualizado!.Nombre);
    }

    [SkippableFact]
    public async Task Deactivate_MarcaInactivoYFueraDelLookup()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CreateAsync(new BodegaEditDto { Codigo = "B4", Nombre = "Para desactivar" }, "tester");
        var id = creado.Id!.Value;

        var lookupAntes = await _service.GetLookupAsync();
        Assert.Contains(lookupAntes, l => l.Id == id);

        var resultado = await _service.DeactivateAsync(id, "tester");
        Assert.True(resultado);

        var lookupDespues = await _service.GetLookupAsync();
        Assert.DoesNotContain(lookupDespues, l => l.Id == id);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
