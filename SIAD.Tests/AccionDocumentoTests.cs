using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Cobranza;
using SIAD.Tests.Infrastructure;
using Xunit;

namespace SIAD.Tests;

[Collection("Postgres")]
public class AccionDocumentoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ICobranzaService? _service;

    public AccionDocumentoTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            var companyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, companyService);
            _context.Database.UseTransaction(Transaction);
            _service = new CobranzaService(_context, companyService, new Fakes.FakeDocumentoCobranzaGenerator());
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task SembrarCatalogoAsync()
    {
        await Connection.ExecuteAsync(
            """
            INSERT INTO axl_accion_cobranza (cod_accion, nombre, activo, genera_documento, documento_codigo)
            VALUES (9911, 'TEST CON DOC', true, true, @codigo),
                   (9912, 'TEST SIN DOC', true, false, NULL);
            """,
            new { codigo = DocumentosCobranzaCodigos.CartaCobranzaPrejudicial },
            Transaction);
    }

    [SkippableFact]
    public async Task RegistrarAccion_ConGeneraDocumento_ArchivaSnapshot()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await SembrarCatalogoAsync();

        var resultado = await _service!.RegistrarAccionAsync(
            new RegistrarAccionCobranzaRequest("TEST-DOC-001", 9911, null, null, "prueba", "tester"),
            "usuario-sesion");

        Assert.True(resultado.DocumentoGenerado);
        Assert.NotNull(resultado.DocumentoId);
        Assert.Null(resultado.DocumentoError);

        var doc = await _service.ObtenerDocumentoAccionAsync(resultado.DocumentoId!.Value);
        Assert.NotNull(doc);
        Assert.NotEmpty(doc!.Contenido);
        Assert.Equal("application/pdf", doc.ContentType);

        // El snapshot quedó ligado a la acción registrada
        var accionId = await Connection.ExecuteScalarAsync<int>(
            "SELECT accion_id FROM cln_accion_cobranza_documento WHERE id = @id",
            new { id = resultado.DocumentoId.Value }, Transaction);
        Assert.Equal(resultado.AccionId, accionId);
    }

    [SkippableFact]
    public async Task RegistrarAccion_SinGeneraDocumento_NoArchivaNada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await SembrarCatalogoAsync();

        var resultado = await _service!.RegistrarAccionAsync(
            new RegistrarAccionCobranzaRequest("TEST-DOC-002", 9912, null, null, "prueba", "tester"),
            "usuario-sesion");

        Assert.False(resultado.DocumentoGenerado);
        Assert.Null(resultado.DocumentoId);

        var count = await Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM cln_accion_cobranza_documento WHERE accion_id = @id",
            new { id = resultado.AccionId }, Transaction);
        Assert.Equal(0, count);
    }

    [SkippableFact]
    public async Task ObtenerDocumento_DevuelveLosMismosBytesArchivados()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await SembrarCatalogoAsync();

        var resultado = await _service!.RegistrarAccionAsync(
            new RegistrarAccionCobranzaRequest("TEST-DOC-003", 9911, null, null, null, "tester"),
            "usuario-sesion");

        var almacenado = await Connection.ExecuteScalarAsync<byte[]>(
            "SELECT contenido FROM cln_accion_cobranza_documento WHERE id = @id",
            new { id = resultado.DocumentoId!.Value }, Transaction);

        var doc = await _service.ObtenerDocumentoAccionAsync(resultado.DocumentoId.Value);

        Assert.NotNull(doc);
        Assert.True(almacenado!.SequenceEqual(doc!.Contenido));
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
