using System;
using System.Collections.Generic;
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
/// Modelo GENERAL de retenciones/deducciones en el PROCESAMIENTO de compromisos
/// (OrdenesPagoDirectoService.MarkAsProcessedAsync). A diferencia del modelo "contra-magnitud"
/// que sigue usando RegistrarAbonoAsync, aqui cada linea del DTO trae su Debito/Credito REAL
/// (excluyente por linea) y el proveedor se agrega automatico al DEBE por el bruto. El banco
/// (linea(s) con BancoCuentaId) va al HABER por el NETO = bruto + Sum(Debe deduccion) - Sum(Haber deduccion).
/// Cada prueba siembra dentro de la transaccion del harness (BEGIN...ROLLBACK).
/// </summary>
[Collection("Postgres")]
public class ProcesamientoRetencionesTests : IntegrationTestBase
{
    private const int OrdenBase = 973001;

    public ProcesamientoRetencionesTests(PostgresFixture fixture) : base(fixture)
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

        return new OrdenesPagoDirectoService(
            context,
            proveedores,
            new TestCurrentCompanyService(CompanyId),
            httpAccessor,
            accountFormat,
            banTransacciones);
    }

    // --- Siembra (misma forma que AbonosCompromisoTests.SeedCompromisoAsync) ---

    private async Task SeedCompromisoAsync(int numeroOrden, decimal monto, string? codProveedor, string cuentaGasto)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.prv_compromiso_hdr
    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor, status_transacc, anulado)
VALUES (@c, @n, now(), @m, 'procesamiento test', @cp, FALSE, FALSE);

INSERT INTO public.prv_compromiso_dtl
    (company_id, numero_orden, cod_presupuestario, programa, actividad,
     objeto_gasto, cuenta_gasto, descripcion, monto)
