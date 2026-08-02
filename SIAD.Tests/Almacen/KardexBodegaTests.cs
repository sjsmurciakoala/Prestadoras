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

    private async Task<int> SeedArticuloAsync(string codigo, decimal existencia = 0m)
    {
        var art = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = $"Artículo {codigo}",
            existencia = existencia
        };
        _context!.alm_articulos.Add(art);
        await _context.SaveChangesAsync();
        return art.id;
    }

    /// <summary>Fila de existencia por bodega (alm_articulo_bodega), el rollup contra el que cuadra el kardex por bodega.</summary>
    private async Task SeedUbicacionAsync(int articuloId, int bodegaId, decimal existencia, bool activo = true)
    {
        _context!.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = bodegaId,
            existencia = existencia,
            activo = activo
        });
        await _context.SaveChangesAsync();
    }

    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();
        return bodega.id;
    }

    private async Task SeedMovimientoAsync(string codigoArticulo, int bodegaId, DateOnly fecha, decimal ingresos, decimal salidas, int? articuloId = null)
    {
        _context!.alm_kardexs.Add(new alm_kardex
        {
            codigo_articulo = codigoArticulo,
            articulo_id = articuloId,
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

    [SkippableFact]
    public async Task PorArticuloId_FiltraPorArticuloId()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZKDXID");
        var b1 = await SeedBodegaAsync("ZZKID");
        await SeedMovimientoAsync("ZZKDXID", b1, new DateOnly(2026, 1, 1), 10, 0, articuloId: artId);
        await SeedMovimientoAsync("ZZKDXID", b1, new DateOnly(2026, 1, 2), 0, 4, articuloId: artId);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = artId });

        Assert.NotNull(kardex);
        Assert.Equal(6m, kardex!.SaldoCalculado); // 10 - 4
        Assert.Equal(2, kardex.Movimientos.Count);
    }

    /// <summary>
    /// El falso positivo de la tarjeta ámbar: con bodega filtrada el saldo es el de ESA
    /// bodega, así que compararlo contra alm_articulo.existencia (total del artículo)
    /// marcaba descuadre en todo artículo multi-bodega perfectamente cuadrado.
    /// </summary>
    [SkippableFact]
    public async Task ConBodega_ArticuloMultiBodegaCuadrado_NoReportaDescuadre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZKDXB1", existencia: 12m); // total = 7 (b1) + 5 (b2)
        var b1 = await SeedBodegaAsync("ZZKB1");
        var b2 = await SeedBodegaAsync("ZZKB2");
        await SeedUbicacionAsync(artId, b1, 7m);
        await SeedUbicacionAsync(artId, b2, 5m);
        await SeedMovimientoAsync("ZZKDXB1", b1, new DateOnly(2026, 1, 1), 10, 0, articuloId: artId);
        await SeedMovimientoAsync("ZZKDXB1", b1, new DateOnly(2026, 1, 2), 0, 3, articuloId: artId);
        await SeedMovimientoAsync("ZZKDXB1", b2, new DateOnly(2026, 1, 3), 5, 0, articuloId: artId);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = artId, BodegaId = b1 });

        Assert.NotNull(kardex);
        Assert.Equal(b1, kardex!.BodegaId);
        Assert.Equal(7m, kardex.SaldoCalculado);
        Assert.Equal(7m, kardex.ExistenciaBodega);
        Assert.Equal(7m, kardex.ExistenciaComparable);
        Assert.Equal(12m, kardex.ExistenciaRegistrada); // el total sigue disponible, sólo no se compara
        Assert.False(kardex.SaldoDescuadrado);
    }

    [SkippableFact]
    public async Task ConBodega_BodegaDescuadrada_ReportaDescuadre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZKDXB2", existencia: 10m);
        var b1 = await SeedBodegaAsync("ZZKB3");
        await SeedUbicacionAsync(artId, b1, 4m); // el rollup dice 4, el kardex 10
        await SeedMovimientoAsync("ZZKDXB2", b1, new DateOnly(2026, 1, 1), 10, 0, articuloId: artId);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = artId, BodegaId = b1 });

        Assert.NotNull(kardex);
        Assert.Equal(10m, kardex!.SaldoCalculado);
        Assert.Equal(4m, kardex.ExistenciaBodega);
        Assert.True(kardex.SaldoDescuadrado);
    }

    /// <summary>
    /// Ubicación inactiva = fuera del contrato de rollup (Σ activas). Sin cifra comparable
    /// no se afirma descuadre: la tarjeta muestra "—".
    /// </summary>
    [SkippableFact]
    public async Task ConBodega_SinUbicacionActiva_NoHayExistenciaComparable()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZKDXB3", existencia: 8m);
        var b1 = await SeedBodegaAsync("ZZKB4");
        await SeedUbicacionAsync(artId, b1, 8m, activo: false);
        await SeedMovimientoAsync("ZZKDXB3", b1, new DateOnly(2026, 1, 1), 8, 0, articuloId: artId);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = artId, BodegaId = b1 });

        Assert.NotNull(kardex);
        Assert.Null(kardex!.ExistenciaBodega);
        Assert.Null(kardex.ExistenciaComparable);
        Assert.False(kardex.SaldoDescuadrado);
    }

    [SkippableFact]
    public async Task SinBodega_ComparaContraExistenciaTotalDelArticulo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZKDXB4", existencia: 12m);
        var b1 = await SeedBodegaAsync("ZZKB5");
        var b2 = await SeedBodegaAsync("ZZKB6");
        await SeedUbicacionAsync(artId, b1, 7m);
        await SeedUbicacionAsync(artId, b2, 5m);
        await SeedMovimientoAsync("ZZKDXB4", b1, new DateOnly(2026, 1, 1), 7, 0, articuloId: artId);
        await SeedMovimientoAsync("ZZKDXB4", b2, new DateOnly(2026, 1, 2), 5, 0, articuloId: artId);

        var kardex = await _service!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = artId });

        Assert.NotNull(kardex);
        Assert.Null(kardex!.BodegaId);
        Assert.Null(kardex.ExistenciaBodega);
        Assert.Equal(12m, kardex.ExistenciaComparable);
        Assert.False(kardex.SaldoDescuadrado);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
