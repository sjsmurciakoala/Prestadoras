using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Backfill del histórico migrado (Database/2026-08-18_alm_kardex_backfill_resultantes_historico.sql):
/// rellena <c>alm_kardex.existencia_resultante</c> y <c>costo_promedio_resultante</c> (hoy NULL en los
/// asientos con <c>uuid</c> NULL) con el saldo y el costo promedio CORRIDO por par (artículo, bodega).
///
/// Se prueba que:
///  (1) el resultado del UPDATE por window functions coincide con lo que deriva <see cref="KardexService"/>
///      (el verificador vivo) para un par SIN carga inicial;
///  (2) NO toca los snapshots que ya persistió el motor (asientos con uuid);
///  (3) un par que termina en saldo ≤ 0 queda con costo NULL.
///
/// El core del script se ejecuta con el trigger de inmutabilidad desactivado dentro de la transacción
/// del test; <c>DISABLE TRIGGER</c> es DDL transaccional en Postgres y revierte con el ROLLBACK de
/// <see cref="IntegrationTestBase"/>, así que la base de prueba queda intacta.
/// </summary>
[Collection("Postgres")]
public class KardexBackfillHistoricoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private KardexService? _kardex;
    private CargaInicialInventarioService? _carga;

    public KardexBackfillHistoricoTests(PostgresFixture fixture) : base(fixture)
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

            await DesactivarIntegracionContableAsync();
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // El UPDATE es EL MISMO del script Database/2026-08-18_alm_kardex_backfill_resultantes_historico.sql
    // (sin BEGIN/COMMIT ni locks: en el test los da la transacción de aislamiento). Mantener sincronizado.
    private const string BackfillUpdateSql = @"
UPDATE alm_kardex k
SET existencia_resultante     = c.saldo_r,
    costo_promedio_resultante = c.costo_r
