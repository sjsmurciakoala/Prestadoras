using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class LecturaV3Tests : IntegrationTestBase
{
    public LecturaV3Tests(PostgresFixture fixture) : base(fixture) { }

    [SkippableFact]
    public async Task SP_lectura_v3_existe_y_tiene_parametro_uuid_idempotencia()
    {
        const string sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = 'public'
                  AND p.proname = 'sp_lectura_v3'
                  AND pg_get_function_arguments(p.oid) ILIKE '%p_lectura_uuid%'
            )";

        var existe = await Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.True(existe, "sp_lectura_v3 no expone p_lectura_uuid — la idempotencia por UUID rompe.");
    }

    [SkippableFact]
    public async Task Idempotencia_UUID_se_persiste_en_adm_cai_correlativo_emitido()
    {
        const string sql = @"
            SELECT
                to_regclass('public.adm_cai_correlativo_emitido')::text AS tabla,
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public'
                      AND table_name='adm_cai_correlativo_emitido'
                      AND column_name='lectura_uuid'
                ) AS tiene_uuid,
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema='public'
                      AND table_name='adm_cai_correlativo_emitido'
                      AND column_name='factura_id'
                ) AS tiene_factura_id";

        var row = await Connection.QueryFirstAsync<(string? tabla, bool tiene_uuid, bool tiene_factura_id)>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.False(string.IsNullOrWhiteSpace(row.tabla), "adm_cai_correlativo_emitido no existe.");
        Assert.True(row.tiene_uuid, "adm_cai_correlativo_emitido debe tener lectura_uuid.");
        Assert.True(row.tiene_factura_id, "adm_cai_correlativo_emitido debe tener factura_id.");
    }

    [SkippableFact]
    public async Task SP_lectura_v3_lanza_FACTURA_YA_EMITIDA_en_doble_emision()
    {
        const string sql = @"
            SELECT f.clientecodigo, f.ano::int AS anio, f.mes::int AS mes, f.numfactura
            FROM public.factura f
            WHERE f.company_id = @CompanyId
              AND COALESCE(f.estado, '') <> 'N'
              AND f.numfactura IS NOT NULL
            ORDER BY f.id DESC
            LIMIT 1";

        var existente = await Connection.QueryFirstOrDefaultAsync<(string clientecodigo, int anio, int mes, string numfactura)?>(
            new CommandDefinition(sql, new { CompanyId = CompanyId }, Transaction));

        Skip.If(existente is null, "No hay facturas activas en esta company.");

        // Pasamos un numero_factura único (timestamp) para evitar el chequeo
        // "Ya existe factura con numero=..." (línea ~158 del SP) y forzar que
        // se evalúe el chequeo por periodo (línea ~172).
        var numUnico = $"TEST-{DateTime.UtcNow:HHmmssfff}";

        var exception = await Record.ExceptionAsync(async () =>
        {
            await Connection.QueryAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_lectura_v3(
                    p_company_id := @CompanyId,
                    p_anio := @Anio,
                    p_mes := @Mes,
                    p_clave := @Clave,
                    p_condicion_lectura := 'SM',
                    p_numero_factura := @NumFactura
                )",
                new
                {
                    CompanyId = CompanyId,
                    Anio = existente!.Value.anio,
                    Mes = existente.Value.mes,
                    Clave = existente.Value.clientecodigo,
                    NumFactura = numUnico
                }, Transaction));
        });

        Assert.NotNull(exception);
        Assert.Contains("FACTURA_YA_EMITIDA", exception!.Message);
    }
}
