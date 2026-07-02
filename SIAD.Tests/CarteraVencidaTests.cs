using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Services.Cobranza;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class CarteraVencidaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CobranzaService? _service;

    public CarteraVencidaTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);
            _service = new CobranzaService(_context, company, new Fakes.FakeDocumentoCobranzaGenerator());
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task ListarCarteraVencida_SumasPorTramo_CuadranConTotal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var result = await _service!.ListarCarteraVencidaAsync(
            new CarteraVencidaFiltroDto { FechaCorte = DateOnly.FromDateTime(DateTime.Today) });

        Assert.All(result, c =>
        {
            Assert.Equal(c.TotalVencido, c.B0_30 + c.B31_60 + c.B61_120 + c.BMas120);
            Assert.NotEqual(0m, c.TotalVencido);
        });
    }

    [SkippableFact]
    public async Task ListarCarteraVencida_FacturaVencida45Dias_CaeEnTramo31_60()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = await ObtenerClaveExistenteAsync();
        Skip.If(clave is null, "No hay clientes en la BD de prueba");

        var corte = DateOnly.FromDateTime(DateTime.Today);

        var antes = await BucketDelCliente(clave!, corte);
        await InsertarFacturaAsync(clave!, fechaEmision: corte.AddDays(-45),
            fechaVence: corte.AddDays(-45), fechaPago: null, saldo: 123.45m);
        var despues = await BucketDelCliente(clave!, corte);

        Assert.Equal(123.45m, despues.B31_60 - antes.B31_60);
        Assert.Equal(0m, despues.B0_30 - antes.B0_30);
    }

    [SkippableFact]
    public async Task ListarCarteraVencida_AsOf_DesplazaTramoPorAntiguedad()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = await ObtenerClaveExistenteAsync();
        Skip.If(clave is null, "No hay clientes en la BD de prueba");

        var corteHoy = DateOnly.FromDateTime(DateTime.Today);
        var cortePasado = corteHoy.AddDays(-20);

        // Baseline antes de sembrar (aísla la factura sembrada de las reales del cliente).
        var hoyAntes = await BucketDelCliente(clave!, corteHoy);
        var pasadoAntes = await BucketDelCliente(clave!, cortePasado);

        // Factura emitida hace 45 días, con saldo abierto.
        await InsertarFacturaAsync(clave!, fechaEmision: corteHoy.AddDays(-45),
            fechaVence: corteHoy.AddDays(-45), fechaPago: null, saldo: 77.00m);

        var hoyDespues = await BucketDelCliente(clave!, corteHoy);      // edad 45 → tramo 31–60
        var pasadoDespues = await BucketDelCliente(clave!, cortePasado); // edad 25 → tramo 0–30

        // As-of hoy: 45 días de antigüedad → 31–60.
        Assert.Equal(77.00m, hoyDespues.B31_60 - hoyAntes.B31_60);
        Assert.Equal(0m, hoyDespues.B0_30 - hoyAntes.B0_30);
        // As-of 20 días atrás: 25 días de antigüedad → 0–30.
        Assert.Equal(77.00m, pasadoDespues.B0_30 - pasadoAntes.B0_30);
        Assert.Equal(0m, pasadoDespues.B31_60 - pasadoAntes.B31_60);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> ObtenerClaveExistenteAsync()
        => await Connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT maestro_cliente_clave FROM cliente_maestro WHERE company_id = @c LIMIT 1",
            new { c = CompanyId }, Transaction);

    private async Task InsertarFacturaAsync(string clave, DateOnly fechaEmision,
        DateOnly fechaVence, DateOnly? fechaPago, decimal saldo)
        => await Connection.ExecuteAsync("""
            INSERT INTO factura (company_id, numfactura, clientecodigo,
                                 fechaemision, fechavence, fechapago, saldototal, estado)
            VALUES (@company_id, @numfactura, @clave,
                    @fechaemision::date, @fechavence::date, @fechapago::date, @saldo, 'P')
            """,
            new
            {
                company_id = CompanyId,
                numfactura = "TEST-" + Guid.NewGuid().ToString("N")[..8],
                clave,
                fechaemision = fechaEmision.ToDateTime(TimeOnly.MinValue),
                fechavence = fechaVence.ToDateTime(TimeOnly.MinValue),
                fechapago = fechaPago?.ToDateTime(TimeOnly.MinValue),
                saldo
            }, Transaction);

    private async Task<(decimal B0_30, decimal B31_60, decimal Total)> BucketDelCliente(
        string clave, DateOnly corte)
    {
        var fila = (await _service!.ListarCarteraVencidaAsync(
            new CarteraVencidaFiltroDto { FechaCorte = corte, Busqueda = clave }))
            .FirstOrDefault(c => c.Clave == clave);
        return fila is null ? (0m, 0m, 0m) : (fila.B0_30, fila.B31_60, fila.TotalVencido);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
