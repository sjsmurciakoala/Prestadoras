using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 6 del plan de integración contable-comercial (2026-07-02):
// con_saldo_cuenta oficial. Cubre la reconstrucción total desde el libro
// (sp_con_reconstruir_saldo_cuenta), la reconciliación caché vs vivo
// (fn_con_verificar_saldo_cuenta) y el balance de comprobación híbrido
// (períodos cerrados leen del caché). Requiere los scripts F1–F6 aplicados;
// la BD de pruebas trae la migración SIMAFI (miles de partidas 2021-2026),
// así que la reconstrucción se ejercita sobre volumen real.
[Collection("Postgres")]
public sealed class SaldosOficialesTests : IntegrationTestBase
{
    public SaldosOficialesTests(PostgresFixture fixture) : base(fixture) { }

    private const long DocumentoBase = 960_000_000L;

    private sealed record SaldoFila(long PeriodoId, string CodigoCuenta, decimal Debitos, decimal Creditos,
        int CantidadDebitos, int CantidadCreditos, decimal Presupuesto);

    private Task<int> ReconstruirAsync(long companyId) =>
        Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT filas_insertadas FROM public.sp_con_reconstruir_saldo_cuenta(@companyId, 'test-f6')",
            new { companyId }, Transaction));

    private async Task<List<SaldoFila>> SnapshotCacheAsync(long companyId)
    {
        var filas = await Connection.QueryAsync<SaldoFila>(new CommandDefinition(@"
            SELECT periodo_id AS PeriodoId, codigo_cuenta AS CodigoCuenta, debitos AS Debitos,
                   creditos AS Creditos, cantidad_debitos AS CantidadDebitos,
                   cantidad_creditos AS CantidadCreditos, presupuesto AS Presupuesto
            FROM public.con_saldo_cuenta
            WHERE company_id = @companyId AND mes = 13 AND tipo_transaccion = 0
            ORDER BY periodo_id, codigo_cuenta",
            new { companyId }, Transaction));
        return filas.ToList();
    }

    private Task<int> DivergenciasAsync(long companyId) =>
        Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.fn_con_verificar_saldo_cuenta(@companyId)",
            new { companyId }, Transaction));

    /// <summary>
    /// Config mínima del comprobante F4 (perfil ERSAPS + asiento CAJA +
    /// período abierto hoy) para postear por el motor único dentro del test.
    /// </summary>
    private async Task<bool> ArrangeComprobanteAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-f6')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET activo_caja = true
            WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));

        var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            SELECT @CompanyId, 'CAJA',
                   (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                   (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                   'test-f6'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId }, Transaction));
        if (!asientoOk)
        {
            return false;
        }

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            SELECT @CompanyId, 'F6-TEST', 'Periodo test F6',
                   current_date - 1, current_date + 1, 0, 'OPEN', now(), 'test-f6'
            WHERE NOT EXISTS (
                SELECT 1 FROM public.con_periodo_contable p
                WHERE p.company_id = @CompanyId AND COALESCE(p.status_id, 2) = 0
                  AND current_date BETWEEN p.start_date::date AND p.end_date::date)",
            new { CompanyId }, Transaction));

        return true;
    }

    [SkippableFact]
    public async Task Reconstruccion_reproduce_exactamente_lo_que_el_motor_acumulo()
    {
        Skip.IfNot(await ArrangeComprobanteAsync(), "Falta diario/tipo en la BD de pruebas.");

        // Normalizar primero: el rebuild deja el caché = libro aunque la BD
        // compartida de dev traiga suciedad ambiental.
        var normalizadas = await ReconstruirAsync(CompanyId);
        Assert.True(normalizadas > 0, "La BD de pruebas no tiene saldos que reconstruir.");
        Assert.Equal(0, await DivergenciasAsync(CompanyId));

        // Postear un comprobante NUEVO por el motor único (F4): el motor
        // incrementa el caché línea a línea.
        var caja = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'CAJA', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));
        var cxc = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'CXC', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));

        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT public.sp_con_generar_comprobante_config(
                @CompanyId, 'CAJA', 'ABO', @DocumentId, @DocumentNumber,
                current_date, 'Comprobante test F6', 'test-f6',
                jsonb_build_array(
                    jsonb_build_object('account_id', @Caja, 'debe', 321.45, 'haber', 0, 'descripcion', 'Caja F6'),
                    jsonb_build_object('account_id', @Cxc, 'debe', 0, 'haber', 321.45, 'descripcion', 'CxC F6')))",
            new { CompanyId, DocumentId = DocumentoBase + 1, DocumentNumber = $"F6-{DocumentoBase + 1}", Caja = caja, Cxc = cxc },
            Transaction));
        Assert.NotNull(polizaId);

        // Lo que el motor acumuló (snapshot) debe ser EXACTAMENTE lo que la
        // reconstrucción recalcula desde el libro — incluida la migración
        // SIMAFI completa (miles de partidas, ~67 períodos).
        var motor = await SnapshotCacheAsync(CompanyId);
        var reconstruidas = await ReconstruirAsync(CompanyId);
        var rebuild = await SnapshotCacheAsync(CompanyId);

        Assert.Equal(motor.Count, reconstruidas);
        Assert.Equal(motor, rebuild);
        Assert.Equal(0, await DivergenciasAsync(CompanyId));
    }

    [SkippableFact]
    public async Task Verificador_detecta_divergencia_inyectada_y_reconstruccion_la_corrige()
    {
        await ReconstruirAsync(CompanyId);
        Assert.Equal(0, await DivergenciasAsync(CompanyId));

        var adulterada = await Connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            UPDATE public.con_saldo_cuenta s
            SET debitos = s.debitos + 5.55
            WHERE s.saldo_id = (
                SELECT s2.saldo_id FROM public.con_saldo_cuenta s2
                WHERE s2.company_id = @CompanyId AND s2.mes = 13 AND s2.tipo_transaccion = 0
                  AND s2.debitos > 0
                ORDER BY s2.periodo_id, s2.codigo_cuenta LIMIT 1)
            RETURNING s.codigo_cuenta",
            new { CompanyId }, Transaction));
        Skip.If(adulterada is null, "La BD de pruebas no tiene saldos cacheados.");

        var divergencia = await Connection.QueryFirstAsync<(string codigo, string tipo, decimal? cache, decimal? libro)>(
            new CommandDefinition(@"
                SELECT codigo_cuenta, tipo_divergencia, debitos_cache, debitos_libro
                FROM public.fn_con_verificar_saldo_cuenta(@CompanyId)",
                new { CompanyId }, Transaction));

        Assert.Equal(1, await DivergenciasAsync(CompanyId));
        Assert.Equal(adulterada, divergencia.codigo);
        Assert.Equal("MONTOS", divergencia.tipo);
        Assert.Equal(5.55m, divergencia.cache - divergencia.libro);

        await ReconstruirAsync(CompanyId);
        Assert.Equal(0, await DivergenciasAsync(CompanyId));
    }

    [SkippableFact]
    public async Task Balance_comprobacion_de_periodo_cerrado_lee_del_cache()
    {
        await ReconstruirAsync(CompanyId);

        // Cuenta hoja con débitos en un período CERRADO (la migración SIMAFI
        // de la BD de pruebas los provee); el rango del reporte = el período
        // completo, así el híbrido lo clasifica al bucket del caché.
        var objetivo = await Connection.QueryFirstOrDefaultAsync<(long periodoId, string codigo, DateTime desde, DateTime hasta)>(
            new CommandDefinition(@"
                SELECT s.periodo_id, s.codigo_cuenta, p.start_date::date, p.end_date::date
                FROM public.con_saldo_cuenta s
                JOIN public.con_periodo_contable p ON p.period_id = s.periodo_id
                JOIN public.con_plan_cuentas a
                  ON a.company_id = s.company_id AND a.code = s.codigo_cuenta
                WHERE s.company_id = @CompanyId AND s.mes = 13 AND s.tipo_transaccion = 0
                  AND s.debitos > 0
                  AND p.status_id = 2
                  AND a.allows_posting
                  AND NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas h
                                  WHERE h.company_id = a.company_id AND h.parent_account_id = a.account_id)
                ORDER BY s.periodo_id, s.codigo_cuenta
                LIMIT 1",
                new { CompanyId }, Transaction));
        Skip.If(objetivo.codigo is null, "La BD de pruebas no tiene períodos cerrados con saldos cacheados.");

        var vivo = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT COALESCE(SUM(d.debit_amount), 0)
            FROM public.con_partida_hdr h
            JOIN public.con_partida_dtl d ON d.company_id = h.company_id AND d.poliza_id = h.poliza_id
            JOIN public.con_plan_cuentas a ON a.account_id = d.account_id
            WHERE h.company_id = @CompanyId AND h.status = 1 AND a.code = @Codigo
              AND h.poliza_date::date BETWEEN @Desde AND @Hasta",
            new { CompanyId, Codigo = objetivo.codigo, objetivo.desde, objetivo.hasta }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_saldo_cuenta
            SET debitos = debitos + 777.77
            WHERE company_id = @CompanyId AND mes = 13 AND tipo_transaccion = 0
              AND periodo_id = @PeriodoId AND codigo_cuenta = @Codigo",
            new { CompanyId, objetivo.periodoId, Codigo = objetivo.codigo }, Transaction));

        // Período cerrado → el reporte refleja el caché (adulterado a propósito).
        var reporteCache = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT debitos_periodo
            FROM public.rep_balance_comprobacion(@CompanyId, @Desde::date, @Hasta::date, true)
            WHERE cuenta_codigo = @Codigo",
            new { CompanyId, objetivo.desde, objetivo.hasta, Codigo = objetivo.codigo }, Transaction));
        Assert.Equal(vivo + 777.77m, reporteCache);

        // Tras reconstruir, el reporte vuelve al valor del libro.
        await ReconstruirAsync(CompanyId);
        var reporteReconstruido = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT debitos_periodo
            FROM public.rep_balance_comprobacion(@CompanyId, @Desde::date, @Hasta::date, true)
            WHERE cuenta_codigo = @Codigo",
            new { CompanyId, objetivo.desde, objetivo.hasta, Codigo = objetivo.codigo }, Transaction));
        Assert.Equal(vivo, reporteReconstruido);
    }

    [SkippableFact]
    public async Task Reconstruccion_y_verificacion_solo_tocan_la_empresa_pedida()
    {
        await ReconstruirAsync(CompanyId);

        // Empresa mínima ajena, con una fila de caché SIN respaldo en el libro
        // (divergencia SOLO_CACHE fabricada). Todo dentro de la transacción.
        var otraCompany = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.cfg_company
                (code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES ('F6X', 'Empresa test F6', 'Empresa test F6', '0000', 'HN', 'HNL', 'America/Tegucigalpa', 'ACTIVO', now(), 'test-f6')
            RETURNING company_id", transaction: Transaction));

        var otroPeriodo = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            VALUES (@otraCompany, '209901', 'Enero 2099 F6', DATE '2099-01-01', DATE '2099-01-31', 2, 'CERRADO', now(), 'test-f6')
            RETURNING period_id", new { otraCompany }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_plan_cuentas
                (company_id, code, name, account_type, status, created_at, created_by, allows_posting, level)
            VALUES (@otraCompany, '10101', 'Caja test F6', 'ACTIVO', 'ACTIVO', now(), 'test-f6', true, 1)",
            new { otraCompany }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_saldo_cuenta
                (company_id, periodo_id, codigo_cuenta, mes, tipo_transaccion,
                 debitos, creditos, cantidad_debitos, cantidad_creditos, presupuesto, created_at)
            VALUES (@otraCompany, @otroPeriodo, '10101', 13, 0, 10.00, 0, 1, 0, 0, now())",
            new { otraCompany, otroPeriodo }, Transaction));

        // La verificación es por empresa: la ajena diverge, la propia no.
        Assert.Equal(1, await DivergenciasAsync(otraCompany));
        Assert.Equal(0, await DivergenciasAsync(CompanyId));

        // Reconstruir la empresa propia NO toca a la ajena.
        var filasAjenasAntes = await SnapshotCacheAsync(otraCompany);
        await ReconstruirAsync(CompanyId);
        Assert.Equal(filasAjenasAntes, await SnapshotCacheAsync(otraCompany));
        Assert.Equal(1, await DivergenciasAsync(otraCompany));

        // Reconstruir la ajena elimina su fila sin libro y no altera la propia.
        var filasPropias = await SnapshotCacheAsync(CompanyId);
        var insertadasAjena = await ReconstruirAsync(otraCompany);
        Assert.Equal(0, insertadasAjena);
        Assert.Equal(0, await DivergenciasAsync(otraCompany));
        Assert.Equal(filasPropias, await SnapshotCacheAsync(CompanyId));
    }

    [SkippableFact]
    public async Task Reconstruccion_rechaza_empresa_inexistente()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ReconstruirAsync(999_999_999L));
        Assert.Contains("cfg_company", ex.MessageText);
    }
}
