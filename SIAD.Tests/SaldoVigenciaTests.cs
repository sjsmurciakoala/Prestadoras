using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Regla de vigencia de transaccion_abonado (fix 2026-07-16). La convención de
/// estado está invertida entre módulos: facturación V3 marca vigente = 'A', pero
/// caja/posteos/WS bancario graban el abono vigente con 'C' y al anular/reversar
/// ponen 'A'. sp_obtener_cliente_saldo suma (debitos - creditos) de los movimientos
/// de vw_transaccion_abonado_vigente, que excluye SOLO lo muerto: 'N' (anulada),
/// 'R' (reversado legacy), 'P' (recibo pendiente) y los pagos 201/202 con 'A'
/// (anulados por caja/WS). Todo lo demás cuenta, incluido el traslado 'PLAN' con
/// 'C' de los planes de pago (crédito que compensa las cuotas PLAN-CUOTA).
/// </summary>
[Collection("Postgres")]
public sealed class SaldoVigenciaTests : IntegrationTestBase
{
    private const long EmpresaSintetica = 9998;   // rollback al final del test
    private const string Clave = "VIGENCIA-01";

    public SaldoVigenciaTests(PostgresFixture fixture) : base(fixture) { }

    private async Task PrepararEmpresaAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X998', 'Vigencia', 'Empresa Vigencia', 'RTN-V', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING",
            new { id = EmpresaSintetica }, Transaction));
    }

    private Task InsertarMovimientoAsync(string tipotransaccion, string estado, decimal debitos, decimal creditos) =>
        Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.transaccion_abonado (company_id, cliente_clave, tipotransaccion, estado, debitos, creditos)
            VALUES (@companyId, @clave, @tipo, @estado, @debitos, @creditos)",
            new { companyId = EmpresaSintetica, clave = Clave, tipo = tipotransaccion, estado, debitos, creditos },
            Transaction));

    private Task<decimal?> SaldoAsync() =>
        Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@companyId, @clave)",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));

    [SkippableFact]
    public async Task Abono_vigente_resta_y_reversado_pendiente_no()
    {
        await PrepararEmpresaAsync();

        // Dos cargos de factura (convención facturación: vigente = 'A').
        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 100m, 0m);
        await InsertarMovimientoAsync("ALCANTARILLADO", "A", 100m, 0m);

        // Abono vigente (convención caja: vigente = 'C').
        await InsertarMovimientoAsync("202", "C", 0m, 50m);

        // Abono reversado (caja marca 'A' al anular) — NO debe restar.
        await InsertarMovimientoAsync("202", "A", 0m, 30m);

        // Recibo pendiente de pago — NO debe restar.
        await InsertarMovimientoAsync("202", "P", 0m, 20m);

        var saldo = await SaldoAsync();

        Assert.Equal(150m, saldo); // 200 facturado − 50 abonado
    }

    [SkippableFact]
    public async Task Factura_anulada_no_suma()
    {
        await PrepararEmpresaAsync();

        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 100m, 0m);
        await InsertarMovimientoAsync("AGUA_POTABLE", "N", 75m, 0m);   // anulada V3
        await InsertarMovimientoAsync("AGUA_POTABLE", "R", 60m, 0m);   // reversada legacy

        var saldo = await SaldoAsync();

        Assert.Equal(100m, saldo);
    }

    [SkippableFact]
    public async Task Cliente_sin_movimientos_devuelve_cero()
    {
        await PrepararEmpresaAsync();

        var saldo = await SaldoAsync();

        Assert.Equal(0m, saldo);
    }

    [SkippableFact]
    public async Task Plan_de_pago_traslado_C_compensa_las_cuotas()
    {
        await PrepararEmpresaAsync();

        // Deuda previa + facturas del mes.
        await InsertarMovimientoAsync("SALDO_ANTERIOR", "A", 550.84m, 0m);
        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 171.94m, 0m);

        // Plan de pago (CobranzaService): traslado 'PLAN' con estado 'C' (crédito)
        // + cuotas 'PLAN-CUOTA' con estado 'A' (débitos por el mismo total).
        await InsertarMovimientoAsync("PLAN", "C", 0m, 171.94m);
        await InsertarMovimientoAsync("PLAN-CUOTA", "A", 57.31m, 0m);
        await InsertarMovimientoAsync("PLAN-CUOTA", "A", 57.31m, 0m);
        await InsertarMovimientoAsync("PLAN-CUOTA", "A", 57.32m, 0m);

        var saldo = await SaldoAsync();

        Assert.Equal(722.78m, saldo); // 550.84 + 171.94: el plan es neutro (traslado = cuotas)
    }

    [SkippableFact]
    public async Task Pago_migrado_de_simafi_con_estado_A_si_resta()
    {
        await PrepararEmpresaAsync();

        await InsertarMovimientoAsync("SALDO_ANTERIOR", "A", 500m, 0m);
        await InsertarMovimientoAsync("PAGO", "A", 0m, 200m); // migrado legacy (no es 201/202)

        var saldo = await SaldoAsync();

        Assert.Equal(300m, saldo);
    }

    // ------------------------------------------------------------------------
    // Unificación cobranza F1 (2026-07-26): espejos numéricos derivados por
    // trigger (tipo_transaccion_id, estado_pago_id) y vista de vigencia sobre
    // adm_estado_pago. La semántica del saldo NO cambia (tests de arriba).
    // ------------------------------------------------------------------------

    private Task<(short? tipoId, short? estadoPagoId, short? estadoId)> UltimoEspejoAsync() =>
        Connection.QuerySingleAsync<(short?, short?, short?)>(new CommandDefinition(@"
            SELECT tipo_transaccion_id, estado_pago_id, estado_id
            FROM public.transaccion_abonado
            WHERE company_id = @companyId AND cliente_clave = @clave
            ORDER BY ide DESC LIMIT 1",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));

    [SkippableFact]
    public async Task Trigger_deriva_espejos_de_abono_de_caja()
    {
        await PrepararEmpresaAsync();

        await InsertarMovimientoAsync("202", "C", 0m, 50m);
        var (tipoId, estadoPagoId, _) = await UltimoEspejoAsync();

        Assert.Equal((short)4, tipoId);       // ABONO (202 caja, sin marker WS)
        Assert.Equal((short)1, estadoPagoId); // APLICADO
    }

    [SkippableFact]
    public async Task Trigger_distingue_anulado_de_caja_y_reversado_del_ws()
    {
        await PrepararEmpresaAsync();

        // Anulación desde caja: 202 sin marker pasa a 'A' → ANULADO(3)
        await InsertarMovimientoAsync("202", "C", 0m, 30m);
        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.transaccion_abonado SET estado = 'A'
            WHERE company_id = @companyId AND cliente_clave = @clave",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));
        var (_, anulado, _) = await UltimoEspejoAsync();
        Assert.Equal((short)3, anulado);

        // Pago WS (marker WSBANCO:) → tipo PAGO_BANCO(3); su reverso → REVERSADO(4)
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.transaccion_abonado
                (company_id, cliente_clave, tipotransaccion, estado, debitos, creditos, trans_aplicar)
            VALUES (@companyId, @clave, '202', 'C', 0, 40, 'WSBANCO:777')",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));
        var (tipoWs, aplicadoWs, _) = await UltimoEspejoAsync();
        Assert.Equal((short)3, tipoWs);
        Assert.Equal((short)1, aplicadoWs);

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.transaccion_abonado SET estado = 'A'
            WHERE company_id = @companyId AND cliente_clave = @clave
              AND trans_aplicar LIKE 'WSBANCO:%'",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));
        var (_, reversadoWs, _) = await UltimoEspejoAsync();
        Assert.Equal((short)4, reversadoWs);
    }

    [SkippableFact]
    public async Task Trigger_deriva_espejos_de_cargos_y_planes_sin_estado_pago()
    {
        await PrepararEmpresaAsync();

        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 100m, 0m);
        var (tipoCargo, pagoCargo, estadoCargo) = await UltimoEspejoAsync();
        Assert.Equal((short)1, tipoCargo);   // CARGO_SERVICIO
        Assert.Null(pagoCargo);              // no es pago
        Assert.Equal((short)1, estadoCargo); // Activa

        await InsertarMovimientoAsync("PLAN-CUOTA", "A", 57.31m, 0m);
        var (tipoCuota, pagoCuota, _) = await UltimoEspejoAsync();
        Assert.Equal((short)9, tipoCuota);   // PLAN_CUOTA
        Assert.Null(pagoCuota);

        await InsertarMovimientoAsync("SALDO_ANTERIOR", "A", 500m, 0m);
        var (tipoSaldo, _, _) = await UltimoEspejoAsync();
        Assert.Equal((short)11, tipoSaldo);  // SALDO_INICIAL
    }

    [SkippableFact]
    public async Task Vista_de_vigencia_gobierna_pagos_por_estado_pago_id()
    {
        await PrepararEmpresaAsync();

        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 200m, 0m);
        await InsertarMovimientoAsync("202", "C", 0m, 50m);  // APLICADO → cuenta
        await InsertarMovimientoAsync("202", "P", 0m, 20m);  // PENDIENTE → fuera
        await InsertarMovimientoAsync("202", "A", 0m, 30m);  // ANULADO → fuera

        var vigentes = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.vw_transaccion_abonado_vigente
            WHERE company_id = @companyId AND cliente_clave = @clave
              AND tipotransaccion = '202'",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));
        Assert.Equal(1L, vigentes);

        var estadoPagoVigente = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            SELECT estado_pago_id FROM public.vw_transaccion_abonado_vigente
            WHERE company_id = @companyId AND cliente_clave = @clave
              AND tipotransaccion = '202'",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));
        Assert.Equal((short)1, estadoPagoVigente);
    }

    [SkippableFact]
    public async Task Catalogos_f1_sembrados_y_factura_B_tiene_estado_id_4()
    {
        await PrepararEmpresaAsync();

        var codigoB = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT codigo FROM public.cfg_estado_documento_comercial WHERE estado_id = 4",
            transaction: Transaction));
        Assert.Equal("B", codigoB);

        var tipos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.adm_tipo_transaccion", transaction: Transaction));
        Assert.Equal(11L, tipos);

        var estadosPago = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.adm_estado_pago", transaction: Transaction));
        Assert.Equal(4L, estadosPago);

        var estadoId = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
            VALUES (@companyId, 'F1-B-TEST', @clave, 'F', '2026', '7', current_date, 'B', 'S', 1)
            RETURNING estado_id",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));
        Assert.Equal((short)4, estadoId);
    }
}
