using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// F7 H4/H5 (2026-07-30): transaccion_abonado quedó CONGELADA (histórico de
/// solo lectura) y la vista de vigencia se retiró — la regla de vigencia que
/// este archivo asertaba murió con ella; el saldo y los reportes viven en el
/// modelo nuevo (documentos + adm_pago, ver SaldoDocumentosTests). Lo que se
/// fija ahora es el CONGELAMIENTO: el candado rechaza la escritura de
/// operación, la migración entra sin trigger de espejo, y los catálogos de F1
/// siguen sembrados (los ids estampados del histórico no se pierden).
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

    private Task<(short? tipoId, short? estadoPagoId, short? estadoId)> UltimoEspejoAsync() =>
        Connection.QuerySingleAsync<(short?, short?, short?)>(new CommandDefinition(@"
            SELECT tipo_transaccion_id, estado_pago_id, estado_id
            FROM public.transaccion_abonado
            WHERE company_id = @companyId AND cliente_clave = @clave
            ORDER BY ide DESC LIMIT 1",
            new { companyId = EmpresaSintetica, clave = Clave }, Transaction));

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
    public async Task Vista_de_vigencia_ya_no_existe_h5()
    {
        await PrepararEmpresaAsync();

        // Decisión "nada legacy se conserva": la vista se retiró; los reportes
        // leen vw_rep_movimiento_vigente (modelo nuevo). Si alguien la recrea,
        // este test falla.
        var existe = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM pg_views WHERE viewname = 'vw_transaccion_abonado_vigente'",
            transaction: Transaction));
        Assert.Equal(0L, existe);

        var nueva = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM pg_views WHERE viewname = 'vw_rep_movimiento_vigente'",
            transaction: Transaction));
        Assert.Equal(1L, nueva);
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
