using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class SarComplianceTests : IntegrationTestBase
{
    public SarComplianceTests(PostgresFixture fixture) : base(fixture) { }

    [SkippableTheory]
    [InlineData("cfg_tipo_documento_fiscal")]
    [InlineData("cfg_motivo_anulacion")]
    [InlineData("cfg_estado_documento_fiscal")]
    [InlineData("cfg_estado_cai")]
    public async Task Catalogos_SAR_existen(string tabla)
    {
        var oid = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition($"SELECT to_regclass('public.{tabla}')::text",
                transaction: Transaction));

        Assert.False(string.IsNullOrWhiteSpace(oid),
            $"Catálogo SAR '{tabla}' no existe.");
    }

    [SkippableFact]
    public async Task adm_cai_facturacion_tiene_columnas_SAR_compliance()
    {
        const string sql = @"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'adm_cai_facturacion'";

        var columnas = (await Connection.QueryAsync<string>(
            new CommandDefinition(sql, transaction: Transaction))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // El RTN del emisor vive en cfg_company y se snapshot-ea a factura/NC/ND al emitir
        // (decisión 2026-05-07/08). NO debe estar en adm_cai_facturacion.
        Assert.Contains("tipo_documento_fiscal_id", columnas);
        Assert.Contains("establecimiento_codigo", columnas);
        Assert.Contains("punto_emision", columnas);
        Assert.Contains("fecha_limite_emision", columnas);
        Assert.Contains("leyenda_rango", columnas);
        Assert.Contains("estado_id", columnas);
        Assert.Contains("correlativo_actual", columnas);
    }

    [SkippableFact]
    public async Task Factura_tiene_columnas_SAR_compliance_y_company_id_NOT_NULL()
    {
        const string sql = @"
            SELECT column_name, is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'factura'";

        var columnas = (await Connection.QueryAsync<(string column_name, string is_nullable)>(
            new CommandDefinition(sql, transaction: Transaction))).ToDictionary(
                r => r.column_name, r => r.is_nullable, StringComparer.OrdinalIgnoreCase);

        Assert.True(columnas.ContainsKey("company_id"), "factura debe tener company_id.");
        Assert.Equal("NO", columnas["company_id"]);

        Assert.True(columnas.ContainsKey("tipo_documento_fiscal_id"),
            "factura debe tener tipo_documento_fiscal_id (SAR Acuerdo 481).");
    }

    [SkippableFact]
    public async Task adm_establecimiento_NO_existe_mono_sucursal()
    {
        var oid = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition("SELECT to_regclass('public.adm_establecimiento')::text",
                transaction: Transaction));

        Assert.True(string.IsNullOrWhiteSpace(oid),
            "Decisión arquitectónica 2026-05-08: mono-sucursal. adm_establecimiento debió eliminarse.");
    }

    [SkippableFact]
    public async Task Funcion_validar_cai_emitible_existe()
    {
        var oid = await Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition("SELECT to_regproc('public.fn_adm_validar_cai_emitible')::text",
                transaction: Transaction));

        Assert.False(string.IsNullOrWhiteSpace(oid),
            "fn_adm_validar_cai_emitible es bloqueante para emitir factura legalmente.");
    }
}
