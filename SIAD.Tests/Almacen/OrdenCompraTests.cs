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
/// Órdenes de compra (alm_orden_compra): correlativo por empresa, cálculo de totales,
/// la regla dura del código de proveedor y la máquina de estados.
/// <para>
/// Los datos de apoyo (proveedor y artículos) se eligen de la base y la relación
/// artículo↔proveedor se siembra DENTRO de la transacción del test, que hace ROLLBACK:
/// nada queda escrito.
/// </para>
/// </summary>
[Collection("Postgres")]
public class OrdenCompraTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private OrdenCompraService? _service;

    private string _codProveedor = string.Empty;
    private int _articuloA;
    private int _articuloB;

    private const string UpcA = "TEST-UPC-A";
    private const string UpcB = "TEST-UPC-B";

    public OrdenCompraTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (!Fixture.Available)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _service = new OrdenCompraService(_context, new TestCurrentCompanyService(CompanyId));

        // Proveedor activo cualquiera de la empresa de pruebas.
        _codProveedor = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == CompanyId && (p.status == null || p.status == true))
            .OrderBy(p => p.cod_proveedor)
            .Select(p => p.cod_proveedor)
            .FirstAsync();

        // Dos artículos activos.
        var articulos = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.activo)
            .OrderBy(a => a.id)
            .Select(a => a.id)
            .Take(2)
            .ToListAsync();

        _articuloA = articulos[0];
        _articuloB = articulos[1];

        // Estado de partida conocido: A con código, B sin relación alguna.
        await SembrarRelacionAsync(_articuloA, UpcA);
        await BorrarRelacionAsync(_articuloB);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Correlativo y totales ────────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_AsignaCorrelativoYCalculaTotales()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(descuento: 10m, impuesto: 38.25m), "tester");

        Assert.True(creada.Numero > 0);
        Assert.Equal(EstadoOrdenCompra.Borrador, creada.Estado);
        Assert.Single(creada.Detalles);

        // 10 × 25.50 = 255.00 · descuento 10% = 25.50 · ISV 38.25
        Assert.Equal(255.00m, creada.SubTotal);
        Assert.Equal(38.25m, creada.Impuesto);
        Assert.Equal(267.75m, creada.Total);   // 255 − 25.50 + 38.25
    }

    [SkippableFact]
    public async Task Crear_DosOrdenes_ElCorrelativoAvanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var primera = await _service!.CrearAsync(NuevaOrden(), "tester");
        var segunda = await _service.CrearAsync(NuevaOrden(), "tester");

        Assert.Equal(primera.Numero + 1, segunda.Numero);
    }

    [SkippableFact]
    public async Task Crear_SinCalculaIsv_IgnoraElImpuestoDelRenglon()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaOrden(impuesto: 99m);
        dto.CalculaIsv = false;

        var creada = await _service!.CrearAsync(dto, "tester");

        Assert.Equal(0m, creada.Impuesto);
        Assert.Equal(0m, creada.Detalles.Single().Impuesto);
        Assert.Equal(255.00m, creada.Total);
    }

    // ── Regla dura del código de proveedor ───────────────────────────────────

    [SkippableFact]
    public async Task Crear_ArticuloSinRelacionConElProveedor_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaOrden();
        dto.Detalles.Single().ArticuloId = _articuloB;   // sin relación sembrada

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(dto, "tester"));
        Assert.Contains("sin código para el proveedor", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_RelacionActivaPeroSinCodigoUpc_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Relación ACTIVA pero con el código en blanco: no habilita la compra.
        await SembrarRelacionAsync(_articuloB, codigoUpc: null);

        var dto = NuevaOrden();
        dto.Detalles.Single().ArticuloId = _articuloB;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    [SkippableFact]
    public async Task Crear_RelacionDeshabilitada_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SembrarRelacionAsync(_articuloB, UpcB, activo: false);

        var dto = NuevaOrden();
        dto.Detalles.Single().ArticuloId = _articuloB;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    [SkippableFact]
    public async Task Crear_GuardaElCodigoDelCatalogo_NoElQueMandaElCliente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaOrden();
        dto.Detalles.Single().CodigoUpc = "INVENTADO-POR-EL-CLIENTE";

        var creada = await _service!.CrearAsync(dto, "tester");

        Assert.Equal(UpcA, creada.Detalles.Single().CodigoUpc);
    }

    [SkippableFact]
    public async Task BuscarArticulosProveedor_SoloDevuelveLosQueTienenCodigo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // B queda con relación activa pero SIN código: no debe aparecer.
        await SembrarRelacionAsync(_articuloB, codigoUpc: null);

        var lista = await _service!.BuscarArticulosProveedorAsync(_codProveedor, null);

        Assert.Contains(lista, x => x.ArticuloId == _articuloA);
        Assert.DoesNotContain(lista, x => x.ArticuloId == _articuloB);
        Assert.All(lista, x => Assert.False(string.IsNullOrWhiteSpace(x.CodigoUpc)));
    }

    // ── Cotas de rango ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_DescuentoMayorA100_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Sin la guarda esto persistía un total NEGATIVO y la orden se podía aprobar.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(NuevaOrden(descuento: 150m), "tester"));
        Assert.Contains("entre 0 y 100", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_DescuentoNegativo_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(NuevaOrden(descuento: -5m), "tester"));
    }

    [SkippableFact]
    public async Task Crear_CantidadFueraDeRango_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaOrden();
        dto.Detalles.Single().CantidadPedida = 99_999_999_999m;   // no cabe en NUMERIC(14,4)

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    [SkippableFact]
    public async Task Crear_SinRenglones_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaOrden();
        dto.Detalles.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    [SkippableFact]
    public async Task Crear_ProveedorInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevaOrden();
        dto.CodProveedor = "ZZZZ-NO-EXISTE";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "tester"));
    }

    // ── Máquina de estados ───────────────────────────────────────────────────

    [SkippableFact]
    public async Task Aprobar_PasaAAprobadaYEstampaUsuario()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(), "tester");

        Assert.True(await _service.AprobarAsync(creada.Id, "jefe"));

        var recargada = await _service.GetByIdAsync(creada.Id);
        Assert.Equal(EstadoOrdenCompra.Aprobada, recargada!.Estado);
        Assert.Equal("jefe", recargada.AprobadoPor);
        Assert.NotNull(recargada.FechaAprobacion);
    }

    [SkippableFact]
    public async Task Aprobar_DosVeces_EsIdempotente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(), "tester");

        Assert.True(await _service.AprobarAsync(creada.Id, "jefe"));
        Assert.True(await _service.AprobarAsync(creada.Id, "jefe"));

        Assert.Equal(EstadoOrdenCompra.Aprobada, (await _service.GetByIdAsync(creada.Id))!.Estado);
    }

    [SkippableFact]
    public async Task Aprobar_Inexistente_DevuelveFalse()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        Assert.False(await _service!.AprobarAsync(-1, "jefe"));
    }

    [SkippableFact]
    public async Task Actualizar_OrdenAprobada_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(), "tester");
        await _service.AprobarAsync(creada.Id, "jefe");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ActualizarAsync(creada.Id, NuevaOrden(), "tester"));
    }

    [SkippableFact]
    public async Task Actualizar_EnBorrador_ReemplazaRenglonesYRecalcula()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(), "tester");

        var cambios = NuevaOrden();
        cambios.Detalles.Single().CantidadPedida = 4m;      // 4 × 25.50 = 102.00

        var actualizada = await _service.ActualizarAsync(creada.Id, cambios, "tester");

        Assert.Single(actualizada.Detalles);
        Assert.Equal(4m, actualizada.Detalles.Single().CantidadPedida);
        Assert.Equal(102.00m, actualizada.SubTotal);
        Assert.Equal(creada.Numero, actualizada.Numero);    // el número no cambia
    }

    [SkippableFact]
    public async Task Anular_EnBorrador_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(), "tester");

        Assert.True(await _service.AnularAsync(creada.Id, "tester"));
        Assert.Equal(EstadoOrdenCompra.Anulada, (await _service.GetByIdAsync(creada.Id))!.Estado);
    }

    [SkippableFact]
    public async Task Anular_ConRenglonYaRecibido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.CrearAsync(NuevaOrden(), "tester");
        await _service.AprobarAsync(creada.Id, "jefe");

        // Simula una recepción parcial sobre el renglón.
        var renglon = await _context!.alm_orden_compra_detalles
            .FirstAsync(d => d.orden_compra_id == creada.Id);
        renglon.cantidad_aplicada = 3m;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AnularAsync(creada.Id, "tester"));
    }

    [SkippableFact]
    public async Task GetById_Inexistente_DevuelveNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        Assert.Null(await _service!.GetByIdAsync(-1));
    }

    [SkippableFact]
    public async Task Get_FiltraPorEstado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var borrador = await _service!.CrearAsync(NuevaOrden(), "tester");
        var aprobada = await _service.CrearAsync(NuevaOrden(), "tester");
        await _service.AprobarAsync(aprobada.Id, "jefe");

        var soloAprobadas = await _service.GetAsync(new OrdenCompraFilterDto { Estado = EstadoOrdenCompra.Aprobada });

        Assert.Contains(soloAprobadas, o => o.Id == aprobada.Id);
        Assert.DoesNotContain(soloAprobadas, o => o.Id == borrador.Id);
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    /// <summary>Orden mínima válida: un renglón de 10 × 25.50 sobre el artículo A.</summary>
    private OrdenCompraDto NuevaOrden(decimal descuento = 0m, decimal impuesto = 0m) => new()
    {
        CodProveedor = _codProveedor,
        Fecha = DateOnly.FromDateTime(DateTime.Today),
        CalculaIsv = true,
        Descuento = descuento,
        OtrosGastos = 0m,
        Observaciones = "Orden de prueba",
        Detalles = new List<OrdenCompraDetalleDto>
        {
            new()
            {
                ArticuloId = _articuloA,
                CantidadPedida = 10m,
                CostoUnitario = 25.50m,
                Impuesto = impuesto,
                Descripcion = "Renglón de prueba"
            }
        }
    };

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

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
