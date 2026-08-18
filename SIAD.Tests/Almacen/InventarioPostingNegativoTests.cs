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
/// Motor de posteo — EXISTENCIA NEGATIVA en salidas (F1 del plan
/// docs/plans/2026-08-15-existencia-negativa-salidas-plan.md).
/// <para>
/// Lo que se vigila: (1) el interruptor —empresa (cfg_inventario_negativo) con override por
/// bodega (alm_bodega.permite_existencia_negativa)— gobierna si una salida puede cruzar a
/// negativo, en las TRES ramas del motor (SalidaDescargo, AjusteNegativo, TrasladoSalida);
/// (2) una entrada sobre existencia negativa NO pondera contra la base negativa (el promedio
/// queda en el costo del lote, nunca distorsionado ni negativo); (3) revertir una salida
/// genérica (AjusteNegativo) DEVUELVE la mercadería, no la vuelve a restar.
/// </para>
/// </summary>
[Collection("Postgres")]
public class InventarioPostingNegativoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private InventarioPostingService? _motor;

    public InventarioPostingNegativoTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);
            _motor = new InventarioPostingService(_context, company, new ArticuloRollupService(_context));
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    private async Task<(int articuloId, int bodegaId, int filaId)> SeedParAsync(
        string codigo, decimal existencia = 0m, decimal costoPromedio = 0m, decimal minima = 0m)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);
        var articulo = new alm_articulo { codigo_articulo = codigo, descripcion = $"Artículo {codigo}", existencia = existencia };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        var fila = new alm_articulo_bodega
        {
            articulo_id = articulo.id, bodega_id = bodega.id,
            existencia = existencia, costo_promedio = costoPromedio, existencia_minima = minima,
            activo = true, principal = true
        };
        _context.alm_articulo_bodegas.Add(fila);
        await _context.SaveChangesAsync();
        return (articulo.id, bodega.id, fila.id);
    }

    /// <summary>Enciende/apaga el interruptor MAESTRO de la empresa (cfg_inventario_negativo).</summary>
    private async Task SetInterruptorEmpresaAsync(bool permitir)
    {
        var cfg = await _context!.cfg_inventario_negativos.FirstOrDefaultAsync();
        if (cfg is null)
        {
            _context.cfg_inventario_negativos.Add(new cfg_inventario_negativo { permitir = permitir });
        }
        else
        {
            cfg.permitir = permitir;
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>Fija el override por bodega (NULL = hereda, true/false = fuerza).</summary>
    private async Task SetOverrideBodegaAsync(int bodegaId, bool? valor)
    {
        var b = await _context!.alm_bodegas.FirstAsync(x => x.id == bodegaId);
        b.permite_existencia_negativa = valor;
        await _context.SaveChangesAsync();
    }

    private static MovimientoInventarioDto Descargo(int filaId, decimal cantidad, int lineaDescargoId)
        => new()
        {
            Tipo = TipoMovimientoInventario.SalidaDescargo,
            ArticuloBodegaId = filaId, Cantidad = cantidad,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoTipo = TipoDocumentoInventario.Descargo, DocumentoId = lineaDescargoId
        };

    private static MovimientoInventarioDto AjusteNegativo(int filaId, decimal cantidad, int docId)
        => new()
        {
            Tipo = TipoMovimientoInventario.AjusteNegativo,
            ArticuloBodegaId = filaId, Cantidad = cantidad,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoTipo = TipoDocumentoInventario.Ajuste, DocumentoId = docId
        };

    private Task<alm_articulo_bodega> ParAsync(int filaId)
        => _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.id == filaId);

    // ── 1. Interruptor apagado: sigue bloqueando (comportamiento actual) ─────────

    [SkippableFact]
    public async Task Salida_InterruptorApagado_SigueBloqueando()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG01", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(false);   // explícito (F0 ya lo sembró en false)

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Descargo(filaId, 8m, 8001), "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(5m, (await ParAsync(filaId)).existencia);   // nada se movió
    }

    // ── 2. Interruptor de empresa encendido: permite negativo en las 3 ramas ─────

    [SkippableFact]
    public async Task Descargo_InterruptorEmpresaEncendido_PermiteNegativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG02", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);

        var r = await _motor!.PostearAsync(Descargo(filaId, 8m, 8002), "tester");

        Assert.Equal(-3m, r.ExistenciaResultante);
        Assert.Equal(10m, r.CostoPromedioResultante);   // una salida no re-pondera, ni en negativo
        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == r.KardexId);
        Assert.Equal(-3m, asiento.existencia_resultante);
        Assert.Equal(8m, asiento.salidas);
    }

    [SkippableFact]
    public async Task AjusteNegativo_InterruptorEncendido_PermiteNegativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG03", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);

        var r = await _motor!.PostearAsync(AjusteNegativo(filaId, 8m, 8003), "tester");

        Assert.Equal(-3m, r.ExistenciaResultante);
        Assert.Equal(-3m, (await ParAsync(filaId)).existencia);
    }

    [SkippableFact]
    public async Task TrasladoSalida_InterruptorEncendido_PermiteNegativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG04", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);

        var r = await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.TrasladoSalida,
            ArticuloBodegaId = filaId, Cantidad = 8m,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoId = 8004
        }, "tester");

        Assert.Equal(-3m, r.ExistenciaResultante);
    }

    // ── 3. Override por bodega gana sobre la empresa (tri-estado) ────────────────

    [SkippableFact]
    public async Task OverrideBodegaTrue_PermiteAunqueLaEmpresaBloquee()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, bodegaId, filaId) = await SeedParAsync("ZZNEG05", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(false);
        await SetOverrideBodegaAsync(bodegaId, true);

        var r = await _motor!.PostearAsync(Descargo(filaId, 8m, 8005), "tester");
        Assert.Equal(-3m, r.ExistenciaResultante);
    }

    [SkippableFact]
    public async Task OverrideBodegaFalse_BloqueaAunqueLaEmpresaPermita()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, bodegaId, filaId) = await SeedParAsync("ZZNEG06", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);
        await SetOverrideBodegaAsync(bodegaId, false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Descargo(filaId, 8m, 8006), "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── 4. Costeo: una entrada sobre existencia NEGATIVA no pondera la base ──────

    [SkippableFact]
    public async Task EntradaSobreExistenciaNegativa_PromedioEsElCostoDelLote()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Par ya en negativo (una salida anterior lo dejó así). Base negativa: -3 @ 10.
        var (_, _, filaId) = await SeedParAsync("ZZNEG07", existencia: -3m, costoPromedio: 10m);

        // Entra un ajuste positivo de 10 @ 20 -> existencia 7. El ponderado clásico daría
        // ((-3*10)+(10*20))/7 = 24.2857… (un costo inventado). La regla: promedio = costo del lote.
        var r = await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjustePositivo,
            ArticuloBodegaId = filaId, Cantidad = 10m, CostoUnitario = 20m,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoTipo = TipoDocumentoInventario.Ajuste, DocumentoId = 8007
        }, "tester");

        Assert.Equal(7m, r.ExistenciaResultante);
        Assert.Equal(20m, r.CostoPromedioResultante);   // costo del lote, no 24.2857
    }

    [SkippableFact]
    public async Task EntradaSobreExistenciaNegativa_NuncaDejaPromedioNegativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Borde peor: si se ponderara, ((-3*10)+(1*5))/(-2) = 12.5 sobre existencia aún negativa.
        var (_, _, filaId) = await SeedParAsync("ZZNEG08", existencia: -3m, costoPromedio: 10m);

        var r = await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjustePositivo,
            ArticuloBodegaId = filaId, Cantidad = 1m, CostoUnitario = 5m,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoTipo = TipoDocumentoInventario.Ajuste, DocumentoId = 8008
        }, "tester");

        Assert.Equal(-2m, r.ExistenciaResultante);
        Assert.Equal(5m, r.CostoPromedioResultante);    // costo del lote, no 12.5
        Assert.True(r.CostoPromedioResultante > 0m);
    }

    // ── 5. Reversa de una salida GENÉRICA (AjusteNegativo) devuelve la mercadería ─

    [SkippableFact]
    public async Task ReversaDeSalidaGenerica_DevuelveLaExistencia_NoLaRestaDeNuevo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Hallazgo del plan §Contexto: EsReversaDeDevolucion solo reconocía Descargo/Traslado,
        // así que revertir un AjusteNegativo (documento_tipo=AJUSTE) caía en la rama de RESTA y
        // volvía a bajar la existencia (100 -> 60 -> 20) en vez de devolverla (100).
        var (_, _, filaId) = await SeedParAsync("ZZNEG09", existencia: 100m, costoPromedio: 25m);

        var salida = await _motor!.PostearAsync(AjusteNegativo(filaId, 40m, 8009), "tester");
        Assert.Equal(60m, salida.ExistenciaResultante);

        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Fecha = new DateOnly(2026, 8, 15),
            KardexIdRevertido = salida.KardexId
        }, "tester");

        Assert.Equal(100m, reversa.ExistenciaResultante);       // vuelve a 100, no baja a 20
        Assert.Equal(25m, reversa.CostoPromedioResultante);

        var asiento = await _context!.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == reversa.KardexId);
        Assert.Equal(40m, asiento.ingresos);                    // ENTRA (devolución)
        Assert.Equal(0m, asiento.salidas);
    }

    [SkippableFact]
    public async Task ReversaDeUnaSalidaQueDejoNegativo_RestituyeElParSinDistorsionar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG10", existencia: 5m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);

        // La salida deja el par en -3 (interruptor encendido).
        var salida = await _motor!.PostearAsync(Descargo(filaId, 8m, 8010), "tester");
        Assert.Equal(-3m, salida.ExistenciaResultante);

        // Revertirla devuelve la mercadería DESDE la base negativa: vuelve a 5 y el promedio no se
        // distorsiona (devolución sobre base negativa → costo devuelto, no ponderado).
        var reversa = await _motor.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = filaId,
            Fecha = new DateOnly(2026, 8, 15),
            KardexIdRevertido = salida.KardexId
        }, "tester");

        Assert.Equal(5m, reversa.ExistenciaResultante);
        Assert.Equal(10m, reversa.CostoPromedioResultante);
    }

    // ── 6. F3: cruzar a "Negativa" avisa aunque ya estuviera en alerta ──────────

    [SkippableFact]
    public async Task CaidaDeBajoMinimoANegativa_MarcaCruceAunqueYaEstabaEnAlerta()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Ya estaba BAJO MÍNIMO (existencia 5 < mínimo 10). El anti-spam normal NO avisaría otra vez.
        var (_, _, filaId) = await SeedParAsync("ZZNEG11", existencia: 5m, costoPromedio: 10m, minima: 10m);
        await SetInterruptorEmpresaAsync(true);

        var r = await _motor!.PostearAsync(AjusteNegativo(filaId, 8m, 8011), "tester");  // 5 -> -3

        Assert.True(r.CruzoAlerta);                          // la excepción F3: cruzar a negativa avisa
        Assert.Equal(StockSeveridad.Negativa, r.SeveridadAlerta);
    }

    [SkippableFact]
    public async Task CaidaDeSinStockANegativa_MarcaCruce()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Ya estaba SIN STOCK (existencia 0).
        var (_, _, filaId) = await SeedParAsync("ZZNEG12", existencia: 0m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);

        var r = await _motor!.PostearAsync(AjusteNegativo(filaId, 3m, 8012), "tester");   // 0 -> -3

        Assert.True(r.CruzoAlerta);
        Assert.Equal(StockSeveridad.Negativa, r.SeveridadAlerta);
    }

    [SkippableFact]
    public async Task CaidaDentroDeNegativo_NoReMarca_ConservaElAntiSpam()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Ya estaba en NEGATIVA (-2). Seguir cayendo dentro de negativo NO re-avisa.
        var (_, _, filaId) = await SeedParAsync("ZZNEG13", existencia: -2m, costoPromedio: 10m);
        await SetInterruptorEmpresaAsync(true);

        var r = await _motor!.PostearAsync(AjusteNegativo(filaId, 3m, 8013), "tester");   // -2 -> -5

        Assert.False(r.CruzoAlerta);
        Assert.Null(r.SeveridadAlerta);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
