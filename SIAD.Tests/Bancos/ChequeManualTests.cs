using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Bancos;

/// <summary>
/// Cheque MANUAL (suelto, sin compromiso): validaciones de
/// <see cref="BanTransaccionesService.RegistrarChequeManualAsync"/> y del DTO de entrada,
/// mas la comprobacion de que el libro admite origen MANUAL con estado 'E' (el cheque
/// manual) ademas del MANUAL/'A' que ya usaba la anulacion de un numero danado.
///
/// ALCANCE: el camino feliz completo (partida + sp_ban_kardex_registrar_movimiento +
/// cheque) NO se cubre aqui: RegistrarMovimientoAsync abre su propia transaccion sobre
/// la conexion, y el harness ya tiene una abierta (BEGIN ... ROLLBACK). Se cubren las
/// validaciones, que corren ANTES de cualquier escritura, y la emision del cheque a
/// nivel de ChequesService (que si acepta la transaccion del llamador).
/// </summary>
[Collection("Postgres")]
public class ChequeManualTests : IntegrationTestBase
{
    private const string SkipTablaFaltante =
        "tablas ban_cheque/ban_cheque_bitacora no existen: aplicar Database/2026-07-21_cheques_numeracion_bitacora.sql";

    public ChequeManualTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }

    private sealed class TestAccountFormatService : IAccountFormatService
    {
        public Task<AccountFormat> GetFormatAsync(CancellationToken ct = default)
            => Task.FromResult(AccountFormat.Default);
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

    private BanTransaccionesService CreateService(SiadDbContext context)
        => new(
            context,
            // El mapper solo se usa en las consultas de listado; las validaciones del
            // cheque manual no pasan por el.
            Substitute.For<IMapper>(),
            new TestCurrentCompanyService(CompanyId),
            new TestAccountFormatService(),
            new ChequesService(context, new TestCurrentCompanyService(CompanyId), new TestAccountFormatService()));

    // --- Siembra (vive solo dentro de la transaccion del harness) ---

    private async Task<long> SeedCuentaAsync(string sufijo, string tipo)
    {
        var codigo = $"TSTCHM{sufijo}";
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, tipo, currency_code, numero_cuenta, activo, estado,
     proximo_cheque, cheque_maximo)
VALUES (@c, @code, 'Cuenta Cheque Manual Test', @tipo, 'LPS', @numero, TRUE, 'ACTIVE', 100, 0)
RETURNING banco_cuenta_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("code", codigo);
        cmd.Parameters.AddWithValue("tipo", tipo);
        cmd.Parameters.AddWithValue("numero", codigo);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task SeedTipoTransaccionAsync(string tipo, string entraSale, string? emiteCheque)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_tipos_transacciones
    (company_id, tipo_transaccion, cod_tipopartida, correlativo, nombre, entra_sale,
     del_sistema, emite_cheque, cuenta_alterna, estado, created_at, created_by)
VALUES (@c, @tipo, 'CHQ', 'TST001', 'Tipo de prueba', @es, 'N', @ec, FALSE, 'ACTIVE', now(), 'tester');";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("tipo", tipo);
        cmd.Parameters.AddWithValue("es", entraSale);
        cmd.Parameters.AddWithValue("ec", (object?)emiteCheque ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static ChequeManualCreateDto NuevoDto(long bancoCuentaId, string tipo) => new()
    {
        BancoCuentaId = bancoCuentaId,
        IdTipoTransaccion = tipo,
        FechaEmision = DateOnly.FromDateTime(DateTime.Today),
        Beneficiario = "Proveedor de prueba",
        Concepto = "Pago de servicios",
        Referencia = "CHM-TEST",
        Monto = 500m,
        TasaCambio = 1m,
        Lineas = new List<BanTransaccionContraLineaDto>
        {
            new() { CuentaId = 1, Monto = 500m, Descripcion = "Gasto", SourceDocument = "CHM-TEST" }
        }
    };

    // --- Validaciones del servicio (antes de cualquier escritura) ---

    [SkippableFact]
    public async Task Cuenta_que_no_es_de_cheques_se_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuentaId = await SeedCuentaAsync("V1", "AHORRO");
        await SeedTipoTransaccionAsync("CM1", "S", "S");

        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegistrarChequeManualAsync(NuevoDto(cuentaId, "CM1"), "tester", CancellationToken.None));

        Assert.Contains("no es una cuenta de cheques", ex.Message, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Tipo_de_transaccion_inexistente_se_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuentaId = await SeedCuentaAsync("V2", "CHEQUES");

        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegistrarChequeManualAsync(NuevoDto(cuentaId, "ZZZ"), "tester", CancellationToken.None));

        Assert.Contains("no existe", ex.Message, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Tipo_que_no_emite_cheque_se_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuentaId = await SeedCuentaAsync("V3", "CHEQUES");
        await SeedTipoTransaccionAsync("CM3", "S", "N");

        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegistrarChequeManualAsync(NuevoDto(cuentaId, "CM3"), "tester", CancellationToken.None));

        Assert.Contains("no emite cheques", ex.Message, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Tipo_de_entrada_se_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuentaId = await SeedCuentaAsync("V4", "CHEQUES");
        await SeedTipoTransaccionAsync("CM4", "E", "S");

        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegistrarChequeManualAsync(NuevoDto(cuentaId, "CM4"), "tester", CancellationToken.None));

        Assert.Contains("de salida", ex.Message, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Sin_lineas_de_detalle_se_rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var cuentaId = await SeedCuentaAsync("V5", "CHEQUES");
        await SeedTipoTransaccionAsync("CM5", "S", "S");

        var dto = NuevoDto(cuentaId, "CM5");
        dto.Lineas.Clear();

        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegistrarChequeManualAsync(dto, "tester", CancellationToken.None));
    }

    // --- El libro admite MANUAL emitido ('E'), no solo MANUAL anulado ---

    [SkippableFact]
    public async Task Cheque_con_origen_manual_queda_emitido()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.IfNot(await EsquemaChequesDisponibleAsync(), SkipTablaFaltante);

        var cuentaId = await SeedCuentaAsync("M6", "CHEQUES");

        await using var context = CreateContext();
        var cheques = new ChequesService(
            context, new TestCurrentCompanyService(CompanyId), new TestAccountFormatService());

        var (chequeId, numero) = await cheques.EmitirChequeAsync(
            Connection, Transaction, cuentaId,
            monto: 500m, beneficiario: "Proveedor de prueba", concepto: "Pago de servicios",
            origen: ChequeOrigen.Manual, origenDocumento: "CHM-TEST",
            banKardexId: null, partidaId: null,
            usuario: "tester", fechaEmision: DateTime.Now, ct: CancellationToken.None);

        Assert.Equal(100m, numero);
        Assert.True(chequeId > 0);

        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT ch.estado, ch.origen, ch.beneficiario, ev.accion
  FROM public.ban_cheque ch
  JOIN public.ban_cheque_bitacora ev ON ev.cheque_id = ch.cheque_id
 WHERE ch.cheque_id = @id;";
        cmd.Parameters.AddWithValue("id", chequeId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("E", reader.GetString(0));
        Assert.Equal(ChequeOrigen.Manual, reader.GetString(1));
        Assert.Equal("Proveedor de prueba", reader.GetString(2));
        Assert.Equal(ChequeAccion.Emitido, reader.GetString(3));
    }

    private async Task<bool> EsquemaChequesDisponibleAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT EXISTS (SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'ban_cheque')
   AND EXISTS (SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'ban_cheque_bitacora')
   AND EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'ban_cuenta'
                  AND column_name = 'cheque_maximo');";
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }
}

