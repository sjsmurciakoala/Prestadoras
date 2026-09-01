using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Core.Tenancy;
using SIAD.Services.Configuracion;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Configuracion;

/// <summary>
/// Notificador de correo: resuelve la configuración (conexión + área) y delega en el transporte
/// (SendGrid), que aquí se mockea. Verifica que envía cuando está configurado, que omite (sin
/// llamar al transporte) cuando el envío está apagado o falta configuración, y que los correos de
/// sistema salen de la empresa fija con el destinatario dado.
/// </summary>
[Collection("Postgres")]
public class CorreoNotificadorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CorreoConfigService? _config;              // seeder + resolver (misma instancia = misma llave)
    private ISendGridCorreoTransport _transport = null!;
    private CorreoNotificador? _notificador;

    public CorreoNotificadorTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _config = new CorreoConfigService(_context, new EphemeralDataProtectionProvider());

        _transport = Substitute.For<ISendGridCorreoTransport>();

        // Empresa de sistema = la empresa de prueba (para los correos de Identity).
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Correo:CompanyIdSistema"] = CompanyId.ToString() })
            .Build();

        _notificador = new CorreoNotificador(_config, _transport, cfg);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task NotificarArea_Configurada_Envia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConexionActivaAsync("def@x.hn", "Default");
        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true,
            destinatarios: [("bodega@x.hn", ClaseDestinatario.To), ("jefe@x.hn", ClaseDestinatario.Cc)]);

        CorreoMensaje? enviado = null;
        _transport.EnviarAsync(Arg.Do<CorreoMensaje>(m => enviado = m), Arg.Any<CancellationToken>())
                  .Returns(CorreoEnvioResultado.Ok(202));

        var r = await _notificador!.NotificarAreaAsync(TipoNotificacion.Almacen, "Stock bajo", "<b>ojo</b>");

        Assert.True(r.Exito);
        Assert.NotNull(enviado);
        Assert.Equal(new[] { "bodega@x.hn" }, enviado!.Para);
        Assert.Equal(new[] { "jefe@x.hn" }, enviado.ConCopia);
        Assert.Equal("def@x.hn", enviado.FromEmail);   // sin override del área → default de la conexión
        Assert.Equal("Stock bajo", enviado.Asunto);
    }

    [SkippableFact]
    public async Task NotificarArea_EnvioApagado_OmiteSinLlamarTransporte()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // Conexión INACTIVA (sin key permitido) + área activa con destinatarios.
        await _config!.GuardarConexionAsync(new ConexionCorreoUpsertDto { Activo = false }, "tester");
        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true,
            destinatarios: [("bodega@x.hn", ClaseDestinatario.To)]);

        var r = await _notificador!.NotificarAreaAsync(TipoNotificacion.Almacen, "x", "y");

        Assert.True(r.Omitido);
        await _transport.DidNotReceive().EnviarAsync(Arg.Any<CorreoMensaje>(), Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task NotificarArea_SinDestinatarios_Omite()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConexionActivaAsync("def@x.hn", "Default");
        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true, destinatarios: []);

        var r = await _notificador!.NotificarAreaAsync(TipoNotificacion.Almacen, "x", "y");

        Assert.True(r.Omitido);
        await _transport.DidNotReceive().EnviarAsync(Arg.Any<CorreoMensaje>(), Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task EnviarSistema_ConexionActiva_EnviaAlDestinatario()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await ConexionActivaAsync("sistema@x.hn", "Sistema");   // sin área SISTEMA → usa el default

        CorreoMensaje? enviado = null;
        _transport.EnviarAsync(Arg.Do<CorreoMensaje>(m => enviado = m), Arg.Any<CancellationToken>())
                  .Returns(CorreoEnvioResultado.Ok(202));

        var r = await _notificador!.EnviarSistemaAsync("usuario@cliente.hn", "Restablece tu contraseña", "<a>link</a>");

        Assert.True(r.Exito);
        Assert.NotNull(enviado);
        Assert.Equal(new[] { "usuario@cliente.hn" }, enviado!.Para);
        Assert.Equal("sistema@x.hn", enviado.FromEmail);
    }

    [SkippableFact]
    public async Task EnviarSistema_ConexionInactiva_Omite()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await _config!.GuardarConexionAsync(new ConexionCorreoUpsertDto { Activo = false }, "tester");

        var r = await _notificador!.EnviarSistemaAsync("usuario@cliente.hn", "x", "y");

        Assert.True(r.Omitido);
        await _transport.DidNotReceive().EnviarAsync(Arg.Any<CorreoMensaje>(), Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task ProbarConexion_InactivaPeroConKey_EnviaIgual()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // Conexión con key y remitente, pero envío global APAGADO: probar debe funcionar igual.
        await _config!.GuardarConexionAsync(new ConexionCorreoUpsertDto
        {
            Proveedor = ProveedorCorreo.SendGrid,
            RemitenteEmailDefault = "def@x.hn",
            Activo = false,
            NuevaApiKey = "SG.key-de-prueba"
        }, "tester");

        CorreoMensaje? enviado = null;
        _transport.EnviarAsync(Arg.Do<CorreoMensaje>(m => enviado = m), Arg.Any<CancellationToken>())
                  .Returns(CorreoEnvioResultado.Ok(202));

        var r = await _notificador!.ProbarConexionAsync("prueba@cliente.hn");

        Assert.True(r.Exito);
        Assert.NotNull(enviado);
        Assert.Equal(new[] { "prueba@cliente.hn" }, enviado!.Para);
        Assert.Equal("def@x.hn", enviado.FromEmail);
    }

    [SkippableFact]
    public async Task ProbarConexion_SinKey_Omite()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // Sin conexión guardada (tablas vacías en la transacción del test) → no hay key que probar.
        var r = await _notificador!.ProbarConexionAsync("prueba@cliente.hn");

        Assert.True(r.Omitido);
        await _transport.DidNotReceive().EnviarAsync(Arg.Any<CorreoMensaje>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────── helpers

    private async Task ConexionActivaAsync(string remitenteEmail, string remitenteNombre) =>
        await _config!.GuardarConexionAsync(new ConexionCorreoUpsertDto
        {
            Proveedor = ProveedorCorreo.SendGrid,
            RemitenteEmailDefault = remitenteEmail,
            RemitenteNombreDefault = remitenteNombre,
            Activo = true,
            NuevaApiKey = "SG.key-de-prueba"
        }, "tester");

    private async Task GuardarAreaAsync(string tipo, bool activo, (string correo, string clase)[] destinatarios) =>
        await _config!.GuardarNotificacionAsync(new NotificacionCorreoDto
        {
            Tipo = tipo,
            Activo = activo,
            Destinatarios = destinatarios
                .Select(d => new DestinatarioCorreoDto { Correo = d.correo, Clase = d.clase, Activo = true })
                .ToList()
        }, "tester");

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
