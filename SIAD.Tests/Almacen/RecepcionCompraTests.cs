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
using SIAD.Services.Aprobaciones;
using SIAD.Services.Almacen;
using SIAD.Services.Presupuesto;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Recepción de compras (alm_compra_hdr + líneas de alm_compra + posteo al kardex).
/// <para>
/// Lo que se vigila: que la factura entre completa o no entre (una transacción), que el
/// inventario suba por la vía auditada, que la O/C se descargue sin poder recibir de más —y
/// que admita VARIAS facturas—, y que el ISV capitalice al costo solo cuando corresponde.
/// </para>
/// <para>
/// Los datos de apoyo (proveedor, artículos, bodega) se eligen de la base y las relaciones se
/// siembran DENTRO de la transacción del test, que hace ROLLBACK: nada queda escrito.
/// </para>
/// </summary>
[Collection("Postgres")]
public class RecepcionCompraTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RecepcionCompraService? _service;
    private OrdenCompraService? _ordenes;
    private InventarioPostingService? _motor;

    private string _codProveedor = string.Empty;
    private int _articuloA;
    private int _articuloB;
    private int _bodegaId;

    private const string UpcA = "TEST-REC-A";
    private const string UpcB = "TEST-REC-B";

    public RecepcionCompraTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Estos tests prueban la MECÁNICA de compras (kardex, CxP, correlativo, anulación),
        // no el presupuesto. cfg_presupuesto_control es estado GLOBAL de la base de prueba:
        // si quedó encendido por una demo o un piloto, estas pruebas fallarían con
        // «excede el presupuesto disponible» sin tener nada que ver con eso.
        await DesactivarControlPresupuestarioAsync();

        if (!Fixture.Available)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        var company = new TestCurrentCompanyService(CompanyId);
        _context = new SiadDbContext(options, company);
        _context.Database.UseTransaction(Transaction);

        _motor = new InventarioPostingService(_context, company, new ArticuloRollupService(_context));
        _service = new RecepcionCompraService(_context, company, _motor, new TasaIsvArticuloResolver(_context),
            new PresupuestoCompromisoService(_context, company));
        _ordenes = new OrdenCompraService(_context, company, new TasaIsvArticuloResolver(_context),
            new PresupuestoCompromisoService(_context, company),
            new AprobacionService(_context, company, new TestCurrentUserService()),
            new AprobacionNotificadorNoop());

        _codProveedor = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && (p.status == null || p.status == true))
            .OrderBy(p => p.cod_proveedor)
            .Select(p => p.cod_proveedor)
            .FirstAsync();

        var articulos = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.activo)
            .OrderBy(a => a.id)
            .Select(a => a.id)
            .Take(2)
            .ToListAsync();
        _articuloA = articulos[0];
        _articuloB = articulos[1];

        // Bodega propia del test: así la existencia de partida es 0 y las cuentas son claras.
        var bodega = new alm_bodega { codigo = "ZREC1", nombre = "Bodega recepción", activo = true };
        _context.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync();
        _bodegaId = bodega.id;

        await SembrarRelacionAsync(_articuloA, UpcA);
        await SembrarRelacionAsync(_articuloB, UpcB);

        // Estos tests vigilan la MECÁNICA de la recepción (kardex, O/C, ISV), no la contabilidad.
        // El asiento de la compra es del módulo COMPRAS (gated activo_compras); se apaga aquí, dentro
        // de la transacción del test, para aislarlos. La contabilidad tiene sus propios tests en
        // CompraCxpTests.
        await ApagarIntegracionContableAsync();
    }

    private async Task ApagarIntegracionContableAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "UPDATE public.con_integracion_config SET activo_compras = false WHERE company_id = @c;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        await cmd.ExecuteNonQueryAsync();
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Compra directa ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_CompraDirecta_RegistraFacturaYSubeElInventario()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 10m, costo: 25m), "tester");

        Assert.True(r.Numero > 0);
        Assert.Equal(EstadoRecepcionCompra.Registrada, r.Estado);
        Assert.True(r.Posteado);
        Assert.Single(r.Detalles);
        Assert.Equal(250m, r.SubTotal);

        // El código de proveedor del renglón sale del catálogo, no del cliente.
        Assert.Equal(UpcA, r.Detalles[0].CodigoUpc);

        // La línea quedó como documento SIAD posteado.
        var linea = await _context!.alm_compras.AsNoTracking().FirstAsync(l => l.id == r.Detalles[0].Id);
        Assert.Equal(OrigenDocumento.Siad, linea.origen);
        Assert.True(linea.posteado);
        Assert.NotNull(linea.uuid);
        Assert.Equal(r.Id, linea.compra_hdr_id);

        // Y el inventario subió con su asiento.
        var par = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(10m, par.existencia);

        var asiento = await _context.alm_kardexs.AsNoTracking()
            .FirstAsync(k => k.documento_tipo == TipoDocumentoInventario.Compra && k.documento_id == linea.id);
        Assert.Equal(10m, asiento.ingresos);
        Assert.Equal(_bodegaId, asiento.bodega_id);
    }

    [SkippableFact]
    public async Task Crear_ElCodigoDelArticuloSaleDelCatalogoNoDelCliente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var esperado = await _context!.alm_articulos.AsNoTracking()
            .Where(a => a.id == _articuloA).Select(a => a.codigo_articulo).FirstAsync();

        // El cliente no manda el código (o manda cualquier cosa): la línea guarda el del catálogo.
        var dto = NuevaFactura();
        dto.Detalles[0].CodigoArticulo = "BASURA-DEL-CLIENTE";

        var r = await _service!.CrearAsync(dto, "tester");

        Assert.Equal(esperado, r.Detalles[0].CodigoArticulo);

        var linea = await _context.alm_compras.AsNoTracking().FirstAsync(l => l.id == r.Detalles[0].Id);
        Assert.Equal(esperado, linea.codigo_articulo);
    }

    [SkippableFact]
    public async Task Crear_ElCorrelativoAvanzaPorEmpresa()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var primera = await _service!.CrearAsync(NuevaFactura(sar: "000-001-01-00000001"), "tester");
        var segunda = await _service.CrearAsync(NuevaFactura(sar: "000-001-01-00000002"), "tester");

        Assert.Equal(primera.Numero + 1, segunda.Numero);
    }

    [SkippableFact]
    public async Task Crear_MismoUuid_NoDuplicaLaFactura()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var uuid = Guid.NewGuid();
        var dto = NuevaFactura(cantidad: 4m, costo: 10m);
        dto.Uuid = uuid;

        var primera = await _service!.CrearAsync(dto, "tester");

        var reintento = NuevaFactura(cantidad: 4m, costo: 10m);
        reintento.Uuid = uuid;
        var segunda = await _service.CrearAsync(reintento, "tester");

        Assert.Equal(primera.Id, segunda.Id);
        Assert.Equal(1, await _context!.alm_compra_hdrs.CountAsync(h => h.uuid == uuid));

        // Lo que de verdad importa: la existencia no entró dos veces.
        var par = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(4m, par.existencia);
    }

    [SkippableFact]
    public async Task Crear_MismaFacturaDelMismoProveedor_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.CrearAsync(NuevaFactura(sar: "000-001-01-00000009"), "tester");

        // Documento distinto (otro uuid) pero misma factura del mismo proveedor: lo frena el
        // índice único parcial de la BD.
        await Assert.ThrowsAnyAsync<Exception>(
            () => _service.CrearAsync(NuevaFactura(sar: "000-001-01-00000009"), "tester"));
    }

    // ── Reglas de renglón ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_ArticuloSinCodigoDeProveedor_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await BorrarRelacionAsync(_articuloB);

        var dto = NuevaFactura();
        dto.Detalles[0].ArticuloId = _articuloB;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(dto, "tester"));
        Assert.Contains("sin código", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Crear_ConCostoCero_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(NuevaFactura(costo: 0m), "tester"));
        Assert.Contains("costo unitario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Crear_SinRenglones_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaFactura();
        dto.Detalles.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    [SkippableFact]
    public async Task Crear_ConBodegaInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaFactura();
        dto.BodegaId = 999_999;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
        Assert.Contains("bodega", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Contra orden de compra ───────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_ContraOrden_DescargaYDejaLaOrdenParcial()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 10m, costo: 25m);
        var renglon = orden.Detalles[0];

        var r = await _service!.CrearAsync(FacturaContraOrden(orden.Id, renglon.Id, cantidad: 4m, costo: 25m), "tester");

        Assert.Equal(orden.Id, r.OrdenCompraId);

        var detalle = await _context!.alm_orden_compra_detalles.AsNoTracking().FirstAsync(d => d.id == renglon.Id);
        Assert.Equal(4m, detalle.cantidad_aplicada);

        var cab = await _context.alm_orden_compras.AsNoTracking().FirstAsync(o => o.id == orden.Id);
        Assert.Equal(EstadoOrdenCompra.RecibidaParcial, cab.estado);
    }

    [SkippableFact]
    public async Task Crear_VariasFacturasContraLaMismaOrden_AcumulanYLaCierran()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // La regla del usuario: una O/C admite una o más facturas.
        var orden = await OrdenAprobadaAsync(cantidad: 10m, costo: 25m);
        var renglon = orden.Detalles[0];

        await _service!.CrearAsync(
            FacturaContraOrden(orden.Id, renglon.Id, cantidad: 6m, costo: 25m, sar: "000-001-01-00000101"), "tester");

        var pendientes = await _service.ObtenerPendientesOrdenAsync(orden.Id);
        Assert.Equal(4m, Assert.Single(pendientes).CantidadPendiente);

        // La orden sigue apareciendo como recibible mientras quede pendiente.
        var recibibles = await _service.ObtenerOrdenesRecibiblesAsync(_codProveedor);
        Assert.Contains(recibibles, o => o.Id == orden.Id);

        await _service.CrearAsync(
            FacturaContraOrden(orden.Id, renglon.Id, cantidad: 4m, costo: 25m, sar: "000-001-01-00000102"), "tester");

        var detalle = await _context!.alm_orden_compra_detalles.AsNoTracking().FirstAsync(d => d.id == renglon.Id);
        Assert.Equal(10m, detalle.cantidad_aplicada);

        var cab = await _context.alm_orden_compras.AsNoTracking().FirstAsync(o => o.id == orden.Id);
        Assert.Equal(EstadoOrdenCompra.Cerrada, cab.estado);

        // Ya cerrada: ni queda pendiente ni se ofrece para recibir.
        Assert.Empty(await _service.ObtenerPendientesOrdenAsync(orden.Id));
        Assert.DoesNotContain(await _service.ObtenerOrdenesRecibiblesAsync(_codProveedor), o => o.Id == orden.Id);

        // Y el inventario acumuló las dos facturas.
        var par = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(10m, par.existencia);
    }

    [SkippableFact]
    public async Task Crear_ContraOrden_MasDeLoPendiente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 10m, costo: 25m);
        var renglon = orden.Detalles[0];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(FacturaContraOrden(orden.Id, renglon.Id, cantidad: 11m, costo: 25m), "tester"));
        Assert.Contains("pendientes", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Y no quedó nada a medias: ni descarga ni existencia.
        var detalle = await _context!.alm_orden_compra_detalles.AsNoTracking().FirstAsync(d => d.id == renglon.Id);
        Assert.Equal(0m, detalle.cantidad_aplicada);
        Assert.False(await _context.alm_articulo_bodegas.AsNoTracking()
            .AnyAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId && u.existencia > 0m));
    }

    [SkippableFact]
    public async Task Crear_ContraOrden_SegundaFacturaQueExcedeElRemanente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 10m, costo: 25m);
        var renglon = orden.Detalles[0];

        await _service!.CrearAsync(
            FacturaContraOrden(orden.Id, renglon.Id, cantidad: 8m, costo: 25m, sar: "000-001-01-00000201"), "tester");

        // Quedan 2: pedir 3 en la segunda factura debe fallar.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CrearAsync(
                FacturaContraOrden(orden.Id, renglon.Id, cantidad: 3m, costo: 25m, sar: "000-001-01-00000202"), "tester"));
    }

    [SkippableFact]
    public async Task Crear_ContraOrdenEnBorrador_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await _ordenes!.CrearAsync(NuevaOrden(10m, 25m), "tester");   // queda en Borrador

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(
                FacturaContraOrden(orden.Id, orden.Detalles[0].Id, cantidad: 1m, costo: 25m), "tester"));
        Assert.Contains("aprobada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Crear_ContraOrdenDeOtroProveedor_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 5m, costo: 25m);

        var otro = await _context!.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && p.cod_proveedor != _codProveedor
                     && (p.status == null || p.status == true))
            .Select(p => p.cod_proveedor)
            .FirstAsync();

        var dto = FacturaContraOrden(orden.Id, orden.Detalles[0].Id, cantidad: 1m, costo: 25m);
        dto.CodProveedor = otro;

        // El artículo tampoco tiene código para ese proveedor, así que puede saltar cualquiera
        // de las dos reglas; lo que importa es que NO se registre.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    [SkippableFact]
    public async Task Crear_ContraOrden_ConRenglonAjeno_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var ordenA = await OrdenAprobadaAsync(cantidad: 5m, costo: 25m);
        var ordenB = await OrdenAprobadaAsync(cantidad: 5m, costo: 25m);

        // Factura contra la orden A pero descargando un renglón de la B.
        var dto = FacturaContraOrden(ordenA.Id, ordenB.Detalles[0].Id, cantidad: 1m, costo: 25m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
        Assert.Contains("no pertenece", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Crear_CompraDirectaConRenglonQueApuntaAUnaOrden_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 5m, costo: 25m);

        var dto = NuevaFactura();
        dto.OrdenCompraId = null;                                  // compra directa…
        dto.Detalles[0].OrdenDetalleId = orden.Detalles[0].Id;     // …pero el renglón dice descargar una O/C

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    // ── ISV ──────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_ConIsvCapitalizado_ElCostoDelKardexLoIncluye()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SembrarIsvDelArticuloAsync(_articuloA, 15m);

        // DetallarIsv = false ⇒ el ISV se capitaliza al costo.
        var dto = NuevaFactura(cantidad: 10m, costo: 100m);
        dto.DetallarIsv = false;

        var r = await _service!.CrearAsync(dto, "tester");

        Assert.Equal(1000m, r.SubTotal);
        Assert.Equal(150m, r.Impuesto);
        Assert.Equal(1150m, r.Total);
        Assert.Equal(115m, r.Detalles[0].CostoPosteado);   // 100 + 15 de ISV por unidad

        var par = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(115m, par.costo_promedio);
        Assert.Equal(115m, par.ultimo_costo);
    }

    [SkippableFact]
    public async Task Crear_ConIsvDetallado_ElCostoDelKardexNoLoIncluye()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SembrarIsvDelArticuloAsync(_articuloA, 15m);

        var dto = NuevaFactura(cantidad: 10m, costo: 100m);
        dto.DetallarIsv = true;      // el ISV va aparte: no capitaliza

        var r = await _service!.CrearAsync(dto, "tester");

        Assert.Equal(150m, r.Impuesto);           // se registra igual…
        Assert.Equal(100m, r.Detalles[0].CostoPosteado);   // …pero el costo del kardex es el puro

        var par = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(100m, par.costo_promedio);
    }

    /// <summary>
    /// Con descuento de cabecera el ISV grava la base NETA, y el costo que se capitaliza al
    /// kardex hereda ese ISV menor: si se paga menos impuesto, menos impuesto entra al costo.
    /// </summary>
    [SkippableFact]
    public async Task Crear_ConDescuento_ElIsvVaSobreLaBaseDescontada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SembrarIsvDelArticuloAsync(_articuloA, 15m);

        // 10 × 100 = 1,000 bruto · descuento 10% = 100 · base imponible 900.
        var dto = NuevaFactura(cantidad: 10m, costo: 100m, descuento: 10m);
        dto.DetallarIsv = false;      // capitaliza, para ver el efecto en el kardex

        var r = await _service!.CrearAsync(dto, "tester");

        Assert.Equal(1000m, r.SubTotal);          // el subtotal sigue siendo el bruto
        Assert.Equal(135m, r.Impuesto);           // 900 × 15% = 135 (no 150)
        Assert.Equal(1035m, r.Total);             // 1000 − 100 + 135
        Assert.Equal(113.50m, r.Detalles[0].CostoPosteado);   // 100 + 13.50 de ISV por unidad

        var par = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(113.50m, par.ultimo_costo);
    }

    [SkippableFact]
    public async Task Crear_ArticuloSinTasa_NoLlevaIsv()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await QuitarIsvDelArticuloAsync(_articuloA);

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 5m, costo: 20m), "tester");

        Assert.Equal(0m, r.Impuesto);
        Assert.Equal(100m, r.Total);
    }

    // ── Anulación ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Anular_RevierteElInventarioYDejaLaFacturaAnulada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 10m, costo: 25m), "tester");

        var par = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(10m, par.existencia);

        Assert.True(await _service.AnularAsync(r.Id, "capturada dos veces", "tester"));

        var anulada = await _service.GetByIdAsync(r.Id);
        Assert.Equal(EstadoRecepcionCompra.Anulada, anulada!.Estado);
        Assert.Contains("ANULADA", anulada.Observaciones!, StringComparison.Ordinal);
        Assert.Contains("capturada dos veces", anulada.Observaciones!, StringComparison.Ordinal);

        // La existencia volvió a su punto de partida…
        par = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(0m, par.existencia);

        // …y el kardex NO se borró: quedó el asiento de compra y su reversa.
        var lineaId = r.Detalles[0].Id;
        var compra = await _context.alm_kardexs.AsNoTracking()
            .FirstAsync(k => k.documento_tipo == TipoDocumentoInventario.Compra && k.documento_id == lineaId);
        var reversa = await _context.alm_kardexs.AsNoTracking()
            .FirstAsync(k => k.documento_tipo == TipoDocumentoInventario.Reversa && k.documento_id == compra.id);
        Assert.Equal(10m, reversa.salidas);
        Assert.Equal(0m, reversa.ingresos);
    }

    [SkippableFact]
    public async Task Anular_EsIdempotente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 6m, costo: 10m), "tester");

        Assert.True(await _service.AnularAsync(r.Id, null, "tester"));
        Assert.True(await _service.AnularAsync(r.Id, null, "tester"));

        // La segunda anulación no revierte otra vez: la existencia no se va a negativo.
        var par = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(0m, par.existencia);

        var reversas = await _context.alm_kardexs.CountAsync(k => k.documento_tipo == TipoDocumentoInventario.Reversa
                                                              && k.bodega_id == _bodegaId);
        Assert.Equal(1, reversas);
    }

    [SkippableFact]
    public async Task Anular_DevuelveLoRecibidoALaOrdenYLaReabre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 10m, costo: 25m);
        var renglon = orden.Detalles[0];

        var r = await _service!.CrearAsync(
            FacturaContraOrden(orden.Id, renglon.Id, cantidad: 10m, costo: 25m), "tester");

        var cerrada = await _context!.alm_orden_compras.AsNoTracking().FirstAsync(o => o.id == orden.Id);
        Assert.Equal(EstadoOrdenCompra.Cerrada, cerrada.estado);

        await _service.AnularAsync(r.Id, "el proveedor se llevó la mercadería", "tester");

        var detalle = await _context.alm_orden_compra_detalles.AsNoTracking().FirstAsync(d => d.id == renglon.Id);
        Assert.Equal(0m, detalle.cantidad_aplicada);

        // Sin nada aplicado, la orden vuelve a estar lista para recibirse.
        var reabierta = await _context.alm_orden_compras.AsNoTracking().FirstAsync(o => o.id == orden.Id);
        Assert.Equal(EstadoOrdenCompra.Aprobada, reabierta.estado);

        var pendientes = await _service.ObtenerPendientesOrdenAsync(orden.Id);
        Assert.Equal(10m, Assert.Single(pendientes).CantidadPendiente);
    }

    [SkippableFact]
    public async Task Anular_ConVariasFacturas_DejaLaOrdenEnParcial()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var orden = await OrdenAprobadaAsync(cantidad: 10m, costo: 25m);
        var renglon = orden.Detalles[0];

        var primera = await _service!.CrearAsync(
            FacturaContraOrden(orden.Id, renglon.Id, 6m, 25m, sar: "000-001-01-00000401"), "tester");
        await _service.CrearAsync(
            FacturaContraOrden(orden.Id, renglon.Id, 4m, 25m, sar: "000-001-01-00000402"), "tester");

        // Se anula sólo la primera: la segunda sigue vigente.
        await _service.AnularAsync(primera.Id, null, "tester");

        var detalle = await _context!.alm_orden_compra_detalles.AsNoTracking().FirstAsync(d => d.id == renglon.Id);
        Assert.Equal(4m, detalle.cantidad_aplicada);

        var oc = await _context.alm_orden_compras.AsNoTracking().FirstAsync(o => o.id == orden.Id);
        Assert.Equal(EstadoOrdenCompra.RecibidaParcial, oc.estado);

        var par = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        Assert.Equal(4m, par.existencia);
    }

    [SkippableFact]
    public async Task Anular_SiLaMercaderiaYaSalio_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 10m, costo: 25m), "tester");

        // Alguien consume 7 de los 10 recibidos (ajuste negativo por la vía auditada).
        var fila = await _context!.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.articulo_id == _articuloA && u.bodega_id == _bodegaId);
        await _motor!.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.AjusteNegativo,
            ArticuloBodegaId = fila.id,
            Cantidad = 7m,
            Fecha = new DateOnly(2026, 7, 31),
            DocumentoTipo = TipoDocumentoInventario.Ajuste,
            DocumentoId = 999_001
        }, "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AnularAsync(r.Id, null, "tester"));
        Assert.Contains("ya salió", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Y no quedó a medias: la factura sigue registrada y la existencia intacta.
        var sigue = await _service.GetByIdAsync(r.Id);
        Assert.Equal(EstadoRecepcionCompra.Registrada, sigue!.Estado);

        var par = await _context.alm_articulo_bodegas.AsNoTracking()
            .FirstAsync(u => u.id == fila.id);
        Assert.Equal(3m, par.existencia);
    }

    [SkippableFact]
    public async Task Anular_Inexistente_DevuelveFalse()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        Assert.False(await _service!.AnularAsync(999_999, null, "tester"));
    }

    [SkippableFact]
    public async Task Anular_LiberaLaFacturaParaRecapturarla()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const string sar = "000-001-01-00000501";
        var primera = await _service!.CrearAsync(NuevaFactura(cantidad: 5m, costo: 12m, sar: sar), "tester");
        await _service.AnularAsync(primera.Id, "mal capturada", "tester");

        // El índice único de factura por proveedor excluye las anuladas.
        var segunda = await _service.CrearAsync(NuevaFactura(cantidad: 5m, costo: 12m, sar: sar), "tester");
        Assert.NotEqual(primera.Id, segunda.Id);
        Assert.Equal(EstadoRecepcionCompra.Registrada, segunda.Estado);
    }

    // ── Consultas ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Get_FiltraPorProveedorYDevuelveLaFacturaCreada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaFactura(sar: "000-001-01-00000301"), "tester");

        var lista = await _service.GetAsync(new RecepcionCompraFilterDto { CodProveedor = _codProveedor });

        var fila = Assert.Single(lista, f => f.Id == creada.Id);
        Assert.Equal(creada.Numero, fila.Numero);
        Assert.Equal(1, fila.Renglones);
        Assert.True(fila.Posteado);
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    // ── Cuenta por pagar (Fase 1) ─────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_GeneraLaCuentaPorPagar_Contado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 10m, costo: 25m), "tester");

        var cxp = await _context!.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.compra_hdr_id == r.Id);
        Assert.Equal(r.Total, cxp.monto);
        Assert.Equal(r.Total, cxp.saldo);
        Assert.Equal(EstadoCompraCxp.Pendiente, cxp.estado_id);
        Assert.Equal(CondicionPagoCompra.Contado, cxp.condicion_pago);
        Assert.Equal(_codProveedor, cxp.cod_proveedor);
        // Contado sin término: vence en la fecha de la factura.
        Assert.Equal(cxp.fecha, cxp.fecha_vencimiento);
    }

    [SkippableFact]
    public async Task Crear_ConTerminoCredito_LaCxpEsCreditoYVenceSegunLosDias()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var termino = new alm_termino_pago { nombre = "TEST-CREDITO-30", dias = 30, activo = true };
        _context!.alm_termino_pagos.Add(termino);
        await _context.SaveChangesAsync();

        var dto = NuevaFactura(cantidad: 4m, costo: 50m);
        dto.TerminoPagoId = termino.id;

        var r = await _service!.CrearAsync(dto, "tester");

        var cxp = await _context.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.compra_hdr_id == r.Id);
        Assert.Equal(CondicionPagoCompra.Credito, cxp.condicion_pago);
        Assert.Equal(cxp.fecha.AddDays(30), cxp.fecha_vencimiento);
    }

    [SkippableFact]
    public async Task Anular_SinPagos_AnulaLaCuentaPorPagar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 10m, costo: 25m), "tester");
        Assert.True(await _service.AnularAsync(r.Id, "prueba", "tester"));

        var cxp = await _context!.alm_compra_cxps.AsNoTracking().FirstAsync(c => c.compra_hdr_id == r.Id);
        Assert.Equal(EstadoCompraCxp.Anulada, cxp.estado_id);
        Assert.Equal(0m, cxp.saldo);
    }

    [SkippableFact]
    public async Task Anular_ConPagoAplicado_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var r = await _service!.CrearAsync(NuevaFactura(cantidad: 10m, costo: 25m), "tester");
        var cxp = await _context!.alm_compra_cxps.FirstAsync(c => c.compra_hdr_id == r.Id);

        // Un abono vigente sobre la CxP: anular la factura debe frenarse.
        _context.alm_compra_cxp_abonos.Add(new alm_compra_cxp_abono
        {
            cxp_id = cxp.id,
            numero_abono = 1,
            fecha = DateOnly.FromDateTime(DateTime.Today),
            monto = 50m,
            estado = "V"
        });
        cxp.saldo = cxp.monto - 50m;
        cxp.estado_id = EstadoCompraCxp.Parcial;
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.AnularAsync(r.Id, null, "tester"));
        Assert.Contains("pagos aplicados", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private RecepcionCompraDto NuevaFactura(
        decimal cantidad = 10m, decimal costo = 25m, string? sar = null,
        decimal descuento = 0m) => new()
    {
        CodProveedor = _codProveedor,
        Fecha = DateOnly.FromDateTime(DateTime.Today),
        FechaFactura = DateOnly.FromDateTime(DateTime.Today),
        NumeroFacturaSar = sar,
        Descuento = descuento,
        BodegaId = _bodegaId,
        Moneda = MonedaCompra.Lempira,
        TasaCambio = 1m,
        Observaciones = "Factura de prueba",
        Detalles = new List<RecepcionCompraDetalleDto>
        {
            new()
            {
                ArticuloId = _articuloA,
                Cantidad = cantidad,
                CostoUnitario = costo,
                Descripcion = "Renglón de prueba"
            }
        }
    };

    private RecepcionCompraDto FacturaContraOrden(
        int ordenId, int ordenDetalleId, decimal cantidad, decimal costo, string? sar = null)
    {
        var dto = NuevaFactura(cantidad, costo, sar);
        dto.OrdenCompraId = ordenId;
        dto.Detalles[0].OrdenDetalleId = ordenDetalleId;
        return dto;
    }

    private OrdenCompraDto NuevaOrden(decimal cantidad, decimal costo) => new()
    {
        CodProveedor = _codProveedor,
        Fecha = DateOnly.FromDateTime(DateTime.Today),
        // Obligatoria desde el borrador (2026-08-14_alm_orden_compra_fecha_entrega.sql).
        FechaEntregaPactada = DateOnly.FromDateTime(DateTime.Today).AddDays(7),
        CalculaIsv = false,
        Detalles = new List<OrdenCompraDetalleDto>
        {
            new()
            {
                ArticuloId = _articuloA,
                CantidadPedida = cantidad,
                CostoUnitario = costo,
                Descripcion = "Renglón de prueba"
            }
        }
    };

    private async Task<OrdenCompraDto> OrdenAprobadaAsync(decimal cantidad, decimal costo)
    {
        var orden = await _ordenes!.CrearAsync(NuevaOrden(cantidad, costo), "tester");
        await _ordenes.AprobarAsync(orden.Id, "tester");
        return (await _ordenes.GetByIdAsync(orden.Id))!;
    }

    private async Task SembrarRelacionAsync(int articuloId, string? codigoUpc, bool activo = true)
    {
        await BorrarRelacionAsync(articuloId);

        _context!.alm_articulo_proveedors.Add(new alm_articulo_proveedor
        {
            articulo_id = articuloId,
            cod_proveedor = _codProveedor,
            codigo_upc = codigoUpc,
            costo = 25.50m,
            principal = false,
            activo = activo
        });
        await _context.SaveChangesAsync();
    }

    private async Task BorrarRelacionAsync(int articuloId)
    {
        var previas = await _context!.alm_articulo_proveedors
            .Where(p => p.articulo_id == articuloId && p.cod_proveedor == _codProveedor)
            .ToListAsync();
        if (previas.Count > 0)
        {
            _context.alm_articulo_proveedors.RemoveRange(previas);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Deja al artículo con un tipo cuya tasa de ISV es la del porcentaje pedido, creando el
    /// tipo si hace falta. Todo dentro de la transacción del test.
    /// </summary>
    private async Task SembrarIsvDelArticuloAsync(int articuloId, decimal porcentaje)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var tasaId = await _context!.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.porcentaje == porcentaje
                     && t.vigencia_desde <= hoy
                     && (t.vigencia_hasta == null || t.vigencia_hasta >= hoy))
            .Select(t => (int?)t.id)
            .FirstOrDefaultAsync();

        Skip.If(tasaId is null, $"El catálogo cfg_impuesto_tasa no tiene una tasa vigente del {porcentaje}%");

        var articulo = await _context.alm_articulos.FirstAsync(a => a.id == articuloId);
        var tipo = articulo.tipo_articulo_id is null
            ? null
            : await _context.alm_tipo_articulos.FirstOrDefaultAsync(t => t.id == articulo.tipo_articulo_id);

        if (tipo is null)
        {
            tipo = new alm_tipo_articulo { codigo = "ZREC", descripcion = "Tipo de prueba", activo = true };
            _context.alm_tipo_articulos.Add(tipo);
            await _context.SaveChangesAsync();
            articulo.tipo_articulo_id = tipo.id;
        }

        tipo.impuesto_tasa_id = tasaId;
        await _context.SaveChangesAsync();
    }

    private async Task QuitarIsvDelArticuloAsync(int articuloId)
    {
        var articulo = await _context!.alm_articulos.AsNoTracking().FirstAsync(a => a.id == articuloId);
        if (articulo.tipo_articulo_id is null) return;

        var tipo = await _context.alm_tipo_articulos.FirstAsync(t => t.id == articulo.tipo_articulo_id);
        tipo.impuesto_tasa_id = null;
        await _context.SaveChangesAsync();
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
