using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Clientes;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class ClienteDesdeSolicitudTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IClientesService? _service;

    public ClienteDesdeSolicitudTests(PostgresFixture fixture) : base(fixture)
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

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);
            _context.Database.UseTransaction(Transaction);

            _service = new ClientesService(_context, mockCompanyService);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task CrearCliente_ConSolicitud_MarcaAsignadaEnMismaTransaccion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var solicitudId = await InsertarSolicitudAsync(asignada: false);
        var dto = NuevoClienteDto();
        dto.SolicitudId = solicitudId;

        var respuesta = await _service!.CrearClienteAsync(dto, "test-user");

        Assert.True(respuesta.Id > 0);
        var asignada = await Connection.ExecuteScalarAsync<bool?>(
            "SELECT asiginada FROM solicitud_servicio WHERE solicitud_servicio_id = @id",
            new { id = solicitudId }, Transaction);
        Assert.True(asignada == true, "La solicitud debe quedar marcada como asignada.");
    }

    [SkippableFact]
    public async Task CrearCliente_ConSolicitudYaAsignada_FallaYNoCreaCliente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var solicitudId = await InsertarSolicitudAsync(asignada: true);
        var dto = NuevoClienteDto();
        dto.SolicitudId = solicitudId;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearClienteAsync(dto, "test-user"));

        var clientes = await Connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM cliente_maestro WHERE maestro_cliente_identidad = @dni",
            new { dni = dto.Dni }, Transaction);
        Assert.Equal(0, clientes);
    }

    [SkippableFact]
    public async Task CrearCliente_ConSolicitudInexistente_Falla()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = NuevoClienteDto();
        dto.SolicitudId = -1;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearClienteAsync(dto, "test-user"));
    }

    [SkippableFact]
    public async Task CrearCliente_ConSolicitudInactiva_FallaYNoCreaCliente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var solicitudId = await InsertarSolicitudAsync(asignada: false, estado: false);
        var dto = NuevoClienteDto();
        dto.SolicitudId = solicitudId;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearClienteAsync(dto, "test-user"));

        var clientes = await Connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM cliente_maestro WHERE maestro_cliente_identidad = @dni",
            new { dni = dto.Dni }, Transaction);
        Assert.Equal(0, clientes);
    }

    [SkippableFact]
    public async Task CrearCliente_SinSolicitud_ComportamientoIntacto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var respuesta = await _service!.CrearClienteAsync(NuevoClienteDto(), "test-user");

        Assert.True(respuesta.Id > 0);
    }

    /// <summary>DTO mínimo válido con Clave/Dni únicos por corrida (el servicio valida duplicados).</summary>
    private static ClienteCreateDto NuevoClienteDto()
    {
        var stamp = DateTime.UtcNow.Ticks.ToString();
        return new ClienteCreateDto
        {
            Clave = "T" + stamp[^7..],
            Nombre = "CLIENTE PRUEBA SOLICITUD",
            Dni = stamp[^13..],
            Activo = true
        };
    }

    private async Task<int> InsertarSolicitudAsync(bool asignada, bool estado = true)
    {
        var categoriaId = await Connection.ExecuteScalarAsync<int?>(
            "SELECT categoria_servicio_id FROM categoria_servicio ORDER BY categoria_servicio_id LIMIT 1",
            transaction: Transaction);
        Skip.If(categoriaId is null, "No hay categorias de servicio en la BD de prueba.");

        var stamp = DateTime.UtcNow.Ticks.ToString();
        return await Connection.ExecuteScalarAsync<int>(
            @"INSERT INTO solicitud_servicio
                  (cliente_identidad, categoria_servicio_id, cliente_nombre, cliente_movil,
                   cliente_direccion, estado, asiginada, usuariocreacion, fechacreacion)
              VALUES (@identidad, @categoriaId, 'SOLICITUD PRUEBA', '99990000',
                      'DIRECCION PRUEBA', @estado, @asignada, 'test-user', now())
              RETURNING solicitud_servicio_id",
            new { identidad = stamp[^13..], categoriaId, asignada, estado }, Transaction);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
