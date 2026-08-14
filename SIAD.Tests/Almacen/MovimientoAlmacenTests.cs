using System;
using System.Collections.Generic;
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
/// Documento de movimiento de almacén (Fase 2 de
/// <c>docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md</c>, plan de pruebas §7).
/// <para>
/// Lo que se vigila: que el documento y su asiento se registren juntos o no se registren,
/// que la salida se valorice al promedio vigente, que la idempotencia no duplique, que el
/// tipo sensible exija permiso, que la anulación revierta exacto y que el kardex cuadre con
/// la existencia siempre.
/// </para>
/// <para>Requiere <c>2026-08-03_alm_movimiento.sql</c> aplicado en la base de <c>SIAD_TEST_DB</c>.</para>
/// </summary>
[Collection("Postgres")]
public class MovimientoAlmacenTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private MovimientoAlmacenService? _service;

    public MovimientoAlmacenTests(PostgresFixture fixture) : base(fixture) { }

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

            // Prueba la MECÁNICA (kardex/existencia/costo/idempotencia), no la contabilidad: se apaga
            // la integración para aislar el test del estado de los flags en la base de prueba.
            await DesactivarIntegracionContableAsync();
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    /// <summary>Par (artículo, bodega) recién creado, existencia y costo en 0.</summary>
    private async Task<(int articuloId, int bodegaId)> SeedParAsync(string codigo, bool ubicacionActiva = true)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();

        var articuloId = await SeedArticuloEnBodegaAsync(codigo, bodega.id, ubicacionActiva);
        return (articuloId, bodega.id);
    }

    /// <summary>Un artículo con su par en una bodega YA existente (para documentos multi-línea).</summary>
    private async Task<int> SeedArticuloEnBodegaAsync(string codigo, int bodegaId, bool ubicacionActiva = true)
    {
        var articulo = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = $"Artículo {codigo}",
            existencia = 0m,
            valor_unitario = 0m,
            activo = true
        };
        _context!.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articulo.id,
            bodega_id = bodegaId,
            existencia = 0m,
            costo_promedio = 0m,
            activo = ubicacionActiva,
            principal = true
        });
        await _context.SaveChangesAsync();

        return articulo.id;
    }

    private async Task<int> SeedTipoAsync(
        string codigo, string clase, bool activo = true, bool requiereAutorizacion = false, long? companyId = null)
    {
        var tipo = new alm_tipo_movimiento
        {
            company_id = companyId ?? CompanyId,
            codigo = codigo,
            nombre = $"Tipo {codigo}",
            clase = clase,
            activo = activo,
            requiere_autorizacion = requiereAutorizacion,
            usuariocreacion = "test",
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context!.alm_tipo_movimientos.Add(tipo);
        await _context.SaveChangesAsync();
        return tipo.id;
    }

    /// <summary>Deja el par con existencia y costo promedio conocidos, vía una ENTRADA real
    /// posteada por el propio servicio (así kardex y existencia quedan cuadrados).</summary>
    private async Task PrecargarAsync(int tipoEntradaId, int articuloId, int bodegaId, decimal cantidad, decimal costo)
    {
        await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = tipoEntradaId,
            BodegaId = bodegaId,
            Motivo = "precarga",
            Detalles = [new MovimientoAlmacenDetalleDto { ArticuloId = articuloId, Cantidad = cantidad, CostoUnitario = costo }]
        }, "test", puedeUsarTiposSensibles: true);
    }

    private async Task<decimal> ExistenciaAsync(int articuloId, int bodegaId)
        => (await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId)).existencia;

    private async Task<decimal> CostoPromedioAsync(int articuloId, int bodegaId)
        => (await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId)).costo_promedio;

    // ── 7 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Entrada_UnaLinea_SubeExistenciaYFijaCostoReal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-E1");
        var tipo = await SeedTipoAsync("ZZ_ENT", ClaseAjusteInventario.Entrada);

        var doc = await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = tipo,
            BodegaId = bod,
            Motivo = "sobrante de conteo",
            Detalles = [new MovimientoAlmacenDetalleDto { ArticuloId = art, Cantidad = 10m, CostoUnitario = 5m }]
        }, "test", puedeUsarTiposSensibles: false);

        Assert.True(doc.Posteado);
        Assert.Equal(EstadoMovimientoAlmacen.Registrado, doc.Estado);
        Assert.Equal(10m, await ExistenciaAsync(art, bod));
        Assert.Equal(5m, await CostoPromedioAsync(art, bod));

        var linea = Assert.Single(doc.Detalles);
        Assert.Equal(5m, linea.CostoReal);          // costo tecleado
        Assert.Equal(50m, linea.Total);
        Assert.Equal(50m, doc.Total);
        Assert.True(doc.Numero > 0);
    }

    // ── 8 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Salida_TresLineas_UnDocumentoTresAsientos_TotalEsSuma()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var ent = await SeedTipoAsync("ZZ_ENT8", ClaseAjusteInventario.Entrada);
        var sal = await SeedTipoAsync("ZZ_SAL8", ClaseAjusteInventario.Salida);

        // Todas las líneas de un documento son de la MISMA bodega: una bodega, tres artículos.
        var (art0, bod) = await SeedParAsync("ZZMV-A");
        var art1 = await SeedArticuloEnBodegaAsync("ZZMV-B", bod);
        var art2 = await SeedArticuloEnBodegaAsync("ZZMV-C", bod);
        await PrecargarAsync(ent, art0, bod, 100m, 10m);
        await PrecargarAsync(ent, art1, bod, 100m, 10m);
        await PrecargarAsync(ent, art2, bod, 100m, 10m);

        var doc = await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = sal,
            BodegaId = bod,
            Motivo = "consumo de tres artículos",
            Detalles =
            [
                new() { ArticuloId = art0, Cantidad = 4m },
                new() { ArticuloId = art1, Cantidad = 5m },
                new() { ArticuloId = art2, Cantidad = 6m }
            ]
        }, "test", puedeUsarTiposSensibles: false);

        Assert.Equal(3, doc.Detalles.Count);
        Assert.All(doc.Detalles, l => Assert.NotNull(l.KardexId));
        // Cada salida se valoriza al promedio 10: 4·10 + 5·10 + 6·10 = 150.
        Assert.Equal(150m, doc.Total);
        Assert.Equal(doc.Detalles.Sum(l => l.Total), doc.Total);
    }

    // ── 9 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Reintento_MismoUuid_NoDuplica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-IDEM");
        var tipo = await SeedTipoAsync("ZZ_IDEM", ClaseAjusteInventario.Entrada);
        var uuid = Guid.NewGuid();

        var dto = new MovimientoAlmacenDto
        {
            TipoMovimientoId = tipo, BodegaId = bod, Motivo = "idempotencia", Uuid = uuid,
            Detalles = [new() { ArticuloId = art, Cantidad = 3m, CostoUnitario = 7m }]
        };

        var primero = await _service!.CrearYPostearAsync(dto, "test", false);
        var segundo = await _service.CrearYPostearAsync(dto, "test", false);

        Assert.Equal(primero.Id, segundo.Id);
        Assert.Equal(1, await _context!.alm_movimiento_hdrs.AsNoTracking().CountAsync(h => h.uuid == uuid));
        // La existencia subió una sola vez.
        Assert.Equal(3m, await ExistenciaAsync(art, bod));
    }

    // ── 10 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TipoInactivo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-INA");
        var tipo = await SeedTipoAsync("ZZ_INA", ClaseAjusteInventario.Entrada, activo: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = tipo, BodegaId = bod, Motivo = "x",
                Detalles = [new() { ArticuloId = art, Cantidad = 1m, CostoUnitario = 1m }]
            }, "test", false));

        Assert.Contains("inactivo", ex.Message);
    }

    // ── 11 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TipoDeOtraEmpresa_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-XT");
        // Tipo sembrado en OTRA empresa: el filtro global de tenant hace que no exista aquí.
        var tipoAjeno = await SeedTipoAsync("ZZ_XT", ClaseAjusteInventario.Entrada, companyId: CompanyId + 90_000);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = tipoAjeno, BodegaId = bod, Motivo = "x",
                Detalles = [new() { ArticuloId = art, Cantidad = 1m, CostoUnitario = 1m }]
            }, "test", false));

        Assert.Contains("no existe", ex.Message);
    }

    // ── 12 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task ArticuloSinUbicacionEnLaBodega_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-U1");
        var (artHuerfano, _) = await SeedParAsync("ZZMV-U2");   // su par está en OTRA bodega
        var tipo = await SeedTipoAsync("ZZ_U", ClaseAjusteInventario.Entrada);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = tipo, BodegaId = bod, Motivo = "x",
                Detalles = [new() { ArticuloId = artHuerfano, Cantidad = 1m, CostoUnitario = 1m, CodigoArticulo = "ZZMV-U2" }]
            }, "test", false));

        Assert.Contains("no tiene ubicación", ex.Message);
    }

    // ── 13 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Salida_QueDejariaNegativo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-NEG");
        var ent = await SeedTipoAsync("ZZ_ENTn", ClaseAjusteInventario.Entrada);
        var sal = await SeedTipoAsync("ZZ_SALn", ClaseAjusteInventario.Salida);
        await PrecargarAsync(ent, art, bod, 5m, 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = sal, BodegaId = bod, Motivo = "saca de más",
                Detalles = [new() { ArticuloId = art, Cantidad = 6m }]
            }, "test", false));

        // Nada quedó a medias: la existencia sigue en 5.
        Assert.Equal(5m, await ExistenciaAsync(art, bod));
    }

    // ── 14 ──────────────────────────────────────────────────────────────────
    [SkippableTheory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MotivoVacio_Rechaza(string motivo)
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-MV");
        var tipo = await SeedTipoAsync("ZZ_MV", ClaseAjusteInventario.Entrada);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = tipo, BodegaId = bod, Motivo = motivo,
                Detalles = [new() { ArticuloId = art, Cantidad = 1m, CostoUnitario = 1m }]
            }, "test", false));
    }

    // ── 15 ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TipoSensible_SinPermiso_LanzaUnauthorized()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-SEN");
        var tipo = await SeedTipoAsync("ZZ_SEN", ClaseAjusteInventario.Entrada, requiereAutorizacion: true);

        var dto = new MovimientoAlmacenDto
        {
            TipoMovimientoId = tipo, BodegaId = bod, Motivo = "donación",
            Detalles = [new() { ArticuloId = art, Cantidad = 1m, CostoUnitario = 1m }]
        };

        // Sin permiso: rechaza con Unauthorized (el controller lo traduce a 403).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service!.CrearYPostearAsync(dto, "test", puedeUsarTiposSensibles: false));

        // Con permiso: pasa.
        var doc = await _service!.CrearYPostearAsync(dto, "test", puedeUsarTiposSensibles: true);
        Assert.True(doc.Posteado);
    }

    // ── Salida descarta el costo tecleado ─────────────────────────────────────
    [SkippableFact]
    public async Task Salida_IgnoraElCostoTecleado_UsaElPromedioVigente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-SC");
        var ent = await SeedTipoAsync("ZZ_ENTsc", ClaseAjusteInventario.Entrada);
        var sal = await SeedTipoAsync("ZZ_SALsc", ClaseAjusteInventario.Salida);
        await PrecargarAsync(ent, art, bod, 10m, 8m);   // promedio 8

        var doc = await _service!.CrearYPostearAsync(new MovimientoAlmacenDto
        {
            TipoMovimientoId = sal, BodegaId = bod, Motivo = "salida",
            // El usuario teclea 999; debe ignorarse.
            Detalles = [new() { ArticuloId = art, Cantidad = 2m, CostoUnitario = 999m }]
        }, "test", false);

        var linea = Assert.Single(doc.Detalles);
        Assert.Equal(8m, linea.CostoReal);      // el promedio, no 999
        Assert.Equal(16m, linea.Total);
    }

    // ── El mismo artículo dos veces ───────────────────────────────────────────
    [SkippableFact]
    public async Task MismoArticuloEnDosRenglones_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (art, bod) = await SeedParAsync("ZZMV-DUP");
        var tipo = await SeedTipoAsync("ZZ_DUP", ClaseAjusteInventario.Entrada);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = tipo, BodegaId = bod, Motivo = "x",
                Detalles =
                [
                    new() { ArticuloId = art, Cantidad = 1m, CostoUnitario = 1m },
                    new() { ArticuloId = art, Cantidad = 2m, CostoUnitario = 1m }
                ]
            }, "test", false));

        Assert.Contains("más de un renglón", ex.Message);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
