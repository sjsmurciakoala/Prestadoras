using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Tenancy;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// F5 del interruptor de existencia negativa: el servicio de configuración por empresa
/// (cfg_inventario_negativo) y el override por bodega (alm_bodega.permite_existencia_negativa).
/// </summary>
[Collection("Postgres")]
public class NegativoInventarioConfigTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private NegativoInventarioConfigService? _service;
    private BodegaService? _bodegas;

    public NegativoInventarioConfigTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
            _context.Database.UseTransaction(Transaction);
            _service = new NegativoInventarioConfigService(_context);
            _bodegas = new BodegaService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Obtener_SinConfig_DevuelveFalsePorDefecto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var existentes = await _context!.cfg_inventario_negativos.ToListAsync();
        _context.cfg_inventario_negativos.RemoveRange(existentes);
        await _context.SaveChangesAsync();

        Assert.False((await _service!.ObtenerAsync()).Permitir);
    }

    [SkippableFact]
    public async Task Guardar_Y_Obtener_Alterna()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GuardarAsync(new NegativoInventarioConfigDto { Permitir = true }, "tester");
        Assert.True((await _service.ObtenerAsync()).Permitir);

        await _service.GuardarAsync(new NegativoInventarioConfigDto { Permitir = false }, "tester");
        Assert.False((await _service.ObtenerAsync()).Permitir);
    }

    [SkippableFact]
    public async Task Bodega_GuardaYLeeElOverrideTriEstado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // NULL (hereda) al crear.
        var creada = await _bodegas!.CreateAsync(new BodegaEditDto
        {
            Codigo = "ZZBN1", Nombre = "Bodega negativo", Activo = true, PermiteExistenciaNegativa = null
        }, "tester");
        Assert.Null((await _bodegas.GetByIdAsync(creada.Id!.Value))!.PermiteExistenciaNegativa);

        // true (permite) al editar.
        creada.PermiteExistenciaNegativa = true;
        await _bodegas.UpdateAsync(creada.Id!.Value, creada, "tester");
        Assert.True((await _bodegas.GetByIdAsync(creada.Id!.Value))!.PermiteExistenciaNegativa);

        // false (bloquea) al editar.
        creada.PermiteExistenciaNegativa = false;
        await _bodegas.UpdateAsync(creada.Id!.Value, creada, "tester");
        Assert.False((await _bodegas.GetByIdAsync(creada.Id!.Value))!.PermiteExistenciaNegativa);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
