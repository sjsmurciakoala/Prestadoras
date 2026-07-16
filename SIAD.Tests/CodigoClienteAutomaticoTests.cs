using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.Tenancy;
using SIAD.Services.Clientes;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Código de cliente automático y secuencia sugerida (2026-07-16):
/// adm_codigo_cliente_config + fn_adm_siguiente_codigo_cliente (correlativo
/// atómico, auto-correctivo ante claves migradas) + fn_adm_siguiente_secuencia
/// (caminata de 10 en 10 por ciclo+libreta, esquema SIMAFI).
/// Requiere Database/2026-07-16_codigo_cliente_automatico.sql en la BD de test.
/// </summary>
[Collection("Postgres")]
public class CodigoClienteAutomaticoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IClientesService? _clientes;

    public CodigoClienteAutomaticoTests(PostgresFixture fixture) : base(fixture)
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
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private static ClienteCreateDto NuevoCliente(string sufijo) => new()
    {
        Nombre = "CLIENTE PRUEBA CODIGO AUTO",
        Dni = $"99979977{sufijo}",
        CicloId = 20,
        Libreta = "00L1",
        Secuencia = "00995",
    };

    [SkippableFact]
    public async Task Crear_sin_clave_genera_codigo_del_correlativo_configurado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var esperado = await Connection.ExecuteScalarAsync<string>(
            "SELECT prefijo || lpad(siguiente::text, longitud - length(prefijo), '0') FROM adm_codigo_cliente_config WHERE company_id = @co AND activo",
            new { co = CompanyId }, Transaction);
        Skip.If(esperado is null, "La empresa de test no tiene generador configurado");

        var dto = NuevoCliente("01");
        dto.Clave = string.Empty; // vacío => automático

        await _clientes!.CrearClienteAsync(dto, "test");

        var clave = await Connection.ExecuteScalarAsync<string>(
            "SELECT maestro_cliente_clave FROM cliente_maestro WHERE company_id = @co AND maestro_cliente_identidad = @dni",
            new { co = CompanyId, dni = dto.Dni }, Transaction);

        Assert.Equal(esperado, clave);
    }

    [SkippableFact]
    public async Task Dos_altas_seguidas_reciben_codigos_consecutivos_distintos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var uno = NuevoCliente("02");
        uno.Clave = null;
        var dos = NuevoCliente("03");
        dos.Clave = null;

        await _clientes!.CrearClienteAsync(uno, "test");
        await _clientes.CrearClienteAsync(dos, "test");

        var claves = (await Connection.QueryAsync<string>(
            "SELECT maestro_cliente_clave FROM cliente_maestro WHERE company_id = @co AND maestro_cliente_identidad IN (@d1, @d2) ORDER BY maestro_cliente_clave",
            new { co = CompanyId, d1 = uno.Dni, d2 = dos.Dni }, Transaction)).AsList();

        Assert.Equal(2, claves.Count);
        Assert.NotEqual(claves[0], claves[1]);
        Assert.Equal(long.Parse(claves[0]) + 1, long.Parse(claves[1]));
    }

    [SkippableFact]
    public async Task Colision_con_clave_migrada_salta_al_maximo_existente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Simula una migración por fuera del generador: retrocede el correlativo
        // a uno YA OCUPADO por un cliente real; el fn debe saltar al máximo
        // existente + 1 en vez de devolver un duplicado.
        var ocupado = await Connection.ExecuteScalarAsync<long?>(
            @"SELECT min(substring(maestro_cliente_clave FROM 3)::bigint)
              FROM cliente_maestro WHERE company_id = @co AND maestro_cliente_clave ~ '^09[0-9]{7}$'",
            new { co = CompanyId }, Transaction);
        Skip.If(ocupado is null, "La empresa de test no tiene claves 09XXXXXXX migradas");

        await Connection.ExecuteAsync(
            "UPDATE adm_codigo_cliente_config SET siguiente = @s WHERE company_id = @co",
            new { co = CompanyId, s = ocupado }, Transaction);

        var generado = await Connection.ExecuteScalarAsync<string>(
            "SELECT fn_adm_siguiente_codigo_cliente(@co)", new { co = CompanyId }, Transaction);

        var maximo = await Connection.ExecuteScalarAsync<long>(
            @"SELECT COALESCE(max(substring(maestro_cliente_clave FROM 3)::bigint), 0)
              FROM cliente_maestro WHERE company_id = @co AND maestro_cliente_clave ~ '^09[0-9]{7}$'",
            new { co = CompanyId }, Transaction);

        Assert.Equal("09" + (maximo + 1).ToString().PadLeft(7, '0'), generado);
    }

    [SkippableFact]
    public async Task Clave_manual_se_respeta_y_no_consume_correlativo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var antes = await Connection.ExecuteScalarAsync<long>(
            "SELECT siguiente FROM adm_codigo_cliente_config WHERE company_id = @co",
            new { co = CompanyId }, Transaction);

        var dto = NuevoCliente("04");
        dto.Clave = "TSTMANUAL04";
        await _clientes!.CrearClienteAsync(dto, "test");

        var despues = await Connection.ExecuteScalarAsync<long>(
            "SELECT siguiente FROM adm_codigo_cliente_config WHERE company_id = @co",
            new { co = CompanyId }, Transaction);

        Assert.Equal(antes, despues);
    }

    [SkippableFact]
    public async Task Secuencia_sugerida_es_max_mas_diez_por_ciclo_y_libreta()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var maxActual = await Connection.ExecuteScalarAsync<int>(
            @"SELECT COALESCE(max(split_part(maestro_cliente_indicativo_ruta,'-',4)::int), 0)
              FROM cliente_maestro
              WHERE company_id = @co AND estado AND ciclos_id = 20
                AND upper(split_part(maestro_cliente_indicativo_ruta,'-',3)) = '00L2'
                AND split_part(maestro_cliente_indicativo_ruta,'-',4) ~ '^[0-9]+$'",
            new { co = CompanyId }, Transaction);

        var sugerida = await _clientes!.SugerirSecuenciaAsync(20, "00l2"); // minúscula a propósito

        Assert.Equal((maxActual / 10 * 10 + 10).ToString().PadLeft(5, '0'), sugerida);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
