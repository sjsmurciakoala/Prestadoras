using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Caja;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Cobranza;
using SIAD.Services.Cobros;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Cobros;

/// <summary>
/// Motor único de cobro (unificación cobranza F2 — CobroService). Verifica las
/// reglas únicas del plan §4 sobre una empresa sintética: cobro total y parcial
/// por documento, FIFO por línea, sesión de caja obligatoria, idempotencia por
/// referencia externa, folio por empresa, dual-write hacia transaccion_abonado
/// y reverso sin DELETE con restitución exacta por línea.
/// </summary>
[Collection("Postgres")]
public sealed class CobroMotorTests : IntegrationTestBase, IAsyncLifetime
{
    private const long Empresa = 9996;   // sintética; todo se revierte por rollback
    private const string Clave = "COBRO-01";
    private const string Cajero = "cajero_f2";

    private SiadDbContext? _context;
    private ICobroService? _motor;

    public CobroMotorTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(Empresa));
        _context.Database.UseTransaction(Transaction);

        _motor = new CobroService(
            _context,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService());
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ------------------------------------------------------------------ setup

    private async Task PrepararEmpresaAsync(bool conSesion = true)
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X996', 'Cobros', 'Empresa Cobros F2', 'RTN-C', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING",
            new { id = Empresa }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_documento_secuencia
                (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'RECIBO_PAGO', 0, 'REC-', 8, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING",
            new { id = Empresa }, Transaction));

        if (conSesion)
        {
            await Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
                VALUES (@id, @usuario, now(), 'ABIERTA')",
                new { id = Empresa, usuario = Cajero }, Transaction));
        }
    }

    /// <summary>Crea una factura activa con dos líneas (60 + 40 = 100).</summary>
    private async Task<int> CrearFacturaAsync(decimal linea1 = 60m, decimal linea2 = 40m, string sufijo = "A")
    {
        var facturaId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id, saldototal)
            VALUES (@companyId, @num, @clave, 'F', '2026', '7', current_date, 'A', 'S', 1, @total)
            RETURNING id",
            new { companyId = Empresa, num = $"F2-{sufijo}", clave = Clave, total = linea1 + linea2 },
            Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, montovalor, montovalor_saldo)
            VALUES (@companyId, @facturaId, 'AGUA_POTABLE', 'AGUA_POTABLE', @m1, @m1),
                   (@companyId, @facturaId, 'ALCANTARILLADO', 'ALCANTARILLADO', @m2, @m2)",
            new { companyId = Empresa, facturaId, m1 = linea1, m2 = linea2 }, Transaction));

        // F7 H2c: sin cargo espejo — la factura ES el documento.

        return facturaId;
    }

    private CobroCrearDto Cobro(int facturaId, decimal monto, string? referencia = null) => new()
    {
        Canal = CanalCobro.Caja,
        ClienteClave = Clave,
        Usuario = Cajero,
        FormaPago = "EFECTIVO",
        ReferenciaExterna = referencia,
        Aplicaciones = [new CobroAplicacionDto { DocumentoTipo = DocumentoCobroTipo.Factura, FacturaId = facturaId, Monto = monto }]
    };

    // ------------------------------------------------------------------ tests

    [SkippableFact]
    public async Task Cobro_total_salda_la_factura_y_escribe_el_modelo_nuevo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var facturaId = await CrearFacturaAsync();

        var result = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 100m));

        Assert.True(result.Success, result.Message);
        var data = Assert.IsType<CobroResultadoDto>(result.Data);
        Assert.Equal("REC-00000001", data.NumeroRecibo);
        Assert.Equal(100m, data.MontoTotal);
        Assert.False(data.Idempotente);

        // Factura saldada, con espejo numérico correcto (F1)
        var factura = await Connection.QuerySingleAsync<(string estado, short estadoId)>(new CommandDefinition(
            "SELECT estado, estado_id FROM public.factura WHERE id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal("C", factura.estado);
        Assert.Equal((short)2, factura.estadoId);

        var saldoDetalles = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(montovalor_saldo), -1) FROM public.factura_detalle WHERE factura_id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal(0m, saldoDetalles);

        // Modelo nuevo: pago aplicado línea a línea, invariante de suma
        var pago = await Connection.QuerySingleAsync<(short estadoId, short canalId, int? sesionId, decimal monto)>(new CommandDefinition(
            "SELECT estado_id, canal_id, sesion_caja_id, monto_total FROM public.adm_pago WHERE pago_id = @id",
            new { id = data.PagoId }, Transaction));
        Assert.Equal((short)1, pago.estadoId);
        Assert.Equal(CanalCobro.Caja, pago.canalId);
        Assert.NotNull(pago.sesionId);   // sesión obligatoria SIEMPRE poblada

        var aplicaciones = (await Connection.QueryAsync<decimal>(new CommandDefinition(
            "SELECT monto_aplicado FROM public.adm_pago_aplicacion WHERE pago_id = @id ORDER BY aplicacion_id",
            new { id = data.PagoId }, Transaction))).ToList();
        Assert.Equal(2, aplicaciones.Count);          // una por línea (60 + 40)
        Assert.Equal(100m, aplicaciones.Sum());

        // F7 H2c: se acabó el espejo legacy — el cobro vive SOLO como documento
        // del motor (adm_pago con su sesión de caja y estado APLICADO).
        Assert.Equal(0, data.TransaccionId);
        var doc = await Connection.QuerySingleAsync<(short canal, short estado, int? sesion, int? taIde)>(new CommandDefinition(
            "SELECT canal_id, estado_id, sesion_caja_id, transaccion_abonado_ide FROM public.adm_pago WHERE pago_id = @id",
            new { id = data.PagoId }, Transaction));
        Assert.Equal(CanalCobro.Caja, doc.canal);
        Assert.Equal(EstadoPago.Aplicado, doc.estado);
        Assert.NotNull(doc.sesion);
        Assert.Null(doc.taIde);
        var espejos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.transaccion_abonado WHERE company_id = @id AND tipotransaccion = '202'",
            new { id = Empresa }, Transaction));
        Assert.Equal(0L, espejos);

        // Saldo del cliente en cero (cargo 100 − pago 100)
        Assert.Equal(0m, data.NuevoSaldoCliente);
    }

    [SkippableFact]
    public async Task Cobro_parcial_deja_la_factura_en_B_con_fifo_por_linea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var facturaId = await CrearFacturaAsync();

        var result = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 70m));

        Assert.True(result.Success, result.Message);

        var factura = await Connection.QuerySingleAsync<(string estado, short estadoId)>(new CommandDefinition(
            "SELECT estado, estado_id FROM public.factura WHERE id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal("B", factura.estado);
        Assert.Equal((short)4, factura.estadoId);   // Parcialmente abonada (F1)

        // FIFO: primera línea (60) saldada, segunda (40) queda en 30
        var saldos = (await Connection.QueryAsync<decimal>(new CommandDefinition(
            "SELECT montovalor_saldo FROM public.factura_detalle WHERE factura_id = @facturaId ORDER BY id",
            new { facturaId }, Transaction))).ToList();
        Assert.Equal([0m, 30m], saldos);
    }

    [SkippableFact]
    public async Task Canal_caja_sin_sesion_abierta_rechaza_el_cobro()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync(conSesion: false);
        var facturaId = await CrearFacturaAsync();

        var result = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 100m));

        Assert.False(result.Success);
        Assert.Contains("sesión de caja abierta", result.Message);
    }

    [SkippableFact]
    public async Task Monto_mayor_al_saldo_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var facturaId = await CrearFacturaAsync();

        var result = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 150m));

        Assert.False(result.Success);
        Assert.Contains("excede el saldo", result.Message);
    }

    [SkippableFact]
    public async Task Referencia_externa_repetida_es_idempotente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var facturaId = await CrearFacturaAsync();

        var primero = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 40m, referencia: "REF-F2-1"));
        Assert.True(primero.Success, primero.Message);
        var datosPrimero = Assert.IsType<CobroResultadoDto>(primero.Data);

        var replay = await _motor.RegistrarCobroAsync(Cobro(facturaId, 40m, referencia: "REF-F2-1"));
        Assert.True(replay.Success, replay.Message);
        var datosReplay = Assert.IsType<CobroResultadoDto>(replay.Data);
        Assert.True(datosReplay.Idempotente);
        Assert.Equal(datosPrimero.PagoId, datosReplay.PagoId);

        // El replay NO volvió a rebajar la factura ni creó otro pago
        var pagos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.adm_pago WHERE company_id = @id AND referencia_externa = 'REF-F2-1'",
            new { id = Empresa }, Transaction));
        Assert.Equal(1L, pagos);

        var saldoDetalles = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT SUM(montovalor_saldo) FROM public.factura_detalle WHERE factura_id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal(60m, saldoDetalles);
    }

    [SkippableFact]
    public async Task Un_cobro_puede_aplicar_a_varias_facturas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var f1 = await CrearFacturaAsync(60m, 40m, "M1");
        var f2 = await CrearFacturaAsync(30m, 20m, "M2");

        var dto = Cobro(f1, 100m);
        dto.Aplicaciones.Add(new CobroAplicacionDto { DocumentoTipo = DocumentoCobroTipo.Factura, FacturaId = f2, Monto = 50m });

        var result = await _motor!.RegistrarCobroAsync(dto);

        Assert.True(result.Success, result.Message);
        var data = Assert.IsType<CobroResultadoDto>(result.Data);
        Assert.Equal(150m, data.MontoTotal);
        Assert.Equal(2, data.Aplicaciones.Count);
        Assert.All(data.Aplicaciones, a => Assert.Equal("C", a.EstadoFactura));

        // Un solo documento de pago para todo el cobro
        var pagos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.adm_pago WHERE company_id = @id", new { id = Empresa }, Transaction));
        Assert.Equal(1L, pagos);
    }

    [SkippableFact]
    public async Task Reverso_restituye_por_linea_y_no_borra_nada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var facturaId = await CrearFacturaAsync();

        var cobro = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 70m));
        Assert.True(cobro.Success, cobro.Message);
        var datos = Assert.IsType<CobroResultadoDto>(cobro.Data);

        var filasAntes = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.transaccion_abonado WHERE company_id = @id", new { id = Empresa }, Transaction));

        var reverso = await _motor.ReversarCobroAsync(new CobroReversoDto
        {
            PagoId = datos.PagoId,
            Usuario = Cajero,
            Motivo = "prueba F2"
        });
        Assert.True(reverso.Success, reverso.Message);

        // F7 H2c: nunca DELETE — el documento del motor queda ANULADO y sus
        // aplicaciones se conservan como auditoría (ya no hay espejo legacy).
        var estadoPago = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.adm_pago WHERE pago_id = @id",
            new { id = datos.PagoId }, Transaction));
        Assert.Equal(EstadoPago.Anulado, estadoPago);

        var aplicaciones = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.adm_pago_aplicacion WHERE pago_id = @id",
            new { id = datos.PagoId }, Transaction));
        Assert.True(aplicaciones > 0);

        // Restitución exacta por línea → factura vuelve a Activa con saldo completo
        var factura = await Connection.QuerySingleAsync<(string estado, decimal saldo)>(new CommandDefinition(@"
            SELECT f.estado, (SELECT SUM(d.montovalor_saldo) FROM public.factura_detalle d WHERE d.factura_id = f.id) AS saldo
            FROM public.factura f WHERE f.id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal("A", factura.estado);
        Assert.Equal(100m, factura.saldo);

        var pago = await Connection.QuerySingleAsync<(short estadoId, string? motivo)>(new CommandDefinition(
            "SELECT estado_id, motivo_reverso FROM public.adm_pago WHERE pago_id = @id",
            new { id = datos.PagoId }, Transaction));
        Assert.Equal((short)3, pago.estadoId);
        Assert.Equal("prueba F2", pago.motivo);
    }

    [SkippableFact]
    public async Task Folio_es_consecutivo_por_empresa()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var f1 = await CrearFacturaAsync(60m, 40m, "S1");
        var f2 = await CrearFacturaAsync(30m, 20m, "S2");

        var r1 = await _motor!.RegistrarCobroAsync(Cobro(f1, 100m));
        var r2 = await _motor.RegistrarCobroAsync(Cobro(f2, 50m));

        Assert.True(r1.Success, r1.Message);
        Assert.True(r2.Success, r2.Message);
        Assert.Equal("REC-00000001", ((CobroResultadoDto)r1.Data!).NumeroRecibo);
        Assert.Equal("REC-00000002", ((CobroResultadoDto)r2.Data!).NumeroRecibo);
    }

    [SkippableFact]
    public async Task Aplicacion_por_porcentajes_prioriza_otros_cargos_y_distribuye_servicios()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();

        // Config estilo "Abono Normal automático": Agua 60 / Alcantarillado 30 /
        // F. Ambiental 5 / Ersaps 5 (suma 100 exacta — requisito para aplicar).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_desglose_abono_porcentaje (company_id, item_codigo, porcentaje, usuario)
            VALUES (@id, 'AGUA_POTABLE', 60, 't'), (@id, 'ALCANTARILLADO', 30, 't'),
                   (@id, 'TASA_AMBIENTAL', 5, 't'), (@id, 'TASA_SVA_ERSAPS', 5, 't')",
            new { id = Empresa }, Transaction));

        // Factura con AGUA 60 + ALCANTARILLADO 40 + una línea NO configurada (OTROS 20).
        var facturaId = await CrearFacturaAsync(60m, 40m, "PCT");
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, montovalor, montovalor_saldo)
            VALUES (@companyId, @facturaId, 'OTROS_CARGOS', 'OTROS_CARGOS', 20, 20);
            UPDATE public.factura SET saldototal = 120 WHERE id = @facturaId;",
            new { companyId = Empresa, facturaId }, Transaction));

        // Pago parcial de 65: primero OTROS (20); el resto (45) se reparte entre
        // AGUA y ALC renormalizado (60/90 y 30/90) → 30 y 15.
        var result = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 65m));
        Assert.True(result.Success, result.Message);

        var saldos = (await Connection.QueryAsync<(string codigo, decimal saldo)>(new CommandDefinition(
            "SELECT codigo, montovalor_saldo FROM public.factura_detalle WHERE factura_id = @facturaId ORDER BY id",
            new { facturaId }, Transaction))).ToDictionary(x => x.codigo, x => x.saldo);

        Assert.Equal(0m, saldos["OTROS_CARGOS"]);       // prioridad: otros cargos primero
        Assert.Equal(30m, saldos["AGUA_POTABLE"]);      // 60 − 30
        Assert.Equal(25m, saldos["ALCANTARILLADO"]);    // 40 − 15
    }

    [SkippableFact]
    public async Task Recibo_pendiente_se_concilia_solo_al_saldarse_la_factura()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var facturaId = await CrearFacturaAsync(60m, 40m, "PEND");
        var numRecibo = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT numrecibo FROM public.factura WHERE id = @facturaId", new { facturaId }, Transaction));

        // Recibo pendiente "para banco": NO rebaja la factura.
        var pendienteId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.transaccion_abonado
                (company_id, cliente_clave, recibo, tipotransaccion, estado, creditos, debitos, descripcion)
            VALUES (@companyId, @clave, @recibo, '202', 'P', 50, 0, 'Recibo pendiente de pago')
            RETURNING ide",
            new { companyId = Empresa, clave = Clave, recibo = (decimal)numRecibo }, Transaction));

        var saldoIntacto = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT SUM(montovalor_saldo) FROM public.factura_detalle WHERE factura_id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal(100m, saldoIntacto);

        // La factura se salda por caja (podría ser el WS: el trigger es el mismo)
        var cobro = await _motor!.RegistrarCobroAsync(Cobro(facturaId, 100m));
        Assert.True(cobro.Success, cobro.Message);

        var pendiente = await Connection.QuerySingleAsync<(string estado, short? estadoPago, string desc)>(new CommandDefinition(
            "SELECT estado, estado_pago_id, descripcion FROM public.transaccion_abonado WHERE ide = @id",
            new { id = pendienteId }, Transaction));
        Assert.Equal("A", pendiente.estado);            // anulado automáticamente
        Assert.Equal((short)3, pendiente.estadoPago);   // ANULADO
        Assert.StartsWith("CUBIERTO:", pendiente.desc); // conciliado como cubierto
    }

    // ------------------------------------------------------------------ stubs

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }

    private sealed class StubBanTransaccionesService : IBanTransaccionesService
    {
        public Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(long companyId, long? bancoId = null, long? bancoCuentaId = null, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, bool incluirAnuladas = false, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<BanTransaccionListDto?> GetTransaccionByIdAsync(long banKardexId, long companyId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<BanTransaccionDetalleDto?> GetTransaccionDetalleAsync(long banKardexId, long companyId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<EstadoCuentaDto?> GetEstadoCuentaAsync(long companyId, long bancoCuentaId, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoAsync(long bancoCuentaId, string idTipoTransaccion, DateOnly fechaMovimiento, string descripcion, string? referencia, string? sourceDocument, decimal tasaCambio, decimal monto, IReadOnlyList<BanTransaccionContraLineaDto> contraCuentas, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException("Los tests del motor F2 cubren solo EFECTIVO.");
        public Task<(long BanKardexIdAnulacion, decimal SaldoResultante)> AnularMovimientoAsync(long bancoCuentaId, long banKardexIdOriginal, string motivo, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StubCorteMasivoService : ICorteMasivoService
    {
        public Task<CorteMasivoHdrDto> GenerarAsync(GenerarCorteMasivoRequest request, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CorteMasivoHdrDto>> ListarAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CorteMasivoDetalleDto?> ObtenerDetalleAsync(int hdrId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CorteMasivoDetalleDto?> ObtenerParaReimpresionAsync(int hdrId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<int> CancelarOrdenesCorteClienteAsync(string clienteClave, string usuario, CancellationToken ct = default)
            => Task.FromResult(0);
    }
}
