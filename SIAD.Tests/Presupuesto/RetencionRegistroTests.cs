using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Services.Presupuesto;
using SIAD.Services.Proveedores;
using SIAD.Services.Retenciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// F4 — el pago con retención (procesar/abonar) escribe el registro fiscal estructurado
/// prv_retencion_hdr/dtl EN LA MISMA transacción del pago, ligado a la partida POSTED (partida_id) y
/// al numero_abono, con estado numérico (1 Vigente / 9 Anulada). Cubre: persistencia hdr/dtl al
/// procesar y al abonar, validación de consistencia (Σ retenido == Σ crédito de esas cuentas),
/// anulación (hdr → 9), y la consulta (RetencionRegistroService) con su tenancy. Requiere la config
/// PROV en el mirror y las tablas de F4 (Database/2026-08-07_prv_retencion_hdr_dtl.sql).
/// </summary>
[Collection("Postgres")]
public class RetencionRegistroTests : IntegrationTestBase
{
    private const int OrdenBase = 981001;

    public RetencionRegistroTests(PostgresFixture fixture) : base(fixture) { }

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

    private IOrdenesPagoDirectoService CreateService(SiadDbContext context)
    {
        var proveedores = Substitute.For<IProveedoresService>();
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var accountFormat = Substitute.For<IAccountFormatService>();
        accountFormat.GetFormatAsync(Arg.Any<CancellationToken>()).Returns(AccountFormat.Default);
        var banTransacciones = Substitute.For<IBanTransaccionesService>();
        var cheques = Substitute.For<IChequesService>();

        return new OrdenesPagoDirectoService(
            context, proveedores, new TestCurrentCompanyService(CompanyId),
            httpAccessor, accountFormat, banTransacciones, cheques,
            new PresupuestoCompromisoService(context, new TestCurrentCompanyService(CompanyId)));
    }

    // --- Siembra (idéntica a PrvContabilidadTests) ---

    private async Task SeedCompromisoAsync(int numeroOrden, decimal monto, string codProveedor, string cuentaGasto, string? rtn = null)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor, rtn, status_transacc, anulado)
VALUES (@c, @n, @f, @m, 'retencion f4 test', @cp, @rtn, FALSE, FALSE);

INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad, objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, @n, '0', '', '', 'Objeto de gasto de prueba', @cg, 'detalle', @m);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("f", DateTime.Now.Date);
        cmd.Parameters.AddWithValue("m", monto);
        cmd.Parameters.AddWithValue("cp", codProveedor);
        cmd.Parameters.AddWithValue("rtn", (object?)rtn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cg", cuentaGasto);
        await cmd.ExecuteNonQueryAsync();
    }

    private readonly record struct Cuentas(
        string CodProveedor, long CuentaProveedorId,
        long CuentaOrigenId, long CuentaRetencionId, string CuentaGastoCode);

    private async Task<Cuentas?> ResolveCuentasAsync()
    {
        string codProveedor; long provId;
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT p.cod_proveedor, c.account_id
  FROM public.prv_proveedores p
  JOIN public.con_plan_cuentas c ON c.company_id=@c AND btrim(c.code)=btrim(p.cuenta_contable)
 WHERE p.company_id=@c AND c.allows_posting=TRUE AND c.status='ACTIVE'
 ORDER BY p.cod_proveedor LIMIT 1;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            codProveedor = r.GetString(0); provId = r.GetInt64(1);
        }

        var otras = new List<long>();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT account_id FROM public.con_plan_cuentas
 WHERE company_id=@c AND allows_posting=TRUE AND status='ACTIVE' AND account_id<>@prov
 ORDER BY account_id LIMIT 2;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("prov", provId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) otras.Add(r.GetInt64(0));
        }
        if (otras.Count < 2) return null;

        string? cuentaGasto;
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT code FROM public.con_plan_cuentas
 WHERE company_id=@c AND allows_posting=TRUE AND status='ACTIVE'
   AND upper(account_type) IN ('GASTO','GASTOS','EGRESO','EGRESOS','COSTO','COSTOS','INGRESO','INGRESOS')
 ORDER BY account_id LIMIT 1;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cuentaGasto = (string?)await cmd.ExecuteScalarAsync();
        }
        if (cuentaGasto is null) return null;

        return new Cuentas(codProveedor, provId, otras[0], otras[1], cuentaGasto);
    }

    private async Task<int?> ResolveRetencionIdAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT id FROM public.cfg_retencion WHERE activo ORDER BY id LIMIT 1;";
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : Convert.ToInt32(raw);
    }

    private async Task<long> PeriodoAbiertoAsync(DateTime fecha)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT public.fn_con_periodo_abierto(@c, @f::date);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("f", fecha.Date);
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? 0 : (long)raw;
    }

    // --- Lecturas del registro fiscal (F4) ---

    private sealed record HdrRow(long HdrId, int Folio, short EstadoId, long? PartidaId, string? PolizaNumber,
        decimal TotalRetenido, decimal BaseTotal, string? CodProveedor, string? RtnProveedor, string? MotivoAnulacion);

    private async Task<HdrRow?> LeerHdrAsync(int numeroOrden, int numeroAbono)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT retencion_hdr_id, folio, estado_id, partida_id, poliza_number, total_retenido, base_total,
       cod_proveedor, rtn_proveedor, motivo_anulacion
  FROM public.prv_retencion_hdr
 WHERE company_id=@c AND numero_orden=@n AND numero_abono=@a;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("a", numeroAbono);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new HdrRow(
            r.GetInt64(0), r.GetInt32(1), r.GetInt16(2),
            r.IsDBNull(3) ? null : r.GetInt64(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            r.GetDecimal(5), r.GetDecimal(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.IsDBNull(8) ? null : r.GetString(8),
            r.IsDBNull(9) ? null : r.GetString(9));
    }

    private async Task<List<(int RetencionId, string Codigo, decimal Monto, decimal Base, decimal Porcentaje, long AccountId)>> LeerDtlAsync(long hdrId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT retencion_id, codigo, monto_retenido, base_linea, porcentaje, account_id
  FROM public.prv_retencion_dtl
 WHERE company_id=@c AND retencion_hdr_id=@h ORDER BY retencion_dtl_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("h", hdrId);
        var list = new List<(int, string, decimal, decimal, decimal, long)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add((r.GetInt32(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetDecimal(4), r.GetInt64(5)));
        return list;
    }

    private async Task<int> ContarHdrAsync(int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT count(*) FROM public.prv_retencion_hdr WHERE company_id=@c AND numero_orden=@n;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private static ProcesarOrdenPagoDirectoDto ProcesarConRetencion(long cuentaOrigenId, long cuentaRetencionId,
        int retencionId, decimal neto, decimal montoRetencion, decimal baseRet, decimal porcentaje) => new()
        {
            MetodoPago = OrdenPagoDirectoMetodoPago.Contable,
            Usuario = "tester",
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new() { CuentaId = cuentaOrigenId, Descripcion = "pago neto", Debito = 0m, Credito = neto },
                new() { CuentaId = cuentaRetencionId, Descripcion = "retencion", Debito = 0m, Credito = montoRetencion }
            },
            Retenciones = new List<RetencionAplicadaDto>
            {
                new() { RetencionId = retencionId, CuentaId = cuentaRetencionId, Base = baseRet, Porcentaje = porcentaje, Monto = montoRetencion }
            }
        };

    private static AbonoCompromisoUpsertDto AbonoConRetencion(decimal monto, decimal neto, long cuentaOrigenId,
        long cuentaRetencionId, int retencionId, decimal montoRetencion, decimal baseRet, decimal porcentaje) => new()
        {
            Monto = monto,
            MetodoPago = OrdenPagoDirectoMetodoPago.Contable,
            Usuario = "tester",
            Fecha = DateTime.Now.Date,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new() { CuentaId = cuentaOrigenId, Descripcion = "pago neto", Debito = 0m, Credito = neto },
                new() { CuentaId = cuentaRetencionId, Descripcion = "retencion", Debito = 0m, Credito = montoRetencion }
            },
            Retenciones = new List<RetencionAplicadaDto>
            {
                new() { RetencionId = retencionId, CuentaId = cuentaRetencionId, Base = baseRet, Porcentaje = porcentaje, Monto = montoRetencion }
            }
        };

    // =====================================================================================
    // (1) Procesar con retención → escribe hdr + dtl ligados a la partida POSTED y al numero_abono.
    // =====================================================================================
    [SkippableFact]
    public async Task ProcesarConRetencion_EscribeHdrDtl_LigadoALaPartida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 1;
        var c = await ResolveCuentasAsync();
        Skip.If(c is null, "No hay proveedor + 2 cuentas posting/ACTIVE en el tenant de prueba.");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        Skip.If(await PeriodoAbiertoAsync(DateTime.Now.Date) <= 0, "No hay período abierto para hoy.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode, rtn: "08011999123456");

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = ProcesarConRetencion(c.Value.CuentaOrigenId, c.Value.CuentaRetencionId, retId!.Value,
            neto: 875m, montoRetencion: 125m, baseRet: 1000m, porcentaje: 12.5m);
        var res = await service.MarkAsProcessedAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        // El procesar inserta su fila de abono (numero_abono=1); el hdr F4 se liga a ese pago.
        var hdr = await LeerHdrAsync(orden, 1);
        Assert.NotNull(hdr);
        Assert.Equal(EstadoRetencion.Vigente, hdr!.EstadoId);
        Assert.True(hdr.Folio > 0);
        Assert.Equal(125m, hdr.TotalRetenido);
        Assert.Equal(1000m, hdr.BaseTotal);
        Assert.Equal("08011999123456", hdr.RtnProveedor);   // snapshot del RTN del compromiso

        // partida_id del hdr == partida_id del abono (la póliza POSTED del motor) y poliza_number snapshotado.
        var partidaAbono = await LeerPartidaIdAbonoAsync(orden, 1);
        Assert.NotNull(partidaAbono);
        Assert.Equal(partidaAbono, hdr.PartidaId);
        Assert.False(string.IsNullOrWhiteSpace(hdr.PolizaNumber));

        // dtl: una línea, Σ monto == total_retenido, cuenta == la del pasivo POSTED.
        var dtl = await LeerDtlAsync(hdr.HdrId);
        var linea = Assert.Single(dtl);
        Assert.Equal(retId.Value, linea.RetencionId);
        Assert.Equal(125m, linea.Monto);
        Assert.Equal(1000m, linea.Base);
        Assert.Equal(c.Value.CuentaRetencionId, linea.AccountId);
        Assert.Equal(hdr.TotalRetenido, dtl.Sum(x => x.Monto));
    }

    // =====================================================================================
    // (2) Abonar con retención → escribe hdr + dtl (mismo camino por RegistrarAbonoAsync).
    // =====================================================================================
    [SkippableFact]
    public async Task AbonarConRetencion_EscribeHdrDtl()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 2;
        var c = await ResolveCuentasAsync();
        Skip.If(c is null, "No hay proveedor + 2 cuentas posting/ACTIVE en el tenant de prueba.");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        Skip.If(await PeriodoAbiertoAsync(DateTime.Now.Date) <= 0, "No hay período abierto para hoy.");

        await SeedCompromisoAsync(orden, 2000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaOrigenId, c.Value.CuentaRetencionId, retId!.Value,
            montoRetencion: 125m, baseRet: 1000m, porcentaje: 12.5m);
        var res = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        var hdr = await LeerHdrAsync(orden, res.NumeroAbono);
        Assert.NotNull(hdr);
        Assert.Equal(EstadoRetencion.Vigente, hdr!.EstadoId);
        Assert.Equal(125m, hdr.TotalRetenido);
        Assert.Equal(1000m, hdr.BaseTotal);   // base = bruto del abono
        var partidaAbono = await LeerPartidaIdAbonoAsync(orden, res.NumeroAbono);
        Assert.Equal(partidaAbono, hdr.PartidaId);

        var dtl = await LeerDtlAsync(hdr.HdrId);
        Assert.Equal(125m, Assert.Single(dtl).Monto);
    }

    // =====================================================================================
    // (3) Consistencia: retenciones que no cuadran con la partida → ArgumentException y NO escribe nada.
    // =====================================================================================
    [SkippableFact]
    public async Task RetencionesDescuadradas_LanzaYNoEscribe()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 3;
        var c = await ResolveCuentasAsync();
        Skip.If(c is null, "No hay proveedor + 2 cuentas posting/ACTIVE en el tenant de prueba.");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        // Declara una retención de 125 pero la partida NO tiene esa línea (solo origen al Haber por 1000):
        // Σ retenido (125) ≠ Σ crédito de la cuenta de retención en Lineas (0) → debe rechazar.
        var dto = new ProcesarOrdenPagoDirectoDto
        {
            MetodoPago = OrdenPagoDirectoMetodoPago.Contable,
            Usuario = "tester",
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new() { CuentaId = c.Value.CuentaOrigenId, Descripcion = "pago", Debito = 0m, Credito = 1000m }
            },
            Retenciones = new List<RetencionAplicadaDto>
            {
                new() { RetencionId = retId!.Value, CuentaId = c.Value.CuentaRetencionId, Base = 1000m, Porcentaje = 12.5m, Monto = 125m }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.MarkAsProcessedAsync(orden, dto, CancellationToken.None));
        Assert.Equal(0, await ContarHdrAsync(orden));   // no se escribió ningún registro fiscal
    }

    // =====================================================================================
    // (4) Anular el pago → el hdr queda estado_id=9 (Anulada) con motivo.
    // =====================================================================================
    [SkippableFact]
    public async Task AnularPago_MarcaHdrAnulado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 4;
        var c = await ResolveCuentasAsync();
        Skip.If(c is null, "No hay proveedor + 2 cuentas posting/ACTIVE en el tenant de prueba.");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        Skip.If(await PeriodoAbiertoAsync(DateTime.Now.Date) <= 0, "No hay período abierto para hoy.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaOrigenId, c.Value.CuentaRetencionId, retId!.Value,
            montoRetencion: 125m, baseRet: 1000m, porcentaje: 12.5m);
        var reg = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);
        Assert.True(reg.Success, reg.Message);
        Assert.Equal(EstadoRetencion.Vigente, (await LeerHdrAsync(orden, reg.NumeroAbono))!.EstadoId);

        var anu = await service.AnularAbonoAsync(orden, reg.NumeroAbono,
            new AnularOrdenPagoDirectoDto { Motivo = "prueba F4 anulacion" }, CancellationToken.None);
        Assert.True(anu.Success, anu.Message);

        var hdr = await LeerHdrAsync(orden, reg.NumeroAbono);
        Assert.NotNull(hdr);
        Assert.Equal(EstadoRetencion.Anulada, hdr!.EstadoId);
        Assert.Equal("prueba F4 anulacion", hdr.MotivoAnulacion);
    }

    // =====================================================================================
    // (5) Consulta: RetencionRegistroService por proveedor devuelve el registro + su detalle, y respeta
    // el tenant (otra empresa no lo ve).
    // =====================================================================================
    [SkippableFact]
    public async Task Consulta_PorProveedor_DevuelveRegistro_YRespetaTenant()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 5;
        var c = await ResolveCuentasAsync();
        Skip.If(c is null, "No hay proveedor + 2 cuentas posting/ACTIVE en el tenant de prueba.");
        var retId = await ResolveRetencionIdAsync();
        Skip.If(retId is null, "No hay retención activa en cfg_retencion (aplicar F1).");
        Skip.If(await PeriodoAbiertoAsync(DateTime.Now.Date) <= 0, "No hay período abierto para hoy.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);
        var dto = ProcesarConRetencion(c.Value.CuentaOrigenId, c.Value.CuentaRetencionId, retId!.Value,
            neto: 875m, montoRetencion: 125m, baseRet: 1000m, porcentaje: 12.5m);
        var res = await service.MarkAsProcessedAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);
        var hdr = await LeerHdrAsync(orden, 1);
        Assert.NotNull(hdr);

        // Consulta por proveedor (misma empresa): encuentra el registro y su detalle.
        await using var ctxConsulta = CreateContext();
        var registro = new RetencionRegistroService(ctxConsulta, new TestCurrentCompanyService(CompanyId));
        var pagina = await registro.BuscarAsync(new RetencionRegistroFilterDto { CodProveedor = c.Value.CodProveedor }, CancellationToken.None);
        Assert.Contains(pagina.Items, x => x.RetencionHdrId == hdr!.HdrId && x.EstadoDescripcion == "VIGENTE");

        var detalle = await registro.GetDetalleAsync(hdr!.HdrId, CancellationToken.None);
        Assert.NotNull(detalle);
        Assert.Equal(125m, detalle!.Cabecera.TotalRetenido);
        Assert.Equal(125m, Assert.Single(detalle.Lineas).MontoRetenido);

        // Tenancy: una empresa distinta NO ve el registro.
        await using var ctxOtra = CreateContext(companyId: CompanyId + 990000);
        var registroOtra = new RetencionRegistroService(ctxOtra, new TestCurrentCompanyService(CompanyId + 990000));
        var paginaOtra = await registroOtra.BuscarAsync(new RetencionRegistroFilterDto { CodProveedor = c.Value.CodProveedor }, CancellationToken.None);
        Assert.DoesNotContain(paginaOtra.Items, x => x.RetencionHdrId == hdr!.HdrId);
        Assert.Null(await registroOtra.GetDetalleAsync(hdr!.HdrId, CancellationToken.None));
    }

    private async Task<long?> LeerPartidaIdAbonoAsync(int numeroOrden, int numeroAbono)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT partida_id FROM public.prv_compromiso_abono WHERE company_id=@c AND numero_orden=@n AND numero_abono=@a;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("a", numeroAbono);
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : (long)raw;
    }
}
