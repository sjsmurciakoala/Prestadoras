using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Regla de vigencia de transaccion_abonado (fix 2026-07-16). La convención de
/// estado está invertida entre módulos: facturación V3 marca vigente = 'A', pero
/// caja/posteos/WS bancario graban el abono vigente con 'C' y al anular/reversar
/// ponen 'A'. vw_transaccion_abonado_vigente excluye SOLO lo muerto: 'N'
/// (anulada), 'R' (reversado legacy), 'P' (recibo pendiente) y los pagos 201/202
/// con 'A' (anulados por caja/WS). Todo lo demás cuenta, incluido el traslado
/// 'PLAN' con 'C' de los planes de pago (crédito que compensa las cuotas).
///
/// F4 (2026-07-28): sp_obtener_cliente_saldo YA NO suma la vista completa —
/// pasa a documentos pendientes + residuo migrado (ver SaldoDocumentosTests).
/// La regla de vigencia sigue gobernando la VISTA (fuente del residuo y de los
/// lectores legacy hasta F7), así que estos tests asertan la vista directa.
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

    // H4: la tabla está congelada (candado por sesión) y el trigger de
    // sincronía de F1 se retiró. La siembra emula filas de la era dual-write,
    // que SÍ llevan los ids estampados: se derivan aquí igual que lo hacía el
    // trigger (solo pagos 201/202: C→1 aplicado, P→2 pendiente, A→3 anulado).
    private Task InsertarMovimientoAsync(string tipotransaccion, string estado, decimal debitos, decimal creditos)
    {
        short? estadoPagoId = tipotransaccion is "201" or "202"
            ? estado switch { "C" => (short)1, "P" => (short)2, "A" => (short)3, _ => null }
            : null;

        return Connection.ExecuteAsync(new CommandDefinition(@"
            SET LOCAL siad.permitir_escritura_legacy = 'on';
            INSERT INTO public.transaccion_abonado (company_id, cliente_clave, tipotransaccion, estado, debitos, creditos, estado_pago_id)
            VALUES (@companyId, @clave, @tipo, @estado, @debitos, @creditos, @estadoPagoId)",
            new { companyId = EmpresaSintetica, clave = Clave, tipo = tipotransaccion, estado, debitos, creditos, estadoPagoId },
            Transaction));
    }

    // F4: la regla de vigencia se aserta sobre la vista (los tests del SP de
    // saldo por documentos viven en SaldoDocumentosTests).
    private Task<decimal?> SaldoAsync() =>
        Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(@"
            SELECT COALESCE(SUM(COALESCE(debitos,0) - COALESCE(creditos,0)), 0)
            FROM public.vw_transaccion_abonado_vigente
            WHERE company_id = @companyId AND cliente_clave = @clave",
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

    // F7 H4 (2026-07-30): los tres tests "Trigger_deriva_*" murieron con el
    // trigger de sincronía de F1. Desde el congelamiento, lo que se fija es lo
    // contrario: la tabla NO acepta escritura de operación, y lo poco que entra
    // (migración) entra tal cual, sin estampado alguno.

    [SkippableFact]
    public async Task Congelada_h4_rechaza_escritura_directa()
    {
        await PrepararEmpresaAsync();

        // Sin el permiso explícito de migración, el candado revienta cualquier
        // escritura — incluida la de un superusuario, que el REVOKE no alcanza.
        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.transaccion_abonado
                    (company_id, cliente_clave, tipotransaccion, estado, debitos, creditos)
                VALUES (@companyId, @clave, '202', 'C', 0, 50)",
                new { companyId = EmpresaSintetica, clave = Clave }, Transaction)));

        Assert.Contains("CONGELADA", ex.MessageText);
    }

    [SkippableFact]
    public async Task Congelada_h4_la_migracion_entra_sin_trigger_de_espejo()
    {
        await PrepararEmpresaAsync();

        // Con el permiso de migración la fila entra tal cual: el trigger de
        // sincronía ya no existe, así que nada deriva ids desde las letras.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            SET LOCAL siad.permitir_escritura_legacy = 'on';
            INSERT INTO public.transaccion_abonado
                (company_id, cliente_clave, tipotransaccion, estado, debitos, creditos)
            VALUES (@companyId, @clave, '202', 'C', 0, 50)",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));

        var (tipoId, estadoPagoId, _) = await UltimoEspejoAsync();
        Assert.Null(tipoId);
        Assert.Null(estadoPagoId);
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
