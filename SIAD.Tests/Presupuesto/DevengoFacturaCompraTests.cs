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
/// Ciclo completo del control presupuestario (fase F3): devengo de la factura, reversa al anularla,
/// cancelación y cierre anticipado de la orden.
/// <para>
/// La comprobación que sostiene todo el modelo está en
/// <see cref="Devengar_MueveDeComprometidoAEjecutado_SinTocarElDisponible"/>: <b>devengar no cambia
/// el disponible</b>. Es lo que evita contar dos veces la misma compra (una al aprobar la orden y
/// otra al facturarla).
/// </para>
/// </summary>
[Collection("Postgres")]
public class DevengoFacturaCompraTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private OrdenCompraService? _ordenes;
    private RecepcionCompraService? _recepciones;
    private PresupuestoCompromisoService? _presupuesto;

    private string _codProveedor = string.Empty;
    private int _articulo;
    private int _bodega;

    private const string Cuenta = "TEST-PST-F3";
    private const string Presupuesto = "TST-F3";   // id_presupuesto es VARCHAR(10)

    public DevengoFacturaCompraTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);

        var empresa = new TestCurrentCompanyService(CompanyId);
        _presupuesto = new PresupuestoCompromisoService(_context, empresa);
        var motor = new InventarioPostingService(_context, empresa, new ArticuloRollupService(_context));
        _ordenes = new OrdenCompraService(_context, empresa, new TasaIsvArticuloResolver(_context), _presupuesto,
            new AprobacionService(_context, empresa, new TestCurrentUserService()),
            new AprobacionNotificadorNoop());
        _recepciones = new RecepcionCompraService(
            _context, empresa, motor, new TasaIsvArticuloResolver(_context), _presupuesto);

        await DesactivarIntegracionContableAsync();

        _codProveedor = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && (p.status == null || p.status == true))
            .OrderBy(p => p.cod_proveedor).Select(p => p.cod_proveedor).FirstAsync();

        _articulo = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.activo).OrderBy(a => a.id).Select(a => a.id).FirstAsync();

        _bodega = await _context.alm_bodegas.AsNoTracking()
            .OrderBy(b => b.id).Select(b => b.id).FirstAsync();

        await SembrarRelacionProveedorAsync();
        await SembrarPresupuestoAsync();
        await FijarModoAsync("COMPRAS_OC", 2);
        await FijarModoAsync("COMPRAS_FACTURA", 2);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── La regla de oro ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Devengar_MueveDeComprometidoAEjecutado_SinTocarElDisponible()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);   // 1,000
        Assert.Equal(1000m, await ComprometidoAsync());
        Assert.Equal(4000m, await DisponibleAsync());                   // partida = 5,000

        // Se recibe la mitad.
        await RecibirAsync(oc, cantidad: 5m, costo: 100m);

        Assert.Equal(500m, await ComprometidoAsync());
        Assert.Equal(500m, await EjecutadoAsync());
        Assert.Equal(4000m, await DisponibleAsync());   // ← NO cambió. Es la regla de oro.
    }

    [SkippableFact]
    public async Task RecepcionTotal_ConsumeTodoElCompromiso_YCierraLaOrden()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        await RecibirAsync(oc, cantidad: 10m, costo: 100m);

        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(1000m, await EjecutadoAsync());
        Assert.Equal(4000m, await DisponibleAsync());
        Assert.Equal(EstadoOrdenCompra.Cerrada, (await _ordenes!.GetByIdAsync(oc.Id))!.Estado);
    }

    // ── Cancelación: libera SOLO el saldo ────────────────────────────────────

    [SkippableFact]
    public async Task CancelarConRecepcionParcial_LiberaSoloElSaldoPendiente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);   // compromete 1,000
        await RecibirAsync(oc, cantidad: 6m, costo: 100m);              // devenga 600

        Assert.Equal(400m, await ComprometidoAsync());

        await _ordenes!.CancelarAsync(oc.Id, "El proveedor no puede completar", "tester");

        // Libera 400, no 1,000: los 600 ya ejecutados no vuelven.
        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(600m, await EjecutadoAsync());
        Assert.Equal(4400m, await DisponibleAsync());
        Assert.Equal(EstadoOrdenCompra.Cancelada, (await _ordenes.GetByIdAsync(oc.Id))!.Estado);
    }

    [SkippableFact]
    public async Task CerrarAnticipadamente_LiberaElSaldoYDejaLaOrdenCerrada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        await RecibirAsync(oc, cantidad: 7m, costo: 100m);

        await _ordenes!.CerrarAsync(oc.Id, "Lo recibido alcanza", "tester");

        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(700m, await EjecutadoAsync());
        Assert.Equal(4300m, await DisponibleAsync());
        Assert.Equal(EstadoOrdenCompra.Cerrada, (await _ordenes.GetByIdAsync(oc.Id))!.Estado);
    }

    [SkippableFact]
    public async Task CancelarSinMotivo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 1m, costo: 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _ordenes!.CancelarAsync(oc.Id, "   ", "tester"));
    }

    // ── Reversa del devengo ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task AnularLaFactura_ConLaOrdenAbierta_RestituyeElCompromiso()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        var factura = await RecibirAsync(oc, cantidad: 4m, costo: 100m);

        Assert.Equal(600m, await ComprometidoAsync());
        Assert.Equal(400m, await EjecutadoAsync());

        await _recepciones!.AnularAsync(factura.Id, "Error de digitación", "tester");

        // El compromiso vuelve: la orden sigue abierta y se puede volver a recibir.
        Assert.Equal(1000m, await ComprometidoAsync());
        Assert.Equal(0m, await EjecutadoAsync());
        Assert.Equal(4000m, await DisponibleAsync());
    }

    [SkippableFact]
    public async Task AnularLaFactura_ConLaOrdenCancelada_DevuelveAlDisponible()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        var factura = await RecibirAsync(oc, cantidad: 6m, costo: 100m);
        await _ordenes!.CancelarAsync(oc.Id, "Ya no se necesita el resto", "tester");

        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(4400m, await DisponibleAsync());

        await _recepciones!.AnularAsync(factura.Id, "Error", "tester");

        // La orden sigue Cancelada: no hay compromiso que restituir, el importe vuelve al disponible.
        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(0m, await EjecutadoAsync());
        Assert.Equal(5000m, await DisponibleAsync());
    }

    [SkippableFact]
    public async Task AnularLaFactura_NoResucitaUnaOrdenCancelada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        var factura = await RecibirAsync(oc, cantidad: 6m, costo: 100m);
        await _ordenes!.CancelarAsync(oc.Id, "Ya no se necesita el resto", "tester");

        await _recepciones!.AnularAsync(factura.Id, "Error", "tester");

        // Cancelada es terminal: si la orden volviera a Aprobada quedaría receptible pero con su
        // presupuesto ya liberado, y se podría recibir contra un compromiso inexistente.
        Assert.Equal(EstadoOrdenCompra.Cancelada, (await _ordenes.GetByIdAsync(oc.Id))!.Estado);
    }

    [SkippableFact]
    public async Task AnularDosVeces_NoRevierteDosVeces()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        var factura = await RecibirAsync(oc, cantidad: 4m, costo: 100m);

        await _recepciones!.AnularAsync(factura.Id, "Error", "tester");
        await _recepciones.AnularAsync(factura.Id, "Error", "tester");

        Assert.Equal(1000m, await ComprometidoAsync());
        Assert.Equal(0m, await EjecutadoAsync());
    }

    // ── Compra directa, sin orden ────────────────────────────────────────────

    [SkippableFact]
    public async Task CompraDirectaSinOrden_ConsumeElDisponible()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarSinOrdenAsync(1);   // 1 = consume disponible

        await RecibirDirectoAsync(cantidad: 3m, costo: 100m);

        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(300m, await EjecutadoAsync());
        Assert.Equal(4700m, await DisponibleAsync());   // aquí SÍ baja: no hubo compromiso previo
    }

    [SkippableFact]
    public async Task CompraDirectaSinOrden_ProhibidaPorConfiguracion_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarSinOrdenAsync(0);   // 0 = prohíbe comprar sin orden

        var ex = await EsperarRechazoAsync(() => RecibirDirectoAsync(cantidad: 1m, costo: 100m));

        Assert.Contains("no permite registrar compras sin orden de compra", ex.Message);
        Assert.Equal(0m, await EjecutadoAsync());
    }

    [SkippableFact]
    public async Task CompraDirectaSinOrden_SinDisponible_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarSinOrdenAsync(1);

        var ex = await EsperarRechazoAsync(() => RecibirDirectoAsync(cantidad: 100m, costo: 100m));

        Assert.Contains("excede el presupuesto disponible", ex.Message);
        Assert.Equal(0m, await EjecutadoAsync());
        Assert.Equal(5000m, await DisponibleAsync());
    }

    // ── Factura por más de lo comprometido ───────────────────────────────────

    [SkippableFact]
    public async Task FacturaPorMasQueLaOrden_ElExcesoValidaContraElDisponible()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);   // compromete 1,000

        // Se recibe la cantidad pedida pero a mayor costo: 10 × 150 = 1,500.
        await RecibirAsync(oc, cantidad: 10m, costo: 150m);

        // 1,000 salen del compromiso; los 500 de más consumen disponible.
        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(1500m, await EjecutadoAsync());
        Assert.Equal(3500m, await DisponibleAsync());
    }

    // ── D2: el devengo va por el TOTAL de la factura ─────────────────────────

    [SkippableFact]
    public async Task Devengo_VaPorElTotalDeLaFactura_IncluyendoElIsv()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarIsvDelTipoAsync(15m);

        // La orden se aprueba SIN ISV en su cálculo (CalculaIsv = false), así que compromete 1,000.
        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        Assert.Equal(1000m, await ComprometidoAsync());

        // La factura sí lleva ISV: 5 × 100 = 500 + 15% = 575. El devengo va por el total, que es lo
        // que se le va a pagar al proveedor y lo mismo que debita el asiento contable.
        await RecibirAsync(oc, cantidad: 5m, costo: 100m);

        Assert.Equal(575m, await EjecutadoAsync());
        Assert.Equal(425m, await ComprometidoAsync());
        Assert.Equal(4000m, await DisponibleAsync());   // sigue sin moverse
    }

    // ── Modo apagado: no-regresión ───────────────────────────────────────────

    [SkippableFact]
    public async Task ModoApagado_LaRecepcionNoTocaPresupuesto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync("COMPRAS_OC", 0);
        await FijarModoAsync("COMPRAS_FACTURA", 0);

        var oc = await AprobarOrdenAsync(cantidad: 10m, costo: 100m);
        await RecibirAsync(oc, cantidad: 5m, costo: 100m);

        Assert.Equal(0, await ContarMovimientosAsync());
        Assert.Equal(0m, await ComprometidoAsync());
        Assert.Equal(0m, await EjecutadoAsync());
    }

    // ── Montaje ──────────────────────────────────────────────────────────────

    private async Task<OrdenCompraDto> AprobarOrdenAsync(decimal cantidad, decimal costo)
    {
        var oc = await _ordenes!.CrearAsync(new OrdenCompraDto
        {
            CodProveedor = _codProveedor,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            FechaEntregaPactada = DateOnly.FromDateTime(DateTime.Today.AddDays(15)),
            CalculaIsv = false,
            Detalles = new List<OrdenCompraDetalleDto>
            {
                new() { ArticuloId = _articulo, CantidadPedida = cantidad, CostoUnitario = costo }
            }
        }, "tester");

        await EjecutarAsync(
            "UPDATE public.alm_orden_compra_detalle SET cuenta_presupuestaria = @cta WHERE company_id = @c AND orden_compra_id = @o;",
            ("cta", Cuenta), ("c", CompanyId), ("o", oc.Id));

        await _ordenes.AprobarAsync(oc.Id, "tester");
        return (await _ordenes.GetByIdAsync(oc.Id))!;
    }

    private async Task<RecepcionCompraDto> RecibirAsync(OrdenCompraDto oc, decimal cantidad, decimal costo)
    {
        var pendientes = await _recepciones!.ObtenerPendientesOrdenAsync(oc.Id);
        var renglon = pendientes.First();

        return await _recepciones.CrearAsync(new RecepcionCompraDto
        {
            CodProveedor = _codProveedor,
            OrdenCompraId = oc.Id,
            BodegaId = _bodega,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            CondicionPago = CondicionPagoCompra.Contado,
            DetallarIsv = true,               // sin ISV al costo: el monto es limpio
            Detalles = new List<RecepcionCompraDetalleDto>
            {
                new()
                {
                    ArticuloId = _articulo,
                    OrdenDetalleId = renglon.OrdenDetalleId,
                    Cantidad = cantidad,
                    CostoUnitario = costo
                }
            }
        }, "tester");
    }

    private Task<RecepcionCompraDto> RecibirDirectoAsync(decimal cantidad, decimal costo)
        => _recepciones!.CrearAsync(new RecepcionCompraDto
        {
            CodProveedor = _codProveedor,
            OrdenCompraId = null,             // ← compra directa
            BodegaId = _bodega,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            CondicionPago = CondicionPagoCompra.Contado,
            DetallarIsv = true,
            Detalles = new List<RecepcionCompraDetalleDto>
            {
                new() { ArticuloId = _articulo, Cantidad = cantidad, CostoUnitario = costo }
            }
        }, "tester");

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

        await EjecutarAsync(@"
INSERT INTO public.con_plan_cuentas (account_id, company_id, code, name, account_type, level,
                                     allows_posting, allows_budget, status, created_at, created_by)
SELECT (SELECT COALESCE(MAX(account_id), 0) + 1 FROM public.con_plan_cuentas),
       @c, @code, 'Cuenta de prueba F3', 'GASTO', 1, TRUE, TRUE, 'A', now(), 'tester'
 WHERE NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas WHERE company_id = @c AND code = @code);",
            ("c", CompanyId), ("code", Cuenta));

        // Por si la cuenta ya existía de otra corrida: hay que asegurar la marca.
        await EjecutarAsync(
            "UPDATE public.con_plan_cuentas SET allows_budget = TRUE WHERE company_id = @c AND code = @code;",
            ("c", CompanyId), ("code", Cuenta));

        await EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_dtl
       (company_id, id_presupuesto, con_cuenta_code, id_presupuesto_dtl,
        valor_proyeccion, valor_real, valor_comprometido, valor_pagado, valor_disponible)
