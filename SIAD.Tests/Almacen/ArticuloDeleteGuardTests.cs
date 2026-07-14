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
/// La guarda que impide borrar un artículo con movimientos de kardex debe apoyarse
/// en articulo_id (la FK real), no en codigo_articulo (columna legacy de snapshot).
///
/// El código de artículo es OPCIONAL desde 2026-07-13: los artículos nuevos se
/// guardan con código en blanco. Una guarda que compara por código no encuentra
/// los movimientos de esos artículos, los deja borrar, y sus asientos de kardex
/// quedan huérfanos (la FK articulo_id anula la referencia en silencio).
/// El kardex es un libro mayor: eso no puede pasar.
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
            _service = new ArticulosService(_context, company);
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
    /// EL BUG: artículo SIN código, con un asiento posteado por el motor
    /// (articulo_id relleno, codigo_articulo NULL). La guarda por codigo_articulo
    /// compara '' contra NULL, no encuentra nada, y deja pasar el borrado.
    /// </summary>
    [SkippableFact]
    public async Task ArticuloSinCodigo_ConMovimientos_NoSePuedeEliminar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync(string.Empty);
        var bodegaId = await SeedBodegaAsync("ZZDG1");
        await SeedMovimientoAsync(artId, codigoArticulo: null, bodegaId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.DeleteAsync(artId));

        // El artículo debe seguir vivo.
        var sigueVivo = await _context!.alm_articulos.AnyAsync(a => a.id == artId);
        Assert.True(sigueVivo, "El artículo con movimientos no debió eliminarse.");
    }

    /// <summary>
    /// No debe haber regresión: el artículo migrado (CON código) que tiene movimientos
    /// tampoco se puede eliminar. Cubre el 99.97% del kardex, donde articulo_id ya está backfilleado.
    /// </summary>
    [SkippableFact]
    public async Task ArticuloConCodigo_ConMovimientos_NoSePuedeEliminar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-CON");
        var bodegaId = await SeedBodegaAsync("ZZDG2");
        await SeedMovimientoAsync(artId, "ZZDG-CON", bodegaId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.DeleteAsync(artId));

        var sigueVivo = await _context!.alm_articulos.AnyAsync(a => a.id == artId);
        Assert.True(sigueVivo, "El artículo con movimientos no debió eliminarse.");
    }

    /// <summary>
    /// El caso feliz sigue funcionando: un artículo SIN movimientos sí se elimina.
    /// Sin este test, la guarda podría "arreglarse" bloqueando todo.
    /// </summary>
    [SkippableFact]
    public async Task ArticuloSinMovimientos_SiSePuedeEliminar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var artId = await SeedArticuloAsync("ZZDG-LIBRE");

        var eliminado = await _service!.DeleteAsync(artId);

        Assert.True(eliminado);
        var sigueVivo = await _context!.alm_articulos.AnyAsync(a => a.id == artId);
        Assert.False(sigueVivo, "El artículo sin movimientos sí debió eliminarse.");
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
