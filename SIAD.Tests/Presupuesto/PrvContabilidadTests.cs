using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using NSubstitute;
using Xunit;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Services.Presupuesto;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// F0 — el asiento de PAGO de compromisos/OPD (procesar + abonos) se postea POSTED al mayor por el
/// motor config-driven (module='PROV'), a diferencia del camino viejo que lo dejaba en borrador
/// (status=0) invisible al mayor. Cubre: POSTED (status=1) + impacto en con_saldo_cuenta, banco una
/// sola vez, reverso, idempotencia del motor, encolado sin período y compatibilidad al anular abonos
/// pre-F0. Requiere la config PROV en el mirror (Database/2026-08-07_con_integracion_prov_activar.sql):
/// con_integracion_asiento module='PROV' + con_integracion_config.activo_proveedores=TRUE.
/// Cada prueba siembra dentro de la transacción del harness (BEGIN...ROLLBACK).
/// </summary>
[Collection("Postgres")]
public class PrvContabilidadTests : IntegrationTestBase
{
    private const int OrdenBase = 980001;

    public PrvContabilidadTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }

    private SiadDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        var context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
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

    // --- Siembra ---

    private async Task SeedCompromisoAsync(int numeroOrden, decimal monto, string codProveedor, string cuentaGasto, DateTime? fecha = null)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor, status_transacc, anulado)
VALUES (@c, @n, @f, @m, 'prv contab test', @cp, FALSE, FALSE);

INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad, objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, @n, '0', '', '', 'Objeto de gasto de prueba', @cg, 'detalle', @m);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("f", (object?)(fecha?.Date) ?? DateTime.Now.Date);
        cmd.Parameters.AddWithValue("m", monto);
        cmd.Parameters.AddWithValue("cp", codProveedor);
        cmd.Parameters.AddWithValue("cg", cuentaGasto);
        await cmd.ExecuteNonQueryAsync();
    }

    // Proveedor real + 3 cuentas posting/ACTIVE distintas (origen, retención pasivo, banco) con sus CÓDIGOS
    // (para leer con_saldo_cuenta, que se indexa por codigo_cuenta) + una ban_cuenta de procesamiento + gasto.
    private readonly record struct CuentasRet(
        string CodProveedor,
        long CuentaProveedorId, string CuentaProveedorCode,
        long CuentaOrigenId, string CuentaOrigenCode,
        long CuentaRetencionId, string CuentaRetencionCode,
        long CuentaBancoId, string CuentaBancoCode,
        long BancoCuentaId,
        string CuentaGastoCode);

    private async Task<long> SeedCuentaBancariaAsync(int numeroOrden, long cuentaContableId)
    {
        var codigo = $"TSTPRV{numeroOrden}";
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, tipo, currency_code, numero_cuenta, cont_account_id, activo, estado)
VALUES (@c, @code, 'Cuenta PRV Test', 'CHEQUES', 'LPS', @numero, @cta, TRUE, 'ACTIVE')
RETURNING banco_cuenta_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("code", codigo);
        cmd.Parameters.AddWithValue("numero", codigo);
        cmd.Parameters.AddWithValue("cta", cuentaContableId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<CuentasRet?> ResolveCuentasAsync(int numeroOrden)
    {
        string codProveedor; long provId; string provCode;
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT p.cod_proveedor, c.account_id, c.code
  FROM public.prv_proveedores p
  JOIN public.con_plan_cuentas c ON c.company_id=@c AND btrim(c.code)=btrim(p.cuenta_contable)
 WHERE p.company_id=@c AND c.allows_posting=TRUE AND c.status='ACTIVE'
 ORDER BY p.cod_proveedor LIMIT 1;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            codProveedor = r.GetString(0); provId = r.GetInt64(1); provCode = r.GetString(2);
        }

        var otras = new List<(long Id, string Code)>();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT account_id, code FROM public.con_plan_cuentas
 WHERE company_id=@c AND allows_posting=TRUE AND status='ACTIVE' AND account_id<>@prov
 ORDER BY account_id LIMIT 3;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("prov", provId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) otras.Add((r.GetInt64(0), r.GetString(1)));
        }
        if (otras.Count < 3) return null;

        var bancoCuentaId = await SeedCuentaBancariaAsync(numeroOrden, otras[2].Id);

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

        return new CuentasRet(
            codProveedor, provId, provCode,
            otras[0].Id, otras[0].Code,
            otras[1].Id, otras[1].Code,
            otras[2].Id, otras[2].Code,
            bancoCuentaId, cuentaGasto);
    }

    // Igual que CompromisoProveedorAbonar.razor con retención: Lineas=[ origen{Credito=neto}, retencion{Credito=monto} ].
    private static AbonoCompromisoUpsertDto AbonoConRetencion(
        decimal monto, decimal neto, long cuentaOrigenId, long? bancoCuentaId,
        long cuentaRetencionId, decimal montoRetencion, string metodoPago, DateTime? fecha = null) => new()
        {
            Monto = monto,
            MetodoPago = metodoPago,
            Usuario = "tester",
            Fecha = fecha ?? DateTime.Now.Date,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new() { CuentaId = cuentaOrigenId, BancoCuentaId = bancoCuentaId, Descripcion = "pago neto", Debito = 0m, Credito = neto },
                new() { CuentaId = cuentaRetencionId, Descripcion = "retencion", Debito = 0m, Credito = montoRetencion }
            }
        };

    // --- Lecturas ---

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

    private async Task<short?> LeerStatusPartidaAsync(long polizaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT status FROM public.con_partida_hdr WHERE company_id=@c AND poliza_id=@p;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", polizaId);
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : Convert.ToInt16(raw);
    }

    private async Task<List<(long AccountId, decimal Debit, decimal Credit)>> LeerLineasPartidaAsync(long polizaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT account_id, debit_amount, credit_amount FROM public.con_partida_dtl WHERE company_id=@c AND poliza_id=@p ORDER BY line_number;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", polizaId);
        var lineas = new List<(long, decimal, decimal)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) lineas.Add((r.GetInt64(0), r.GetDecimal(1), r.GetDecimal(2)));
        return lineas;
    }

    /// <summary>Saldo neto (creditos - debitos) del mayor por código de cuenta y período (con_saldo_cuenta, solo status=1).</summary>
    private async Task<decimal> LeerSaldoMayorAsync(string codigoCuenta, long periodoId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT COALESCE(SUM(creditos - debitos), 0)
  FROM public.con_saldo_cuenta
 WHERE company_id=@c AND periodo_id=@p AND btrim(codigo_cuenta)=btrim(@code);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", periodoId);
        cmd.Parameters.AddWithValue("code", codigoCuenta);
        return (decimal)(await cmd.ExecuteScalarAsync())!;
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

    private async Task<List<(decimal Monto, long BancoCuentaId)>> LeerMovimientosBancoAsync(long polizaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT monto, banco_cuenta_id FROM public.ban_kardex WHERE company_id=@c AND partida_cuenta_id=@p ORDER BY ban_kardex_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", polizaId);
        var m = new List<(decimal, long)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) m.Add((r.GetDecimal(0), r.GetInt64(1)));
        return m;
    }

    private async Task<int> ContarPartidasDocAsync(string docType, int documentId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT count(*) FROM public.con_partida_hdr WHERE company_id=@c AND module='PROV' AND document_type=@dt AND document_id=@id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("dt", docType);
        cmd.Parameters.AddWithValue("id", (long)documentId);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<int> ContarPendientesAsync(string origenTipo, int origenId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT count(*) FROM public.con_partida_pendiente WHERE company_id=@c AND module='PROV' AND origen_tipo=@t AND origen_id=@id AND status_id=1;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("t", origenTipo);
        cmd.Parameters.AddWithValue("id", (long)origenId);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    // =====================================================================================
    // (1) Abono con retención → la partida queda POSTED (status=1) e impacta el mayor.
    // A diferencia de hoy (status=0, invisible al mayor), la retención llega a con_saldo_cuenta.
    // =====================================================================================
    [SkippableFact]
    public async Task AbonoConRetencion_PosteaPosted_YRetencionLlegaAlMayor()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 1;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 2000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);
        var periodo = await PeriodoAbiertoAsync(DateTime.Now.Date);
        Skip.If(periodo <= 0, "No hay período contable abierto para hoy en el mirror.");

        var retencionAntes = await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaOrigenId, bancoCuentaId: null,
            c.Value.CuentaRetencionId, 125m, OrdenPagoDirectoMetodoPago.Contable);
        var res = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        var poliza = await LeerPartidaIdAbonoAsync(orden, res.NumeroAbono);
        Assert.NotNull(poliza);

        // ★ La diferencia con hoy: la partida del pago queda POSTED (status=1), no en borrador (0).
        Assert.Equal((short)1, await LeerStatusPartidaAsync(poliza!.Value));

        // La retención llega al mayor: la cuenta sube su saldo acreedor por el retenido.
        var retencionDespues = await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo);
        Assert.Equal(125m, retencionDespues - retencionAntes);
    }

    // =====================================================================================
    // (2) Procesar con retención → POSTED. Mismo motor por el camino del PROCESAR.
    // =====================================================================================
    [SkippableFact]
    public async Task ProcesarConRetencion_PosteaPosted()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 2;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);
        var periodo = await PeriodoAbiertoAsync(DateTime.Now.Date);
        Skip.If(periodo <= 0, "No hay período contable abierto para hoy en el mirror.");

        await using var context = CreateContext();
        var service = CreateService(context);

        // Procesar paga el saldo completo (1000): proveedor DEBE 1000, retención HABER 125, origen HABER 875.
        var dto = new ProcesarOrdenPagoDirectoDto
        {
            MetodoPago = OrdenPagoDirectoMetodoPago.Contable,
            Usuario = "tester",
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new() { CuentaId = c.Value.CuentaOrigenId, Descripcion = "pago neto", Debito = 0m, Credito = 875m },
                new() { CuentaId = c.Value.CuentaRetencionId, Descripcion = "retencion", Debito = 0m, Credito = 125m }
            }
        };
        var res = await service.MarkAsProcessedAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        // El procesar inserta su fila de abono por el saldo; su partida es la del pago (POSTED).
        var poliza = await LeerPartidaIdAbonoAsync(orden, 1);
        Assert.NotNull(poliza);
        Assert.Equal((short)1, await LeerStatusPartidaAsync(poliza!.Value));

        var lineas = await LeerLineasPartidaAsync(poliza.Value);
        var lineaProv = Assert.Single(lineas.Where(l => l.AccountId == c.Value.CuentaProveedorId));
        Assert.Equal(1000m, lineaProv.Debit);
        var lineaRet = Assert.Single(lineas.Where(l => l.AccountId == c.Value.CuentaRetencionId));
        Assert.Equal(125m, lineaRet.Credit);
    }

    // =====================================================================================
    // (3) Abono por transferencia con retención → el banco se contabiliza UNA sola vez:
    // una única línea de banco al HABER por el neto y un único movimiento en ban_kardex por el neto.
    // =====================================================================================
    [SkippableFact]
    public async Task AbonoConRetencion_Transferencia_BancoContabilizadoUnaVez()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 3;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 2000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);
        Skip.If(await PeriodoAbiertoAsync(DateTime.Now.Date) <= 0, "No hay período abierto para hoy.");

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaBancoId, c.Value.BancoCuentaId,
            c.Value.CuentaRetencionId, 125m, OrdenPagoDirectoMetodoPago.Transferencia);
        var res = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        var poliza = await LeerPartidaIdAbonoAsync(orden, res.NumeroAbono);
        Assert.NotNull(poliza);
        Assert.Equal((short)1, await LeerStatusPartidaAsync(poliza!.Value));

        // UNA sola línea contable de banco (la cuenta del banco) al HABER por el neto.
        var lineas = await LeerLineasPartidaAsync(poliza.Value);
        var lineasBanco = lineas.Where(l => l.AccountId == c.Value.CuentaBancoId).ToList();
        var lineaBanco = Assert.Single(lineasBanco);
        Assert.Equal(875m, lineaBanco.Credit);
        Assert.Equal(0m, lineaBanco.Debit);

        // UN solo movimiento de kardex vinculado a la partida, por el neto (no el bruto, no doble).
        var movs = await LeerMovimientosBancoAsync(poliza.Value);
        var mov = Assert.Single(movs);
        Assert.Equal(875m, Math.Abs(mov.Monto));
        Assert.Equal(c.Value.BancoCuentaId, mov.BancoCuentaId);
    }

    // =====================================================================================
    // (4) Anular el abono (post-F0) → el motor revierte la partida y el saldo del mayor vuelve;
    // el compromiso se reabre.
    // =====================================================================================
    [SkippableFact]
    public async Task AnularAbono_PorMotor_RevierteMayor_YReabre()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 4;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);
        var periodo = await PeriodoAbiertoAsync(DateTime.Now.Date);
        Skip.If(periodo <= 0, "No hay período abierto para hoy.");

        var retencionAntes = await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo);

        await using var context = CreateContext();
        var service = CreateService(context);

        // Abono total (1000) con retención 125: liquida el compromiso (queda pagado).
        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaOrigenId, bancoCuentaId: null,
            c.Value.CuentaRetencionId, 125m, OrdenPagoDirectoMetodoPago.Contable);
        var reg = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);
        Assert.True(reg.Success, reg.Message);
        Assert.True(reg.Pagado);
        Assert.Equal(125m, await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo) - retencionAntes);

        var anu = await service.AnularAbonoAsync(orden, reg.NumeroAbono, new AnularOrdenPagoDirectoDto { Motivo = "prueba F0" }, CancellationToken.None);
        Assert.True(anu.Success, anu.Message);

        // El mayor vuelve a su saldo previo (el reverso del motor compensa la retención).
        Assert.Equal(0m, await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo) - retencionAntes);

        // El compromiso se reabre (ya no está pagado) y el saldo vuelve al total.
        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.False(saldo!.Pagado);
        Assert.Equal(1000m, saldo.Saldo);
    }

    // =====================================================================================
    // (5) Idempotencia del motor para PROV: reintentar el mismo documento (module, docType, documentId)
    // devuelve la MISMA póliza, sin duplicar la partida ni el saldo.
    // =====================================================================================
    [SkippableFact]
    public async Task Motor_Idempotente_NoDuplicaLaPartidaDelPago()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 5;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);
        var periodo = await PeriodoAbiertoAsync(DateTime.Now.Date);
        Skip.If(periodo <= 0, "No hay período abierto para hoy.");

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaOrigenId, bancoCuentaId: null,
            c.Value.CuentaRetencionId, 125m, OrdenPagoDirectoMetodoPago.Contable);
        var reg = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);
        Assert.True(reg.Success, reg.Message);

        var poliza = await LeerPartidaIdAbonoAsync(orden, reg.NumeroAbono);
        Assert.NotNull(poliza);
        var docType = $"OPD-ABO{reg.NumeroAbono}";
        Assert.Equal(1, await ContarPartidasDocAsync(docType, orden));
        var retencionTrasUno = await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo);

        // Reintento del MISMO documento por el motor (el SP corta por idempotencia ANTES de validar líneas):
        // devuelve la misma póliza y NO crea una segunda partida ni duplica el saldo.
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "SELECT public.sp_con_generar_comprobante_config(@c,'PROV',@dt,@id,'reintento',now()::date,'reintento','tester','[]'::jsonb);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("dt", docType);
        cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = (long)orden });
        var polizaReintento = (long)(await cmd.ExecuteScalarAsync())!;

        Assert.Equal(poliza!.Value, polizaReintento);
        Assert.Equal(1, await ContarPartidasDocAsync(docType, orden));
        Assert.Equal(retencionTrasUno, await LeerSaldoMayorAsync(c.Value.CuentaRetencionCode, periodo));
    }

    // =====================================================================================
    // (6) Encolado sin período abierto: con fecha en período CERRADO el motor encola la partida y
    // devuelve null; el abono se registra igual (partida_id null), sin romper, con entrada en la cola.
    // =====================================================================================
    [SkippableFact]
    public async Task AbonoSinPeriodoAbierto_Encola_NoRompe()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 6;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        // Fecha en período CERRADO (existe el período pero no está abierto → el motor encola).
        var fechaCerrada = new DateTime(2026, 6, 15);
        Skip.If(await PeriodoAbiertoAsync(fechaCerrada) > 0, "2026-06-15 está abierto en este mirror; ajustar la fecha del test.");
        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode, fecha: fechaCerrada);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = AbonoConRetencion(1000m, 875m, c.Value.CuentaOrigenId, bancoCuentaId: null,
            c.Value.CuentaRetencionId, 125m, OrdenPagoDirectoMetodoPago.Contable, fecha: fechaCerrada);
        var res = await service.RegistrarAbonoAsync(orden, dto, CancellationToken.None);

        // No rompe: el abono se registra aunque la partida quede encolada.
        Assert.True(res.Success, res.Message);
        Assert.Null(await LeerPartidaIdAbonoAsync(orden, res.NumeroAbono));
        // La partida quedó en la cola (con_partida_pendiente), no en el mayor.
        Assert.Equal(1, await ContarPendientesAsync($"OPD-ABO{res.NumeroAbono}", orden));
        Assert.Equal(0, await ContarPartidasDocAsync($"OPD-ABO{res.NumeroAbono}", orden));
    }

    // =====================================================================================
    // (7) Compatibilidad: anular un abono PRE-F0 (partida por el camino viejo, sin comprobante del
    // motor con la llave OPD-ABO{n}) usa el camino viejo (contrapartida por poliza_id), no el motor.
    // =====================================================================================
    [SkippableFact]
    public async Task AnularAbono_PreF0_UsaCaminoViejo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        const int orden = OrdenBase + 7;
        var c = await ResolveCuentasAsync(orden);
        Skip.If(c is null, "No hay proveedor + 3 cuentas posting/ACTIVE en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, c!.Value.CodProveedor, c.Value.CuentaGastoCode);
        var periodo = await PeriodoAbiertoAsync(DateTime.Now.Date);
        Skip.If(periodo <= 0, "No hay período abierto para hoy.");

        // Simular un abono PRE-F0: partida por el camino viejo (sp_registrar_partida_contable →
        // document_type='OPD', BORRADOR), NO por el motor (que usaría docType 'OPD-ABO1').
        var journalId = await EscalarAsync("SELECT journal_id FROM public.con_diario WHERE company_id=@c AND is_active ORDER BY journal_id LIMIT 1;");
        var typeId = await EscalarAsync("SELECT type_id FROM public.con_tipo_transaccion WHERE company_id=@c AND status IN ('ACTIVE','ACTIVO') ORDER BY type_id LIMIT 1;");
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
CALL public.sp_registrar_partida_contable(
  @c, @j, @per, 'PROV', 'OPD', @docnum, @docnum, now(), 'pre-F0',
  'tester', @ty,
  ARRAY[
    ROW(@prov, NULL, 'prov', 1000, 0, NULL, 'HNL', 1)::public.tipo_linea_partida,
    ROW(@orig, NULL, 'orig', 0, 1000, NULL, 'HNL', 1)::public.tipo_linea_partida
  ]);";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("j", journalId);
            cmd.Parameters.AddWithValue("per", periodo);
            cmd.Parameters.AddWithValue("docnum", $"OPD-{orden}");
            cmd.Parameters.AddWithValue("ty", typeId);
            cmd.Parameters.AddWithValue("prov", c!.Value.CuentaProveedorId);
            cmd.Parameters.AddWithValue("orig", c.Value.CuentaOrigenId);
            await cmd.ExecuteNonQueryAsync();
        }
        var polizaVieja = await EscalarAsync("SELECT poliza_id FROM public.con_partida_hdr WHERE company_id=@c AND module='PROV' AND document_type='OPD' AND btrim(document_number)=btrim(@d) ORDER BY poliza_id DESC LIMIT 1;", ("d", $"OPD-{orden}"));
        Assert.True(polizaVieja > 0);
        Assert.Equal((short)0, await LeerStatusPartidaAsync(polizaVieja)); // borrador (camino viejo)

        // Insertar la fila de abono 'V' pre-F0 ligada a esa partida vieja (sin banco).
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
INSERT INTO public.prv_compromiso_abono
    (company_id, numero_orden, numero_abono, fecha, monto, metodo_pago, cuenta_contra_id, partida_id, estado, usuario_creo)
