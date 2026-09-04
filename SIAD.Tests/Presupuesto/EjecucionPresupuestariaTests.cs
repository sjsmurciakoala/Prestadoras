using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Services.Aprobaciones;
using SIAD.Services.Almacen;
using SIAD.Services.Presupuesto;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// Consultas y configuración del control presupuestario (fase F5): ejecución por partida,
/// compromisos con saldo pendiente, kardex y el interruptor.
/// <para>
/// El kardex es la razón de ser de esta fase: antes, <c>valor_real</c> era un número acumulado sin
/// historia y nadie podía responder «¿por qué esta cuenta está al 90%?».
/// </para>
/// </summary>
[Collection("Postgres")]
public class EjecucionPresupuestariaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private OrdenCompraService? _ordenes;
    private PresupuestoEjecucionService? _ejecucion;

    private string _codProveedor = string.Empty;
    private int _articulo;

    private const string Cuenta = "TEST-PST-F5";
    private const string Presupuesto = "TST-F5";   // id_presupuesto es VARCHAR(10)

    public EjecucionPresupuestariaTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);

        var empresa = new TestCurrentCompanyService(CompanyId);
        _ordenes = new OrdenCompraService(
            _context, empresa, new TasaIsvArticuloResolver(_context),
            new PresupuestoCompromisoService(_context, empresa),
            new AprobacionService(_context, empresa, new TestCurrentUserService()),
            new AprobacionNotificadorNoop());
        _ejecucion = new PresupuestoEjecucionService(_context, empresa);

        await DesactivarIntegracionContableAsync();

        _codProveedor = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && (p.status == null || p.status == true))
            .OrderBy(p => p.cod_proveedor).Select(p => p.cod_proveedor).FirstAsync();

        _articulo = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.activo).OrderBy(a => a.id).Select(a => a.id).FirstAsync();

        await SembrarAsync();
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Ejecución por partida ────────────────────────────────────────────────

    [SkippableFact]
    public async Task Ejecucion_MuestraLosCuatroMontosYElDisponible()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await AprobarOrdenAsync(300m);

        var filas = await _ejecucion!.ListarEjecucionAsync(
            new PresupuestoEjecucionFilterDto { IdPresupuesto = Presupuesto });

        var partida = Assert.Single(filas, f => f.ConCuentaCode == Cuenta);
        Assert.Equal(1000m, partida.Presupuesto);
        Assert.Equal(300m, partida.Comprometido);
        Assert.Equal(0m, partida.Ejecutado);
        Assert.Equal(700m, partida.Disponible);
        Assert.True(partida.CuentaPresupuestable);

        // 300 de 1000 comprometidos = 30% utilizado.
        Assert.Equal(30m, partida.PctUtilizado);
    }

    [SkippableFact]
    public async Task Ejecucion_FiltraSoloLasCuentasQueElControlMira()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await MarcarPresupuestableAsync(false);

        var todas = await _ejecucion!.ListarEjecucionAsync(
            new PresupuestoEjecucionFilterDto { IdPresupuesto = Presupuesto });
        var controladas = await _ejecucion.ListarEjecucionAsync(
            new PresupuestoEjecucionFilterDto { IdPresupuesto = Presupuesto, SoloPresupuestables = true });

        Assert.Contains(todas, f => f.ConCuentaCode == Cuenta);
        Assert.DoesNotContain(controladas, f => f.ConCuentaCode == Cuenta);
    }

    [SkippableFact]
    public async Task Ejecucion_FiltraSoloLasPartidasConMovimiento()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var sinMovimiento = await _ejecucion!.ListarEjecucionAsync(
            new PresupuestoEjecucionFilterDto { IdPresupuesto = Presupuesto, SoloConMovimiento = true });
        Assert.DoesNotContain(sinMovimiento, f => f.ConCuentaCode == Cuenta);

        await AprobarOrdenAsync(300m);

        var conMovimiento = await _ejecucion.ListarEjecucionAsync(
            new PresupuestoEjecucionFilterDto { IdPresupuesto = Presupuesto, SoloConMovimiento = true });
        Assert.Contains(conMovimiento, f => f.ConCuentaCode == Cuenta);
    }

    // ── Compromisos pendientes ───────────────────────────────────────────────

    [SkippableFact]
    public async Task Compromisos_ListaLaOrdenQueRetieneSaldo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(300m);

        var filas = await _ejecucion!.ListarCompromisosPendientesAsync(
            new PresupuestoCompromisoFilterDto { IdPresupuesto = Presupuesto });

        var fila = Assert.Single(filas);
        Assert.Equal(oc.Id, fila.DocumentoId);
        Assert.Equal(300m, fila.MontoComprometido);
        Assert.Equal(0m, fila.MontoDevengado);
        Assert.Equal(300m, fila.SaldoComprometido);
        Assert.Equal(Cuenta, fila.ConCuentaCode);
    }

    [SkippableFact]
    public async Task Compromisos_LaOrdenLiberadaDejaDeAparecer()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(300m);
        await _ordenes!.AnularAsync(oc.Id, "tester");

        var filas = await _ejecucion!.ListarCompromisosPendientesAsync(
            new PresupuestoCompromisoFilterDto { IdPresupuesto = Presupuesto });

        Assert.Empty(filas);
    }

    // ── Kardex ───────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Kardex_ReconstruyeLaHistoriaDeLaPartida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(300m);
        await _ordenes!.AnularAsync(oc.Id, "tester");

        var movimientos = await _ejecucion!.ListarMovimientosAsync(Presupuesto, Cuenta);

        Assert.Equal(2, movimientos.Count);

        var compromiso = movimientos[0];
        Assert.Equal("Compromiso", compromiso.TipoMovimientoNombre);
        Assert.Equal(300m, compromiso.Monto);
        Assert.Equal(300m, compromiso.EfectoComprometido);
        Assert.Equal(0m, compromiso.ComprometidoAnterior);
        Assert.Equal(300m, compromiso.ComprometidoPosterior);
        Assert.Equal(1000m, compromiso.DisponibleAnterior);
        Assert.Equal(700m, compromiso.DisponiblePosterior);

        var liberacion = movimientos[1];
        Assert.Equal("Liberación de compromiso", liberacion.TipoMovimientoNombre);
        Assert.Equal(-300m, liberacion.EfectoComprometido);
        Assert.Equal(1000m, liberacion.DisponiblePosterior);
        Assert.Equal("Orden de compra anulada", liberacion.Observacion);
    }

    [SkippableFact]
    public async Task Kardex_DeUnaPartidaSinMovimientos_EsVacio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var movimientos = await _ejecucion!.ListarMovimientosAsync(Presupuesto, Cuenta);
        Assert.Empty(movimientos);
    }

    // ── Configuración ────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Configuracion_DevuelveSiempreLosCuatroModulos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var config = await _ejecucion!.ListarConfiguracionAsync();

        Assert.Equal(4, config.Count);
        Assert.Contains(config, c => c.Modulo == PresupuestoControlModulos.ComprasOc);
        Assert.Contains(config, c => c.Modulo == PresupuestoControlModulos.ComprasFactura);

        // El montaje de esta clase enciende COMPRAS_OC en Bloqueo; los módulos que no se tocan
        // siguen apagados, que es como nacen.
        Assert.Equal(PresupuestoControlModos.Bloqueo,
            config.Single(c => c.Modulo == PresupuestoControlModulos.ComprasOc).Modo);
        Assert.Equal(PresupuestoControlModos.Apagado,
            config.Single(c => c.Modulo == PresupuestoControlModulos.Proveedores).Modo);
        Assert.Equal(PresupuestoControlModos.Apagado,
            config.Single(c => c.Modulo == PresupuestoControlModulos.Bancos).Modo);
    }

    [SkippableFact]
    public async Task Configuracion_GuardarCambiaElModo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _ejecucion!.GuardarConfiguracionAsync(new PresupuestoControlConfigDto
        {
            Modulo = PresupuestoControlModulos.ComprasOc,
            Modo = PresupuestoControlModos.Bloqueo,
            ExigePresupuestoAprobado = true,
            ToleranciaPct = 0m,
            PermiteDevengoSinOc = 1
        }, "tester");

        var config = await _ejecucion.ListarConfiguracionAsync();
        var compras = Assert.Single(config, c => c.Modulo == PresupuestoControlModulos.ComprasOc);
        Assert.Equal(PresupuestoControlModos.Bloqueo, compras.Modo);
        Assert.Equal("Bloqueo", compras.ModoDescripcion);
    }

    [SkippableFact]
    public async Task Configuracion_RechazaValoresFueraDeRango()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ejecucion!.GuardarConfiguracionAsync(new PresupuestoControlConfigDto
            {
                Modulo = PresupuestoControlModulos.ComprasOc,
                Modo = 7
            }, "tester"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ejecucion!.GuardarConfiguracionAsync(new PresupuestoControlConfigDto
            {
                Modulo = PresupuestoControlModulos.ComprasFactura,
                Modo = PresupuestoControlModos.Bloqueo,
                ToleranciaPct = 250m
            }, "tester"));
    }

    // ── Montaje ──────────────────────────────────────────────────────────────

    private async Task<OrdenCompraDto> AprobarOrdenAsync(decimal monto)
    {
        var oc = await _ordenes!.CrearAsync(new OrdenCompraDto
        {
            CodProveedor = _codProveedor,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            FechaEntregaPactada = DateOnly.FromDateTime(DateTime.Today.AddDays(15)),
            CalculaIsv = false,
            Detalles = new List<OrdenCompraDetalleDto>
            {
                new() { ArticuloId = _articulo, CantidadPedida = 1m, CostoUnitario = monto }
            }
        }, "tester");

        await EjecutarAsync(
            "UPDATE public.alm_orden_compra_detalle SET cuenta_presupuestaria = @cta WHERE company_id = @c AND orden_compra_id = @o;",
            ("cta", Cuenta), ("c", CompanyId), ("o", oc.Id));

        await _ordenes.AprobarAsync(oc.Id, "tester");
        return (await _ordenes.GetByIdAsync(oc.Id))!;
    }

    private async Task SembrarAsync()
    {
        await EjecutarAsync(@"
INSERT INTO public.alm_articulo_proveedor (company_id, articulo_id, cod_proveedor, codigo_upc, activo)
VALUES (@c, @a, @p, 'TEST-UPC-F5', TRUE) ON CONFLICT DO NOTHING;",
            ("c", CompanyId), ("a", _articulo), ("p", _codProveedor));

        await EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_hdr
       (company_id, id_presupuesto, valor_global, valor_disponible, valor_comprometido,
        rango_periodo, fecha_inicia, fecha_finaliza, estado_aprobado)
VALUES (@c, @p, 50000, 50000, 0, 12,
        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, 1, 1),
        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, 12, 31), TRUE)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto));

        await EjecutarAsync(@"
INSERT INTO public.con_plan_cuentas (account_id, company_id, code, name, account_type, level,
                                     allows_posting, allows_budget, status, created_at, created_by)
SELECT (SELECT COALESCE(MAX(account_id), 0) + 1 FROM public.con_plan_cuentas),
       @c, @code, 'Cuenta de prueba F5', 'GASTO', 1, TRUE, TRUE, 'A', now(), 'tester'
 WHERE NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas WHERE company_id = @c AND code = @code);",
            ("c", CompanyId), ("code", Cuenta));

        await MarcarPresupuestableAsync(true);

        await EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_dtl
       (company_id, id_presupuesto, con_cuenta_code, id_presupuesto_dtl,
        valor_proyeccion, valor_real, valor_comprometido, valor_pagado, valor_disponible)
VALUES (@c, @p, @cuenta,
        (SELECT COALESCE(MAX(id_presupuesto_dtl), 0) + 1 FROM public.pst_config_presupuesto_dtl),
        1000, 0, 0, 0, 1000)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto), ("cuenta", Cuenta));

        await EjecutarAsync(@"
INSERT INTO public.cfg_presupuesto_control (company_id, modulo, modo)
VALUES (@c, 'COMPRAS_OC', 2)
ON CONFLICT (company_id, modulo) DO UPDATE SET modo = 2;", ("c", CompanyId));
    }

    private Task MarcarPresupuestableAsync(bool marcada) => EjecutarAsync(
        "UPDATE public.con_plan_cuentas SET allows_budget = @b WHERE company_id = @c AND code = @code;",
        ("b", marcada), ("c", CompanyId), ("code", Cuenta));

    private async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
