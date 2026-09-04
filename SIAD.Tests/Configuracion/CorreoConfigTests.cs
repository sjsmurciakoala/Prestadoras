using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Core.Tenancy;
using SIAD.Services.Configuracion;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Configuracion;

/// <summary>
/// Mantenimiento de correo por empresa: conexión (API key cifrada con DataProtection) +
/// áreas de notificación con destinatarios TO/CC. Cubre: round-trip de cifrado, que el GET
/// nunca exponga la key, conservar la key al no re-enviarla, reemplazo del conjunto de
/// destinatarios, resolución del remitente (override → default), CASCADE y aislamiento por tenant.
/// </summary>
[Collection("Postgres")]
public class CorreoConfigTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CorreoConfigService? _service;

    public CorreoConfigTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
            _context.Database.UseTransaction(Transaction);
            // EphemeralDataProtectionProvider: llaves en memoria, estables dentro del test →
            // Protect en GuardarConexión y Unprotect en ResolverEnvío usan la misma llave.
            _service = new CorreoConfigService(_context, new EphemeralDataProtectionProvider());
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────── conexión

    [SkippableFact]
    public async Task Conexion_SinConfig_DevuelveDefaultSinKey()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await LimpiarConexionAsync();

        var cfg = await _service!.ObtenerConexionAsync();

        Assert.Equal(ProveedorCorreo.SendGrid, cfg.Proveedor);
        Assert.False(cfg.Activo);
        Assert.False(cfg.TieneApiKey);
    }

    [SkippableFact]
    public async Task GuardarConexion_CifraKey_RoundTripPorResolver()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        const string apiKey = "SG.secreto-de-prueba-123";
        await _service!.GuardarConexionAsync(new ConexionCorreoUpsertDto
        {
            Proveedor = ProveedorCorreo.SendGrid,
            RemitenteEmailDefault = "no-reply@x.hn",
            RemitenteNombreDefault = "Prueba",
            Activo = true,
            NuevaApiKey = apiKey
        }, "tester");

        // El GET nunca trae la key, solo el flag.
        var cfg = await _service.ObtenerConexionAsync();
        Assert.True(cfg.TieneApiKey);

        // La única vía de descifrado es el resolver (uso del sender). Necesita un área activa.
        await GuardarAreaAsync(TipoNotificacion.Administracion, activo: true,
            destinatarios: [("a@x.hn", ClaseDestinatario.To, true)]);

        var envio = await _service.ResolverEnvioAsync(TipoNotificacion.Administracion);
        Assert.NotNull(envio);
        Assert.Equal(apiKey, envio!.ApiKey);
    }

    [SkippableFact]
    public async Task GuardarConexion_KeyVacia_ConservaLaPrevia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        const string apiKey = "SG.primera-key";
        await _service!.GuardarConexionAsync(new ConexionCorreoUpsertDto
        {
            RemitenteEmailDefault = "a@x.hn", Activo = true, NuevaApiKey = apiKey
        }, "tester");

        // Segundo guardado SIN NuevaApiKey (solo cambia el remitente): debe conservar la key.
        await _service.GuardarConexionAsync(new ConexionCorreoUpsertDto
        {
            RemitenteEmailDefault = "b@x.hn", Activo = true, NuevaApiKey = null
        }, "tester");

        var cfg = await _service.ObtenerConexionAsync();
        Assert.True(cfg.TieneApiKey);
        Assert.Equal("b@x.hn", cfg.RemitenteEmailDefault);

        await GuardarAreaAsync(TipoNotificacion.Administracion, activo: true,
            destinatarios: [("a@x.hn", ClaseDestinatario.To, true)]);
        var envio = await _service.ResolverEnvioAsync(TipoNotificacion.Administracion);
        Assert.Equal(apiKey, envio!.ApiKey);
    }

    [SkippableFact]
    public async Task GuardarConexion_ActivarSinKey_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.GuardarConexionAsync(new ConexionCorreoUpsertDto { Activo = true }, "tester"));
    }

    [SkippableFact]
    public async Task GuardarConexion_ProveedorInvalido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.GuardarConexionAsync(new ConexionCorreoUpsertDto { Proveedor = "MAILGUN" }, "tester"));
    }

    // ─────────────────────────────────────────────────────── notificaciones

    [SkippableFact]
    public async Task GuardarNotificacion_Upsert_ReemplazaDestinatarios()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true, destinatarios:
        [
            ("bodega@x.hn", ClaseDestinatario.To, true),
            ("jefe@x.hn", ClaseDestinatario.Cc, true)
        ]);

        var primera = (await _service!.ListarNotificacionesAsync()).Single(n => n.Tipo == TipoNotificacion.Almacen);
        Assert.Equal(2, primera.Destinatarios.Count);

        // Segundo guardado del MISMO tipo: no duplica el área (UNIQUE) y reemplaza los destinatarios.
        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true, destinatarios:
        [
            ("compras@x.hn", ClaseDestinatario.To, true)
        ]);

        var areas = (await _service.ListarNotificacionesAsync()).Where(n => n.Tipo == TipoNotificacion.Almacen).ToList();
        Assert.Single(areas);
        Assert.Equal(primera.Id, areas[0].Id); // misma fila (upsert por tipo)
        Assert.Single(areas[0].Destinatarios);
        Assert.Equal("compras@x.hn", areas[0].Destinatarios[0].Correo);
    }

    [SkippableFact]
    public async Task GuardarNotificacion_TipoInvalido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.GuardarNotificacionAsync(new NotificacionCorreoDto { Tipo = "MARKETING" }, "tester"));
    }

    [SkippableFact]
    public async Task GuardarNotificacion_ClaseDestinatarioInvalida_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var dto = new NotificacionCorreoDto
        {
            Tipo = TipoNotificacion.Cobranza,
            Destinatarios = [new DestinatarioCorreoDto { Correo = "x@x.hn", Clase = "BCC" }]
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.GuardarNotificacionAsync(dto, "tester"));
    }

    // ─────────────────────────────────────────────────────── resolver (sender)

    [SkippableFact]
    public async Task ResolverEnvio_RemitenteOverride_GanaSobreDefault()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        await ConexionActivaConKeyAsync("def@x.hn", "Default");

        // Área CON override → su remitente.
        await GuardarAreaAsync(TipoNotificacion.Administracion, activo: true,
            remitenteEmail: "admin@x.hn", remitenteNombre: "Admin",
            destinatarios: [("t@x.hn", ClaseDestinatario.To, true)]);
        var conOverride = await _service!.ResolverEnvioAsync(TipoNotificacion.Administracion);
        Assert.Equal("admin@x.hn", conOverride!.RemitenteEmail);
        Assert.Equal("Admin", conOverride.RemitenteNombre);

        // Área SIN override → cae al default de la conexión.
        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true,
            destinatarios: [("t@x.hn", ClaseDestinatario.To, true)]);
        var sinOverride = await _service.ResolverEnvioAsync(TipoNotificacion.Almacen);
        Assert.Equal("def@x.hn", sinOverride!.RemitenteEmail);
        Assert.Equal("Default", sinOverride.RemitenteNombre);
    }

    [SkippableFact]
    public async Task ResolverEnvio_SoloDestinatariosActivos_SeparadosPorClase()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        await ConexionActivaConKeyAsync("def@x.hn", "Default");
        await GuardarAreaAsync(TipoNotificacion.Cobranza, activo: true, destinatarios:
        [
            ("para@x.hn", ClaseDestinatario.To, true),
            ("inactivo@x.hn", ClaseDestinatario.To, false),
            ("copia@x.hn", ClaseDestinatario.Cc, true)
        ]);

        var envio = await _service!.ResolverEnvioAsync(TipoNotificacion.Cobranza);

        Assert.Equal(new[] { "para@x.hn" }, envio!.Para);
        Assert.Equal(new[] { "copia@x.hn" }, envio.ConCopia);
    }

    [SkippableFact]
    public async Task ResolverEnvio_EnvioApagado_DevuelveNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        // Conexión inactiva (sin key permitido) + área activa: el resolver corta por la conexión.
        await _service!.GuardarConexionAsync(new ConexionCorreoUpsertDto { Activo = false }, "tester");
        await GuardarAreaAsync(TipoNotificacion.Administracion, activo: true,
            destinatarios: [("a@x.hn", ClaseDestinatario.To, true)]);

        Assert.Null(await _service.ResolverEnvioAsync(TipoNotificacion.Administracion));
    }

    [SkippableFact]
    public async Task ResolverEnvio_AreaInactiva_DevuelveNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        await ConexionActivaConKeyAsync("def@x.hn", "Default");
        await GuardarAreaAsync(TipoNotificacion.Administracion, activo: false,
            destinatarios: [("a@x.hn", ClaseDestinatario.To, true)]);

        Assert.Null(await _service!.ResolverEnvioAsync(TipoNotificacion.Administracion));
    }

    // ─────────────────────────────────────────────────────── CASCADE + tenant

    [SkippableFact]
    public async Task Cascade_BorrarArea_BorraSusDestinatarios()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await GuardarAreaAsync(TipoNotificacion.Almacen, activo: true, destinatarios:
        [
            ("a@x.hn", ClaseDestinatario.To, true),
            ("b@x.hn", ClaseDestinatario.Cc, true)
        ]);
        var id = (await _service!.ListarNotificacionesAsync()).Single(n => n.Tipo == TipoNotificacion.Almacen).Id;

        Assert.Equal(2, await ContarDestinatariosAsync(id));

        // Borrado directo del área en la BD: la FK ON DELETE CASCADE se lleva los destinatarios.
        await EjecutarAsync("DELETE FROM public.cfg_notificacion WHERE id = @id",
            ("id", id));

        Assert.Equal(0, await ContarDestinatariosAsync(id));
    }

    [SkippableFact]
    public async Task AislamientoTenant_NoVeConfigDeOtraEmpresa()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await LimpiarConexionAsync();

        var otra = CompanyId + 100_000;
        await EjecutarAsync(
            "INSERT INTO public.cfg_correo (company_id, proveedor, activo) VALUES (@c, 'SENDGRID', true)",
            ("c", otra));
        await EjecutarAsync(
            "INSERT INTO public.cfg_notificacion (company_id, tipo, activo) VALUES (@c, 'ADMINISTRACION', true)",
            ("c", otra));

        // La conexión de la otra empresa no se ve (filtro global por company).
        var cfg = await _service!.ObtenerConexionAsync();
        Assert.False(cfg.Activo);
        Assert.False(cfg.TieneApiKey);

        // Sus áreas tampoco.
        var areas = await _service.ListarNotificacionesAsync();
        Assert.DoesNotContain(areas, n => n.Tipo == TipoNotificacion.Administracion);
    }

    // ─────────────────────────────────────────────────────────── helpers

    private async Task ConexionActivaConKeyAsync(string remitenteEmail, string remitenteNombre) =>
        await _service!.GuardarConexionAsync(new ConexionCorreoUpsertDto
        {
            Proveedor = ProveedorCorreo.SendGrid,
            RemitenteEmailDefault = remitenteEmail,
            RemitenteNombreDefault = remitenteNombre,
            Activo = true,
            NuevaApiKey = "SG.key-de-prueba"
        }, "tester");

    private async Task GuardarAreaAsync(
        string tipo, bool activo,
        (string correo, string clase, bool activo)[] destinatarios,
        string? remitenteEmail = null, string? remitenteNombre = null) =>
        await _service!.GuardarNotificacionAsync(new NotificacionCorreoDto
        {
            Tipo = tipo,
            Activo = activo,
            RemitenteEmail = remitenteEmail,
            RemitenteNombre = remitenteNombre,
            Destinatarios = destinatarios
                .Select(d => new DestinatarioCorreoDto { Correo = d.correo, Clase = d.clase, Activo = d.activo })
                .ToList()
        }, "tester");

    private async Task LimpiarConexionAsync()
    {
        var existentes = await _context!.cfg_correos.ToListAsync();
        _context.cfg_correos.RemoveRange(existentes);
        await _context.SaveChangesAsync();
    }

    private async Task<int> ContarDestinatariosAsync(long notificacionId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT count(*) FROM public.cfg_notificacion_destinatario WHERE notificacion_id = @id";
        cmd.Parameters.AddWithValue("id", notificacionId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task EjecutarAsync(string sql, params (string name, object value)[] parametros)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (name, value) in parametros)
            cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
