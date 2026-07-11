using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// prv_compromiso_hdr/_dtl no tenían company_id y OrdenesPagoDirectoService los
/// consultaba sin filtro, así que los compromisos se veían desde cualquier tenant.
/// Estas pruebas fijan el aislamiento por compañía.
/// </summary>
[Collection("Postgres")]
public class CompromisoTenancyTests : IntegrationTestBase, IAsyncLifetime
{
    private const int NumeroOrdenA = 990001;
    private const int NumeroOrdenB = 990002;
    private const long OtraCompania = 987654;

    public CompromisoTenancyTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private SiadDbContext CreateContext(long companyId)
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        var context = new SiadDbContext(options, new TestCurrentCompanyService(companyId));
        context.Database.UseTransaction(Transaction);
        return context;
    }

    /// <summary>Inserta una cabecera y su detalle directamente, sin pasar por EF.</summary>
    private async Task SeedCompromisoAsync(long companyId, int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, anulado)
VALUES (@c, @n, now(), 100.00, 'compromiso de prueba', FALSE);

INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad,
     objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, @n, 'CP1', '01', '01', 'OG', 'CG', 'detalle de prueba', 100.00);";
        cmd.Parameters.AddWithValue("c", companyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        await cmd.ExecuteNonQueryAsync();
    }

    [SkippableFact]
    public async Task Cabeceras_SoloSeVenLasDeLaCompaniaActual()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedCompromisoAsync(CompanyId, NumeroOrdenA);
        await SeedCompromisoAsync(OtraCompania, NumeroOrdenB);

        await using var propio = CreateContext(CompanyId);
        var visiblesPropias = await propio.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == NumeroOrdenA || x.numero_orden == NumeroOrdenB)
            .Select(x => x.numero_orden)
            .ToListAsync();

        Assert.Equal(new[] { NumeroOrdenA }, visiblesPropias);

        await using var ajeno = CreateContext(OtraCompania);
        var visiblesAjenas = await ajeno.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == NumeroOrdenA || x.numero_orden == NumeroOrdenB)
            .Select(x => x.numero_orden)
            .ToListAsync();

        Assert.Equal(new[] { NumeroOrdenB }, visiblesAjenas);
    }

    [SkippableFact]
    public async Task Detalles_SoloSeVenLosDeLaCompaniaActual()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedCompromisoAsync(CompanyId, NumeroOrdenA);
        await SeedCompromisoAsync(OtraCompania, NumeroOrdenB);

        await using var propio = CreateContext(CompanyId);
        var detalleAjenoVisible = await propio.prv_compromiso_dtls
            .AsNoTracking()
            .AnyAsync(x => x.numero_orden == NumeroOrdenB);

        Assert.False(detalleAjenoVisible);

        var detallePropio = await propio.prv_compromiso_dtls
            .AsNoTracking()
            .CountAsync(x => x.numero_orden == NumeroOrdenA);

        Assert.Equal(1, detallePropio);
    }

    [SkippableFact]
    public async Task BorrarCabecera_ArrastraSuDetallePorCascade()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await SeedCompromisoAsync(CompanyId, NumeroOrdenA);

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = "DELETE FROM public.prv_compromiso_hdr WHERE company_id=@c AND numero_orden=@n";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("n", NumeroOrdenA);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var check = Connection.CreateCommand();
        check.Transaction = Transaction;
        check.CommandText = "SELECT count(*) FROM public.prv_compromiso_dtl WHERE company_id=@c AND numero_orden=@n";
        check.Parameters.AddWithValue("c", CompanyId);
        check.Parameters.AddWithValue("n", NumeroOrdenA);

        Assert.Equal(0L, (long)(await check.ExecuteScalarAsync())!);
    }

    [SkippableFact]
    public async Task Detalle_SinCabecera_ViolaLaLlaveForanea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad,
     objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, 999999, 'CP1', '01', '01', 'OG', 'CG', 'huerfano', 1.00);";
        cmd.Parameters.AddWithValue("c", CompanyId);

        await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
