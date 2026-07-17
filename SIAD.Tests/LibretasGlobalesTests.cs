using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.DTOs.Libretas;
using SIAD.Core.Tenancy;
using SIAD.Services.Clientes;
using SIAD.Services.Libretas;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Libretas globales (2026-07-16): la libreta viene del catálogo adm_libreta
/// (sin ciclo, espejo del modelo real de SIMAFI) y viaja tal cual, en
/// MAYÚSCULAS, como segmento 3 del indicativo. El esquema numérico legacy
/// (CC+LLL) se conserva para libretas de solo dígitos.
/// Requiere Database/2026-07-16_libretas_globales.sql aplicado en la BD de test.
/// </summary>
[Collection("Postgres")]
public class LibretasGlobalesTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IClientesService? _clientes;
    private ILibretasService? _libretas;

    public LibretasGlobalesTests(PostgresFixture fixture) : base(fixture)
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

            _clientes = new ClientesService(_context, company);
            _libretas = new LibretasService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private static ClienteCreateDto NuevoCliente(string sufijo) => new()
    {
        Clave = $"TSTLIB{sufijo}",
        Nombre = "CLIENTE PRUEBA LIBRETAS",
        Dni = $"99989988{sufijo}",
        CicloId = 20,
        Secuencia = "00990",
    };

    [SkippableFact]
    public async Task Crear_cliente_con_libreta_de_catalogo_usa_el_codigo_tal_cual_en_mayusculas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevoCliente("01");
        dto.Libreta = "00l2"; // minúscula a propósito: debe normalizar a 00L2

        var creado = await _clientes!.CrearClienteAsync(dto, "test");

        var indicativo = await Connection.ExecuteScalarAsync<string>(
            "SELECT maestro_cliente_indicativo_ruta FROM cliente_maestro WHERE company_id = @co AND maestro_cliente_clave = @clave",
            new { co = CompanyId, clave = dto.Clave }, Transaction);

        Assert.NotNull(creado);
        Assert.Equal("00L2", indicativo!.Split('-')[2]);
        Assert.StartsWith("20-", indicativo);
        Assert.EndsWith("-00990", indicativo);
    }

    [SkippableFact]
    public async Task Crear_cliente_con_libreta_fuera_del_catalogo_es_rechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevoCliente("02");
        dto.Libreta = "XXL9"; // alfanumérica pero no existe en adm_libreta

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _clientes!.CrearClienteAsync(dto, "test"));
        Assert.Contains("XXL9", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_cliente_con_libreta_numerica_conserva_el_esquema_legacy()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevoCliente("03");
        dto.Libreta = "70"; // dígitos: deriva CC+LLL = 20 + 070

        await _clientes!.CrearClienteAsync(dto, "test");

        var indicativo = await Connection.ExecuteScalarAsync<string>(
            "SELECT maestro_cliente_indicativo_ruta FROM cliente_maestro WHERE company_id = @co AND maestro_cliente_clave = @clave",
            new { co = CompanyId, clave = dto.Clave }, Transaction);

        Assert.Equal("20070", indicativo!.Split('-')[2]);
    }

    [SkippableFact]
    public async Task Catalogo_normaliza_codigo_y_rechaza_duplicados()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var id = await _libretas!.CrearAsync(
            new LibretaUpsertDto { Codigo = " 00l7 ", Descripcion = "LIBRO 007" }, "test");

        var creada = await _libretas.ObtenerAsync(id);
        Assert.Equal("00L7", creada!.Codigo);

        await Assert.ThrowsAsync<ArgumentException>(() => _libretas.CrearAsync(
            new LibretaUpsertDto { Codigo = "00L7", Descripcion = "duplicada" }, "test"));
    }

    [SkippableFact]
    public async Task Rutas_del_ciclo_se_derivan_de_los_clientes_no_del_catalogo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Las 5 libretas reales del ciclo 19 (clientes migrados) deben salir en
        // fn_adm_periodo_ciclo_info aunque la tabla rutas dejara de consumirse.
        var rutas = await Connection.ExecuteScalarAsync<string>(
            @"SELECT rutas::text FROM fn_adm_periodo_ciclo_info(@co, 2026, 7, '19')",
            new { co = CompanyId }, Transaction);

        Assert.NotNull(rutas);
        foreach (var libreta in new[] { "00L1", "00L2", "00L3", "00L4", "00L5" })
        {
            Assert.Contains(libreta, rutas);
        }
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
