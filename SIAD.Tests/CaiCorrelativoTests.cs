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
        var def = await FunctionDefAsync("sp_adm_confirmar_correlativo_cai_sync");

        Skip.If(string.IsNullOrWhiteSpace(def), "sp_adm_confirmar_correlativo_cai_sync no existe.");

        // Desde el BUGFIX #4 (2026-08-22) el avance vive en el helper compartido
        // con el prepare; antes estaba inline en esta funcion.
        var helper = await FunctionDefAsync("sp_adm_avanzar_correlativo_actual_cai");

        if (!string.IsNullOrWhiteSpace(helper))
        {
            Assert.Contains("sp_adm_avanzar_correlativo_actual_cai", def, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GREATEST", helper, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("correlativo_actual", helper, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("adm_cai_facturacion", helper, StringComparison.OrdinalIgnoreCase);
            return;
        }

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

    // BUGFIX #4 (2026-08-22) — reservar un correlativo tambien lo consume.
    // El escenario funcional vive en Database/ddl_v3/tests/20260822_bug4_*.sql.

    [SkippableFact]
    public async Task SP_prepare_correlativo_cai_sync_avanza_el_contador()
    {
        var def = await FunctionDefAsync("sp_adm_prepare_correlativo_cai_sync");

        Skip.If(string.IsNullOrWhiteSpace(def), "sp_adm_prepare_correlativo_cai_sync no existe.");

        Assert.Contains("sp_adm_avanzar_correlativo_actual_cai", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status_id = 1", def, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Reparto_de_bloque_deriva_del_correlativo_realmente_emitido()
    {
        var def = await FunctionDefAsync("sp_adm_obtener_o_reservar_bloque_cai_ruta");

        Skip.If(string.IsNullOrWhiteSpace(def), "sp_adm_obtener_o_reservar_bloque_cai_ruta no existe.");

        Assert.Contains("adm_cai_correlativo_emitido", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max_emitido", def, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Ningun_bloque_quedo_por_debajo_de_lo_emitido()
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM public.adm_cai_bloque_reservado b
            JOIN (
                SELECT e.company_id, e.cai_bloque_id, MAX(e.correlativo) AS max_correlativo
                FROM public.adm_cai_correlativo_emitido e
                WHERE e.status_id = 1
                  AND e.estado_codigo <> 'SYNC_CONFLICT'
                GROUP BY e.company_id, e.cai_bloque_id
            ) sub ON sub.company_id = b.company_id AND sub.cai_bloque_id = b.cai_bloque_id
            WHERE b.correlativo_actual < sub.max_correlativo";

        var tabla = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition("SELECT to_regclass('public.adm_cai_correlativo_emitido')::text",
                transaction: Transaction));

        Skip.If(tabla is null, "adm_cai_correlativo_emitido no existe.");

        var desfasados = await Connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, transaction: Transaction));

        Assert.True(desfasados == 0,
            $"{desfasados} bloque(s) con correlativo_actual por debajo de lo emitido: el proximo snapshot repartiria un correlativo ya tomado.");
    }

    private Task<string?> FunctionDefAsync(string nombre)
    {
        const string sql = @"
            SELECT pg_get_functiondef(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'public' AND p.proname = @Nombre
            LIMIT 1";

        return Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { Nombre = nombre }, transaction: Transaction));
    }
}
