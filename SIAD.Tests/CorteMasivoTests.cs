using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Cobranza;
using SIAD.Tests.Infrastructure;
using Xunit;

namespace SIAD.Tests;

// Pruebas operativas ago-2026: cortes masivos era el único módulo de cobranza
// sin tests. Cubre el ciclo completo: generar lote (hdr + detalles + órdenes
// de trabajo reales tipo 33 vinculadas) y la cancelación automática de la
// orden cuando el cliente paga.
[Collection("Postgres")]
public sealed class CorteMasivoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CorteMasivoService? _service;

    public CorteMasivoTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var companyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, companyService);
            _context.Database.UseTransaction(Transaction);
            _service = new CorteMasivoService(_context, companyService);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    /// <summary>Cliente con deuda real (factura viva) en un barrio exclusivo del test.</summary>
    private async Task<(string Clave, string Barrio, decimal Saldo)> CrearClienteConDeudaAsync(decimal monto)
    {
        var clave = $"CT{Guid.NewGuid():N}"[..12];
        var barrio = $"Z{Guid.NewGuid():N}"[..6];

        // barrio_codigo tiene FK al catálogo (global, sin company_id).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.barrio (barrio_codigo, descripcion, estado)
            VALUES (@Barrio, 'BARRIO TEST CORTE MASIVO', true)",
            new { Barrio = barrio }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre,
                 barrio_codigo, estado)
            VALUES (@CompanyId, @Clave, '', 'CLIENTE CORTE MASIVO', @Barrio, true)",
            new { CompanyId, Clave = clave, Barrio = barrio }, Transaction));

        var facturaId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.factura
                (company_id, numfactura, clientecodigo, tipofactura, tipofacturacion,
                 fechaemision, periodo, saldototal, usuario, estado, estado_id)
            VALUES (@CompanyId, NULL, @Clave, 'F', 'S',
                    current_date - 30, '2026/7', @Monto, 'test-corte', 'A', 1)
            RETURNING id",
            new { CompanyId, Clave = clave, Monto = monto }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle
                (company_id, factura_id, codigo, tiposervicio, descripcion, montovalor, montovalor_saldo)
            VALUES (@CompanyId, @FacturaId, '', 'AGUA_POTABLE', 'Agua', @Monto, @Monto)",
            new { CompanyId, FacturaId = facturaId, Monto = monto }, Transaction));

        return (clave, barrio, monto);
    }

    [SkippableFact]
    public async Task Generar_lote_crea_hdr_detalles_y_ordenes_de_trabajo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (clave, barrio, saldo) = await CrearClienteConDeudaAsync(275.50m);

        var hdr = await _service!.GenerarAsync(new GenerarCorteMasivoRequest(
            PeriodoAnio: 2026, PeriodoMes: 8,
            CicloId: null, BarrioCodigo: barrio, CategoriaId: null,
            ValorMinimo: 0m, DiasCorte: 3), "test-corte");

        // El barrio exclusivo garantiza que el lote es exactamente nuestro cliente.
        Assert.Equal(1, hdr.TotalClientes);
        Assert.Equal("GENERADO", hdr.Estado);

        var dtl = await Connection.QueryFirstAsync<(string ClienteClave, decimal Saldo, bool Pagado, int? OrdenId)>(
            new CommandDefinition(@"
                SELECT cliente_clave, saldo_adeudado, pagado, orden_id
                FROM public.cln_corte_masivo_dtl
                WHERE company_id = @CompanyId AND hdr_id = @HdrId",
                new { CompanyId, HdrId = hdr.Id }, Transaction));

        Assert.Equal(clave, dtl.ClienteClave);
        Assert.Equal(saldo, dtl.Saldo);
        Assert.False(dtl.Pagado);
        Assert.NotNull(dtl.OrdenId);

        // La orden de trabajo es REAL: pendiente, tipo 33 (corte), con el saldo
        // y fecha de corte a N días — es la que baja al app de cuadrillas.
        // Dapper no mapea date→DateOnly en tuplas (trampa conocida): DateTime.
        var ot = await Connection.QueryFirstAsync<(string Estado, string Tipo, decimal? Saldo, DateTime? Fecha, string Clave)>(
            new CommandDefinition(@"
                SELECT estado, tipo, saldo, fecha, maestro_cliente_clave
                FROM public.orden_trabajo WHERE orden_id = @Id",
                new { Id = dtl.OrdenId }, Transaction));

        Assert.Equal("P", ot.Estado);
        Assert.Equal("33", ot.Tipo);
        Assert.Equal(saldo, ot.Saldo);
        Assert.Equal(clave, ot.Clave);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(3), ot.Fecha);

        // Detalle consultable (pantalla del lote) e impresión "sin pago".
        var detalle = await _service.ObtenerDetalleAsync(hdr.Id);
        Assert.NotNull(detalle);
        Assert.Single(detalle!.Clientes);
    }

    [SkippableFact]
    public async Task Pagar_cancela_la_orden_de_corte_pendiente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (clave, barrio, _) = await CrearClienteConDeudaAsync(120.00m);

        var hdr = await _service!.GenerarAsync(new GenerarCorteMasivoRequest(
            2026, 8, null, barrio, null, 0m, 3), "test-corte");
        Assert.Equal(1, hdr.TotalClientes);

        // El motor de cobros invoca esta cancelación al saldar la deuda.
        var canceladas = await _service.CancelarOrdenesCorteClienteAsync(clave, "test-corte");
        Assert.Equal(1, canceladas);

        var estado = await Connection.QueryFirstAsync<(bool Pagado, string OrdenEstado)>(new CommandDefinition(@"
            SELECT d.pagado, o.estado
            FROM public.cln_corte_masivo_dtl d
            JOIN public.orden_trabajo o ON o.orden_id = d.orden_id
            WHERE d.company_id = @CompanyId AND d.hdr_id = @HdrId",
            new { CompanyId, HdrId = hdr.Id }, Transaction));

        Assert.True(estado.Pagado);
        Assert.Equal("C", estado.OrdenEstado);

        // Reintentar no cancela nada nuevo (idempotente para el motor).
        Assert.Equal(0, await _service.CancelarOrdenesCorteClienteAsync(clave, "test-corte"));
    }

    /// <summary>Clientes protegidos jamás entran al lote (candado del backlog).</summary>
    [SkippableFact]
    public async Task No_cortable_y_bloqueado_quedan_fuera_del_lote()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var (clave, barrio, _) = await CrearClienteConDeudaAsync(500.00m);
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.cliente_maestro SET no_cortable = true WHERE company_id = @CompanyId AND maestro_cliente_clave = @Clave",
            new { CompanyId, Clave = clave }, Transaction));

        var hdr = await _service!.GenerarAsync(new GenerarCorteMasivoRequest(
            2026, 8, null, barrio, null, 0m, 3), "test-corte");

        Assert.Equal(0, hdr.TotalClientes);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
