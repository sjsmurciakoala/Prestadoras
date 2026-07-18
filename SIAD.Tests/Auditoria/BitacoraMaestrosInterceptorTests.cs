using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Almacen;
using SIAD.Services.Auditoria;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Auditoria;

[Collection("Postgres")]
public class BitacoraMaestrosInterceptorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IBodegaService? _bodegas;
    private readonly FakeAuditConfig _cfg = new(true);

    public BitacoraMaestrosInterceptorTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;
        var interceptor = new BitacoraMaestrosInterceptor(_cfg, new FakeUser("tester"));
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection).AddInterceptors(interceptor).Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _bodegas = new BodegaService(_context);
    }

    public new Task DisposeAsync() { _context?.Dispose(); return base.DisposeAsync(); }

    private Task<List<bitacora_maestros>> FilasDe(string registroId)
        => _context!.bitacora_maestros.IgnoreQueryFilters()
            .Where(b => b.tabla == "alm_bodega" && b.registro_id == registroId)
            .OrderBy(b => b.bitacora_maestro_id).ToListAsync();

    [SkippableFact]
    public async Task Crear_bodega_genera_Creacion_con_valores_nuevos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var creado = await _bodegas!.CreateAsync(new BodegaEditDto { Codigo = "AUD1", Nombre = "X" }, "tester");
        var fila = Assert.Single(await FilasDe(creado.Id!.Value.ToString()));
        Assert.Equal(AccionesBitacora.Creacion, fila.accion);
        Assert.Equal("Almacén", fila.modulo);
        Assert.Equal("tester", fila.usuario);
        Assert.Null(fila.valores_anteriores);
        Assert.NotNull(fila.valores_nuevos);
    }

    [SkippableFact]
    public async Task Editar_bodega_genera_Actualizacion_con_diff()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var creado = await _bodegas!.CreateAsync(new BodegaEditDto { Codigo = "AUD2", Nombre = "Nombre viejo" }, "tester");
        await _bodegas.UpdateAsync(creado.Id!.Value, new BodegaEditDto { Codigo = "AUD2", Nombre = "Nombre nuevo", Activo = true }, "tester");
        var edit = (await FilasDe(creado.Id.Value.ToString())).Single(f => f.accion == AccionesBitacora.Actualizacion);
        Assert.NotNull(edit.valores_anteriores);
        Assert.NotNull(edit.valores_nuevos);
        Assert.Contains("Nombre nuevo", edit.valores_nuevos!);
        Assert.Contains("Nombre viejo", edit.valores_anteriores!);
    }

    [SkippableFact]
    public async Task Baja_logica_genera_Eliminacion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var creado = await _bodegas!.CreateAsync(new BodegaEditDto { Codigo = "AUD3", Nombre = "Y" }, "tester");
        await _bodegas.DeactivateAsync(creado.Id!.Value, "tester");
        Assert.Contains(await FilasDe(creado.Id.Value.ToString()), f => f.accion == AccionesBitacora.Eliminacion);
    }

    [SkippableFact]
    public async Task Config_deshabilitada_no_audita()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        _cfg.Habilitado = false;
        var creado = await _bodegas!.CreateAsync(new BodegaEditDto { Codigo = "AUD4", Nombre = "Z" }, "tester");
        Assert.Empty(await FilasDe(creado.Id!.Value.ToString()));
    }

    private sealed class FakeAuditConfig : IAuditConfigProvider
    {
        public bool Habilitado;
        public FakeAuditConfig(bool habilitado) => Habilitado = habilitado;
        public bool DebeAuditar(long companyId, string tabla, string accion) => Habilitado && AuditableMaestros.EsAuditable(tabla);
        public void Invalidar(long companyId) { }
    }
    private sealed class FakeUser : ICurrentUserAudit
    {
        public FakeUser(string u) => Usuario = u;
        public string Usuario { get; }
    }
    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _id;
        public TestCurrentCompanyService(long id) => _id = id;
        public long GetCompanyId() => _id;
    }
}