VALUES (@c, @n, 1, now(), 1000, 'CONTABLE', @orig, @pid, 'V', 'tester');
UPDATE public.prv_compromiso_hdr SET status_transacc=TRUE WHERE company_id=@c AND numero_orden=@n;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("n", orden);
            cmd.Parameters.AddWithValue("orig", c.Value.CuentaOrigenId);
            cmd.Parameters.AddWithValue("pid", polizaVieja);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var context = CreateContext();
        var service = CreateService(context);

        var anu = await service.AnularAbonoAsync(orden, 1, new AnularOrdenPagoDirectoDto { Motivo = "reverso pre-F0" }, CancellationToken.None);
        Assert.True(anu.Success, anu.Message);

        // El reverso NO lo hizo el motor (no hay comprobante OPD-ABO1): fue el camino viejo, que crea
        // una contrapartida en BORRADOR (status=0) por poliza_id. Verificamos que exista esa contrapartida.
        var reversoId = await EscalarAsync("SELECT partida_reverso_id FROM public.prv_compromiso_abono WHERE company_id=@c AND numero_orden=@n AND numero_abono=1;", ("n", orden));
        Assert.True(reversoId > 0);
        Assert.Equal((short)0, await LeerStatusPartidaAsync(reversoId)); // contrapartida vieja = borrador
        Assert.Equal(0, await ContarPartidasDocAsync("OPD-ABO1", orden));  // el motor nunca posteó este pago
    }

    private async Task<long> EscalarAsync(string sql, params (string Name, object Value)[] extra)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("c", CompanyId);
        foreach (var (name, value) in extra) cmd.Parameters.AddWithValue(name, value);
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? 0 : Convert.ToInt64(raw);
    }
}
