using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Retenciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Retenciones;

/// <summary>
/// Integración (mirror): el reporte mensual para la declaración (F5) lee el detalle usando
/// <c>dtl.base_linea</c> (no el bruto del pago), excluye las anuladas cuando se filtra Vigentes,
/// resuelve el nombre del proveedor desde el compromiso y respeta el tenant.
/// </summary>
[Collection("Postgres")]
public class RetencionDeclaracionTests : IntegrationTestBase
{
    private const int OrdenBase = 983001;

    public RetencionDeclaracionTests(PostgresFixture fixture) : base(fixture) { }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }

    private SiadDbContext CreateContext(long? companyId = null)
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var context = new SiadDbContext(options, new TestCurrentCompanyService(companyId ?? CompanyId));
        context.Database.UseTransaction(Transaction);
        return context;
    }

    private async Task SeedCompromisoHdrAsync(int orden, string concepto, string cod, string nombre, string? rtn)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor, rtn, nombre_proveedor, status_transacc, anulado)
VALUES (@c, @n, @f, @m, @concepto, @cp, @rtn, @np, FALSE, FALSE);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", orden);
        cmd.Parameters.AddWithValue("f", DateTime.Now.Date);
        cmd.Parameters.AddWithValue("m", 1000m);
        cmd.Parameters.AddWithValue("concepto", concepto);
        cmd.Parameters.AddWithValue("cp", cod);
        cmd.Parameters.AddWithValue("rtn", (object?)rtn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("np", nombre);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> SeedHdrAsync(int orden, int abono, int folio, string cod, string? rtn,
        decimal baseTotal, decimal totalRet, short estado)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_retencion_hdr
    (company_id, numero_orden, numero_abono, folio, fecha_emision, cod_proveedor, rtn_proveedor,
     base_total, total_retenido, estado_id, usuario_creo)
VALUES (@c, @n, @a, @folio, @fe, @cp, @rtn, @bt, @tr, @est, 'tester')
RETURNING retencion_hdr_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", orden);
        cmd.Parameters.AddWithValue("a", abono);
        cmd.Parameters.AddWithValue("folio", folio);
        cmd.Parameters.AddWithValue("fe", DateOnly.FromDateTime(DateTime.Now));
        cmd.Parameters.AddWithValue("cp", cod);
        cmd.Parameters.AddWithValue("rtn", (object?)rtn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bt", baseTotal);
        cmd.Parameters.AddWithValue("tr", totalRet);
        cmd.Parameters.AddWithValue("est", estado);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task SeedDtlAsync(long hdrId, int retId, string codigo, string nombre, decimal pct,
        decimal baseLinea, decimal monto, long accountId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_retencion_dtl
    (company_id, retencion_hdr_id, retencion_id, codigo, nombre, porcentaje, base_linea, monto_retenido, account_id)
VALUES (@c, @h, @r, @cod, @nom, @pct, @bl, @m, @acc);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("h", hdrId);
        cmd.Parameters.AddWithValue("r", retId);
        cmd.Parameters.AddWithValue("cod", codigo);
        cmd.Parameters.AddWithValue("nom", nombre);
        cmd.Parameters.AddWithValue("pct", pct);
        cmd.Parameters.AddWithValue("bl", baseLinea);
        cmd.Parameters.AddWithValue("m", monto);
        cmd.Parameters.AddWithValue("acc", accountId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int?> ResolveRetencionIdAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT id FROM public.cfg_retencion WHERE activo ORDER BY id LIMIT 1;";
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : Convert.ToInt32(raw);
    }

    private async Task<long?> ResolveAccountIdAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT account_id FROM public.con_plan_cuentas WHERE company_id=@c AND allows_posting=TRUE ORDER BY account_id LIMIT 1;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : (long)raw;
    }

    [SkippableFact]
    public async Task Declaracion_UsaBaseLinea_ExcluyeAnuladas_ResuelveProveedor_YTenant()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        var accountId = await ResolveAccountIdAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en el tenant de prueba.");

        const int ordenA = OrdenBase + 1;
        const int ordenB = OrdenBase + 2;
        await SeedCompromisoHdrAsync(ordenA, "Servicios A", "PRVA", "Proveedor A", "0801A");
        await SeedCompromisoHdrAsync(ordenB, "Servicios B", "PRVB", "Proveedor B", "0801B");

        // A vigente: base_total 1150 (bruto) pero base_linea 1000 (sin ISV) → probamos que el reporte usa base_linea.
        var hdrA = await SeedHdrAsync(ordenA, 1, 1_900_003_001, "PRVA", "0801A", baseTotal: 1150m, totalRet: 125m, EstadoRetencion.Vigente);
        await SeedDtlAsync(hdrA, retId!.Value, "ISR125", "ISR 12.5%", 12.5m, baseLinea: 1000m, monto: 125m, accountId!.Value);

        // B vigente.
        var hdrB = await SeedHdrAsync(ordenB, 1, 1_900_003_002, "PRVB", "0801B", baseTotal: 2000m, totalRet: 200m, EstadoRetencion.Vigente);
        await SeedDtlAsync(hdrB, retId.Value, "ISR125", "ISR 12.5%", 12.5m, baseLinea: 2000m, monto: 200m, accountId.Value);

        // A abono 2: ANULADA → no debe aparecer en Vigentes ni sumar al total.
        var hdrAnu = await SeedHdrAsync(ordenA, 2, 1_900_003_003, "PRVA", "0801A", baseTotal: 600m, totalRet: 62.5m, EstadoRetencion.Anulada);
        await SeedDtlAsync(hdrAnu, retId.Value, "ISR125", "ISR 12.5%", 12.5m, baseLinea: 500m, monto: 62.5m, accountId.Value);

        await using var ctx = CreateContext();
        var service = new RetencionRegistroService(ctx, new TestCurrentCompanyService(CompanyId));

        // Vigentes: aparecen A y B; NO la anulada; la base es dtl.base_linea (1000), no hdr.base_total (1150).
        var vigentes = await service.BuscarDeclaracionAsync(
            new RetencionDeclaracionFilterDto { EstadoId = EstadoRetencion.Vigente }, CancellationToken.None);

        var filaA = vigentes.Single(x => x.Folio == 1_900_003_001);
        Assert.Equal(1000m, filaA.BaseLinea);                    // ← base_linea, no base_total (1150)
        Assert.Equal(125m, filaA.MontoRetenido);
        Assert.Equal("Proveedor A", filaA.NombreProveedor);      // resuelto del compromiso
        Assert.Equal("ISR 12.5%", filaA.TipoNombre);
        Assert.Equal("VIGENTE", filaA.EstadoDescripcion);
        Assert.StartsWith("PRVA", filaA.ProveedorDisplay);       // clave de agrupación

        Assert.Contains(vigentes, x => x.Folio == 1_900_003_002);
        Assert.DoesNotContain(vigentes, x => x.Folio == 1_900_003_003);   // anulada excluida

        // Todas (estado null): la anulada aparece, marcada, con su propia base_linea.
        var todas = await service.BuscarDeclaracionAsync(new RetencionDeclaracionFilterDto(), CancellationToken.None);
        var anu = todas.Single(x => x.Folio == 1_900_003_003);
        Assert.Equal("ANULADA", anu.EstadoDescripcion);
        Assert.Equal(500m, anu.BaseLinea);

        // Tenancy: otra empresa no ve ninguna de estas filas.
        await using var ctxOtra = CreateContext(companyId: CompanyId + 990000);
        var svcOtra = new RetencionRegistroService(ctxOtra, new TestCurrentCompanyService(CompanyId + 990000));
        var otras = await svcOtra.BuscarDeclaracionAsync(new RetencionDeclaracionFilterDto(), CancellationToken.None);
        Assert.DoesNotContain(otras, x =>
            x.Folio == 1_900_003_001 || x.Folio == 1_900_003_002 || x.Folio == 1_900_003_003);
    }

    [SkippableFact]
    public async Task GetDatosDeclaracionImpresion_ArmaEmpresaItemsYFiltroTexto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        var accountId = await ResolveAccountIdAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en el tenant de prueba.");

        const int orden = OrdenBase + 7;
        await SeedCompromisoHdrAsync(orden, "Servicios impresión", "PRVIMP", "Proveedor Impresión", "0801IMP");
        var hdrId = await SeedHdrAsync(orden, 1, 1_900_003_007, "PRVIMP", "0801IMP",
            baseTotal: 1150m, totalRet: 125m, EstadoRetencion.Vigente);
        await SeedDtlAsync(hdrId, retId!.Value, "ISR125", "ISR 12.5%", 12.5m, baseLinea: 1000m, monto: 125m, accountId!.Value);

        await using var ctx = CreateContext();
        var service = new RetencionRegistroService(ctx, new TestCurrentCompanyService(CompanyId));

        var dto = await service.GetDatosDeclaracionImpresionAsync(
            new RetencionDeclaracionFilterDto { EstadoId = EstadoRetencion.Vigente }, "tester", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(dto.EmpresaNombre));   // cfg_company del tenant de prueba
        Assert.Equal("tester", dto.ImpresoPor);
        Assert.Contains("Vigentes", dto.FiltroTexto);
        Assert.Contains(dto.Items, x =>
            x.Folio == 1_900_003_007 && x.BaseLinea == 1000m && x.NombreProveedor == "Proveedor Impresión");
    }
}
