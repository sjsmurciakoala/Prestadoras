using System;
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
/// Motor de posteo — lado de bajo nivel del traslado entre bodegas (Fase 5.1 del diseño
/// <c>docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md</c>, §3 y plan de pruebas §7).
/// <para>
/// Lo que se vigila: que <see cref="TipoMovimientoInventario.TrasladoSalida"/> reste de origen sin
/// mover su promedio, que <see cref="TipoMovimientoInventario.TrasladoEntrada"/> pondere el promedio
/// de destino al costo que viaja, que ambos asienten <c>documento_tipo = TRASLADO</c> con
/// <c>bodega_destino_id</c>, y que la reversa-espejo funcione en los TRES sitios — en particular que
/// anular el envío tras despachar TODO el stock de origen NO falle por la guarda de negativo
/// (hallazgo R-2 de la revisión adversarial).
/// </para>
/// </summary>
[Collection("Postgres")]
public class InventarioPostingTrasladoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private InventarioPostingService? _motor;

    public InventarioPostingTrasladoTests(PostgresFixture fixture) : base(fixture) { }

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
            _motor = new InventarioPostingService(_context, company, rollup);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    private sealed record ParTraslado(
        int ArticuloId, int OrigenBodegaId, int OrigenFilaId, int DestinoBodegaId, int DestinoFilaId);

    /// <summary>
    /// Un artículo con dos ubicaciones: origen (con existencia y costo) y destino. Directo a la
    /// tabla, para fijar el estado inicial sin pasar por los servicios.
    /// </summary>
    private async Task<ParTraslado> SeedParTrasladoAsync(
        string codigo, decimal existenciaOrigen, decimal costoOrigen,
        decimal existenciaDestino = 0m, decimal costoDestino = 0m)
    {
        var origen = new alm_bodega { codigo = $"{codigo}-O", nombre = $"Origen {codigo}", activo = true };
        var destino = new alm_bodega { codigo = $"{codigo}-D", nombre = $"Destino {codigo}", activo = true };
        _context!.alm_bodegas.AddRange(origen, destino);

        var articulo = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = $"Artículo {codigo}",
            existencia = existenciaOrigen + existenciaDestino,
            activo = true
        };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        var filaO = new alm_articulo_bodega
        {
            articulo_id = articulo.id, bodega_id = origen.id,
            existencia = existenciaOrigen, costo_promedio = costoOrigen, activo = true, principal = true
        };
        var filaD = new alm_articulo_bodega
        {
            articulo_id = articulo.id, bodega_id = destino.id,
            existencia = existenciaDestino, costo_promedio = costoDestino, activo = true, principal = false
        };
        _context.alm_articulo_bodegas.AddRange(filaO, filaD);
        await _context.SaveChangesAsync();

        return new ParTraslado(articulo.id, origen.id, filaO.id, destino.id, filaD.id);
    }

    private MovimientoInventarioDto Salida(ParTraslado p, decimal cantidad, int documentoId = 5001)
        => new()
        {
            Tipo = TipoMovimientoInventario.TrasladoSalida,
            ArticuloBodegaId = p.OrigenFilaId,
            Cantidad = cantidad,
            Fecha = new DateOnly(2026, 8, 4),
            DocumentoId = documentoId,
            BodegaDestinoId = p.DestinoBodegaId
        };

    private MovimientoInventarioDto Entrada(ParTraslado p, decimal cantidad, decimal costo, int documentoId = 6001)
        => new()
        {
            Tipo = TipoMovimientoInventario.TrasladoEntrada,
            ArticuloBodegaId = p.DestinoFilaId,
            Cantidad = cantidad,
            CostoUnitario = costo,
            Fecha = new DateOnly(2026, 8, 4),
            DocumentoId = documentoId,
            BodegaDestinoId = p.OrigenBodegaId   // informativa
        };

    private MovimientoInventarioDto Reversa(int filaId, int kardexId, decimal cantidad, decimal costo)
        => new()
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = cantidad,
            CostoUnitario = costo,
            Fecha = new DateOnly(2026, 8, 4),
            KardexIdRevertido = kardexId
        };

    private Task<alm_articulo_bodega> FilaAsync(int filaId)
        => _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);

    // ── 1 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TrasladoSalida_RestaDeOrigen_SinMoverPromedio_YAsientaTraslado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var p = await SeedParTrasladoAsync("ZZTR-S1", existenciaOrigen: 100m, costoOrigen: 10m);

        var r = await _motor!.PostearAsync(Salida(p, 30m), "tester");

        var origen = await FilaAsync(p.OrigenFilaId);
        Assert.Equal(70m, origen.existencia);
        Assert.Equal(10m, origen.costo_promedio);   // una salida NO mueve el promedio

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(TipoDocumentoInventario.Traslado, asiento.documento_tipo);
        Assert.Equal(p.DestinoBodegaId, asiento.bodega_destino_id);
        Assert.Equal(p.OrigenBodegaId, asiento.bodega_id);
        Assert.Equal(30m, asiento.salidas);
        Assert.Equal(0m, asiento.ingresos);
        Assert.False(asiento.es_ajuste);
        Assert.Equal(70m, asiento.existencia_resultante);
    }

    // ── 2 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TrasladoEntrada_SumaADestino_PonderaElPromedio_AlCostoQueViaja()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Destino ya tenía 20 @ 8; entran 30 @ 10 → 50 @ (160+300)/50 = 9.2
        var p = await SeedParTrasladoAsync("ZZTR-E1", existenciaOrigen: 0m, costoOrigen: 0m,
            existenciaDestino: 20m, costoDestino: 8m);

        var r = await _motor!.PostearAsync(Entrada(p, 30m, 10m), "tester");

        var destino = await FilaAsync(p.DestinoFilaId);
        Assert.Equal(50m, destino.existencia);
        Assert.Equal(9.2m, destino.costo_promedio);

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(TipoDocumentoInventario.Traslado, asiento.documento_tipo);
        Assert.Equal(p.DestinoBodegaId, asiento.bodega_id);
        Assert.Equal(30m, asiento.ingresos);
        Assert.Equal(0m, asiento.salidas);
        Assert.False(asiento.es_ajuste);
    }

    // ── 3a ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task ReversaDeTrasladoSalida_DevuelveAOrigen()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var p = await SeedParTrasladoAsync("ZZTR-R1", existenciaOrigen: 100m, costoOrigen: 10m);
        var salida = await _motor!.PostearAsync(Salida(p, 30m), "tester");   // origen → 70

        await _motor.PostearAsync(Reversa(p.OrigenFilaId, salida.KardexId, 30m, 10m), "tester");

        var origen = await FilaAsync(p.OrigenFilaId);
        Assert.Equal(100m, origen.existencia);      // devuelta
        Assert.Equal(10m, origen.costo_promedio);
    }

    // ── 3b ───────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task ReversaDeTrasladoEntrada_SacaDeDestino_YDespondera()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var p = await SeedParTrasladoAsync("ZZTR-R2", existenciaOrigen: 0m, costoOrigen: 0m,
            existenciaDestino: 20m, costoDestino: 8m);
        var entrada = await _motor!.PostearAsync(Entrada(p, 30m, 10m), "tester");   // destino → 50 @ 9.2

        await _motor.PostearAsync(Reversa(p.DestinoFilaId, entrada.KardexId, 30m, 10m), "tester");

        var destino = await FilaAsync(p.DestinoFilaId);
        Assert.Equal(20m, destino.existencia);       // sacada
        // valorRestante = 50*9.2 - 30*10 = 160; 160/20 = 8 → vuelve al promedio previo
        Assert.Equal(8m, destino.costo_promedio);
    }

    // ── 4 ────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task TrasladoSalida_QueDejariaNegativo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var p = await SeedParTrasladoAsync("ZZTR-NEG", existenciaOrigen: 5m, costoOrigen: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.PostearAsync(Salida(p, 6m), "tester"));

        Assert.Equal(5m, (await FilaAsync(p.OrigenFilaId)).existencia);   // nada a medias
    }

    // ── Guarda de costo 0 (no despachar sin costo) ────────────────────────────
    [SkippableFact]
    public async Task TrasladoSalida_SinCostoPromedio_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var p = await SeedParTrasladoAsync("ZZTR-C0", existenciaOrigen: 50m, costoOrigen: 0m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.PostearAsync(Salida(p, 10m), "tester"));
        Assert.Contains("costo promedio", ex.Message);
    }

    // ── 17 (R-2, crítico) ─────────────────────────────────────────────────────
    [SkippableFact]
    public async Task ReversaDeTrasladoSalida_TrasVaciarOrigen_NoFallaPorGuardaDeNegativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Se traslada TODO el stock de origen: origen queda en 0. Es el caso normal en producción,
        // y el que rompía la implementación literal (la guarda de negativo evaluaba 0 - 30 < 0).
        var p = await SeedParTrasladoAsync("ZZTR-R2F", existenciaOrigen: 30m, costoOrigen: 10m);
        var salida = await _motor!.PostearAsync(Salida(p, 30m), "tester");
        Assert.Equal(0m, (await FilaAsync(p.OrigenFilaId)).existencia);

        // Anular el envío devuelve la mercadería: NO debe lanzar «la mercadería ya salió».
        var r = await _motor.PostearAsync(Reversa(p.OrigenFilaId, salida.KardexId, 30m, 10m), "tester");
        Assert.False(r.YaExistia);

        var origen = await FilaAsync(p.OrigenFilaId);
        Assert.Equal(30m, origen.existencia);
        Assert.Equal(10m, origen.costo_promedio);
    }

    // ── DeferirRollup: el motor no toca la cabecera del artículo ───────────────
    [SkippableFact]
    public async Task DeferirRollup_NoRecalculaLaCabeceraDelArticulo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var p = await SeedParTrasladoAsync("ZZTR-DEF", existenciaOrigen: 100m, costoOrigen: 10m);
        var antes = (await _context!.alm_articulos.AsNoTracking().FirstAsync(a => a.id == p.ArticuloId)).existencia;

        await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.TrasladoSalida,
            ArticuloBodegaId = p.OrigenFilaId,
            Cantidad = 30m,
            Fecha = new DateOnly(2026, 8, 4),
            DocumentoId = 5001,
            BodegaDestinoId = p.DestinoBodegaId,
            DeferirRollup = true
        }, "tester");

        // La fila del par sí bajó, pero la cabecera del artículo NO se recomputó (la difiere el servicio).
        Assert.Equal(70m, (await FilaAsync(p.OrigenFilaId)).existencia);
        var despues = (await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == p.ArticuloId)).existencia;
        Assert.Equal(antes, despues);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
