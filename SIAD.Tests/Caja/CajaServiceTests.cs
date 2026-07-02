using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.DTOs.Caja;
using SIAD.Services.Caja;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Caja;

[Collection("Postgres")]
public class CajaServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ICajaService? _service;

    public CajaServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        // First run the base initialization to setup Connection and Transaction
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);
            
            // EF Core needs to use the transaction initiated by IntegrationTestBase
            _context.Database.UseTransaction(Transaction);
            
            _service = new CajaService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task AbrirCaja_CuandoNoHaySesionActiva_DebeCrearSesion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Act
        var result = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("test_user"));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var sesionActiva = await _service.ObtenerSesionActivaAsync("test_user");
        Assert.NotNull(sesionActiva);
        Assert.Equal("ABIERTA", sesionActiva.Estado);
        Assert.Equal("test_user", sesionActiva.UsuarioApertura);
    }

    [SkippableFact]
    public async Task AbrirCaja_CuandoYaHaySesionAbierta_DebeRechazar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Arrange
        await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("user1"));

        // Act - segunda apertura sobre el mismo usuario
        var result = await _service.AbrirCajaAsync(new AbrirCajaRequestDto("user1"));

        // Assert
        Assert.False(result.Success);
        Assert.Equal("El usuario ya tiene una sesión de caja abierta.", result.Message);
    }

    [SkippableFact]
    public async Task CerrarCaja_DespuesDeAbrir_DebeCerrarConTotal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Arrange
        var apertura = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("user1"));
        var sesionId = (int)apertura.Data!;

        // Act
        var cierre = await _service.CerrarCajaAsync(new CerrarCajaRequestDto(sesionId, "user1", "cierre test"));

        // Assert
        Assert.True(cierre.Success);

        var sesionActiva = await _service.ObtenerSesionActivaAsync("user1");
        Assert.Null(sesionActiva); // ya no hay sesión activa
    }

    [SkippableFact]
    public async Task CerrarCaja_ConTransaccionesAsociadas_DebeCalcularTotalCorrectamente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Arrange
        var apertura = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("user_trans"));
        var sesionId = (int)apertura.Data!;

        // Insert a dummy transaccion_abonado linked to this caja
        var transaccion = new SIAD.Core.Entities.transaccion_abonado
        {
            company_id = CompanyId,
            caja_id = sesionId,
            creditos = 750.50m,
            debitos = 0m,
            estado = "C", // Cobrado
            descripcion = "Pago Factura Dummy de Prueba"
        };
        _context!.transaccion_abonados.Add(transaccion);
        await _context.SaveChangesAsync();

        // Act
        var cierre = await _service.CerrarCajaAsync(new CerrarCajaRequestDto(sesionId, "user_trans", "cierre con recaudacion"));

        // Assert
        Assert.True(cierre.Success);

        // Fetch closed session from DB directly to verify total_cobrado
        var sesionCerrada = await _context.sesion_cajas.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.id == sesionId);
        Assert.NotNull(sesionCerrada);
        Assert.Equal("CERRADA", sesionCerrada.estado);
        Assert.Equal(750.50m, sesionCerrada.total_cobrado);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
