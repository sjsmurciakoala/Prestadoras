using System.Text.Json;
using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 7 del plan de integración contable-comercial (2026-07-02):
// adm_periodo_comercial(_ciclo) reemplaza a historialmes, espejo legacy,
// cierres comercial y contable con checklist, y avisos de períodos.
// Requiere los scripts de F1–F7 aplicados en la BD de pruebas.
// Los períodos fabricados usan años 2001/2099 y ciclos '98'/'99' (sin
// catálogo ni datos reales) para no depender del estado de la BD.
[Collection("Postgres")]
public sealed class PeriodoCierreF7Tests : IntegrationTestBase
{
    public PeriodoCierreF7Tests(PostgresFixture fixture) : base(fixture) { }

    // Fase B (2026-07-15): la apertura vive en sp_adm_periodo_ciclo_abrir
    // (integral, sin bypass de secuencia; devuelve resumen jsonb). Los meses
    // fabricados aquí no son consecutivos, así que la secuencia no bloquea.
    private async Task<long> AbrirComercialAsync(int anio, int mes, string ciclo)
    {
        var json = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(@"
            SELECT public.sp_adm_periodo_ciclo_abrir(@CompanyId, @Anio, @Mes, @Ciclo, 'test-f7')::text",
            new { CompanyId, Anio = anio, Mes = mes, Ciclo = ciclo }, Transaction));
        using var doc = JsonDocument.Parse(json!);
        return doc.RootElement.GetProperty("periodo_comercial_id").GetInt64();
    }

    private Task<long> CicloIdAsync(long periodoId, string ciclo) =>
        Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT periodo_ciclo_id FROM public.adm_periodo_comercial_ciclo
            WHERE company_id = @CompanyId AND periodo_comercial_id = @PeriodoId AND ciclo_codigo = @Ciclo",
            new { CompanyId, PeriodoId = periodoId, Ciclo = ciclo }, Transaction));

    private Task CerrarCicloAsync(long periodoCicloId, bool forzar = false) =>
        Connection.ExecuteAsync(new CommandDefinition(@"
            SELECT public.sp_adm_periodo_ciclo_cerrar(@CompanyId, @CicloId, 'test-f7', @Forzar)",
            new { CompanyId, CicloId = periodoCicloId, Forzar = forzar }, Transaction));

    private Task CerrarMesAsync(long periodoId) =>
        Connection.ExecuteAsync(new CommandDefinition(@"
            SELECT public.sp_adm_periodo_comercial_cerrar(@CompanyId, @PeriodoId, 'test-f7')",
            new { CompanyId, PeriodoId = periodoId }, Transaction));

    private Task<long> InsertarPeriodoContableAsync(string code, DateTime desde, DateTime hasta, short statusId = 0) =>
        Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status, status_id, created_at, created_by)
            VALUES (@CompanyId, @Code, 'Periodo test F7', @Desde, @Hasta,
                    CASE @Status::smallint WHEN 0 THEN 'OPEN' WHEN 1 THEN 'PRECIERRE' ELSE 'CERRADO' END,
                    @Status, now(), 'test-f7')
            RETURNING period_id",
            new { CompanyId, Code = code, Desde = desde, Hasta = hasta, Status = statusId }, Transaction));

    // ── Migración y funciones de consulta ──────────────────────────────────

    [SkippableFact]
    public async Task Migracion_replica_cada_fila_de_historialmes_con_estado_equivalente()
    {
        var filasLegacy = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.historialmes", transaction: Transaction));
        Skip.If(filasLegacy == 0, "historialmes está vacía en la BD de pruebas.");

        var sinReplica = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*)
            FROM public.historialmes hm
            WHERE NOT EXISTS (
                SELECT 1
                FROM public.adm_periodo_comercial p
                JOIN public.adm_periodo_comercial_ciclo pc
                  ON pc.company_id = p.company_id
                 AND pc.periodo_comercial_id = p.periodo_comercial_id
                WHERE p.company_id = @CompanyId
                  AND p.anio = hm.ano::int
                  AND p.mes = hm.mes::int
                  AND pc.ciclo_codigo = btrim(hm.ciclo)
                  AND pc.status_id = CASE WHEN COALESCE(hm.cerrarperiodo, 'P') = 'P' THEN 1 ELSE 2 END)",
            new { CompanyId }, Transaction));

        Assert.Equal(0, sinReplica);
    }

    [SkippableFact]
    public async Task Fn_periodo_actual_devuelve_el_abierto_mas_reciente()
    {
        var esperado = await Connection.QueryFirstOrDefaultAsync<(int anio, short mes)?>(new CommandDefinition(@"
            SELECT anio, mes FROM public.adm_periodo_comercial
            WHERE company_id = @CompanyId AND status_id = 1
            ORDER BY anio DESC, mes DESC LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(esperado is null, "No hay períodos comerciales abiertos en la BD de pruebas.");

        var actual = await Connection.QueryFirstAsync<(long id, int anio, short mes)>(new CommandDefinition(@"
            SELECT periodo_comercial_id, anio, mes FROM public.fn_adm_periodo_comercial_actual(@CompanyId)",
            new { CompanyId }, Transaction));

        Assert.Equal(esperado.Value.anio, actual.anio);
        Assert.Equal(esperado.Value.mes, actual.mes);
    }

    [Fact]
    public async Task Fn_ciclo_abierto_normaliza_el_codigo_de_ciclo()
    {
        var periodoId = await AbrirComercialAsync(2099, 1, "09");
        Assert.True(periodoId > 0);

        // '09' y '9' deben resolver al mismo ciclo; un mes sin período no.
        var conPadding = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT public.fn_adm_periodo_comercial_ciclo_abierto(@CompanyId, 2099, 1, '09')",
            new { CompanyId }, Transaction));
        var sinPadding = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT public.fn_adm_periodo_comercial_ciclo_abierto(@CompanyId, 2099, 1, '9')",
            new { CompanyId }, Transaction));
        var mesInexistente = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT public.fn_adm_periodo_comercial_ciclo_abierto(@CompanyId, 2099, 2, '09')",
            new { CompanyId }, Transaction));

        Assert.True(conPadding);
        Assert.True(sinPadding);
        Assert.False(mesInexistente);
    }

    // ── Ciclo de vida comercial + espejo historialmes ──────────────────────

    [Fact]
    public async Task Flujo_comercial_completo_abre_cierra_y_sincroniza_el_espejo()
    {
        var periodoId = await AbrirComercialAsync(2099, 1, "99");

        // Espejo: la fila legacy nace abierta con la convención del WS (A/P)
        var espejo = await Connection.QueryFirstAsync<(string cerrado, string cerrarperiodo)>(new CommandDefinition(@"
            SELECT cerrado::text, cerrarperiodo::text FROM public.historialmes
            WHERE ano = 2099 AND mes = 1 AND ciclo = '99'", transaction: Transaction));
        Assert.Equal("A", espejo.cerrado);
        Assert.Equal("P", espejo.cerrarperiodo);

        // Ciclo '99' no existe en el catálogo → sin rutas que validar → cierra sin forzar
        var cicloId = await CicloIdAsync(periodoId, "99");
        await CerrarCicloAsync(cicloId);

        espejo = await Connection.QueryFirstAsync<(string cerrado, string cerrarperiodo)>(new CommandDefinition(@"
            SELECT cerrado::text, cerrarperiodo::text FROM public.historialmes
            WHERE ano = 2099 AND mes = 1 AND ciclo = '99'", transaction: Transaction));
        Assert.Equal("C", espejo.cerrado);
        Assert.Equal("C", espejo.cerrarperiodo);

        // Con todos los ciclos cerrados el checklist queda en verde y el mes cierra
        var fallas = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.fn_adm_periodo_comercial_checklist(@CompanyId, @PeriodoId) c
            WHERE NOT c.ok",
            new { CompanyId, PeriodoId = periodoId }, Transaction));
        Assert.Equal(0, fallas);

        await CerrarMesAsync(periodoId);

        var status = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            SELECT status_id FROM public.adm_periodo_comercial
            WHERE company_id = @CompanyId AND periodo_comercial_id = @PeriodoId",
            new { CompanyId, PeriodoId = periodoId }, Transaction));
        Assert.Equal((short)2, status);

        var abierto = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT public.fn_adm_periodo_comercial_ciclo_abierto(@CompanyId, 2099, 1, '99')",
            new { CompanyId }, Transaction));
        Assert.False(abierto);
    }

    [Fact]
    public async Task Cierre_de_mes_bloquea_si_hay_ciclos_abiertos()
    {
        var periodoId = await AbrirComercialAsync(2099, 3, "98");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => CerrarMesAsync(periodoId));
        Assert.Contains("CHECKLIST_CIERRE_COMERCIAL", ex.MessageText);
        Assert.Contains("CICLOS_ABIERTOS", ex.MessageText);
    }

    [Fact]
    public async Task Abrir_mes_bloquea_si_el_mes_anterior_sigue_abierto()
    {
        await AbrirComercialAsync(2099, 5, "99");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AbrirComercialAsync(2099, 6, "99"));
        Assert.Contains("PERIODO_ANTERIOR_ABIERTO", ex.MessageText);
    }

    [Fact]
    public async Task Periodo_comercial_cerrado_no_se_reabre()
    {
        var periodoId = await AbrirComercialAsync(2099, 8, "99");
        var cicloId = await CicloIdAsync(periodoId, "99");
        await CerrarCicloAsync(cicloId);
        await CerrarMesAsync(periodoId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AbrirComercialAsync(2099, 8, "99"));
        Assert.Contains("PERIODO_CERRADO", ex.MessageText);
    }

    [Fact]
    public async Task Espejo_elimina_la_fila_legacy_al_borrar_el_ciclo()
    {
        var periodoId = await AbrirComercialAsync(2099, 10, "99");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            DELETE FROM public.adm_periodo_comercial_ciclo
            WHERE company_id = @CompanyId AND periodo_comercial_id = @PeriodoId",
            new { CompanyId, PeriodoId = periodoId }, Transaction));

        var existe = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM public.historialmes WHERE ano = 2099 AND mes = 10 AND ciclo = '99')",
            transaction: Transaction));
        Assert.False(existe);
    }

    // ── Regresión del motor tarifario (riesgo mayor de la fase) ────────────

    [SkippableFact]
    public async Task Tarifario_bloquea_lectura_de_mes_con_ciclo_cerrado_en_el_modelo_nuevo()
    {
        // Cliente activo con histórico en un mes cuyo ciclo quedó CERRADO tras
        // la migración (el legacy trataba cerrado='C' como abierto; F7 bloquea).
        var candidato = await Connection.QueryFirstOrDefaultAsync<(long clienteId, int anio, int mes)?>(new CommandDefinition(@"
            SELECT cm.maestro_cliente_id, hm.ano, hm.mes
            FROM public.historicomedicion hm
            JOIN public.cliente_maestro cm
              ON cm.company_id = hm.company_id AND cm.maestro_cliente_clave = hm.clave AND cm.estado = true
            JOIN public.adm_periodo_comercial p
              ON p.company_id = hm.company_id AND p.anio = hm.ano AND p.mes = hm.mes
            JOIN public.adm_periodo_comercial_ciclo pc
              ON pc.company_id = p.company_id AND pc.periodo_comercial_id = p.periodo_comercial_id
             AND (pc.ciclo_codigo = btrim(hm.ciclo)
                  OR (pc.ciclo_codigo ~ '^[0-9]+$' AND btrim(hm.ciclo) ~ '^[0-9]+$'
                      AND pc.ciclo_codigo::int = btrim(hm.ciclo)::int))
            WHERE hm.company_id = @CompanyId
              AND pc.status_id = 2
            LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(candidato is null, "No hay lecturas de un mes con ciclo cerrado en la BD de pruebas.");

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_adm_calcular_factura_lectura(
                    @CompanyId, @Anio, @Mes, @ClienteId)",
                new
                {
                    CompanyId,
                    Anio = candidato.Value.anio,
                    Mes = candidato.Value.mes,
                    ClienteId = candidato.Value.clienteId
                },
                Transaction)));

        Assert.Contains("No hay periodo abierto", ex.MessageText);
    }

    // ── Cierre contable con checklist ──────────────────────────────────────

    [Fact]
    public async Task Flujo_contable_precierre_reabrir_cierre_crea_el_periodo_siguiente()
    {
        // Cola de pendientes en cero para este escenario (dentro del rollback)
        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_partida_pendiente SET status_id = 3
            WHERE company_id = @CompanyId AND status_id = 1",
            new { CompanyId }, Transaction));

        var periodId = await InsertarPeriodoContableAsync("F7T901", new DateTime(2099, 1, 1), new DateTime(2099, 1, 31));

        var fallas = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.fn_con_checklist_cierre_periodo(@CompanyId, @PeriodId) c WHERE NOT c.ok",
            new { CompanyId, PeriodId = periodId }, Transaction));
        Assert.Equal(0, fallas);

        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT public.sp_con_periodo_precerrar(@CompanyId, @PeriodId, 'test-f7')",
            new { CompanyId, PeriodId = periodId }, Transaction));

        var status = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT status_id FROM public.con_periodo_contable WHERE company_id = @CompanyId AND period_id = @PeriodId",
            new { CompanyId, PeriodId = periodId }, Transaction));
        Assert.Equal((short)1, status);

        // Reapertura solo desde precierre
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT public.sp_con_periodo_reabrir(@CompanyId, @PeriodId, 'test-f7')",
            new { CompanyId, PeriodId = periodId }, Transaction));
        status = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT status_id FROM public.con_periodo_contable WHERE company_id = @CompanyId AND period_id = @PeriodId",
            new { CompanyId, PeriodId = periodId }, Transaction));
        Assert.Equal((short)0, status);

        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT public.sp_con_periodo_precerrar(@CompanyId, @PeriodId, 'test-f7')",
            new { CompanyId, PeriodId = periodId }, Transaction));

        var siguienteId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT public.sp_con_periodo_cerrar(@CompanyId, @PeriodId, 'test-f7')",
            new { CompanyId, PeriodId = periodId }, Transaction));

        Assert.NotNull(siguienteId);
        var siguiente = await Connection.QueryFirstAsync<(string code, short? statusId)>(new CommandDefinition(@"
            SELECT code, status_id FROM public.con_periodo_contable
            WHERE company_id = @CompanyId AND period_id = @SiguienteId",
            new { CompanyId, SiguienteId = siguienteId }, Transaction));
        Assert.Equal("209902", siguiente.code);
        Assert.Equal((short)0, siguiente.statusId);

        var cerrado = await Connection.QueryFirstAsync<(short? statusId, string closedBy)>(new CommandDefinition(@"
            SELECT status_id, closed_by FROM public.con_periodo_contable
            WHERE company_id = @CompanyId AND period_id = @PeriodId",
            new { CompanyId, PeriodId = periodId }, Transaction));
        Assert.Equal((short)2, cerrado.statusId);
        Assert.Equal("test-f7", cerrado.closedBy);
    }

    [Fact]
    public async Task Precierre_contable_bloquea_por_cola_de_pendientes()
    {
        var periodId = await InsertarPeriodoContableAsync("F7T903", new DateTime(2099, 3, 1), new DateTime(2099, 3, 31));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_partida_pendiente
                (company_id, module, origen_tipo, origen_id, fecha_documento, descripcion, payload, created_by)
            VALUES (@CompanyId, 'VENTAS', 'LOTE_FACTURACION', NULL, '2099-03-15',
                    'pendiente test F7', '{}'::jsonb, 'test-f7')",
            new { CompanyId }, Transaction));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(
                "SELECT public.sp_con_periodo_precerrar(@CompanyId, @PeriodId, 'test-f7')",
                new { CompanyId, PeriodId = periodId }, Transaction)));

        Assert.Contains("CHECKLIST_CIERRE_PERIODO", ex.MessageText);
        Assert.Contains("PENDIENTES_COLA", ex.MessageText);
    }

    [Fact]
    public async Task Cierre_contable_directo_desde_abierto_esta_bloqueado()
    {
        var periodId = await InsertarPeriodoContableAsync("F7T905", new DateTime(2099, 5, 1), new DateTime(2099, 5, 31));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(
                "SELECT public.sp_con_periodo_cerrar(@CompanyId, @PeriodId, 'test-f7')",
                new { CompanyId, PeriodId = periodId }, Transaction)));

        Assert.Contains("ESTADO_INVALIDO", ex.MessageText);
    }

    // ── Avisos ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Avisos_detectan_mes_vencido_y_desfase_comercial_contable()
    {
        // Config con tolerancia mínima de desfase
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_integracion_config (company_id, created_by)
            VALUES (@CompanyId, 'test-f7')
            ON CONFLICT (company_id) DO NOTHING;
            UPDATE public.con_integracion_config SET desfase_max_meses = 1 WHERE company_id = @CompanyId;",
            new { CompanyId }, Transaction));

        // Mes comercial viejísimo abierto (vencido) + otro futuro (desfase) y
        // un período contable abierto como contraparte.
        await AbrirComercialAsync(2001, 1, "99");
        await AbrirComercialAsync(2099, 12, "99");
        await InsertarPeriodoContableAsync("F7T9AV", new DateTime(2098, 1, 1), new DateTime(2098, 1, 31));

        var avisos = (await Connection.QueryAsync<(string tipo, string severidad, string mensaje, long cantidad)>(
            new CommandDefinition(
                "SELECT tipo, severidad, mensaje, cantidad FROM public.fn_adm_avisos_periodos(@CompanyId)",
                new { CompanyId }, Transaction))).ToList();

        Assert.Contains(avisos, a => a.tipo == "COMERCIAL_VENCIDO");
        Assert.Contains(avisos, a => a.tipo == "DESFASE_COMERCIAL_CONTABLE");
    }
}
