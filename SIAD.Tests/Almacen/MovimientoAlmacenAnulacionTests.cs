using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Anulación del documento de movimiento de almacén (plan de pruebas §7, casos 16-18).
/// <para>
/// La regla de oro: anular NO borra ni hace UPDATE del kardex, postea una reversa por
/// renglón. Tras anular, la existencia y el costo vuelven al estado previo, y anular dos
/// veces no revierte dos veces.
/// </para>
/// </summary>
[Collection("Postgres")]
public class MovimientoAlmacenAnulacionTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private MovimientoAlmacenService? _service;

    public MovimientoAlmacenAnulacionTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);

            var rollup = new ArticuloRollupService(_context);
            var motor = new InventarioPostingService(_context, company, rollup);
            var poliza = new SIAD.Services.Contabilidad.PolizaService(_context, company);
            _service = new MovimientoAlmacenService(_context, company, motor, poliza, new AlertasStockNotificadorNoop());

            // Prueba la MECÁNICA de la anulación (revierte el kardex/estado), no el asiento contable:
            // se apaga la integración para aislar el test del estado de los flags en la base de prueba.
            await DesactivarIntegracionContableAsync();
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task<(int articuloId, int bodegaId)> SeedParAsync(string codigo)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        var articulo = new alm_articulo
        {
            codigo_articulo = codigo, descripcion = $"Artículo {codigo}",
            existencia = 0m, valor_unitario = 0m, activo = true
        };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articulo.id, bodega_id = bodega.id,
            existencia = 0m, costo_promedio = 0m, activo = true, principal = true
        });
        await _context.SaveChangesAsync();
        return (articulo.id, bodega.id);
    }

    private async Task<int> SeedTipoAsync(string codigo, string clase)
    {
        var tipo = new alm_tipo_movimiento
        {
            company_id = CompanyId, codigo = codigo, nombre = $"Tipo {codigo}", clase = clase,
            activo = true, usuariocreacion = "test",
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context!.alm_tipo_movimientos.Add(tipo);
        await _context.SaveChangesAsync();
        return tipo.id;
    }

    private Task<alm_articulo_bodega> ParAsync(int articuloId, int bodegaId)
        => _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);

    // ── 16 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Anular_DocumentoPosteado_RevierteYMarcaEstado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZAN-16");
        var ent = await SeedTipoAsync("ZZ_E16", ClaseAjusteInventario.Entrada);

        var doc = await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = ent, BodegaId = bod, Motivo = "entrada a revertir",
            Detalles = [new() { ArticuloId = art, Cantidad = 10m, CostoUnitario = 5m }]
        }, "test", false);

        Assert.Equal(10m, (await ParAsync(art, bod)).existencia);

        var ok = await _service.AnularAsync(doc.Id!.Value, "cargado por error", "tester");
        Assert.True(ok);

        // La existencia volvió a 0 y el documento quedó marcado con quién/cuándo/por qué.
        Assert.Equal(0m, (await ParAsync(art, bod)).existencia);
        var recargado = await _service.GetByIdAsync(doc.Id!.Value);
        Assert.Equal(EstadoMovimientoAlmacen.Anulado, recargado!.Estado);
        Assert.Equal("tester", recargado.AnuladoPor);
        Assert.Equal("cargado por error", recargado.MotivoAnulacion);
        Assert.NotNull(recargado.FechaAnulacion);
    }

    // ── 17 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Anular_DosVeces_EsIdempotente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZAN-17");
        var ent = await SeedTipoAsync("ZZ_E17", ClaseAjusteInventario.Entrada);

        var doc = await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = ent, BodegaId = bod, Motivo = "entrada",
            Detalles = [new() { ArticuloId = art, Cantidad = 8m, CostoUnitario = 3m }]
        }, "test", false);

        Assert.True(await _service.AnularAsync(doc.Id!.Value, "primera", "tester"));
        Assert.True(await _service.AnularAsync(doc.Id!.Value, "segunda", "tester"));   // no revierte de nuevo

        // La existencia quedó en 0, no en -8: la segunda anulación no posteó otra reversa.
        Assert.Equal(0m, (await ParAsync(art, bod)).existencia);
        // Existen exactamente 2 asientos del par: la entrada y su única reversa.
        var asientos = await _context!.alm_kardexs.AsNoTracking()
            .CountAsync(k => k.articulo_id == art && k.bodega_id == bod);
        Assert.Equal(2, asientos);
    }

    // ── 18 ──────────────────────────────────────────────────────────────────
    /// <summary>
    /// ⚠️ LIMITACIÓN CONOCIDA DEL MOTOR, no un defecto de este servicio. El diseño (§7, caso
    /// 18) pedía que anular una corrección de valor restituyera el promedio anterior. HOY NO
    /// LO HACE: <c>InventarioPostingService.Aplicar</c> (rama <c>AjusteValor</c>, :456-458)
    /// graba el costo NUEVO sin guardar el anterior en ninguna columna, así que la reversa
    /// (:489-494) no tiene de dónde restituirlo — es exactamente el defecto documentado en
    /// <c>docs/plans/2026-08-01-costeo-articulo-diseno.md</c> §3, corrección 1, cuya solución
    /// (<c>alm_kardex.costo_promedio_anterior</c>) es la Fase B de ESE diseño, sin implementar.
    /// <para>
    /// Esta prueba fija el comportamiento REAL (la existencia sí vuelve; el promedio NO) para
    /// que quede documentado y no se lea como un olvido. Cuando la Fase B del costeo entre,
    /// hay que cambiar la aserción del promedio a 10 y quitar esta nota.
    /// </para>
    /// </summary>
    [SkippableFact]
    public async Task Anular_CorreccionDeValor_DevuelveExistencia_PeroNoRestituyePromedio_LimitacionConocida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZAN-18");
        var ent = await SeedTipoAsync("ZZ_E18", ClaseAjusteInventario.Entrada);
        var val = await SeedTipoAsync("ZZ_V18", ClaseAjusteInventario.Valor);

        // Parte en promedio 10.
        await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = ent, BodegaId = bod, Motivo = "precarga",
            Detalles = [new() { ArticuloId = art, Cantidad = 10m, CostoUnitario = 10m }]
        }, "test", false);
        Assert.Equal(10m, (await ParAsync(art, bod)).costo_promedio);

        // Corrige el costo a 15.
        var correccion = await _service.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = val, BodegaId = bod, Motivo = "revaluación",
            Detalles = [new() { ArticuloId = art, Cantidad = 0m, CostoUnitario = 15m }]
        }, "test", false);
        Assert.Equal(15m, (await ParAsync(art, bod)).costo_promedio);

        // Anular la corrección: la existencia NO se toca (siempre fue 10) y no rompe nada...
        Assert.True(await _service.AnularAsync(correccion.Id!.Value, "revaluación errónea", "tester"));
        Assert.Equal(10m, (await ParAsync(art, bod)).existencia);

        // ...pero el promedio se queda en 15 (la limitación). El día que la Fase B del costeo
        // entre, esta aserción pasa a esperar 10m.
        Assert.Equal(15m, (await ParAsync(art, bod)).costo_promedio);
    }

    // ── Anular sin motivo ─────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Anular_SinMotivo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZAN-NM");
        var ent = await SeedTipoAsync("ZZ_ENM", ClaseAjusteInventario.Entrada);
        var doc = await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = ent, BodegaId = bod, Motivo = "entrada",
            Detalles = [new() { ArticuloId = art, Cantidad = 1m, CostoUnitario = 1m }]
        }, "test", false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AnularAsync(doc.Id!.Value, "   ", "tester"));
    }

    // ── Anular inexistente ────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Anular_Inexistente_DevuelveFalse()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Assert.False(await _service!.AnularAsync(987_654_321, "motivo", "tester"));
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
