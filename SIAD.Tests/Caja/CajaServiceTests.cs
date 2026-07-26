using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.DTOs.Caja;
using SIAD.Services.Caja;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Caja;

/// <summary>
/// Sesiones de caja (F3, unificación cobranza): la apertura resuelve la caja
/// ASIGNADA al usuario (adm_caja_usuario) — sin asignación no se puede cobrar;
/// una sola sesión ABIERTA por caja; varias cajas operan simultáneamente.
/// </summary>
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
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);
            _context.Database.UseTransaction(Transaction);

            _service = new CajaService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    /// <summary>Crea una caja y asigna al usuario (modelo F3: la apertura la exige).</summary>
    private async Task<int> AsignarCajaAsync(string usuario, string codigo = "CAJA-T1")
    {
        var caja = await _context!.adm_cajas.FirstOrDefaultAsync(c => c.codigo == codigo);
        if (caja is null)
        {
            caja = new SIAD.Core.Entities.adm_caja
            {
                company_id = CompanyId,
                codigo = codigo,
                nombre = $"Caja test {codigo}",
                activo = true
            };
            _context.adm_cajas.Add(caja);
            await _context.SaveChangesAsync();
        }

        var resultado = await _service!.AsignarCajeroAsync(new AsignarCajeroDto(caja.caja_id, usuario), "test");
        Assert.True(resultado.Success, resultado.Message);
        return caja.caja_id;
    }

    [SkippableFact]
    public async Task AbrirCaja_SinAsignacion_DebeRechazar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var result = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("sin_caja"));

        Assert.False(result.Success);
        Assert.Contains("No tiene una caja asignada", result.Message);
    }

    [SkippableFact]
    public async Task AbrirCaja_ConAsignacion_AbreEnSuCaja()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cajaId = await AsignarCajaAsync("test_user");

        var result = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("test_user"));

        Assert.True(result.Success, result.Message);

        var sesionActiva = await _service.ObtenerSesionActivaAsync("test_user");
        Assert.NotNull(sesionActiva);
        Assert.Equal("ABIERTA", sesionActiva.Estado);
        Assert.Equal(cajaId, sesionActiva.CajaFisicaId);
    }

    [SkippableFact]
    public async Task AbrirCaja_CuandoYaHaySesionAbierta_DebeRechazar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await AsignarCajaAsync("user1");
        await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("user1"));

        var result = await _service.AbrirCajaAsync(new AbrirCajaRequestDto("user1"));

        Assert.False(result.Success);
        Assert.Equal("El usuario ya tiene una sesión de caja abierta.", result.Message);
    }

    [SkippableFact]
    public async Task Caja_ocupada_por_otro_cajero_rechaza_la_apertura()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await AsignarCajaAsync("cajero_a", "CAJA-OCUP");
        await AsignarCajaAsync("cajero_b", "CAJA-OCUP"); // misma caja, turnos

        var primera = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("cajero_a"));
        Assert.True(primera.Success, primera.Message);

        var segunda = await _service.AbrirCajaAsync(new AbrirCajaRequestDto("cajero_b"));
        Assert.False(segunda.Success);
        Assert.Contains("ya tiene una sesión abierta", segunda.Message);
        Assert.Contains("cajero_a", segunda.Message);
    }

    [SkippableFact]
    public async Task VariasCajas_OperanSimultaneamente_CadaUnaConSuCajero()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await AsignarCajaAsync("cajero_1", "CAJA-S1");
        await AsignarCajaAsync("cajero_2", "CAJA-S2");

        var a1 = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("cajero_1"));
        var a2 = await _service.AbrirCajaAsync(new AbrirCajaRequestDto("cajero_2"));

        Assert.True(a1.Success, a1.Message);
        Assert.True(a2.Success, a2.Message);

        var cajas = await _service.ListarCajasAsync();
        Assert.True(cajas.Where(c => c.Codigo is "CAJA-S1" or "CAJA-S2").All(c => c.Ocupada));
    }

    [SkippableFact]
    public async Task MiCaja_refleja_asignacion_y_ocupacion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Assert.Null(await _service!.ObtenerMiCajaAsync("nadie"));

        await AsignarCajaAsync("cajero_mi", "CAJA-MI");
        var libre = await _service.ObtenerMiCajaAsync("cajero_mi");
        Assert.NotNull(libre);
        Assert.False(libre.Ocupada);

        await _service.AbrirCajaAsync(new AbrirCajaRequestDto("cajero_mi"));
        var ocupada = await _service.ObtenerMiCajaAsync("cajero_mi");
        Assert.NotNull(ocupada);
        Assert.True(ocupada.Ocupada);
        Assert.Equal("cajero_mi", ocupada.OcupadaPor);
    }

    [SkippableFact]
    public async Task CerrarCaja_DespuesDeAbrir_DebeCerrarConTotal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await AsignarCajaAsync("user1");

        var apertura = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("user1"));
        var sesionId = (int)apertura.Data!;

        var cierre = await _service.CerrarCajaAsync(new CerrarCajaRequestDto(sesionId, "user1", "cierre test"));

        Assert.True(cierre.Success);

        var sesionActiva = await _service.ObtenerSesionActivaAsync("user1");
        Assert.Null(sesionActiva);
    }

    [SkippableFact]
    public async Task CerrarCaja_ConTransaccionesAsociadas_DebeCalcularTotalCorrectamente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await AsignarCajaAsync("user_trans");

        var apertura = await _service!.AbrirCajaAsync(new AbrirCajaRequestDto("user_trans"));
        var sesionId = (int)apertura.Data!;

        var transaccion = new SIAD.Core.Entities.transaccion_abonado
        {
            company_id = CompanyId,
            caja_id = sesionId,
            creditos = 750.50m,
            debitos = 0m,
            estado = "C",
            descripcion = "Pago Factura Dummy de Prueba"
        };
        _context!.transaccion_abonados.Add(transaccion);
        await _context.SaveChangesAsync();

        var cierre = await _service.CerrarCajaAsync(new CerrarCajaRequestDto(sesionId, "user_trans", "cierre con recaudacion"));

        Assert.True(cierre.Success);

        var sesionCerrada = await _context.sesion_cajas.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.id == sesionId);
        Assert.NotNull(sesionCerrada);
        Assert.Equal("CERRADA", sesionCerrada.estado);
        Assert.Equal(750.50m, sesionCerrada.total_cobrado);
    }

    [SkippableFact]
    public async Task Mantenimiento_crear_caja_y_reasignar_cajero()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creada = await _service!.GuardarCajaAsync(new CajaGuardarDto(null, "caja-m1", "Caja mantenimiento", true), "admin");
        Assert.True(creada.Success, creada.Message);

        var duplicada = await _service.GuardarCajaAsync(new CajaGuardarDto(null, "CAJA-M1", "Otra", true), "admin");
        Assert.False(duplicada.Success);

        var cajaId = (int)creada.Data!;
        var asignado = await _service.AsignarCajeroAsync(new AsignarCajeroDto(cajaId, "cajero_m"), "admin");
        Assert.True(asignado.Success, asignado.Message);

        // Reasignar lo MUEVE de caja (un usuario pertenece a una sola caja)
        var otra = await _service.GuardarCajaAsync(new CajaGuardarDto(null, "CAJA-M2", "Caja dos", true), "admin");
        var movido = await _service.AsignarCajeroAsync(new AsignarCajeroDto((int)otra.Data!, "cajero_m"), "admin");
        Assert.True(movido.Success, movido.Message);

        var admin = await _service.ListarCajasAdminAsync();
        Assert.DoesNotContain("cajero_m", admin.First(c => c.Codigo == "CAJA-M1").Asignados);
        Assert.Contains("cajero_m", admin.First(c => c.Codigo == "CAJA-M2").Asignados);

        var quitado = await _service.QuitarCajeroAsync("cajero_m");
        Assert.True(quitado.Success, quitado.Message);
        Assert.Null(await _service.ObtenerMiCajaAsync("cajero_m"));
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
