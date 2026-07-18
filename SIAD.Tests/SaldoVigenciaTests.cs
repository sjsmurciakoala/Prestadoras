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
}
