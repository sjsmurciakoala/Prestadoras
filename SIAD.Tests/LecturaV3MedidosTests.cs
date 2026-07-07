using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Regresión del camino MEDIDO del motor tarifario (fix-resolver-medidos, 2026-07-06).
/// Antes del fix, un cliente con medidor sobre un cuadro RANGO_CONSUMO no podía
/// facturarse: sp_adm_resolver_servicio_derivado / el LATERAL de servicios base
/// pasaban p_consumo=NULL a las referencias PORCENTAJE_SERVICIO (ALCANTARILLADO,
/// que es % de AGUA medida), y la recursión sobre AGUA RANGO_CONSUMO reventaba con
/// "requiere consumo". Estos tests fijan: (1) el escalonado por tramos, (2) el %
/// sobre el agua medida, y (3) que el camino NO medido queda intacto.
///
/// Se apoyan en el cliente piloto 102838 (company 2). Cada test corre dentro de la
/// transacción con ROLLBACK del IntegrationTestBase, así que la reasignación de
/// cuadro/condición no ensucia la BD.
/// </summary>
[Collection("Postgres")]
public sealed class LecturaV3MedidosTests : IntegrationTestBase
{
    private const long ClientePilotoId = 102838;   // "con medidor" en los fixtures L8
    private const int Anio = 2026;
    private const int Mes = 6;                      // sin histórico → lect_ant=0 (igual que los fixtures L8)

    public LecturaV3MedidosTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record FacturaCalc(
        decimal consumo_facturable,
        decimal taservi1,   // AGUA_POTABLE
        decimal taservi2,   // ALCANTARILLADO
        decimal taservi3,   // TASA_AMBIENTAL
        decimal taservi4,   // TASA_SVA_ERSAPS
        decimal subtotal_servicios);

