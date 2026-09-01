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
/// Motor de posteo — EXISTENCIA NEGATIVA en salidas. Regla FIRME (2026-08-20): ninguna salida
/// puede dejar la existencia en negativo. Se eliminó por completo el interruptor de existencia
/// negativa (empresa <c>cfg_inventario_negativo</c> + override por bodega
/// <c>alm_bodega.permite_existencia_negativa</c> + confirmación en pantalla del descargo).
/// <para>
/// Lo que se vigila: (1) las TRES ramas de salida del motor (SalidaDescargo, AjusteNegativo,
/// TrasladoSalida) rechazan siempre la salida que cruzaría a negativo, y una que llega justo a
/// cero se permite; (2) el costeo defensivo sigue vivo para los negativos que YA existen en los
/// datos históricos: una entrada sobre existencia negativa NO pondera contra la base negativa (el
/// promedio queda en el costo del lote, nunca distorsionado ni negativo); (3) revertir una salida
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

    // ── 1. Toda salida que cruzaría a negativo se BLOQUEA (las 3 ramas) ──────────

    [SkippableFact]
    public async Task Descargo_QueDejariaNegativo_Bloquea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG01", existencia: 5m, costoPromedio: 10m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(Descargo(filaId, 8m, 8001), "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(5m, (await ParAsync(filaId)).existencia);   // nada se movió
    }

    [SkippableFact]
    public async Task AjusteNegativo_QueDejariaNegativo_Bloquea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG02", existencia: 5m, costoPromedio: 10m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(AjusteNegativo(filaId, 8m, 8002), "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(5m, (await ParAsync(filaId)).existencia);
    }

    [SkippableFact]
    public async Task TrasladoSalida_QueDejariaNegativo_Bloquea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG03", existencia: 5m, costoPromedio: 10m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _motor!.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.TrasladoSalida,
                ArticuloBodegaId = filaId, Cantidad = 8m,
                Fecha = new DateOnly(2026, 8, 15),
                DocumentoId = 8003
            }, "tester"));
        Assert.Contains("negativo", ex.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(5m, (await ParAsync(filaId)).existencia);
    }

    // ── 2. Una salida que llega JUSTO a cero se permite (el corte es en < 0) ─────

    [SkippableFact]
    public async Task Descargo_QueDejaExactamenteCero_SePermite()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, _, filaId) = await SeedParAsync("ZZNEG04", existencia: 5m, costoPromedio: 10m);

        var r = await _motor!.PostearAsync(Descargo(filaId, 5m, 8004), "tester");

        Assert.Equal(0m, r.ExistenciaResultante);
        Assert.Equal(10m, r.CostoPromedioResultante);   // una salida no re-pondera
        Assert.Equal(0m, (await ParAsync(filaId)).existencia);
    }

    // ── 3. Costeo defensivo: una entrada sobre existencia NEGATIVA (histórica) ───
    //     no pondera contra la base negativa; el promedio queda en el costo del lote.

    [SkippableFact]
    public async Task EntradaSobreExistenciaNegativa_PromedioEsElCostoDelLote()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Par ya en negativo por dato histórico (el motor ya no crea negativos, pero existen en la
        // base migrada). Base negativa: -3 @ 10.
        var (_, _, filaId) = await SeedParAsync("ZZNEG05", existencia: -3m, costoPromedio: 10m);

        // Entra un ajuste positivo de 10 @ 20 -> existencia 7. El ponderado clásico daría
        // ((-3*10)+(10*20))/7 = 24.2857… (un costo inventado). La regla: promedio = costo del lote.
        var r = await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjustePositivo,
            ArticuloBodegaId = filaId, Cantidad = 10m, CostoUnitario = 20m,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoTipo = TipoDocumentoInventario.Ajuste, DocumentoId = 8005
        }, "tester");

        Assert.Equal(7m, r.ExistenciaResultante);
        Assert.Equal(20m, r.CostoPromedioResultante);   // costo del lote, no 24.2857
    }

    [SkippableFact]
    public async Task EntradaSobreExistenciaNegativa_NuncaDejaPromedioNegativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Borde peor: si se ponderara, ((-3*10)+(1*5))/(-2) = 12.5 sobre existencia aún negativa.
        var (_, _, filaId) = await SeedParAsync("ZZNEG06", existencia: -3m, costoPromedio: 10m);

        var r = await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjustePositivo,
            ArticuloBodegaId = filaId, Cantidad = 1m, CostoUnitario = 5m,
            Fecha = new DateOnly(2026, 8, 15),
            DocumentoTipo = TipoDocumentoInventario.Ajuste, DocumentoId = 8006
        }, "tester");

        Assert.Equal(-2m, r.ExistenciaResultante);
        Assert.Equal(5m, r.CostoPromedioResultante);    // costo del lote, no 12.5
        Assert.True(r.CostoPromedioResultante > 0m);
    }

    // ── 4. Reversa de una salida GENÉRICA (AjusteNegativo) devuelve la mercadería ─

    [SkippableFact]
    public async Task ReversaDeSalidaGenerica_DevuelveLaExistencia_NoLaRestaDeNuevo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Hallazgo del plan §Contexto: EsReversaDeDevolucion solo reconocía Descargo/Traslado,
        // así que revertir un AjusteNegativo (documento_tipo=AJUSTE) caía en la rama de RESTA y
        // volvía a bajar la existencia (100 -> 60 -> 20) en vez de devolverla (100).
        var (_, _, filaId) = await SeedParAsync("ZZNEG07", existencia: 100m, costoPromedio: 25m);

        var salida = await _motor!.PostearAsync(AjusteNegativo(filaId, 40m, 8007), "tester");
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

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
