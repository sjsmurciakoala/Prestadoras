using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Entities;
using SIAD.Core.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Retenciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Retenciones;

/// <summary>
/// Tests PUROS (sin BD) del ensamblado del DTO de impresión de la constancia (F5): totales, monto en
/// letras sobre el total retenido, marca de anulada y mapeo empresa/proveedor. No dependen del render
/// binario del reporte.
/// </summary>
public class ConstanciaRetencionBuilderTests
{
    private static prv_retencion_hdr Hdr(short estado = EstadoRetencion.Vigente) => new()
    {
        retencion_hdr_id = 10,
        company_id = 2,
        numero_orden = 555,
        numero_abono = 1,
        folio = 42,
        fecha_emision = new DateOnly(2026, 8, 7),
        cod_proveedor = "PRV1",
        rtn_proveedor = "08011999123456",
        base_total = 1150m,
        total_retenido = 125m,
        poliza_number = "POL-0001",
        estado_id = estado,
        motivo_anulacion = estado == EstadoRetencion.Anulada ? "prueba" : null,
    };

    private static cfg_company Company() => new()
    {
        company_id = 2,
        code = "EMP",
        commercial_name = "Comercial X",
        legal_name = "Empresa X S. de R.L.",
        tax_id = "08019999888777",
        address = "Tegucigalpa",
        phone = "2200-0000",
        email = "info@x.hn",
    };

    private static RetencionRegistroLineaDto Linea(decimal baseLinea, decimal monto, decimal pct) => new()
    {
        RetencionDtlId = 1,
        RetencionId = 3,
        Codigo = "ISR125",
        Nombre = "ISR 12.5% honorarios",
        Porcentaje = pct,
        BaseLinea = baseLinea,
        MontoRetenido = monto,
        AccountId = 99
    };

    [Fact]
    public void Build_MapeaEmpresaProveedorTotalesYEnLetras()
    {
        var dto = ConstanciaRetencionBuilder.Build(
            Hdr(), new[] { Linea(1000m, 125m, 12.5m) }, Company(),
            nombreProveedor: "Proveedor Uno", concepto: "Servicios profesionales", impresoPor: "tester");

        Assert.Equal("Comercial X", dto.EmpresaNombre);
        Assert.Equal("08019999888777", dto.EmpresaRtn);
        Assert.Equal("Proveedor Uno", dto.ProveedorNombre);
        Assert.Equal("08011999123456", dto.ProveedorRtn);
        Assert.Equal(42, dto.Folio);
        Assert.Equal(1150m, dto.BaseTotal);
        Assert.Equal(125m, dto.TotalRetenido);
        Assert.Equal("Servicios profesionales", dto.Concepto);
        Assert.Single(dto.Lineas);
        Assert.False(dto.Anulada);
        Assert.Equal("tester", dto.ImpresoPor);
        Assert.Contains("LEMPIRAS", dto.MontoEnLetras);
    }

    [Fact]
    public void Build_EnLetras_SobreTotalRetenido_NoSobreLaBase()
    {
        // total_retenido = 125 → "CIENTO VEINTICINCO …"; NO 1150 (que llevaría "MIL").
        var dto = ConstanciaRetencionBuilder.Build(
            Hdr(), new[] { Linea(1000m, 125m, 12.5m) }, Company(), "p", "c", "t");

        Assert.StartsWith("CIENTO VEINTICINCO", dto.MontoEnLetras);
        Assert.DoesNotContain("MIL", dto.MontoEnLetras);
    }

    [Fact]
    public void Build_MarcaAnulada_CuandoEstado9()
    {
        var dto = ConstanciaRetencionBuilder.Build(
            Hdr(EstadoRetencion.Anulada), new[] { Linea(1000m, 125m, 12.5m) }, Company(),
            "Proveedor Uno", "x", "tester");

        Assert.True(dto.Anulada);
        Assert.Equal(EstadoRetencion.Anulada, dto.EstadoId);
        Assert.Equal("prueba", dto.MotivoAnulacion);
    }

    [Fact]
    public void Build_SinCompany_UsaVacio_CaeAlCodigoDeProveedor_YHooksCaiNulos()
    {
        var dto = ConstanciaRetencionBuilder.Build(
            Hdr(), Array.Empty<RetencionRegistroLineaDto>(), company: null,
            nombreProveedor: null, concepto: null, impresoPor: null);

        Assert.Equal(string.Empty, dto.EmpresaNombre);
        Assert.Null(dto.EmpresaRtn);
        Assert.Equal("PRV1", dto.ProveedorNombre);   // sin nombre → cae al código
        Assert.Equal("sistema", dto.ImpresoPor);
        Assert.Empty(dto.Lineas);
        // Hooks CAI (F5b): no se implementan todavía.
        Assert.Null(dto.CaiCorrelativo);
        Assert.Null(dto.CaiLeyenda);
    }
}

