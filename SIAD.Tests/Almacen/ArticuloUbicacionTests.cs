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
public class ArticuloUbicacionTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IArticuloUbicacionService? _service;

    public ArticuloUbicacionTests(PostgresFixture fixture) : base(fixture)
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
            _service = new ArticuloUbicacionService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task<int> SeedArticuloAsync(string codigo)
    {
        var art = new alm_articulo { codigo_articulo = codigo, descripcion = $"Artículo {codigo}" };
        _context!.alm_articulos.Add(art);
        await _context.SaveChangesAsync();
        return art.id;
    }

    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();
        return bodega.id;
    }

    private async Task<int> SeedEstanteAsync(int bodegaId, string estanteriaCodigo, string estanteCodigo)
    {
        var estanteria = new alm_estanteria { bodega_id = bodegaId, codigo = estanteriaCodigo, activo = true };
        _context!.alm_estanterias.Add(estanteria);
        await _context.SaveChangesAsync();

        var estante = new alm_estante { estanteria_id = estanteria.id, codigo = estanteCodigo, activo = true };
        _context.alm_estantes.Add(estante);
        await _context.SaveChangesAsync();
        return estante.id;
    }

    [SkippableFact]
    public async Task Add_UbicacionValida_Persiste()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART1");
        var bodegaId = await SeedBodegaAsync("B1");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaId }, "tester");

        var lista = await _service.GetAsync(articuloId);
        var item = Assert.Single(lista);
        Assert.Equal(bodegaId, item.BodegaId);
        Assert.False(string.IsNullOrWhiteSpace(item.BodegaDisplay));
    }

    [SkippableFact]
    public async Task Add_BodegaDuplicada_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART2");
        var bodegaId = await SeedBodegaAsync("B1");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaId }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaId }, "tester"));
    }

    [SkippableFact]
    public async Task Add_EstanteDeOtraBodega_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART3");
        var bodegaA = await SeedBodegaAsync("BA");
        var bodegaB = await SeedBodegaAsync("BB");
        var estanteDeB = await SeedEstanteAsync(bodegaB, "E1", "1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaA, EstanteId = estanteDeB }, "tester"));
    }

    [SkippableFact]
    public async Task Add_ConEstanteDeLaBodega_GuardaUbicacionCompuesta()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART4");
        var bodegaId = await SeedBodegaAsync("BOD");
        var estanteId = await SeedEstanteAsync(bodegaId, "EST", "1");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaId, EstanteId = estanteId }, "tester");

        var item = Assert.Single(await _service.GetAsync(articuloId));
        Assert.Equal("BOD-EST-1", item.EstanteUbicacion);
        Assert.Equal(estanteId, item.EstanteId);
    }

    [SkippableFact]
    public async Task Principal_SoloUnoPorArticulo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART5");
        var bodegaA = await SeedBodegaAsync("BA");
        var bodegaB = await SeedBodegaAsync("BB");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaA, Principal = true }, "tester");
        await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaB, Principal = true }, "tester");

        var lista = await _service.GetAsync(articuloId);
        Assert.Equal(1, lista.Count(u => u.Principal));
        Assert.Equal(bodegaB, lista.Single(u => u.Principal).BodegaId);
    }

    [SkippableFact]
    public async Task Delete_Quita()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART6");
        var bodegaId = await SeedBodegaAsync("B1");
        var creado = await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaId }, "tester");

        var ok = await _service.DeleteAsync(articuloId, creado.Id!.Value);
        Assert.True(ok);
        Assert.Empty(await _service.GetAsync(articuloId));
    }

    [SkippableFact]
    public async Task Rollup_ExistenciaYMinimoSonSumaDeBodegas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZROLL1");
        var bodegaA = await SeedBodegaAsync("ZZRA");
        var bodegaB = await SeedBodegaAsync("ZZRB");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaA, Existencia = 10, ExistenciaMinima = 3 }, "tester");
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaB, Existencia = 5, ExistenciaMinima = 2 }, "tester");

        var art = await _context!.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(15m, art.existencia);
        Assert.Equal(5m, art.existencia_minima);

        await _service.DeleteAsync(articuloId, enB.Id!.Value);

        var art2 = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(10m, art2.existencia);
        Assert.Equal(3m, art2.existencia_minima);
    }

    [SkippableFact]
    public async Task Alerta_BajoMinimoPorBodega_SeGeneraConBodega()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZALERTA1");
        var bodegaId = await SeedBodegaAsync("ZZALRT");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bodegaId, Existencia = 2, ExistenciaMinima = 5 }, "tester");

        var articulos = new ArticulosService(_context!);
        var alertas = await articulos.GetAlertasStockAsync(new AlertaStockFilterDto { Search = "ZZALERTA1" });

        var alerta = Assert.Single(alertas);
        Assert.Equal(bodegaId, alerta.BodegaId);
        Assert.Equal("Bodega ZZALRT", alerta.BodegaNombre);
        Assert.Equal(StockSeveridad.BajoMinimo, alerta.Severidad);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
