using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Tenancy;
using SIAD.Services.Aprobaciones;
using SIAD.Services.Almacen;
using SIAD.Services.Presupuesto;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// Control presupuestario al aprobar la orden de compra (fase F2).
/// <para>
/// Todo el montaje —encender el control, marcar la cuenta como presupuestable, sembrar la partida y
/// fijar la cuenta del renglón— corre DENTRO de la transacción del test, que hace ROLLBACK. La base
/// queda intacta, y en particular el control vuelve a quedar apagado.
/// </para>
/// <para>
/// <b>Lo que estos tests NO cubren:</b> la concurrencia (casos 25 y 26 del diseño) necesita dos
/// conexiones simultáneas viendo el mismo dato comprometido, algo imposible dentro de un
/// BEGIN … ROLLBACK. Se verificó a mano contra el mirror el 2026-08-28 (una sesión bloqueó a la otra
/// 4.72 s y la segunda falló con error de negocio); queda registrado en el runbook, no aquí.
/// </para>
/// </summary>
[Collection("Postgres")]
public class CompromisoOrdenCompraTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private OrdenCompraService? _ordenes;

    private string _codProveedor = string.Empty;
    private int _articuloA;
    private int _articuloB;

    private const string CuentaA = "TEST-PST-A";
    private const string CuentaB = "TEST-PST-B";
    private const string Presupuesto = "TEST-PST";

    public CompromisoOrdenCompraTests(PostgresFixture fixture) : base(fixture) { }

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

        // El asiento contable de la compra no es lo que se prueba aquí.
        await DesactivarIntegracionContableAsync();

        _codProveedor = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && (p.status == null || p.status == true))
            .OrderBy(p => p.cod_proveedor).Select(p => p.cod_proveedor).FirstAsync();

        var articulos = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.activo).OrderBy(a => a.id).Select(a => a.id).Take(2).ToListAsync();
        _articuloA = articulos[0];
        _articuloB = articulos[1];

        await SembrarRelacionProveedorAsync(_articuloA);
        await SembrarRelacionProveedorAsync(_articuloB);
        await SembrarPresupuestoAsync();
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── 1. No-regresión: apagado no hace nada ────────────────────────────────

    [SkippableFact]
    public async Task ModoApagado_NoConsultaPresupuesto_NiRegistraNada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(0);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 999_999m);

        var ok = await _ordenes!.AprobarAsync(oc.Id, "tester");

        Assert.True(ok);
        Assert.Equal(EstadoOrdenCompra.Aprobada, (await _ordenes.GetByIdAsync(oc.Id))!.Estado);
        Assert.Equal(0, await ContarMovimientosAsync());
        Assert.Equal(0m, await ComprometidoAsync(CuentaA));
        Assert.Empty(_ordenes.UltimosAvisosPresupuesto);
    }

    // ── 2. Cuenta no presupuestable: se ignora ───────────────────────────────

    [SkippableFact]
    public async Task CuentaSinMarcaDePresupuesto_SeIgnora_YLaOrdenSeAprueba()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, false);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 999_999m);

        var ok = await _ordenes!.AprobarAsync(oc.Id, "tester");

        Assert.True(ok);
        Assert.Equal(0, await ContarMovimientosAsync());
    }

    // ── 3. Camino feliz ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task PresupuestoSuficiente_ComprometeYBajaElDisponible()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 400m);

        await _ordenes!.AprobarAsync(oc.Id, "tester");

        Assert.Equal(400m, await ComprometidoAsync(CuentaA));
        Assert.Equal(600m, await DisponibleAsync(CuentaA));   // partida sembrada con 1000
        Assert.Equal(0m, await EjecutadoAsync(CuentaA));
        Assert.Equal(1, await ContarCompromisosAsync(oc.Id));
        Assert.Equal(1, await ContarMovimientosAsync());
        Assert.Empty(_ordenes.UltimosAvisosPresupuesto);
    }

    // ── 4. Insuficiente en modo Bloqueo: no queda NADA ───────────────────────

    [SkippableFact]
    public async Task PresupuestoInsuficiente_EnModoBloqueo_RechazaYDejaLaOrdenEnBorrador()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 1500m);   // partida = 1000

        var ex = await EsperarRechazoAsync(() => _ordenes!.AprobarAsync(oc.Id, "tester"));

        Assert.Contains("excede el presupuesto disponible", ex.Message);
        Assert.Contains(CuentaA, ex.Message);

        // Lo que realmente importa: la transacción se revirtió entera.
        Assert.Equal(EstadoOrdenCompra.Borrador, (await _ordenes!.GetByIdAsync(oc.Id))!.Estado);
        Assert.Equal(0, await ContarMovimientosAsync());
        Assert.Equal(0, await ContarCompromisosAsync(oc.Id));
        Assert.Equal(0m, await ComprometidoAsync(CuentaA));
    }

    // ── 5. Insuficiente en modo Advertencia: pasa y avisa ────────────────────

    [SkippableFact]
    public async Task PresupuestoInsuficiente_EnModoAdvertencia_PasaYDevuelveElAviso()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(1);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 1500m);

        await _ordenes!.AprobarAsync(oc.Id, "tester");

        var aviso = Assert.Single(_ordenes.UltimosAvisosPresupuesto);
        Assert.Equal(CuentaA, aviso.CuentaCode);
        Assert.True(aviso.Excedio);
        Assert.Equal(1000m, aviso.Disponible);
        Assert.Equal(1500m, aviso.Requerido);
        Assert.Equal(500m, aviso.Exceso);
        Assert.Equal(1500m, await ComprometidoAsync(CuentaA));

        var movimiento = await PrimerMovimientoAsync();
        Assert.True(movimiento.Excedio);
    }

    // ── 6. Cuenta presupuestable sin presupuesto vigente ─────────────────────

    [SkippableFact]
    public async Task CuentaPresupuestableSinPresupuestoVigente_FallaEnBloqueo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaB, true);   // marcada, pero sin partida sembrada
        var oc = await CrearOrdenAsync(cuenta: CuentaB, monto: 10m);

        var ex = await EsperarRechazoAsync(() => _ordenes!.AprobarAsync(oc.Id, "tester"));

        Assert.Contains("no tiene un presupuesto vigente", ex.Message);
    }

    // ── 8. Multi-partida: si UNA no alcanza, no se aprueba nada ──────────────

    [SkippableFact]
    public async Task OrdenMultiPartida_SiUnaPartidaNoAlcanza_NoSeApruebaNada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        await MarcarCuentaPresupuestableAsync(CuentaB, true);
        await SembrarPartidaAsync(CuentaB, 100m);              // B alcanza para poco

        var oc = await CrearOrdenDosRenglonesAsync(
            cuentaPrimero: CuentaA, montoPrimero: 300m,        // cabe en A (1000)
            cuentaSegundo: CuentaB, montoSegundo: 900m);       // NO cabe en B (100)

        await EsperarRechazoAsync(() => _ordenes!.AprobarAsync(oc.Id, "tester"));

        // Ni siquiera la partida que sí alcanzaba quedó comprometida.
        Assert.Equal(0m, await ComprometidoAsync(CuentaA));
        Assert.Equal(0m, await ComprometidoAsync(CuentaB));
        Assert.Equal(EstadoOrdenCompra.Borrador, (await _ordenes!.GetByIdAsync(oc.Id))!.Estado);
    }

    // ── 9. Dos renglones contra la misma partida se validan JUNTOS ───────────

    [SkippableFact]
    public async Task DosRenglonesEnLaMismaPartida_SeConsolidanParaValidar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);

        // Cada renglón cabe por separado en 1000; juntos (1200) no. Deben validarse consolidados.
        var oc = await CrearOrdenDosRenglonesAsync(
            cuentaPrimero: CuentaA, montoPrimero: 600m,
            cuentaSegundo: CuentaA, montoSegundo: 600m);

        await EsperarRechazoAsync(() => _ordenes!.AprobarAsync(oc.Id, "tester"));

        Assert.Equal(0m, await ComprometidoAsync(CuentaA));
    }

    [SkippableFact]
    public async Task DosRenglonesEnLaMismaPartida_SiCaben_ComprometenUnaFilaPorRenglon()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);

        var oc = await CrearOrdenDosRenglonesAsync(
            cuentaPrimero: CuentaA, montoPrimero: 300m,
            cuentaSegundo: CuentaA, montoSegundo: 400m);

        await _ordenes!.AprobarAsync(oc.Id, "tester");

        Assert.Equal(700m, await ComprometidoAsync(CuentaA));
        Assert.Equal(2, await ContarCompromisosAsync(oc.Id));   // el compromiso vive por renglón
    }

    // ── Idempotencia ─────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Reaprobar_EsUnNoOp_NoComprometeDosVeces()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 400m);

        await _ordenes!.AprobarAsync(oc.Id, "tester");
        // Segunda llamada: AprobarAsync corta en "ya está Aprobada", así que se fuerza el camino
        // completo llamando directo al servicio de presupuesto (que es el que debe ser idempotente).
        var servicio = new PresupuestoCompromisoService(_context!, new TestCurrentCompanyService(CompanyId));
        var avisos = await servicio.ComprometerOrdenCompraAsync(
            oc.Id, oc.Numero.ToString("00000"), DateOnly.FromDateTime(DateTime.Today), "tester", "tester");

        Assert.Empty(avisos);
        Assert.Equal(400m, await ComprometidoAsync(CuentaA));   // sigue en 400, no 800
        Assert.Equal(1, await ContarMovimientosAsync());
    }

    // ── Anulación: libera ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnularOrdenAprobada_LiberaElCompromiso()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 400m);

        await _ordenes!.AprobarAsync(oc.Id, "tester");
        Assert.Equal(600m, await DisponibleAsync(CuentaA));

        await _ordenes.AnularAsync(oc.Id, "tester");

        Assert.Equal(0m, await ComprometidoAsync(CuentaA));
        Assert.Equal(1000m, await DisponibleAsync(CuentaA));    // devuelto íntegro
        Assert.Equal(2, await ContarMovimientosAsync());        // compromiso + liberación
    }

    // ── Panel previo de la pantalla (F4) ─────────────────────────────────────

    [SkippableFact]
    public async Task Previo_ConControlApagado_NoDevuelvePartidas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(0);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 400m);

        var previo = await ServicioPresupuesto().ConsultarPrevioOrdenCompraAsync(
            oc.Id, DateOnly.FromDateTime(DateTime.Today));

        Assert.False(previo.Activo);        // la pantalla no dibuja el panel
        Assert.Empty(previo.Partidas);
    }

    [SkippableFact]
    public async Task Previo_MuestraRequeridoYDisponible_YAvisaQueAlcanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 400m);

        var previo = await ServicioPresupuesto().ConsultarPrevioOrdenCompraAsync(
            oc.Id, DateOnly.FromDateTime(DateTime.Today));

        Assert.True(previo.Activo);
        Assert.False(previo.TieneFaltantes);

        var partida = Assert.Single(previo.Partidas);
        Assert.Equal(CuentaA, partida.CuentaCode);
        Assert.True(partida.Presupuestable);
        Assert.Equal(400m, partida.Requerido);
        Assert.Equal(1000m, partida.Disponible);
        Assert.Equal(600m, partida.Restante);
        Assert.False(partida.Falta);
    }

    [SkippableFact]
    public async Task Previo_AvisaAntesDeAprobar_CuandoNoAlcanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 1500m);

        var previo = await ServicioPresupuesto().ConsultarPrevioOrdenCompraAsync(
            oc.Id, DateOnly.FromDateTime(DateTime.Today));

        // El panel lo advierte ANTES de que el usuario pulse Aprobar y se lleve el rechazo.
        Assert.True(previo.TieneFaltantes);
        var partida = Assert.Single(previo.Partidas);
        Assert.True(partida.Falta);
        Assert.Equal(500m, partida.Faltante);
    }

    [SkippableFact]
    public async Task Previo_MarcaLasCuentasQueNoParticipanDelControl()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, false);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 99_999m);

        var previo = await ServicioPresupuesto().ConsultarPrevioOrdenCompraAsync(
            oc.Id, DateOnly.FromDateTime(DateTime.Today));

        var partida = Assert.Single(previo.Partidas);
        Assert.False(partida.Presupuestable);
        Assert.False(partida.Falta);          // no controlada: no puede faltar
        Assert.False(previo.TieneFaltantes);
    }

    private PresupuestoCompromisoService ServicioPresupuesto()
        => new(_context!, new TestCurrentCompanyService(CompanyId));

    // ── Aislamiento multiempresa ─────────────────────────────────────────────

    [SkippableFact]
    public async Task ElCompromisoSeEstampaConLaEmpresaActual()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(2);
        await MarcarCuentaPresupuestableAsync(CuentaA, true);
        var oc = await CrearOrdenAsync(cuenta: CuentaA, monto: 100m);

        await _ordenes!.AprobarAsync(oc.Id, "tester");

        Assert.Equal(1, await EscalarAsync<int>(
            "SELECT count(*)::int FROM public.pst_compromiso WHERE documento_id = @d AND company_id = @c",
            ("d", (long)oc.Id), ("c", CompanyId)));
        Assert.Equal(0, await EscalarAsync<int>(
            "SELECT count(*)::int FROM public.pst_compromiso WHERE documento_id = @d AND company_id <> @c",
            ("d", (long)oc.Id), ("c", CompanyId)));
    }

    // ── Montaje ──────────────────────────────────────────────────────────────

    private async Task SembrarPresupuestoAsync()
    {
        await EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_hdr
       (company_id, id_presupuesto, valor_global, valor_disponible, valor_comprometido,
        rango_periodo, fecha_inicia, fecha_finaliza, estado_aprobado)
VALUES (@c, @p, 100000, 100000, 0, 12,
        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, 1, 1),
        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, 12, 31), TRUE)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto));

        await SembrarCuentaDelPlanAsync(CuentaA);
        await SembrarCuentaDelPlanAsync(CuentaB);
        await SembrarPartidaAsync(CuentaA, 1000m);
    }

    /// <summary>Cuenta de prueba en el plan, si no existe. Nace SIN marca de presupuesto.</summary>
    private Task SembrarCuentaDelPlanAsync(string code) => EjecutarAsync(@"
INSERT INTO public.con_plan_cuentas (account_id, company_id, code, name, account_type, level,
                                     allows_posting, allows_budget, status, created_at, created_by)
SELECT (SELECT COALESCE(MAX(account_id), 0) + 1 FROM public.con_plan_cuentas),
       @c, @code, 'Cuenta de prueba presupuesto', 'GASTO', 1, TRUE, FALSE, 'A', now(), 'tester'
 WHERE NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas WHERE company_id = @c AND code = @code);",
        ("c", CompanyId), ("code", code));

    private Task SembrarPartidaAsync(string cuenta, decimal proyeccion) => EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_dtl
       (company_id, id_presupuesto, con_cuenta_code, id_presupuesto_dtl,
        valor_proyeccion, valor_real, valor_comprometido, valor_pagado, valor_disponible)
