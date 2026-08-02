using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Bancos;

/// <summary>
/// Integracion de ChequesService contra ban_cuenta/ban_cheque: emision (FOR UPDATE +
/// incremento de proximo_cheque), normalizacion de proximo &lt;= 0, agotamiento contra
/// cheque_maximo, anulacion por kardex (reverso), anulacion manual del siguiente numero
/// y unicidad uq_ban_cheque_numero. Cada prueba siembra su propia ban_cuenta tipo
/// CHEQUES dentro de la transaccion del harness (BEGIN...ROLLBACK).
///
/// GUARD: las tablas ban_cheque / ban_cheque_bitacora y la columna
/// ban_cuenta.cheque_maximo las crea Database/2026-07-21_cheques_numeracion_bitacora.sql
/// (Task 1, la aplica EL USUARIO). Si la BD de test aun no lo tiene aplicado,
/// estas pruebas quedan Skipped en vez de fallar.
/// </summary>
[Collection("Postgres")]
public class ChequesServiceTests : IntegrationTestBase
{
    private const string SkipTablaFaltante =
        "tablas ban_cheque/ban_cheque_bitacora no existen: aplicar Database/2026-07-21_cheques_numeracion_bitacora.sql";

    public ChequesServiceTests(PostgresFixture fixture) : base(fixture)
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
        // Engancha la transaccion del harness: AnularSiguienteNumeroAsync la detecta como
        // ambiente (Database.CurrentTransaction) y NO comitea, asi el ROLLBACK limpia todo.
        context.Database.UseTransaction(Transaction);
        return context;
    }

    private ChequesService CreateService(SiadDbContext context)
        => new(context, new TestCurrentCompanyService(CompanyId), new TestAccountFormatService());

    /// <summary>SQL de la Task 1 aplicado en la BD de test (tabla + columna nuevas).</summary>
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

    private async Task SkipSiFaltaEsquemaAsync()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.IfNot(await EsquemaChequesDisponibleAsync(), SkipTablaFaltante);
    }

    // --- Siembra ---

    // Mismo patron que SeedCuentaBancariaAhorroAsync (AbonosCompromisoTests): la cuenta vive
    // solo dentro de la transaccion del test. ban_banco_id queda NULL (nullable; ninguno de
    // estos casos consulta el join con ban_banco).
    private async Task<long> SeedCuentaChequesAsync(string sufijo, decimal proximoCheque, decimal chequeMaximo)
    {
        var codigo = $"TSTCHQ{sufijo}";
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
INSERT INTO public.ban_cuenta
    (company_id, code, nombre, tipo, currency_code, numero_cuenta, activo, estado,
     proximo_cheque, cheque_maximo)
VALUES (@c, @code, 'Cuenta Cheques Test', 'CHEQUES', 'LPS', @numero, TRUE, 'ACTIVE',
        @prox, @max)
RETURNING banco_cuenta_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("code", codigo);
        cmd.Parameters.AddWithValue("numero", codigo);
        cmd.Parameters.AddWithValue("prox", proximoCheque);
        cmd.Parameters.AddWithValue("max", chequeMaximo);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    // --- Verificaciones ---

    private async Task<decimal> LeerProximoChequeAsync(long bancoCuentaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT proximo_cheque FROM public.ban_cuenta
 WHERE company_id = @c AND banco_cuenta_id = @b;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("b", bancoCuentaId);
        return (decimal)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<int> ContarChequesAsync(long bancoCuentaId)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT count(*) FROM public.ban_cheque
 WHERE company_id = @c AND banco_cuenta_id = @b;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("b", bancoCuentaId);
        return (int)(long)(await cmd.ExecuteScalarAsync())!;
    }

    private sealed record ChequeRow(
        long ChequeId,
        string Estado,
        string Origen,
        decimal Monto,
        long? BanKardexId,
        long? BanKardexIdReverso,
        string? MotivoAnulacion,
        string? UsuarioAnulacion,
        DateTime? FechaAnulacion);

    private async Task<ChequeRow?> LeerChequeAsync(long bancoCuentaId, decimal numeroCheque)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT cheque_id, estado, origen, monto, ban_kardex_id, ban_kardex_id_reverso,
       motivo_anulacion, usuario_anulacion, fecha_anulacion
  FROM public.ban_cheque
 WHERE company_id = @c AND banco_cuenta_id = @b AND numero_cheque = @n;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("b", bancoCuentaId);
        cmd.Parameters.AddWithValue("n", numeroCheque);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new ChequeRow(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.IsDBNull(4) ? null : reader.GetInt64(4),
            reader.IsDBNull(5) ? null : reader.GetInt64(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetDateTime(8));
    }

    // --- Bitacora de eventos (ban_cheque_bitacora, append-only) ---

    private sealed record EventoRow(
        long ChequeId,
        string Accion,
        decimal Monto,
        string Usuario,
        string? Motivo,
        string Origen,
        long? BanKardexId);

    private async Task<List<EventoRow>> LeerEventosAsync(long bancoCuentaId, decimal numeroCheque)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT cheque_id, accion, monto, usuario, motivo, origen, ban_kardex_id
  FROM public.ban_cheque_bitacora
 WHERE company_id = @c AND banco_cuenta_id = @b AND numero_cheque = @n
 ORDER BY bitacora_id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("b", bancoCuentaId);
        cmd.Parameters.AddWithValue("n", numeroCheque);

        var eventos = new List<EventoRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            eventos.Add(new EventoRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6)));
        }

        return eventos;
    }

    private async Task<decimal> EmitirAsync(ChequesService service, long cuentaId, long? banKardexId = null)
        => (await service.EmitirChequeAsync(
            Connection, Transaction, cuentaId,
            monto: 150.25m, beneficiario: "Proveedor Test", concepto: "cheque de prueba",
            origen: "PROCESAR", origenDocumento: "OPD-TEST",
            banKardexId: banKardexId, partidaId: null,
            usuario: "tester", fechaEmision: DateTime.Now, ct: CancellationToken.None)).NumeroCheque;

    // (1) Emitir asigna proximo_cheque e incrementa; la segunda emision toma el siguiente.
    [SkippableFact]
    public async Task Emitir_asigna_proximo_e_incrementa()
    {
        await SkipSiFaltaEsquemaAsync();

        var cuentaId = await SeedCuentaChequesAsync("E1", proximoCheque: 100m, chequeMaximo: 0m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var primero = await EmitirAsync(service, cuentaId);
        Assert.Equal(100m, primero);
        Assert.Equal(101m, await LeerProximoChequeAsync(cuentaId));

        var fila = await LeerChequeAsync(cuentaId, 100m);
        Assert.NotNull(fila);
        Assert.Equal("E", fila!.Estado);
        Assert.Equal("PROCESAR", fila.Origen);
        Assert.Equal(150.25m, fila.Monto);
        Assert.Null(fila.FechaAnulacion);

        // Bitacora de eventos: exactamente 1 EMITIDO apuntando al cheque del libro.
        var eventos = await LeerEventosAsync(cuentaId, 100m);
        var evento = Assert.Single(eventos);
        Assert.Equal("EMITIDO", evento.Accion);
        Assert.Equal(fila.ChequeId, evento.ChequeId);
        Assert.Equal(150.25m, evento.Monto);
        Assert.Equal("tester", evento.Usuario);
        Assert.Null(evento.Motivo);

        var segundo = await EmitirAsync(service, cuentaId);
        Assert.Equal(101m, segundo);
        Assert.Equal(102m, await LeerProximoChequeAsync(cuentaId));
        Assert.Equal(2, await ContarChequesAsync(cuentaId));
    }

    // (2) proximo_cheque = 0 se normaliza: emite el 1.
    [SkippableFact]
    public async Task Emitir_normaliza_proximo_cero_a_uno()
    {
        await SkipSiFaltaEsquemaAsync();

        var cuentaId = await SeedCuentaChequesAsync("E2", proximoCheque: 0m, chequeMaximo: 0m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var numero = await EmitirAsync(service, cuentaId);

        Assert.Equal(1m, numero);
        Assert.Equal(2m, await LeerProximoChequeAsync(cuentaId));
        Assert.NotNull(await LeerChequeAsync(cuentaId, 1m));
    }

    // (3) Numeracion agotada (proximo > maximo): lanza y NO inserta fila.
    [SkippableFact]
    public async Task Emitir_con_maximo_agotado_lanza_y_no_inserta()
    {
        await SkipSiFaltaEsquemaAsync();

        var cuentaId = await SeedCuentaChequesAsync("E3", proximoCheque: 201m, chequeMaximo: 200m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => EmitirAsync(service, cuentaId));

        Assert.Contains("agotó su numeración", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, await ContarChequesAsync(cuentaId));
        Assert.Equal(201m, await LeerProximoChequeAsync(cuentaId));
    }

    // (4) proximo == maximo: el ultimo del talonario todavia se emite; el siguiente lanza.
    [SkippableFact]
    public async Task Emitir_en_el_maximo_todavia_funciona()
    {
        await SkipSiFaltaEsquemaAsync();

        var cuentaId = await SeedCuentaChequesAsync("E4", proximoCheque: 200m, chequeMaximo: 200m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var numero = await EmitirAsync(service, cuentaId);
        Assert.Equal(200m, numero);
        Assert.Equal(201m, await LeerProximoChequeAsync(cuentaId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => EmitirAsync(service, cuentaId));
        Assert.Equal(1, await ContarChequesAsync(cuentaId));
    }

    // (5) Anulacion por reverso del movimiento: marca 'A' y puebla motivo/usuario/fecha/reverso.
    [SkippableFact]
    public async Task Anular_por_kardex_marca_A()
    {
        await SkipSiFaltaEsquemaAsync();

        const long kardexOriginal = 987654301L;
        const long kardexReverso = 987654302L;
        var cuentaId = await SeedCuentaChequesAsync("A5", proximoCheque: 300m, chequeMaximo: 0m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var numero = await EmitirAsync(service, cuentaId, banKardexId: kardexOriginal);
        Assert.Equal(300m, numero);

        var anulado = await service.AnularPorKardexAsync(
            Connection, Transaction, kardexOriginal, kardexReverso,
            motivo: "Reverso de prueba", usuario: "anulador", ct: CancellationToken.None);
        Assert.True(anulado);

        var fila = await LeerChequeAsync(cuentaId, 300m);
        Assert.NotNull(fila);
        Assert.Equal("A", fila!.Estado);
        Assert.Equal(kardexOriginal, fila.BanKardexId);
        Assert.Equal(kardexReverso, fila.BanKardexIdReverso);
        Assert.Equal("Reverso de prueba", fila.MotivoAnulacion);
        Assert.Equal("anulador", fila.UsuarioAnulacion);
        Assert.NotNull(fila.FechaAnulacion);

        // Bitacora de eventos: EMITIDO (emision) + ANULADO (reverso) sobre el mismo cheque.
        var eventos = await LeerEventosAsync(cuentaId, 300m);
        Assert.Equal(2, eventos.Count);
        Assert.Equal("EMITIDO", eventos[0].Accion);
        Assert.Equal(kardexOriginal, eventos[0].BanKardexId);
        var anuladoEvento = eventos[1];
        Assert.Equal("ANULADO", anuladoEvento.Accion);
        Assert.Equal(fila.ChequeId, anuladoEvento.ChequeId);
        Assert.Equal("Reverso de prueba", anuladoEvento.Motivo);
        Assert.Equal(kardexReverso, anuladoEvento.BanKardexId);
        Assert.Equal("anulador", anuladoEvento.Usuario);
    }

    // (6) Movimiento sin cheque (DEP/TRF/...): la anulacion es no-op y retorna false.
    [SkippableFact]
    public async Task Anular_por_kardex_sin_cheque_es_noop()
    {
        await SkipSiFaltaEsquemaAsync();

        await using var context = CreateContext();
        var service = CreateService(context);

        // Ningun cheque del test apunta a este kardex (los ban_cheque solo existen dentro
        // de esta transaccion, asi que basta con no haber emitido contra el).
        var anulado = await service.AnularPorKardexAsync(
            Connection, Transaction, banKardexIdOriginal: 987654399L, banKardexIdReverso: 987654400L,
            motivo: "no deberia anular nada", usuario: "tester", ct: CancellationToken.None);

        Assert.False(anulado);
    }

    // (7) Anulacion manual del siguiente numero (cheque danado): fila MANUAL/'A'/monto 0
    // y el correlativo avanza igual.
    [SkippableFact]
    public async Task Anular_siguiente_numero_consume_y_queda_A()
    {
        await SkipSiFaltaEsquemaAsync();

        var cuentaId = await SeedCuentaChequesAsync("A7", proximoCheque: 50m, chequeMaximo: 0m);

        await using var context = CreateContext();
        var service = CreateService(context);

        var numero = await service.AnularSiguienteNumeroAsync(
            cuentaId, motivo: "Cheque danado en impresora", usuario: "tester", ct: CancellationToken.None);

        Assert.Equal(50m, numero);
        Assert.Equal(51m, await LeerProximoChequeAsync(cuentaId));

        var fila = await LeerChequeAsync(cuentaId, 50m);
        Assert.NotNull(fila);
        Assert.Equal("A", fila!.Estado);
        Assert.Equal("MANUAL", fila.Origen);
        Assert.Equal(0m, fila.Monto);
        Assert.Null(fila.BanKardexId);
        Assert.Equal("Cheque danado en impresora", fila.MotivoAnulacion);
        Assert.Equal("tester", fila.UsuarioAnulacion);
        Assert.NotNull(fila.FechaAnulacion);

        // Bitacora de eventos: exactamente 1 ANULADO y 0 EMITIDO (el cheque nunca se emitio).
        var eventos = await LeerEventosAsync(cuentaId, 50m);
        var evento = Assert.Single(eventos);
        Assert.Equal("ANULADO", evento.Accion);
        Assert.DoesNotContain(eventos, e => e.Accion == "EMITIDO");
        Assert.Equal(fila.ChequeId, evento.ChequeId);
        Assert.Equal("MANUAL", evento.Origen);
        Assert.Equal("Cheque danado en impresora", evento.Motivo);
        Assert.Null(evento.BanKardexId);
    }

    // (8) uq_ban_cheque_numero: un INSERT directo duplicado revienta con 23505
    // (defensa en BD ante carreras; el FOR UPDATE del servicio es la primera linea).
    [SkippableFact]
    public async Task Unicidad_de_numero()
    {
        await SkipSiFaltaEsquemaAsync();

        var cuentaId = await SeedCuentaChequesAsync("U8", proximoCheque: 700m, chequeMaximo: 0m);

        async Task InsertarDirectoAsync()
        {
            await using var cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandText = @"
INSERT INTO public.ban_cheque
    (company_id, banco_cuenta_id, numero_cheque, fecha_emision, monto, origen, usuario_emision)
VALUES (@c, @b, 700, now(), 10, 'MANUAL', 'tester');";
            cmd.Parameters.AddWithValue("c", CompanyId);
            cmd.Parameters.AddWithValue("b", cuentaId);
            await cmd.ExecuteNonQueryAsync();
        }

        await InsertarDirectoAsync();

        // OJO: tras el 23505 la transaccion del harness queda abortada (25P01); este
        // assert debe ser lo ultimo que toque la BD. El ROLLBACK del harness la limpia.
        var ex = await Assert.ThrowsAsync<PostgresException>(InsertarDirectoAsync);

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Equal("uq_ban_cheque_numero", ex.ConstraintName);
    }
}
