using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
/// apc/Program.cs activa Npgsql.EnableLegacyTimestampBehavior al arrancar el host real, lo que permite
/// escribir DateTime con Kind Unspecified/Local en columnas timestamptz (p.ej. con_partida_hdr via
/// sp_registrar_partida_contable en RegisterPartidaContableAsync). SIAD.Tests nunca corre ese Program.cs,
/// asi que sin este ModuleInitializer Npgsql aplica su modo estricto (solo Kind=Utc) y cualquier prueba
/// que llegue a registrar una partida contable revienta con
/// "Cannot write DateTime with Kind=Unspecified/Local to PostgreSQL type 'timestamp with time zone'".
/// Replica el mismo switch aqui para que el proceso de pruebas se comporte como el host real.
/// </summary>
internal static class NpgsqlTestTimestampSetup
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void EnableLegacyTimestampBehavior()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }
}

/// <summary>
/// Abonos parciales sobre prv_compromiso_hdr: saldo = monto - SUM(vigentes), estados 'V'/'A',
/// partida por abono, rechazo por exceso/metodo invalido, cierre (status_transacc) y anulacion.
/// Cada prueba siembra dentro de la transaccion del harness (BEGIN...ROLLBACK).
/// </summary>
[Collection("Postgres")]
public class AbonosCompromisoTests : IntegrationTestBase
{
    private const int OrdenBase = 970001;

    public AbonosCompromisoTests(PostgresFixture fixture) : base(fixture)
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
        // Ctor de 6 parametros (Task 7). Solo context + currentCompany son reales; el resto NSubstitute.
        var proveedores = Substitute.For<IProveedoresService>();
        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        var accountFormat = Substitute.For<IAccountFormatService>();
        // Sin stub, GetFormatAsync devuelve Task<AccountFormat> con resultado null (NSubstitute no configurado
        // sobre un record de referencia) y GetCuentasContraProcesamientoAsync truena con NullReferenceException
        // en BuildCuentaContraProcesamientoDisplay en cuanto exista >=1 fila ban_cuenta valida para la company.
        accountFormat.GetFormatAsync(Arg.Any<CancellationToken>()).Returns(AccountFormat.Default);
        var banTransacciones = Substitute.For<IBanTransaccionesService>();

        return new OrdenesPagoDirectoService(
            context,
            proveedores,
            new TestCurrentCompanyService(CompanyId),
            httpAccessor,
            accountFormat,
            banTransacciones);
    }

    // --- Siembra ---

    // cod_presupuestario = '0' es el "escape hatch" que PrepareDetallesAsync reconoce para lineas SIN
    // codigo de centro de costo (no pasa por LoadCentroCostoSaveMapAsync): solo exige que cuenta_gasto,
    // si se manda, exista en con_plan_cuentas como cuenta de gasto/ingreso posting/ACTIVE. Un codigo
    // inventado como el 'CP1' original revienta con "No existe el codigo presupuestario CP1" en cuanto
    // RegistrarAbonoAsync necesita generar la partida GEN del compromiso (primer abono).
    private async Task SeedCompromisoAsync(int numeroOrden, decimal monto, string? codProveedor = null, string cuentaGasto = "CG")
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor, status_transacc, anulado)
VALUES (@c, @n, now(), @m, 'abono test', @cp, FALSE, FALSE);

INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad,
     objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, @n, '0', '', '', 'Objeto de gasto de prueba', @cg, 'detalle abono', @m);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("m", monto);
        cmd.Parameters.AddWithValue("cp", (object?)codProveedor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cg", cuentaGasto);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task MarkProcesadoLegacyAsync(int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
UPDATE public.prv_compromiso_hdr SET status_transacc = TRUE
 WHERE company_id = @c AND numero_orden = @n;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        await cmd.ExecuteNonQueryAsync();
    }

    // Proveedor real (cod_proveedor) cuya cuenta_contable resuelve a una cuenta de con_plan_cuentas
    // posting/ACTIVE (requisito de BuildProviderProcessingPartidaLineasAsync para poder agregar el
    // credito automatico del proveedor), mas una SEGUNDA cuenta posting/ACTIVE DISTINTA que se usa como
    // contracuenta (origen/debito) del abono -- BuildProviderProcessingPartidaLineasAsync rechaza que la
    // contracuenta sea la misma cuenta contable del proveedor -- mas una cuenta de GASTO/INGRESO
    // posting/ACTIVE para sembrar prv_compromiso_dtl.cuenta_gasto (la necesita BuildCreatePartidaLineasAsync
    // cuando RegistrarAbonoAsync genera la partida GEN del compromiso en su primer abono).
    private readonly record struct ProveedorConContra(
        string CodProveedor, long CuentaProveedorAccountId, long CuentaContraAccountId, string CuentaGastoCode);

    private async Task<ProveedorConContra?> ResolveProveedorYContraPostingAsync()
    {
        string codProveedor;
        long cuentaProveedorId;
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT p.cod_proveedor, c.account_id
  FROM public.prv_proveedores p
  JOIN public.con_plan_cuentas c
    ON c.company_id = @c AND btrim(c.code) = btrim(p.cuenta_contable)
 WHERE p.company_id = @c
   AND c.allows_posting = TRUE AND c.status = 'ACTIVE'
 ORDER BY p.cod_proveedor
 LIMIT 1;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;
            codProveedor = reader.GetString(0);
            cuentaProveedorId = reader.GetInt64(1);
        }

        long contraId;
        await using (var cmd2 = Connection.CreateCommand())
        {
            cmd2.Transaction = Transaction;
            cmd2.CommandText = @"
SELECT account_id FROM public.con_plan_cuentas
 WHERE company_id = @c AND allows_posting = TRUE AND status = 'ACTIVE' AND account_id <> @excl
 ORDER BY account_id LIMIT 1;";
            cmd2.Parameters.AddWithValue("c", CompanyId);
            cmd2.Parameters.AddWithValue("excl", cuentaProveedorId);
            var raw = (long?)await cmd2.ExecuteScalarAsync();
            if (raw is null)
                return null;
            contraId = raw.Value;
        }

        await using var cmd3 = Connection.CreateCommand();
        cmd3.Transaction = Transaction;
        cmd3.CommandText = @"
