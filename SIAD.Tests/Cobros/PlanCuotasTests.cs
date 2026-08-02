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
/// Unificación cobranza F6 (2026-07-29): el plan de pago traslada la deuda por
/// APLICACIÓN DE DOCUMENTOS (compensación FIFO de líneas + registro en
/// cln_plan_pago_traslado) y las cuotas nacen documentos cobrables
/// (cln_plan_pago_dtl con estado_id/saldo_cuota; la prima es la cuota mes 0).
/// Mueren los asientos legacy PLAN y PLAN-CUOTA; SOLO la prima conserva espejo
/// (deuda nueva) para que el saldo legacy y el de documentos sigan iguales
/// durante el dual-write (auditoría que sostiene el corte de F7).
/// </summary>
[Collection("Postgres")]
public sealed class PlanCuotasTests : IntegrationTestBase, IAsyncLifetime
{
    private const long Empresa = 9995;   // sintética; rollback al final
    private const string Clave = "PLAN-01";

    private SiadDbContext? _context;
    private CobranzaService? _servicio;

    public PlanCuotasTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(Empresa));
        _context.Database.UseTransaction(Transaction);

        _servicio = new CobranzaService(
            _context,
            new TestCurrentCompanyService(Empresa),
            new StubDocumentoGenerator());
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private sealed class TestCurrentCompanyService(long companyId) : ICurrentCompanyService
    {
        public long GetCompanyId() => companyId;
    }

    private sealed class StubDocumentoGenerator : IDocumentoCobranzaGenerator
    {
        public bool Soporta(string documentoCodigo) => false;
        public DocumentoGenerado Generar(string documentoCodigo, DocumentoCobranzaDatos datos)
            => throw new NotSupportedException();
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
        public Task<(long BanKardexId, decimal SaldoResultante, long? ChequeId, decimal? NumeroCheque)> RegistrarMovimientoAsync(long bancoCuentaId, string idTipoTransaccion, DateOnly fechaMovimiento, string descripcion, string? referencia, string? sourceDocument, decimal tasaCambio, decimal monto, IReadOnlyList<BanTransaccionContraLineaDto> contraCuentas, string usuario, CancellationToken ct = default, string? beneficiarioCheque = null, string? conceptoCheque = null, string origenCheque = ChequeOrigen.Transaccion, string? descripcionPartidaBanco = null)
            => throw new NotSupportedException("Los tests del motor F2 cubren solo EFECTIVO.");
        public Task<ChequeManualResultadoDto> RegistrarChequeManualAsync(ChequeManualCreateDto dto, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException("El E2E de cuotas cubre solo EFECTIVO.");
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

    // ------------------------------------------------------------------ setup

    private async Task PrepararEmpresaYClienteAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X995', 'Planes', 'Empresa Planes F6', 'RTN-P', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING",
            new { id = Empresa }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@id, @clave, '0000000000000', 'CLIENTE PLAN F6', true)",
            new { id = Empresa, clave = Clave }, Transaction));

        // F7 H5: el correlativo del plan sale de la serie atómica.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_documento_secuencia
                (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'PLAN_PAGO', 0, '', 6, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING",
            new { id = Empresa }, Transaction));
    }

    /// <summary>Factura activa con dos líneas y su cargo espejo legacy (mismo total).</summary>
    private async Task<int> CrearFacturaConEspejoAsync(decimal linea1, decimal linea2, string sufijo)
    {
        var facturaId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id, saldototal)
            VALUES (@companyId, @num, @clave, 'F', '2026', '7', current_date, 'A', 'S', 1, @total)
            RETURNING id",
            new { companyId = Empresa, num = $"F6-{sufijo}", clave = Clave, total = linea1 + linea2 },
            Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, montovalor, montovalor_saldo)
            VALUES (@companyId, @facturaId, 'AGUA_POTABLE', 'AGUA_POTABLE', @m1, @m1),
                   (@companyId, @facturaId, 'ALCANTARILLADO', 'ALCANTARILLADO', @m2, @m2)",
            new { companyId = Empresa, facturaId, m1 = linea1, m2 = linea2 }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            SET LOCAL siad.permitir_escritura_legacy = 'on';   -- H4: tabla congelada
            INSERT INTO public.transaccion_abonado (company_id, cliente_clave, tipotransaccion, estado, debitos, creditos)
            VALUES (@companyId, @clave, 'AGUA_POTABLE', 'A', @total, 0)",
            new { companyId = Empresa, clave = Clave, total = linea1 + linea2 }, Transaction));

        return facturaId;
    }

    private Task<decimal?> SaldoDocumentosAsync() =>
        Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@companyId, @clave)",
            new { companyId = Empresa, clave = Clave }, Transaction));

    private Task<decimal?> SaldoLegacyAsync() =>
        Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(@"
            -- H5: la vista de vigencia se retiró; el filtro va inline sobre
            -- el histórico congelado (misma semántica).
            SELECT COALESCE(SUM(COALESCE(debitos,0) - COALESCE(creditos,0)), 0)
            FROM public.transaccion_abonado
            WHERE company_id = @companyId AND cliente_clave = @clave
              AND COALESCE(estado,'') NOT IN ('N','R','P')
              AND (estado_pago_id IS NULL OR estado_pago_id = 1)",
            new { companyId = Empresa, clave = Clave }, Transaction));

    private static CobranzaPlanGuardarDto Plan(decimal montoFinanciar, decimal prima, int meses) => new()
    {
        ClienteClave = Clave,
        Meses = meses,
        MontoFinanciar = montoFinanciar,
        Total = montoFinanciar + prima,
        ValorPrima = prima,
        Fecha = DateTime.Today,
        FechaPrimerPago = DateTime.Today.AddMonths(1),
        Usuario = "test-f6"
    };

    // ------------------------------------------------------------------ tests

    [SkippableFact]
    public async Task Crear_plan_compensa_documentos_y_cuotas_nacen_con_saldo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaYClienteAsync();
        var facturaId = await CrearFacturaConEspejoAsync(60m, 40m, "A");

        var resultado = await _servicio!.GuardarPlanPagoAsync(Plan(100m, 10m, 3));
        Assert.True(resultado.Success, resultado.Message);

        // La factura quedó compensada por el traslado (líneas en 0, estado C).
        var (estado, estadoId, saldoLineas) = await Connection.QueryFirstAsync<(string, short?, decimal)>(new CommandDefinition(@"
            SELECT f.estado, f.estado_id,
                   (SELECT COALESCE(SUM(d.montovalor_saldo), -1) FROM public.factura_detalle d WHERE d.factura_id = f.id)
            FROM public.factura f WHERE f.id = @Id",
            new { Id = facturaId }, Transaction));
        Assert.Equal("C", estado);
        Assert.Equal((short)2, estadoId);
        Assert.Equal(0m, saldoLineas);

        // Traslado registrado línea a línea por el monto financiado.
        var traslados = (await Connection.QueryAsync<decimal>(new CommandDefinition(
            "SELECT monto_trasladado FROM public.cln_plan_pago_traslado WHERE company_id = @C ORDER BY traslado_id",
            new { C = Empresa }, Transaction))).ToList();
        Assert.Equal([60m, 40m], traslados);

        // Cuotas: prima como mes 0 + 3 cuotas, todas activas con saldo vivo.
        var cuotas = (await Connection.QueryAsync<(int? mes, decimal? valor, short estadoId, decimal saldo)>(new CommandDefinition(@"
            SELECT d.mes, d.valorcuota, d.estado_id, d.saldo_cuota
            FROM public.cln_plan_pago_dtl d WHERE d.company_id = @C ORDER BY d.mes",
            new { C = Empresa }, Transaction))).ToList();
        Assert.Equal(4, cuotas.Count);
        Assert.Equal(0, cuotas[0].mes);
        Assert.Equal(10m, cuotas[0].saldo);
        Assert.All(cuotas, c => Assert.Equal((short)1, c.estadoId));
        Assert.Equal(110m, cuotas.Sum(c => c.saldo));

        // F7 H2c: el plan NO escribe nada en transaccion_abonado (ni traslado,
        // ni cuotas, ni prima) — vive solo en sus tablas.
        var espejos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.transaccion_abonado
            WHERE company_id = @C AND cliente_clave = @Clave AND tipotransaccion LIKE 'PLAN%'",
            new { C = Empresa, Clave }, Transaction));
        Assert.Equal(0L, espejos);

        // El saldo por documentos son las 4 cuotas (3 x 100/3 + prima 10).
        Assert.Equal(110m, await SaldoDocumentosAsync());

        // El plan queda ACTIVO.
        var estadoPlan = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.cln_plan_pago_hdr WHERE company_id = @C", new { C = Empresa }, Transaction));
        Assert.Equal((short)1, estadoPlan);
    }

    [SkippableFact]
    public async Task E2E_cobrar_cuotas_en_caja_completa_el_plan_y_reversar_lo_reabre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaYClienteAsync();
        await CrearFacturaConEspejoAsync(60m, 40m, "C");

        // Infraestructura de caja del motor (folio + sesión abierta).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_documento_secuencia
                (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'RECIBO_PAGO', 0, 'REC-', 8, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING",
            new { id = Empresa }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
            VALUES (@id, 'test-f6', now(), 'ABIERTA')",
            new { id = Empresa }, Transaction));

        // Plan sin prima de 2 cuotas (50 + 50) sobre los 100 en documentos.
        var creado = await _servicio!.GuardarPlanPagoAsync(Plan(100m, 0m, 2));
        Assert.True(creado.Success, creado.Message);

        var cuotas = (await Connection.QueryAsync<int>(new CommandDefinition(
            "SELECT id FROM public.cln_plan_pago_dtl WHERE company_id = @C ORDER BY mes",
            new { C = Empresa }, Transaction))).ToList();
        Assert.Equal(2, cuotas.Count);

        var motor = new CobroService(
            _context!,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService());

        // Cobrar la primera cuota completa: cuota Cobrada, plan sigue ACTIVO.
        var cobro1 = await motor.RegistrarCobroAsync(new CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = Clave,
            Usuario = "test-f6",
            FormaPago = "EFECTIVO",
            Aplicaciones = [new CobroAplicacionDto
            {
                DocumentoTipo = DocumentoCobroTipo.CuotaPlan,
                PlanCuotaId = cuotas[0],
                Monto = 50m
            }]
        });
        Assert.True(cobro1.Success, cobro1.Message);
        Assert.Equal(50m, await SaldoDocumentosAsync());

        var estadoPlan = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.cln_plan_pago_hdr WHERE company_id = @C", new { C = Empresa }, Transaction));
        Assert.Equal((short)1, estadoPlan);

        // Cobrar la segunda: última cuota viva → plan COMPLETADO.
        var cobro2 = await motor.RegistrarCobroAsync(new CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = Clave,
            Usuario = "test-f6",
            FormaPago = "EFECTIVO",
            Aplicaciones = [new CobroAplicacionDto
            {
                DocumentoTipo = DocumentoCobroTipo.CuotaPlan,
                PlanCuotaId = cuotas[1],
                Monto = 50m
            }]
        });
        Assert.True(cobro2.Success, cobro2.Message);
        Assert.Equal(0m, await SaldoDocumentosAsync());

        estadoPlan = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.cln_plan_pago_hdr WHERE company_id = @C", new { C = Empresa }, Transaction));
        Assert.Equal((short)2, estadoPlan);   // Completado

        // El documento del motor referencia la cuota (documento_tipo 2).
        var aplicacion = await Connection.QueryFirstAsync<(short tipo, int? cuotaId)>(new CommandDefinition(@"
            SELECT a.documento_tipo, a.plan_cuota_id
            FROM public.adm_pago_aplicacion a
            JOIN public.adm_pago p ON p.pago_id = a.pago_id
            WHERE p.company_id = @C ORDER BY a.aplicacion_id DESC LIMIT 1",
            new { C = Empresa }, Transaction));
        Assert.Equal(2, aplicacion.tipo);
        Assert.Equal(cuotas[1], aplicacion.cuotaId);

        // Reversar el último cobro: cuota vuelve viva y el plan se REABRE.
        var pagoId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT MAX(pago_id) FROM public.adm_pago WHERE company_id = @C", new { C = Empresa }, Transaction));
        var reverso = await motor.ReversarCobroAsync(new CobroReversoDto
        {
            PagoId = pagoId,
            Usuario = "test-f6",
            Motivo = "prueba F6"
        });
        Assert.True(reverso.Success, reverso.Message);

        var (cuotaEstado, cuotaSaldo) = await Connection.QueryFirstAsync<(short, decimal)>(new CommandDefinition(
            "SELECT estado_id, saldo_cuota FROM public.cln_plan_pago_dtl WHERE id = @Id",
            new { Id = cuotas[1] }, Transaction));
        Assert.Equal((short)1, cuotaEstado);   // Activa otra vez
        Assert.Equal(50m, cuotaSaldo);

        estadoPlan = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.cln_plan_pago_hdr WHERE company_id = @C", new { C = Empresa }, Transaction));
        Assert.Equal((short)1, estadoPlan);    // reabierto

        Assert.Equal(50m, await SaldoDocumentosAsync());
    }

    [SkippableFact]
    public async Task Plan_no_puede_financiar_mas_que_la_deuda_en_documentos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaYClienteAsync();
        await CrearFacturaConEspejoAsync(60m, 40m, "B");

        // Residuo migrado NO financiable hasta F7: financiar 150 con 100 en docs.
        var resultado = await _servicio!.GuardarPlanPagoAsync(Plan(150m, 0m, 3));

        Assert.False(resultado.Success);
        Assert.Contains("excede", resultado.Message, StringComparison.OrdinalIgnoreCase);

        // Nada quedó escrito: ni plan, ni traslados, ni cuotas, ni espejos PLAN%.
        var restos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT (SELECT COUNT(*) FROM public.cln_plan_pago_hdr WHERE company_id = @C)
                 + (SELECT COUNT(*) FROM public.cln_plan_pago_dtl WHERE company_id = @C)
                 + (SELECT COUNT(*) FROM public.cln_plan_pago_traslado WHERE company_id = @C)
                 + (SELECT COUNT(*) FROM public.transaccion_abonado WHERE company_id = @C AND tipotransaccion LIKE 'PLAN%')",
            new { C = Empresa }, Transaction));
        Assert.Equal(0L, restos);

        // Y la factura sigue intacta.
        Assert.Equal(100m, await SaldoDocumentosAsync());
    }

    // ------------------------------------------------------------------------
    // Pruebas operativas jul-2026 (lote 4 convenios):
    //   * ANTICIPO: una cuota con vencimiento futuro se puede cobrar antes de
    //     tiempo y fuera de orden (el motor no restringe por fecha).
    //   * ANULACIÓN: lo cobrado queda como pago histórico; el saldo de las
    //     cuotas vivas vuelve a las facturas de origen vía el traslado, la
    //     factura recupera estado pendiente y el plan queda ANULADO.
    // ------------------------------------------------------------------------

    [SkippableFact]
    public async Task Anticipo_de_cuota_futura_y_anulacion_restituyen_correctamente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararEmpresaYClienteAsync();
        var facturaId = await CrearFacturaConEspejoAsync(60m, 40m, "ANU");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_documento_secuencia
                (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'RECIBO_PAGO', 0, 'REC-', 8, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING",
            new { id = Empresa }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
            VALUES (@id, 'test-f6', now(), 'ABIERTA')",
            new { id = Empresa }, Transaction));

        var creado = await _servicio!.GuardarPlanPagoAsync(Plan(100m, 0m, 2));
        Assert.True(creado.Success, creado.Message);

        var cuotas = (await Connection.QueryAsync<int>(new CommandDefinition(
            "SELECT id FROM public.cln_plan_pago_dtl WHERE company_id = @C ORDER BY mes",
            new { C = Empresa }, Transaction))).ToList();
        Assert.Equal(2, cuotas.Count);

        var motor = new CobroService(
            _context!,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService());

        // ANTICIPO: se cobra la SEGUNDA cuota (vence en ~3 meses) antes que la primera.
        var anticipo = await motor.RegistrarCobroAsync(new CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = Clave,
            Usuario = "test-f6",
            FormaPago = "EFECTIVO",
            Aplicaciones = [new CobroAplicacionDto
            {
                DocumentoTipo = DocumentoCobroTipo.CuotaPlan,
                PlanCuotaId = cuotas[1],
                Monto = 50m
            }]
        });
        Assert.True(anticipo.Success, anticipo.Message);

        // ANULACIÓN: la primera cuota (50, viva) vuelve a la factura de origen.
        var planId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT id FROM public.cln_plan_pago_hdr WHERE company_id = @C", new { C = Empresa }, Transaction));

        var anulacion = await _servicio!.AnularPlanPagoAsync(planId, "prueba de anulación lote 4", "test-f6");
        Assert.True(anulacion.Success, anulacion.Message);

        var (estadoPlan, estadoPagoLegacy) = await Connection.QueryFirstAsync<(short, string)>(new CommandDefinition(
            "SELECT estado_id, estadopago FROM public.cln_plan_pago_hdr WHERE id = @Id",
            new { Id = planId }, Transaction));
        Assert.Equal((short)3, estadoPlan);        // EstadoPlan.Anulado
        Assert.Equal("Anulado", estadoPagoLegacy);

        var estadosCuotas = (await Connection.QueryAsync<(int? mes, short estadoId)>(new CommandDefinition(
            "SELECT mes, estado_id FROM public.cln_plan_pago_dtl WHERE idhdr = @Id ORDER BY mes",
            new { Id = planId }, Transaction))).ToList();
        Assert.Equal((short)3, estadosCuotas[0].estadoId);   // viva → anulada
        Assert.Equal((short)2, estadosCuotas[1].estadoId);   // cobrada → intacta

        // La factura recuperó los 50 no cobrados y quedó parcial.
        var (estadoFactura, saldoLineas) = await Connection.QueryFirstAsync<(string, decimal)>(new CommandDefinition(@"
            SELECT f.estado,
                   (SELECT COALESCE(SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)), 0)
                    FROM public.factura_detalle d WHERE d.factura_id = f.id)
            FROM public.factura f WHERE f.id = @Id",
            new { Id = facturaId }, Transaction));
        Assert.Equal("B", estadoFactura);
        Assert.Equal(50m, saldoLineas);

        // Saldo del cliente: ahora por la factura (50), sin cuotas fantasma.
        Assert.Equal(50m, await SaldoDocumentosAsync());

        // La marca de convenio del cliente se apagó (era su único plan activo).
        var tieneConvenio = await Connection.ExecuteScalarAsync<bool?>(new CommandDefinition(
            "SELECT maestro_cliente_tiene_convenio FROM public.cliente_maestro WHERE company_id = @C AND maestro_cliente_clave = @Clave",
            new { C = Empresa, Clave }, Transaction));
        Assert.False(tieneConvenio ?? false);
    }
}
