using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 2 del plan de integración contable-comercial (2026-07-02):
// con_integracion_asiento (diario y tipo de partida por módulo, pestaña
// Asientos de la pantalla de configuración). Requiere el script
// 20260703_ci_fase2_asientos_config.sql aplicado.
[Collection("Postgres")]
public sealed class IntegracionContableAsientosTests : IntegrationTestBase
{
    public IntegracionContableAsientosTests(PostgresFixture fixture) : base(fixture) { }

    private Task<long?> DiarioIdAsync() =>
        Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1",
            new { CompanyId }, Transaction));

    private Task<long?> TipoPartidaIdAsync() =>
        Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1",
            new { CompanyId }, Transaction));

    [SkippableFact]
    public async Task Inserta_asiento_por_modulo_con_diario_y_tipo()
    {
        var journalId = await DiarioIdAsync();
        var typeId = await TipoPartidaIdAsync();
        Skip.If(journalId is null || typeId is null, "La empresa de pruebas no tiene diarios/tipos de partida.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            DELETE FROM public.con_integracion_asiento WHERE company_id = @CompanyId;
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            VALUES (@CompanyId, 'VENTAS', @JournalId, @TypeId, 'test')",
            new { CompanyId, JournalId = journalId, TypeId = typeId }, Transaction));

        var fila = await Connection.QueryFirstAsync<(long journal_id, long type_id)>(new CommandDefinition(
            "SELECT journal_id, type_id FROM public.con_integracion_asiento WHERE company_id = @CompanyId AND module = 'VENTAS'",
            new { CompanyId }, Transaction));

        Assert.Equal(journalId, fila.journal_id);
        Assert.Equal(typeId, fila.type_id);
    }

    [SkippableFact]
    public async Task Rechaza_modulo_duplicado_por_empresa()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            DELETE FROM public.con_integracion_asiento WHERE company_id = @CompanyId;
            INSERT INTO public.con_integracion_asiento (company_id, module, created_by)
            VALUES (@CompanyId, 'CAJA', 'test')",
            new { CompanyId }, Transaction));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, created_by)
                VALUES (@CompanyId, 'CAJA', 'test')",
                new { CompanyId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [SkippableFact]
    public async Task Rechaza_modulo_desconocido()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, created_by)
                VALUES (@CompanyId, 'INVENTARIO', 'test')",
                new { CompanyId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [SkippableFact]
    public async Task Rechaza_vocabulario_viejo_FACTURACION()
    {
        // v2 del script: el vocabulario quedó alineado al motor (VENTAS).
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, created_by)
                VALUES (@CompanyId, 'FACTURACION', 'test')",
                new { CompanyId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [SkippableFact]
    public async Task Rechaza_tipo_de_partida_de_otra_empresa()
    {
        var tipoAjeno = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT type_id FROM public.con_tipo_transaccion WHERE company_id <> @CompanyId ORDER BY type_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(tipoAjeno is null, "No hay tipos de partida de otra empresa en la BD de pruebas.");

        // La BD de pruebas puede traer un asiento BANCOS configurado desde la
        // pantalla: despejarlo dentro de la transacción para que el INSERT
        // ejerza la FK compuesta y no la unique (company, module).
        await Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.con_integracion_asiento WHERE company_id = @CompanyId AND module = 'BANCOS'",
            new { CompanyId }, Transaction));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, type_id, created_by)
                VALUES (@CompanyId, 'BANCOS', @TypeId, 'test')",
                new { CompanyId, TypeId = tipoAjeno }, Transaction)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);
    }

    [SkippableFact]
    public async Task Rechaza_diario_de_otra_empresa()
    {
        // Crea una segunda empresa con su diario dentro de la transacción para
        // ejercer la FK compuesta aunque la BD de pruebas sea monoempresa.
        var diarioAjeno = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            WITH otra AS (
                INSERT INTO public.cfg_company
                    (code, commercial_name, legal_name, tax_id, country_code, currency_code,
                     timezone, status, created_at, created_by)
                VALUES ('TESTF2', 'Empresa test F2', 'Empresa test F2 SA', '0000-0000-000000',
                        'HND', 'HNL', 'America/Tegucigalpa', 'ACTIVE', now(), 'test')
                RETURNING company_id
            )
            INSERT INTO public.con_diario
                (company_id, code, name, last_sequence, is_active, allows_manual, created_at, created_by)
            SELECT company_id, 'TESTDIA', 'Diario test ajeno', 0, true, true, now(), 'test'
            FROM otra
            RETURNING journal_id",
            new { }, Transaction));

        // Ver nota en Rechaza_tipo_de_partida_de_otra_empresa: despejar el
        // asiento BANCOS que la pantalla pudo dejar en la BD de pruebas.
        await Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.con_integracion_asiento WHERE company_id = @CompanyId AND module = 'BANCOS'",
            new { CompanyId }, Transaction));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, created_by)
                VALUES (@CompanyId, 'BANCOS', @JournalId, 'test')",
                new { CompanyId, JournalId = diarioAjeno }, Transaction)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);
    }
}