VALUES (@c, @p, @cuenta,
        (SELECT COALESCE(MAX(id_presupuesto_dtl), 0) + 1 FROM public.pst_config_presupuesto_dtl),
        5000, 0, 0, 0, 5000)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto), ("cuenta", Cuenta));

        // La compra directa no tiene renglón de orden del cual heredar la cuenta: la toma del tipo
        // del artículo, así que el tipo tiene que apuntar a la cuenta de prueba.
        // Dos cosas sobre el tipo del artículo:
        //  1) su cuenta_inventario es la que usa la COMPRA DIRECTA (sin renglón de orden del cual
        //     heredar la cuenta presupuestaria);
        //  2) se le quita la tasa de ISV para que los montos de las pruebas sean limpios. El
        //     comportamiento CON impuesto tiene su propia prueba
        //     (Devengo_VaPorElTotalDeLaFactura_IncluyendoElIsv), porque el devengo va por el TOTAL
        //     de la factura — decisión D2 del diseño, y lo mismo que hace el asiento contable.
        await EjecutarAsync(@"
UPDATE public.alm_tipo_articulo SET cuenta_inventario = @cuenta, impuesto_tasa_id = NULL
 WHERE company_id = @c
   AND id = (SELECT tipo_articulo_id FROM public.alm_articulo WHERE company_id = @c AND id = @a);",
            ("cuenta", Cuenta), ("c", CompanyId), ("a", _articulo));
    }

    private Task SembrarRelacionProveedorAsync() => EjecutarAsync(@"
INSERT INTO public.alm_articulo_proveedor (company_id, articulo_id, cod_proveedor, codigo_upc, activo)
VALUES (@c, @a, @p, @upc, TRUE)
ON CONFLICT DO NOTHING;",
        ("c", CompanyId), ("a", _articulo), ("p", _codProveedor), ("upc", "TEST-UPC-F3"));

    private Task FijarModoAsync(string modulo, short modo) => EjecutarAsync(@"
INSERT INTO public.cfg_presupuesto_control (company_id, modulo, modo)
VALUES (@c, @m, @modo)
ON CONFLICT (company_id, modulo) DO UPDATE SET modo = EXCLUDED.modo;",
        ("c", CompanyId), ("m", modulo), ("modo", (int)modo));

    /// <summary>Asigna al tipo del artículo una tasa de ISV vigente con el porcentaje pedido.</summary>
    private Task FijarIsvDelTipoAsync(decimal porcentaje) => EjecutarAsync(@"
UPDATE public.alm_tipo_articulo SET impuesto_tasa_id = (
        SELECT t.id FROM public.cfg_impuesto_tasa t
         WHERE t.porcentaje = @pct
           AND CURRENT_DATE BETWEEN t.vigencia_desde AND COALESCE(t.vigencia_hasta, DATE '9999-12-31')
         ORDER BY t.id LIMIT 1)
 WHERE company_id = @c
   AND id = (SELECT tipo_articulo_id FROM public.alm_articulo WHERE company_id = @c AND id = @a);",
        ("pct", porcentaje), ("c", CompanyId), ("a", _articulo));

    private Task FijarSinOrdenAsync(short valor) => EjecutarAsync(
        "UPDATE public.cfg_presupuesto_control SET permite_devengo_sin_oc = @v WHERE company_id = @c AND modulo = 'COMPRAS_FACTURA';",
        ("v", (int)valor), ("c", CompanyId));

    // ── Lecturas ─────────────────────────────────────────────────────────────

    private Task<decimal> ComprometidoAsync() => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_comprometido,0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", Cuenta));

    private Task<decimal> EjecutadoAsync() => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_real,0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", Cuenta));

    private Task<decimal> DisponibleAsync() => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_disponible,0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", Cuenta));

    private Task<int> ContarMovimientosAsync() => EscalarAsync<int>(
        "SELECT count(*)::int FROM public.pst_movimiento WHERE company_id=@c AND id_presupuesto=@p",
        ("c", CompanyId), ("p", Presupuesto));

    // ── Utilería ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Ver <c>CompromisoOrdenCompraTests</c>: el rechazo aborta la transacción del test, así que hay
    /// que envolverlo en un SAVEPOINT para poder comprobar que no quedó nada escrito.
    /// </summary>
    private async Task<InvalidOperationException> EsperarRechazoAsync(Func<Task> accion)
    {
        await Transaction.SaveAsync("antes_del_rechazo");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        await Transaction.RollbackAsync("antes_del_rechazo");
        _context!.ChangeTracker.Clear();
        return ex;
    }

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
        var leido = await cmd.ExecuteScalarAsync();
        return leido is null or DBNull ? default! : (T)Convert.ChangeType(leido, typeof(T));
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