/// <summary>
/// Integración (mirror): <see cref="RetencionRegistroService.GetDatosConstanciaAsync"/> arma el DTO
/// desde el libro fiscal sembrado + la empresa (cfg_company) + el compromiso (nombre/concepto).
/// Requiere las tablas de F4 y F1 aplicadas en el mirror.
/// </summary>
[Collection("Postgres")]
public class ConstanciaRetencionServiceTests : IntegrationTestBase
{
    private const int OrdenBase = 982001;

    public ConstanciaRetencionServiceTests(PostgresFixture fixture) : base(fixture) { }

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
     base_total, total_retenido, estado_id, motivo_anulacion, usuario_creo, poliza_number)
VALUES (@c, @n, @a, @folio, @fe, @cp, @rtn, @bt, @tr, @est, @mot, 'tester', 'POL-TEST')
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
        cmd.Parameters.AddWithValue("mot", estado == EstadoRetencion.Anulada ? (object)"anulada de prueba" : DBNull.Value);
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
    public async Task GetDatosConstancia_ArmaEmpresaProveedorLineasYTotal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        var accountId = await ResolveAccountIdAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en el tenant de prueba.");

        const int orden = OrdenBase + 1;
        await SeedCompromisoHdrAsync(orden, "Servicios de auditoría", "PRVX", "Proveedor Auditor", "08011999000001");
        var hdrId = await SeedHdrAsync(orden, 1, 1_900_002_001, "PRVX", "08011999000001",
            baseTotal: 1150m, totalRet: 125m, EstadoRetencion.Vigente);
        await SeedDtlAsync(hdrId, retId!.Value, "ISR125", "ISR 12.5% honorarios", 12.5m, baseLinea: 1000m, monto: 125m, accountId!.Value);

        await using var ctx = CreateContext();
        var service = new RetencionRegistroService(ctx, new TestCurrentCompanyService(CompanyId));

        var dto = await service.GetDatosConstanciaAsync(hdrId, "tester", CancellationToken.None);
        Assert.NotNull(dto);
        Assert.Equal(1_900_002_001, dto!.Folio);
        Assert.Equal("Proveedor Auditor", dto.ProveedorNombre);          // resuelto del compromiso
        Assert.Equal("08011999000001", dto.ProveedorRtn);
        Assert.Equal("Servicios de auditoría", dto.Concepto);
        Assert.Equal(1150m, dto.BaseTotal);
        Assert.Equal(125m, dto.TotalRetenido);
        var linea = Assert.Single(dto.Lineas);
        Assert.Equal(1000m, linea.BaseLinea);
        Assert.Equal(125m, linea.MontoRetenido);
        Assert.False(dto.Anulada);
        Assert.Contains("LEMPIRAS", dto.MontoEnLetras);
        Assert.False(string.IsNullOrWhiteSpace(dto.EmpresaNombre));      // cfg_company del tenant de prueba
    }

    [SkippableFact]
    public async Task GetDatosConstancia_Anulada_MarcaFlag()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        var accountId = await ResolveAccountIdAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en el tenant de prueba.");

        const int orden = OrdenBase + 2;
        await SeedCompromisoHdrAsync(orden, "Servicio anulado", "PRVY", "Proveedor Anulado", "08011999000002");
        var hdrId = await SeedHdrAsync(orden, 1, 1_900_002_002, "PRVY", "08011999000002",
            baseTotal: 500m, totalRet: 62.5m, EstadoRetencion.Anulada);
        await SeedDtlAsync(hdrId, retId!.Value, "ISR125", "ISR 12.5%", 12.5m, baseLinea: 500m, monto: 62.5m, accountId!.Value);

        await using var ctx = CreateContext();
        var service = new RetencionRegistroService(ctx, new TestCurrentCompanyService(CompanyId));

        var dto = await service.GetDatosConstanciaAsync(hdrId, "tester", CancellationToken.None);
        Assert.NotNull(dto);
        Assert.True(dto!.Anulada);
        Assert.Equal(EstadoRetencion.Anulada, dto.EstadoId);
        Assert.Equal("anulada de prueba", dto.MotivoAnulacion);
    }

    [SkippableFact]
    public async Task GetDatosConstancia_NoExiste_DevuelveNull()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await using var ctx = CreateContext();
        var service = new RetencionRegistroService(ctx, new TestCurrentCompanyService(CompanyId));
        Assert.Null(await service.GetDatosConstanciaAsync(2_100_000_099, "tester", CancellationToken.None));
    }
}