/// <summary>
/// Validaciones del DTO del cheque manual (sin BD): el detalle contable debe cuadrar
/// con el monto y la fecha no puede ser futura.
/// </summary>
public class ChequeManualCreateDtoTests
{
    private static ChequeManualCreateDto DtoValido() => new()
    {
        BancoCuentaId = 1,
        IdTipoTransaccion = "CHQ",
        FechaEmision = DateOnly.FromDateTime(DateTime.Today),
        Beneficiario = "Proveedor",
        Concepto = "Pago",
        Referencia = "CHM-1",
        Monto = 100m,
        TasaCambio = 1m,
        Lineas = new List<BanTransaccionContraLineaDto>
        {
            new() { CuentaId = 5, Monto = 100m, Descripcion = "Gasto", SourceDocument = "CHM-1" }
        }
    };

    private static List<ValidationResult> Validar(ChequeManualCreateDto dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados;
    }

    [Fact]
    public void Dto_valido_no_produce_errores()
        => Assert.Empty(Validar(DtoValido()));

    [Fact]
    public void Detalle_descuadrado_produce_error()
    {
        var dto = DtoValido();
        dto.Lineas[0].Monto = 90m;

        Assert.Contains(Validar(dto), r => r.ErrorMessage!.Contains("no coincide", StringComparison.Ordinal));
    }

    [Fact]
    public void Sin_lineas_produce_error()
    {
        var dto = DtoValido();
        dto.Lineas.Clear();

        Assert.Contains(Validar(dto), r => r.ErrorMessage!.Contains("al menos una línea", StringComparison.Ordinal));
    }

    [Fact]
    public void Fecha_futura_produce_error()
    {
        var dto = DtoValido();
        dto.FechaEmision = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        Assert.Contains(Validar(dto), r => r.ErrorMessage!.Contains("fecha futura", StringComparison.Ordinal));
    }
}
