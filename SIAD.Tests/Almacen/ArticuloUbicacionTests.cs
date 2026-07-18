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

    private async Task<int> SeedTipoAsync(string codigo)
    {
        var t = new alm_tipo_articulo
        {
            codigo = codigo,
            nombre = $"Tipo {codigo}",
            activo = true,
            maneja_inventario = true
        };
        _context!.alm_tipo_articulos.Add(t);
        await _context.SaveChangesAsync();
        return t.id;
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
    public async Task Add_ConUbicacionManual_PersisteYSeMuestra()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART4");
        var bodegaId = await SeedBodegaAsync("BOD");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto
        {
            BodegaId = bodegaId,
            Ubicacion1 = "Pasillo A",
            Ubicacion3 = "Nivel 2"
        }, "tester");

        var item = Assert.Single(await _service.GetAsync(articuloId));
        Assert.Equal("Pasillo A", item.Ubicacion1);
        Assert.Null(item.Ubicacion2);
        Assert.Equal("Nivel 2", item.Ubicacion3);
        Assert.Equal("Pasillo A · Nivel 2", item.UbicacionDisplay);
    }

    [SkippableFact]
    public async Task Add_UbicacionMayorA20_SeTrunca()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART7");
        var bodegaId = await SeedBodegaAsync("BODT");

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto
        {
            BodegaId = bodegaId,
            Ubicacion1 = new string('X', 40)
        }, "tester");

        var item = Assert.Single(await _service.GetAsync(articuloId));
        Assert.Equal(20, item.Ubicacion1!.Length);
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
    public async Task Deshabilitar_MarcaInactivaYConservaHistorico()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ART6");
        var bA = await SeedBodegaAsync("B1");
        var bB = await SeedBodegaAsync("B2");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Principal = true }, "tester");
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB }, "tester");

        var ok = await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester");
        Assert.True(ok);

        var activas = await _service.GetAsync(articuloId);
        Assert.Single(activas);
        Assert.Equal(bA, activas[0].BodegaId);

        var todas = await _service.GetAsync(articuloId, incluirInactivas: true);
        Assert.Equal(2, todas.Count);
        Assert.False(todas.Single(u => u.BodegaId == bB).Activo);
    }

    [SkippableFact]
    public async Task Deshabilitar_Principal_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ARTP");
        var bA = await SeedBodegaAsync("PA");
        var bB = await SeedBodegaAsync("PB");
        var enA = await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Principal = true }, "tester");
        await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeshabilitarAsync(articuloId, enA.Id!.Value, "tester"));
    }

    [SkippableFact]
    public async Task Deshabilitar_UltimaActiva_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ARTU");
        var bA = await SeedBodegaAsync("UA");
        var enA = await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeshabilitarAsync(articuloId, enA.Id!.Value, "tester"));
    }

    [SkippableFact]
    public async Task Reactivar_VuelveActiva()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ARTR");
        var bA = await SeedBodegaAsync("RA");
        var bB = await SeedBodegaAsync("RB");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Principal = true }, "tester");
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB }, "tester");
        await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester");

        var ok = await _service.ReactivarAsync(articuloId, enB.Id!.Value, "tester");
        Assert.True(ok);
        Assert.Equal(2, (await _service.GetAsync(articuloId)).Count);
    }

    [SkippableFact]
    public async Task Add_BodegaDeshabilitada_Reactiva()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ARTRE");
        var bA = await SeedBodegaAsync("REA");
        var bB = await SeedBodegaAsync("REB");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Principal = true }, "tester");
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB }, "tester");
        await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester");

        // Re-agregar la misma bodega reactiva la fila existente (no lanza, no duplica).
        var reAdd = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB, Ubicacion1 = "Rack 9" }, "tester");
        Assert.Equal(enB.Id, reAdd.Id);
        Assert.True(reAdd.Activo);

        var todas = await _service.GetAsync(articuloId, incluirInactivas: true);
        Assert.Equal(2, todas.Count);
        Assert.Equal("Rack 9", todas.Single(u => u.BodegaId == bB).Ubicacion1);
    }

    [SkippableFact]
    public async Task Create_SinUbicaciones_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoAsync("ZTC1");
        var articulos = new ArticulosService(_context!, new TestCurrentCompanyService(CompanyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            articulos.CreateAsync(new ArticuloEditDto { Codigo = "ZZCRE1", Descripcion = "Sin bodega", TipoArticuloId = tipo }, "tester"));
    }

    [SkippableFact]
    public async Task Create_ConUbicaciones_PrimeraPrincipalYSumaExistencia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bA = await SeedBodegaAsync("CRA");
        var bB = await SeedBodegaAsync("CRB");
        var tipo = await SeedTipoAsync("ZTC2");
        var articulos = new ArticulosService(_context!, new TestCurrentCompanyService(CompanyId));

        var creado = await articulos.CreateAsync(new ArticuloEditDto
        {
            Codigo = "ZZCRE2",
            Descripcion = "Con bodegas",
            TipoArticuloId = tipo,
            Ubicaciones =
            {
                new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, ExistenciaMinima = 3 },
                new ArticuloUbicacionDto { BodegaId = bB, Existencia = 5, ExistenciaMinima = 2 }
            }
        }, "tester");

        var art = await _context!.alm_articulos.AsNoTracking().FirstAsync(a => a.id == creado.Id!.Value);
        Assert.Equal(15m, art.existencia);
        Assert.Equal(5m, art.existencia_minima);

        var ubic = await _service!.GetAsync(creado.Id!.Value);
        Assert.Equal(2, ubic.Count);
        Assert.Equal(bA, ubic.Single(u => u.Principal).BodegaId);
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

        await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester");

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

        var articulos = new ArticulosService(_context!, new TestCurrentCompanyService(CompanyId));
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
