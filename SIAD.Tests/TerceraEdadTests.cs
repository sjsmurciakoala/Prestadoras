using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class TerceraEdadTests : IntegrationTestBase
{
    public TerceraEdadTests(PostgresFixture fixture) : base(fixture) { }

    [SkippableFact]
    public async Task Existe_ajuste_TERCERA_EDAD_DOMESTICO_con_tope_300()
    {
        const string sql = @"
            SELECT (parametros ->> 'tope_mensual')::numeric AS tope
            FROM public.adm_ajuste_tarifario
            WHERE condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
            ORDER BY ajuste_tarifario_id DESC
            LIMIT 1";

        var tope = await Connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(sql, transaction: Transaction));

        Skip.If(tope is null, "No hay ajuste TERCERA_EDAD_DOMESTICO en esta BD.");

        Assert.Equal(300.00m, tope!.Value);
    }

    [SkippableFact]
    public async Task Ajuste_TERCERA_EDAD_DOMESTICO_es_porcentaje_25()
    {
        const string sql = @"
            SELECT porcentaje
            FROM public.adm_ajuste_tarifario
            WHERE condicion_codigo = 'TERCERA_EDAD_DOMESTICO'
            ORDER BY ajuste_tarifario_id DESC
            LIMIT 1";

        var porcentaje = await Connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(sql, transaction: Transaction));

        Skip.If(porcentaje is null, "No hay ajuste TERCERA_EDAD_DOMESTICO en esta BD.");

        Assert.Equal(25m, porcentaje!.Value);
    }
}
