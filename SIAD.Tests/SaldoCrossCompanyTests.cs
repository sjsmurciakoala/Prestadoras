using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Multitenancy del saldo previo en el motor de factura (fix-saldo-cross-company,
/// 2026-07-07). sp_adm_calcular_factura_lectura resolvía el saldo con el overload de
/// 1 argumento public.sp_obtener_cliente_saldo(clave), que NO filtra company_id: con
/// claves de cliente colisionantes entre empresas devolvía el saldo de OTRA empresa
/// (viola la regla #1 del repo). El fix migra la llamada al overload de 2 args
/// (p_company_id, clave). Se simulan 2 empresas dentro de la transacción con ROLLBACK.
/// </summary>
[Collection("Postgres")]
public sealed class SaldoCrossCompanyTests : IntegrationTestBase
{
    private const long ClienteConSaldoId = 102814;   // piloto company 2 con saldo > 0 (662.22)
    private const long EmpresaOtraId = 9999;         // empresa sintética (rollback)
    private const decimal SaldoOtra = 99999.99m;     // saldo distinto y "más nuevo" en la otra empresa

    public SaldoCrossCompanyTests(PostgresFixture fixture) : base(fixture) { }

    private Task<bool> ClienteExisteAsync() =>
        Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli AND estado=true)",
            new { c = CompanyId, cli = ClienteConSaldoId }, Transaction));

    private async Task<(string clave, decimal saldo2)> PrepararAsync()
    {
        var clave = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT maestro_cliente_clave FROM public.cliente_maestro WHERE company_id=@c AND maestro_cliente_id=@cli AND estado=true",
            new { c = CompanyId, cli = ClienteConSaldoId }, Transaction));

        var saldo2 = await Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@c, @clave)",
            new { c = CompanyId, clave }, Transaction)) ?? 0m;

        return (clave, saldo2);
    }

    private async Task InsertarSaldoColisionanteAsync(string clave)
    {
        // Segunda empresa con la MISMA clave y otro saldo. El movimiento nuevo obtiene el
        // ide identity más alto → es el "último" GLOBAL, que es justo lo que el 1-arg tomaba.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X999', 'Otra', 'Otra Empresa', 'RTN-X', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING",
            new { id = EmpresaOtraId }, Transaction));

        // debitos y saldo llevan el mismo valor: la firma de 2 args suma
        // (debitos - creditos) de los vigentes (fix vigencia 2026-07-16) y la de
        // 1 arg (deprecated) sigue leyendo la columna saldo del último movimiento.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.transaccion_abonado (company_id, cliente_clave, debitos, saldo, estado, estado_id)
            VALUES (@id, @clave, @saldo, @saldo, 'A', 1)",
            new { id = EmpresaOtraId, clave, saldo = SaldoOtra }, Transaction));
    }

    [SkippableFact]
    public async Task Saldo_2arg_filtra_por_company_id()
    {
        Skip.IfNot(await ClienteExisteAsync(), "Falta el cliente piloto en esta BD.");
        var (clave, saldo2) = await PrepararAsync();
        Skip.If(saldo2 <= 0m, "El cliente piloto no tiene saldo > 0 en esta BD.");

        await InsertarSaldoColisionanteAsync(clave);

        var s2 = await Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@c, @clave)",
            new { c = CompanyId, clave }, Transaction));
        var sOtra = await Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@c, @clave)",
            new { c = EmpresaOtraId, clave }, Transaction));

        Assert.Equal(saldo2, s2);        // la empresa de prueba conserva su saldo
        Assert.Equal(SaldoOtra, sOtra);  // la otra empresa queda aislada
    }

    [SkippableFact]
    public async Task Overload_1arg_es_cross_company_documenta_el_bug()
    {
        Skip.IfNot(await ClienteExisteAsync(), "Falta el cliente piloto en esta BD.");
        var (clave, saldo2) = await PrepararAsync();
        Skip.If(saldo2 <= 0m, "El cliente piloto no tiene saldo > 0 en esta BD.");

        await InsertarSaldoColisionanteAsync(clave);

        // El overload viejo (1-arg) toma el último movimiento GLOBAL → devuelve el de la otra
        // empresa. Este test fija POR QUÉ el motor NO debe usarlo (si alguien lo revierte, falla).
        var s1 = await Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@clave)",
            new { clave }, Transaction));

        Assert.Equal(SaldoOtra, s1);
        Assert.NotEqual(saldo2, s1);
    }

    [SkippableFact]
    public async Task Calcular_factura_usa_el_saldo_de_su_empresa_no_el_de_otra()
    {
        Skip.IfNot(await ClienteExisteAsync(), "Falta el cliente piloto en esta BD.");
        var (clave, saldo2) = await PrepararAsync();
        Skip.If(saldo2 <= 0m, "El cliente piloto no tiene saldo > 0 en esta BD.");

        await InsertarSaldoColisionanteAsync(clave);

        var saldosAnteriores = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT saldos_anteriores
            FROM public.sp_adm_calcular_factura_lectura(
                p_company_id := @c, p_anio := 2026, p_mes := 6, p_cliente_id := @cli,
                p_contador := NULL, p_fecha_lectura := CURRENT_DATE,
                p_lectura_actual := NULL, p_condicion_lectura := 'N')",
            new { c = CompanyId, cli = ClienteConSaldoId }, Transaction));

        // Con el fix el motor factura el saldo de SU empresa (662.22), no el leak (99999.99).
        // Con el código viejo (1-arg) esto habría dado SaldoOtra.
        Assert.Equal(saldo2, saldosAnteriores);
        Assert.NotEqual(SaldoOtra, saldosAnteriores);
    }
}
