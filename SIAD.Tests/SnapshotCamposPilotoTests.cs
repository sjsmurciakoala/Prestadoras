using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Campos que el ticket fiscal y la factura necesitan, embebidos en el snapshot
/// offline (snapshot-campos-piloto, 2026-07-07): encabezado del emisor (cfg_company),
/// RTN del abonado (cliente_maestro.maestro_cliente_rtn) y fecha de la lectura anterior
/// del medidor (historicomedicion.fecha_lect_ant). Todos MULTITENANT por p_company_id.
///
/// Construye sobre el PR #12 (tramos CM) y el PR #13 (bloque mora): son campos aditivos,
/// no cambian el contrato (OFFLINE_SNAPSHOT_V3_2), la app los detecta por presencia.
/// </summary>
[Collection("Postgres")]
public sealed class SnapshotCamposPilotoTests : IntegrationTestBase
{
    private const long ClienteMedidoId = 102838;  // piloto con medidor (company 2)
    private const int Anio = 2026;
    private const int Mes = 6;                     // sin histórico → fecha_lectura_anterior null (igual que lect_ant=0)

    public SnapshotCamposPilotoTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Emisor(string? nombre, string? nombre_comercial, string? rtn,
                                 string? direccion, string? telefono, string? email);
    private sealed record CfgCompanyRow(string? legal_name, string? commercial_name, string? code,
                                        string? tax_id, string? address, string? phone, string? email);

    private Task<bool> ClienteExisteAsync(long clienteId) =>
        Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli AND estado=true)",
            new { c = CompanyId, cli = clienteId }, Transaction));

    [SkippableFact]
    public async Task Emisor_coincide_con_cfg_company_de_la_empresa()
    {
        Skip.IfNot(await ClienteExisteAsync(ClienteMedidoId), "Falta el cliente piloto en esta BD.");

        var cfg = await Connection.QueryFirstOrDefaultAsync<CfgCompanyRow>(new CommandDefinition(
            "SELECT legal_name, commercial_name, code, tax_id, address, phone, email FROM public.cfg_company WHERE company_id=@c",
            new { c = CompanyId }, Transaction));
        Skip.If(cfg is null, "Falta cfg_company para la empresa de prueba.");

        var emisor = await Connection.QueryFirstAsync<Emisor>(new CommandDefinition(@"
            SELECT
                snapshot_json->'emisor'->>'nombre'           AS nombre,
                snapshot_json->'emisor'->>'nombre_comercial' AS nombre_comercial,
                snapshot_json->'emisor'->>'rtn'              AS rtn,
                snapshot_json->'emisor'->>'direccion'        AS direccion,
                snapshot_json->'emisor'->>'telefono'         AS telefono,
                snapshot_json->'emisor'->>'email'            AS email
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteMedidoId, anio = Anio, mes = Mes }, Transaction));

        // 'nombre' con la misma prioridad que el encabezado de reportes del portal:
        // legal_name → commercial_name → code (→ 'EMPRESA' si todo vacío).
        var nombreEsperado = FirstNonEmpty(cfg!.legal_name, cfg.commercial_name, cfg.code) ?? "EMPRESA";
        Assert.Equal(nombreEsperado, emisor.nombre);
        Assert.Equal(Trim(cfg.tax_id), emisor.rtn);                     // RTN del emisor (lo que imprime el ticket)
        Assert.Equal(Trim(cfg.address), emisor.direccion);              // dirección del emisor
        Assert.Equal(Trim(cfg.commercial_name), emisor.nombre_comercial);
        Assert.Equal(Trim(cfg.phone), emisor.telefono);
        Assert.Equal(Trim(cfg.email), emisor.email);
    }

    [SkippableFact]
    public async Task Cliente_rtn_coincide_con_cliente_maestro()
    {
        Skip.IfNot(await ClienteExisteAsync(ClienteMedidoId), "Falta el cliente piloto en esta BD.");

        var rtnMaestro = await Connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT NULLIF(BTRIM(COALESCE(maestro_cliente_rtn,'')),'') FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli",
            new { c = CompanyId, cli = ClienteMedidoId }, Transaction));

        var rtnSnapshot = await Connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT snapshot_json->>'cliente_rtn'
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteMedidoId, anio = Anio, mes = Mes }, Transaction));

        Assert.Equal(rtnMaestro, rtnSnapshot);
    }

    [SkippableFact]
    public async Task Fecha_lectura_anterior_fluye_del_historico_de_la_empresa()
    {
        Skip.IfNot(await ClienteExisteAsync(ClienteMedidoId), "Falta el cliente piloto en esta BD.");

        var clave = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT maestro_cliente_clave FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli",
            new { c = CompanyId, cli = ClienteMedidoId }, Transaction));

        // Histórico controlado para el período (rollback al final de la transacción del test).
        var fechaEsperada = new DateTime(2026, 4, 15);
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.historicomedicion (ano, mes, clave, ciclo, fecha_lect_ant, lect_ant, company_id, condicion_id)
            VALUES (@anio, @mes, @clave, '02', @fecha, 123, @c, 0)",
            new { anio = 2026, mes = 5, clave, fecha = fechaEsperada, c = CompanyId }, Transaction));

        var fecha = await Connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(@"
            SELECT (snapshot_json->>'fecha_lectura_anterior')::date
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteMedidoId, anio = 2026, mes = 5 }, Transaction));

        Assert.Equal(fechaEsperada.Date, fecha);
    }

    [SkippableFact]
    public async Task Historico_de_otra_empresa_no_contamina_la_fecha_A6()
    {
        Skip.IfNot(await ClienteExisteAsync(ClienteMedidoId), "Falta el cliente piloto en esta BD.");

        var clave = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT maestro_cliente_clave FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli",
            new { c = CompanyId, cli = ClienteMedidoId }, Transaction));

        // Empresa sintética con la MISMA clave y un histórico del mismo período; el ide
        // identity queda más alto (sería el "último" si el SP no filtrara por company_id).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (9999, 'X999', 'Otra', 'Otra Empresa', 'RTN-X', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING", Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.historicomedicion (ano, mes, clave, ciclo, fecha_lect_ant, lect_ant, company_id, condicion_id)
            VALUES (2026, 5, @clave, '02', DATE '1999-01-01', 999, 9999, 0)",
            new { clave }, Transaction));

        var fecha = await Connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(@"
            SELECT (snapshot_json->>'fecha_lectura_anterior')::date
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, 2026, 5, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteMedidoId }, Transaction));

        // La empresa de prueba no tiene histórico propio para el período → null, NUNCA la fecha de la otra empresa.
        Assert.Null(fecha);
    }

    [SkippableFact]
    public async Task Campos_nuevos_presentes_y_contrato_v3_2_intacto()
    {
        Skip.IfNot(await ClienteExisteAsync(ClienteMedidoId), "Falta el cliente piloto en esta BD.");

        var faltantes = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT (CASE WHEN snapshot_json ? 'emisor'                 THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'emisor' ? 'nombre'       THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'emisor' ? 'rtn'          THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'emisor' ? 'direccion'    THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json ? 'cliente_rtn'            THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json ? 'fecha_lectura_anterior' THEN 0 ELSE 1 END)
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteMedidoId, anio = Anio, mes = Mes }, Transaction));
        Assert.Equal(0, faltantes);

        var version = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(@"
            SELECT snapshot_json->>'contract_version'
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteMedidoId, anio = Anio, mes = Mes }, Transaction));
        Assert.Equal("OFFLINE_SNAPSHOT_V3_2", version);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
