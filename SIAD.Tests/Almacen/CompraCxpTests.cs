using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Entities;
using SIAD.Services.Almacen;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Services.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Pagos a proveedores sobre las cuentas por pagar de compra (alm_compra_cxp): saldo/estado,
/// numeración de abonos, validaciones, anulación (reabre la CxP) y el movimiento bancario real.
/// Cada test corre dentro de BEGIN … ROLLBACK.
/// </summary>
[Collection("Postgres")]
public class CompraCxpTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RecepcionCompraService? _recepciones;
    private CompraCxpService? _service;

    private string _codProveedor = string.Empty;
    private int _articuloA;
    private int _bodegaId;

    private const string UpcA = "TEST-CXP-A";

    public CompraCxpTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var company = new TestCurrentCompanyService(CompanyId);
        _context = new SiadDbContext(options, company);
        _context.Database.UseTransaction(Transaction);

        var motor = new InventarioPostingService(_context, company, new ArticuloRollupService(_context));
        _recepciones = new RecepcionCompraService(_context, company, motor, new TasaIsvArticuloResolver(_context));
        var cheques = new ChequesService(_context, company, Substitute.For<IAccountFormatService>());
        _service = new CompraCxpService(_context, company, cheques);

        _codProveedor = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && (p.status == null || p.status == true))
            .OrderBy(p => p.cod_proveedor).Select(p => p.cod_proveedor).FirstAsync();

        _articuloA = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.activo).OrderBy(a => a.id).Select(a => a.id).FirstAsync();

        var bodega = new alm_bodega { codigo = "ZCXP1", nombre = "Bodega CxP", activo = true };
        _context.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();
        _bodegaId = bodega.id;

        await SembrarRelacionAsync(_articuloA, UpcA);

        // La contabilidad de compras se gatea con activo_almacen (factura) y activo_proveedores
        // (pago), ambos encendidos en el mirror. Se apagan aquí, dentro de la transacción del test,
        // para aislar los tests de mecánica de pago; los tests de Fase 2 encienden lo que prueban.
        await ApagarIntegracionContableAsync();
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Pago en efectivo (sin banco): cubre saldo/estado/numeración/anulación ────

    [SkippableFact]
    public async Task RegistrarAbono_Efectivo_Parcial_BajaElSaldoYQuedaParcial()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();

        var r = await _service!.RegistrarAbonoAsync(cxpId, Pago(monto - 100m, MetodoPagoCompra.Efectivo), "tester");

        Assert.True(r.Success);
        Assert.Equal(1, r.NumeroAbono);
        Assert.Equal(100m, r.Saldo);
        Assert.Equal(EstadoCompraCxp.Parcial, r.EstadoId);

        var cxp = await _context!.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.id == cxpId);
        Assert.Equal(100m, cxp.saldo);
        Assert.Equal(EstadoCompraCxp.Parcial, cxp.estado_id);
    }

    [SkippableFact]
    public async Task RegistrarAbono_Efectivo_Total_QuedaPagada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();

        var r = await _service!.RegistrarAbonoAsync(cxpId, Pago(monto, MetodoPagoCompra.Efectivo), "tester");

        Assert.Equal(0m, r.Saldo);
        Assert.Equal(EstadoCompraCxp.Pagada, r.EstadoId);
    }

    [SkippableFact]
    public async Task RegistrarAbono_MayorQueElSaldo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.RegistrarAbonoAsync(cxpId, Pago(monto + 1m, MetodoPagoCompra.Efectivo), "tester"));
        Assert.Contains("supera el saldo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task RegistrarAbono_DosPagos_ElNumeroAvanzaYLiquida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();

        var primero = await _service!.RegistrarAbonoAsync(cxpId, Pago(monto - 50m, MetodoPagoCompra.Efectivo), "tester");
        var segundo = await _service.RegistrarAbonoAsync(cxpId, Pago(50m, MetodoPagoCompra.Efectivo), "tester");

        Assert.Equal(1, primero.NumeroAbono);
        Assert.Equal(2, segundo.NumeroAbono);
        Assert.Equal(EstadoCompraCxp.Pagada, segundo.EstadoId);
        Assert.Equal(0m, segundo.Saldo);
    }

    [SkippableFact]
    public async Task AnularAbono_Efectivo_ReabreLaCxp()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();
        await _service!.RegistrarAbonoAsync(cxpId, Pago(monto, MetodoPagoCompra.Efectivo), "tester");

        Assert.True(await _service.AnularAbonoAsync(cxpId, 1, "pago mal aplicado", "tester"));

        var cxp = await _context!.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.id == cxpId);
        Assert.Equal(monto, cxp.saldo);
        Assert.Equal(EstadoCompraCxp.Pendiente, cxp.estado_id);

        var abono = await _context.alm_compra_cxp_abonos.AsNoTracking().FirstAsync(a => a.cxp_id == cxpId && a.numero_abono == 1);
        Assert.Equal("A", abono.estado);
    }

    [SkippableFact]
    public async Task AnularAbono_QueNoEsElUltimoVigente_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();
        await _service!.RegistrarAbonoAsync(cxpId, Pago(monto - 60m, MetodoPagoCompra.Efectivo), "tester");
        await _service.RegistrarAbonoAsync(cxpId, Pago(30m, MetodoPagoCompra.Efectivo), "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AnularAbonoAsync(cxpId, 1, "no debería", "tester"));
        Assert.Contains("último pago", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Listar_MuestraLaCxpPendienteConSuSaldo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();

        var lista = await _service!.ListarAsync(new CompraCxpFilterDto { CodProveedor = _codProveedor });
        var fila = lista.FirstOrDefault(x => x.Id == cxpId);

        Assert.NotNull(fila);
        Assert.Equal(monto, fila!.Saldo);
        Assert.Equal(0m, fila.Abonado);
        Assert.Equal(EstadoCompraCxp.Pendiente, fila.EstadoId);
    }

    // ── Pago bancario (transferencia): genera el movimiento en ban_kardex ────────

    [SkippableFact]
    public async Task RegistrarAbono_Transferencia_GeneraMovimientoBancario()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var (cxpId, monto) = await CrearCxpAsync();

        var cuentaContableId = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuentaContableId is null, "El tenant de prueba no tiene cuenta contable posteable");
        var bancoCuentaId = await SembrarCuentaBancariaAsync(cuentaContableId!.Value);
        // El tenant ya tiene el tipo de transacción de salida 'TRF' (Transferencia); no se siembra.

        var r = await _service!.RegistrarAbonoAsync(
            cxpId,
            new CompraCxpAbonoUpsertDto { Monto = 100m, MetodoPago = MetodoPagoCompra.Transferencia, BancoCuentaId = bancoCuentaId },
            "tester");

        Assert.True(r.Success);
        Assert.Equal(monto - 100m, r.Saldo);

        // El abono quedó ligado a un movimiento bancario (ban_kardex), sin póliza (Fase 2).
        var abono = await _context!.alm_compra_cxp_abonos.AsNoTracking().FirstAsync(a => a.cxp_id == cxpId && a.numero_abono == 1);
        Assert.NotNull(abono.ban_kardex_id);
        Assert.Null(abono.partida_id);

        var movimiento = await _context.ban_kardex.AsNoTracking().FirstAsync(k => k.ban_kardex_id == abono.ban_kardex_id!.Value);
        Assert.Equal(bancoCuentaId, movimiento.banco_cuenta_id);
    }

    // ── Fase 2: módulo contable propio COMPRAS (factura + pago), gated por activo_compras ──

    [SkippableFact]
    public async Task Fase2_ComprasApagado_LaFacturaNoGeneraPoliza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // El setup dejó activo_compras apagado: la factura nace SIN asiento al mayor.
        var (cxpId, _) = await CrearCxpAsync();
        var hdrId = await _context!.alm_compra_cxps.AsNoTracking()
            .Where(c => c.id == cxpId).Select(c => c.compra_hdr_id).FirstAsync();

        Assert.Equal(0, await ContarPartidasAsync("COMPRAS", "FACTURA", hdrId));
    }

    [SkippableFact]
    public async Task Fase2_ComprasApagado_ElPagoBancarioNoGeneraPoliza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // El setup dejó activo_compras apagado: el pago mueve el banco pero no postea al mayor.
        var (cxpId, _) = await CrearCxpAsync();
        var cuentaContableId = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuentaContableId is null, "El tenant de prueba no tiene cuenta contable posteable");
        var bancoCuentaId = await SembrarCuentaBancariaAsync(cuentaContableId!.Value);

        await _service!.RegistrarAbonoAsync(
            cxpId,
            new CompraCxpAbonoUpsertDto { Monto = 100m, MetodoPago = MetodoPagoCompra.Transferencia, BancoCuentaId = bancoCuentaId },
            "tester");

        Assert.Equal(0, await ContarPartidasAsync("COMPRAS", "CXP-ABO1", cxpId));
        var abono = await _context!.alm_compra_cxp_abonos.AsNoTracking().FirstAsync(a => a.cxp_id == cxpId && a.numero_abono == 1);
        Assert.Null(abono.partida_id);
    }

    [SkippableFact]
    public async Task Fase2_ComprasEncendido_LaFacturaGeneraAsientoCuadrado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConfigurarCuentasAsync(_codProveedor, _articuloA);
        await SembrarAsientoComprasAsync();
        await EncenderModuloAsync("activo_compras");

        var (cxpId, monto) = await CrearCxpAsync();
        var hdrId = await _context!.alm_compra_cxps.AsNoTracking()
            .Where(c => c.id == cxpId).Select(c => c.compra_hdr_id).FirstAsync();

        var resumen = await ResumenPartidaAsync("COMPRAS", "FACTURA", hdrId);
        Skip.If(resumen is null || resumen.Value.Lineas == 0,
            "Sin período contable abierto en el mirror: el asiento se encoló (no hay póliza que verificar).");

        Assert.Equal(resumen!.Value.Debe, resumen.Value.Haber);   // el asiento cuadra
        Assert.Equal(monto, resumen.Value.Haber);                 // HABER proveedor = total de la factura
    }

    [SkippableFact]
    public async Task Fase2_ComprasEncendido_ElPagoBancarioGeneraAsientoCuadrado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConfigurarCuentasAsync(_codProveedor, _articuloA);
        await SembrarAsientoComprasAsync();
        await EncenderModuloAsync("activo_compras");

        var (cxpId, _) = await CrearCxpAsync();
        var cuentaContableId = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuentaContableId is null, "El tenant de prueba no tiene cuenta contable posteable");
        var bancoCuentaId = await SembrarCuentaBancariaAsync(cuentaContableId!.Value);

        await _service!.RegistrarAbonoAsync(
            cxpId,
            new CompraCxpAbonoUpsertDto { Monto = 150m, MetodoPago = MetodoPagoCompra.Transferencia, BancoCuentaId = bancoCuentaId },
            "tester");

        var resumen = await ResumenPartidaAsync("COMPRAS", "CXP-ABO1", cxpId);
        Skip.If(resumen is null || resumen.Value.Lineas == 0,
            "Sin período contable abierto en el mirror: el asiento del pago se encoló.");

        Assert.Equal(resumen!.Value.Debe, resumen.Value.Haber);   // el asiento del pago cuadra
        Assert.Equal(150m, resumen.Value.Debe);                   // DEBE proveedor = monto del pago
        var abono = await _context!.alm_compra_cxp_abonos.AsNoTracking().FirstAsync(a => a.cxp_id == cxpId && a.numero_abono == 1);
        Assert.NotNull(abono.partida_id);                         // el abono quedó ligado a su póliza
    }

    // ── Retención en el pago (libro fiscal compartido, origen=compra) ────────────

    [SkippableFact]
    public async Task RegistrarAbono_Efectivo_ConRetencion_CuadraYEscribeElRegistroFiscal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConfigurarCuentasAsync(_codProveedor, _articuloA);
        await SembrarAsientoComprasAsync();
        await EncenderModuloAsync("activo_compras");

        var cuenta = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuenta is null, "El tenant de prueba no tiene cuenta contable posteable");
        var retencionId = await PrimeraRetencionIdAsync();
        Skip.If(retencionId is null, "El mirror no tiene catálogo de retenciones (cfg_retencion)");

        var (cxpId, monto) = await CrearCxpAsync();
        // Retención 12.5% sobre 200 = 25; bruto aplicado 200, neto en caja 175.
        var ret = Retencion(retencionId.Value, cuenta!.Value, 200m, 12.5m);
        var dto = new CompraCxpAbonoUpsertDto
        {
            Monto = 200m,
            MetodoPago = MetodoPagoCompra.Efectivo,
            CuentaContableId = cuenta.Value,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Retenciones = new List<RetencionAplicadaDto> { ret }
        };

        var r = await _service!.RegistrarAbonoAsync(cxpId, dto, "tester");

        Assert.True(r.Success);
        Assert.Equal(25m, r.Retenido);
        Assert.Equal(monto - 200m, r.Saldo);   // el saldo baja por el BRUTO (200), no por el neto

        var abono = await _context!.alm_compra_cxp_abonos.AsNoTracking().FirstAsync(a => a.cxp_id == cxpId && a.numero_abono == 1);
        Assert.Equal(200m, abono.monto);       // monto = bruto
        Assert.Equal(25m, abono.retenido);

        var resumen = await ResumenPartidaAsync("COMPRAS", "CXP-ABO1", cxpId);
        Skip.If(resumen is null || resumen.Value.Lineas == 0, "Sin período contable abierto en el mirror: el asiento se encoló.");
        Assert.Equal(resumen!.Value.Debe, resumen.Value.Haber);   // cuadra
        Assert.Equal(200m, resumen.Value.Debe);                   // DEBE proveedor = bruto

        // Registro fiscal escrito: origen=compra, ligado a la CxP (sin compromiso) y a la partida.
        var fiscal = await LeerRetencionFiscalAsync(cxpId, 1);
        Assert.NotNull(fiscal);
        Assert.Equal(OrigenRetencion.Compra, fiscal!.Value.Origen);
        Assert.Equal(cxpId, fiscal.Value.CxpId);
        Assert.Null(fiscal.Value.NumeroOrden);
        Assert.Equal(25m, fiscal.Value.TotalRetenido);
        Assert.Equal(EstadoRetencion.Vigente, fiscal.Value.EstadoId);
        Assert.Equal(1, fiscal.Value.DtlCount);
        Assert.NotNull(fiscal.Value.PartidaId);
    }

    [SkippableFact]
    public async Task RegistrarAbono_Transferencia_ConRetencion_ElBancoMueveElNeto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuenta = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuenta is null, "El tenant de prueba no tiene cuenta contable posteable");
        var retencionId = await PrimeraRetencionIdAsync();
        Skip.If(retencionId is null, "El mirror no tiene catálogo de retenciones (cfg_retencion)");

        var (cxpId, _) = await CrearCxpAsync();
        var bancoCuentaId = await SembrarCuentaBancariaAsync(cuenta!.Value);
        // Retención 12.5% sobre 100 = 12.5; bruto 100, neto al banco 87.5.
        var ret = Retencion(retencionId.Value, cuenta.Value, 100m, 12.5m);
        var dto = new CompraCxpAbonoUpsertDto
        {
            Monto = 100m,
            MetodoPago = MetodoPagoCompra.Transferencia,
            BancoCuentaId = bancoCuentaId,
            Retenciones = new List<RetencionAplicadaDto> { ret }
        };

        var r = await _service!.RegistrarAbonoAsync(cxpId, dto, "tester");

        Assert.Equal(12.5m, r.Retenido);
        var abono = await _context!.alm_compra_cxp_abonos.AsNoTracking().FirstAsync(a => a.cxp_id == cxpId && a.numero_abono == 1);
        Assert.Equal(100m, abono.monto);       // el saldo/monto es el bruto
        Assert.NotNull(abono.ban_kardex_id);
        var mov = await _context.ban_kardex.AsNoTracking().FirstAsync(k => k.ban_kardex_id == abono.ban_kardex_id!.Value);
        Assert.Equal(87.5m, Math.Abs(mov.monto));   // el banco movió el NETO, no el bruto
    }

    [SkippableFact]
    public async Task RegistrarAbono_RetencionDejaNetoCero_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuenta = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuenta is null, "El tenant de prueba no tiene cuenta contable posteable");
        var retencionId = await PrimeraRetencionIdAsync();
        Skip.If(retencionId is null, "El mirror no tiene catálogo de retenciones (cfg_retencion)");

        var (cxpId, _) = await CrearCxpAsync();
        // Retención del 100% ⇒ neto 0 ⇒ rechaza (no se puede pagar 0 al banco/caja).
        var ret = Retencion(retencionId.Value, cuenta!.Value, 100m, 100m);
        var dto = new CompraCxpAbonoUpsertDto
        {
            Monto = 100m,
            MetodoPago = MetodoPagoCompra.Efectivo,
            CuentaContableId = cuenta.Value,
            Retenciones = new List<RetencionAplicadaDto> { ret }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.RegistrarAbonoAsync(cxpId, dto, "tester"));
        Assert.Contains("neto", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task RegistrarAbono_ConRetencion_ComprasApagado_EscribeFiscalSinPartida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // El setup dejó activo_compras apagado: no hay partida, pero el registro fiscal SÍ se escribe.
        var cuenta = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuenta is null, "El tenant de prueba no tiene cuenta contable posteable");
        var retencionId = await PrimeraRetencionIdAsync();
        Skip.If(retencionId is null, "El mirror no tiene catálogo de retenciones (cfg_retencion)");

        var (cxpId, _) = await CrearCxpAsync();
        var ret = Retencion(retencionId.Value, cuenta!.Value, 100m, 12.5m);
        var dto = new CompraCxpAbonoUpsertDto
        {
            Monto = 100m,
            MetodoPago = MetodoPagoCompra.Efectivo,
            CuentaContableId = cuenta.Value,
            Retenciones = new List<RetencionAplicadaDto> { ret }
        };

        var r = await _service!.RegistrarAbonoAsync(cxpId, dto, "tester");

        Assert.Equal(12.5m, r.Retenido);
        Assert.Equal(0, await ContarPartidasAsync("COMPRAS", "CXP-ABO1", cxpId));   // sin partida
        var fiscal = await LeerRetencionFiscalAsync(cxpId, 1);
        Assert.NotNull(fiscal);
        Assert.Equal(OrigenRetencion.Compra, fiscal!.Value.Origen);   // el libro fiscal se escribe igual
        Assert.Equal(12.5m, fiscal.Value.TotalRetenido);
        Assert.Null(fiscal.Value.PartidaId);                          // sin partida (compras apagado)
    }

    [SkippableFact]
    public async Task AnularAbono_ConRetencion_MarcaElRegistroFiscalAnuladoYReabre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConfigurarCuentasAsync(_codProveedor, _articuloA);
        await SembrarAsientoComprasAsync();
        await EncenderModuloAsync("activo_compras");

        var cuenta = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuenta is null, "El tenant de prueba no tiene cuenta contable posteable");
        var retencionId = await PrimeraRetencionIdAsync();
        Skip.If(retencionId is null, "El mirror no tiene catálogo de retenciones (cfg_retencion)");

        var (cxpId, monto) = await CrearCxpAsync();
        var ret = Retencion(retencionId.Value, cuenta!.Value, 200m, 12.5m);
        var dto = new CompraCxpAbonoUpsertDto
        {
            Monto = 200m,
            MetodoPago = MetodoPagoCompra.Efectivo,
            CuentaContableId = cuenta.Value,
            Retenciones = new List<RetencionAplicadaDto> { ret }
        };
        await _service!.RegistrarAbonoAsync(cxpId, dto, "tester");

        Assert.True(await _service.AnularAbonoAsync(cxpId, 1, "pago mal aplicado", "tester"));

        var fiscal = await LeerRetencionFiscalAsync(cxpId, 1);
        Assert.NotNull(fiscal);
        Assert.Equal(EstadoRetencion.Anulada, fiscal!.Value.EstadoId);   // registro fiscal anulado

        var cxp = await _context!.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.id == cxpId);
        Assert.Equal(monto, cxp.saldo);                                  // reabre por el bruto
        Assert.Equal(EstadoCompraCxp.Pendiente, cxp.estado_id);
    }

    [SkippableFact]
    public async Task Declaracion_IncluyeLaRetencionDeCompra_ConNombreDelProveedor()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuenta = await ResolverCuentaContablePosteableAsync();
        Skip.If(cuenta is null, "El tenant de prueba no tiene cuenta contable posteable");
        var retencionId = await PrimeraRetencionIdAsync();
        Skip.If(retencionId is null, "El mirror no tiene catálogo de retenciones (cfg_retencion)");

        var (cxpId, _) = await CrearCxpAsync();
        var ret = Retencion(retencionId.Value, cuenta!.Value, 100m, 12.5m);
        var dto = new CompraCxpAbonoUpsertDto
        {
            Monto = 100m,
            MetodoPago = MetodoPagoCompra.Efectivo,
            CuentaContableId = cuenta.Value,
            Retenciones = new List<RetencionAplicadaDto> { ret }
        };
        await _service!.RegistrarAbonoAsync(cxpId, dto, "tester");

        var cxpProv = await _context!.alm_compra_cxps.AsNoTracking().Where(c => c.id == cxpId).Select(c => c.proveedor).FirstAsync();

        // El servicio de consulta (constancia/declaración) resuelve el nombre desde la CxP cuando origen=compra.
        var registro = new RetencionRegistroService(_context, new TestCurrentCompanyService(CompanyId));
        var lineas = await registro.BuscarDeclaracionAsync(new RetencionDeclaracionFilterDto());
        var mia = lineas.FirstOrDefault(l => l.Origen == OrigenRetencion.Compra && l.CodProveedor == _codProveedor && l.MontoRetenido == 12.5m);

        Assert.NotNull(mia);
        Assert.Null(mia!.NumeroOrden);                 // compras no lleva compromiso
        Assert.Equal(cxpProv, mia.NombreProveedor);    // nombre resuelto desde la CxP
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static RetencionAplicadaDto Retencion(int retencionId, long cuentaId, decimal baseImp, decimal pct) => new()
    {
        RetencionId = retencionId,
        CuentaId = cuentaId,
        Base = baseImp,
        Porcentaje = pct,
        Monto = Math.Round(baseImp * pct / 100m, 2, MidpointRounding.AwayFromZero)
    };

    private async Task<int?> PrimeraRetencionIdAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT id FROM public.cfg_retencion WHERE activo = TRUE ORDER BY id LIMIT 1;";
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? null : Convert.ToInt32(v);
    }

    private async Task<(short Origen, int? CxpId, int? NumeroOrden, decimal TotalRetenido, short EstadoId, long? PartidaId, int DtlCount)?>
        LeerRetencionFiscalAsync(int cxpId, int numeroAbono)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT h.origen, h.cxp_id, h.numero_orden, h.total_retenido, h.estado_id, h.partida_id,
       (SELECT count(*) FROM public.prv_retencion_dtl d
         WHERE d.company_id = h.company_id AND d.retencion_hdr_id = h.retencion_hdr_id)
  FROM public.prv_retencion_hdr h
 WHERE h.company_id = @c AND h.origen = @origen AND h.cxp_id = @cxp AND h.numero_abono = @num
 LIMIT 1;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("origen", (short)OrigenRetencion.Compra);
        cmd.Parameters.AddWithValue("cxp", cxpId);
        cmd.Parameters.AddWithValue("num", numeroAbono);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (
            r.GetInt16(0),
            r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
            r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            r.GetDecimal(3),
            r.GetInt16(4),
            r.IsDBNull(5) ? (long?)null : r.GetInt64(5),
            Convert.ToInt32(r.GetValue(6)));
    }

    private static CompraCxpAbonoUpsertDto Pago(decimal monto, string metodo) =>
        new() { Monto = monto, MetodoPago = metodo, Fecha = DateOnly.FromDateTime(DateTime.Today) };

    private async Task<(int CxpId, decimal Monto)> CrearCxpAsync(decimal cantidad = 10m, decimal costo = 25m)
    {
        var dto = new RecepcionCompraDto
        {
            CodProveedor = _codProveedor,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            FechaFactura = DateOnly.FromDateTime(DateTime.Today),
            BodegaId = _bodegaId,
            Moneda = MonedaCompra.Lempira,
            TasaCambio = 1m,
            Detalles = new List<RecepcionCompraDetalleDto>
            {
                new() { ArticuloId = _articuloA, Cantidad = cantidad, CostoUnitario = costo, Descripcion = "Renglón CxP" }
            }
        };
        var r = await _recepciones!.CrearAsync(dto, "tester");
        var cxp = await _context!.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.compra_hdr_id == r.Id);
        return (cxp.id, cxp.monto);
    }

    private async Task SembrarRelacionAsync(int articuloId, string codigoUpc)
    {
        _context!.alm_articulo_proveedors.Add(new alm_articulo_proveedor
        {
            articulo_id = articuloId,
            cod_proveedor = _codProveedor,
            codigo_upc = codigoUpc,
            costo = 25m,
            principal = false,
            activo = true
        });
        await _context.SaveChangesAsync();
    }

    private async Task<long?> ResolverCuentaContablePosteableAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT account_id FROM public.con_plan_cuentas
 WHERE company_id = @c AND allows_posting = TRUE AND status = 'ACTIVE'
 ORDER BY account_id LIMIT 1;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? null : Convert.ToInt64(v);
    }

    private async Task<int> ContarPartidasAsync(string module, string docType, long documentId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT count(*) FROM public.con_partida_hdr
 WHERE company_id = @c AND module = @m AND document_type = @dt AND document_id = @id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("m", module);
        cmd.Parameters.AddWithValue("dt", docType);
        cmd.Parameters.AddWithValue("id", documentId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<(int Lineas, decimal Debe, decimal Haber)?> ResumenPartidaAsync(string module, string docType, long documentId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT count(d.*), COALESCE(sum(d.debit_amount), 0), COALESCE(sum(d.credit_amount), 0)
  FROM public.con_partida_hdr h
  JOIN public.con_partida_dtl d ON d.company_id = h.company_id AND d.poliza_id = h.poliza_id
 WHERE h.company_id = @c AND h.module = @m AND h.document_type = @dt AND h.document_id = @id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("m", module);
        cmd.Parameters.AddWithValue("dt", docType);
        cmd.Parameters.AddWithValue("id", documentId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return (Convert.ToInt32(r.GetValue(0)), Convert.ToDecimal(r.GetValue(1)), Convert.ToDecimal(r.GetValue(2)));
    }

    private async Task ApagarIntegracionContableAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "UPDATE public.con_integracion_config SET activo_compras = false WHERE company_id = @c;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SembrarAsientoComprasAsync()
    {
        // El motor exige una fila con_integracion_asiento del módulo COMPRAS (diario/tipo). Se copia
        // de ALMACEN como default para el test (en producción lo configura el contador).
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id)
SELECT @c, 'COMPRAS', a.journal_id, a.type_id
  FROM public.con_integracion_asiento a
 WHERE a.company_id = @c AND a.module = 'ALMACEN'
   AND NOT EXISTS (SELECT 1 FROM public.con_integracion_asiento x WHERE x.company_id = @c AND x.module = 'COMPRAS');";
        cmd.Parameters.AddWithValue("c", CompanyId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task EncenderModuloAsync(string columnaFlag)
    {
        // columnaFlag es un literal controlado por el test ('activo_almacen' / 'activo_proveedores').
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = $"UPDATE public.con_integracion_config SET {columnaFlag} = TRUE WHERE company_id = @c;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ConfigurarCuentasAsync(string codProveedor, int articuloId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
UPDATE public.prv_proveedores SET cuenta_contable = '11101020000' WHERE company_id = @c AND cod_proveedor = @prov;
UPDATE public.alm_tipo_articulo t SET cuenta_inventario = '11101010000'
 WHERE t.company_id = @c AND t.id = (SELECT a.tipo_articulo_id FROM public.alm_articulo a WHERE a.id = @art);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("prov", codProveedor);
        cmd.Parameters.AddWithValue("art", articuloId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> SembrarCuentaBancariaAsync(long cuentaContableId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, tipo, currency_code, numero_cuenta, cont_account_id, activo, estado, proximo_cheque, cheque_maximo)
VALUES (@c, 'ZCXPBK', 'Cuenta CxP Test', 'AHORRO', 'LPS', 'ZCXPBK', @cta, TRUE, 'ACTIVE', 1, 0)
RETURNING banco_cuenta_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("cta", cuentaContableId);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
