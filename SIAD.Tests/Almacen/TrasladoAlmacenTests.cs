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
/// Traslado entre bodegas (Fase 5.3 de
/// <c>docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md</c>, plan de pruebas §7).
/// <para>Requiere <c>2026-08-04_alm_traslado.sql</c> (paso 29) aplicado en la base de <c>SIAD_TEST_DB</c>.</para>
/// </summary>
[Collection("Postgres")]
public class TrasladoAlmacenTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private TrasladoAlmacenService? _service;
    private MovimientoAlmacenService? _movService;

    public TrasladoAlmacenTests(PostgresFixture fixture) : base(fixture) { }

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
            _service = new TrasladoAlmacenService(_context, company, motor, rollup);
            var poliza = new SIAD.Services.Contabilidad.PolizaService(_context, company);
            _movService = new MovimientoAlmacenService(_context, company, motor, poliza, new AlertasStockNotificadorNoop());
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    private async Task<int> SeedBodegaAsync(string codigo, bool activo = true)
    {
        var b = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = activo };
        _context!.alm_bodegas.Add(b);
        await _context.SaveChangesAsync();
        return b.id;
    }

    private async Task<int> SeedArticuloAsync(string codigo)
    {
        var a = new alm_articulo { codigo_articulo = codigo, descripcion = $"Artículo {codigo}", existencia = 0m, activo = true };
        _context!.alm_articulos.Add(a);
        await _context.SaveChangesAsync();
        return a.id;
    }

    private async Task SeedParAsync(int articuloId, int bodegaId, decimal existencia, decimal costo, bool activo = true)
    {
        _context!.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articuloId, bodega_id = bodegaId,
            existencia = existencia, costo_promedio = costo, activo = activo, principal = false
        });
        await _context.SaveChangesAsync();
    }

    private async Task<int> SeedTipoTrasladoAsync(string codigo = "ZZTRF")
    {
        var t = new alm_tipo_movimiento
        {
            company_id = CompanyId, codigo = codigo, nombre = "Traslado prueba",
            clase = ClaseMovimientoInventario.Traslado, activo = true, requiere_autorizacion = false,
            usuariocreacion = "test", fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context!.alm_tipo_movimientos.Add(t);
        await _context.SaveChangesAsync();
        return t.id;
    }

    private static TrasladoDto Nuevo(int tipo, int origen, int destino, bool requiereRecepcion,
        params (int art, decimal cant)[] lineas) => new()
    {
        TipoMovimientoId = tipo, BodegaOrigenId = origen, BodegaDestinoId = destino,
        RequiereRecepcion = requiereRecepcion, Motivo = "prueba", Uuid = Guid.NewGuid(),
        Detalles = lineas.Select(l => new TrasladoDetalleDto { ArticuloId = l.art, Cantidad = l.cant }).ToList()
    };

    private async Task<decimal> ExistenciaAsync(int art, int bod)
        => (await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.articulo_id == art && u.bodega_id == bod)).existencia;
    private async Task<decimal> TransitoAsync(int art, int bod)
        => (await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.articulo_id == art && u.bodega_id == bod)).existencia_transito;
    private async Task<decimal> CostoAsync(int art, int bod)
        => (await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.articulo_id == art && u.bodega_id == bod)).costo_promedio;

    // ── 5 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Envio_ConRecepcion_BajaOrigen_CargaTransito_QuedaEnTransito()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-5");
        var destino = await SeedBodegaAsync("ZZD-5");
        var a1 = await SeedArticuloAsync("ZZT5-A");
        var a2 = await SeedArticuloAsync("ZZT5-B");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a2, origen, 50m, 20m);
        await SeedParAsync(a1, destino, 0m, 0m);
        await SeedParAsync(a2, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, requiereRecepcion: true, (a1, 30m), (a2, 20m)), "test");

        Assert.Equal(EstadoMovimientoAlmacen.EnTransito, doc.Estado);
        Assert.Equal(70m, await ExistenciaAsync(a1, origen));
        Assert.Equal(30m, await ExistenciaAsync(a2, origen));   // 50 - 20
        Assert.Equal(0m, await ExistenciaAsync(a1, destino));   // aún no recibido
        Assert.Equal(30m, await TransitoAsync(a1, destino));     // en tránsito
        Assert.Equal(20m, await TransitoAsync(a2, destino));
        // total = 30*10 + 20*20 = 700
        Assert.Equal(700m, doc.Total);
        Assert.All(doc.Detalles, d => Assert.Equal(0m, d.CantidadRecibida));
    }

    // ── 6 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task RecepcionTotal_EntraADestino_LiberaTransito_QuedaRecibido()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-6");
        var destino = await SeedBodegaAsync("ZZD-6");
        var a1 = await SeedArticuloAsync("ZZT6-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 20m, 16m);   // destino ya tenía 20 @ 16

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 30m)), "test");
        var det = doc.Detalles.Single();

        var rec = new RecepcionTrasladoDto { Uuid = Guid.NewGuid(), Lineas = [new() { MovimientoDtlId = det.Id, Cantidad = 30m }] };
        var recibido = await _service.RecibirAsync(doc.Id, rec, "test");

        Assert.Equal(EstadoMovimientoAlmacen.Recibido, recibido.Estado);
        Assert.Equal(50m, await ExistenciaAsync(a1, destino));   // 20 + 30
        Assert.Equal(0m, await TransitoAsync(a1, destino));       // tránsito liberado
        // promedio destino: (20*16 + 30*10)/50 = (320+300)/50 = 12.4
        Assert.Equal(12.4m, await CostoAsync(a1, destino));
        Assert.NotNull(recibido.FechaRecepcion);
    }

    // ── 7 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task RecepcionParcial_SigueEnTransito_LuegoCompleta_QuedaRecibido()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-7");
        var destino = await SeedBodegaAsync("ZZD-7");
        var a1 = await SeedArticuloAsync("ZZT7-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 30m)), "test");
        var det = doc.Detalles.Single();

        // Primera tanda: 12 de 30.
        var r1 = await _service.RecibirAsync(doc.Id,
            new RecepcionTrasladoDto { Uuid = Guid.NewGuid(), Lineas = [new() { MovimientoDtlId = det.Id, Cantidad = 12m }] }, "test");
        Assert.Equal(EstadoMovimientoAlmacen.EnTransito, r1.Estado);
        Assert.Equal(12m, await ExistenciaAsync(a1, destino));
        Assert.Equal(18m, await TransitoAsync(a1, destino));    // 30 - 12
        Assert.Equal(12m, r1.Detalles.Single().CantidadRecibida);

        // Segunda tanda: los 18 restantes → completa.
        var r2 = await _service.RecibirAsync(doc.Id,
            new RecepcionTrasladoDto { Uuid = Guid.NewGuid(), Lineas = [new() { MovimientoDtlId = det.Id, Cantidad = 18m }] }, "test");
        Assert.Equal(EstadoMovimientoAlmacen.Recibido, r2.Estado);
        Assert.Equal(30m, await ExistenciaAsync(a1, destino));
        Assert.Equal(0m, await TransitoAsync(a1, destino));
        Assert.Equal(2, r2.Recepciones.Count);
    }

    // ── 8a ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task OrigenIgualDestino_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var bod = await SeedBodegaAsync("ZZO-8");
        var a1 = await SeedArticuloAsync("ZZT8-A");
        await SeedParAsync(a1, bod, 10m, 5m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.EnviarAsync(Nuevo(tipo, bod, bod, true, (a1, 1m)), "test"));
        Assert.Contains("misma bodega", ex.Message);
    }

    // ── 8b ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task ArticuloSinUbicacionEnDestino_CreaElParEnDestino()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-8b");
        var destino = await SeedBodegaAsync("ZZD-8b");
        var a1 = await SeedArticuloAsync("ZZT8b-A");
        await SeedParAsync(a1, origen, 40m, 7m);   // destino NO tiene par

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 10m)), "test");

        Assert.Equal(EstadoMovimientoAlmacen.EnTransito, doc.Estado);
        // El par de destino se creó con existencia 0 y tránsito 10.
        var parD = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstOrDefaultAsync(u => u.articulo_id == a1 && u.bodega_id == destino);
        Assert.NotNull(parD);
        Assert.Equal(0m, parD!.existencia);
        Assert.Equal(10m, parD.existencia_transito);
        Assert.True(parD.activo);
    }

    // ── 9 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task RecibirMasDeLoPendiente_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-9");
        var destino = await SeedBodegaAsync("ZZD-9");
        var a1 = await SeedArticuloAsync("ZZT9-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 10m)), "test");
        var det = doc.Detalles.Single();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RecibirAsync(doc.Id,
                new RecepcionTrasladoDto { Uuid = Guid.NewGuid(), Lineas = [new() { MovimientoDtlId = det.Id, Cantidad = 11m }] }, "test"));
        Assert.Contains("pendiente", ex.Message);
    }

    // ── 10a ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Envio_MismoUuid_NoDuplica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-10");
        var destino = await SeedBodegaAsync("ZZD-10");
        var a1 = await SeedArticuloAsync("ZZT10-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var dto = Nuevo(tipo, origen, destino, true, (a1, 30m));
        var d1 = await _service!.EnviarAsync(dto, "test");
        var d2 = await _service.EnviarAsync(dto, "test");   // mismo uuid

        Assert.Equal(d1.Id, d2.Id);
        Assert.Equal(70m, await ExistenciaAsync(a1, origen));   // bajó una sola vez
        Assert.Equal(1, await _context!.alm_movimiento_hdrs.AsNoTracking().CountAsync(h => h.uuid == dto.Uuid!.Value));
    }

    // ── 10b ──────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Recepcion_MismoActoUuid_NoDuplica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-10b");
        var destino = await SeedBodegaAsync("ZZD-10b");
        var a1 = await SeedArticuloAsync("ZZT10b-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 30m)), "test");
        var det = doc.Detalles.Single();
        var rec = new RecepcionTrasladoDto { Uuid = Guid.NewGuid(), Lineas = [new() { MovimientoDtlId = det.Id, Cantidad = 12m }] };

        await _service.RecibirAsync(doc.Id, rec, "test");
        await _service.RecibirAsync(doc.Id, rec, "test");   // mismo acto

        Assert.Equal(12m, await ExistenciaAsync(a1, destino));   // entró una sola vez
        Assert.Equal(1, await _context!.alm_traslado_recepcions.AsNoTracking().CountAsync(r => r.uuid == rec.Uuid!.Value));
    }

    // ── 11 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task AnularEnTransito_DevuelveAOrigen_DescargaTransito()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-11");
        var destino = await SeedBodegaAsync("ZZD-11");
        var a1 = await SeedArticuloAsync("ZZT11-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 30m)), "test");
        Assert.Equal(70m, await ExistenciaAsync(a1, origen));

        var ok = await _service.AnularAsync(doc.Id, "me equivoqué", "test");
        Assert.True(ok);

        Assert.Equal(100m, await ExistenciaAsync(a1, origen));   // devuelto
        Assert.Equal(0m, await TransitoAsync(a1, destino));       // tránsito descargado
        Assert.Equal(0m, await ExistenciaAsync(a1, destino));
        var recargado = await _service.GetByIdAsync(doc.Id);
        Assert.Equal(EstadoMovimientoAlmacen.Anulado, recargado!.Estado);

        // Idempotente: anular otra vez no revierte de nuevo.
        Assert.True(await _service.AnularAsync(doc.Id, "otra vez", "test"));
        Assert.Equal(100m, await ExistenciaAsync(a1, origen));
    }

    // ── 11b — anular con recepción parcial revierte ambos lados ───────────────
    [SkippableFact]
    public async Task AnularConRecepcionParcial_RevierteEntradasYSalidas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-11b");
        var destino = await SeedBodegaAsync("ZZD-11b");
        var a1 = await SeedArticuloAsync("ZZT11b-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 30m)), "test");
        var det = doc.Detalles.Single();
        await _service.RecibirAsync(doc.Id,
            new RecepcionTrasladoDto { Uuid = Guid.NewGuid(), Lineas = [new() { MovimientoDtlId = det.Id, Cantidad = 12m }] }, "test");
        Assert.Equal(12m, await ExistenciaAsync(a1, destino));

        await _service.AnularAsync(doc.Id, "anular parcial", "test");

        Assert.Equal(100m, await ExistenciaAsync(a1, origen));   // todo devuelto a origen
        Assert.Equal(0m, await ExistenciaAsync(a1, destino));     // lo recibido, revertido
        Assert.Equal(0m, await TransitoAsync(a1, destino));        // lo pendiente, descargado
    }

    // ── 14 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TrasladoDirecto_UnPaso_BajaOrigenSubeDestino_QuedaRecibido()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-14");
        var destino = await SeedBodegaAsync("ZZD-14");
        var a1 = await SeedArticuloAsync("ZZT14-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, requiereRecepcion: false, (a1, 30m)), "test");

        Assert.Equal(EstadoMovimientoAlmacen.Recibido, doc.Estado);
        Assert.Equal(70m, await ExistenciaAsync(a1, origen));
        Assert.Equal(30m, await ExistenciaAsync(a1, destino));   // entró de una
        Assert.Equal(0m, await TransitoAsync(a1, destino));       // tránsito neto 0
        Assert.Equal(10m, await CostoAsync(a1, destino));         // al costo de origen
        Assert.Single(doc.Recepciones);                           // recepción automática
    }

    // ── 15 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TrasladoDirecto_Anulacion_RevierteAmbosLados()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-15");
        var destino = await SeedBodegaAsync("ZZD-15");
        var a1 = await SeedArticuloAsync("ZZT15-A");
        await SeedParAsync(a1, origen, 100m, 10m);
        await SeedParAsync(a1, destino, 0m, 0m);

        var doc = await _service!.EnviarAsync(Nuevo(tipo, origen, destino, false, (a1, 30m)), "test");
        await _service.AnularAsync(doc.Id, "revertir directo", "test");

        Assert.Equal(100m, await ExistenciaAsync(a1, origen));
        Assert.Equal(0m, await ExistenciaAsync(a1, destino));
        Assert.Equal(EstadoMovimientoAlmacen.Anulado, (await _service.GetByIdAsync(doc.Id))!.Estado);
    }

    // ── 13 ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task MovimientoAlmacen_ConClaseTraslado_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var bod = await SeedBodegaAsync("ZZO-13");
        var a1 = await SeedArticuloAsync("ZZT13-A");
        await SeedParAsync(a1, bod, 10m, 5m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _movService!.CrearYPostearAsync(new MovimientoAlmacenDto
            {
                TipoMovimientoId = tipo, BodegaId = bod, Motivo = "x",
                Detalles = [new() { ArticuloId = a1, Cantidad = 1m }]
            }, "test", puedeUsarTiposSensibles: true));
        Assert.Contains("Traslados", ex.Message);
    }

    // ── 21 — origen inactivo rechaza ──────────────────────────────────────────
    [SkippableFact]
    public async Task TrasladoDesdeParInactivo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoTrasladoAsync();
        var origen = await SeedBodegaAsync("ZZO-21");
        var destino = await SeedBodegaAsync("ZZD-21");
        var a1 = await SeedArticuloAsync("ZZT21-A");
        await SeedParAsync(a1, origen, 50m, 10m, activo: false);   // par de origen deshabilitado
        await SeedParAsync(a1, destino, 0m, 0m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.EnviarAsync(Nuevo(tipo, origen, destino, true, (a1, 10m)), "test"));
        Assert.Contains("deshabilitada", ex.Message);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
