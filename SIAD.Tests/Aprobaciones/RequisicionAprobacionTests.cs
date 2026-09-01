using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Almacen;
using SIAD.Services.Aprobaciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Aprobaciones;

/// <summary>
/// La requisición usando el mismo motor que la orden de compra (F7): el estado «En revisión» deja
/// de ser un solo escalón y pasa a juntar las firmas que exija el total.
/// <para>
/// Incluye la <b>no-regresión</b>: con el control apagado, aprobar sigue siendo de un clic, que es
/// como funciona hoy en producción.
/// </para>
/// </summary>
[Collection("Postgres")]
public class RequisicionAprobacionTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RequisicionDocumentoService? _requisiciones;
    private readonly TestCurrentUserService _usuario = new();

    private int _bodegaId;
    private int _articuloId;

    private const string Solicitante = "solicitante@test.com";
    private const string Aprobador1 = "jefe1@test.com";
    private const string Aprobador2 = "jefe2@test.com";

    public RequisicionAprobacionTests(PostgresFixture fixture) : base(fixture)
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

        _requisiciones = new RequisicionDocumentoService(
            _context, empresa, new AprobacionService(_context, empresa, _usuario));

        _bodegaId = await Connection.ExecuteScalarAsync<int>(
            "SELECT id FROM public.alm_bodega WHERE company_id = @c ORDER BY id LIMIT 1;",
            new { c = CompanyId }, Transaction);

        _articuloId = await Connection.ExecuteScalarAsync<int>(
            "SELECT id FROM public.alm_articulo WHERE company_id = @c ORDER BY id LIMIT 1;",
            new { c = CompanyId }, Transaction);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Con_el_control_apagado_aprobar_sigue_siendo_de_un_clic()
    {
        Skip.IfNot(Fixture.Available);

        var id = await CrearRequisicionAsync(EstadoRequisicionHdr.EnRevision, 75000m);

        var aprobada = await _requisiciones!.AprobarAsync(id, "tester");

        Assert.Equal(EstadoRequisicionHdr.Aprobada, aprobada.Estado);
        Assert.Equal(0, await ContarFlujoAsync(id));
    }

    [SkippableFact]
    public async Task Con_escalera_la_requisicion_junta_firmas_antes_de_aprobarse()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearRequisicionAsync(EstadoRequisicionHdr.Borrador, 75000m);

        // Enviar a revisión abre la escalera: 75,000 exige los dos niveles.
        var enviada = await _requisiciones!.EnviarARevisionAsync(id, Solicitante);
        Assert.Equal(EstadoRequisicionHdr.EnRevision, enviada.Estado);
        Assert.Equal(2, await ContarFlujoAsync(id));

        // La primera firma NO aprueba: la requisición sigue en revisión.
        _usuario.Establecer(Aprobador1);
        var tras1 = await _requisiciones.AprobarAsync(id, Aprobador1);
        Assert.Equal(EstadoRequisicionHdr.EnRevision, tras1.Estado);

        // La última sí.
        _usuario.Establecer(Aprobador2);
        var tras2 = await _requisiciones.AprobarAsync(id, Aprobador2);
        Assert.Equal(EstadoRequisicionHdr.Aprobada, tras2.Estado);
        Assert.Equal(Aprobador2, tras2.AprobadoPor);
    }

    [SkippableFact]
    public async Task Quien_no_es_aprobador_no_puede_firmar_la_requisicion()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearRequisicionAsync(EstadoRequisicionHdr.Borrador, 75000m);
        await _requisiciones!.EnviarARevisionAsync(id, Solicitante);

        _usuario.Establecer("intruso@test.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _requisiciones.AprobarAsync(id, "intruso@test.com"));
    }

    [SkippableFact]
    public async Task El_solicitante_no_puede_aprobar_su_propia_requisicion()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        // La crea quien SÍ es aprobador del primer nivel.
        var id = await CrearRequisicionAsync(EstadoRequisicionHdr.Borrador, 75000m, Aprobador1);
        await _requisiciones!.EnviarARevisionAsync(id, Aprobador1);

        _usuario.Establecer(Aprobador1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _requisiciones.AprobarAsync(id, Aprobador1));

        Assert.Contains("usted mismo creó", error.Message);
    }

    [SkippableFact]
    public async Task Rechazar_con_escalera_deja_rastro_en_la_bitacora()
    {
        Skip.IfNot(Fixture.Available);
        await EncenderEscaleraAsync();

        var id = await CrearRequisicionAsync(EstadoRequisicionHdr.Borrador, 75000m);
        await _requisiciones!.EnviarARevisionAsync(id, Solicitante);

        _usuario.Establecer(Aprobador1);
        var rechazada = await _requisiciones.RechazarAsync(id, "No hay presupuesto", Aprobador1);

        Assert.Equal(EstadoRequisicionHdr.Rechazada, rechazada.Estado);

        var enBitacora = await Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.apr_bitacora WHERE company_id = @c AND documento = @d " +
            "  AND documento_id = @id AND accion = @a;",
            new { c = CompanyId, d = DocumentosAprobacion.Requisicion, id, a = AccionAprobacion.Rechazada },
            Transaction);

        Assert.Equal(1, enBitacora);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task EncenderEscaleraAsync()
    {
        await Connection.ExecuteAsync(
            @"INSERT INTO public.cfg_aprobacion_control (company_id, documento, modo, permite_autoaprobacion)
              VALUES (@c, @d, 1, false)
              ON CONFLICT (company_id, documento) DO UPDATE SET modo = 1, permite_autoaprobacion = false;",
            new { c = CompanyId, d = DocumentosAprobacion.Requisicion }, Transaction);

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
            new { c = CompanyId, d = DocumentosAprobacion.Requisicion, n = nivel, desc = descripcion, desde },
            Transaction);

    /// <summary>
    /// Requisición con un renglón, creada por SQL: lo que se prueba es la máquina de estados, no
    /// la captura.
    /// </summary>
    private async Task<int> CrearRequisicionAsync(short estado, decimal total, string? solicita = null)
    {
        var numero = await Connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(numero), 0) + 1 FROM public.alm_requisicion_hdr WHERE company_id = @c;",
            new { c = CompanyId }, Transaction);

        var id = await Connection.ExecuteScalarAsync<int>(
            @"INSERT INTO public.alm_requisicion_hdr
                     (company_id, numero, tipo, estado, fecha, bodega_id, solicitante,
                      usuario_solicita, total, usuariocreacion)
              VALUES (@c, @n, 1, @estado, CURRENT_DATE, @bodega, 'Prueba',
                      @solicita, @total, @solicita) RETURNING id;",
            new
            {
                c = CompanyId, n = numero, estado, bodega = _bodegaId,
                solicita = solicita ?? Solicitante, total
            }, Transaction);

        // El renglón conserva la forma del histórico SIMAFI: la cantidad es `cantidad` y el valor
        // va en `valor`/`total`. Un renglón con origen SIAD exige uuid (ck_alm_requisicion_uuid_si_siad).
        await Connection.ExecuteAsync(
            @"INSERT INTO public.alm_requisicion
                     (company_id, requisicion_hdr_id, numero, articulo_id, bodega_id, descripcion,
                      cantidad, precio_unitario, valor, total, origen, uuid)
              VALUES (@c, @hdr, @n, @a, @bodega, 'Renglón de prueba',
                      1, @total, @total, @total, 'SIAD', gen_random_uuid());",
            new { c = CompanyId, hdr = id, n = numero, a = _articuloId, bodega = _bodegaId, total },
            Transaction);

        return id;
    }

    private Task<int> ContarFlujoAsync(int id)
        => Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM public.alm_requisicion_aprobacion WHERE company_id = @c AND requisicion_id = @id;",
            new { c = CompanyId, id }, Transaction);

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
