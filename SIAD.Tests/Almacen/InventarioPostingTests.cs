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
/// Motor de posteo de inventario (Fase 1 del diseño de carga inicial).
///
/// Lo que se vigila aquí: que un movimiento deje SIEMPRE tres cosas cuadradas —el asiento
/// en el kardex, la fila del par (artículo, bodega) y el rollup de cabecera— y que
/// reintentar no duplique. El kardex es inmutable en la BD: un asiento de más no se borra,
/// solo se revierte, así que la idempotencia no es un lujo.
/// </summary>
[Collection("Postgres")]
public class InventarioPostingTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private InventarioPostingService? _motor;
    private ArticuloRollupService? _rollup;

    public InventarioPostingTests(PostgresFixture fixture) : base(fixture)
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
            _rollup = new ArticuloRollupService(_context);
            _motor = new InventarioPostingService(_context, company, _rollup);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Siembra un par (artículo, bodega) DIRECTO en la tabla, sin pasar por los servicios:
    /// así se puede fijar una existencia inicial arbitraria (incluido el caso retroactivo,
    /// que es justo el que el motor debe reconciliar).
    /// </summary>
    private async Task<(int articuloId, int bodegaId, int filaId)> SeedParAsync(
        string codigo, decimal existencia = 0m, decimal costoPromedio = 0m)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);

        var articulo = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = $"Artículo {codigo}",
            existencia = existencia
        };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        var fila = new alm_articulo_bodega
        {
            articulo_id = articulo.id,
            bodega_id = bodega.id,
            existencia = existencia,
            costo_promedio = costoPromedio,
            activo = true,
            principal = true
        };
        _context.alm_articulo_bodegas.Add(fila);
        await _context.SaveChangesAsync();

        return (articulo.id, bodega.id, fila.id);
    }

    private static MovimientoInventarioDto Apertura(int filaId, decimal cantidad, decimal costo,
        TipoMovimientoInventario tipo = TipoMovimientoInventario.CargaInicialNueva, int intento = 1)
        => new()
        {
            Tipo = tipo,
            ArticuloBodegaId = filaId,
            Cantidad = cantidad,
            CostoUnitario = costo,
            Fecha = new DateOnly(2026, 7, 31),
            DocumentoTipo = TipoDocumentoInventario.CargaInicial,
            DocumentoId = filaId,
            Intento = intento
        };

    // ── Apertura nueva ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AperturaNueva_SiembraExistenciaCostoYRollup()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId, filaId) = await SeedParAsync("ZZMP01");

        var r = await _motor!.PostearAsync(Apertura(filaId, 100m, 25m), "tester");

        Assert.False(r.YaExistia);
        Assert.Equal(100m, r.ExistenciaResultante);
        // Entrada sobre existencia 0: el promedio ES el costo de entrada (borde documentado).
        Assert.Equal(25m, r.CostoPromedioResultante);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(100m, fila.existencia);
        Assert.Equal(25m, fila.costo_promedio);
        Assert.Equal(25m, fila.ultimo_costo);

        // El asiento quedó con su trazabilidad y sus snapshots.
        var asiento = await _context.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(TipoDocumentoInventario.CargaInicial, asiento.documento_tipo);
        Assert.Equal(r.Uuid, asiento.uuid);
        Assert.Equal(100m, asiento.ingresos);
        Assert.Equal(0m, asiento.salidas);
        Assert.Equal(2500m, asiento.total);
        Assert.Equal(100m, asiento.existencia_resultante);
        Assert.Equal(25m, asiento.costo_promedio_resultante);
        Assert.NotNull(asiento.fechacreacion);   // EF no aplica el DEFAULT: lo estampa el motor
        Assert.NotNull(asiento.usuariocreacion);

        // El rollup de cabecera quedó cuadrado.
        var cab = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(100m, cab.existencia);
        Assert.Equal(100m, cab.cantidad);
        Assert.True(await _motor.TieneAperturaVigenteAsync(articuloId, bodegaId));
    }

    [SkippableFact]
    public async Task Reintento_NoDuplicaElAsiento()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP02");

        var primero = await _motor!.PostearAsync(Apertura(filaId, 50m, 10m), "tester");
        var segundo = await _motor.PostearAsync(Apertura(filaId, 50m, 10m), "tester");

        Assert.False(primero.YaExistia);
        Assert.True(segundo.YaExistia);
        Assert.Equal(primero.KardexId, segundo.KardexId);

        // Y sobre todo: la existencia NO se duplicó.
        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(50m, fila.existencia);
        Assert.Equal(1, await _context.alm_kardexs.CountAsync(k => k.uuid == primero.Uuid));
    }

    [SkippableFact]
    public async Task AperturaNueva_SobreFilaConExistencia_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP03", existencia: 40m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Apertura(filaId, 10m, 5m), "tester"));
        Assert.Contains("existencia previa 0", ex.Message);
    }

    [SkippableFact]
    public async Task SegundaApertura_DelMismoPar_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP04");
        await _motor!.PostearAsync(Apertura(filaId, 10m, 5m), "tester");

        // Intento distinto ⇒ uuid distinto ⇒ no lo frena la idempotencia, lo frena la regla.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor.PostearAsync(Apertura(filaId, 10m, 5m, intento: 2), "tester"));
        Assert.Contains("carga inicial vigente", ex.Message);
    }

    [SkippableFact]
    public async Task AperturaConCosto0_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP05");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Apertura(filaId, 10m, 0m), "tester"));
        Assert.Contains("costo", ex.Message.ToLowerInvariant());
    }

    // ── Reconciliación (el lote retroactivo) ─────────────────────────────────

    [SkippableFact]
    public async Task Reconciliacion_NoMueveLaExistencia_SoloSiembraElCosto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, _, filaId) = await SeedParAsync("ZZMP06", existencia: 80m);

        var r = await _motor!.PostearAsync(
            Apertura(filaId, 80m, 12m, TipoMovimientoInventario.CargaInicialReconciliacion), "tester");

        // La existencia NO se duplica: el asiento describe lo que ya había.
        Assert.Equal(80m, r.ExistenciaResultante);
        Assert.Equal(12m, r.CostoPromedioResultante);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(80m, fila.existencia);
        Assert.Equal(12m, fila.costo_promedio);

        var cab = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(80m, cab.existencia);
    }

    [SkippableFact]
    public async Task Reconciliacion_ConCantidadDistintaALaExistencia_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP07", existencia: 80m);

        // La reconciliación no acepta que le dicten la cifra.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(
                Apertura(filaId, 79m, 12m, TipoMovimientoInventario.CargaInicialReconciliacion), "tester"));
        Assert.Contains("exactamente la existencia registrada", ex.Message);
    }

    // ── Ajustes ──────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AjustePositivo_AplicaPromedioPonderado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP08");
        await _motor!.PostearAsync(Apertura(filaId, 100m, 10m), "tester"); // 100 @ 10

        var r = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjustePositivo,
            ArticuloBodegaId = filaId,
            Cantidad = 100m,
            CostoUnitario = 20m,
            Fecha = new DateOnly(2026, 8, 1),
            DocumentoTipo = TipoDocumentoInventario.Ajuste,
            DocumentoId = 1
        }, "tester");

        // (100×10 + 100×20) / 200 = 15
        Assert.Equal(200m, r.ExistenciaResultante);
        Assert.Equal(15m, r.CostoPromedioResultante);
    }

    [SkippableFact]
    public async Task AjusteNegativo_NoCambiaElCostoPromedio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP09");
        await _motor!.PostearAsync(Apertura(filaId, 100m, 10m), "tester");

        var r = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjusteNegativo,
            ArticuloBodegaId = filaId,
            Cantidad = 30m,
            Fecha = new DateOnly(2026, 8, 1),
            DocumentoTipo = TipoDocumentoInventario.Ajuste,
            DocumentoId = 2
        }, "tester");

        // Una salida sale AL promedio vigente: no lo mueve.
        Assert.Equal(70m, r.ExistenciaResultante);
        Assert.Equal(10m, r.CostoPromedioResultante);
    }

    [SkippableFact]
    public async Task AjusteNegativo_QueDejariaNegativo_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP10");
        await _motor!.PostearAsync(Apertura(filaId, 10m, 5m), "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.AjusteNegativo,
                ArticuloBodegaId = filaId,
                Cantidad = 11m,
                Fecha = new DateOnly(2026, 8, 1),
                DocumentoTipo = TipoDocumentoInventario.Ajuste,
                DocumentoId = 3
            }, "tester"));
        Assert.Contains("negativo", ex.Message);
    }

    [SkippableFact]
    public async Task AjusteValor_CorrigeCostoSinMoverExistencia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP11");
        await _motor!.PostearAsync(Apertura(filaId, 40m, 7m), "tester");

        var r = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjusteValor,
            ArticuloBodegaId = filaId,
            Cantidad = 0m,
            CostoUnitario = 9m,
            Fecha = new DateOnly(2026, 8, 1),
            DocumentoTipo = TipoDocumentoInventario.Ajuste,
            DocumentoId = 4
        }, "tester");

        Assert.Equal(40m, r.ExistenciaResultante);
        Assert.Equal(9m, r.CostoPromedioResultante);
    }

    // ── Reversa y reapertura ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task Reversa_DejaElParSinAperturaVigente_YPermiteReabrir()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId, filaId) = await SeedParAsync("ZZMP12");
        var apertura = await _motor!.PostearAsync(Apertura(filaId, 100m, 10m), "tester");
        Assert.True(await _motor.TieneAperturaVigenteAsync(articuloId, bodegaId));

        // Revertir la apertura mal costeada.
        await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = 100m,
            CostoUnitario = 10m,
            Fecha = new DateOnly(2026, 8, 1),
            DocumentoTipo = TipoDocumentoInventario.CargaInicial,
            DocumentoId = filaId,
            KardexIdRevertido = apertura.KardexId
        }, "tester");

        Assert.False(await _motor.TieneAperturaVigenteAsync(articuloId, bodegaId));

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(0m, fila.existencia);

        // Reabrir con el costo correcto: intento 2 ⇒ uuid distinto ⇒ no choca con el único.
        var reapertura = await _motor.PostearAsync(Apertura(filaId, 100m, 18m, intento: 2), "tester");
        Assert.False(reapertura.YaExistia);
        Assert.Equal(100m, reapertura.ExistenciaResultante);
        Assert.Equal(18m, reapertura.CostoPromedioResultante);
        Assert.True(await _motor.TieneAperturaVigenteAsync(articuloId, bodegaId));
    }

    // ── Tipos no implementados ───────────────────────────────────────────────

    [SkippableFact]
    public async Task TipoNoSoportado_LanzaNotSupported()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP13");

        await Assert.ThrowsAsync<NotSupportedException>(
            () => _motor!.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = (TipoMovimientoInventario)99,
                ArticuloBodegaId = filaId,
                Cantidad = 1m,
                CostoUnitario = 1m,
                Fecha = new DateOnly(2026, 8, 1),
                DocumentoTipo = TipoDocumentoInventario.Compra,
                DocumentoId = 1
            }, "tester"));
    }

    // ── Compra (recepción de factura de proveedor) ───────────────────────────

    private static MovimientoInventarioDto Compra(int filaId, decimal cantidad, decimal costo,
        int lineaCompraId, string? documentoTipo = null)
        => new()
        {
            Tipo = TipoMovimientoInventario.Compra,
            ArticuloBodegaId = filaId,
            Cantidad = cantidad,
            CostoUnitario = costo,
            Fecha = new DateOnly(2026, 7, 31),
            // Lo que mande el llamador aquí es indiferente: el motor fuerza COMPRA.
            DocumentoTipo = documentoTipo ?? TipoDocumentoInventario.Compra,
            DocumentoId = lineaCompraId
        };

    [SkippableFact]
    public async Task Compra_EntraConPromedioPonderadoMovil()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // 100 @ 25 en existencia; entran 100 @ 35 ⇒ 200 @ 30 (promedio ponderado).
        var (articuloId, _, filaId) = await SeedParAsync("ZZMP20", existencia: 100m, costoPromedio: 25m);

        var r = await _motor!.PostearAsync(Compra(filaId, 100m, 35m, lineaCompraId: 5001), "tester");

        Assert.False(r.YaExistia);
        Assert.Equal(200m, r.ExistenciaResultante);
        Assert.Equal(30m, r.CostoPromedioResultante);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(200m, fila.existencia);
        Assert.Equal(30m, fila.costo_promedio);
        // El último costo es el de ESTA compra, no el promedio.
        Assert.Equal(35m, fila.ultimo_costo);

        var asiento = await _context.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(TipoDocumentoInventario.Compra, asiento.documento_tipo);
        Assert.Equal(5001, asiento.documento_id);
        Assert.Equal(TipoTransaccionKardex.EntradaInventarioInicial, asiento.tipo_transaccion);   // 102 = entrada
        Assert.False(asiento.es_ajuste);                                                          // una compra NO es ajuste
        Assert.Equal(100m, asiento.ingresos);
        Assert.Equal(0m, asiento.salidas);
        Assert.Equal(3500m, asiento.total);
        Assert.Equal(3500m, asiento.debe);
        Assert.Equal(0m, asiento.haber);
        Assert.Equal(200m, asiento.existencia_resultante);
        Assert.Equal(30m, asiento.costo_promedio_resultante);

        var cab = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(200m, cab.existencia);
        // Una compra no abre el par: la apertura sigue siendo cosa de la carga inicial.
        Assert.False(await _motor.TieneAperturaVigenteAsync(cab.id, fila.bodega_id));
    }

    [SkippableFact]
    public async Task Compra_SobreParVacio_TomaElCostoDeEntrada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Borde del promedio ponderado: sin existencia previa no hay con qué ponderar.
        var (_, _, filaId) = await SeedParAsync("ZZMP21");

        var r = await _motor!.PostearAsync(Compra(filaId, 10m, 12.5m, lineaCompraId: 5002), "tester");

        Assert.Equal(10m, r.ExistenciaResultante);
        Assert.Equal(12.5m, r.CostoPromedioResultante);
    }

    [SkippableFact]
    public async Task Compra_Reintento_NoDuplicaLaExistencia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP22", existencia: 20m, costoPromedio: 10m);

        var primero = await _motor!.PostearAsync(Compra(filaId, 30m, 20m, lineaCompraId: 5003), "tester");
        var segundo = await _motor.PostearAsync(Compra(filaId, 30m, 20m, lineaCompraId: 5003), "tester");

        Assert.False(primero.YaExistia);
        Assert.True(segundo.YaExistia);
        Assert.Equal(primero.KardexId, segundo.KardexId);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(50m, fila.existencia);
        Assert.Equal(1, await _context.alm_kardexs.CountAsync(k => k.uuid == primero.Uuid));
    }

    [SkippableFact]
    public async Task Compra_ElUuidNoDependeDelDocumentoTipoQueMandeElLlamador()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Misma línea de recepción, pero el llamador manda otro documento_tipo: si el uuid
        // se derivara de ese texto, la línea entraría DOS veces al inventario.
        var (_, _, filaId) = await SeedParAsync("ZZMP23");

        var primero = await _motor!.PostearAsync(Compra(filaId, 10m, 5m, lineaCompraId: 5004), "tester");
        var segundo = await _motor.PostearAsync(
            Compra(filaId, 10m, 5m, lineaCompraId: 5004, documentoTipo: TipoDocumentoInventario.Ajuste), "tester");

        Assert.True(segundo.YaExistia);
        Assert.Equal(primero.Uuid, segundo.Uuid);

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == primero.KardexId);
        Assert.Equal(TipoDocumentoInventario.Compra, asiento.documento_tipo);

        var fila = await _context.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(10m, fila.existencia);
    }

    [SkippableFact]
    public async Task Compra_ConCostoCero_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP24");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Compra(filaId, 5m, 0m, lineaCompraId: 5005), "tester"));
        Assert.Contains("costo unitario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Compra_SinLineaDeRecepcion_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP25");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Compra(filaId, 5m, 10m, lineaCompraId: 0), "tester"));
        Assert.Contains("documento", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Compra_ConCantidadCero_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP26");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Compra(filaId, 0m, 10m, lineaCompraId: 5006), "tester"));
    }

    [SkippableFact]
    public async Task Reversa_DeUnaCompra_DevuelveElCostoPromedioAlValorPrevio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // 18 unidades a 56.3889 (el promedio que dejaría una compra previa).
        var (_, _, filaId) = await SeedParAsync("ZZMP27", existencia: 18m, costoPromedio: 56.3889m);

        // Entra una compra a un costo distinto: el promedio se mueve.
        var compra = await _motor!.PostearAsync(Compra(filaId, 6m, 58m, lineaCompraId: 5007), "tester");
        Assert.Equal(24m, compra.ExistenciaResultante);
        Assert.Equal(56.7917m, Math.Round(compra.CostoPromedioResultante, 4));

        // Al revertirla, el inventario debe quedar como si esa compra nunca hubiera existido.
        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = 6m,
            CostoUnitario = 58m,
            Fecha = new DateOnly(2026, 7, 31),
            KardexIdRevertido = compra.KardexId
        }, "tester");

        Assert.Equal(18m, reversa.ExistenciaResultante);
        Assert.Equal(56.3889m, Math.Round(reversa.CostoPromedioResultante, 4));

        // Y el valor del inventario vuelve exacto: 18 × 56.3889 = 1,015.00
        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(1015.00m, Math.Round(fila.existencia * fila.costo_promedio, 2));
    }

    [SkippableFact]
    public async Task Reversa_DeUnaApertura_DejaExistenciaCeroSinInventarCosto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP28");
        var apertura = await _motor!.PostearAsync(Apertura(filaId, 40m, 15m), "tester");

        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = 40m,
            CostoUnitario = 15m,
            Fecha = new DateOnly(2026, 7, 31),
            KardexIdRevertido = apertura.KardexId
        }, "tester");

        // Sin unidades no hay nada que valorizar: se conserva el costo, no se fabrica uno.
        Assert.Equal(0m, reversa.ExistenciaResultante);
        Assert.Equal(15m, reversa.CostoPromedioResultante);
    }

    // ── Salida por descargo (entrega de materiales) ──────────────────────────

    private static MovimientoInventarioDto Salida(int filaId, decimal cantidad, int lineaDescargoId,
        decimal costoQueSeIgnora = 0m)
        => new()
        {
            Tipo = TipoMovimientoInventario.SalidaDescargo,
            ArticuloBodegaId = filaId,
            Cantidad = cantidad,
            // La salida sale al promedio vigente: lo que venga aquí NO se usa.
            CostoUnitario = costoQueSeIgnora,
            Fecha = new DateOnly(2026, 7, 31),
            DocumentoTipo = TipoDocumentoInventario.Descargo,
            DocumentoId = lineaDescargoId
        };

    [SkippableFact]
    public async Task Salida_DescuentaExistenciaYNoMueveElPromedio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, _, filaId) = await SeedParAsync("ZZMP30", existencia: 100m, costoPromedio: 20m);

        var r = await _motor!.PostearAsync(Salida(filaId, 30m, lineaDescargoId: 7001), "tester");

        Assert.Equal(70m, r.ExistenciaResultante);
        Assert.Equal(20m, r.CostoPromedioResultante);   // una salida nunca re-pondera

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(TipoDocumentoInventario.Descargo, asiento.documento_tipo);
        Assert.Equal(7001, asiento.documento_id);
        Assert.Equal(TipoTransaccionKardex.Salida, asiento.tipo_transaccion);   // 202
        Assert.False(asiento.es_ajuste);                                        // no es un ajuste
        Assert.Equal(30m, asiento.salidas);
        Assert.Equal(0m, asiento.ingresos);
        Assert.Equal(600m, asiento.total);              // 30 × 20 (promedio), no × lo del DTO
        Assert.Equal(600m, asiento.haber);

        var cab = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(70m, cab.existencia);
    }

    [SkippableFact]
    public async Task Salida_IgnoraElCostoQueMandeElLlamador()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP31", existencia: 50m, costoPromedio: 12m);

        // El llamador manda 999: si el motor lo usara, valorizaría la salida a un costo inventado.
        var r = await _motor!.PostearAsync(Salida(filaId, 10m, 7002, costoQueSeIgnora: 999m), "tester");

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(12m, asiento.valor_unitario);
        Assert.Equal(120m, asiento.total);
    }

    [SkippableFact]
    public async Task Salida_SinExistenciaSuficiente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP32", existencia: 5m, costoPromedio: 10m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Salida(filaId, 6m, 7003), "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Salida_SobreParSinCostoPromedio_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El caso del mirror: 241 de 245 pares con existencia tienen costo 0 hasta el corte.
        var (_, _, filaId) = await SeedParAsync("ZZMP33", existencia: 40m, costoPromedio: 0m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Salida(filaId, 1m, 7004), "tester"));
        Assert.Contains("costo promedio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Salida_SinLineaDeDescargo_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP34", existencia: 10m, costoPromedio: 5m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Salida(filaId, 1m, lineaDescargoId: 0), "tester"));
    }

    [SkippableFact]
    public async Task Salida_Reintento_NoDescuentaDosVeces()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP35", existencia: 20m, costoPromedio: 8m);

        var primera = await _motor!.PostearAsync(Salida(filaId, 5m, 7005), "tester");
        var segunda = await _motor.PostearAsync(Salida(filaId, 5m, 7005), "tester");

        Assert.False(primera.YaExistia);
        Assert.True(segunda.YaExistia);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(15m, fila.existencia);
    }

    [SkippableFact]
    public async Task Salida_DosDescargosDeLaMismaRequisicion_SonAsientosDistintos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El caso que rompía anclar el uuid a la requisición: 28 pares del histórico tienen
        // dos entregas. Con el uuid en la LÍNEA DE DESCARGO, las dos se postean.
        var (_, _, filaId) = await SeedParAsync("ZZMP36", existencia: 20m, costoPromedio: 10m);

        var primera = await _motor!.PostearAsync(Salida(filaId, 6m, lineaDescargoId: 7006), "tester");
        var segunda = await _motor.PostearAsync(Salida(filaId, 4m, lineaDescargoId: 7007), "tester");

        Assert.False(segunda.YaExistia);
        Assert.NotEqual(primera.Uuid, segunda.Uuid);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);
        Assert.Equal(10m, fila.existencia);
    }

    // ── Reversa espejo: anular una salida DEVUELVE la mercadería ─────────────

    [SkippableFact]
    public async Task ReversaDeUnaSalida_DevuelveLaMercaderiaALaBodega()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // ESTE es el defecto que se corrige: antes, revertir una salida volvía a restar.
        var (_, _, filaId) = await SeedParAsync("ZZMP37", existencia: 100m, costoPromedio: 25m);

        var salida = await _motor!.PostearAsync(Salida(filaId, 40m, 7008), "tester");
        Assert.Equal(60m, salida.ExistenciaResultante);

        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = 40m,
            CostoUnitario = 25m,
            Fecha = new DateOnly(2026, 7, 31),
            KardexIdRevertido = salida.KardexId
        }, "tester");

        // Vuelve a 100, no baja a 20.
        Assert.Equal(100m, reversa.ExistenciaResultante);
        Assert.Equal(25m, reversa.CostoPromedioResultante);

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == reversa.KardexId);
        Assert.Equal(40m, asiento.ingresos);                                    // ENTRA
        Assert.Equal(0m, asiento.salidas);
        Assert.Equal(TipoTransaccionKardex.EntradaInventarioInicial, asiento.tipo_transaccion);  // 102
    }

    [SkippableFact]
    public async Task ReversaDeUnaSalida_TomaCantidadYCostoDelAsientoOriginal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP38", existencia: 50m, costoPromedio: 30m);
        var salida = await _motor!.PostearAsync(Salida(filaId, 20m, 7009), "tester");

        // El DTO miente en cantidad y costo: el motor debe ignorarlo y usar lo posteado.
        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = 999m,
            CostoUnitario = 1m,
            Fecha = new DateOnly(2026, 7, 31),
            KardexIdRevertido = salida.KardexId
        }, "tester");

        Assert.Equal(50m, reversa.ExistenciaResultante);   // 30 + 20, no 30 + 999
        Assert.Equal(30m, reversa.CostoPromedioResultante);

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == reversa.KardexId);
        Assert.Equal(20m, asiento.cantidad);
        Assert.Equal(30m, asiento.valor_unitario);
    }

    [SkippableFact]
    public async Task Reversa_DeUnaReversa_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP39", existencia: 30m, costoPromedio: 10m);
        var salida = await _motor!.PostearAsync(Salida(filaId, 10m, 7010), "tester");

        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Cantidad = 10m,
            CostoUnitario = 10m,
            Fecha = new DateOnly(2026, 7, 31),
            KardexIdRevertido = salida.KardexId
        }, "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.Reversa,
                ArticuloBodegaId = filaId,
                Cantidad = 10m,
                CostoUnitario = 10m,
                Fecha = new DateOnly(2026, 7, 31),
                KardexIdRevertido = reversa.KardexId
            }, "tester"));
        Assert.Contains("revertir una reversa", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task ReversaDeUnaEntrada_SinExistenciaSuficiente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZMP40", existencia: 10m, costoPromedio: 10m);
        var compra = await _motor!.PostearAsync(Compra(filaId, 10m, 12m, lineaCompraId: 7011), "tester");

        // Se consume casi todo lo que había: revertir la compra dejaría negativo.
        await _motor.PostearAsync(Salida(filaId, 15m, 7012), "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.Reversa,
                ArticuloBodegaId = filaId,
                Cantidad = 10m,
                CostoUnitario = 12m,
                Fecha = new DateOnly(2026, 7, 31),
                KardexIdRevertido = compra.KardexId
            }, "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Rollup compartido ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Rollup_SumaSoloBodegasActivas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, _, _) = await SeedParAsync("ZZMP14", existencia: 30m);

        // Segunda bodega, DESHABILITADA y con stock remanente: no debe contar.
        var otra = new alm_bodega { codigo = "ZZMP14B", nombre = "Bodega B", activo = true };
        _context!.alm_bodegas.Add(otra);
        await _context.SaveChangesAsync();
        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = otra.id,
            existencia = 999m,
            activo = false
        });
        await _context.SaveChangesAsync();

        await _rollup!.RecomputeAsync(articuloId);

        var cab = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        Assert.Equal(30m, cab.existencia);
        Assert.Equal(30m, cab.cantidad);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
