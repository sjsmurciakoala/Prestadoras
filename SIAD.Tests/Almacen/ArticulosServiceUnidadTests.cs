using System;
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
/// Las tres unidades del artículo (medida, almacenaje, salida) deben tener categoría
/// (FK a alm_categoria_unidad) y pertenecer todas a la misma categoría.
/// </summary>
[Collection("Postgres")]
public class ArticulosServiceUnidadTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ArticulosService? _service;

    public ArticulosServiceUnidadTests(PostgresFixture fixture) : base(fixture)
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

    private async Task<int> SeedCategoriaAsync(string nombre)
    {
        var c = new alm_categoria_unidad { nombre = nombre, activo = true };
        _context!.alm_categoria_unidads.Add(c);
        await _context.SaveChangesAsync();
        return c.id;
    }

    private async Task<int> SeedUnidadAsync(string codigo, int? categoriaId)
    {
        var u = new alm_unidad_medida
        {
            codigo = codigo,
            nombre = $"Unidad {codigo}",
            categoria_id = categoriaId,
            activo = true,
            factor_conversion = 1m
        };
        _context!.alm_unidad_medidas.Add(u);
        await _context.SaveChangesAsync();
        return u.id;
    }

    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var b = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(b);
        await _context.SaveChangesAsync();
        return b.id;
    }

    private async Task<int> SeedTipoAsync(string codigo)
    {
        var t = new alm_tipo_articulo
        {
            codigo = codigo,
            nombre = $"Tipo {codigo}",
            activo = true,
            maneja_inventario = true
        };
        _context!.alm_tipo_articulos.Add(t);
        await _context.SaveChangesAsync();
        return t.id;
    }

    /// <summary>Tipo que NO maneja inventario (ej. Servicios): sin existencias, bodega ni kardex.</summary>
    private async Task<int> SeedTipoSinInventarioAsync(string codigo)
    {
        var t = new alm_tipo_articulo
        {
            codigo = codigo,
            nombre = $"Tipo {codigo}",
            activo = true,
            maneja_inventario = false
        };
        _context!.alm_tipo_articulos.Add(t);
        await _context.SaveChangesAsync();
        return t.id;
    }

    private static ArticuloEditDto NuevoArticulo(string codigo, int bodegaId, int tipoId, int? medida, int? almacenaje, int? salida)
        => new()
        {
            Codigo = codigo,
            Descripcion = $"Artículo {codigo}",
            TipoArticuloId = tipoId,
            UnidadMedidaId = medida,
            UnidadAlmacenajeId = almacenaje,
            UnidadSalidaId = salida,
            Ubicaciones = { new ArticuloUbicacionDto { BodegaId = bodegaId } }
        };

    [SkippableFact]
    public async Task Create_UnidadesMismaCategoria_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodega = await SeedBodegaAsync("UCB1");
        var tipo = await SeedTipoAsync("ZT1");
        var peso = await SeedCategoriaAsync("Peso-T1");
        var kg = await SeedUnidadAsync("UKG1", peso);
        var lb = await SeedUnidadAsync("ULB1", peso);

        var creado = await _service!.CreateAsync(NuevoArticulo("ZZUCAT1", bodega, tipo, kg, lb, kg), "tester");
        Assert.NotNull(creado.Id);
    }

    [SkippableFact]
    public async Task Create_CategoriasDistintas_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodega = await SeedBodegaAsync("UCB2");
        var tipo = await SeedTipoAsync("ZT2");
        var peso = await SeedCategoriaAsync("Peso-T2");
        var vol = await SeedCategoriaAsync("Volumen-T2");
        var kg = await SeedUnidadAsync("UKG2", peso);
        var lt = await SeedUnidadAsync("ULT2", vol);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CreateAsync(NuevoArticulo("ZZUCAT2", bodega, tipo, kg, lt, null), "tester"));
    }

    [SkippableFact]
    public async Task Create_UnidadSinCategoria_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodega = await SeedBodegaAsync("UCB3");
        var tipo = await SeedTipoAsync("ZT3");
        var sinCat = await SeedUnidadAsync("USINCAT3", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CreateAsync(NuevoArticulo("ZZUCAT3", bodega, tipo, sinCat, null, null), "tester"));
    }

    [SkippableFact]
    public async Task Create_AlmacenajeSinUnidadMedida_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodega = await SeedBodegaAsync("UCB4");
        var tipo = await SeedTipoAsync("ZT4");
        var peso = await SeedCategoriaAsync("Peso-T4");
        var kg = await SeedUnidadAsync("UKG4", peso);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CreateAsync(NuevoArticulo("ZZUCAT4", bodega, tipo, null, kg, null), "tester"));
    }

    /// <summary>
    /// CAMBIO 2026-07-29: crear SIN unidad de medida ya NO es válido si el tipo maneja
    /// inventario — sin unidad el kardex muestra cantidades sin unidad y no hay cómo
    /// convertir almacenaje/salida. Antes este caso pasaba (test `Create_SinNingunaUnidad_Ok`).
    /// La exigencia es solo AL CREAR: editar un artículo histórico sin unidad sigue siendo
    /// posible (ver <see cref="Update_ArticuloHistoricoSinUnidad_NoSeBloquea"/>).
    /// </summary>
    [SkippableFact]
    public async Task Create_SinUnidadMedida_ConTipoQueManejaInventario_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodega = await SeedBodegaAsync("UCB5");
        var tipo = await SeedTipoAsync("ZT5"); // maneja_inventario = true

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CreateAsync(NuevoArticulo("ZZUCAT5", bodega, tipo, null, null, null), "tester"));
    }

    /// <summary>
    /// Un tipo que NO maneja inventario (ej. Servicios) no lleva existencias ni kardex,
    /// así que tampoco necesita unidad: crearlo sin unidad sigue siendo válido.
    /// </summary>
    [SkippableFact]
    public async Task Create_SinUnidad_TipoSinInventario_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoSinInventarioAsync("ZT5B");

        // Sin inventario el artículo NO puede llevar bodegas: se crea sin ubicaciones.
        var dto = new ArticuloEditDto
        {
            Codigo = "ZZUCAT5B",
            Descripcion = "Servicio sin unidad",
            TipoArticuloId = tipo
        };

        var creado = await _service!.CreateAsync(dto, "tester");
        Assert.NotNull(creado.Id);
    }

    /// <summary>
    /// El artículo histórico migrado de SIMAFI tiene unidad_medida_id NULL. Editarlo (por
    /// ejemplo para corregir la descripción) NO debe quedar bloqueado por la nueva exigencia
    /// de unidad, que aplica solo al crear. Este es el escenario que se decidió proteger.
    /// </summary>
    [SkippableFact]
    public async Task Update_ArticuloHistoricoSinUnidad_NoSeBloquea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipo = await SeedTipoAsync("ZT5C");

        // Se siembra directo en la tabla para simular el artículo migrado: sin unidad.
        var historico = new alm_articulo
        {
            codigo_articulo = "ZZUCAT5C",
            descripcion = "Artículo migrado sin unidad",
            tipo_articulo_id = tipo
        };
        _context!.alm_articulos.Add(historico);
        await _context.SaveChangesAsync();

        var dto = await _service!.GetByIdAsync(historico.id);
        Assert.NotNull(dto);
        Assert.Null(dto!.UnidadMedidaId);

        dto.Descripcion = "Descripción corregida";
        var actualizado = await _service!.UpdateAsync(historico.id, dto, "tester");

        Assert.Equal("Descripción corregida", actualizado.Descripcion);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
