using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Retenciones;

// Catálogo de retenciones a proveedores (F1, 2026-08-06):
//   cfg_retencion / cfg_retencion_tasa (GLOBALES) + prv_retencion_cuenta (TENANT).
// Requiere una BD con el script Database/2026-08-06_cfg_retenciones.sql aplicado.
// Cada test corre dentro de BEGIN…ROLLBACK (IntegrationTestBase), así que los códigos
// fijos 'TEST-RET-*' no persisten ni chocan entre corridas.
[Collection("Postgres")]
public sealed class RetencionesCatalogoTests : IntegrationTestBase
{
    public RetencionesCatalogoTests(PostgresFixture fixture) : base(fixture) { }

    private Task<int> InsertRetencionAsync(string codigo, string baseCalculo = "SIN_ISV", string tipoImpuesto = "ISR") =>
        Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cfg_retencion (codigo, nombre, base_calculo, tipo_impuesto, usuariocreacion)
            VALUES (@Codigo, 'Retención de prueba', @Base, @Tipo, 'TEST')
            RETURNING id",
            new { Codigo = codigo, Base = baseCalculo, Tipo = tipoImpuesto }, Transaction));

    private Task<long?> CuentaPosteableAsync() =>
        Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT account_id FROM public.con_plan_cuentas
            WHERE company_id = @CompanyId AND allows_posting
            LIMIT 1",
            new { CompanyId }, Transaction));

    // ------------------------------------------------------------------ CRUD

    [SkippableFact]
    public async Task Crud_retencion_y_tasa()
    {
        const string codigo = "TEST-RET-CRUD";

        var retencionId = await InsertRetencionAsync(codigo);
        Assert.True(retencionId > 0);

        var tasaId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, usuariocreacion)
            VALUES (@RetencionId, 12.50, DATE '2020-01-01', 'TEST')
            RETURNING id",
            new { RetencionId = retencionId }, Transaction));
        Assert.True(tasaId > 0);

        // Tupla de Dapper = mapeo POSICIONAL (columnas en el mismo orden que la tupla).
        var leido = await Connection.QueryFirstAsync<(string codigo, string baseCalculo, string tipo, decimal porcentaje)>(
            new CommandDefinition(@"
                SELECT r.codigo, r.base_calculo, r.tipo_impuesto, t.porcentaje
                FROM public.cfg_retencion r
                JOIN public.cfg_retencion_tasa t ON t.retencion_id = r.id
                WHERE r.id = @Id",
                new { Id = retencionId }, Transaction));

        Assert.Equal(codigo, leido.codigo);
        Assert.Equal("SIN_ISV", leido.baseCalculo);
        Assert.Equal("ISR", leido.tipo);
        Assert.Equal(12.50m, leido.porcentaje);
    }

    // ------------------------------------- no-solape de vigencia (EXCLUDE gist)

    [SkippableFact]
    public async Task Tasas_con_vigencias_solapadas_violan_el_exclude()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-EXCLUDE");

        // Primera tasa: vigente desde 2020, abierta (vigencia_hasta NULL = infinito).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, usuariocreacion)
            VALUES (@RetencionId, 12.50, DATE '2020-01-01', 'TEST')",
            new { RetencionId = retencionId }, Transaction));

        // Segunda tasa de la MISMA retención cuyo rango se pisa con la abierta.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, usuariocreacion)
                VALUES (@RetencionId, 13.00, DATE '2021-01-01', 'TEST')",
                new { RetencionId = retencionId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.ExclusionViolation, ex.SqlState); // 23P01
    }

    [SkippableFact]
    public async Task Tasas_de_vigencias_contiguas_no_se_solapan()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-CONTIGUA");

        // Cerrada 2020-01-01 → 2020-12-31.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, vigencia_hasta, usuariocreacion)
            VALUES (@RetencionId, 12.50, DATE '2020-01-01', DATE '2020-12-31', 'TEST')",
            new { RetencionId = retencionId }, Transaction));

        // Sucesora abierta desde el día siguiente: NO se solapa.
        var id = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, usuariocreacion)
            VALUES (@RetencionId, 13.00, DATE '2021-01-01', 'TEST')
            RETURNING id",
            new { RetencionId = retencionId }, Transaction));

        Assert.True(id > 0);
    }

    // ------------------------------------------- tenancy de prv_retencion_cuenta

    [SkippableFact]
    public async Task Cuenta_por_empresa_no_permite_duplicado_company_retencion()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-CUENTA-UQ");
        var accountId = await CuentaPosteableAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en la empresa de prueba.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.prv_retencion_cuenta (company_id, retencion_id, account_id, usuariocreacion)
            VALUES (@CompanyId, @RetencionId, @AccountId, 'TEST')",
            new { CompanyId, RetencionId = retencionId, AccountId = accountId }, Transaction));

        // Segunda cuenta para la MISMA (empresa, retención) → viola el UNIQUE.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.prv_retencion_cuenta (company_id, retencion_id, account_id, usuariocreacion)
                VALUES (@CompanyId, @RetencionId, @AccountId, 'TEST')",
                new { CompanyId, RetencionId = retencionId, AccountId = accountId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState); // 23505
    }

    [SkippableFact]
    public async Task Cuenta_por_empresa_es_tenant_scoped()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-TENANT");
        var accountId = await CuentaPosteableAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en la empresa de prueba.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.prv_retencion_cuenta (company_id, retencion_id, account_id, usuariocreacion)
            VALUES (@CompanyId, @RetencionId, @AccountId, 'TEST')",
            new { CompanyId, RetencionId = retencionId, AccountId = accountId }, Transaction));

        // La fila existe para la empresa del test…
        var enEmpresa = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.prv_retencion_cuenta
            WHERE company_id = @CompanyId AND retencion_id = @RetencionId",
            new { CompanyId, RetencionId = retencionId }, Transaction));
        Assert.Equal(1, enEmpresa);

        // …y NO para otra empresa (aislamiento por company_id).
        var otraEmpresa = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.prv_retencion_cuenta
            WHERE company_id = @Otra AND retencion_id = @RetencionId",
            new { Otra = CompanyId + 999_999, RetencionId = retencionId }, Transaction));
        Assert.Equal(0, otraEmpresa);
    }

    [SkippableFact]
    public async Task Cuenta_por_empresa_exige_cuenta_existente()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-CUENTA-FK");

        // account_id inexistente → viola la FK a con_plan_cuentas.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.prv_retencion_cuenta (company_id, retencion_id, account_id, usuariocreacion)
                VALUES (@CompanyId, @RetencionId, @AccountId, 'TEST')",
                new { CompanyId, RetencionId = retencionId, AccountId = -999_999L }, Transaction)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState); // 23503
    }

    // ---------------------------------------------------- CHECKs de coherencia

    [SkippableFact]
    public async Task Base_calculo_invalida_viola_check()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.cfg_retencion (codigo, nombre, base_calculo, tipo_impuesto, usuariocreacion)
                VALUES ('TEST-RET-BADBASE', 'x', 'OTRA', 'ISR', 'TEST')",
                new { }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState); // 23514
    }

    [SkippableFact]
    public async Task Tipo_impuesto_invalido_viola_check()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.cfg_retencion (codigo, nombre, base_calculo, tipo_impuesto, usuariocreacion)
                VALUES ('TEST-RET-BADTIPO', 'x', 'SIN_ISV', 'IVA', 'TEST')",
                new { }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState); // 23514
    }

    [SkippableFact]
    public async Task Porcentaje_no_positivo_viola_check()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-BADPCT");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, usuariocreacion)
                VALUES (@RetencionId, 0, DATE '2020-01-01', 'TEST')",
                new { RetencionId = retencionId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState); // 23514
    }

    [SkippableFact]
    public async Task Vigencia_hasta_anterior_a_desde_viola_check()
    {
        var retencionId = await InsertRetencionAsync("TEST-RET-BADVIG");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.cfg_retencion_tasa (retencion_id, porcentaje, vigencia_desde, vigencia_hasta, usuariocreacion)
                VALUES (@RetencionId, 12.50, DATE '2021-01-01', DATE '2020-12-31', 'TEST')",
                new { RetencionId = retencionId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState); // 23514
    }

    [SkippableFact]
    public async Task Codigo_de_retencion_es_unico()
    {
        await InsertRetencionAsync("TEST-RET-DUP");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InsertRetencionAsync("TEST-RET-DUP"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState); // 23505
    }
}
