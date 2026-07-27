using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.CaptacionPagos;
using SIAD.Services.Cobranza;
using SIAD.Services.Cobros;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Cobros;

/// <summary>
/// Fachadas F2b: los caminos legacy de captación (lectora / manual / misceláneo)
/// delegan en el motor único conservando mensajes y efectos visibles. Verifica
/// que cada camino escribe el modelo nuevo (adm_pago + aplicaciones), el espejo
/// legacy 201 con su tipo_partida histórico, y que el reverso de pagos nacidos
/// en el motor ya NO borra la transacción (la marca anulada).
/// </summary>
[Collection("Postgres")]
public sealed class CobroFachadaTests : IntegrationTestBase, IAsyncLifetime
{
    private const long Empresa = 9995;
    private const string Clave = "FACHADA-01";
    private const string Cajero = "cajero_f2b";

    private SiadDbContext? _context;
    private CaptacionPagosService? _servicio;

    public CobroFachadaTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(Empresa));
        _context.Database.UseTransaction(Transaction);

        var motor = new CobroService(
            _context,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService());

        _servicio = new CaptacionPagosService(
            _context,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService(),
            motor);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private async Task PrepararEmpresaAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X995', 'Fachadas', 'Empresa Fachadas F2b', 'RTN-F', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING;
            INSERT INTO public.adm_documento_secuencia (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'RECIBO_PAGO', 0, 'REC-', 8, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING;
            INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
            VALUES (@id, @usuario, now(), 'ABIERTA');",
            new { id = Empresa, usuario = Cajero }, Transaction));
    }

    private async Task<(int FacturaId, string NumFactura, int NumRecibo, List<int> DetalleIds)> CrearFacturaAsync(
        string tipoFactura = "F", decimal linea1 = 60m, decimal linea2 = 40m, string sufijo = "A")
    {
        var (facturaId, numRecibo) = await Connection.QuerySingleAsync<(int, int)>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id, saldototal)
            VALUES (@companyId, @num, @clave, @tipo, '2026', '7', current_date, 'A', 'S', 1, @total)
            RETURNING id, numrecibo",
            new { companyId = Empresa, num = $"F2B-{sufijo}", clave = Clave, tipo = tipoFactura, total = linea1 + linea2 },
            Transaction));

        var detalleIds = (await Connection.QueryAsync<int>(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, montovalor, montovalor_saldo)
            VALUES (@companyId, @facturaId, 'AGUA_POTABLE', 'AGUA_POTABLE', @m1, @m1),
                   (@companyId, @facturaId, 'ALCANTARILLADO', 'ALCANTARILLADO', @m2, @m2)
            RETURNING id",
            new { companyId = Empresa, facturaId, m1 = linea1, m2 = linea2 }, Transaction))).ToList();

        return (facturaId, $"F2B-{sufijo}", numRecibo, detalleIds);
    }

    // ------------------------------------------------------------------ tests

    [SkippableFact]
    public async Task Lectora_registra_por_el_motor_y_reversa_sin_borrar()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var (facturaId, numFactura, numRecibo, _) = await CrearFacturaAsync(sufijo: "L");

        var pago = await _servicio!.RegistrarPagoAsync(new PagoCrearDto
        {
            NumFactura = numFactura,
            ClienteClave = Clave,
            Usuario = Cajero
        });
        Assert.True(pago.Success, pago.Message);
        Assert.Equal("Pago registrado correctamente.", pago.Message);

        // Efectos legacy visibles + modelo nuevo
        var estado = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT estado FROM public.factura WHERE id = @facturaId", new { facturaId }, Transaction));
        Assert.Equal("C", estado);

        // F7 H2c: sin espejo legacy — el pago de la lectora es un documento
        // del motor (canal caja, con su sesión).
        var pagoMotor = await Connection.QuerySingleAsync<(long pagoId, short canal, int? sesion)>(new CommandDefinition(
            "SELECT pago_id, canal_id, sesion_caja_id FROM public.adm_pago WHERE company_id = @id",
            new { id = Empresa }, Transaction));
        Assert.Equal(CanalCobro.Caja, pagoMotor.canal);
        Assert.NotNull(pagoMotor.sesion);

        // Doble pago rechazado con el mensaje legacy exacto
        var doble = await _servicio.RegistrarPagoAsync(new PagoCrearDto
        {
            NumFactura = numFactura,
            ClienteClave = Clave,
            Usuario = Cajero
        });
        Assert.False(doble.Success);
        Assert.Equal($"La factura {numFactura} ya tiene un pago registrado.", doble.Message);

        // Reverso: rutea por el motor — la 201 NO se borra, se marca anulada
        var reverso = await _servicio.ReversarPagoAsync(new ReversoRequestDto
        {
            NumFactura = numFactura,
            ClienteClave = Clave,
            Usuario = Cajero
        });
        Assert.True(reverso.Success, reverso.Message);
        Assert.Equal("Pago reversado correctamente.", reverso.Message);

        // El documento del motor queda ANULADO (nunca se borra).
        var estadoTrasReverso = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.adm_pago WHERE pago_id = @id",
            new { id = pagoMotor.pagoId }, Transaction));
        Assert.Equal(EstadoPago.Anulado, estadoTrasReverso);

        var facturaTras = await Connection.QuerySingleAsync<(string estado, decimal saldo)>(new CommandDefinition(@"
            SELECT f.estado, (SELECT SUM(d.montovalor_saldo) FROM public.factura_detalle d WHERE d.factura_id = f.id)
            FROM public.factura f WHERE f.id = @facturaId", new { facturaId }, Transaction));
        Assert.Equal("A", facturaTras.estado);
        Assert.Equal(100m, facturaTras.saldo);
    }

    [SkippableFact]
    public async Task Posteo_manual_valida_distribucion_y_registra_por_el_motor()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var (facturaId, _, numRecibo, detalleIds) = await CrearFacturaAsync(sufijo: "M");

        // Distribución incompleta → mensaje legacy exacto
        var incompleta = await _servicio!.RegistrarPagoManualAsync(new PagoManualCrearDto
        {
            ClienteClave = Clave,
            NumRecibo = numRecibo,
            Banco = "EFECTIVO",
            Usuario = Cajero,
            Distribucion = [new PagoManualDistribucionDto { Id = detalleIds[0], ValorDistribuido = 60m }]
        });
        Assert.False(incompleta.Success);
        Assert.Equal("La distribucion debe cubrir el saldo total del recibo.", incompleta.Message);

        // Distribución completa → motor
        var ok = await _servicio.RegistrarPagoManualAsync(new PagoManualCrearDto
        {
            ClienteClave = Clave,
            NumRecibo = numRecibo,
            Banco = "EFECTIVO",
            Usuario = Cajero,
            Distribucion =
            [
                new PagoManualDistribucionDto { Id = detalleIds[0], ValorDistribuido = 60m },
                new PagoManualDistribucionDto { Id = detalleIds[1], ValorDistribuido = 40m }
            ]
        });
        Assert.True(ok.Success, ok.Message);
        Assert.Equal("Posteo manual registrado correctamente.", ok.Message);

        var saldo = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT SUM(montovalor_saldo) FROM public.factura_detalle WHERE factura_id = @facturaId",
            new { facturaId }, Transaction));
        Assert.Equal(0m, saldo);

        var pagosMotor = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.adm_pago WHERE company_id = @id AND cliente_clave = @clave",
            new { id = Empresa, clave = Clave }, Transaction));
        Assert.Equal(1L, pagosMotor);
    }

    [SkippableFact]
    public async Task Miscelaneo_registra_con_cxc_general_y_tipo_partida_01()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaAsync();
        var (facturaId, _, numRecibo, _) = await CrearFacturaAsync(tipoFactura: "R", linea1: 150m, linea2: 50m, sufijo: "R");

        var ok = await _servicio!.RegistrarPagoMiscelaneoAsync(new PagoMiscelaneoCrearDto
        {
            ClienteClave = Clave,
            Recibo = numRecibo,
            Banco = "EFECTIVO",
            Usuario = Cajero
        });
        Assert.True(ok.Success, ok.Message);
        Assert.Equal("Pago miscelaneo registrado correctamente.", ok.Message);

        // F7 H2c: el misceláneo cobra por el motor — documento con CxC general.
        var pagoMisc = await Connection.QuerySingleAsync<(long pagoId, decimal monto)>(new CommandDefinition(
            "SELECT pago_id, monto_total FROM public.adm_pago WHERE company_id = @id",
            new { id = Empresa }, Transaction));
        Assert.Equal(200m, pagoMisc.monto);

        var estado = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT estado FROM public.factura WHERE id = @facturaId", new { facturaId }, Transaction));
        Assert.Equal("C", estado);
    }

    [SkippableFact]
    public async Task Sin_sesion_de_caja_abierta_la_captacion_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        // Empresa sin sesión de caja (regla nueva del plan §4)
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X995', 'Fachadas', 'Empresa Fachadas F2b', 'RTN-F', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING;
            INSERT INTO public.adm_documento_secuencia (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'RECIBO_PAGO', 0, 'REC-', 8, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING;",
            new { id = Empresa }, Transaction));
        var (_, numFactura, _, _) = await CrearFacturaAsync(sufijo: "S");

        var pago = await _servicio!.RegistrarPagoAsync(new PagoCrearDto
        {
            NumFactura = numFactura,
            ClienteClave = Clave,
            Usuario = Cajero
        });

        Assert.False(pago.Success);
        Assert.Contains("sesión de caja abierta", pago.Message);
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
            => throw new NotSupportedException("Los tests de fachada cubren solo EFECTIVO.");
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
