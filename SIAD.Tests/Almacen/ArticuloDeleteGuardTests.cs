using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.Entities;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Desde 2026-07-29 el artículo NO se borra físicamente: se DESCONTINÚA (activo = false).
///
/// Esto elimina de raíz la clase de bug que antes vigilaba este archivo: la vieja guarda
/// comparaba por codigo_articulo (columna legacy de snapshot) en vez de articulo_id (la FK
/// real), así que no encontraba los movimientos de los artículos nuevos —que llevan código
/// en blanco desde 2026-07-13—, los dejaba borrar y sus asientos de kardex quedaban
/// huérfanos. Al no borrar nunca la fila, el kardex ya no puede quedar huérfano, con o sin
/// código. Lo que estos tests vigilan ahora es que descontinuar CONSERVE la fila y su
/// historia, en los mismos tres escenarios de antes.
/// </summary>
[Collection("Postgres")]
public class ArticuloDeleteGuardTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IArticulosService? _service;

    public ArticuloDeleteGuardTests(PostgresFixture fixture) : base(fixture)
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

            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);

            var rollup = new ArticuloRollupService(_context);
            var motor = new InventarioPostingService(_context, company, rollup);
            var carga = new CargaInicialInventarioService(_context, company, motor);
            _service = new ArticulosService(_context, company, rollup, carga);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task<int> SeedArticuloAsync(string codigo)
    {
        var art = new alm_articulo { codigo_articulo = codigo, descripcion = "Artículo de prueba" };
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

    /// <summary>
    /// Siembra un asiento como lo hará el motor de posteo: la referencia real es
    /// articulo_id; codigo_articulo (columna legacy de snapshot SIMAFI) queda en NULL.
    /// </summary>
    private async Task SeedMovimientoAsync(int articuloId, string? codigoArticulo, int bodegaId)
    {
        _context!.alm_kardexs.Add(new alm_kardex
        {
            articulo_id = articuloId,
            codigo_articulo = codigoArticulo,
            bodega_id = bodegaId,
            fecha = new DateOnly(2026, 1, 1),
            ingresos = 10,
            salidas = 0
        });
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// El caso que antes era EL BUG: artículo SIN código, con un asiento posteado por el
    /// motor (articulo_id relleno, codigo_articulo NULL). Se descontinúa y, sobre todo,
    /// la fila y su movimiento siguen ahí: el kardex no queda huérfano.
    /// </summary>
    [SkippableFact]
    public async Task ArticuloSinCodigo_ConMovimientos_SeDescontinuaYConservaKardex()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync(string.Empty);
        var bodegaId = await SeedBodegaAsync("ZZDG1");
        await SeedMovimientoAsync(artId, codigoArticulo: null, bodegaId);

        var ok = await _service!.DeleteAsync(artId, "tester");
        Assert.True(ok);

        var art = await _context!.alm_articulos.AsNoTracking().FirstOrDefaultAsync(a => a.id == artId);
        Assert.NotNull(art);
        Assert.False(art!.activo, "El artículo debió quedar descontinuado, no borrado.");

        // Lo esencial: el asiento sigue apuntando al artículo (no quedó huérfano).
        var movimientoVive = await _context.alm_kardexs.AsNoTracking().AnyAsync(k => k.articulo_id == artId);
        Assert.True(movimientoVive, "El movimiento de kardex debió conservarse.");
    }

    /// <summary>
    /// Mismo comportamiento para el artículo migrado (CON código): descontinuar conserva
    /// la fila. Cubre el 99.97% del kardex, donde articulo_id ya está backfilleado.
    /// </summary>
    [SkippableFact]
    public async Task ArticuloConCodigo_ConMovimientos_SeDescontinuaYConservaKardex()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-CON");
        var bodegaId = await SeedBodegaAsync("ZZDG2");
        await SeedMovimientoAsync(artId, "ZZDG-CON", bodegaId);

        var ok = await _service!.DeleteAsync(artId, "tester");
        Assert.True(ok);

        var art = await _context!.alm_articulos.AsNoTracking().FirstOrDefaultAsync(a => a.id == artId);
        Assert.NotNull(art);
        Assert.False(art!.activo, "El artículo debió quedar descontinuado, no borrado.");

        var movimientoVive = await _context.alm_kardexs.AsNoTracking().AnyAsync(k => k.articulo_id == artId);
        Assert.True(movimientoVive, "El movimiento de kardex debió conservarse.");
    }

    /// <summary>
    /// Un artículo SIN movimientos tampoco se borra: la decisión (2026-07-29) fue que el
    /// maestro nunca borra físicamente, para que el botón tenga un solo comportamiento.
    /// Antes este caso sí hacía DELETE.
    /// </summary>
    [SkippableFact]
    public async Task ArticuloSinMovimientos_TambienSeDescontinua_NoSeBorra()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-LIBRE");

        var ok = await _service!.DeleteAsync(artId, "tester");
        Assert.True(ok);

        var art = await _context!.alm_articulos.AsNoTracking().FirstOrDefaultAsync(a => a.id == artId);
        Assert.NotNull(art);
        Assert.False(art!.activo, "El artículo sin movimientos debió descontinuarse, no borrarse.");
    }

    /// <summary>Descontinuar dos veces se rechaza: evita registrar un borrado duplicado en la bitácora.</summary>
    [SkippableFact]
    public async Task Descontinuar_DosVeces_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-DOBLE");
        await _service!.DeleteAsync(artId, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.DeleteAsync(artId, "tester"));
    }

    /// <summary>
    /// Un artículo histórico SIN tipo (tipo_articulo_id NULL, como los migrados de SIMAFI)
    /// se puede reactivar: exigirle tipo aquí impediría deshacer un descontinuado por error.
    /// </summary>
    [SkippableFact]
    public async Task Reactivar_ArticuloSinTipo_VuelveAActivo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-REACT");
        await _service!.DeleteAsync(artId, "tester");

        var ok = await _service!.ReactivarAsync(artId, "tester");
        Assert.True(ok);

        var art = await _context!.alm_articulos.AsNoTracking().FirstOrDefaultAsync(a => a.id == artId);
        Assert.NotNull(art);
        Assert.True(art!.activo, "El artículo debió quedar activo otra vez.");
    }

    /// <summary>Reactivar uno que ya está activo se rechaza.</summary>
    [SkippableFact]
    public async Task Reactivar_ArticuloActivo_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-YAACT");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.ReactivarAsync(artId, "tester"));
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
