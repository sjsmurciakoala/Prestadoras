using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Services.Auditoria;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Auditoria;

[Collection("Postgres")]
public class CatalogoAuditoriaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private AuditoriaConfigService? _service;

    public CatalogoAuditoriaTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        // Primero corre la inicialización base para preparar Connection y Transaction.
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));

            // EF Core debe usar la transacción iniciada por IntegrationTestBase.
            _context.Database.UseTransaction(Transaction);

            _service = new AuditoriaConfigService(
                _context, new FakeConfigProv(), new FakeCatalogProv(), new TestCurrentCompanyService(CompanyId));
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task AutoSeed_GetAsync_siembra_toda_la_lista_blanca()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El auto-seed de GetAsync solo corre si el catálogo está VACÍO para la empresa.
        // Contra una base que ya lo tiene sembrado, este test comparaba la lista blanca del
        // código contra las filas viejas de la BD y pasaba solo por casualidad: se rompía
        // apenas se agregaba una tabla a AuditableMaestros (fue lo que pasó al sumar
        // prv_proveedor_contacto y prv_tipo_contacto). Vaciarlo primero hace que el test
        // ejercite de verdad el auto-seed que promete su nombre. Todo esto vive dentro de la
        // transacción del fixture, así que se revierte al terminar.
        await _context!.bitacora_maestro_configs.ExecuteDeleteAsync();
        await _context.bitacora_maestro_catalogos.ExecuteDeleteAsync();

        var items = await _service!.GetAsync();

        Assert.Equal(AuditableMaestros.All.Count, items.Count);
        Assert.All(items, i => Assert.False(i.Habilitado));
        Assert.Contains(items, i => i.Entidad == "cliente_maestro");
        Assert.Contains(items, i => i.Entidad == "alm_bodega");
        Assert.Contains(items, i => i.Entidad == "prv_proveedor_contacto");
    }

    [SkippableFact]
    public async Task Agregar_suma_al_catalogo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GetAsync(); // siembra el catálogo

        await _service.AgregarAlCatalogoAsync(
            new AgregarMaestroDto { Tabla = "ban_cuenta", Nombre = "Cuentas bancarias", Modulo = "Bancos" }, "tester");

        var items = await _service.GetAsync();
        Assert.Contains(items, i => i.Entidad == "ban_cuenta");
    }

    [SkippableFact]
    public async Task Agregar_tabla_invalida_lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.AgregarAlCatalogoAsync(
                new AgregarMaestroDto { Tabla = "tabla_que_no_existe_xyz", Nombre = "X", Modulo = "Y" }, "tester"));
    }

    [SkippableFact]
    public async Task Desactivar_quita_de_GetAsync()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GetAsync(); // siembra el catálogo

        await _service.DesactivarCatalogoAsync("alm_bodega", "tester");

        var items = await _service.GetAsync();
        Assert.DoesNotContain(items, i => i.Entidad == "alm_bodega");
    }

    [SkippableFact]
    public async Task TablasDisponibles_excluye_las_del_catalogo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.GetAsync(); // siembra el catálogo

        var disp = await _service.TablasDisponiblesAsync();

        Assert.DoesNotContain(disp, d => d.Tabla == "alm_bodega");
        Assert.NotEmpty(disp);
    }

    private sealed class FakeConfigProv : IAuditConfigProvider
    {
        public bool DebeAuditar(long companyId, string tabla, string accion) => false;
        public void Invalidar(long companyId) { }
    }

    private sealed class FakeCatalogProv : IAuditableCatalogProvider
    {
        public bool EsAuditable(long companyId, string tabla) => false;
        public string NombreDe(long companyId, string tabla) => tabla;
        public string ModuloDe(long companyId, string tabla) => tabla;
        public void Invalidar(long companyId) { }
    }

    private sealed class TestCurrentCompanyService : SIAD.Core.Tenancy.ICurrentCompanyService
    {
        private readonly long _id;
        public TestCurrentCompanyService(long id) => _id = id;
        public long GetCompanyId() => _id;
    }
}
