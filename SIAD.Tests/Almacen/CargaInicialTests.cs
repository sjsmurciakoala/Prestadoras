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
/// Fase 4: servicio de carga inicial y ajustes de inventario.
///
/// Lo que se vigila: que el corte CLASIFIQUE bien el universo (lo que se puede postear y
/// lo que no), que el lote reconcilie sin duplicar existencia, que la reapertura sea
/// atómica y que el ajuste deje documento y asiento cuadrados.
/// </summary>
[Collection("Postgres")]
public class CargaInicialTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CargaInicialInventarioService? _carga;
    private AjusteInventarioService? _ajustes;
    private InventarioPostingService? _motor;

    public CargaInicialTests(PostgresFixture fixture) : base(fixture)
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
            _motor = new InventarioPostingService(_context, company, rollup);
            _carga = new CargaInicialInventarioService(_context, company, _motor);
            _ajustes = new AjusteInventarioService(_context, company, _motor);

            // Prueba la MECÁNICA (apertura/ajustes/kardex), no la contabilidad: se apaga la
            // integración para aislar el test del estado de los flags en la base de prueba.
            await DesactivarIntegracionContableAsync();
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    private async Task<(int articuloId, int bodegaId)> SeedParAsync(
        string codigo, decimal existencia, decimal valorUnitario,
        bool articuloActivo = true, bool ubicacionActiva = true)
    {
        var bodega = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(bodega);

        var articulo = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = $"Artículo {codigo}",
            existencia = existencia,
            valor_unitario = valorUnitario,
            activo = articuloActivo
        };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = articulo.id,
            bodega_id = bodega.id,
            existencia = existencia,
            activo = ubicacionActiva,
            principal = true
        });
        await _context.SaveChangesAsync();

        return (articulo.id, bodega.id);
    }

    private static readonly DateOnly Corte = new(2026, 7, 31);

    // ── Clasificación del universo ───────────────────────────────────────────

    [SkippableFact]
    public async Task Pendientes_ClasificaCadaCasoDelUniverso()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedParAsync("ZZCI-OK", 10m, 5m);                                  // posteable
        await SeedParAsync("ZZCI-SC", 10m, 0m);                                  // sin costo
        await SeedParAsync("ZZCI-NE", -3m, 5m);                                  // negativa
        await SeedParAsync("ZZCI-DE", 10m, 5m, articuloActivo: false);           // descontinuado
        await SeedParAsync("ZZCI-BI", 10m, 5m, ubicacionActiva: false);          // bodega inactiva

        var pendientes = (await _carga!.GetPendientesAsync())
            .Where(p => p.ArticuloCodigo.StartsWith("ZZCI-"))
            .ToDictionary(p => p.ArticuloCodigo, p => p.Clase);

        Assert.Equal(ClaseePendienteCarga.Posteable, pendientes["ZZCI-OK"]);
        Assert.Equal(ClaseePendienteCarga.SinCosto, pendientes["ZZCI-SC"]);
        Assert.Equal(ClaseePendienteCarga.Negativa, pendientes["ZZCI-NE"]);
        Assert.Equal(ClaseePendienteCarga.ArticuloDescontinuado, pendientes["ZZCI-DE"]);
        Assert.Equal(ClaseePendienteCarga.BodegaInactiva, pendientes["ZZCI-BI"]);
    }

    [SkippableFact]
    public async Task Simular_NoEscribeNada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-SIM", 20m, 3m);

        var sim = await _carga!.SimularLoteAsync();
        Assert.True(sim.TotalPares >= 1);

        // Nada posteado: el dry-run es solo lectura.
        Assert.False(await _motor!.TieneAperturaVigenteAsync(articuloId, bodegaId));
        Assert.Empty(await _context!.alm_kardexs.Where(k => k.articulo_id == articuloId).ToListAsync());
    }

    // ── Lote de reconciliación ───────────────────────────────────────────────

    [SkippableFact]
    public async Task EjecutarLote_ReconciliaSinDuplicarExistencia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-LOTE", 40m, 2.5m);

        var r = await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        Assert.True(r.Posteadas >= 1);

        // Lo esencial: la existencia NO se duplicó. El asiento describe lo que ya había.
        var fila = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);
        Assert.Equal(40m, fila.existencia);
        Assert.Equal(2.5m, fila.costo_promedio);

        var asiento = await _context.alm_kardexs.AsNoTracking()
            .FirstAsync(k => k.articulo_id == articuloId && k.documento_tipo == TipoDocumentoInventario.CargaInicial);
        Assert.Equal(Corte, asiento.fecha);
        Assert.Equal(40m, asiento.existencia_resultante);

        // La fecha de corte quedó persistida en la configuración.
        var config = await _carga.GetConfigAsync();
        Assert.Equal(Corte, config.FechaCorteApertura);
    }

    [SkippableFact]
    public async Task EjecutarLote_DosVeces_NoDuplica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-IDEM", 15m, 4m);

        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);
        var segunda = await _carga.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        // En la segunda corrida el par ya tiene apertura vigente: sale del universo.
        Assert.DoesNotContain(await _carga.GetPendientesAsync(),
            p => p.ArticuloId == articuloId && p.BodegaId == bodegaId);

        var asientos = await _context!.alm_kardexs.AsNoTracking()
            .CountAsync(k => k.articulo_id == articuloId && k.documento_tipo == TipoDocumentoInventario.CargaInicial);
        Assert.Equal(1, asientos);

        var fila = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);
        Assert.Equal(15m, fila.existencia);
    }

    [SkippableFact]
    public async Task EjecutarLote_NoPosteaLosQueTienenProblema()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (sinCostoId, bodegaSinCosto) = await SeedParAsync("ZZCI-NOSC", 10m, 0m);
        var (negativaId, bodegaNegativa) = await SeedParAsync("ZZCI-NONE", -5m, 8m);

        // Una corrida por bodega: cada par de prueba vive en la suya.
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaSinCosto);
        await _carga.EjecutarLoteAsync(Corte, 200, "tester", bodegaNegativa);

        Assert.Empty(await _context!.alm_kardexs.Where(k => k.articulo_id == sinCostoId).ToListAsync());
        Assert.Empty(await _context.alm_kardexs.Where(k => k.articulo_id == negativaId).ToListAsync());
    }

    // ── Costo manual ─────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task CostoManual_PosteaElParSinCosto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-MAN", 12m, 0m);

        var r = await _carga!.PostearConCostoManualAsync(
            new List<CargaInicialCostoManualDto> { new() { ArticuloId = articuloId, BodegaId = bodegaId, Costo = 7m } },
            Corte, "tester");

        Assert.Equal(1, r.Posteadas);

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);
        Assert.Equal(12m, fila.existencia);
        Assert.Equal(7m, fila.costo_promedio);
    }

    [SkippableFact]
    public async Task CostoManual_ConCostoCero_SeOmiteConMotivo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-MAN0", 12m, 0m);

        var r = await _carga!.PostearConCostoManualAsync(
            new List<CargaInicialCostoManualDto> { new() { ArticuloId = articuloId, BodegaId = bodegaId, Costo = 0m } },
            Corte, "tester");

        Assert.Equal(0, r.Posteadas);
        Assert.Equal(1, r.Omitidas);
        Assert.Contains(r.Detalle, d => d.Motivo.Contains("mayor que cero"));
    }

    // ── Reapertura ───────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Reabrir_CambiaElCostoYDejaUnaSolaAperturaVigente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-REAB", 10m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var r = await _carga.ReabrirAsync(articuloId, bodegaId, 9m, "costo mal capturado", "tester");

        Assert.Equal(10m, r.ExistenciaResultante);
        Assert.Equal(9m, r.CostoPromedioResultante);
        Assert.True(await _motor!.TieneAperturaVigenteAsync(articuloId, bodegaId));

        var fila = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId);
        Assert.Equal(10m, fila.existencia);
        Assert.Equal(9m, fila.costo_promedio);
    }

    [SkippableFact]
    public async Task Reabrir_SinAperturaVigente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-NOAP", 10m, 5m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carga!.ReabrirAsync(articuloId, bodegaId, 9m, "motivo", "tester"));
        Assert.Contains("no tiene una carga inicial vigente", ex.Message);
    }

    [SkippableFact]
    public async Task Reabrir_SinMotivo_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-NOMOT", 10m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carga.ReabrirAsync(articuloId, bodegaId, 9m, "   ", "tester"));
    }

    [SkippableFact]
    public async Task Reabrir_ConMovimientosPosteriores_MandaAlAjuste()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-POST", 10m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        // Un movimiento posterior a la apertura.
        await _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Entrada,
            Cantidad = 5m,
            CostoUnitario = 6m,
            Motivo = "sobrante de conteo",
            Fecha = Corte.AddDays(1)
        }, "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carga.ReabrirAsync(articuloId, bodegaId, 9m, "motivo", "tester"));
        Assert.Contains("ajuste", ex.Message.ToLowerInvariant());
    }

    // ── Cierre del corte ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Cerrar_ConParesSinCosto_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Código de 10 caracteres o menos: alm_bodega.codigo es varchar(10).
        await SeedParAsync("ZZCI-CIE", 10m, 0m); // sin costo: bloquea el cierre

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carga!.CerrarAperturaAsync("tester"));
        Assert.Contains("sin costo", ex.Message);
    }

    // ── Ajustes ──────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Ajuste_Entrada_DejaDocumentoYAsientoCuadrados()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZAJ-ENT", 10m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var creado = await _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Entrada,
            Cantidad = 10m,
            CostoUnitario = 15m,
            Motivo = "sobrante de conteo físico"
        }, "tester");

        Assert.True(creado.Posteado);
        Assert.Equal(20m, creado.ExistenciaResultante);
        // (10×5 + 10×15) / 20 = 10
        Assert.Equal(10m, creado.CostoPromedioResultante);

        var doc = await _context!.alm_ajuste_inventarios.AsNoTracking().FirstAsync(a => a.id == creado.Id);
        Assert.True(doc.posteado);

        var asiento = await _context.alm_kardexs.AsNoTracking().FirstAsync(k => k.id == creado.KardexId);
        Assert.Equal(TipoDocumentoInventario.Ajuste, asiento.documento_tipo);
        Assert.Equal(doc.id, asiento.documento_id);
        Assert.True(asiento.es_ajuste);
    }

    [SkippableFact]
    public async Task Ajuste_Salida_NoExigeCostoYUsaElPromedioVigente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZAJ-SAL", 10m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        // Sin CostoUnitario: la salida se valoriza al promedio vigente.
        var creado = await _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Salida,
            Cantidad = 4m,
            Motivo = "faltante de conteo"
        }, "tester");

        Assert.Equal(6m, creado.ExistenciaResultante);
        Assert.Equal(5m, creado.CostoPromedioResultante); // una salida no mueve el promedio
    }

    [SkippableFact]
    public async Task Ajuste_Valor_CorrigeCostoSinMoverUnidades()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZAJ-VAL", 10m, 5m);
        await _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId);

        var creado = await _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Valor,
            Cantidad = 0m,
            CostoUnitario = 8m,
            Motivo = "corrección de costo"
        }, "tester");

        Assert.Equal(10m, creado.ExistenciaResultante);
        Assert.Equal(8m, creado.CostoPromedioResultante);
    }

    [SkippableFact]
    public async Task Ajuste_SinMotivo_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZAJ-NOMOT", 10m, 5m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
            {
                ArticuloId = articuloId,
                BodegaId = bodegaId,
                Clase = ClaseAjusteInventario.Entrada,
                Cantidad = 1m,
                CostoUnitario = 1m,
                Motivo = "   "
            }, "tester"));
    }

    [SkippableFact]
    public async Task Ajuste_ValorConCantidad_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (articuloId, bodegaId) = await SeedParAsync("ZZAJ-VALC", 10m, 5m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _ajustes!.CrearYPostearAsync(new AjusteInventarioDto
            {
                ArticuloId = articuloId,
                BodegaId = bodegaId,
                Clase = ClaseAjusteInventario.Valor,
                Cantidad = 5m,
                CostoUnitario = 8m,
                Motivo = "no debería pasar"
            }, "tester"));
        Assert.Contains("cantidad debe ser 0", ex.Message);
    }

    // ── Alcance del cierre del corte (Fase 6) ────────────────────────────────

    /// <summary>
    /// Cerrar el corte prohíbe abrir pares PREEXISTENTES, no los que nacen después. Sin
    /// esto, cerrar el corte dejaría imposible dar de alta un artículo con existencia
    /// para siempre — y el alta es justo el camino que la Fase 6 conectó al motor.
    /// </summary>
    [SkippableFact]
    public async Task AperturaUnitaria_ConElCorteCerrado_SigueFuncionandoParaParesNuevos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Par nuevo: existencia 0 (es lo que exige el modo CargaInicialNueva).
        var (articuloId, bodegaId) = await SeedParAsync("ZZCI-CERR", 0m, 4m);

        await CerrarCorteAsync();

        var r = await _carga!.PostearAperturaAsync(articuloId, bodegaId, 7m, 4m, "tester");

        Assert.False(r.YaExistia);
        Assert.Equal(7m, r.ExistenciaResultante);
        Assert.Equal(4m, r.CostoPromedioResultante);
    }

    /// <summary>El lote SÍ queda bloqueado por el cierre: ese es el universo preexistente.</summary>
    [SkippableFact]
    public async Task EjecutarLote_ConElCorteCerrado_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (_, bodegaId) = await SeedParAsync("ZZCI-CER2", 12m, 3m);

        await CerrarCorteAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _carga!.EjecutarLoteAsync(Corte, 200, "tester", bodegaId));
        Assert.Contains("cerrado", ex.Message);
    }

    /// <summary>
    /// Marca el corte como cerrado saltándose el gate de CerrarAperturaAsync: lo que se
    /// prueba es el efecto del cierre, no el gate (que tiene sus propios casos).
    /// </summary>
    private async Task CerrarCorteAsync()
    {
        var config = await _context!.alm_config_inventarios.FirstOrDefaultAsync();
        if (config is null)
        {
            config = new alm_config_inventario();
            _context.alm_config_inventarios.Add(config);
        }
        config.apertura_cerrada = true;
        await _context.SaveChangesAsync();
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