SELECT code FROM public.con_plan_cuentas
 WHERE company_id = @c AND allows_posting = TRUE AND status = 'ACTIVE'
   AND upper(account_type) IN ('GASTO','GASTOS','EGRESO','EGRESOS','COSTO','COSTOS','INGRESO','INGRESOS')
 ORDER BY account_id LIMIT 1;";
        cmd3.Parameters.AddWithValue("c", CompanyId);
        var cuentaGasto = (string?)await cmd3.ExecuteScalarAsync();

        return cuentaGasto is null
            ? null
            : new ProveedorConContra(codProveedor, cuentaProveedorId, contraId, cuentaGasto);
    }

    // Siembra una ban_cuenta activa de tipo AHORRO (no CHEQUES) vinculada a una cuenta contable posting,
    // para ejercer de verdad la regla de "cuenta bancaria de origen debe ser de cheques" cuando MetodoPago
    // es Cheque (ver RegisterLinkedBankTransactionsAsync). El tenant de prueba solo tiene cuentas tipo
    // CHEQUES reales, por eso se siembra dentro de la transaccion del test (se revierte con el ROLLBACK).
    private async Task<long> SeedCuentaBancariaAhorroAsync(int numeroOrden, long cuentaContableId)
    {
        var codigo = $"TSTAHO{numeroOrden}";
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, tipo, currency_code, numero_cuenta, cont_account_id, activo, estado)
VALUES (@c, @code, 'Cuenta Ahorro Test', 'AHORRO', 'LPS', @numero, @cta, TRUE, 'ACTIVE')
RETURNING banco_cuenta_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("code", codigo);
        cmd.Parameters.AddWithValue("numero", codigo);
        cmd.Parameters.AddWithValue("cta", cuentaContableId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // --- Verificaciones ---

    private async Task<int> ContarAbonosAsync(int numeroOrden, string estado)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT count(*) FROM public.prv_compromiso_abono
 WHERE company_id = @c AND numero_orden = @n AND estado = @e;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("e", estado);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<bool> AbonoTienePartidaAsync(int numeroOrden, int numeroAbono)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT partida_id IS NOT NULL FROM public.prv_compromiso_abono
 WHERE company_id = @c AND numero_orden = @n AND numero_abono = @a;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("a", numeroAbono);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<bool> StatusTransaccAsync(int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT COALESCE(status_transacc, FALSE) FROM public.prv_compromiso_hdr
 WHERE company_id = @c AND numero_orden = @n;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Lee el partida_id (poliza_id) del abono desde prv_compromiso_abono.</summary>
    private async Task<long?> LeerPartidaIdAbonoAsync(int numeroOrden, int numeroAbono)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT partida_id FROM public.prv_compromiso_abono
 WHERE company_id = @c AND numero_orden = @n AND numero_abono = @a;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("a", numeroAbono);
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : (long)raw;
    }

    /// <summary>Lee las lineas (account_id, debit_amount, credit_amount) de una partida (poliza_id).</summary>
    private async Task<List<(long AccountId, decimal Debit, decimal Credit)>> LeerLineasPartidaAsync(long partidaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT account_id, debit_amount, credit_amount
  FROM public.con_partida_dtl
 WHERE company_id = @c AND poliza_id = @p
 ORDER BY line_number;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", partidaId);

        var lineas = new List<(long, decimal, decimal)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lineas.Add((reader.GetInt64(0), reader.GetDecimal(1), reader.GetDecimal(2)));
        }
        return lineas;
    }

    // Igual que la pagina real (CompromisoProveedorAbonar.razor.RegistrarAbonoAsync): solo se envia la
    // CONTRACUENTA (origen, debito) via CuentaContraId; el CREDITO al proveedor lo agrega automaticamente
    // BuildProviderProcessingPartidaLineasAsync desde la cuenta_contable del proveedor del compromiso.
    // NormalizeContraProcessingLinesAsync rechaza cualquier linea de contracuenta con Credito != 0.
    private static AbonoCompromisoUpsertDto AbonoContable(decimal monto, long ctaContra) => new()
    {
        Monto = monto,
        MetodoPago = OrdenPagoDirectoMetodoPago.Contable,
        CuentaContraId = ctaContra,
        Usuario = "tester",
        Fecha = DateTime.Now.Date,
    };

    // (1) Sin abonos: saldo == monto, no pagado.
    [SkippableFact]
    public async Task Saldo_SinAbonos_EsIgualAlMonto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 1;
        await SeedCompromisoAsync(orden, 1000m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);

        Assert.NotNull(saldo);
        Assert.Equal(1000m, saldo!.Monto);
        Assert.Equal(0m, saldo.Abonado);
        Assert.Equal(1000m, saldo.Saldo);
        Assert.False(saldo.Pagado);
        Assert.Empty(saldo.Abonos);
    }

    // (2) Abono parcial: baja el saldo, crea fila 'V' y su partida.
    [SkippableFact]
    public async Task AbonoParcial_BajaSaldo_CreaFilaVigenteYPartida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 2;
        await SeedCompromisoAsync(orden, 1000m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(400m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);
        Assert.True(res.Success, res.Message);
        Assert.Equal(1, res.NumeroAbono);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(400m, saldo!.Abonado);
        Assert.Equal(600m, saldo.Saldo);
        Assert.False(saldo.Pagado);

        Assert.Equal(1, await ContarAbonosAsync(orden, "V"));
        Assert.True(await AbonoTienePartidaAsync(orden, 1));
        Assert.False(await StatusTransaccAsync(orden));
    }

    // (3) Abono que excede el saldo: rechazado, sin fila.
    [SkippableFact]
    public async Task Abono_QueExcedeSaldo_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 3;
        await SeedCompromisoAsync(orden, 500m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        // RegistrarAbonoAsync tolera diferencias de redondeo hasta 0.01 (dto.Monto - saldo > 0.01m):
        // 500.01 contra un saldo de 500 cae justo en el borde de esa tolerancia y el servicio lo acepta.
        // 500.50 excede la tolerancia sin ambiguedad y ejerce el rechazo real.
        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(500.50m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
    }

    // (4) Cheque contra cuenta bancaria de ORIGEN que no es de cheques: rechazado.
    // La regla vive en RegisterLinkedBankTransactionsAsync sobre la cuenta bancaria de origen (contra),
    // no sobre la cuenta destino del proveedor (que el servicio ni siquiera mira para esta validacion).
    [SkippableFact]
    public async Task Abono_Cheque_DesdeCuentaAhorro_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 4;
        await SeedCompromisoAsync(orden, 1000m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);
        // El tenant de prueba solo tiene ban_cuenta reales de tipo CHEQUES: se siembra una de AHORRO
        // (no CHEQ) dentro de la transaccion del test para ejercer la regla real, en vez de simularla.
        var bancoCuentaId = await SeedCuentaBancariaAhorroAsync(orden, cuentas.Value.CuentaContraAccountId);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new AbonoCompromisoUpsertDto
        {
            Monto = 200m,
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            Usuario = "tester",
            Fecha = DateTime.Now.Date,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new() { BancoCuentaId = bancoCuentaId, Descripcion = "abono cheque desde ahorro", Debito = 200m, Credito = 0m }
            }
        };

        // La regla de cheque es un error de negocio "duro": RegisterLinkedBankTransactionsAsync lanza
        // InvalidOperationException (no retorna Success=false). RegistrarAbonoAsync no la atrapa; el
        // controller la traduce a 409 (OrdenesPagoDirectoController.RegistrarAbono, catch InvalidOperationException).
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegistrarAbonoAsync(orden, dto, CancellationToken.None));

        Assert.Contains("cuenta de cheques", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
    }

    // (5) Abonar el 100%: status_transacc=TRUE y Pagado.
    [SkippableFact]
    public async Task AbonoTotal_MarcaPagado_YStatusTransacc()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 5;
        await SeedCompromisoAsync(orden, 750m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(750m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);
        Assert.True(res.Success, res.Message);
        Assert.True(res.Pagado);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(0m, saldo!.Saldo);
        Assert.True(saldo.Pagado);
        Assert.True(await StatusTransaccAsync(orden));
    }

    // (6) Anular el ultimo abono: 'A', restaura saldo, reabre Pagado.
    [SkippableFact]
    public async Task AnularUltimoAbono_LoPoneAnulado_RestauraSaldo_YReabrePagado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 6;
        await SeedCompromisoAsync(orden, 300m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var reg = await service.RegistrarAbonoAsync(orden, AbonoContable(300m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);
        Assert.True(reg.Success, reg.Message);
        Assert.True(await StatusTransaccAsync(orden));

        var anu = await service.AnularAbonoAsync(orden, numeroAbono: 1, new AnularOrdenPagoDirectoDto { Motivo = "prueba" }, CancellationToken.None);
        Assert.True(anu.Success, anu.Message);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(0m, saldo!.Abonado);
        Assert.Equal(300m, saldo.Saldo);
        Assert.False(saldo.Pagado);

        Assert.Equal(1, await ContarAbonosAsync(orden, "A"));
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
        Assert.False(await StatusTransaccAsync(orden));
    }

    // (7) No se puede anular un abono que no es el ultimo vigente.
    [SkippableFact]
    public async Task AnularAbono_QueNoEsElUltimoVigente_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 7;
        await SeedCompromisoAsync(orden, 1000m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var a1 = await service.RegistrarAbonoAsync(orden, AbonoContable(200m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);
        Assert.True(a1.Success, a1.Message);
        var a2 = await service.RegistrarAbonoAsync(orden, AbonoContable(300m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);
        Assert.True(a2.Success, a2.Message);

        var res = await service.AnularAbonoAsync(orden, numeroAbono: 1, new AnularOrdenPagoDirectoDto { Motivo = "x" }, CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(2, await ContarAbonosAsync(orden, "V"));
        Assert.Equal(0, await ContarAbonosAsync(orden, "A"));
    }

    // (8) Compat legacy: procesado sin abonos => saldo 0, no acepta abonos.
    [SkippableFact]
    public async Task Legacy_ProcesadoSinAbonos_SaldoCeroYNoAceptaAbonos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 8;
        await SeedCompromisoAsync(orden, 1200m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);
        await MarkProcesadoLegacyAsync(orden);

        await using var context = CreateContext();
        var service = CreateService(context);

        var saldo = await service.GetSaldoConAbonosAsync(orden, CancellationToken.None);
        Assert.Equal(0m, saldo!.Saldo);
        Assert.True(saldo.Pagado);
        Assert.Empty(saldo.Abonos);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(100m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);

        Assert.False(res.Success);
        Assert.Equal(0, await ContarAbonosAsync(orden, "V"));
    }

    // (9) Direccion contable del abono: Proveedor al DEBE, Banco/contra al HABER (contabilidad estandar).
    // Fija la inversion hecha en BuildProviderProcessingPartidaLineasAsync leyendo directamente
    // con_partida_dtl de la partida generada por el abono (no confia solo en los DTOs de retorno).
    [SkippableFact]
    public async Task AbonoParcial_Partida_ProveedorAlDebe_BancoAlHaber()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var cuentas = await ResolveProveedorYContraPostingAsync();
        Skip.If(cuentas is null, "No hay proveedor con cuenta_contable posting/ACTIVE y una segunda cuenta posting/ACTIVE distinta en el tenant de prueba.");

        const int orden = OrdenBase + 9;
        await SeedCompromisoAsync(orden, 1000m, codProveedor: cuentas!.Value.CodProveedor, cuentaGasto: cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var res = await service.RegistrarAbonoAsync(orden, AbonoContable(400m, cuentas.Value.CuentaContraAccountId), CancellationToken.None);
        Assert.True(res.Success, res.Message);
        Assert.Equal(1, res.NumeroAbono);

        var partidaId = await LeerPartidaIdAbonoAsync(orden, res.NumeroAbono);
        Assert.NotNull(partidaId);

        var lineas = await LeerLineasPartidaAsync(partidaId!.Value);
        Assert.NotEmpty(lineas);

        var lineasProveedor = lineas.Where(l => l.AccountId == cuentas.Value.CuentaProveedorAccountId).ToList();
        var lineaProveedor = Assert.Single(lineasProveedor);
        Assert.True(lineaProveedor.Debit > 0m, "La cuenta del proveedor debe quedar al DEBE por el monto del abono.");
        Assert.Equal(0m, lineaProveedor.Credit);
        Assert.Equal(400m, lineaProveedor.Debit);

        var lineasContra = lineas.Where(l => l.AccountId != cuentas.Value.CuentaProveedorAccountId).ToList();
        Assert.NotEmpty(lineasContra);
        Assert.All(lineasContra, l =>
        {
            Assert.True(l.Credit > 0m, "La(s) linea(s) de la contracuenta/banco deben quedar al HABER.");
            Assert.Equal(0m, l.Debit);
        });

        var totalDebe = lineas.Sum(l => l.Debit);
        var totalHaber = lineas.Sum(l => l.Credit);
        Assert.Equal(totalDebe, totalHaber);
        Assert.Equal(400m, totalDebe);
    }
}
