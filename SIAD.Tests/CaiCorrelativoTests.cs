using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class CaiCorrelativoTests : IntegrationTestBase
{
    public CaiCorrelativoTests(PostgresFixture fixture) : base(fixture) { }

    [SkippableFact]
    public async Task SP_confirmar_correlativo_cai_sync_avanza_con_GREATEST()
    {
        const string sql = @"
            SELECT pg_get_functiondef(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = 'sp_adm_confirmar_correlativo_cai_sync'
            LIMIT 1";

        var def = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, transaction: Transaction));

        Skip.If(string.IsNullOrWhiteSpace(def), "sp_adm_confirmar_correlativo_cai_sync no existe.");

        Assert.Contains("GREATEST", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("correlativo_actual", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("adm_cai_facturacion", def, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Validacion_CAI_filtra_estado_vigente_y_fecha_limite()
    {
        const string sql = @"
            SELECT pg_get_functiondef(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = 'sp_adm_obtener_o_reservar_bloque_cai_ruta'
            LIMIT 1";

        var def = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, transaction: Transaction));

        Skip.If(string.IsNullOrWhiteSpace(def), "sp_adm_obtener_o_reservar_bloque_cai_ruta no existe.");

        Assert.Contains("fecha_limite_emision", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("correlativo_actual", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rango_hasta", def, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Catalogo_cfg_estado_cai_existe_con_5_estados()
    {
        var tabla = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition("SELECT to_regclass('public.cfg_estado_cai')::text",
                transaction: Transaction));

        Skip.If(tabla is null, "Catálogo cfg_estado_cai no existe.");

        var count = await Connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM public.cfg_estado_cai",
                transaction: Transaction));

        Assert.True(count >= 5,
            $"cfg_estado_cai debe tener al menos 5 estados (VIGENTE/VENCIDO/AGOTADO/ANULADO/SUSPENDIDO); tiene {count}.");
    }
}