VALUES (@c, @n, '0', '', '', 'Objeto de gasto de prueba', @cg, 'detalle procesamiento', @m);";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("n", numeroOrden);
        cmd.Parameters.AddWithValue("m", monto);
        cmd.Parameters.AddWithValue("cp", (object?)codProveedor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cg", cuentaGasto);
        await cmd.ExecuteNonQueryAsync();
    }

    // Proveedor real + cuenta bancaria de procesamiento real (ban_cuenta.activo con cont_account_id
    // posting/ACTIVE, distinta de la del proveedor) + 2 cuentas posting/ACTIVE adicionales y distintas
    // para las retenciones + una cuenta de gasto para sembrar prv_compromiso_dtl.cuenta_gasto.
    private readonly record struct CuentasProceso(
        string CodProveedor,
        long CuentaProveedorAccountId,
        long BancoCuentaId,
        long CuentaBancoAccountId,
        long CuentaRetencion1AccountId,
        long CuentaRetencion2AccountId,
        string CuentaGastoCode);

    // El tenant de prueba (mirror) solo tiene ban_cuenta reales con activo=TRUE SIN cont_account_id
    // (las filas con cont_account_id poblado estan todas INACTIVE). Por eso, igual que
    // AbonosCompromisoTests.SeedCuentaBancariaAhorroAsync, se siembra una ban_cuenta propia dentro de
    // la transaccion del test (se revierte con el ROLLBACK) en vez de depender de datos reales.
    private async Task<long> SeedCuentaBancariaProcesamientoAsync(int numeroOrden, long cuentaContableId)
    {
        var codigo = $"TSTPRC{numeroOrden}";
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, tipo, currency_code, numero_cuenta, cont_account_id, activo, estado)
VALUES (@c, @code, 'Cuenta Procesamiento Test', 'CHEQUES', 'LPS', @numero, @cta, TRUE, 'ACTIVE')
RETURNING banco_cuenta_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("code", codigo);
        cmd.Parameters.AddWithValue("numero", codigo);
        cmd.Parameters.AddWithValue("cta", cuentaContableId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<CuentasProceso?> ResolveCuentasProcesoAsync(int numeroOrden)
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

        // 3 cuentas posting/ACTIVE distintas de la del proveedor: una para la cuenta contable del
        // banco de procesamiento y dos para las retenciones.
        var otras = new List<long>();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
SELECT account_id FROM public.con_plan_cuentas
 WHERE company_id = @c AND allows_posting = TRUE AND status = 'ACTIVE'
   AND account_id <> @prov
 ORDER BY account_id
 LIMIT 3;";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("prov", cuentaProveedorId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                otras.Add(reader.GetInt64(0));
            }
        }
        if (otras.Count < 3)
            return null;

        var cuentaBancoAccountId = otras[0];
        var cuentaRetencion1AccountId = otras[1];
        var cuentaRetencion2AccountId = otras[2];

        var bancoCuentaId = await SeedCuentaBancariaProcesamientoAsync(numeroOrden, cuentaBancoAccountId);

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
            : new CuentasProceso(
                codProveedor, cuentaProveedorId, bancoCuentaId, cuentaBancoAccountId,
                cuentaRetencion1AccountId, cuentaRetencion2AccountId, cuentaGasto);
    }

    // --- Verificaciones ---

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

    /// <summary>Ultimo poliza_id registrado para el compromiso (la partida PRC, la mas reciente).</summary>
    private async Task<long?> LeerUltimoPolizaIdAsync(int numeroOrden)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT poliza_id FROM public.con_partida_hdr
 WHERE company_id = @c AND ""module"" = 'PROV' AND document_type = 'OPD'
   AND btrim(coalesce(document_number,'')) = btrim(@doc)
 ORDER BY poliza_id DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("doc", $"OPD-{numeroOrden}");
        var raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : (long)raw;
    }

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

    private async Task<List<(decimal Monto, long BancoCuentaId)>> LeerMovimientosBancoAsync(long partidaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT monto, banco_cuenta_id FROM public.ban_kardex
 WHERE company_id = @c AND partida_cuenta_id = @p
 ORDER BY ban_kardex_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", partidaId);

        var movimientos = new List<(decimal, long)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            movimientos.Add((reader.GetDecimal(0), reader.GetInt64(1)));
        }
        return movimientos;
    }

    // (1) Sin deducciones: proveedor Debe bruto, banco Haber bruto, movimiento = bruto.
    [SkippableFact]
    public async Task Procesar_SinDeducciones_ProveedorDebeBrutoYBancoHaberBruto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 1;
        var cuentas = await ResolveCuentasProcesoAsync(orden);
        Skip.If(cuentas is null, "No hay proveedor + cuenta bancaria de procesamiento + 2 cuentas de retencion posting/ACTIVE distintas en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, cuentas!.Value.CodProveedor, cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new ProcesarOrdenPagoDirectoDto
        {
            Usuario = "tester",
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = cuentas.Value.CuentaBancoAccountId,
                    BancoCuentaId = cuentas.Value.BancoCuentaId,
                    Descripcion = "pago proveedor",
                    Debito = 0m,
                    Credito = 1000m
                }
            }
        };

        var res = await service.MarkAsProcessedAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        var polizaId = await LeerUltimoPolizaIdAsync(orden);
        Assert.NotNull(polizaId);

        var lineas = await LeerLineasPartidaAsync(polizaId!.Value);

        var lineaProveedor = Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaProveedorAccountId));
        Assert.Equal(1000m, lineaProveedor.Debit);
        Assert.Equal(0m, lineaProveedor.Credit);

        var lineaBanco = Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaBancoAccountId));
        Assert.Equal(0m, lineaBanco.Debit);
        Assert.Equal(1000m, lineaBanco.Credit);

        Assert.Equal(lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit));

        var movimientos = await LeerMovimientosBancoAsync(polizaId.Value);
        var movimiento = Assert.Single(movimientos);
        // sp_ban_kardex_registrar_movimiento normaliza el signo por entra_sale del tipo de transaccion
        // (CHQ es 'S' = salida => negativo); lo relevante para esta prueba es la MAGNITUD del neto.
        Assert.Equal(1000m, Math.Abs(movimiento.Monto));
        Assert.Equal(cuentas.Value.BancoCuentaId, movimiento.BancoCuentaId);

        Assert.True(await StatusTransaccAsync(orden));
    }

    // (2) Con 1 retencion (haber): banco Haber = bruto - retencion; movimiento = neto; cuadra.
    [SkippableFact]
    public async Task Procesar_Con1Retencion_BancoHaberEsNeto()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 2;
        var cuentas = await ResolveCuentasProcesoAsync(orden);
        Skip.If(cuentas is null, "No hay proveedor + cuenta bancaria de procesamiento + 2 cuentas de retencion posting/ACTIVE distintas en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, cuentas!.Value.CodProveedor, cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new ProcesarOrdenPagoDirectoDto
        {
            Usuario = "tester",
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = cuentas.Value.CuentaRetencion1AccountId,
                    Descripcion = "retencion ISR",
                    Debito = 0m,
                    Credito = 150m
                },
                new()
                {
                    CuentaId = cuentas.Value.CuentaBancoAccountId,
                    BancoCuentaId = cuentas.Value.BancoCuentaId,
                    Descripcion = "pago proveedor neto",
                    Debito = 0m,
                    Credito = 850m
                }
            }
        };

        var res = await service.MarkAsProcessedAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        var polizaId = await LeerUltimoPolizaIdAsync(orden);
        Assert.NotNull(polizaId);
        var lineas = await LeerLineasPartidaAsync(polizaId!.Value);

        var lineaProveedor = Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaProveedorAccountId));
        Assert.Equal(1000m, lineaProveedor.Debit);

        var lineaRetencion = Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaRetencion1AccountId));
        Assert.Equal(150m, lineaRetencion.Credit);
        Assert.Equal(0m, lineaRetencion.Debit);

        var lineaBanco = Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaBancoAccountId));
        Assert.Equal(850m, lineaBanco.Credit);
        Assert.Equal(0m, lineaBanco.Debit);

        Assert.Equal(lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit));
        Assert.Equal(1000m, lineas.Sum(l => l.Debit));

        var movimientos = await LeerMovimientosBancoAsync(polizaId.Value);
        var movimiento = Assert.Single(movimientos);
        Assert.Equal(850m, Math.Abs(movimiento.Monto));

        Assert.True(await StatusTransaccAsync(orden));
    }

    // (3) Con 2 retenciones (haber): banco Haber = bruto - Sum(retenciones); movimiento = neto; cuadra.
    [SkippableFact]
    public async Task Procesar_Con2Retenciones_BancoHaberEsNetoDeAmbas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 3;
        var cuentas = await ResolveCuentasProcesoAsync(orden);
        Skip.If(cuentas is null, "No hay proveedor + cuenta bancaria de procesamiento + 2 cuentas de retencion posting/ACTIVE distintas en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, cuentas!.Value.CodProveedor, cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new ProcesarOrdenPagoDirectoDto
        {
            Usuario = "tester",
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = cuentas.Value.CuentaRetencion1AccountId,
                    Descripcion = "retencion ISR",
                    Debito = 0m,
                    Credito = 100m
                },
                new()
                {
                    CuentaId = cuentas.Value.CuentaRetencion2AccountId,
                    Descripcion = "retencion ISV",
                    Debito = 0m,
                    Credito = 50m
                },
                new()
                {
                    CuentaId = cuentas.Value.CuentaBancoAccountId,
                    BancoCuentaId = cuentas.Value.BancoCuentaId,
                    Descripcion = "pago proveedor neto",
                    Debito = 0m,
                    Credito = 850m
                }
            }
        };

        var res = await service.MarkAsProcessedAsync(orden, dto, CancellationToken.None);
        Assert.True(res.Success, res.Message);

        var polizaId = await LeerUltimoPolizaIdAsync(orden);
        Assert.NotNull(polizaId);
        var lineas = await LeerLineasPartidaAsync(polizaId!.Value);

        Assert.Equal(1000m, Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaProveedorAccountId)).Debit);
        Assert.Equal(100m, Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaRetencion1AccountId)).Credit);
        Assert.Equal(50m, Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaRetencion2AccountId)).Credit);

        var lineaBanco = Assert.Single(lineas.Where(l => l.AccountId == cuentas.Value.CuentaBancoAccountId));
        Assert.Equal(850m, lineaBanco.Credit);

        Assert.Equal(lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit));

        var movimientos = await LeerMovimientosBancoAsync(polizaId.Value);
        var movimiento = Assert.Single(movimientos);
        Assert.Equal(850m, Math.Abs(movimiento.Monto));

        Assert.True(await StatusTransaccAsync(orden));
    }

    // (4a) Rechazo: una linea con Debito Y Credito a la vez (viola la exclusividad por linea).
    [SkippableFact]
    public async Task Procesar_LineaConDebitoYCreditoALaVez_EsRechazada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 4;
        var cuentas = await ResolveCuentasProcesoAsync(orden);
        Skip.If(cuentas is null, "No hay proveedor + cuenta bancaria de procesamiento + 2 cuentas de retencion posting/ACTIVE distintas en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, cuentas!.Value.CodProveedor, cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new ProcesarOrdenPagoDirectoDto
        {
            Usuario = "tester",
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = cuentas.Value.CuentaRetencion1AccountId,
                    Descripcion = "linea invalida",
                    Debito = 100m,
                    Credito = 50m
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.MarkAsProcessedAsync(orden, dto, CancellationToken.None));

        Assert.Contains("Debito", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await StatusTransaccAsync(orden));
        Assert.Null(await LeerUltimoPolizaIdAsync(orden));
    }

    // (4b) Rechazo: neto de banco <= 0. Construccion deliberada: la linea de "banco" queda con
    // Debito=200 (Credito=0, pasa el XOR individual) y la retencion absorbe bruto+200=1200 al Haber,
    // de modo que el cuadre (Sum(Credito)-Sum(Debito) del DTO == bruto) se mantiene, pero
    // Sum(Credito de lineas de banco) = 0: ese es el neto real que se enviaria al banco.
    [SkippableFact]
    public async Task Procesar_NetoBancoMenorOIgualACero_EsRechazado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        const int orden = OrdenBase + 5;
        var cuentas = await ResolveCuentasProcesoAsync(orden);
        Skip.If(cuentas is null, "No hay proveedor + cuenta bancaria de procesamiento + 2 cuentas de retencion posting/ACTIVE distintas en el tenant de prueba.");

        await SeedCompromisoAsync(orden, 1000m, cuentas!.Value.CodProveedor, cuentas.Value.CuentaGastoCode);

        await using var context = CreateContext();
        var service = CreateService(context);

        var dto = new ProcesarOrdenPagoDirectoDto
        {
            Usuario = "tester",
            MetodoPago = OrdenPagoDirectoMetodoPago.Cheque,
            Lineas = new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = cuentas.Value.CuentaBancoAccountId,
                    BancoCuentaId = cuentas.Value.BancoCuentaId,
                    Descripcion = "banco",
                    Debito = 200m,
                    Credito = 0m
                },
                new()
                {
                    CuentaId = cuentas.Value.CuentaRetencion1AccountId,
                    Descripcion = "retencion excesiva",
                    Debito = 0m,
                    Credito = 1200m
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.MarkAsProcessedAsync(orden, dto, CancellationToken.None));

        Assert.Contains("neto", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await StatusTransaccAsync(orden));
        Assert.Null(await LeerUltimoPolizaIdAsync(orden));
    }
}
