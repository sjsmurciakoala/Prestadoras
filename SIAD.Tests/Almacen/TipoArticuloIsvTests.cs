using System;
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
/// Clasificación de ISV en compras POR TIPO de artículo (2026-07-30): el tipo apunta a una
/// tasa del ISV (cfg_impuesto_tasa). Una tasa gravada = las compras registran ISV al costo;
/// una tasa exenta o NULL = no registra. Sin parte contable.
/// </summary>
[Collection("Postgres")]
public class TipoArticuloIsvTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private TipoArticuloService? _service;

    public TipoArticuloIsvTests(PostgresFixture fixture) : base(fixture)
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
            _service = new TipoArticuloService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ------------------------------------------------------------------ seeds

    /// <summary>El ISV es GLOBAL y único por código: se reutiliza si el catálogo ya lo tiene.</summary>
    private async Task<int> SeedImpuestoIsvAsync()
    {
        var existente = await _context!.cfg_impuestos.FirstOrDefaultAsync(i => i.codigo == "ISV");
        if (existente is not null) return existente.id;

        var imp = new cfg_impuesto { codigo = "ISV", nombre = "Impuesto Sobre Ventas", activo = true };
        _context.cfg_impuestos.Add(imp);
        await _context.SaveChangesAsync();
        return imp.id;
    }

    private async Task<int> SeedImpuestoNoIsvAsync(string codigo)
    {
        var imp = new cfg_impuesto { codigo = codigo, nombre = $"Impuesto {codigo}", activo = true };
        _context!.cfg_impuestos.Add(imp);
        await _context.SaveChangesAsync();
        return imp.id;
    }

    /// <summary>
    /// Tasa vigente (desde 2010, sin cierre). El código va con prefijo Z único por prueba para
    /// no chocar con el EXCLUDE de vigencias del catálogo real (que es por código de tasa).
    /// </summary>
    private async Task<int> SeedTasaAsync(int impuestoId, string codigo, string tipo, decimal porcentaje)
    {
        var t = new cfg_impuesto_tasa
        {
            impuesto_id = impuestoId,
            codigo = codigo,
            nombre = $"Tasa {codigo}",
            tipo = tipo,
            porcentaje = porcentaje,
            vigencia_desde = new DateOnly(2010, 1, 1),
            vigencia_hasta = null,
            activo = true
        };
        _context!.cfg_impuesto_tasas.Add(t);
        await _context.SaveChangesAsync();
        return t.id;
    }

    private Task<TipoArticuloEditDto> CrearTipoAsync(string codigo, int? impuestoTasaId)
        => _service!.CreateAsync(new TipoArticuloEditDto
        {
            Codigo = codigo,
            Nombre = $"Tipo {codigo}",
            ImpuestoTasaId = impuestoTasaId,
            Activo = true
        }, "tester");

    // ------------------------------------------------------------------ tests

    [SkippableFact]
    public async Task Create_ConTasaGravada_GuardaLaTasa_YMuestraIsvEnLista()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var impuestoId = await SeedImpuestoIsvAsync();
        var tasaId = await SeedTasaAsync(impuestoId, "ZISV15G", TipoImpuestoTasa.Gravado, 15m);

        var creado = await CrearTipoAsync("ZTISV1", tasaId);

        var edit = await _service!.GetByIdAsync(creado.Id!.Value);
        Assert.Equal(tasaId, edit!.ImpuestoTasaId);

        var lista = await _service.GetAsync(new ClasificacionFilterDto { Search = "ZTISV1" });
        Assert.Equal("ISV 15%", Assert.Single(lista).IsvDisplay);
    }

    [SkippableFact]
    public async Task Create_ConTasaExenta_MuestraExento()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var impuestoId = await SeedImpuestoIsvAsync();
        var tasaId = await SeedTasaAsync(impuestoId, "ZISVEX", TipoImpuestoTasa.Exento, 0m);

        await CrearTipoAsync("ZTISV2", tasaId);

        var lista = await _service!.GetAsync(new ClasificacionFilterDto { Search = "ZTISV2" });
        Assert.Equal("Exento", Assert.Single(lista).IsvDisplay);
    }

    [SkippableFact]
    public async Task Create_SinTasa_NoRegistraIsv()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearTipoAsync("ZTISV3", null);

        var edit = await _service!.GetByIdAsync(creado.Id!.Value);
        Assert.Null(edit!.ImpuestoTasaId);

        var lista = await _service.GetAsync(new ClasificacionFilterDto { Search = "ZTISV3" });
        Assert.Equal("—", Assert.Single(lista).IsvDisplay);
    }

    [SkippableFact]
    public async Task Update_AsignaYLuegoQuitaLaTasa()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var impuestoId = await SeedImpuestoIsvAsync();
        var tasaId = await SeedTasaAsync(impuestoId, "ZISV15U", TipoImpuestoTasa.Gravado, 15m);

        var creado = await CrearTipoAsync("ZTISV4", null);
        var id = creado.Id!.Value;

        // Asignar la tasa.
        var dto = await _service!.GetByIdAsync(id);
        dto!.ImpuestoTasaId = tasaId;
        await _service.UpdateAsync(id, dto, "tester");
        Assert.Equal(tasaId, (await _service.GetByIdAsync(id))!.ImpuestoTasaId);

        // Quitarla (el tipo deja de registrar ISV).
        dto = await _service.GetByIdAsync(id);
        dto!.ImpuestoTasaId = null;
        await _service.UpdateAsync(id, dto, "tester");
        Assert.Null((await _service.GetByIdAsync(id))!.ImpuestoTasaId);
    }

    [SkippableFact]
    public async Task Create_TasaInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearTipoAsync("ZTISV5", 999_999_999));
    }

    [SkippableFact]
    public async Task Create_TasaDeOtroImpuesto_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var otroId = await SeedImpuestoNoIsvAsync("ZOTRO");
        var tasaOtro = await SeedTasaAsync(otroId, "ZOTRO15", TipoImpuestoTasa.Gravado, 15m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearTipoAsync("ZTISV6", tasaOtro));
    }

    [SkippableFact]
    public async Task GetTasasIsv_IncluyeLasDelIsv_YExcluyeOtroImpuesto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var impuestoId = await SeedImpuestoIsvAsync();
        var tasaIsv = await SeedTasaAsync(impuestoId, "ZISVSEL", TipoImpuestoTasa.Gravado, 15m);

        var otroId = await SeedImpuestoNoIsvAsync("ZOTRO2");
        var tasaOtro = await SeedTasaAsync(otroId, "ZOTRO2A", TipoImpuestoTasa.Gravado, 10m);

        var tasas = await _service!.GetTasasIsvAsync();

        Assert.Contains(tasas, t => t.Id == tasaIsv);
        Assert.DoesNotContain(tasas, t => t.Id == tasaOtro);

        var opt = tasas.First(t => t.Id == tasaIsv);
        Assert.True(opt.EsGravada);
        Assert.Equal("ISV 15% (gravado)", opt.Display);
    }

    [SkippableFact]
    public async Task Lookup_IncluyeLaTasaHeredada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var impuestoId = await SeedImpuestoIsvAsync();
        var tasaId = await SeedTasaAsync(impuestoId, "ZISV15L", TipoImpuestoTasa.Gravado, 15m);
        var creado = await CrearTipoAsync("ZTISV7", tasaId);

        var lookup = await _service!.GetLookupAsync();
        var item = lookup.First(l => l.Id == creado.Id);

        Assert.Equal(tasaId, item.ImpuestoTasaId);
        Assert.Equal("ISV 15%", item.ImpuestoTasaDisplay);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
