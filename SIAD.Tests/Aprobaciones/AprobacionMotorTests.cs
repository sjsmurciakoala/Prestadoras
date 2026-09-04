using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Aprobaciones;
using SIAD.Core.Security;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Aprobaciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Aprobaciones;

/// <summary>
/// Motor de autorización por monto: quién puede aprobar cuánto, y que la aprobación
/// <b>NO sea en cascada</b> (regla del usuario, 2026-09-01).
/// <para>
/// El caso que da nombre a la regla —una compra de 75,000 con niveles de 10,000, 50,000 y
/// 100,000, aprobada directamente por el de 100,000— está probado tal cual en
/// <see cref="El_nivel_que_alcanza_el_monto_aprueba_directamente_sin_los_de_abajo"/>.
/// </para>
/// <para>
/// Todo ocurre dentro de la transacción del test, que hace ROLLBACK: los tramos, los aprobadores
/// y la orden de prueba no quedan escritos, y el control se enciende DENTRO de la transacción.
/// </para>
/// </summary>
[Collection("Postgres")]
public class AprobacionMotorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private AprobacionService? _motor;
    private readonly TestCurrentUserService _usuario = new();

    private int _ordenId;
    private int _tramoBajoId;
    private int _tramoMedioId;
    private int _tramoAltoId;

    private const string Creador = "creador@test.com";

    // Los tres del ejemplo del requerimiento.
    private const string UsuarioA = "usuario.a@test.com";   // hasta 10,000
    private const string UsuarioB = "usuario.b@test.com";   // hasta 50,000
    private const string UsuarioC = "usuario.c@test.com";   // hasta 100,000
    private const string RolAlto = "GerenciaTest";          // también en el tramo alto

    private const decimal TotalOrden = 75000m;

    private const short DeBorrador = EstadoOrdenCompra.Borrador;
    private const short AEnAprobacion = EstadoOrdenCompra.EnAprobacion;
    private const short AAprobada = EstadoOrdenCompra.Aprobada;

    public AprobacionMotorTests(PostgresFixture fixture) : base(fixture)
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
        _motor = new AprobacionService(_context, empresa, _usuario);

        await EncenderControlAsync(autoaprobacion: false);
        await SembrarTramosAsync();
        _ordenId = await CrearOrdenAsync(Creador, TotalOrden);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Quién puede autorizar un monto ───────────────────────────────────────

    [SkippableFact]
    public async Task Solo_los_niveles_cuyo_limite_alcanza_el_monto_pueden_autorizarlo()
    {
        Skip.IfNot(Fixture.Available);

        var para75k = await _motor!.ResolverAutorizadoresAsync(DocumentosAprobacion.OrdenCompra, TotalOrden);

        // 10,000 y 50,000 se quedan cortos; 100,000 alcanza.
        Assert.Equal(2, para75k.Count);
        Assert.Equal(100000m, para75k[0].MontoHasta);
        Assert.Null(para75k[1].MontoHasta);            // el sin tope va al final
        Assert.True(para75k[0].TieneAprobadores);

        // Un monto pequeño lo puede autorizar cualquiera de los tres.
        var para5k = await _motor.ResolverAutorizadoresAsync(DocumentosAprobacion.OrdenCompra, 5000m);
        Assert.Equal(4, para5k.Count);
        Assert.Equal(10000m, para5k[0].MontoHasta);    // el más bajo primero

        // Un monto que supera todos los topes: nadie.
        var paraMillon = await _motor.ResolverAutorizadoresAsync(DocumentosAprobacion.OrdenCompra, 1000000m);
        Assert.Single(paraMillon);                     // solo el sin tope
        Assert.Null(paraMillon[0].MontoHasta);
    }

    [SkippableFact]
    public async Task Control_apagado_no_exige_autorizacion()
    {
        Skip.IfNot(Fixture.Available);

        await EjecutarAsync(
            "UPDATE public.cfg_aprobacion_control SET modo = 0 WHERE company_id = @c AND documento = @d;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra });

        Assert.False(await _motor!.RequiereAprobacionAsync(DocumentosAprobacion.OrdenCompra));
        Assert.Empty(await _motor.ResolverAutorizadoresAsync(DocumentosAprobacion.OrdenCompra, TotalOrden));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden,
                Creador, DeBorrador, AEnAprobacion));
    }

    // ── LA REGLA: sin cascada ────────────────────────────────────────────────

    [SkippableFact]
    public async Task El_nivel_que_alcanza_el_monto_aprueba_directamente_sin_los_de_abajo()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        // El caso literal del requerimiento: A llega a 10,000 y B a 50,000, y NINGUNO ha firmado.
        // C, que llega a 100,000, aprueba los 75,000 de una vez.
        _usuario.Establecer(UsuarioC);
        var firma = await _motor!.AutorizarAsync(
            DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, "Va", AEnAprobacion, AAprobada);

        Assert.Equal(100000m, firma.LimiteUtilizado);
        Assert.Equal(TotalOrden, firma.MontoAprobado);
        Assert.Equal(AEnAprobacion, firma.EstadoAnterior);
        Assert.Equal(AAprobada, firma.EstadoNuevo);

        // Una sola fila de autorización: no hay escalones que nadie tuviera que firmar.
        Assert.Equal(1, await ContarFlujoAsync());

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden);
        Assert.NotNull(estado.Firma);
        Assert.Equal(UsuarioC, estado.Firma!.UsuarioFirma);
        Assert.Equal(EstadoAprobacionNivel.Aprobado, estado.Firma.Estado);
    }

    [SkippableFact]
    public async Task Quien_no_alcanza_el_monto_no_puede_autorizarlo()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        // B autoriza hasta 50,000: 75,000 le queda grande.
        _usuario.Establecer(UsuarioB);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.AutorizarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, null,
                AEnAprobacion, AAprobada));

        Assert.Contains("Su límite de aprobación no alcanza", error.Message);
        Assert.Equal(0, await ContarFlujoAsync());
    }

    [SkippableFact]
    public async Task Un_nivel_sin_tope_autoriza_cualquier_monto()
    {
        Skip.IfNot(Fixture.Available);

        var ordenEnorme = await CrearOrdenAsync(Creador, 9500000m, numero: 990010);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, ordenEnorme, "99010", 9500000m,
            Creador, DeBorrador, AEnAprobacion);

        // Ni siquiera C (100,000) llega; el tramo sin tope sí.
        _usuario.Establecer(UsuarioC);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.AutorizarAsync(DocumentosAprobacion.OrdenCompra, ordenEnorme, 9500000m, null,
                AEnAprobacion, AAprobada));

        _usuario.Establecer("gerente@test.com", RolAlto);
        var firma = await _motor.AutorizarAsync(
            DocumentosAprobacion.OrdenCompra, ordenEnorme, 9500000m, null, AEnAprobacion, AAprobada);

        Assert.Null(firma.LimiteUtilizado);   // sin tope
    }

    [SkippableFact]
    public async Task Se_puede_autorizar_por_rol_ademas_de_por_usuario()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        // El tramo alto autoriza también a un ROL de Identity, no solo a personas nombradas.
        _usuario.Establecer("otro@test.com", RolAlto);
        var firma = await _motor!.AutorizarAsync(
            DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, null, AEnAprobacion, AAprobada);

        Assert.Equal("otro@test.com", (await _motor.ObtenerEstadoAsync(
            DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden)).Firma!.UsuarioFirma);
        Assert.NotEqual(0, firma.Nivel);
    }

    [SkippableFact]
    public async Task El_tramo_registrado_es_el_mas_bajo_que_alcanza()
    {
        Skip.IfNot(Fixture.Available);

        // Una orden de 5,000 la puede autorizar A (10,000) y también C (100,000). Si la autoriza
        // C, se registra SU tramo: es el que ejerció la capacidad.
        var ordenChica = await CrearOrdenAsync(Creador, 5000m, numero: 990011);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, ordenChica, "99011", 5000m,
            Creador, DeBorrador, AEnAprobacion);

        _usuario.Establecer(UsuarioA);
        var firma = await _motor.AutorizarAsync(
            DocumentosAprobacion.OrdenCompra, ordenChica, 5000m, null, AEnAprobacion, AAprobada);

        Assert.Equal(10000m, firma.LimiteUtilizado);
    }

    // ── Reglas que se conservan ──────────────────────────────────────────────

    [SkippableFact]
    public async Task Nadie_autoriza_su_propio_documento_con_la_autoaprobacion_apagada()
    {
        Skip.IfNot(Fixture.Available);

        var propia = await CrearOrdenAsync(UsuarioC, TotalOrden, numero: 990002);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, propia, "99002", TotalOrden,
            UsuarioC, DeBorrador, AEnAprobacion);

        _usuario.Establecer(UsuarioC);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.AutorizarAsync(DocumentosAprobacion.OrdenCompra, propia, TotalOrden, null,
                AEnAprobacion, AAprobada));

        Assert.Contains("usted mismo creó", error.Message);
    }

    [SkippableFact]
    public async Task Con_la_autoaprobacion_encendida_el_creador_si_autoriza()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderControlAsync(autoaprobacion: true);

        var propia = await CrearOrdenAsync(UsuarioC, TotalOrden, numero: 990003);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, propia, "99003", TotalOrden,
            UsuarioC, DeBorrador, AEnAprobacion);

        _usuario.Establecer(UsuarioC);
        var firma = await _motor.AutorizarAsync(
            DocumentosAprobacion.OrdenCompra, propia, TotalOrden, null, AEnAprobacion, AAprobada);

        Assert.Equal(100000m, firma.LimiteUtilizado);
    }

    [SkippableFact]
    public async Task Un_documento_ya_resuelto_no_se_vuelve_a_autorizar()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        _usuario.Establecer(UsuarioC);
        await _motor!.AutorizarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, null,
            AEnAprobacion, AAprobada);

        _usuario.Establecer("gerente@test.com", RolAlto);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.AutorizarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, null,
                AEnAprobacion, AAprobada));

        Assert.Contains("ya fue resuelto", error.Message);
        Assert.Equal(1, await ContarFlujoAsync());
    }

    [SkippableFact]
    public async Task Iniciar_dos_veces_el_mismo_documento_falla()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        _usuario.Establecer(UsuarioC);
        await _motor!.AutorizarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, null,
            AEnAprobacion, AAprobada);

        await Assert.ThrowsAsync<InvalidOperationException>(() => IniciarTramiteAsync());
    }

    // ── Rechazo y devolución ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task Rechazar_exige_la_misma_capacidad_que_aprobar()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        // Quien no podría autorizar el monto tampoco puede tumbar el documento.
        _usuario.Establecer(UsuarioB);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.RechazarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden,
                "No procede", AEnAprobacion, EstadoOrdenCompra.Rechazada));

        _usuario.Establecer(UsuarioC);
        await _motor!.RechazarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden,
            "Precio fuera de mercado", AEnAprobacion, EstadoOrdenCompra.Rechazada);

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden);
        Assert.Equal(EstadoAprobacionNivel.Rechazado, estado.Firma!.Estado);
        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Rechazada));
    }

    [SkippableFact]
    public async Task Rechazar_sin_motivo_no_se_permite()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        _usuario.Establecer(UsuarioC);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.RechazarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, "   ",
                AEnAprobacion, EstadoOrdenCompra.Rechazada));
    }

    [SkippableFact]
    public async Task Devolver_borra_la_autorizacion_pero_la_bitacora_la_conserva()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        _usuario.Establecer(UsuarioC);
        await _motor!.AutorizarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden, null,
            AEnAprobacion, AAprobada);

        _usuario.Establecer(Creador);
        await _motor.ReiniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "Cambian las cantidades",
            AAprobada, DeBorrador);

        Assert.Equal(0, await ContarFlujoAsync());
        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Aprobada));
        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Devuelta));

        // Y puede volver a enviarse desde cero.
        await IniciarTramiteAsync();
        Assert.Null((await _motor.ObtenerEstadoAsync(
            DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden)).Firma);
    }

    // ── Estado, capacidad y bandeja ──────────────────────────────────────────

    [SkippableFact]
    public async Task El_estado_avisa_cuando_ningun_nivel_alcanza_el_monto()
    {
        Skip.IfNot(Fixture.Available);

        // 9.5 millones supera todos los topes... salvo el tramo sin tope, así que se desactiva
        // para dejar el caso que el requerimiento pide mostrar.
        await EjecutarAsync(
            "UPDATE public.cfg_aprobacion_nivel SET activo = false WHERE company_id = @c AND monto_hasta IS NULL;",
            new { c = CompanyId });

        var ordenEnorme = await CrearOrdenAsync(Creador, 9500000m, numero: 990012);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, ordenEnorme, "99012", 9500000m,
            Creador, DeBorrador, AEnAprobacion);

        var estado = await _motor.ObtenerEstadoAsync(
            DocumentosAprobacion.OrdenCompra, ordenEnorme, 9500000m);

        Assert.False(estado.HayAprobadorCapaz);
        Assert.Null(estado.TramoMinimo);
        Assert.Null(estado.Firma);

        // El documento QUEDA pendiente: enviarlo no falló, solo no hay quién lo autorice.
        var capacidad = await BuscarCapacidadAsync(ordenEnorme);
        Assert.False(capacidad.HayAprobadorCapaz);
    }

    [SkippableFact]
    public async Task El_estado_dice_a_cada_quien_si_puede_autorizar()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        _usuario.Establecer(UsuarioC);
        var paraC = await _motor!.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden);
        Assert.True(paraC.PuedoAutorizar);
        Assert.True(paraC.HayAprobadorCapaz);
        Assert.Equal("Gerencia", paraC.TramoMinimo);

        _usuario.Establecer(UsuarioB);
        Assert.False((await _motor.ObtenerEstadoAsync(
            DocumentosAprobacion.OrdenCompra, _ordenId, TotalOrden)).PuedoAutorizar);
    }

    [SkippableFact]
    public async Task La_bandeja_solo_trae_lo_que_el_limite_de_cada_quien_cubre()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarTramiteAsync();

        _usuario.Establecer(UsuarioC);
        Assert.Contains(await _motor!.PendientesOrdenCompraAsync(), p => p.DocumentoId == _ordenId);

        // B no llega al monto: no la ve, aunque sea aprobador de un tramo.
        _usuario.Establecer(UsuarioB);
        Assert.DoesNotContain(await _motor.PendientesOrdenCompraAsync(), p => p.DocumentoId == _ordenId);

        _usuario.Establecer("intruso@test.com");
        Assert.DoesNotContain(await _motor.PendientesOrdenCompraAsync(), p => p.DocumentoId == _ordenId);
    }

    [SkippableFact]
    public async Task La_bandeja_no_ofrece_al_creador_su_propio_documento()
    {
        Skip.IfNot(Fixture.Available);

        var propia = await CrearOrdenAsync(UsuarioC, TotalOrden, numero: 990004);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, propia, "99004", TotalOrden,
            UsuarioC, DeBorrador, AEnAprobacion);

        _usuario.Establecer(UsuarioC);
        Assert.DoesNotContain(await _motor.PendientesOrdenCompraAsync(), p => p.DocumentoId == propia);
    }

    [SkippableFact]
    public async Task Los_correos_avisan_a_todos_los_capaces_y_solo_a_los_usuarios()
    {
        Skip.IfNot(Fixture.Available);

        // Para 75,000 alcanzan el tramo alto (usuario C + un rol) y el sin tope (solo un rol).
        var para75k = await _motor!.CorreosAutorizadoresAsync(DocumentosAprobacion.OrdenCompra, TotalOrden);
        Assert.Equal(UsuarioC, Assert.Single(para75k));

        // Para 5,000 alcanzan los tres tramos con usuario nominal.
        var para5k = await _motor.CorreosAutorizadoresAsync(DocumentosAprobacion.OrdenCompra, 5000m);
        Assert.Equal(3, para5k.Count);
    }

    [SkippableFact]
    public async Task Un_documento_que_no_esta_enganchado_no_abre_tramite()
    {
        Skip.IfNot(Fixture.Available);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _motor!.IniciarAsync(DocumentosAprobacion.FacturaCompra, _ordenId, "99001", TotalOrden,
                Creador, DeBorrador, AEnAprobacion));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private Task IniciarTramiteAsync()
        => _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden,
            Creador, DeBorrador, AEnAprobacion);

    private async Task EncenderControlAsync(bool autoaprobacion)
    {
        await EjecutarAsync(
            @"INSERT INTO public.cfg_aprobacion_control (company_id, documento, modo, permite_autoaprobacion)
              VALUES (@c, @d, 1, @auto)
              ON CONFLICT (company_id, documento)
              DO UPDATE SET modo = 1, permite_autoaprobacion = @auto;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra, auto = autoaprobacion });
    }

    /// <summary>Los tres del ejemplo del requerimiento, más un tramo sin tope.</summary>
    private async Task SembrarTramosAsync()
    {
        _tramoBajoId = await CrearTramoAsync(1, "Jefatura", 10000m);
        _tramoMedioId = await CrearTramoAsync(2, "Subgerencia", 50000m);
        _tramoAltoId = await CrearTramoAsync(3, "Gerencia", 100000m);
        var sinTope = await CrearTramoAsync(4, "Dirección", null);

        await AgregarAprobadorAsync(_tramoBajoId, TipoAprobador.Usuario, UsuarioA);
        await AgregarAprobadorAsync(_tramoMedioId, TipoAprobador.Usuario, UsuarioB);
        await AgregarAprobadorAsync(_tramoAltoId, TipoAprobador.Usuario, UsuarioC);
        await AgregarAprobadorAsync(_tramoAltoId, TipoAprobador.Rol, RolAlto);
        await AgregarAprobadorAsync(sinTope, TipoAprobador.Rol, RolAlto);
    }

    private async Task<int> CrearTramoAsync(short nivel, string descripcion, decimal? hasta)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText =
            @"INSERT INTO public.cfg_aprobacion_nivel
                     (company_id, documento, nivel, descripcion, monto_hasta, activo)
              VALUES (@c, @d, @n, @desc, @hasta, true) RETURNING id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("d", DocumentosAprobacion.OrdenCompra);
        cmd.Parameters.AddWithValue("n", nivel);
        cmd.Parameters.AddWithValue("desc", descripcion);
        cmd.Parameters.AddWithValue("hasta", (object?)hasta ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private Task AgregarAprobadorAsync(int nivelId, short tipo, string valor)
        => EjecutarAsync(
            "INSERT INTO public.cfg_aprobacion_aprobador (company_id, nivel_id, tipo, valor) " +
            "VALUES (@c, @n, @t, @v);",
            new { c = CompanyId, n = nivelId, t = tipo, v = valor });

    /// <summary>
    /// Orden mínima creada por SQL: al motor solo le importan el id, el creador y el total. No usa
    /// OrdenCompraService a propósito — este test prueba el motor, no el documento.
    /// </summary>
    private async Task<int> CrearOrdenAsync(string creador, decimal total, int numero = 990001)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText =
            @"INSERT INTO public.alm_orden_compra
                     (company_id, numero, fecha, cod_proveedor, estado, total, usuariocreacion)
              VALUES (@c, @n, CURRENT_DATE, 'TEST-APR', @estado, @total, @creador) RETURNING id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numero);
        cmd.Parameters.AddWithValue("estado", EstadoOrdenCompra.EnAprobacion);
        cmd.Parameters.AddWithValue("total", total);
        cmd.Parameters.AddWithValue("creador", creador);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<CapacidadAprobacionDto> BuscarCapacidadAsync(int ordenId)
    {
        foreach (var fila in await _motor!.CapacidadOrdenesCompraAsync())
        {
            if (fila.DocumentoId == ordenId) return fila;
        }

        throw new Xunit.Sdk.XunitException($"La orden {ordenId} no aparece en la capacidad.");
    }

    private Task EjecutarAsync(string sql, object parametros)
        => Connection.ExecuteAsync(sql, parametros, Transaction);

    private Task<int> ContarFlujoAsync()
        => Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.alm_orden_compra_aprobacion WHERE company_id = @c AND orden_compra_id = @o;",
            new { c = CompanyId, o = _ordenId }, Transaction);

    /// <summary>
    /// Cuenta SOLO la bitácora de la orden del test: <c>apr_bitacora</c> es append-only y guarda la
    /// historia real de la empresa, así que contar por empresa haría fallar estas pruebas en cuanto
    /// alguien apruebe algo de verdad en la base de prueba.
    /// </summary>
    private Task<int> ContarBitacoraAsync(string accion)
        => Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.apr_bitacora " +
            " WHERE company_id = @c AND documento = @d AND documento_id = @o AND accion = @a;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra, o = (long)_ordenId, a = accion },
            Transaction);

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
