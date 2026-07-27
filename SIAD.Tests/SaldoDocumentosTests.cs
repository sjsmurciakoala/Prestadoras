using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Unificación cobranza F4 (2026-07-28): sp_obtener_cliente_saldo pasa de sumar
/// la vista vigente a calcular por DOCUMENTOS:
///
///   saldo = SUM(líneas de facturas A/B: montovalor_saldo ?? montovalor)
///         + residuo migrado (vigente SALDO_ANTERIOR / SALDO_INICIAL)
///
/// El residuo es la cartera SIMAFI que aún no existe como documento y muere en
/// F7. Equivalencia auditada contra el saldo legacy: 850/850 clientes exactos
/// en siad_v3_copia09 (diff neta 0.00). sp_obtener_cliente_saldo_servicio_detalle
/// pasa a sumar las líneas pendientes del servicio (la corrida legacy quedaba
/// desactualizada con los pagos del motor).
/// </summary>
[Collection("Postgres")]
public sealed class SaldoDocumentosTests : IntegrationTestBase
{
    private const long EmpresaSintetica = 9997;   // rollback al final del test
    private const string Clave = "DOCS-01";

    public SaldoDocumentosTests(PostgresFixture fixture) : base(fixture) { }

    private async Task PrepararEmpresaAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X997', 'Docs', 'Empresa Saldo Docs', 'RTN-D', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING",
            new { id = EmpresaSintetica }, Transaction));
    }

    private async Task<(int facturaId, int numRecibo)> InsertarFacturaAsync(string estado)
    {
        var row = await Connection.QuerySingleAsync<(int, int)>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
            VALUES (@companyId, 'F4-' || @estado || '-' || clock_timestamp()::text, @clave, 'F',
                '2026', '7', current_date, @estado, 'S', 1)
            RETURNING id, numrecibo",
            new { companyId = EmpresaSintetica, clave = Clave, estado }, Transaction));
        return row;
    }

    private Task InsertarLineaAsync(int facturaId, int numRecibo, string servicio, decimal monto, decimal? saldo) =>
        Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, numrecibo, codigo, tiposervicio, descripcion, montovalor, factura_id, montovalor_saldo)
            VALUES (@companyId, @numRecibo, '', @servicio, @servicio, @monto, @facturaId, @saldo)",
            new { companyId = EmpresaSintetica, numRecibo, servicio, monto, facturaId, saldo }, Transaction));

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

    private Task<decimal?> SaldoServicioAsync(string servicio) =>
        Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT public.sp_obtener_cliente_saldo_servicio_detalle(@companyId, @clave, @servicio)",
            new { companyId = EmpresaSintetica, clave = Clave, servicio }, Transaction));

    [SkippableFact]
    public async Task Saldo_suma_lineas_de_facturas_A_y_B_no_C_ni_N()
    {
        await PrepararEmpresaAsync();

        var (fa, ra) = await InsertarFacturaAsync("A");
        await InsertarLineaAsync(fa, ra, "AGUA_POTABLE", 100m, null);   // sin saldo → cuenta montovalor
        await InsertarLineaAsync(fa, ra, "ALCANTARILLADO", 50m, 20m);   // abonada → cuenta el saldo

        var (fb, rb) = await InsertarFacturaAsync("B");
        await InsertarLineaAsync(fb, rb, "AGUA_POTABLE", 80m, 30m);

        var (fc, rc) = await InsertarFacturaAsync("C");                 // pagada/compensada → fuera
        await InsertarLineaAsync(fc, rc, "AGUA_POTABLE", 999m, 999m);

        var (fn, rn) = await InsertarFacturaAsync("N");                 // anulada → fuera
        await InsertarLineaAsync(fn, rn, "AGUA_POTABLE", 555m, 555m);

        Assert.Equal(150m, await SaldoAsync()); // 100 + 20 + 30
    }

    [SkippableFact]
    public async Task Residuo_migrado_suma_y_movimientos_sueltos_no()
    {
        await PrepararEmpresaAsync();

        // Cartera migrada sin documento: SÍ cuenta (residuo hasta F7).
        await InsertarMovimientoAsync("SALDO_ANTERIOR", "A", 500m, 0m);
        Assert.Equal(500m, await SaldoAsync());

        // Movimientos sueltos de otros tipos ya NO cuentan: la fuente son los
        // documentos (el cargo real de una factura vive en factura_detalle).
        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 999m, 0m);
        await InsertarMovimientoAsync("202", "C", 0m, 100m);
        Assert.Equal(500m, await SaldoAsync());

        // Y el residuo respeta la vigencia: SALDO_ANTERIOR anulado no cuenta.
        await InsertarMovimientoAsync("SALDO_ANTERIOR", "N", 400m, 0m);
        Assert.Equal(500m, await SaldoAsync());
    }

    [SkippableFact]
    public async Task Pago_del_motor_rebaja_el_saldo_via_lineas()
    {
        await PrepararEmpresaAsync();

        var (f, r) = await InsertarFacturaAsync("A");
        await InsertarLineaAsync(f, r, "AGUA_POTABLE", 300m, null);
        Assert.Equal(300m, await SaldoAsync());

        // El motor aplica el pago rebajando montovalor_saldo de la línea.
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.factura_detalle SET montovalor_saldo = 120 WHERE factura_id = @f",
            new { f }, Transaction));
        Assert.Equal(120m, await SaldoAsync());
    }

    [SkippableFact]
    public async Task Saldo_por_servicio_solo_lineas_pendientes_del_servicio()
    {
        await PrepararEmpresaAsync();

        var (fa, ra) = await InsertarFacturaAsync("A");
        await InsertarLineaAsync(fa, ra, "AGUA_POTABLE", 200m, 150m);
        await InsertarLineaAsync(fa, ra, "ALCANTARILLADO", 90m, null);

        var (fc, rc) = await InsertarFacturaAsync("C");   // compensada → fuera
        await InsertarLineaAsync(fc, rc, "AGUA_POTABLE", 500m, 500m);

        Assert.Equal(150m, await SaldoServicioAsync("AGUA_POTABLE"));
        Assert.Equal(90m, await SaldoServicioAsync("ALCANTARILLADO"));
        Assert.Equal(0m, await SaldoServicioAsync("TASA_AMBIENTAL"));
    }

    [SkippableFact]
    public async Task Equivalencia_con_el_saldo_legacy_en_flujo_espejo()
    {
        await PrepararEmpresaAsync();

        // Flujo real del dual-write: la factura como documento + su espejo en
        // transaccion_abonado. El saldo por documentos debe IGUALAR la suma
        // legacy de la vista vigente (auditoría que sostiene el corte de F7).
        var (f, r) = await InsertarFacturaAsync("A");
        await InsertarLineaAsync(f, r, "AGUA_POTABLE", 199.27m, null);
        await InsertarLineaAsync(f, r, "TASA_AMBIENTAL", 5.90m, null);
        await InsertarMovimientoAsync("AGUA_POTABLE", "A", 199.27m, 0m);
        await InsertarMovimientoAsync("TASA_AMBIENTAL", "A", 5.90m, 0m);
        await InsertarMovimientoAsync("SALDO_ANTERIOR", "A", 550.84m, 0m);

        var legacy = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT COALESCE(SUM(COALESCE(debitos,0) - COALESCE(creditos,0)), 0)
            FROM public.vw_transaccion_abonado_vigente
            WHERE company_id = @companyId AND cliente_clave = @clave",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));

        Assert.Equal(756.01m, legacy);
        Assert.Equal(legacy, await SaldoAsync());
    }
}
