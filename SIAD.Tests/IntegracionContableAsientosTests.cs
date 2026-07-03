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
            VALUES (@CompanyId, 'FACTURACION', @JournalId, @TypeId, 'test')",
            new { CompanyId, JournalId = journalId, TypeId = typeId }, Transaction));

        var fila = await Connection.QueryFirstAsync<(long journal_id, long type_id)>(new CommandDefinition(
            "SELECT journal_id, type_id FROM public.con_integracion_asiento WHERE company_id = @CompanyId AND module = 'FACTURACION'",
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
    public async Task Rechaza_tipo_de_partida_de_otra_empresa()
    {
        var tipoAjeno = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT type_id FROM public.con_tipo_transaccion WHERE company_id <> @CompanyId ORDER BY type_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(tipoAjeno is null, "No hay tipos de partida de otra empresa en la BD de pruebas.");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, type_id, created_by)
                VALUES (@CompanyId, 'BANCOS', @TypeId, 'test')",
                new { CompanyId, TypeId = tipoAjeno }, Transaction)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);
    }
}
