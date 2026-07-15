using System.Text.Json;
using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase B del plan apertura-ciclo-único (2026-07-15): apertura única e integral
// de ciclo. Verifica sp_adm_periodo_ciclo_abrir (secuencia, planilla roll-over
// y desde clientes, idempotencia, fecha_limite del calendario, avisos),
// sp_adm_periodo_ciclo_deshacer y fn_adm_periodo_ciclo_preview.
// Se usa un año lejano (2095) y un ciclo sintético ('77') para no chocar con
// los períodos reales del entorno de pruebas; todo corre dentro de la
// transacción del fixture (BEGIN ... ROLLBACK).
[Collection("Postgres")]
public sealed class AperturaCicloIntegralTests : IntegrationTestBase
{
    private const int Anio = 2095;
    private const string Ciclo = "77";

    public AperturaCicloIntegralTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<JsonDocument> AbrirAsync(int anio, int mes, string ciclo)
    {
        var json = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT public.sp_adm_periodo_ciclo_abrir(@CompanyId, @Anio, @Mes, @Ciclo, 'test-apertura')::text",
            new { CompanyId, Anio = anio, Mes = mes, Ciclo = ciclo }, Transaction));
        return JsonDocument.Parse(json!);
    }

    private async Task<long> ContarPlanillaAsync(int anio, int mes, string ciclo)
    {
        return await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            @"SELECT count(*) FROM public.historicomedicion
              WHERE company_id = @CompanyId AND ano = @Anio AND mes = @Mes
                AND public.fn_adm_ciclo_norm(ciclo) = @Ciclo",
            new { CompanyId, Anio = anio, Mes = mes, Ciclo = ciclo }, Transaction));
    }

    private async Task InsertarLecturaPreviaAsync(int anio, int mes, string clave, decimal lectAct)
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO public.historicomedicion
                  (company_id, ano, mes, ciclo, ruta, secuencia, clave, propietario,
                   fecha, usuario, lect_ant, lect_act, consumo, consumoant, contador)
              VALUES (@CompanyId, @Anio, @Mes, @Ciclo, '00T1', '001', @Clave, 'CLIENTE TEST',
                      current_date, 'lector-test', 10, @LectAct, 5, 3, @Contador)",
            new { CompanyId, Anio = anio, Mes = mes, Ciclo, Clave = clave, LectAct = lectAct, Contador = $"MED-{clave}" },
            Transaction));
    }

    [SkippableFact]
    public async Task Abrir_con_rollover_genera_planilla_desde_el_mes_anterior()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        // Mes anterior con 2 lecturas tomadas → la apertura del siguiente hace
        // roll-over: lect_ant = lect_act previa, usuario NULL (pendiente).
        await InsertarLecturaPreviaAsync(Anio, 2, "T-0001", 123);
        await InsertarLecturaPreviaAsync(Anio, 2, "T-0002", 456);

        using var resumen = await AbrirAsync(Anio, 3, Ciclo);
        var root = resumen.RootElement;

        Assert.Equal("ROLL_OVER", root.GetProperty("origen_planilla").GetString());
        Assert.Equal(2, root.GetProperty("clientes_planilla").GetInt64());

        var fila = await Connection.QueryFirstAsync<(decimal? lect_ant, decimal? lect_act, string? usuario)>(
            new CommandDefinition(
                @"SELECT lect_ant, lect_act, usuario FROM public.historicomedicion
                  WHERE company_id = @CompanyId AND ano = @Anio AND mes = 3 AND clave = 'T-0001'",
                new { CompanyId, Anio }, Transaction));

        Assert.Equal(123, fila.lect_ant);
        Assert.Null(fila.lect_act);
        Assert.True(string.IsNullOrEmpty(fila.usuario), "La fila del roll-over debe quedar pendiente (sin usuario).");
    }

    [SkippableFact]
    public async Task Abrir_es_idempotente_no_duplica_planilla()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        await InsertarLecturaPreviaAsync(Anio, 2, "T-0003", 50);

        using var primera = await AbrirAsync(Anio, 3, Ciclo);
        var planillaInicial = await ContarPlanillaAsync(Anio, 3, Ciclo);

        using var segunda = await AbrirAsync(Anio, 3, Ciclo);
        var avisos = segunda.RootElement.GetProperty("avisos").EnumerateArray()
            .Select(a => a.GetString()).ToList();

        Assert.Contains("CICLO_YA_ABIERTO", avisos);
        Assert.Contains("PLANILLA_EXISTENTE", avisos);
        Assert.Equal(planillaInicial, await ContarPlanillaAsync(Anio, 3, Ciclo));
    }

    [SkippableFact]
    public async Task Abrir_exige_el_mes_anterior_cerrado()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        using var marzo = await AbrirAsync(Anio, 3, Ciclo);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => AbrirAsync(Anio, 4, Ciclo));
        Assert.Contains("PERIODO_ANTERIOR_ABIERTO", ex.Message);
    }

    [SkippableFact]
    public async Task Abrir_no_reabre_un_periodo_cerrado()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        using var resumen = await AbrirAsync(Anio, 3, Ciclo);
        var cicloId = resumen.RootElement.GetProperty("periodo_ciclo_id").GetInt64();
        var periodoId = resumen.RootElement.GetProperty("periodo_comercial_id").GetInt64();

        // Cierra ciclo (forzado: el ciclo sintético no tiene rutas) y luego el mes.
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT public.sp_adm_periodo_ciclo_cerrar(@CompanyId, @CicloId, 'test-apertura', true)",
            new { CompanyId, CicloId = cicloId }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT public.sp_adm_periodo_comercial_cerrar(@CompanyId, @PeriodoId, 'test-apertura')",
            new { CompanyId, PeriodoId = periodoId }, Transaction));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => AbrirAsync(Anio, 3, Ciclo));
        Assert.Contains("PERIODO_CERRADO", ex.Message);
    }

    [SkippableFact]
    public async Task Abrir_toma_fecha_limite_del_calendario_y_sin_calendario_avisa()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        // Con fila de calendario: fecha_limite = fechalec y sin aviso.
        await Connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO public.calendariopro (company_id, ano, mes, ciclo, fechalec, fechafac, fechavence, diasvence)
              VALUES (@CompanyId, @Anio, 3, @Ciclo, make_date(@Anio, 3, 9), make_date(@Anio, 3, 9), make_date(@Anio, 3, 14), 5)",
            new { CompanyId, Anio, Ciclo }, Transaction));

        using var conCalendario = await AbrirAsync(Anio, 3, Ciclo);
        var root = conCalendario.RootElement;
        Assert.Equal($"{Anio}-03-09", root.GetProperty("fecha_limite").GetString());
        Assert.DoesNotContain("SIN_CALENDARIO",
            root.GetProperty("avisos").EnumerateArray().Select(a => a.GetString()));

        var fechaLimite = await Connection.ExecuteScalarAsync<DateTime>(new CommandDefinition(
            @"SELECT pc.fecha_limite FROM public.adm_periodo_comercial_ciclo pc
              JOIN public.adm_periodo_comercial p ON p.company_id = pc.company_id
               AND p.periodo_comercial_id = pc.periodo_comercial_id
              WHERE pc.company_id = @CompanyId AND p.anio = @Anio AND p.mes = 3
                AND pc.ciclo_codigo = @Ciclo",
            new { CompanyId, Anio, Ciclo }, Transaction));
        Assert.Equal(new DateTime(Anio, 3, 9), fechaLimite);
    }

    [SkippableFact]
    public async Task Abrir_sin_calendario_usa_fin_de_mes_y_avisa()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        using var resumen = await AbrirAsync(Anio, 3, Ciclo);
        var root = resumen.RootElement;

        Assert.Contains("SIN_CALENDARIO",
            root.GetProperty("avisos").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal($"{Anio}-03-31", root.GetProperty("fecha_limite").GetString());
    }

    [SkippableFact]
    public async Task Deshacer_borra_planilla_ciclo_y_periodo_vacio()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        await InsertarLecturaPreviaAsync(Anio, 2, "T-0004", 77);
        using var resumen = await AbrirAsync(Anio, 3, Ciclo);
        var cicloId = resumen.RootElement.GetProperty("periodo_ciclo_id").GetInt64();

        var json = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT public.sp_adm_periodo_ciclo_deshacer(@CompanyId, @CicloId, 'test-apertura')::text",
            new { CompanyId, CicloId = cicloId }, Transaction));
        using var deshecho = JsonDocument.Parse(json!);

        Assert.Equal(1, deshecho.RootElement.GetProperty("planilla_eliminada").GetInt64());
        Assert.True(deshecho.RootElement.GetProperty("periodo_eliminado").GetBoolean());
        Assert.Equal(0, await ContarPlanillaAsync(Anio, 3, Ciclo));

        var periodoExiste = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT EXISTS (SELECT 1 FROM public.adm_periodo_comercial
              WHERE company_id = @CompanyId AND anio = @Anio AND mes = 3)",
            new { CompanyId, Anio }, Transaction));
        Assert.False(periodoExiste);
    }

    [SkippableFact]
    public async Task Deshacer_se_bloquea_si_hay_lecturas_registradas()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        await InsertarLecturaPreviaAsync(Anio, 2, "T-0005", 88);
        using var resumen = await AbrirAsync(Anio, 3, Ciclo);
        var cicloId = resumen.RootElement.GetProperty("periodo_ciclo_id").GetInt64();

        // Simula una lectura tomada por la app.
        await Connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE public.historicomedicion
              SET usuario = 'lector-app', lect_act = 90
              WHERE company_id = @CompanyId AND ano = @Anio AND mes = 3 AND clave = 'T-0005'",
            new { CompanyId, Anio }, Transaction));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            Connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "SELECT public.sp_adm_periodo_ciclo_deshacer(@CompanyId, @CicloId, 'test-apertura')::text",
                new { CompanyId, CicloId = cicloId }, Transaction)));
        Assert.Contains("LECTURAS_REGISTRADAS", ex.Message);
    }

    [SkippableFact]
    public async Task Preview_no_escribe_nada_y_reporta_bloqueos()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        var json = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT public.fn_adm_periodo_ciclo_preview(@CompanyId, @Anio, 3, @Ciclo)::text",
            new { CompanyId, Anio, Ciclo }, Transaction));
        using var preview = JsonDocument.Parse(json!);

        // Sin período previo abierto en 2095-02 el preview no bloquea.
        Assert.Equal(JsonValueKind.Null, preview.RootElement.GetProperty("bloqueo").ValueKind);

        var periodoExiste = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT EXISTS (SELECT 1 FROM public.adm_periodo_comercial
              WHERE company_id = @CompanyId AND anio = @Anio AND mes = 3)",
            new { CompanyId, Anio }, Transaction));
        Assert.False(periodoExiste, "El preview no debe crear el período.");

        // Y con el mes anterior abierto, reporta el bloqueo de secuencia.
        using var marzo = await AbrirAsync(Anio, 3, Ciclo);
        var json2 = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT public.fn_adm_periodo_ciclo_preview(@CompanyId, @Anio, 4, @Ciclo)::text",
            new { CompanyId, Anio, Ciclo }, Transaction));
        using var bloqueado = JsonDocument.Parse(json2!);
        Assert.Equal("PERIODO_ANTERIOR_ABIERTO", bloqueado.RootElement.GetProperty("bloqueo").GetString());
    }

    [SkippableFact]
    public async Task El_sp_viejo_de_apertura_quedo_retirado()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        var existe = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT EXISTS (
                SELECT 1 FROM pg_proc p
                JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname = 'public' AND p.proname = 'sp_adm_periodo_comercial_abrir')",
            transaction: Transaction));

        Assert.False(existe, "sp_adm_periodo_comercial_abrir (con bypass de secuencia) debía retirarse en la Fase B.");
    }
}
