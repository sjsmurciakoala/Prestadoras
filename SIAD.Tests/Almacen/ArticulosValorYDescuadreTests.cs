using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Fase 4 del maestro de almacén (2026-07-29):
/// (a) ValorTotal del grid es DINERO (existencia × valor unitario) y cuadra con el KPI
///     ValorInventario por construcción;
/// (b) detector de descuadre cabecera vs Σ bodegas ACTIVAS (filtro ConDescuadre,
///     ExistenciaBodegas y contador del resumen);
/// (c) GetByIdAsync excluye ubicaciones inactivas de la existencia del form.
/// </summary>
[Collection("Postgres")]
public class ArticulosValorYDescuadreTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ArticulosService? _service;

    public ArticulosValorYDescuadreTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);

            var rollup = new ArticuloRollupService(_context);
            var motor = new InventarioPostingService(_context, company, rollup);
            var carga = new CargaInicialInventarioService(_context, company, motor);
            _service = new ArticulosService(_context, company, rollup, carga);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var b = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(b);
        await _context.SaveChangesAsync();
        return b.id;
    }

    private async Task<int> SeedTipoAsync(string codigo)
    {
        var t = new alm_tipo_articulo { codigo = codigo, nombre = $"Tipo {codigo}", activo = true, maneja_inventario = true };
        _context!.alm_tipo_articulos.Add(t);
        await _context.SaveChangesAsync();
        return t.id;
    }

    private async Task<int> SeedUnidadAsync(string codigo)
    {
        var cat = new alm_categoria_unidad { nombre = $"Cat {codigo}", activo = true };
        _context!.alm_categoria_unidads.Add(cat);
        await _context.SaveChangesAsync();

        var u = new alm_unidad_medida
        {
            codigo = codigo,
            nombre = $"Unidad {codigo}",
            categoria_id = cat.id,
            activo = true,
            factor_conversion = 1m
        };
        _context.alm_unidad_medidas.Add(u);
        await _context.SaveChangesAsync();
        return u.id;
    }

    /// <summary>
    /// Crea un artículo por el SERVICIO con dos bodegas (10 + 5) y valor unitario dado.
    /// Desde la Fase 6 la existencia inicial NO se escribe: el servicio la postea como
    /// carga inicial en el kardex, y por eso cada bodega lleva su costo de apertura.
    /// </summary>
    private async Task<int> CrearArticuloAsync(string codigo, decimal valorUnitario)
    {
        var bodegaA = await SeedBodegaAsync($"{codigo}A");
        var bodegaB = await SeedBodegaAsync($"{codigo}B");
        var tipo = await SeedTipoAsync($"T{codigo}");
        var unidad = await SeedUnidadAsync($"U{codigo}");

        var creado = await _service!.CreateAsync(new ArticuloEditDto
        {
            Codigo = codigo,
            Descripcion = $"Artículo {codigo}",
            TipoArticuloId = tipo,
            UnidadMedidaId = unidad,
            ValorUnitario = valorUnitario,
            Ubicaciones =
            {
                new ArticuloUbicacionDto { BodegaId = bodegaA, Existencia = 10, CostoApertura = 1m },
                new ArticuloUbicacionDto { BodegaId = bodegaB, Existencia = 5, CostoApertura = 1m }
            }
        }, "tester");

        return creado.Id!.Value;
    }

    // ── (a) ValorTotal en dinero, cuadrado con el KPI ────────────────────────

    [SkippableFact]
    public async Task SearchPaged_ValorTotalEsDinero_YCuadraConElKpi()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await CrearArticuloAsync("ZZF4V1", valorUnitario: 2.5m);
        var filtro = new ArticuloFilterDto { Search = "ZZF4V1" };

        var pagina = await _service!.SearchPagedAsync(filtro, 0, 10, null, false);
        var item = Assert.Single(pagina.Items);

        // 15 unidades × 2.50 = 37.50 de valor, no "15" (la vieja cantidad).
        Assert.Equal(15m, item.Existencia);
        Assert.Equal(37.5m, item.ValorTotal);
        Assert.Equal(15m, item.ExistenciaBodegas);
        Assert.False(item.Descuadrado);

        // La suma de la columna ES el KPI para el mismo universo filtrado.
        var resumen = await _service.GetResumenAsync(filtro);
        Assert.Equal(1, resumen.Total);
        Assert.Equal(37.5m, resumen.ValorInventario);
        Assert.Equal(0, resumen.ConDescuadre);
    }

    // ── (b) Detector de descuadre ────────────────────────────────────────────

    [SkippableFact]
    public async Task ConDescuadre_FiltraYCuentaElArticuloDesincronizado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var id = await CrearArticuloAsync("ZZF4D1", valorUnitario: 1m);

        // Desincronizar la cabecera a mano (simula el descuadre real: ediciones sin kardex).
        // Va por ExecuteUpdate y no por SaveChanges: la instancia rastreada quedó con el
        // xmin previo al rollup del alta (que también escribe con ExecuteUpdate), así que
        // un UPDATE con token de concurrencia no afectaría filas.
        await _context!.alm_articulos.Where(a => a.id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.existencia, 20m)); // las bodegas suman 15

        var filtro = new ArticuloFilterDto { Search = "ZZF4D1", ConDescuadre = true };
        var pagina = await _service!.SearchPagedAsync(filtro, 0, 10, null, false);
        var item = Assert.Single(pagina.Items);

        Assert.Equal(20m, item.Existencia);
        Assert.Equal(15m, item.ExistenciaBodegas);
        Assert.True(item.Descuadrado);

        var resumen = await _service.GetResumenAsync(new ArticuloFilterDto { Search = "ZZF4D1" });
        Assert.Equal(1, resumen.ConDescuadre);
    }

    [SkippableFact]
    public async Task ConDescuadre_NoTraeArticulosCuadrados()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await CrearArticuloAsync("ZZF4D2", valorUnitario: 1m); // cuadrado: cabecera = Σ bodegas

        var pagina = await _service!.SearchPagedAsync(
            new ArticuloFilterDto { Search = "ZZF4D2", ConDescuadre = true }, 0, 10, null, false);

        Assert.Empty(pagina.Items);
    }

    // ── (c) GetByIdAsync excluye ubicaciones inactivas ───────────────────────

    [SkippableFact]
    public async Task GetById_ExistenciaExcluyeUbicacionesInactivas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodegaA = await SeedBodegaAsync("ZZF4GA");
        var bodegaB = await SeedBodegaAsync("ZZF4GB");
        var tipo = await SeedTipoAsync("ZZF4GT");

        // Sembrado directo: una ubicación activa (10) y una deshabilitada con stock
        // remanente (5). El contrato del rollup: la cabecera solo cuenta las activas.
        var art = new alm_articulo
        {
            codigo_articulo = "ZZF4G1",
            descripcion = "Artículo con ubicación inactiva",
            tipo_articulo_id = tipo,
            existencia = 10m
        };
        art.ubicaciones.Add(new alm_articulo_bodega { bodega_id = bodegaA, existencia = 10m, activo = true, principal = true });
        art.ubicaciones.Add(new alm_articulo_bodega { bodega_id = bodegaB, existencia = 5m, activo = false });
        _context!.alm_articulos.Add(art);
        await _context.SaveChangesAsync();

        var dto = await _service!.GetByIdAsync(art.id);

        Assert.NotNull(dto);
        // Antes del fix devolvía 15 (sumaba también la inactiva) y el form contradecía al maestro.
        Assert.Equal(10m, dto!.Existencia);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
