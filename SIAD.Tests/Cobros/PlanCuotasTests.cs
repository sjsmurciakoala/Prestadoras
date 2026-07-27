using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Cobranza;
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
            SELECT COALESCE(SUM(COALESCE(debitos,0) - COALESCE(creditos,0)), 0)
            FROM public.vw_transaccion_abonado_vigente
            WHERE company_id = @companyId AND cliente_clave = @clave",
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

        // Mueren los asientos PLAN/PLAN-CUOTA; solo queda la prima (deuda nueva).
        var espejos = (await Connection.QueryAsync<string>(new CommandDefinition(@"
            SELECT tipotransaccion FROM public.transaccion_abonado
            WHERE company_id = @C AND cliente_clave = @Clave AND tipotransaccion LIKE 'PLAN%'",
            new { C = Empresa, Clave }, Transaction))).ToList();
        Assert.Equal(["PLAN-PR"], espejos);

        // Equivalencia dual-write: documentos (cuotas 110) == legacy (100 + prima).
        var saldoDocs = await SaldoDocumentosAsync();
        var saldoLegacy = await SaldoLegacyAsync();
        Assert.Equal(110m, saldoDocs);
        Assert.Equal(saldoLegacy, saldoDocs);

        // El plan queda ACTIVO.
        var estadoPlan = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT estado_id FROM public.cln_plan_pago_hdr WHERE company_id = @C", new { C = Empresa }, Transaction));
        Assert.Equal((short)1, estadoPlan);
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
}
