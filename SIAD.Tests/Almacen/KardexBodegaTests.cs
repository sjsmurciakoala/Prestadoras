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

/// <summary>
/// Fase 3 — consulta del kardex por bodega. La bodega delimita el universo del
/// kardex (y por tanto su saldo corrido); fecha/tipo sólo recortan la presentación.
/// </summary>
[Collection("Postgres")]
public class KardexBodegaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IKardexService? _service;

    public KardexBodegaTests(PostgresFixture fixture) : base(fixture)
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
            _service = new KardexService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task SeedArticuloAsync(string codigo)
    {
        _context!.alm_articulos.Add(new alm_articulo { codigo_articulo = codigo, descripcion = $"Artículo {codigo}" });
        await _context.SaveChangesAsync();
    }

    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();
        return bodega.id;
    }

    private async Task SeedMovimientoAsync(string codigoArticulo, int bodegaId, DateOnly fecha, decimal ingresos, decimal salidas)
    {
        _context!.alm_kardexs.Add(new alm_kardex
        {
            codigo_articulo = codigoArticulo,
            bodega_id = bodegaId,
            fecha = fecha,
            ingresos = ingresos,
            salidas = salidas
        });
        await _context.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task SinBodega_SaldoEsGlobalDelArticulo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedArticuloAsync("ZZKDX1");
        var b1 = await SeedBodegaAsync("ZZK1");
        var b2 = await SeedBodegaAsync("ZZK2");
        await SeedMovimientoAsync("ZZKDX1", b1, new DateOnly(2026, 1, 1), 10, 0);
        await SeedMovimientoAsync("ZZKDX1", b1, new DateOnly(2026, 1, 2), 0, 3);
        await SeedMovimientoAsync("ZZKDX1", b2, new DateOnly(2026, 1, 3), 5, 0);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { CodigoArticulo = "ZZKDX1" });

        Assert.NotNull(kardex);
        Assert.Equal(12m, kardex!.SaldoCalculado); // 10 - 3 + 5, todas las bodegas
        Assert.Equal(3, kardex.Movimientos.Count);
    }

    [SkippableFact]
    public async Task ConBodega_SaldoYMovimientosSoloDeEsaBodega()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedArticuloAsync("ZZKDX2");
        var b1 = await SeedBodegaAsync("ZZK1");
        var b2 = await SeedBodegaAsync("ZZK2");
        await SeedMovimientoAsync("ZZKDX2", b1, new DateOnly(2026, 1, 1), 10, 0);
        await SeedMovimientoAsync("ZZKDX2", b1, new DateOnly(2026, 1, 2), 0, 3);
        await SeedMovimientoAsync("ZZKDX2", b2, new DateOnly(2026, 1, 3), 5, 0);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { CodigoArticulo = "ZZKDX2", BodegaId = b1 });

        Assert.NotNull(kardex);
        Assert.Equal(7m, kardex!.SaldoCalculado); // 10 - 3, sólo bodega b1
        Assert.Equal(2, kardex.Movimientos.Count);
        Assert.All(kardex.Movimientos, m => Assert.Equal(b1, m.BodegaId));
    }

    [SkippableFact]
    public async Task Proyeccion_IncluyeCodigoYNombreDeBodega()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedArticuloAsync("ZZKDX3");
        var b1 = await SeedBodegaAsync("ZZK9");
        await SeedMovimientoAsync("ZZKDX3", b1, new DateOnly(2026, 1, 1), 10, 0);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { CodigoArticulo = "ZZKDX3" });

        var mov = Assert.Single(kardex!.Movimientos);
        Assert.Equal(b1, mov.BodegaId);
        Assert.Equal("ZZK9", mov.BodegaCodigo);
        Assert.Equal("Bodega ZZK9", mov.BodegaNombre);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
