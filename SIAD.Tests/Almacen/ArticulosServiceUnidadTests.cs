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

            _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
            _context.Database.UseTransaction(Transaction);
            _service = new ArticulosService(_context, new TestCurrentCompanyService(CompanyId));
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

    [SkippableFact]
    public async Task Create_SinNingunaUnidad_Ok()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bodega = await SeedBodegaAsync("UCB5");
        var tipo = await SeedTipoAsync("ZT5");
        var creado = await _service!.CreateAsync(NuevoArticulo("ZZUCAT5", bodega, tipo, null, null, null), "tester");
        Assert.NotNull(creado.Id);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