VALUES (@c, @p, @cuenta,
        (SELECT COALESCE(MAX(id_presupuesto_dtl), 0) + 1 FROM public.pst_config_presupuesto_dtl),
        @v, 0, 0, 0, @v)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto), ("cuenta", cuenta), ("v", proyeccion));

    private Task FijarModoAsync(short modo) => EjecutarAsync(@"
INSERT INTO public.cfg_presupuesto_control (company_id, modulo, modo)
VALUES (@c, 'COMPRAS_OC', @m)
ON CONFLICT (company_id, modulo) DO UPDATE SET modo = EXCLUDED.modo;",
        ("c", CompanyId), ("m", (int)modo));

    private Task MarcarCuentaPresupuestableAsync(string cuenta, bool marcada) => EjecutarAsync(
        "UPDATE public.con_plan_cuentas SET allows_budget = @b WHERE company_id = @c AND code = @cuenta;",
        ("b", marcada), ("c", CompanyId), ("cuenta", cuenta));

    private Task SembrarRelacionProveedorAsync(int articuloId) => EjecutarAsync(@"
INSERT INTO public.alm_articulo_proveedor (company_id, articulo_id, cod_proveedor, codigo_upc, activo)
VALUES (@c, @a, @p, @upc, TRUE)
ON CONFLICT DO NOTHING;",
        ("c", CompanyId), ("a", articuloId), ("p", _codProveedor), ("upc", $"TEST-UPC-{articuloId}"));

    private async Task<OrdenCompraDto> CrearOrdenAsync(string cuenta, decimal monto)
    {
        var oc = await _ordenes!.CrearAsync(new OrdenCompraDto
        {
            CodProveedor = _codProveedor,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            FechaEntregaPactada = DateOnly.FromDateTime(DateTime.Today.AddDays(15)),
            CalculaIsv = false,
            Detalles = new List<OrdenCompraDetalleDto>
            {
                new() { ArticuloId = _articuloA, CantidadPedida = 1m, CostoUnitario = monto }
            }
        }, "tester");

        await FijarCuentaDelRenglonAsync(oc.Id, cuenta);
        return oc;
    }

    private async Task<OrdenCompraDto> CrearOrdenDosRenglonesAsync(
        string cuentaPrimero, decimal montoPrimero, string cuentaSegundo, decimal montoSegundo)
    {
        var oc = await _ordenes!.CrearAsync(new OrdenCompraDto
        {
            CodProveedor = _codProveedor,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            FechaEntregaPactada = DateOnly.FromDateTime(DateTime.Today.AddDays(15)),
            CalculaIsv = false,
            Detalles = new List<OrdenCompraDetalleDto>
            {
                new() { ArticuloId = _articuloA, CantidadPedida = 1m, CostoUnitario = montoPrimero },
                new() { ArticuloId = _articuloB, CantidadPedida = 1m, CostoUnitario = montoSegundo }
            }
        }, "tester");

        var ids = await IdsDeRenglonesAsync(oc.Id);
        await EjecutarAsync("UPDATE public.alm_orden_compra_detalle SET cuenta_presupuestaria = @cta WHERE id = @id;",
            ("cta", cuentaPrimero), ("id", ids[0]));
        await EjecutarAsync("UPDATE public.alm_orden_compra_detalle SET cuenta_presupuestaria = @cta WHERE id = @id;",
            ("cta", cuentaSegundo), ("id", ids[1]));
        return oc;
    }

    private Task FijarCuentaDelRenglonAsync(int ordenId, string cuenta) => EjecutarAsync(
        "UPDATE public.alm_orden_compra_detalle SET cuenta_presupuestaria = @cta WHERE company_id = @c AND orden_compra_id = @o;",
        ("cta", cuenta), ("c", CompanyId), ("o", ordenId));

    private async Task<List<int>> IdsDeRenglonesAsync(int ordenId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT id FROM public.alm_orden_compra_detalle WHERE company_id = @c AND orden_compra_id = @o ORDER BY id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("o", ordenId);
        var ids = new List<int>();
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync()) ids.Add(rd.GetInt32(0));
        return ids;
    }

    // ── Lecturas ─────────────────────────────────────────────────────────────

    private Task<decimal> ComprometidoAsync(string cuenta) => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_comprometido, 0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", cuenta));

    private Task<decimal> EjecutadoAsync(string cuenta) => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_real, 0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", cuenta));

    private Task<decimal> DisponibleAsync(string cuenta) => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_disponible, 0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", cuenta));

    private Task<int> ContarMovimientosAsync() => EscalarAsync<int>(
        "SELECT count(*)::int FROM public.pst_movimiento WHERE company_id=@c AND id_presupuesto=@p",
        ("c", CompanyId), ("p", Presupuesto));

    private Task<int> ContarCompromisosAsync(int ordenId) => EscalarAsync<int>(
        "SELECT count(*)::int FROM public.pst_compromiso WHERE company_id=@c AND documento_id=@d AND estado=1",
        ("c", CompanyId), ("d", (long)ordenId));

    private async Task<(bool Excedio, decimal Monto)> PrimerMovimientoAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT excedio, monto FROM public.pst_movimiento WHERE company_id=@c AND id_presupuesto=@p ORDER BY id LIMIT 1;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", Presupuesto);
        await using var rd = await cmd.ExecuteReaderAsync();
        Assert.True(await rd.ReadAsync(), "No se registró ningún movimiento presupuestario.");
        return (rd.GetBoolean(0), rd.GetDecimal(1));
    }

    // ── Utilería SQL ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta una acción que se espera que FALLE y deja la transacción del test utilizable.
    /// <para>
    /// Cuando el control rechaza, el <c>RAISE</c> de PostgreSQL aborta la transacción en curso —que
    /// aquí es la del test, porque <c>TransaccionAmbiente</c> reusa la ambiente en vez de anidar—.
    /// Sin un SAVEPOINT, toda consulta posterior fallaría con «transacción abortada» y no se podría
    /// comprobar lo único que importa: que NO quedó nada escrito.
    /// </para>
    /// <para>
    /// En producción no hace falta: ahí <c>AprobarAsync</c> abre su propia transacción y el rechazo
    /// la revierte a ella sola, sin tocar el resto de la petición.
    /// </para>
    /// </summary>
    private async Task<InvalidOperationException> EsperarRechazoAsync(Func<Task> accion)
    {
        await Transaction.SaveAsync("antes_del_rechazo");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        await Transaction.RollbackAsync("antes_del_rechazo");
        _context!.ChangeTracker.Clear();
        return ex;
    }

    // ── Utilería SQL ─────────────────────────────────────────────────────────

    private async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        var valorLeido = await cmd.ExecuteScalarAsync();
        return valorLeido is null or DBNull ? default! : (T)Convert.ChangeType(valorLeido, typeof(T));
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
