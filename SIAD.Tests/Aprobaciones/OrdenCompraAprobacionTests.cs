using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Almacen;
using SIAD.Services.Aprobaciones;
using SIAD.Services.Presupuesto;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Aprobaciones;

/// <summary>
/// Enganche de la escalera en la orden de compra (F3): enviar, firmar, rechazar y devolver, más
/// la <b>no-regresión</b> del camino histórico cuando el control está apagado.
/// <para>
/// El control presupuestario se apaga en el arranque: aquí se prueba la máquina de estados, no la
/// reserva. Que la PRIMERA firma sea la que compromete se verifica por la bandera del resultado
/// (<c>ComprometioPresupuesto</c>), que es justo el disparador que consume el servicio.
/// </para>
/// </summary>
[Collection("Postgres")]
public class OrdenCompraAprobacionTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private OrdenCompraService? _ordenes;
    private readonly TestCurrentUserService _usuario = new();

    /// <summary>Registra los avisos que se intentaron enviar, sin mandar correo de verdad.</summary>
    private readonly AprobacionNotificadorNoop avisos = new();

    private int _articuloId;

    private const string Creador = "comprador@test.com";
    private const string Aprobador1 = "jefe1@test.com";
    private const string Aprobador2 = "jefe2@test.com";
    private const decimal TotalOrden = 75000m;

    public OrdenCompraAprobacionTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        await DesactivarControlPresupuestarioAsync();
        await DesactivarIntegracionContableAsync();

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var empresa = new TestCurrentCompanyService(CompanyId);

        _context = new SiadDbContext(options, empresa);
        _context.Database.UseTransaction(Transaction);

        _ordenes = new OrdenCompraService(
            _context, empresa, new TasaIsvArticuloResolver(_context),
            new PresupuestoCompromisoService(_context, empresa),
            new AprobacionService(_context, empresa, _usuario),
            avisos);

        _articuloId = await Connection.ExecuteScalarAsync<int>(
            "SELECT id FROM public.alm_articulo WHERE company_id = @c ORDER BY id LIMIT 1;",
            new { c = CompanyId }, Transaction);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── No-regresión: con el control apagado nada cambia ─────────────────────

    [SkippableFact]
    public async Task Con_la_escalera_apagada_aprobar_sigue_siendo_de_un_clic()
    {
        Skip.IfNot(Fixture.Available);

        // El control de aprobación nace apagado: no se toca nada en este test.
        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);

        Assert.True(await _ordenes!.AprobarAsync(id, "tester"));

        Assert.Equal(EstadoOrdenCompra.Aprobada, await EstadoAsync(id));
        Assert.Equal(0, await ContarFlujoAsync(id));   // no se abrió ninguna escalera
    }

    [SkippableFact]
    public async Task Con_la_escalera_apagada_no_se_puede_enviar_a_aprobacion()
    {
        Skip.IfNot(Fixture.Available);

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ordenes!.EnviarAAprobacionAsync(id, "tester"));

        Assert.Contains("no está activada", error.Message);
        Assert.Equal(EstadoOrdenCompra.Borrador, await EstadoAsync(id));
    }

    // ── Camino con escalera ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task Enviar_a_aprobacion_deja_la_orden_en_estado_7_y_abre_el_flujo()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);

        Assert.True(await _ordenes!.EnviarAAprobacionAsync(id, Creador));

        Assert.Equal(EstadoOrdenCompra.EnAprobacion, await EstadoAsync(id));
        Assert.Equal(2, await ContarFlujoAsync(id));   // 75,000 exige dos niveles
    }

    [SkippableFact]
    public async Task Con_la_escalera_encendida_no_se_aprueba_un_borrador_de_un_clic()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        _usuario.Establecer(Aprobador1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ordenes!.AprobarAsync(id, Aprobador1));

        Assert.Contains("enviarse a aprobación", error.Message);
        Assert.Equal(EstadoOrdenCompra.Borrador, await EstadoAsync(id));
    }

    [SkippableFact]
    public async Task La_primera_firma_marca_el_compromiso_y_la_ultima_aprueba_la_orden()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes!.EnviarAAprobacionAsync(id, Creador);

        // Primera firma: reserva presupuesto (D2) y la orden sigue en aprobación.
        _usuario.Establecer(Aprobador1);
        var primera = await _ordenes.FirmarAprobacionAsync(id, "De acuerdo", Aprobador1);

        Assert.NotNull(primera);
        Assert.True(primera!.ComprometioPresupuesto);
        Assert.False(primera.FlujoCompleto);
        Assert.Equal((short)2, primera.NivelPendiente);
        Assert.Equal(EstadoOrdenCompra.EnAprobacion, await EstadoAsync(id));

        // Segunda y última: aprueba la orden y sella al firmante FINAL.
        _usuario.Establecer(Aprobador2);
        var segunda = await _ordenes.FirmarAprobacionAsync(id, null, Aprobador2);

        Assert.NotNull(segunda);
        Assert.False(segunda!.ComprometioPresupuesto);
        Assert.True(segunda.FlujoCompleto);
        Assert.Equal(EstadoOrdenCompra.Aprobada, await EstadoAsync(id));
        Assert.Equal(Aprobador2, await AprobadoPorAsync(id));
    }

    [SkippableFact]
    public async Task Una_orden_en_aprobacion_no_se_puede_editar()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes!.EnviarAAprobacionAsync(id, Creador);

        // Es lo que da sentido a la firma: lo que se aprueba ya no cambia bajo los pies.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ordenes.ActualizarAsync(id, new OrdenCompraDto { CodProveedor = "TEST-APR" }, "tester"));
    }

    [SkippableFact]
    public async Task Rechazar_desde_la_escalera_deja_la_orden_rechazada()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes!.EnviarAAprobacionAsync(id, Creador);

        _usuario.Establecer(Aprobador1);
        Assert.True(await _ordenes.RechazarAsync(id, "Proveedor no calificado", Aprobador1));

        Assert.Equal(EstadoOrdenCompra.Rechazada, await EstadoAsync(id));
        Assert.Equal(1, await ContarBitacoraAsync(id, AccionAprobacion.Rechazada));
    }

    [SkippableFact]
    public async Task Devolver_deja_la_orden_editable_y_sin_firmas()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes!.EnviarAAprobacionAsync(id, Creador);

        _usuario.Establecer(Aprobador1);
        await _ordenes.FirmarAprobacionAsync(id, null, Aprobador1);

        _usuario.Establecer(Creador);
        Assert.True(await _ordenes.DevolverABorradorAsync(id, "Faltó un renglón", Creador));

        // D4: vuelve a Borrador, sin firmas, y la bitácora conserva lo que pasó.
        Assert.Equal(EstadoOrdenCompra.Borrador, await EstadoAsync(id));
        Assert.Equal(0, await ContarFlujoAsync(id));
        Assert.Equal(1, await ContarBitacoraAsync(id, AccionAprobacion.Devuelta));

        // Y se puede volver a enviar desde cero.
        Assert.True(await _ordenes.EnviarAAprobacionAsync(id, Creador));
        Assert.Equal(EstadoOrdenCompra.EnAprobacion, await EstadoAsync(id));
    }

    [SkippableFact]
    public async Task Anular_una_orden_en_aprobacion_queda_en_la_bitacora()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes!.EnviarAAprobacionAsync(id, Creador);

        Assert.True(await _ordenes.AnularAsync(id, "tester"));

        Assert.Equal(EstadoOrdenCompra.Anulada, await EstadoAsync(id));
        Assert.Equal(1, await ContarBitacoraAsync(id, AccionAprobacion.Anulada));
    }

    // ── avisos por correo (F6) ───────────────────────────────────────────────

    [SkippableFact]
    public async Task Cada_paso_avisa_a_quien_le_toca()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);

        // Al enviar: se avisa al primer nivel.
        await _ordenes!.EnviarAAprobacionAsync(id, Creador);
        Assert.Equal(new[] { "Jefatura" }, avisos.Pendientes);

        // Al firmar el primero: se avisa al segundo, y todavía no hay desenlace que contar.
        _usuario.Establecer(Aprobador1);
        await _ordenes.FirmarAprobacionAsync(id, null, Aprobador1);
        Assert.Equal(new[] { "Jefatura", "Gerencia" }, avisos.Pendientes);
        Assert.Empty(avisos.Resueltas);

        // Al firmar el último: se le avisa al comprador que su orden quedó aprobada.
        _usuario.Establecer(Aprobador2);
        await _ordenes.FirmarAprobacionAsync(id, null, Aprobador2);
        Assert.Equal(new[] { "aprobada" }, avisos.Resueltas);
    }

    [SkippableFact]
    public async Task Rechazar_y_devolver_avisan_al_comprador()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var rechazada = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes!.EnviarAAprobacionAsync(rechazada, Creador);
        _usuario.Establecer(Aprobador1);
        await _ordenes.RechazarAsync(rechazada, "Precio fuera de mercado", Aprobador1);

        var devuelta = await CrearOrdenAsync(EstadoOrdenCompra.Borrador);
        await _ordenes.EnviarAAprobacionAsync(devuelta, Creador);
        _usuario.Establecer(Creador);
        await _ordenes.DevolverABorradorAsync(devuelta, "Faltó un renglón", Creador);

        Assert.Equal(new[] { "rechazada", "devuelta a borrador" }, avisos.Resueltas);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Enciende la escalera de la empresa DENTRO de la transacción del test, con dos niveles:
    /// nivel 1 desde 0 y nivel 2 desde 10,000.01. Una orden de 75,000 exige los dos.
    /// </summary>
    private async Task EncenderEscaleraAsync()
    {
        await Connection.ExecuteAsync(
            @"INSERT INTO public.cfg_aprobacion_control (company_id, documento, modo, permite_autoaprobacion)
              VALUES (@c, @d, 1, false)
              ON CONFLICT (company_id, documento) DO UPDATE SET modo = 1, permite_autoaprobacion = false;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra }, Transaction);

        var nivel1 = await CrearNivelAsync(1, "Jefatura", 0m);
        var nivel2 = await CrearNivelAsync(2, "Gerencia", 10000.01m);

        await Connection.ExecuteAsync(
            "INSERT INTO public.cfg_aprobacion_aprobador (company_id, nivel_id, tipo, valor) VALUES (@c, @n, 1, @v);",
            new[]
            {
                new { c = CompanyId, n = nivel1, v = Aprobador1 },
                new { c = CompanyId, n = nivel2, v = Aprobador2 }
            }, Transaction);
    }

    private Task<int> CrearNivelAsync(short nivel, string descripcion, decimal desde)
        => Connection.ExecuteScalarAsync<int>(
            @"INSERT INTO public.cfg_aprobacion_nivel (company_id, documento, nivel, descripcion, monto_desde, activo)
              VALUES (@c, @d, @n, @desc, @desde, true) RETURNING id;",
            new { c = CompanyId, d = DocumentosAprobacion.OrdenCompra, n = nivel, desc = descripcion, desde },
            Transaction);

    /// <summary>Orden con un renglón, creada por SQL: lo que se prueba es la máquina de estados.</summary>
    private async Task<int> CrearOrdenAsync(short estado)
    {
        var numero = await Connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(numero), 0) + 1 FROM public.alm_orden_compra WHERE company_id = @c;",
            new { c = CompanyId }, Transaction);

        var id = await Connection.ExecuteScalarAsync<int>(
            @"INSERT INTO public.alm_orden_compra
                     (company_id, numero, fecha, cod_proveedor, estado, sub_total, total, usuariocreacion)
              VALUES (@c, @n, CURRENT_DATE, 'TEST-APR', @estado, @total, @total, @creador) RETURNING id;",
            new { c = CompanyId, n = numero, estado, total = TotalOrden, creador = Creador }, Transaction);

        await Connection.ExecuteAsync(
            @"INSERT INTO public.alm_orden_compra_detalle
                     (company_id, orden_compra_id, articulo_id, descripcion, cantidad_pedida, costo_unitario, total)
              VALUES (@c, @o, @a, 'Renglón de prueba', 1, @total, @total);",
            new { c = CompanyId, o = id, a = _articuloId, total = TotalOrden }, Transaction);

        return id;
    }

    private Task<short> EstadoAsync(int id)
        => Connection.ExecuteScalarAsync<short>(
            "SELECT estado FROM public.alm_orden_compra WHERE company_id = @c AND id = @id;",
            new { c = CompanyId, id }, Transaction);

    private Task<string?> AprobadoPorAsync(int id)
        => Connection.ExecuteScalarAsync<string?>(
            "SELECT aprobado_por FROM public.alm_orden_compra WHERE company_id = @c AND id = @id;",
            new { c = CompanyId, id }, Transaction);

    private Task<int> ContarFlujoAsync(int id)
        => Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.alm_orden_compra_aprobacion WHERE company_id = @c AND orden_compra_id = @id;",
            new { c = CompanyId, id }, Transaction);

    private Task<int> ContarBitacoraAsync(int id, string accion)
        => Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.apr_bitacora WHERE company_id = @c AND documento_id = @id AND accion = @a;",
            new { c = CompanyId, id, a = accion }, Transaction);

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
