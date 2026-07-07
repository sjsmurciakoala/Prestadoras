using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Paridad de la MORA entre el snapshot offline y el motor online (mora-en-snapshot,
/// 2026-07-06). La app de lectores calcula la factura offline; el snapshot debe llevar
/// todo lo necesario para reproducir EXACTO el campo `recargos` de
/// sp_adm_calcular_factura_lectura. Se agrega el bloque top-level "mora" al snapshot_json.
///
/// Construye sobre el PR #12 (snapshot con tramos CM): estos tests conviven con
/// LecturaV3MedidosTests sin cambiar comportamiento.
/// </summary>
[Collection("Postgres")]
public sealed class SnapshotMoraTests : IntegrationTestBase
{
    private const long ClienteConSaldoId = 102814;  // cliente piloto con saldo > 0 (662.22)
    private const int Anio = 2026;
    private const int Mes = 6;                       // sin histórico → lect_ant=0 (igual que los fixtures L8)
    private const decimal TasaMora = 0.001667m;      // fracción mensual de referencia (Art. 130)

    public SnapshotMoraTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record MoraBloque(bool present, bool? activo, decimal? tasa_mensual, decimal? @base, decimal? recargo);

    private Task<MoraBloque> LeerMoraAsync(long clienteId)
        => Connection.QueryFirstAsync<MoraBloque>(new CommandDefinition(@"
            SELECT
                snapshot_json ? 'mora'                              AS present,
                (snapshot_json->'mora'->>'activo')::boolean         AS activo,
                (snapshot_json->'mora'->>'tasa_mensual')::numeric   AS tasa_mensual,
                (snapshot_json->'mora'->>'base')::numeric           AS base,
                (snapshot_json->'mora'->>'recargo')::numeric        AS recargo
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = clienteId, anio = Anio, mes = Mes }, Transaction));

    private Task<decimal> RecargoOnlineAsync(long clienteId)
        => Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT recargos
            FROM public.sp_adm_calcular_factura_lectura(
                p_company_id := @c, p_anio := @anio, p_mes := @mes, p_cliente_id := @cli,
                p_contador := NULL, p_fecha_lectura := CURRENT_DATE,
                p_lectura_actual := NULL, p_condicion_lectura := 'N')",
            new { c = CompanyId, anio = Anio, mes = Mes, cli = clienteId }, Transaction));

    private async Task<bool> ClienteTieneRowMoraYSaldoAsync()
    {
        var hayConfig = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM public.cfg_recargo_mora WHERE company_id=@c)",
            new { c = CompanyId }, Transaction));
        if (!hayConfig) return false;

        var existeCliente = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli AND estado=true)",
            new { c = CompanyId, cli = ClienteConSaldoId }, Transaction));
        return existeCliente;
    }

    [SkippableFact]
    public async Task Recargo_del_snapshot_coincide_EXACTO_con_recargos_del_calcular_con_mora_activa()
    {
        Skip.IfNot(await ClienteTieneRowMoraYSaldoAsync(),
            "Falta cfg_recargo_mora o el cliente piloto con saldo en esta BD.");

        // La empresa activa la mora con una tasa conocida (dentro de la transacción con ROLLBACK).
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.cfg_recargo_mora SET activo=true, tasa_mensual=@t WHERE company_id=@c",
            new { c = CompanyId, t = TasaMora }, Transaction));

        var mora = await LeerMoraAsync(ClienteConSaldoId);
        Skip.If((mora.@base ?? 0m) <= 0m, "El cliente piloto no tiene saldo previo > 0 en esta BD.");

        var recargoOnline = await RecargoOnlineAsync(ClienteConSaldoId);

        // 1) El recargo autoritativo del snapshot == recargos del motor online, EXACTO.
        Assert.True(mora.present);
        Assert.True(mora.activo);
        Assert.Equal(recargoOnline, mora.recargo);

        // 2) El recargo que la app calcularía DESDE el bloque (base * tasa_mensual) también coincide.
        var recargoApp = Math.Round(mora.@base!.Value * mora.tasa_mensual!.Value, 4);
        Assert.Equal(recargoOnline, recargoApp);

        // 3) Y es un recargo real (> 0) para este cliente con saldo, no un falso verde.
        Assert.True(recargoOnline > 0m, "Se esperaba un recargo > 0 para un cliente con saldo y mora activa.");
    }

    [SkippableFact]
    public async Task Mora_inactiva_bloque_presente_activo_false_recargo_cero()
    {
        Skip.IfNot(await ClienteTieneRowMoraYSaldoAsync(),
            "Falta cfg_recargo_mora o el cliente piloto en esta BD.");

        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.cfg_recargo_mora SET activo=false WHERE company_id=@c",
            new { c = CompanyId }, Transaction));

        var mora = await LeerMoraAsync(ClienteConSaldoId);

        // El bloque SIEMPRE presente: la app distingue "no aplica" (activo:false) de
        // "no vino el dato" (bloque ausente).
        Assert.True(mora.present, "El bloque 'mora' debe estar presente aun con mora inactiva.");
        Assert.False(mora.activo);
        Assert.Equal(0m, mora.recargo);

        // Y el motor online también factura recargo 0 con la mora inactiva → paridad.
        var recargoOnline = await RecargoOnlineAsync(ClienteConSaldoId);
        Assert.Equal(0m, recargoOnline);
        Assert.Equal(recargoOnline, mora.recargo);
    }

    [SkippableFact]
    public async Task Bloque_mora_tiene_la_forma_documentada()
    {
        Skip.IfNot(await ClienteTieneRowMoraYSaldoAsync(),
            "Falta cfg_recargo_mora o el cliente piloto en esta BD.");

        var camposFaltantes = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT (CASE WHEN snapshot_json->'mora' ? 'activo'       THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'mora' ? 'tasa_mensual' THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'mora' ? 'dias_gracia'  THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'mora' ? 'base'         THEN 0 ELSE 1 END
                  + CASE WHEN snapshot_json->'mora' ? 'recargo'      THEN 0 ELSE 1 END)
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClienteConSaldoId, anio = Anio, mes = Mes }, Transaction));

        Assert.Equal(0, camposFaltantes);
    }
}
