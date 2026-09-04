using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Auditoria;
using SIAD.Services.Mantenimientos;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Mantenimientos;

/// <summary>
/// Catálogo de formatos fiscales (cfg_formato_fiscal, 2026-08-22): la máscara del No. de
/// factura (SAR) y del CAI que se transcriben del proveedor.
/// <para>
/// El contexto se arma CON el interceptor de la bitácora de maestros, porque el historial de
/// cambios es parte de lo que se pide del mantenimiento: no se escribe una línea de auditoría
/// en el servicio, así que hay que probar que el interceptor lo ve.
/// </para>
/// </summary>
[Collection("Postgres")]
public class FormatoFiscalTests : IntegrationTestBase, IAsyncLifetime
{
    private const string Tabla = "cfg_formato_fiscal";
    private const string MascaraSar = "###-###-##-########";

    private SiadDbContext? _context;
    private IFormatoFiscalService? _service;

    public FormatoFiscalTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var interceptor = new BitacoraMaestrosInterceptor(new FakeAuditConfig(), new FakeCatalog(), new FakeUser("tester"));
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection).AddInterceptors(interceptor).Options;

        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _service = new FormatoFiscalService(_context);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ------------------------------------------------------------------ ayuda

    /// <summary>Códigos con prefijo Z para no chocar con la semilla real (NUMERO_SAR y CAI).</summary>
    private Task<FormatoFiscalEditDto> CrearAsync(string codigo, string mascara = MascaraSar,
        short modo = ModoValidacionFormatoFiscal.Bloquea, bool obligatorio = false, string? patron = null)
        => _service!.CreateAsync(new FormatoFiscalEditDto
        {
            Codigo = codigo,
            Nombre = $"Campo {codigo}",
            Mascara = mascara,
            Patron = patron,
            ModoValidacion = modo,
            Obligatorio = obligatorio,
            Normalizar = true,
            Mayusculas = true,
            Activo = true
        }, "tester");

    private Task<List<bitacora_maestros>> BitacoraDe(int id)
        => _context!.bitacora_maestros.IgnoreQueryFilters()
            .Where(b => b.tabla == Tabla && b.registro_id == id.ToString())
            .OrderBy(b => b.bitacora_maestro_id)
            .ToListAsync();

    // ---------------------------------------------------------------- catálogo

    [SkippableFact]
    public async Task Create_GuardaElFormato_YNormalizaElCodigo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("z fmt uno");

        Assert.Equal("Z_FMT_UNO", creado.Codigo);

        var edit = await _service!.GetByIdAsync(creado.Id!.Value);
        Assert.NotNull(edit);
        Assert.Equal(MascaraSar, edit!.Mascara);
        Assert.Equal(ModoValidacionFormatoFiscal.Bloquea, edit.ModoValidacion);
        Assert.True(edit.Activo);
    }

    [SkippableFact]
    public async Task Create_CodigoDuplicado_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await CrearAsync("ZFMTDUP");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearAsync("zfmtdup"));
        Assert.Contains("ZFMTDUP", ex.Message);
    }

    [SkippableFact]
    public async Task Create_MascaraSinMetacaracteres_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Una máscara de puros literales no pide nada al usuario: no sirve de formato.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearAsync("ZFMTNOMETA", "-----"));
        Assert.Contains("máscara", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Create_PatronInvalido_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CrearAsync("ZFMTREGEX", MascaraSar, patron: "([sin cerrar"));
        Assert.Contains("expresión regular", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Create_PatronVacio_SeGuardaNulo_YSeDerivaEnLaLectura()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("ZFMTDERIV", MascaraSar, patron: "   ");

        var edit = await _service!.GetByIdAsync(creado.Id!.Value);
        Assert.Null(edit!.Patron);

        var lookup = (await _service.GetLookupAsync()).Single(f => f.Codigo == "ZFMTDERIV");
        Assert.Equal(@"^\d{3}-\d{3}-\d{2}-\d{8}$", lookup.Patron);
    }

    [SkippableFact]
    public async Task GetLookup_TraeDerivadaLaMascaraDeDevExpressYElEjemplo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await CrearAsync("ZFMTLOOK");

        var lookup = (await _service!.GetLookupAsync()).Single(f => f.Codigo == "ZFMTLOOK");
        Assert.Equal("000-000-00-00000000", lookup.MascaraDevExpress);
        Assert.Equal("000-000-00-00000000", lookup.Ejemplo);
        Assert.True(lookup.Bloquea);
        Assert.False(lookup.Advierte);
    }

    [SkippableFact]
    public async Task GetLookup_ExcluyeLosInactivos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("ZFMTOFF");
        Assert.Contains(await _service!.GetLookupAsync(), f => f.Codigo == "ZFMTOFF");

        await _service.DeactivateAsync(creado.Id!.Value, "tester");

        // Sin formato activo, la vista vuelve a texto libre: por eso no puede seguir en el lookup.
        Assert.DoesNotContain(await _service.GetLookupAsync(), f => f.Codigo == "ZFMTOFF");
    }

    [SkippableFact]
    public async Task Deactivate_EsBajaLogica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("ZFMTBAJA");
        Assert.True(await _service!.DeactivateAsync(creado.Id!.Value, "tester"));

        var edit = await _service.GetByIdAsync(creado.Id.Value);
        Assert.NotNull(edit);
        Assert.False(edit!.Activo);
    }

    // ---------------------------------------------------------------- historial

    [SkippableFact]
    public async Task Alta_dejaFilaDeCreacionEnLaBitacora()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("ZFMTAUD1");

        var fila = Assert.Single(await BitacoraDe(creado.Id!.Value));
        Assert.Equal(AccionesBitacora.Creacion, fila.accion);
        Assert.Equal("tester", fila.usuario);
        Assert.Null(fila.valores_anteriores);
        Assert.NotNull(fila.valores_nuevos);
        Assert.Contains(MascaraSar, fila.valores_nuevos!);
    }

    [SkippableFact]
    public async Task Editar_la_mascara_dejaElAntesYElDespues()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("ZFMTAUD2");

        var edit = await _service!.GetByIdAsync(creado.Id!.Value);
        edit!.Mascara = "###-###-########";
        await _service.UpdateAsync(creado.Id.Value, edit, "tester");

        var fila = (await BitacoraDe(creado.Id.Value)).Single(f => f.accion == AccionesBitacora.Actualizacion);
        Assert.Contains(MascaraSar, fila.valores_anteriores!);
        Assert.Contains("###-###-########", fila.valores_nuevos!);
    }

    [SkippableFact]
    public async Task Desactivar_seRegistraComoEliminacion_NoComoEdicion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await CrearAsync("ZFMTAUD3");
        await _service!.DeactivateAsync(creado.Id!.Value, "tester");

        Assert.Contains(await BitacoraDe(creado.Id.Value), f => f.accion == AccionesBitacora.Eliminacion);
    }

    // ------------------------------------------------------------------ fakes

    private sealed class FakeAuditConfig : IAuditConfigProvider
    {
        public bool DebeAuditar(long companyId, string tabla, string accion) => AuditableMaestros.EsAuditable(tabla);
        public void Invalidar(long companyId) { }
    }

    private sealed class FakeCatalog : IAuditableCatalogProvider
    {
        public bool EsAuditable(long companyId, string tabla) => AuditableMaestros.EsAuditable(tabla);
        public string NombreDe(long companyId, string tabla) => AuditableMaestros.NombreDe(tabla);
        public string ModuloDe(long companyId, string tabla) =>
            AuditableMaestros.All.FirstOrDefault(x => string.Equals(x.Tabla, tabla, StringComparison.OrdinalIgnoreCase))?.Modulo ?? tabla;
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
