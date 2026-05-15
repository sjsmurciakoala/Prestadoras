using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class AnulacionTests : IntegrationTestBase
{
    public AnulacionTests(PostgresFixture fixture) : base(fixture) { }

    /// <summary>
    /// Bug descubierto 2026-05-15: sp_adm_emitir_nota_credito intenta hacer
    /// UPDATE public.factura SET updated_at = now() al anular una factura total,
    /// pero la tabla factura no tiene columna updated_at → 42703.
    /// Este test FALLA hasta que se aplique el fix (agregar updated_at a factura
    /// o quitar la asignación del SP).
    /// </summary>
    [SkippableFact]
    public async Task BUG_factura_debe_tener_updated_at_para_que_NC_total_anule()
    {
        const string sql = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'factura'
                  AND column_name = 'updated_at'
            )";

        var existe = await Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.True(existe,
            "sp_adm_emitir_nota_credito (línea ~441 de 20260514_nc_nd_v3_modelo.sql) " +
            "escribe factura.updated_at, pero esa columna NO existe. La anulación TOTAL " +
            "via NC falla con 42703. Fix: ALTER TABLE factura ADD COLUMN updated_at timestamptz, " +
            "o quitar updated_at del UPDATE del SP.");
    }

    [SkippableFact]
    public async Task SP_NC_referencia_motivo_anulacion_obligatorio()
    {
        const string sql = @"
            SELECT pg_get_function_arguments(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = 'sp_adm_emitir_nota_credito'
            LIMIT 1";

        var args = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, transaction: Transaction));

        Skip.If(string.IsNullOrWhiteSpace(args), "sp_adm_emitir_nota_credito no existe.");

        Assert.Contains("p_motivo_anulacion_id", args, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p_factura_origen_id", args, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("p_cai_id", args, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task adm_nota_credito_referencia_factura_origen()
    {
        const string sql = @"
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public'
                  AND table_name='adm_nota_credito'
                  AND column_name='factura_origen_id'
            ) AS tiene_factura_origen,
            EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public'
                  AND table_name='adm_nota_credito'
                  AND column_name='motivo_anulacion_id'
            ) AS tiene_motivo";

        var row = await Connection.QueryFirstAsync<(bool tiene_factura_origen, bool tiene_motivo)>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.True(row.tiene_factura_origen, "Gap SAR #4: NC sin referencia a factura origen.");
        Assert.True(row.tiene_motivo, "Gap SAR #5: NC sin motivo estructurado.");
    }
}
