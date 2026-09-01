using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Aprobaciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Aprobaciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Aprobaciones;

/// <summary>
/// Mantenimiento de la escalera (F4): el interruptor, los niveles, los aprobadores y las
/// validaciones que impiden dejar una configuración que no podría operar.
/// <para>
/// Es la pantalla que decide <b>quién puede autorizar compras y por cuánto</b>: una escalera mal
/// guardada aquí no da un error visible, deja documentos trabados o los deja pasar sin firma. Por
/// eso se prueban las validaciones una por una.
/// </para>
/// </summary>
[Collection("Postgres")]
public class AprobacionConfigTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private AprobacionConfigService? _config;

    private const string Doc = DocumentosAprobacion.OrdenCompra;

    public AprobacionConfigTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var empresa = new TestCurrentCompanyService(CompanyId);

        _context = new SiadDbContext(options, empresa);
        _context.Database.UseTransaction(Transaction);

        _config = new AprobacionConfigService(
            _context, empresa, new TestCurrentUserService("configurador@test.com"));
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── El interruptor ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task El_control_nace_apagado_y_se_puede_encender()
    {
        Skip.IfNot(Fixture.Available);

        // Estado de fábrica: la semilla deja todas las empresas en 0.
        var inicial = await _config!.ObtenerAsync(Doc);
        Assert.Equal(ModoAprobacion.Apagado, inicial.Modo);
        Assert.False(inicial.PermiteAutoaprobacion);
        Assert.Equal("Orden de compra", inicial.DocumentoDescripcion);

        await _config.GuardarControlAsync(Doc, ModoAprobacion.Encendido, permiteAutoaprobacion: true);

        var guardado = await _config.ObtenerAsync(Doc);
        Assert.Equal(ModoAprobacion.Encendido, guardado.Modo);
        Assert.True(guardado.PermiteAutoaprobacion);
    }

    [SkippableFact]
    public async Task Un_modo_que_no_existe_no_se_guarda()
    {
        Skip.IfNot(Fixture.Available);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config!.GuardarControlAsync(Doc, 7, false));

        Assert.Equal(ModoAprobacion.Apagado, (await _config!.ObtenerAsync(Doc)).Modo);
    }

    [SkippableFact]
    public async Task Un_documento_desconocido_se_rechaza()
    {
        Skip.IfNot(Fixture.Available);

        await Assert.ThrowsAsync<ArgumentException>(() => _config!.ObtenerAsync("   "));
    }

    // ── Niveles ──────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task Guardar_un_nivel_lo_crea_y_volver_a_guardarlo_lo_edita()
    {
        Skip.IfNot(Fixture.Available);

        var creado = await _config!.GuardarNivelAsync(Doc, Nivel(1, "  Jefatura  ", 0m));

        Assert.True(creado.Id > 0);
        Assert.Equal("Jefatura", creado.Descripcion);   // se recorta al guardar

        creado.Descripcion = "Jefatura de compras";
        creado.MontoDesde = 500m;
        await _config.GuardarNivelAsync(Doc, creado);

        var config = await _config.ObtenerAsync(Doc);
        var nivel = Assert.Single(config.Niveles);
        Assert.Equal("Jefatura de compras", nivel.Descripcion);
        Assert.Equal(500m, nivel.MontoDesde);
    }

    [SkippableFact]
    public async Task No_se_puede_repetir_el_numero_de_nivel()
    {
        Skip.IfNot(Fixture.Available);

        await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.GuardarNivelAsync(Doc, Nivel(1, "Otra jefatura", 100m)));

        Assert.Contains("Ya existe un nivel 1", error.Message);
    }

    [SkippableFact]
    public async Task La_escalera_no_puede_ir_de_mayor_a_menor()
    {
        Skip.IfNot(Fixture.Available);

        await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 5000m));

        // Un nivel 2 más barato que el 1 dejaría montos que exigen el 2 pero no el 1, y el motor
        // —que firma en orden— nunca llegaría a habilitarlo.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.GuardarNivelAsync(Doc, Nivel(2, "Gerencia", 1000m)));

        Assert.Contains("de menor a mayor", error.Message);

        // Y al revés: un nivel 1 más caro que el 2 existente.
        await _config.GuardarNivelAsync(Doc, Nivel(3, "Dirección", 9000m));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.GuardarNivelAsync(Doc, Nivel(2, "Gerencia", 99000m)));
    }

    [SkippableFact]
    public async Task Un_nivel_sin_descripcion_o_con_monto_negativo_no_se_guarda()
    {
        Skip.IfNot(Fixture.Available);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config!.GuardarNivelAsync(Doc, Nivel(1, "   ", 0m)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", -1m)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config!.GuardarNivelAsync(Doc, Nivel(0, "Jefatura", 0m)));

        Assert.Empty((await _config!.ObtenerAsync(Doc)).Niveles);
    }

    [SkippableFact]
    public async Task Eliminar_un_nivel_se_lleva_sus_aprobadores()
    {
        Skip.IfNot(Fixture.Available);

        var nivel = await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));
        await _config.AgregarAprobadorAsync(nivel.Id, Aprobador(TipoAprobador.Usuario, "jefe@test.com"));

        Assert.True(await _config.EliminarNivelAsync(nivel.Id));

        Assert.Empty((await _config.ObtenerAsync(Doc)).Niveles);
        Assert.Equal(0, await Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.cfg_aprobacion_aprobador WHERE company_id = @c AND nivel_id = @n;",
            new { c = CompanyId, n = nivel.Id }, Transaction));
    }

    // ── Aprobadores ──────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task El_usuario_se_guarda_en_minusculas_y_el_rol_conserva_su_forma()
    {
        Skip.IfNot(Fixture.Available);

        var nivel = await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));

        var usuario = await _config.AgregarAprobadorAsync(
            nivel.Id, Aprobador(TipoAprobador.Usuario, "  Jefe@Empresa.COM  "));
        var rol = await _config.AgregarAprobadorAsync(
            nivel.Id, Aprobador(TipoAprobador.Rol, "Super Administrador"));

        // La normalización es lo que hace comparables la elegibilidad y la regla de autoaprobación.
        Assert.Equal("jefe@empresa.com", usuario.Valor);
        Assert.Equal("Usuario", usuario.TipoDescripcion);

        // El rol es un nombre de Identity: sus mayúsculas son parte del valor.
        Assert.Equal("Super Administrador", rol.Valor);
        Assert.Equal("Rol", rol.TipoDescripcion);
    }

    [SkippableFact]
    public async Task No_se_puede_repetir_un_aprobador_aunque_cambien_las_mayusculas()
    {
        Skip.IfNot(Fixture.Available);

        var nivel = await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));
        await _config.AgregarAprobadorAsync(nivel.Id, Aprobador(TipoAprobador.Rol, "Compras"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.AgregarAprobadorAsync(nivel.Id, Aprobador(TipoAprobador.Rol, "COMPRAS")));

        Assert.Contains("ya es aprobador", error.Message);
    }

    [SkippableFact]
    public async Task Un_aprobador_vacio_de_tipo_desconocido_o_de_un_nivel_que_no_existe_se_rechaza()
    {
        Skip.IfNot(Fixture.Available);

        var nivel = await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.AgregarAprobadorAsync(nivel.Id, Aprobador(TipoAprobador.Usuario, "   ")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.AgregarAprobadorAsync(nivel.Id, Aprobador(9, "algo")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _config.AgregarAprobadorAsync(999999, Aprobador(TipoAprobador.Usuario, "jefe@test.com")));
    }

    [SkippableFact]
    public async Task Quitar_un_aprobador_lo_saca_del_nivel()
    {
        Skip.IfNot(Fixture.Available);

        var nivel = await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));
        var uno = await _config.AgregarAprobadorAsync(nivel.Id, Aprobador(TipoAprobador.Usuario, "a@test.com"));
        await _config.AgregarAprobadorAsync(nivel.Id, Aprobador(TipoAprobador.Usuario, "b@test.com"));

        Assert.True(await _config.EliminarAprobadorAsync(uno.Id));
        Assert.False(await _config.EliminarAprobadorAsync(uno.Id));   // ya no está

        var config = await _config.ObtenerAsync(Doc);
        var quedan = Assert.Single(config.Niveles).Aprobadores;
        Assert.Equal("b@test.com", Assert.Single(quedan).Valor);
    }

    // ── Advertencias: configuración que no podría operar ──────────────────────

    [SkippableFact]
    public async Task Avisa_del_nivel_sin_aprobadores_y_del_que_tiene_uno_solo()
    {
        Skip.IfNot(Fixture.Available);

        var solo = await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura", 0m));
        await _config.GuardarNivelAsync(Doc, Nivel(2, "Gerencia", 10000m));

        // Nivel 2 sin nadie: ningún documento que lo exija podría enviarse.
        var sinNadie = await _config.ObtenerAsync(Doc);
        Assert.Contains(sinNadie.Advertencias, a => a.Contains("«Gerencia»") && a.Contains("no tiene aprobadores"));

        // Nivel 1 con uno solo: opera, pero sus vacaciones detienen las compras.
        await _config.AgregarAprobadorAsync(solo.Id, Aprobador(TipoAprobador.Usuario, "jefe@test.com"));
        var conUno = await _config.ObtenerAsync(Doc);
        Assert.Contains(conUno.Advertencias, a => a.Contains("«Jefatura»") && a.Contains("un solo aprobador"));
    }

    [SkippableFact]
    public async Task Avisa_si_el_control_esta_encendido_y_no_hay_ningun_nivel()
    {
        Skip.IfNot(Fixture.Available);

        await _config!.GuardarControlAsync(Doc, ModoAprobacion.Encendido, false);

        var config = await _config.ObtenerAsync(Doc);
        Assert.Contains(config.Advertencias, a => a.Contains("no hay ningún nivel configurado"));
    }

    [SkippableFact]
    public async Task La_configuracion_de_un_documento_no_se_mezcla_con_la_de_otro()
    {
        Skip.IfNot(Fixture.Available);

        await _config!.GuardarNivelAsync(Doc, Nivel(1, "Jefatura de compras", 0m));
        await _config.GuardarNivelAsync(DocumentosAprobacion.Requisicion, Nivel(1, "Jefe de bodega", 0m));

        // Mismo número de nivel, documentos distintos: son escaleras independientes.
        Assert.Equal("Jefatura de compras", Assert.Single((await _config.ObtenerAsync(Doc)).Niveles).Descripcion);
        Assert.Equal("Jefe de bodega",
            Assert.Single((await _config.ObtenerAsync(DocumentosAprobacion.Requisicion)).Niveles).Descripcion);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static AprobacionNivelConfigDto Nivel(short nivel, string descripcion, decimal desde)
        => new() { Nivel = nivel, Descripcion = descripcion, MontoDesde = desde, Activo = true };

    private static AprobacionAprobadorConfigDto Aprobador(short tipo, string valor)
        => new() { Tipo = tipo, Valor = valor };

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
