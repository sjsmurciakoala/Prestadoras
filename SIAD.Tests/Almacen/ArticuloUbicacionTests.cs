using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    /// <summary>
    /// Siembra una fila por bodega DIRECTO por EF, sin pasar por el servicio: es la única
    /// forma de fijar lo que el servicio nunca escribe desde el DTO (existencia negativa,
    /// comprometida y tránsito los mueve el motor de posteo / kardex).
    /// </summary>
    private async Task<int> SeedUbicacionDirectaAsync(
        int articuloId,
        int bodegaId,
        decimal existencia = 0m,
        decimal comprometida = 0m,
        decimal transito = 0m)
    {
        var fila = new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = bodegaId,
            existencia = existencia,
            existencia_comprometida = comprometida,
            existencia_transito = transito,
            activo = true
        };
        _context!.alm_articulo_bodegas.Add(fila);
        await _context.SaveChangesAsync();
        return fila.id;
    }

    /// <summary>Existencia de la CABECERA (alm_articulo) leída de la BD.</summary>
    private async Task<decimal> CabeceraExistenciaAsync(int articuloId)
        => (await _context!.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId)).existencia;

    /// <summary>Σ de existencias de las ubicaciones ACTIVAS: el contrato del rollup.</summary>
    private async Task<decimal> SumaActivasAsync(int articuloId)
        => await _context!.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId && u.activo)
            .SumAsync(u => u.existencia);

    /// <summary>
    /// Unidad con categoría, para los CreateAsync de artículo: desde 2026-07-29 un tipo
    /// que maneja inventario exige unidad de medida (y la unidad debe tener categoría).
    /// </summary>
    private async Task<int> SeedUnidadAsync(string codigo)
    {
        var cat = new alm_categoria_unidad { nombre = $"Cat {codigo}", activo = true };
        _context!.alm_categoria_unidads.Add(cat);
        await _context.SaveChangesAsync();

        var u = new alm_unidad_medida
        {
            codigo = codigo,
            nombre = $"Unidad {codigo}",
            categoria_id = cat.id,
            activo = true,
            factor_conversion = 1m
        };
        _context.alm_unidad_medidas.Add(u);
        await _context.SaveChangesAsync();
        return u.id;
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
        // Existencia 0 explícita: la regla de deshabilitación exige bodega en cero, así que
        // este escenario (reactivar por re-alta) solo aplica a ubicaciones sin stock.
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB, Existencia = 0m }, "tester");
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
        // Con unidad válida, para que lo que dispare la excepción sea la falta de bodega
        // (sin ella lanzaría antes la regla de unidad obligatoria y el test probaría otra cosa).
        var unidad = await SeedUnidadAsync("ZUC1");
        var articulos = new ArticulosService(_context!, new TestCurrentCompanyService(CompanyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            articulos.CreateAsync(new ArticuloEditDto { Codigo = "ZZCRE1", Descripcion = "Sin bodega", TipoArticuloId = tipo, UnidadMedidaId = unidad }, "tester"));
    }

    [SkippableFact]
    public async Task Create_ConUbicaciones_PrimeraPrincipalYSumaExistencia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bA = await SeedBodegaAsync("CRA");
        var bB = await SeedBodegaAsync("CRB");
        var tipo = await SeedTipoAsync("ZTC2");
        var unidad = await SeedUnidadAsync("ZUC2");
        var articulos = new ArticulosService(_context!, new TestCurrentCompanyService(CompanyId));

        var creado = await articulos.CreateAsync(new ArticuloEditDto
        {
            Codigo = "ZZCRE2",
            Descripcion = "Con bodegas",
            TipoArticuloId = tipo,
            UnidadMedidaId = unidad,
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

        // Vaciar B (equivalente al traslado/ajuste previo que ahora exige la regla de
        // deshabilitación) también recalcula la cabecera: 15 → 10, el mínimo no cambia.
        enB.Existencia = 0m;
        await _service.UpdateAsync(articuloId, enB.Id!.Value, enB, "tester");

        var artVaciada = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(10m, artVaciada.existencia);
        Assert.Equal(5m, artVaciada.existencia_minima);

        // Deshabilitarla la saca del rollup: el mínimo de B (2) también deja de contar.
        await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester");

        var art2 = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(10m, art2.existencia);
        Assert.Equal(3m, art2.existencia_minima);
    }

    // ── Deshabilitar exige bodega en CERO (anti-descuadre, 2026-07-29) ───────────
    // Deshabilitar una ubicación con saldo la saca del rollup de cabecera sin generar
    // ningún movimiento de kardex: es el generador más común del descuadre que reporta
    // el filtro "Con descuadre" del maestro. La regla: primero se vacía la bodega
    // (traslado o ajuste), después se deshabilita.

    [SkippableFact]
    public async Task Deshabilitar_ConExistencia_LanzaYNoDescuadra()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZDESH1");
        var bA = await SeedBodegaAsync("ZZD1A");
        var bB = await SeedBodegaAsync("ZZD1B");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, Principal = true }, "tester");
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB, Existencia = 7 }, "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester"));
        Assert.Contains("existencia", ex.Message);
        Assert.Contains("7", ex.Message); // el mensaje dice cuánto queda

        // Nada cambió: la ubicación sigue activa y la cabecera sigue cuadrada (10 + 7).
        Assert.Equal(2, (await _service.GetAsync(articuloId)).Count);
        Assert.Equal(17m, await CabeceraExistenciaAsync(articuloId));
        Assert.Equal(await SumaActivasAsync(articuloId), await CabeceraExistenciaAsync(articuloId));
    }

    [SkippableFact]
    public async Task Deshabilitar_ConExistenciaNegativa_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZDESH2");
        var bA = await SeedBodegaAsync("ZZD2A");
        var bB = await SeedBodegaAsync("ZZD2B");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, Principal = true }, "tester");

        // Negativa: no se puede teclear por el DTO, la deja un kardex mal cuadrado.
        // Deshabilitarla SUBIRÍA la cabecera en silencio, así que también se bloquea.
        var idB = await SeedUbicacionDirectaAsync(articuloId, bB, existencia: -3m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.DeshabilitarAsync(articuloId, idB, "tester"));
        Assert.Contains("existencia", ex.Message);

        var todas = await _service!.GetAsync(articuloId, incluirInactivas: true);
        Assert.True(todas.Single(u => u.BodegaId == bB).Activo);
    }

    [SkippableFact]
    public async Task Deshabilitar_ConComprometida_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZDESH3");
        var bA = await SeedBodegaAsync("ZZD3A");
        var bB = await SeedBodegaAsync("ZZD3B");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, Principal = true }, "tester");

        // Existencia en 0 pero con reserva pendiente de despacho.
        var idB = await SeedUbicacionDirectaAsync(articuloId, bB, existencia: 0m, comprometida: 4m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.DeshabilitarAsync(articuloId, idB, "tester"));
        Assert.Contains("comprometida", ex.Message);
    }

    [SkippableFact]
    public async Task Deshabilitar_ConTransito_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZDESH4");
        var bA = await SeedBodegaAsync("ZZD4A");
        var bB = await SeedBodegaAsync("ZZD4B");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, Principal = true }, "tester");

        // Existencia en 0 pero con mercadería en camino a esa bodega.
        var idB = await SeedUbicacionDirectaAsync(articuloId, bB, existencia: 0m, transito: 2m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.DeshabilitarAsync(articuloId, idB, "tester"));
        Assert.Contains("tránsito", ex.Message);
    }

    /// <summary>El camino permitido: se vacía la bodega y entonces sí se deshabilita.</summary>
    [SkippableFact]
    public async Task Deshabilitar_TrasVaciarLaBodega_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZDESH5");
        var bA = await SeedBodegaAsync("ZZD5A");
        var bB = await SeedBodegaAsync("ZZD5B");
        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, Principal = true }, "tester");
        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB, Existencia = 7 }, "tester");

        enB.Existencia = 0m;
        await _service.UpdateAsync(articuloId, enB.Id!.Value, enB, "tester");

        Assert.True(await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester"));

        var activas = await _service.GetAsync(articuloId);
        Assert.Single(activas);
        Assert.Equal(bA, activas[0].BodegaId);
        Assert.Equal(10m, await CabeceraExistenciaAsync(articuloId));
        Assert.Equal(await SumaActivasAsync(articuloId), await CabeceraExistenciaAsync(articuloId));
    }

    // ── Atomicidad fila de bodega + cabecera ─────────────────────────────────────

    /// <summary>
    /// Cada escritura (alta, edición, deshabilitación, reactivación) guarda la fila de bodega
    /// y recalcula la cabecera en UNA transacción: al final de cada operación la invariante
    /// cabecera == Σ bodegas ACTIVAS se cumple. Además verifica que el servicio REUSA la
    /// transacción ambiente (la del fixture) en vez de abrir una anidada — Npgsql reventaría —
    /// y que no la confirma ni la cierra (el rollback del fixture sigue mandando).
    /// </summary>
    [SkippableFact]
    public async Task Escrituras_ReusanLaTransaccionAmbiente_YDejanLaCabeceraCuadrada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var articuloId = await SeedArticuloAsync("ZZTX1");
        var bA = await SeedBodegaAsync("ZZTXA");
        var bB = await SeedBodegaAsync("ZZTXB");

        async Task AssertCuadradaAsync(string paso)
        {
            var cabecera = await CabeceraExistenciaAsync(articuloId);
            var suma = await SumaActivasAsync(articuloId);
            Assert.True(cabecera == suma, $"Descuadre tras {paso}: cabecera {cabecera} vs Σ activas {suma}.");
            Assert.Same(Transaction, _context!.Database.CurrentTransaction!.GetDbTransaction());
        }

        await _service!.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bA, Existencia = 10, Principal = true }, "tester");
        await AssertCuadradaAsync("add-A");

        var enB = await _service.AddAsync(articuloId, new ArticuloUbicacionDto { BodegaId = bB, Existencia = 4 }, "tester");
        await AssertCuadradaAsync("add-B");

        enB.Existencia = 0m;
        await _service.UpdateAsync(articuloId, enB.Id!.Value, enB, "tester");
        await AssertCuadradaAsync("update-B");

        await _service.DeshabilitarAsync(articuloId, enB.Id!.Value, "tester");
        await AssertCuadradaAsync("deshabilitar-B");

        await _service.ReactivarAsync(articuloId, enB.Id!.Value, "tester");
        await AssertCuadradaAsync("reactivar-B");

        Assert.Equal(10m, await CabeceraExistenciaAsync(articuloId));
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
