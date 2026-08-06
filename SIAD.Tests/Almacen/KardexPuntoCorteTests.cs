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
/// Fase 5: PUNTO DE CORTE del kardex.
///
/// El problema que resuelve: los ~47 mil asientos migrados de SIMAFI no arrancan de un
/// punto cero conocido. Si el saldo corrido los sumara MÁS el asiento de carga inicial,
/// nunca cuadraría contra la existencia registrada y la pantalla marcaría descuadre para
/// todo artículo con histórico — es decir, postear la apertura EMPEORARÍA la vista.
///
/// La regla: el saldo arranca en cero en la carga inicial del par; lo anterior es
/// histórico informativo (Saldo = null, EsPreCorte = true). Un par SIN carga inicial se
/// comporta como antes: es lo que mantiene usable la pantalla durante la transición.
/// </summary>
[Collection("Postgres")]
public class KardexPuntoCorteTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private KardexService? _kardex;
    private CargaInicialInventarioService? _carga;
    private AjusteInventarioService? _ajustes;

    public KardexPuntoCorteTests(PostgresFixture fixture) : base(fixture)
    {
    }

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
            _kardex = new KardexService(_context, company);
            _carga = new CargaInicialInventarioService(_context, company, motor);
            _ajustes = new AjusteInventarioService(_context, company, motor);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private static readonly DateOnly Corte = new(2026, 7, 31);

    private async Task<(int articuloId, int bodegaId)> SeedParAsync(string codigo, decimal existencia, decimal costo)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);

        var articulo = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = $"Artículo {codigo}",
            existencia = existencia,
            valor_unitario = costo,
            activo = true
        };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articulo.id,
            bodega_id = bodega.id,
            existencia = existencia,
            activo = true,
            principal = true
        });
        await _context.SaveChangesAsync();

        return (articulo.id, bodega.id);
    }

    /// <summary>Asiento del histórico SIMAFI: sin uuid ni documento_tipo, como los migrados.</summary>
    private async Task SeedHistoricoAsync(int articuloId, int bodegaId, DateOnly fecha, decimal ingresos, decimal salidas)
    {
        _context!.alm_kardexs.Add(new alm_kardex
        {
            articulo_id = articuloId,
            bodega_id = bodegaId,
            fecha = fecha,
            tipo_transaccion = ingresos > 0 ? "102" : "202",
            ingresos = ingresos,
            salidas = salidas
        });
        await _context.SaveChangesAsync();
    }

    // ── La regla central ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ConCargaInicial_ElSaldoArrancaEnElCorte_YCuadra()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZPC-01", 100m, 5m);

        // Histórico SIMAFI: dos asientos viejos que NO deben contar para el saldo.
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 1, 10), 500m, 0m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 6, 20), 0m, 120m);

        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var k = await _kardex!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = articuloId, BodegaId = bodegaId });

        Assert.NotNull(k);
        // El saldo NO es 500-120+100 = 480: arranca en el corte.
        Assert.Equal(100m, k!.SaldoCalculado);
        Assert.Equal(100m, k.ExistenciaBodega);
        Assert.False(k.SaldoDescuadrado);

        var movs = k.Movimientos.OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToList();
        Assert.Equal(3, movs.Count);

        // Los dos históricos quedan pre-corte, sin saldo.
        Assert.True(movs[0].EsPreCorte);
        Assert.Null(movs[0].Saldo);
        Assert.True(movs[1].EsPreCorte);
        Assert.Null(movs[1].Saldo);

        // El tercero es la línea de corte y abre el saldo.
        Assert.True(movs[2].EsLineaDeCorte);
        Assert.False(movs[2].EsPreCorte);
        Assert.Equal(100m, movs[2].Saldo);
        Assert.Equal(TipoDocumentoInventario.CargaInicial, movs[2].DocumentoTipo);
        Assert.Equal(100m, movs[2].ExistenciaResultante);
        Assert.Equal(5m, movs[2].CostoPromedioResultante);
    }

    [SkippableFact]
    public async Task SinCargaInicial_ElSaldoSigueSiendoElHistoricoCompleto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Compatibilidad hacia atrás: sin apertura, el comportamiento no cambia.
        var (articuloId, bodegaId) = await SeedParAsync("ZZPC-02", 0m, 5m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 1, 10), 80m, 0m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 3, 10), 0m, 30m);

        var k = await _kardex!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = articuloId, BodegaId = bodegaId });

        Assert.NotNull(k);
        Assert.Equal(50m, k!.SaldoCalculado);

        var movs = k.Movimientos.OrderBy(m => m.Id).ToList();
        Assert.All(movs, m => Assert.False(m.EsPreCorte));
        Assert.Equal(80m, movs[0].Saldo);
        Assert.Equal(50m, movs[1].Saldo);
    }

    [SkippableFact]
    public async Task MovimientosPosterioresAlCorte_SumanAlSaldo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZPC-03", 40m, 2m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 5, 5), 999m, 0m); // ruido pre-corte
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        await _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Entrada,
            Cantidad = 10m,
            CostoUnitario = 2m,
            Motivo = "sobrante",
            Fecha = Corte.AddDays(1)
        }, "tester");

        var k = await _kardex!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = articuloId, BodegaId = bodegaId });

        Assert.NotNull(k);
        // 40 (apertura) + 10 (ajuste) = 50. El 999 pre-corte no cuenta.
        Assert.Equal(50m, k!.SaldoCalculado);
        Assert.Equal(50m, k.ExistenciaBodega);
        Assert.False(k.SaldoDescuadrado);
    }

    [SkippableFact]
    public async Task ParReabierto_ElCorteEsLaAperturaVigente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZPC-04", 20m, 3m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        // Reabrir: la apertura vieja y su reversa quedan del lado pre-corte.
        await _carga.ReabrirAsync(articuloId, bodegaId, 11m, "costo mal capturado", "tester");

        var k = await _kardex!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = articuloId, BodegaId = bodegaId });

        Assert.NotNull(k);
        // No se cuenta dos veces: el saldo es el de la apertura VIGENTE.
        Assert.Equal(20m, k!.SaldoCalculado);
        Assert.Equal(20m, k.ExistenciaBodega);
        Assert.False(k.SaldoDescuadrado);

        // Exactamente una línea de corte.
        Assert.Single(k.Movimientos.Where(m => m.EsLineaDeCorte));
    }

    [SkippableFact]
    public async Task LaProyeccion_TraeLaTrazabilidadDelLibroNuevo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZPC-05", 12m, 4m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 2, 2), 7m, 0m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var k = await _kardex!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = articuloId, BodegaId = bodegaId });
        var movs = k!.Movimientos.OrderBy(m => m.Id).ToList();

        // El histórico migrado no tiene documento: es lo que lo distingue en pantalla.
        var historico = movs.First(m => m.EsPreCorte);
        Assert.Null(historico.DocumentoTipo);
        Assert.Null(historico.ExistenciaResultante);

        var apertura = movs.First(m => m.EsLineaDeCorte);
        Assert.Equal(TipoDocumentoInventario.CargaInicial, apertura.DocumentoTipo);
        Assert.NotNull(apertura.DocumentoId);
        Assert.Equal(12m, apertura.ExistenciaResultante);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