    /// <summary>
    /// Deja al cliente piloto realmente MEDIDO sobre el cuadro CON_MEDICION de agua
    /// (RANGO_CONSUMO por tramos). Devuelve el contador. Skip si faltan datos.
    /// </summary>
    private async Task<string> PrepararClienteMedidoAsync()
    {
        var contador = await Connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT contador FROM public.cliente_maestro
            WHERE company_id = @c AND maestro_cliente_id = @cli AND estado = true",
            new { c = CompanyId, cli = ClientePilotoId }, Transaction));

        Skip.If(string.IsNullOrWhiteSpace(contador),
            $"No existe el cliente piloto {ClientePilotoId} en esta BD.");

        // Asegurar el flag de medidor.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.cliente_maestro SET maestro_cliente_tiene_medidor = true
            WHERE company_id = @c AND maestro_cliente_id = @cli",
            new { c = CompanyId, cli = ClientePilotoId }, Transaction));

        // Resolver por código (sin ids mágicos) el cuadro CON_MEDICION de AGUA y la
        // condición CON_MEDICION, y asignarlos al cliente_servicio de AGUA_POTABLE.
        var afectados = await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.adm_cliente_servicio cs
               SET cuadro_tarifario_id = ct.cuadro_tarifario_id,
                   condicion_medicion_id = cm.condicion_medicion_id
              FROM public.adm_servicio s,
                   public.adm_cuadro_tarifario ct,
                   public.adm_condicion_medicion cm
             WHERE cs.company_id = @c
               AND cs.cliente_id = @cli
               AND s.company_id = cs.company_id AND s.servicio_id = cs.servicio_id
               AND s.codigo = 'AGUA_POTABLE'
               AND cs.status_id = 1
               AND ct.company_id = @c AND ct.codigo = 'APC_AGUA_CM_DOMESTICO'
               AND cm.company_id = @c AND cm.codigo = 'CON_MEDICION'",
            new { c = CompanyId, cli = ClientePilotoId }, Transaction));

        Skip.If(afectados == 0,
            "No se pudo asignar el cuadro medido APC_AGUA_CM_DOMESTICO al cliente piloto.");

        return contador!;
    }

    private Task<FacturaCalc> CalcularAsync(string contador, decimal consumo, string condicion = "N")
        => Connection.QueryFirstAsync<FacturaCalc>(new CommandDefinition(@"
            SELECT consumo_facturable, taservi1, taservi2, taservi3, taservi4, subtotal_servicios
            FROM public.sp_adm_calcular_factura_lectura(
                p_company_id := @c,
                p_anio := @anio,
                p_mes := @mes,
                p_cliente_id := @cli,
                p_contador := @contador,
                p_fecha_lectura := CURRENT_DATE,
                p_lectura_actual := @lectura,
                p_condicion_lectura := @cond)",
            new { c = CompanyId, anio = Anio, mes = Mes, cli = ClientePilotoId, contador, lectura = consumo, cond = condicion },
            Transaction));

    [SkippableFact]
    public async Task Cliente_medido_factura_sin_error_consumo_35_da_escalonado_correcto()
    {
        var contador = await PrepararClienteMedidoAsync();

        // Cuadro APC_AGUA_CM_DOMESTICO (ACUMULADO_POR_RANGO_APC):
        //   0-20  base 163.67
        //   21-30 12.98/m³ (+1.50 alquiler)
        //   31-40 15.55/m³
        //   41+   19.20/m³
        // consumo=35 → 163.67 + (10*12.98+1.50) + (5*15.55) = 163.67 + 131.30 + 77.75 = 372.72
        var f = await CalcularAsync(contador, 35m);

        Assert.Equal(35m, f.consumo_facturable);
        Assert.Equal(372.72m, f.taservi1);                   // AGUA por tramos
    }

    [SkippableTheory]
    [InlineData(15, 163.67)]    // solo tramo 1
    [InlineData(25, 230.07)]    // tramo 1 + parte del 2
    [InlineData(35, 372.72)]    // tramos 1 + 2 + parte del 3
    [InlineData(45, 546.47)]    // tramos 1 + 2 + 3 + parte del 4
    public async Task Consumo_que_cruza_tramos_da_total_escalonado(int consumo, decimal aguaEsperada)
    {
        var contador = await PrepararClienteMedidoAsync();

        var f = await CalcularAsync(contador, consumo);

        Assert.Equal(consumo, f.consumo_facturable);
        Assert.Equal(aguaEsperada, f.taservi1);
    }

    [SkippableFact]
    public async Task Alcantarillado_porcentaje_sale_sobre_el_agua_medida()
    {
        var contador = await PrepararClienteMedidoAsync();

        var f = await CalcularAsync(contador, 35m);

        // ALCANTARILLADO = 60% del monto de AGUA medida (no re-derivando el consumo).
        Assert.Equal(372.72m, f.taservi1);
        Assert.Equal(223.632m, f.taservi2);
        Assert.Equal(Math.Round(f.taservi1 * 0.60m, 4), f.taservi2);

        // TASA_SVA_ERSAPS = 2% AGUA + 2% ALCANTARILLADO, ambos sobre montos medidos.
        Assert.Equal(Math.Round(f.taservi1 * 0.02m + f.taservi2 * 0.02m, 4), f.taservi4);
        Assert.Equal(11.927m, f.taservi4);
    }

    [SkippableFact]
    public async Task Snapshot_offline_de_cliente_medido_trae_los_tramos_para_calculo_offline()
    {
        var contador = await PrepararClienteMedidoAsync();

        var json = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(@"
            SELECT snapshot_json::text
            FROM public.sp_adm_generar_snapshot_offline_cliente_lectura(@c, @cli, @anio, @mes, CURRENT_DATE)",
            new { c = CompanyId, cli = ClientePilotoId, anio = Anio, mes = Mes }, Transaction));

        Assert.False(string.IsNullOrWhiteSpace(json), "El snapshot offline vino vacío.");
        // Debe reflejar cliente medido y embeber el cuadro medido con sus tramos,
        // que es lo que la app L8 usa para calcular offline.
        Assert.Contains("\"tiene_medidor\": true", json);
        Assert.Contains("APC_AGUA_CM_DOMESTICO", json);
        Assert.Contains("ACUMULADO_POR_RANGO_APC", json);
    }

    [SkippableFact]
    public async Task Regresion_cliente_NO_medido_factura_igual_que_antes()
    {
        // Cliente 102813 (domestico SIN medidor) NO se toca: debe seguir facturando
        // AGUA por MONTO_FIJO 199.27 exactamente igual que antes del fix.
        const long clienteSinMedidor = 102813;

        var esSinMedidor = await Connection.ExecuteScalarAsync<bool?>(new CommandDefinition(@"
            SELECT COALESCE(maestro_cliente_tiene_medidor, false) = false
            FROM public.cliente_maestro
            WHERE company_id=@c AND maestro_cliente_id=@cli AND estado=true",
            new { c = CompanyId, cli = clienteSinMedidor }, Transaction));

        Skip.If(esSinMedidor is null, $"No existe el cliente {clienteSinMedidor} en esta BD.");
        // Guarda de intención: el test solo prueba el camino NO medido si el cliente
        // realmente no tiene medidor (si no, no estaría ejerciendo la regresión).
        Assert.True(esSinMedidor!.Value, $"El cliente {clienteSinMedidor} debe ser SIN medidor para esta regresión.");

        var agua = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            SELECT taservi1
            FROM public.sp_adm_calcular_factura_lectura(
                p_company_id := @c, p_anio := @anio, p_mes := @mes, p_cliente_id := @cli,
                p_contador := '0', p_fecha_lectura := CURRENT_DATE,
                p_lectura_actual := 15, p_condicion_lectura := 'N')",
            new { c = CompanyId, anio = Anio, mes = Mes, cli = clienteSinMedidor }, Transaction));

        Assert.Equal(199.27m, agua);
    }
}
