using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class RecargoMoraTests : IntegrationTestBase
{
    public RecargoMoraTests(PostgresFixture fixture) : base(fixture) { }

    [SkippableFact]
    public async Task Tabla_cfg_recargo_mora_existe_con_constraints_basicas()
    {
        const string sql = @"
            SELECT
                to_regclass('public.cfg_recargo_mora')::text                     AS tabla,
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='cfg_recargo_mora' AND column_name='tasa_mensual'
                )                                                                AS tiene_tasa,
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='cfg_recargo_mora' AND column_name='dias_gracia'
                )                                                                AS tiene_dias_gracia,
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public' AND table_name='cfg_recargo_mora' AND column_name='company_id'
                )                                                                AS es_multiempresa";

        var row = await Connection.QueryFirstAsync<(string? tabla, bool tiene_tasa, bool tiene_dias_gracia, bool es_multiempresa)>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.False(string.IsNullOrWhiteSpace(row.tabla), "cfg_recargo_mora no existe.");
        Assert.True(row.tiene_tasa);
        Assert.True(row.tiene_dias_gracia);
        Assert.True(row.es_multiempresa);
    }

    [SkippableFact]
    public async Task Tasa_mora_es_decimal_entre_0_y_1()
    {
        const string sql = @"
            SELECT tasa_mensual
            FROM public.cfg_recargo_mora
            WHERE company_id = @CompanyId AND activo = true
            LIMIT 1";

        var tasa = await Connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(sql, new { CompanyId = CompanyId }, Transaction));

        Skip.If(tasa is null, "No hay configuración de mora activa para esta company.");

        Assert.InRange(tasa!.Value, 0m, 1m);
    }

    [SkippableFact]
    public async Task SP_calcular_factura_lectura_invoca_cfg_recargo_mora()
    {
        const string sql = @"
            SELECT pg_get_functiondef(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = 'sp_adm_calcular_factura_lectura'
            LIMIT 1";

        var def = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, transaction: Transaction));

        Skip.If(string.IsNullOrWhiteSpace(def), "sp_adm_calcular_factura_lectura no existe.");

        Assert.Contains("cfg_recargo_mora", def, StringComparison.OrdinalIgnoreCase);
    }
}
