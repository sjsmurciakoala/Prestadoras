using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Services.Clientes;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class ClientesServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IClientesService? _service;

    public ClientesServiceTests(PostgresFixture fixture) : base(fixture)
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
            
            _service = new ClientesService(_context, mockCompanyService);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Test_GetMovimientosPagedAsync_Succeeds()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Act
        var result = await _service!.GetMovimientosPagedAsync(
            clienteId: 102814, // Using ID with actual movements in DB
            skip: 0,
            take: 20,
            sortField: null,
            sortDesc: false);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.Items.Count > 0, "Debe retornar al menos un movimiento.");
        System.Diagnostics.Debug.WriteLine($"Items Count: {result.Items.Count}");
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
