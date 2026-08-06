using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Tenancy;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Configuración del ISV en compras a nivel de empresa (cfg_compra_isv): tratamiento
/// COSTO / FISCAL, una fila por empresa (la PK ES el company_id).
/// </summary>
[Collection("Postgres")]
public class IsvCompraConfigTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IsvCompraConfigService? _service;

    public IsvCompraConfigTests(PostgresFixture fixture) : base(fixture)
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
            _service = new IsvCompraConfigService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Obtener_SinConfig_DevuelveCostoPorDefecto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Aseguramos que no haya fila para la empresa actual DENTRO de esta transacción.
        var existentes = await _context!.cfg_compra_isvs.ToListAsync();
        _context.cfg_compra_isvs.RemoveRange(existentes);
        await _context.SaveChangesAsync();

        var cfg = await _service!.ObtenerAsync();
        Assert.Equal(TratamientoIsvCompra.Costo, cfg.Tratamiento);
    }

    [SkippableFact]
    public async Task Guardar_Fiscal_Y_Obtener_LaDevuelve()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GuardarAsync(new IsvCompraConfigDto { Tratamiento = TratamientoIsvCompra.Fiscal }, "tester");

        var cfg = await _service.ObtenerAsync();
        Assert.Equal(TratamientoIsvCompra.Fiscal, cfg.Tratamiento);
    }

    [SkippableFact]
    public async Task Guardar_AlternaTratamiento()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GuardarAsync(new IsvCompraConfigDto { Tratamiento = TratamientoIsvCompra.Fiscal }, "tester");
        Assert.Equal(TratamientoIsvCompra.Fiscal, (await _service.ObtenerAsync()).Tratamiento);

        await _service.GuardarAsync(new IsvCompraConfigDto { Tratamiento = TratamientoIsvCompra.Costo }, "tester");
        Assert.Equal(TratamientoIsvCompra.Costo, (await _service.ObtenerAsync()).Tratamiento);
    }

    [SkippableFact]
    public async Task Guardar_NormalizaMinusculas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GuardarAsync(new IsvCompraConfigDto { Tratamiento = "fiscal" }, "tester");
        Assert.Equal(TratamientoIsvCompra.Fiscal, (await _service.ObtenerAsync()).Tratamiento);
    }

    [SkippableFact]
    public async Task Guardar_ValorInvalido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.GuardarAsync(new IsvCompraConfigDto { Tratamiento = "OTRO" }, "tester"));
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
