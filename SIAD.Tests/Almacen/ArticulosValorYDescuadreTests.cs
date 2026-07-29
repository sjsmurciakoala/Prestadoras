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
/// (c) GetByIdAsync excluye ubicaciones inactivas de la existencia del form;
/// (d) la cuenta contable se valida contra el plan de cuentas al crear, y al editar solo
///     si CAMBIA (los históricos SIMAFI con cuenta inválida siguen editables).
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
            _service = new ArticulosService(_context, company);
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

    /// <summary>Cuenta del plan (con_plan_cuentas); company_id lo estampa el contexto.</summary>
    private async Task<string> SeedCuentaAsync(string code, bool allowsPosting = true, string status = "ACTIVE")
    {
        var c = new con_plan_cuenta
        {
            code = code,
            name = $"Cuenta {code}",
            account_type = "ASSET",
            status = status,
            level = 1,
            allows_posting = allowsPosting,
            created_at = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            created_by = "tester"
        };
        _context!.con_plan_cuentas.Add(c);
        await _context.SaveChangesAsync();
        return code;
    }

    /// <summary>Crea un artículo por el SERVICIO con dos bodegas (10 + 5) y valor unitario dado.</summary>
    private async Task<int> CrearArticuloAsync(string codigo, decimal valorUnitario, string? cuenta = null)
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
            CuentaContable = cuenta,
            Ubicaciones =
            {
                new ArticuloUbicacionDto { BodegaId = bodegaA, Existencia = 10 },
                new ArticuloUbicacionDto { BodegaId = bodegaB, Existencia = 5 }
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
        var entity = await _context!.alm_articulos.FirstAsync(a => a.id == id);
        entity.existencia = 20m; // las bodegas suman 15
        await _context.SaveChangesAsync();

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

    // ── (d) Cuenta contable contra el plan de cuentas ────────────────────────

    [SkippableFact]
    public async Task Create_CuentaContableValida_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuenta = await SeedCuentaAsync("9910000101");
        var id = await CrearArticuloAsync("ZZF4C1", valorUnitario: 0m, cuenta: cuenta);
        Assert.True(id > 0);
    }

    [SkippableFact]
    public async Task Create_CuentaContableInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearArticuloAsync("ZZF4C2", valorUnitario: 0m, cuenta: "9909090909"));
        Assert.Contains("no existe en el plan", ex.Message);
    }

    [SkippableFact]
    public async Task Create_CuentaContableNoImputable_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuenta = await SeedCuentaAsync("9910000202", allowsPosting: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearArticuloAsync("ZZF4C3", valorUnitario: 0m, cuenta: cuenta));
        Assert.Contains("no permite movimientos", ex.Message);
    }

    [SkippableFact]
    public async Task Create_CuentaContableInactiva_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuenta = await SeedCuentaAsync("9910000303", status: "INACTIVE");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearArticuloAsync("ZZF4C4", valorUnitario: 0m, cuenta: cuenta));
        Assert.Contains("inactiva", ex.Message);
    }

    /// <summary>
    /// El artículo histórico SIMAFI con una cuenta que ya no existe en el plan debe seguir
    /// siendo editable mientras NO se cambie la cuenta: un re-save del form (que siempre
    /// reenvía CuentaContable) no puede reventar.
    /// </summary>
    [SkippableFact]
    public async Task Update_CuentaHistoricaInvalidaSinCambiarla_NoLanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoAsync("ZZF4HT");
        var art = new alm_articulo
        {
            codigo_articulo = "ZZF4H1",
            descripcion = "Histórico con cuenta inválida",
            tipo_articulo_id = tipo,
            cuenta_contable = "99NOEXISTE"
        };
        _context!.alm_articulos.Add(art);
        await _context.SaveChangesAsync();

        var dto = await _service!.GetByIdAsync(art.id);
        Assert.NotNull(dto);

        dto!.Descripcion = "Descripción corregida";
        var actualizado = await _service.UpdateAsync(art.id, dto, "tester");

        Assert.Equal("Descripción corregida", actualizado.Descripcion);
    }

    [SkippableFact]
    public async Task Update_CambiandoACuentaInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoAsync("ZZF4JT");
        var art = new alm_articulo
        {
            codigo_articulo = "ZZF4J1",
            descripcion = "Artículo",
            tipo_articulo_id = tipo
        };
        _context!.alm_articulos.Add(art);
        await _context.SaveChangesAsync();

        var dto = await _service!.GetByIdAsync(art.id);
        dto!.CuentaContable = "9909090908"; // no existe en el plan

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(art.id, dto, "tester"));
    }

    /// <summary>
    /// Reenviar la MISMA cuenta con separadores (máscara de presentación) no es un cambio:
    /// no dispara la validación y el valor almacenado se conserva tal cual.
    /// </summary>
    [SkippableFact]
    public async Task Update_MismaCuentaConSeparadores_NoValidaNiReescribe()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoAsync("ZZF4KT");
        var art = new alm_articulo
        {
            codigo_articulo = "ZZF4K1",
            descripcion = "Histórico",
            tipo_articulo_id = tipo,
            cuenta_contable = "99887766" // NO existe en el plan (histórico inválido)
        };
        _context!.alm_articulos.Add(art);
        await _context.SaveChangesAsync();

        var dto = await _service!.GetByIdAsync(art.id);
        dto!.CuentaContable = "99-88-77-66"; // misma cuenta, con guiones

        await _service.UpdateAsync(art.id, dto, "tester"); // no lanza

        var recargado = await _context.alm_articulos.AsNoTracking().FirstAsync(a => a.id == art.id);
        Assert.Equal("99887766", recargado.cuenta_contable);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
