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
/// Motor de aprobación por niveles (F2): escalera acumulativa, elegibilidad por usuario o rol,
/// secuencia de firmas y las tres reglas de separación de funciones.
/// <para>
/// Todo ocurre dentro de la transacción del test, que hace ROLLBACK: la escalera, los aprobadores
/// y la orden de prueba no quedan escritos. El control de aprobación se enciende <b>dentro</b> de
/// la transacción, así que la configuración real de la base no se toca.
/// </para>
/// </summary>
[Collection("Postgres")]
public class AprobacionMotorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private AprobacionService? _motor;
    private readonly FakeUsuarioActual _usuario = new();

    private int _ordenId;
    private int _nivel1Id;
    private int _nivel2Id;
    private int _nivel3Id;

    private const string Creador = "creador@test.com";
    private const string Aprobador1 = "aprobador1@test.com";
    private const string Aprobador3 = "aprobador3@test.com";
    private const string RolNivel2 = "ComprasTest";
    private const decimal TotalOrden = 75000m;

    public AprobacionMotorTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _motor = new AprobacionService(_context, new TestCurrentCompanyService(CompanyId), _usuario);

        await EncenderControlAsync(autoaprobacion: false);
        await SembrarEscaleraAsync();
        _ordenId = await CrearOrdenAsync(Creador, TotalOrden);
    }

    // ------------------------------------------------------------------ escalera (D1)

    [SkippableFact]
    public async Task Escalera_es_acumulativa_y_exige_todos_los_niveles_bajo_el_monto()
    {
        Skip.IfNot(Fixture.Available);

        var escalera = await _motor!.ResolverEscaleraAsync(DocumentosAprobacion.OrdenCompra, TotalOrden);

        // 75,000 con umbrales 0 / 10,000.01 / 50,000.01 exige TRES niveles, no solo el tercero.
        Assert.Equal(3, escalera.Count);
        Assert.Equal((short)1, escalera[0].Nivel);
        Assert.Equal((short)3, escalera[2].Nivel);
        Assert.True(escalera[0].TieneAprobadores);
    }

    [SkippableFact]
    public async Task Escalera_de_monto_bajo_exige_solo_el_primer_nivel()
    {
        Skip.IfNot(Fixture.Available);

        var escalera = await _motor!.ResolverEscaleraAsync(DocumentosAprobacion.OrdenCompra, 5000m);

        Assert.Single(escalera);
        Assert.Equal((short)1, escalera[0].Nivel);
    }

    [SkippableFact]
    public async Task Control_apagado_no_exige_aprobacion()
    {
        Skip.IfNot(Fixture.Available);

        await EjecutarAsync(
            "UPDATE public.cfg_aprobacion_control SET modo = 0 WHERE company_id = @c AND documento = @d;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra });

        Assert.False(await _motor!.RequiereAprobacionAsync(DocumentosAprobacion.OrdenCompra));
        Assert.Empty(await _motor.ResolverEscaleraAsync(DocumentosAprobacion.OrdenCompra, TotalOrden));

        // Y no deja abrir un flujo que nadie pidió.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador));
    }

    // ------------------------------------------------------------------ apertura del flujo

    [SkippableFact]
    public async Task Iniciar_abre_el_flujo_con_el_primer_nivel_pendiente()
    {
        Skip.IfNot(Fixture.Available);

        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador);

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);

        Assert.Equal(3, estado.Total);
        Assert.Equal(0, estado.Firmados);
        Assert.Equal((short)1, estado.NivelPendiente);
        Assert.Equal(EstadoAprobacionNivel.Pendiente, estado.Niveles[0].Estado);
        Assert.Equal(EstadoAprobacionNivel.Bloqueado, estado.Niveles[1].Estado);
        Assert.Equal(EstadoAprobacionNivel.Bloqueado, estado.Niveles[2].Estado);

        // El monto firmado queda como snapshot en cada renglón.
        Assert.Equal(TotalOrden, estado.Niveles[0].TotalDocumento);

        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Enviada));
    }

    [SkippableFact]
    public async Task Iniciar_rechaza_un_nivel_sin_aprobadores()
    {
        Skip.IfNot(Fixture.Available);

        await EjecutarAsync(
            "DELETE FROM public.cfg_aprobacion_aprobador WHERE company_id = @c AND nivel_id = @n;",
            new { c = CompanyId, n = _nivel2Id });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador));

        Assert.Contains("no tiene aprobadores", error.Message);

        // Y no dejó el flujo a medio abrir.
        Assert.Equal(0, await ContarFlujoAsync());
    }

    [SkippableFact]
    public async Task Iniciar_dos_veces_el_mismo_documento_falla()
    {
        Skip.IfNot(Fixture.Available);

        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador));
    }

    // ------------------------------------------------------------------ firma (D1b, D3)

    [SkippableFact]
    public async Task Primera_firma_se_marca_como_tal_y_habilita_el_siguiente_nivel()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        var resultado = await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "Va");

        // EsPrimeraFirma es el disparador del compromiso presupuestario (D2).
        Assert.True(resultado.EsPrimeraFirma);
        Assert.False(resultado.FlujoCompleto);
        Assert.Equal((short)1, resultado.NivelFirmado);
        Assert.Equal((short)2, resultado.NivelPendiente);

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.Equal(1, estado.Firmados);
        Assert.Equal(EstadoAprobacionNivel.Aprobado, estado.Niveles[0].Estado);
        Assert.Equal(EstadoAprobacionNivel.Pendiente, estado.Niveles[1].Estado);
        Assert.Equal(Aprobador1, estado.Niveles[0].UsuarioFirma);
    }

    [SkippableFact]
    public async Task Se_puede_firmar_por_rol_ademas_de_por_usuario()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        // El nivel 2 no nombra a nadie: autoriza a un ROL de Identity (D3).
        _usuario.Establecer("otro@test.com", RolNivel2);
        var resultado = await _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        Assert.Equal((short)2, resultado.NivelFirmado);
        Assert.False(resultado.EsPrimeraFirma);
        Assert.Equal((short)3, resultado.NivelPendiente);
    }

    [SkippableFact]
    public async Task La_ultima_firma_completa_el_flujo()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        _usuario.Establecer("otro@test.com", RolNivel2);
        await _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        _usuario.Establecer(Aprobador3);
        var resultado = await _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        Assert.True(resultado.FlujoCompleto);
        Assert.Null(resultado.NivelPendiente);

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.Equal(3, estado.Firmados);
        Assert.Null(estado.NivelPendiente);
        Assert.Equal(3, await ContarBitacoraAsync(AccionAprobacion.Aprobada));
    }

    [SkippableFact]
    public async Task Quien_no_es_aprobador_del_nivel_no_puede_firmar()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer("intruso@test.com");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null));

        Assert.Contains("No está autorizado", error.Message);
        Assert.Equal(0, (await _motor!.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId)).Firmados);
    }

    [SkippableFact]
    public async Task El_aprobador_del_nivel_3_no_puede_saltarse_los_anteriores()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        // Es aprobador legítimo, pero de un nivel que todavía no está habilitado.
        _usuario.Establecer(Aprobador3);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null));
    }

    // ------------------------------------------------------------------ D5 y separación de funciones

    [SkippableFact]
    public async Task Nadie_aprueba_su_propia_orden_con_la_autoaprobacion_apagada()
    {
        Skip.IfNot(Fixture.Available);

        // Orden creada por quien SÍ es aprobador del nivel 1.
        var propia = await CrearOrdenAsync(Aprobador1, TotalOrden, numero: 990002);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, propia, "99002", TotalOrden, Aprobador1);

        _usuario.Establecer(Aprobador1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, propia, null));

        Assert.Contains("usted mismo creó", error.Message);
    }

    [SkippableFact]
    public async Task Con_la_autoaprobacion_encendida_el_creador_si_puede_firmar()
    {
        Skip.IfNot(Fixture.Available);

        await EncenderControlAsync(autoaprobacion: true);

        var propia = await CrearOrdenAsync(Aprobador1, TotalOrden, numero: 990003);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, propia, "99003", TotalOrden, Aprobador1);

        _usuario.Establecer(Aprobador1);
        var resultado = await _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, propia, null);

        Assert.Equal((short)1, resultado.NivelFirmado);
        Assert.True(resultado.EsPrimeraFirma);
    }

    [SkippableFact]
    public async Task Un_mismo_usuario_no_firma_dos_niveles_del_mismo_documento()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        // Ahora también es aprobador del nivel 2: elegible, pero ya firmó este documento.
        await EjecutarAsync(
            "INSERT INTO public.cfg_aprobacion_aprobador (company_id, nivel_id, tipo, valor) " +
            "VALUES (@c, @n, 1, @v);",
            new { c = CompanyId, n = _nivel2Id, v = Aprobador1 });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null));

        Assert.Contains("ya firmó", error.Message);
    }

    // ------------------------------------------------------------------ rechazo y devolución (D4)

    [SkippableFact]
    public async Task Rechazar_marca_el_nivel_y_queda_en_la_bitacora()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        await _motor!.RechazarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "Precio fuera de mercado");

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.Equal(EstadoAprobacionNivel.Rechazado, estado.Niveles[0].Estado);
        Assert.Null(estado.NivelPendiente);
        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Rechazada));
    }

    [SkippableFact]
    public async Task Rechazar_sin_motivo_no_se_permite()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _motor!.RechazarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "   "));
    }

    [SkippableFact]
    public async Task Devolver_borra_las_firmas_pero_la_bitacora_las_conserva()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        _usuario.Establecer(Creador);
        await _motor.ReiniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "Cambian las cantidades");

        // D4: el flujo se borra entero, sin comparar montos.
        Assert.Equal(0, await ContarFlujoAsync());

        // Pero la firma sigue existiendo donde importa para auditoría.
        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Aprobada));
        Assert.Equal(1, await ContarBitacoraAsync(AccionAprobacion.Devuelta));

        // Y el documento puede volver a enviarse desde cero.
        await _motor.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador);
        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.Equal(0, estado.Firmados);
        Assert.Equal((short)1, estado.NivelPendiente);
    }

    // ------------------------------------------------------------------ bandeja

    [SkippableFact]
    public async Task La_bandeja_solo_ofrece_lo_que_esa_persona_puede_firmar()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        // El aprobador del nivel 1 la ve.
        _usuario.Establecer(Aprobador1);
        var suyas = await _motor!.PendientesOrdenCompraAsync();
        Assert.Contains(suyas, p => p.DocumentoId == _ordenId && p.Nivel == 1);

        // El del nivel 3 todavía no.
        _usuario.Establecer(Aprobador3);
        Assert.DoesNotContain(await _motor.PendientesOrdenCompraAsync(), p => p.DocumentoId == _ordenId);

        // Un extraño tampoco.
        _usuario.Establecer("intruso@test.com");
        Assert.DoesNotContain(await _motor.PendientesOrdenCompraAsync(), p => p.DocumentoId == _ordenId);
    }

    [SkippableFact]
    public async Task La_bandeja_no_ofrece_al_creador_su_propia_orden()
    {
        Skip.IfNot(Fixture.Available);

        var propia = await CrearOrdenAsync(Aprobador1, TotalOrden, numero: 990004);
        await _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, propia, "99004", TotalOrden, Aprobador1);

        _usuario.Establecer(Aprobador1);
        Assert.DoesNotContain(await _motor.PendientesOrdenCompraAsync(), p => p.DocumentoId == propia);
    }

    // ------------------------------------------------------------------ casos de borde

    [SkippableFact]
    public async Task Un_nivel_inactivo_no_entra_en_la_escalera()
    {
        Skip.IfNot(Fixture.Available);

        await EjecutarAsync(
            "UPDATE public.cfg_aprobacion_nivel SET activo = false WHERE company_id = @c AND id = @n;",
            new { c = CompanyId, n = _nivel2Id });

        var escalera = await _motor!.ResolverEscaleraAsync(DocumentosAprobacion.OrdenCompra, TotalOrden);

        // Desactivar es la forma de retirar un escalón sin borrar su historia.
        Assert.Equal(2, escalera.Count);
        Assert.Equal((short)1, escalera[0].Nivel);
        Assert.Equal((short)3, escalera[1].Nivel);
    }

    [SkippableFact]
    public async Task El_progreso_del_listado_cuenta_las_firmas_de_cada_orden()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        var recienAbierto = await BuscarProgresoAsync(_ordenId);
        Assert.Equal(0, recienAbierto.Firmados);
        Assert.Equal(3, recienAbierto.Total);
        Assert.Equal((short)1, recienAbierto.NivelPendiente);
        Assert.Equal("Aprobación Nivel 1", recienAbierto.DescripcionPendiente);

        _usuario.Establecer(Aprobador1);
        await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        var trasFirmar = await BuscarProgresoAsync(_ordenId);
        Assert.Equal(1, trasFirmar.Firmados);
        Assert.Equal((short)2, trasFirmar.NivelPendiente);
    }

    [SkippableFact]
    public async Task Los_correos_del_nivel_pendiente_traen_usuarios_y_no_roles()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        // Nivel 1: un usuario nominal -> tiene correo.
        var delPrimero = await _motor!.CorreosNivelPendienteAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.Equal(Aprobador1, Assert.Single(delPrimero));

        _usuario.Establecer(Aprobador1);
        await _motor.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        // Nivel 2: solo un ROL. Sus miembros viven en Identity, asi que no hay a quien escribirle
        // nominalmente: la lista sale vacia y el aviso queda en la copia al area.
        Assert.Empty(await _motor.CorreosNivelPendienteAsync(DocumentosAprobacion.OrdenCompra, _ordenId));
    }

    [SkippableFact]
    public async Task El_estado_dice_a_cada_quien_si_puede_firmar_el_nivel_pendiente()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        var paraElAprobador = await _motor!.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.True(paraElAprobador.Niveles[0].PuedoFirmar);
        Assert.Equal("Pendiente", paraElAprobador.Niveles[0].EstadoDescripcion);
        Assert.Equal("Bloqueado", paraElAprobador.Niveles[1].EstadoDescripcion);

        _usuario.Establecer("intruso@test.com");
        var paraUnExtrano = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.False(paraUnExtrano.Niveles[0].PuedoFirmar);
    }

    [SkippableFact]
    public async Task Reenviar_con_otro_monto_arma_la_escalera_de_ese_monto()
    {
        Skip.IfNot(Fixture.Available);
        await IniciarFlujoAsync();

        _usuario.Establecer(Aprobador1);
        await _motor!.FirmarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, null);

        _usuario.Establecer(Creador);
        await _motor.ReiniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "Se recorto el pedido");

        // La orden vuelve con 5,000: ya no alcanza los umbrales de los niveles 2 y 3.
        await _motor.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", 5000m, Creador);

        var estado = await _motor.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, _ordenId);
        Assert.Equal(1, estado.Total);
        Assert.Equal(0, estado.Firmados);
        Assert.Equal(5000m, estado.Niveles[0].TotalDocumento);
    }

    [SkippableFact]
    public async Task Un_documento_que_no_esta_enganchado_no_abre_flujo()
    {
        Skip.IfNot(Fixture.Available);

        // La factura de compra esta en el catalogo pero no tiene tabla de flujo: antes que
        // escribir en la tabla equivocada, el motor se niega.
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _motor!.IniciarAsync(DocumentosAprobacion.FacturaCompra, _ordenId, "99001", TotalOrden, Creador));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Encuentra la orden del test dentro del progreso de toda la empresa.</summary>
    private async Task<ProgresoAprobacionDto> BuscarProgresoAsync(int ordenId)
    {
        foreach (var fila in await _motor!.ProgresoOrdenesCompraAsync())
        {
            if (fila.DocumentoId == ordenId) return fila;
        }

        throw new Xunit.Sdk.XunitException($"La orden {ordenId} no aparece en el progreso.");
    }

    private Task IniciarFlujoAsync()
        => _motor!.IniciarAsync(DocumentosAprobacion.OrdenCompra, _ordenId, "99001", TotalOrden, Creador);

    private async Task EncenderControlAsync(bool autoaprobacion)
    {
        // El control es estado GLOBAL de la base de prueba: se enciende DENTRO de la transacción
        // del test, que hace ROLLBACK. Con INSERT … ON CONFLICT por si la empresa no tiene semilla.
        await EjecutarAsync(
            @"INSERT INTO public.cfg_aprobacion_control (company_id, documento, modo, permite_autoaprobacion)
              VALUES (@c, @d, 1, @auto)
              ON CONFLICT (company_id, documento)
              DO UPDATE SET modo = 1, permite_autoaprobacion = @auto;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra, auto = autoaprobacion });
    }

    private async Task SembrarEscaleraAsync()
    {
        _nivel1Id = await CrearNivelAsync(1, "Aprobación Nivel 1", 0m);
        _nivel2Id = await CrearNivelAsync(2, "Aprobación Nivel 2", 10000.01m);
        _nivel3Id = await CrearNivelAsync(3, "Aprobación Nivel 3", 50000.01m);

        await AgregarAprobadorAsync(_nivel1Id, TipoAprobador.Usuario, Aprobador1);
        await AgregarAprobadorAsync(_nivel2Id, TipoAprobador.Rol, RolNivel2);
        await AgregarAprobadorAsync(_nivel3Id, TipoAprobador.Usuario, Aprobador3);
    }

    private async Task<int> CrearNivelAsync(short nivel, string descripcion, decimal desde)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText =
            @"INSERT INTO public.cfg_aprobacion_nivel
                     (company_id, documento, nivel, descripcion, monto_desde, activo)
              VALUES (@c, @d, @n, @desc, @desde, true) RETURNING id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("d", DocumentosAprobacion.OrdenCompra);
        cmd.Parameters.AddWithValue("n", nivel);
        cmd.Parameters.AddWithValue("desc", descripcion);
        cmd.Parameters.AddWithValue("desde", desde);
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

    private Task EjecutarAsync(string sql, object parametros)
        => Connection.ExecuteAsync(sql, parametros, Transaction);

    private Task<int> ContarFlujoAsync()
        => Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.alm_orden_compra_aprobacion WHERE company_id = @c AND orden_compra_id = @o;",
            new { c = CompanyId, o = _ordenId }, Transaction);

    /// <summary>
    /// Cuenta SOLO la bitácora de la orden del test. Filtrar por documento es imprescindible:
    /// <c>apr_bitacora</c> es append-only y guarda la historia real de la empresa, así que contar
    /// por empresa haría que estas pruebas empezaran a fallar en cuanto alguien apruebe algo de
    /// verdad en la base de prueba.
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

    /// <summary>Usuario de la sesión, cambiable entre firmas para simular varios aprobadores.</summary>
    private sealed class FakeUsuarioActual : ICurrentUserService
    {
        private string _userName = string.Empty;
        private List<string> _roles = new();

        public void Establecer(string userName, params string[] roles)
        {
            _userName = userName;
            _roles = new List<string>(roles);
        }

        public string GetUserName() => _userName.Trim().ToLowerInvariant();
        public IReadOnlyCollection<string> GetRoles() => _roles;
    }
}