FROM (
    SELECT id,
           ROUND(saldo_acum, 2) AS saldo_r,
           CASE WHEN saldo_acum > 0 THEN ROUND(valor_acum / saldo_acum, 4) ELSE NULL END AS costo_r
    FROM (
        SELECT id,
               SUM(ingresos - salidas) OVER w AS saldo_acum,
               SUM((ingresos - salidas) * valor_unitario) OVER w AS valor_acum
        FROM alm_kardex
        WHERE uuid IS NULL AND articulo_id IS NOT NULL
        WINDOW w AS (PARTITION BY company_id, articulo_id, COALESCE(bodega_id, 0)
                     ORDER BY fecha, id ROWS UNBOUNDED PRECEDING)
    ) acum
) c
WHERE k.id = c.id AND k.uuid IS NULL AND k.existencia_resultante IS NULL;";

    private async Task EjecutarBackfillAsync()
    {
        // Escotilla de inmutabilidad, igual que el script. Se ejecutan por separado para que un
        // fallo del UPDATE deje el ROLLBACK del test a cargo de revertir el DISABLE.
        await _context!.Database.ExecuteSqlRawAsync("ALTER TABLE alm_kardex DISABLE TRIGGER trg_alm_kardex_inmutable;");
        await _context.Database.ExecuteSqlRawAsync(BackfillUpdateSql);
        await _context.Database.ExecuteSqlRawAsync("ALTER TABLE alm_kardex ENABLE TRIGGER trg_alm_kardex_inmutable;");
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

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

    /// <summary>Asiento del histórico migrado: sin uuid ni documento_tipo, con las resultantes en NULL.</summary>
    private async Task SeedHistoricoAsync(int articuloId, int bodegaId, DateOnly fecha,
        decimal ingresos, decimal salidas, decimal valorUnitario)
    {
        _context!.alm_kardexs.Add(new alm_kardex
        {
            articulo_id = articuloId,
            bodega_id = bodegaId,
            fecha = fecha,
            tipo_transaccion = ingresos > 0 ? "102" : "202",
            ingresos = ingresos,
            salidas = salidas,
            valor_unitario = valorUnitario,
            total = (ingresos + salidas) * valorUnitario
        });
        await _context.SaveChangesAsync();
    }

    // ── (1) El backfill coincide con la derivación de KardexService ───────────

    [SkippableFact]
    public async Task Backfill_RellenaElCorrido_YCoincideConKardexService()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Par SÓLO histórico (sin carga inicial): el corrido corre desde el primer movimiento,
        // sin punto de corte, así que la columna backfill y el corrido de KardexService coinciden.
        // Costos que no dividen exacto: donde se vería una fuga de centavos si se redondeara antes de tiempo.
        var (articuloId, bodegaId) = await SeedParAsync("ZZBF-01", 0m, 0m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2020, 1, 5), 7m, 0m, 3.3333m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2020, 2, 10), 11m, 0m, 7.7777m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2020, 3, 15), 0m, 9m, 5.0000m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2020, 4, 20), 13m, 0m, 1.1111m);

        await EjecutarBackfillAsync();

        // Lo que quedó grabado por el backfill (leído fresco de la base, fuera del change tracker).
        var grabados = await _context!.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId && k.bodega_id == bodegaId)
            .OrderBy(k => k.fecha).ThenBy(k => k.id)
            .Select(k => new { k.id, k.existencia_resultante, k.costo_promedio_resultante })
            .ToListAsync();
        Assert.Equal(4, grabados.Count);

        // Lo que deriva KardexService (verificador vivo).
        var k = await _kardex!.GetByArticuloAsync(new KardexFilterDto { ArticuloId = articuloId, BodegaId = bodegaId });
        Assert.NotNull(k);
        var derivado = k!.Movimientos.ToDictionary(m => m.Id, m => m);

        foreach (var g in grabados)
        {
            var d = derivado[g.id];
            // Saldo: exacto (ambos suman NUMERIC(15,2)).
            Assert.Equal(d.Saldo, g.existencia_resultante);
            // Costo: tolerancia de un centavo (el motor y el corrido redondean en momentos distintos).
            if (d.CostoPromedioCorrido is null)
            {
                Assert.Null(g.costo_promedio_resultante);
            }
            else
            {
                Assert.NotNull(g.costo_promedio_resultante);
                Assert.True(Math.Abs(g.costo_promedio_resultante!.Value - d.CostoPromedioCorrido!.Value) <= 0.01m,
                    $"Asiento {g.id}: backfill {g.costo_promedio_resultante} vs corrido {d.CostoPromedioCorrido}");
            }
        }
    }

    // ── (2) No toca los snapshots del motor; sólo rellena el histórico ────────

    [SkippableFact]
    public async Task Backfill_NoTocaLosSnapshotsDelMotor_YRellenaSoloElHistorico()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZBF-02", 60m, 4m);
        // Histórico migrado (uuid NULL): 100 @ 4 y luego salida de 40 @ 4 → saldo 60.
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2019, 5, 1), 100m, 0m, 4m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2019, 6, 1), 0m, 40m, 4m);
        // Carga inicial del motor (uuid NOT NULL, con snapshot persistido por el motor).
        await _carga!.EjecutarLoteAsync(new DateOnly(2026, 7, 31), 200, "tester", bodegaId);

        var motorAntes = await _context!.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId && k.uuid != null)
            .Select(k => new { k.id, k.existencia_resultante, k.costo_promedio_resultante })
            .ToListAsync();
        Assert.NotEmpty(motorAntes);

        await EjecutarBackfillAsync();

        // El motor NO cambió.
        var motorDespues = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId && k.uuid != null)
            .Select(k => new { k.id, k.existencia_resultante, k.costo_promedio_resultante })
            .ToListAsync();
        Assert.Equal(motorAntes.Count, motorDespues.Count);
        foreach (var m in motorDespues)
        {
            var antes = motorAntes.First(x => x.id == m.id);
            Assert.Equal(antes.existencia_resultante, m.existencia_resultante);
            Assert.Equal(antes.costo_promedio_resultante, m.costo_promedio_resultante);
        }

        // El histórico (uuid NULL) SÍ se rellenó, con su saldo/costo corrido.
        var historico = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId && k.uuid == null)
            .OrderBy(k => k.fecha).ThenBy(k => k.id)
            .Select(k => new { k.existencia_resultante, k.costo_promedio_resultante })
            .ToListAsync();
        Assert.Equal(2, historico.Count);
        Assert.Equal(100m, historico[0].existencia_resultante);
        Assert.Equal(4m, historico[0].costo_promedio_resultante);
        Assert.Equal(60m, historico[1].existencia_resultante);
        Assert.Equal(4m, historico[1].costo_promedio_resultante);
    }

    // ── (3) Saldo ≤ 0 → costo NULL ───────────────────────────────────────────

    [SkippableFact]
    public async Task Backfill_SaldoEnCero_DejaElCostoEnNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZBF-03", 0m, 0m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2021, 1, 1), 10m, 0m, 5m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2021, 2, 1), 0m, 10m, 5m); // saldo baja a 0

        await EjecutarBackfillAsync();

        var grabados = await _context!.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId && k.bodega_id == bodegaId)
            .OrderBy(k => k.fecha).ThenBy(k => k.id)
            .Select(k => new { k.existencia_resultante, k.costo_promedio_resultante })
            .ToListAsync();
        Assert.Equal(2, grabados.Count);

        Assert.Equal(10m, grabados[0].existencia_resultante);
        Assert.Equal(5m, grabados[0].costo_promedio_resultante);
        // El saldo llegó a cero: sin costo (la UI pinta "—").
        Assert.Equal(0m, grabados[1].existencia_resultante);
        Assert.Null(grabados[1].costo_promedio_resultante);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
