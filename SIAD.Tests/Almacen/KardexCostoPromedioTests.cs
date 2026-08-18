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
/// Costo promedio CORRIDO del kardex: valor acumulado ÷ cantidad acumulada, por par
/// (artículo, bodega), la regla del kardex legacy.
///
/// Por qué se deriva en vez de leerse: <c>alm_kardex.costo_promedio_resultante</c> sólo
/// existe en los asientos que posteó el motor y viene NULL en el histórico migrado, así que
/// una columna que lo lea sale vacía en la mayor parte del libro. Derivarlo de cantidad y
/// costo da una columna continua y, de paso, un verificador del costo almacenado en
/// <c>alm_articulo_bodega</c>.
///
/// La invariante que se prueba aquí: una ENTRADA pondera el promedio; una SALIDA sale al
/// promedio vigente y por lo tanto NO lo mueve.
/// </summary>
[Collection("Postgres")]
public class KardexCostoPromedioTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private KardexService? _kardex;
    private CargaInicialInventarioService? _carga;
    private AjusteInventarioService? _ajustes;

    public KardexCostoPromedioTests(PostgresFixture fixture) : base(fixture)
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

            // Se prueba el costeo, no la contabilidad: la integración se apaga para aislar
            // el test del estado de los flags en la base de prueba.
            await DesactivarIntegracionContableAsync();
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private static readonly DateOnly Corte = new(2026, 7, 31);

    /// <summary>
    /// Par con existencia previa. El costo de apertura sale de <c>alm_articulo.valor_unitario</c>
    /// (base por defecto), y como el par YA tiene existencia la apertura se postea como
    /// reconciliación: escribe ingresos = existencia sin mover la existencia de la fila.
    /// </summary>
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

    /// <summary>Asiento del histórico migrado: sin uuid ni documento_tipo, con costo propio.</summary>
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

    private Task<KardexArticuloDto?> ConsultarAsync(int articuloId, int bodegaId, DateOnly? desde = null)
        => _kardex!.GetByArticuloAsync(new KardexFilterDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            FechaDesde = desde
        });

    private Task EntradaAsync(int articuloId, int bodegaId, decimal cantidad, decimal costo, DateOnly fecha)
        => _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Entrada,
            Cantidad = cantidad,
            CostoUnitario = costo,
            Motivo = "entrada de prueba",
            Fecha = fecha
        }, "tester");

    private Task SalidaAsync(int articuloId, int bodegaId, decimal cantidad, DateOnly fecha)
        => _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Salida,
            Cantidad = cantidad,
            Motivo = "salida de prueba",
            Fecha = fecha
        }, "tester");

    private Task RevaluarAsync(int articuloId, int bodegaId, decimal costo, DateOnly fecha)
        => _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Valor,
            Cantidad = 0m,
            CostoUnitario = costo,
            Motivo = "revaluación de prueba",
            Fecha = fecha
        }, "tester");

    // ── La regla central ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task LaEntradaPondera_YLaSalidaNoMueveElPromedio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // 100 @ 10 (apertura por reconciliación) + 100 @ 20 = 200 @ 15.
        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-01", 100m, 10m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        await EntradaAsync(articuloId, bodegaId, 100m, 20m, Corte.AddDays(1));
        // La salida se valoriza al promedio vigente: retira 50 × 15 y el promedio queda igual.
        await SalidaAsync(articuloId, bodegaId, 50m, Corte.AddDays(2));

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        Assert.Equal(150m, k!.SaldoCalculado);
        Assert.Equal(2250m, k.SaldoValorizado);
        Assert.Equal(15m, k.CostoPromedioActual);

        var movs = k.Movimientos.OrderBy(m => m.Id).ToList();
        Assert.Equal(3, movs.Count);

        // Apertura: 100 @ 10.
        Assert.Equal(100m, movs[0].Saldo);
        Assert.Equal(1000m, movs[0].SaldoValorizado);
        Assert.Equal(10m, movs[0].CostoPromedioCorrido);
        Assert.Equal(0m, movs[0].ExistenciaAnterior);
        Assert.Null(movs[0].CostoPromedioAnterior);   // no había saldo previo

        // Entrada: pondera 10 → 15.
        Assert.Equal(200m, movs[1].Saldo);
        Assert.Equal(3000m, movs[1].SaldoValorizado);
        Assert.Equal(15m, movs[1].CostoPromedioCorrido);
        Assert.Equal(10m, movs[1].CostoPromedioAnterior);
        Assert.Equal(2000m, movs[1].ValorMovimiento);

        // Salida: el promedio NO se mueve.
        Assert.Equal(150m, movs[2].Saldo);
        Assert.Equal(2250m, movs[2].SaldoValorizado);
        Assert.Equal(15m, movs[2].CostoPromedioCorrido);
        Assert.Equal(15m, movs[2].CostoPromedioAnterior);
        Assert.Equal(-750m, movs[2].ValorMovimiento);

        // El libro cuadra con la ficha de existencias.
        Assert.False(k.CostoDescuadrado);
    }

    [SkippableFact]
    public async Task ElCorridoCoincideConElSnapshotDelMotor()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Costos que no dividen exacto: es donde se vería una fuga de centavos si el corrido
        // reconstruyera el numerador desde un promedio ya redondeado en vez de arrastrar el valor.
        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-02", 7m, 3.3333m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        await EntradaAsync(articuloId, bodegaId, 11m, 7.7777m, Corte.AddDays(1));
        await EntradaAsync(articuloId, bodegaId, 13m, 1.1111m, Corte.AddDays(2));
        await SalidaAsync(articuloId, bodegaId, 9m, Corte.AddDays(3));

        var k = await ConsultarAsync(articuloId, bodegaId);
        Assert.NotNull(k);

        var conSnapshot = k!.Movimientos.Where(m => m.CostoPromedioResultante.HasValue).ToList();
        Assert.NotEmpty(conSnapshot);

        foreach (var m in conSnapshot)
        {
            Assert.NotNull(m.CostoPromedioCorrido);
            // Tolerancia de un centavo: el motor redondea el promedio a 4 decimales en cada
            // paso y el corrido arrastra el valor sin redondear.
            Assert.True(Math.Abs(m.CostoPromedioCorrido!.Value - m.CostoPromedioResultante!.Value) <= 0.01m,
                $"Asiento {m.Id}: corrido {m.CostoPromedioCorrido} vs snapshot {m.CostoPromedioResultante}");
            Assert.Equal(m.ExistenciaResultante, m.Saldo);
        }

        Assert.False(k.CostoDescuadrado);
    }

    // ── Bordes ───────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SinSaldo_NoHayCostoPromedio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El kardex legacy dividía sin guarda y reventaba aquí; el corrido devuelve null.
        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-03", 10m, 4m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        await SalidaAsync(articuloId, bodegaId, 10m, Corte.AddDays(1));

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        Assert.Equal(0m, k!.SaldoCalculado);
        Assert.Equal(0m, k.SaldoValorizado);
        Assert.Null(k.CostoPromedioActual);

        var ultimo = k.Movimientos.OrderBy(m => m.Id).Last();
        Assert.Equal(0m, ultimo.Saldo);
        Assert.Null(ultimo.CostoPromedioCorrido);
        // El saldo llegó a cero limpio: la salida retiró exactamente lo que valía.
        Assert.Equal(0m, ultimo.SaldoValorizado);
        // Sin costo del libro no se afirma descuadre, aunque la ficha conserve el costo.
        Assert.False(k.CostoDescuadrado);
    }

    [SkippableFact]
    public async Task PreCorte_NoTieneCostoNiValorizacion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El histórico migrado no arranca de un valor conocido: valorizarlo sería inventar.
        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-04", 40m, 6m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 2, 3), 500m, 0m, 2m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 8, 9), 0m, 60m, 2m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var k = await ConsultarAsync(articuloId, bodegaId);
        Assert.NotNull(k);

        var movs = k!.Movimientos.OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToList();
        Assert.Equal(3, movs.Count);

        foreach (var m in movs.Take(2))
        {
            Assert.True(m.EsPreCorte);
            Assert.Null(m.Saldo);
            Assert.Null(m.SaldoValorizado);
            Assert.Null(m.CostoPromedioCorrido);
            Assert.Null(m.CostoPromedioAnterior);
            Assert.Null(m.ExistenciaAnterior);
        }

        // El corte abre la valorización: los 500 y los 60 de antes no entran.
        Assert.True(movs[2].EsLineaDeCorte);
        Assert.Equal(40m, movs[2].Saldo);
        Assert.Equal(240m, movs[2].SaldoValorizado);
        Assert.Equal(6m, movs[2].CostoPromedioCorrido);
        Assert.Equal(240m, k.SaldoValorizado);
    }

    [SkippableFact]
    public async Task ElAjusteDeCosto_RevalorizaSinMoverUnidades()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Es el único asiento del motor con entradas Y salidas en cero. Si el corrido lo
        // ignorara, el costo del libro se quedaría en 8 mientras la ficha pasa a 12, y el
        // artículo quedaría marcado como descuadrado para siempre.
        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-09", 25m, 8m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        await RevaluarAsync(articuloId, bodegaId, 12m, Corte.AddDays(1));

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        Assert.Equal(25m, k!.SaldoCalculado);      // las unidades no se mueven
        Assert.Equal(300m, k.SaldoValorizado);     // 25 × 12
        Assert.Equal(12m, k.CostoPromedioActual);
        Assert.False(k.CostoDescuadrado);

        var revaluacion = k.Movimientos.OrderBy(m => m.Id).Last();
        Assert.Equal(8m, revaluacion.CostoPromedioAnterior);
        Assert.Equal(12m, revaluacion.CostoPromedioCorrido);
        Assert.Equal(25m, revaluacion.Saldo);
    }

    // ── Arrastre del período ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task ConFechaDesde_ElArrastreEsElAcumuladoPrevio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-05", 100m, 10m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        await EntradaAsync(articuloId, bodegaId, 100m, 20m, Corte.AddDays(5));

        var k = await ConsultarAsync(articuloId, bodegaId, desde: Corte.AddDays(1));

        Assert.NotNull(k);
        Assert.True(k!.TieneArrastre);
        Assert.Equal(100m, k.CantidadAnterior);
        Assert.Equal(1000m, k.ValorAnterior);
        Assert.Equal(10m, k.CostoPromedioAnterior);

        // El filtro de fecha recorta la presentación, no el acumulado.
        Assert.Single(k.Movimientos);
        Assert.Equal(200m, k.SaldoCalculado);
        Assert.Equal(3000m, k.SaldoValorizado);
        Assert.Equal(15m, k.CostoPromedioActual);
    }

    [SkippableFact]
    public async Task SinFechaDesde_NoHayArrastreQueMostrar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-06", 20m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        Assert.False(k!.TieneArrastre);
        Assert.Null(k.CantidadAnterior);
        Assert.Null(k.ValorAnterior);
        Assert.Null(k.CostoPromedioAnterior);
    }

    // ── Verificación contra la ficha de existencias ──────────────────────────

    [SkippableFact]
    public async Task CostoAlmacenadoDesfasado_SeMarcaDescuadre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-07", 50m, 8m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        // Se corrompe la caché a mano (lo que haría un UPDATE directo contra la base). El
        // kardex es inmutable, así que el libro sigue diciendo la verdad y la diferencia
        // aparece: ésa es la razón de derivar el costo en vez de leerlo.
        var fila = await _context!.alm_articulo_bodegas
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);
        fila.costo_promedio = 99m;
        await _context.SaveChangesAsync();

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        Assert.Equal(8m, k!.CostoPromedioActual);
        Assert.Equal(99m, k.CostoPromedioCache);
        Assert.True(k.CostoDescuadrado);
    }

    [SkippableFact]
    public async Task FichaSinCosto_EsPendienteDeCosteo_NoDescuadre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Mientras el corte de inventario no se ejecute hay cientos de pares con existencia y
        // costo cero. El libro sí sabe valorizarlos, así que la pantalla debe decir "falta
        // costear", no "el costo no cuadra": son acciones distintas y marcarlos a todos como
        // descuadre volvería inútil el aviso.
        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-10", 15m, 0m);
        await SeedHistoricoAsync(articuloId, bodegaId, new DateOnly(2025, 4, 1), 15m, 0m, 7m);

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        // Sin carga inicial el saldo corre desde el primer movimiento (compatibilidad).
        Assert.Equal(7m, k!.CostoPromedioActual);
        Assert.Equal(0m, k.CostoPromedioCache);
        Assert.False(k.CostoDescuadrado);
        Assert.True(k.CostoSinRegistrar);
    }

    [SkippableFact]
    public async Task DiferenciaDeCentavos_NoEsDescuadre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCP-08", 30m, 12m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        // Dentro de la tolerancia: el motor redondea a 4 decimales y el corrido no, así que
        // una diferencia de centavos es esperable y no debe alarmar.
        var fila = await _context!.alm_articulo_bodegas
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);
        fila.costo_promedio = 12.005m;
        await _context.SaveChangesAsync();

        var k = await ConsultarAsync(articuloId, bodegaId);

        Assert.NotNull(k);
        Assert.False(k!.CostoDescuadrado);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
