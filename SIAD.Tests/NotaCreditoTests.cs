using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class NotaCreditoTests : IntegrationTestBase
{
    public NotaCreditoTests(PostgresFixture fixture) : base(fixture) { }

    [SkippableFact]
    public async Task Tablas_y_SPs_NC_ND_V3_existen()
    {
        const string sql = @"
            SELECT
                to_regclass('public.adm_nota_credito')::text           AS tabla_nc,
                to_regclass('public.adm_nota_credito_detalle')::text   AS tabla_nc_det,
                to_regclass('public.adm_nota_debito')::text            AS tabla_nd,
                to_regclass('public.adm_nota_debito_detalle')::text    AS tabla_nd_det,
                to_regclass('public.cfg_motivo_aumento')::text         AS catalogo_motivo_aumento,
                to_regproc('public.sp_adm_emitir_nota_credito')::text  AS sp_nc,
                to_regproc('public.sp_adm_emitir_nota_debito')::text   AS sp_nd";

        var row = await Connection.QueryFirstAsync<(string? tabla_nc, string? tabla_nc_det,
            string? tabla_nd, string? tabla_nd_det, string? catalogo_motivo_aumento,
            string? sp_nc, string? sp_nd)>(
                new CommandDefinition(sql, transaction: Transaction));

        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nc), "adm_nota_credito no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nc_det), "adm_nota_credito_detalle no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nd), "adm_nota_debito no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nd_det), "adm_nota_debito_detalle no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.catalogo_motivo_aumento), "cfg_motivo_aumento no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.sp_nc), "sp_adm_emitir_nota_credito no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.sp_nd), "sp_adm_emitir_nota_debito no existe.");
    }

    [SkippableFact]
    public async Task SP_emitir_nota_credito_rechaza_factura_inexistente()
    {
        const string sql = @"
            SELECT cai_id FROM public.adm_cai_facturacion
            WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
            LIMIT 1";
        var caiNcId = await Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(sql, new { CompanyId = CompanyId }, Transaction));

        Skip.If(caiNcId is null, "No hay CAI tipo NC (6) en esta company — no se puede probar.");

        var exception = await Record.ExceptionAsync(async () =>
        {
            await Connection.QueryAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_adm_emitir_nota_credito(
                    p_company_id := @CompanyId,
                    p_factura_origen_id := -1,
                    p_motivo_anulacion_id := 1::smallint,
                    p_motivo_detalle := 'test inexistente',
                    p_monto_disminuir := NULL::numeric,
                    p_lineas := NULL::jsonb,
                    p_usuario_emisor := 'TEST',
                    p_cai_id := @CaiId
                )",
                new { CompanyId = CompanyId, CaiId = caiNcId }, Transaction));
        });

        Assert.NotNull(exception);
        Assert.Contains("FACTURA_NO_EXISTE", exception!.Message);
    }

    [SkippableFact]
    public async Task SP_emitir_nota_credito_rechaza_factura_ya_anulada()
    {
        const string sql = @"
            SELECT f.id
            FROM public.factura f
            WHERE f.company_id = @CompanyId AND COALESCE(f.estado, '') = 'N'
            LIMIT 1";

        var facturaAnulada = await Connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { CompanyId = CompanyId }, Transaction));

        Skip.If(facturaAnulada is null, "No hay facturas anuladas en esta company — no se puede probar el rechazo.");

        var caiNcId = await Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(@"
                SELECT cai_id FROM public.adm_cai_facturacion
                WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
                LIMIT 1",
                new { CompanyId = CompanyId }, Transaction));

        Skip.If(caiNcId is null, "No hay CAI tipo NC para esta company.");

        var exception = await Record.ExceptionAsync(async () =>
        {
            await Connection.QueryAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_adm_emitir_nota_credito(
                    p_company_id := @CompanyId,
                    p_factura_origen_id := @FacturaId,
                    p_motivo_anulacion_id := 1::smallint,
                    p_motivo_detalle := 'test rechazo',
                    p_monto_disminuir := NULL::numeric,
                    p_lineas := NULL::jsonb,
                    p_usuario_emisor := 'TEST',
                    p_cai_id := @CaiId
                )",
                new { CompanyId = CompanyId, FacturaId = facturaAnulada, CaiId = caiNcId }, Transaction));
        });

        Assert.NotNull(exception);
        Assert.Contains("FACTURA_YA_ANULADA", exception!.Message);
    }
}
