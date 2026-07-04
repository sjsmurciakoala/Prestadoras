using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 5 del plan de integración contable-comercial (2026-07-02): posteo
// analítico de NC/ND por configuración (20260704_ci_fase5_ncnd_posteo_config.sql,
// supersede a la versión con plantillas del 2026-07-02). Al emitir con
// activo_notas encendido, el SP arma la partida espejo de la factura origen
// (NC: Debe Ingresos / Haber CxC analítica; ND: inverso) vía
// sp_con_generar_comprobante_config en la misma transacción.
// Requiere los scripts de F1/F2/F3/F4/F5 aplicados.
[Collection("Postgres")]
public sealed class NotaCreditoDebitoContabilidadTests : IntegrationTestBase
{
    public NotaCreditoDebitoContabilidadTests(PostgresFixture fixture) : base(fixture) { }

    private sealed class NotaResult
    {
        public bool success { get; set; }
        public long nota_credito_id { get; set; }
        public long nota_debito_id { get; set; }
        public string? numero_documento { get; set; }
    }

    private sealed class PartidaRow
    {
        public long poliza_id { get; set; }
        public string module { get; set; } = "";
        public string document_type { get; set; } = "";
        public long document_id { get; set; }
        public short status { get; set; }
        public decimal total_debit { get; set; }
        public decimal total_credit { get; set; }
    }

    /// <summary>
    /// Arma dentro de la transacción la config de F1/F2 (perfil ERSAPS +
    /// asiento del módulo NOTAS + flags) y los fixtures de emisión (CAI
    /// vigente del tipo fiscal, factura activa con detalle positivo).
    /// Con conPeriodo=false CIERRA los períodos abiertos que cubren hoy
    /// (la partida de una nota siempre se fecha al día de emisión).
    /// Devuelve null si faltan fixtures en la BD de pruebas.
    /// </summary>
    private async Task<(int facturaId, long caiId)?> ArrangeAsync(
        short tipoFiscal,
        bool activoNotas = true,
        bool encolarSinPeriodo = true,
        bool conPeriodo = true,
        bool conAsientoNotas = true)
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-f5')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET activo_notas = @ActivoNotas, encolar_sin_periodo = @Encolar
            WHERE company_id = @CompanyId",
            new { CompanyId, ActivoNotas = activoNotas, Encolar = encolarSinPeriodo }, Transaction));

        // Determinismo: sin DEVOLUCION_NC salvo que el test la configure
        // (el perfil ERSAPS no la llena, pero la BD podría traerla de demos).
        await Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.con_integracion_cuenta WHERE company_id = @CompanyId AND uso = 'DEVOLUCION_NC'",
            new { CompanyId }, Transaction));

        if (conAsientoNotas)
        {
            var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
                INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
                SELECT @CompanyId, 'NOTAS',
                       (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                       (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                       'test-f5'
                ON CONFLICT (company_id, module)
                DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
                RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
                new { CompanyId }, Transaction));
            if (!asientoOk)
            {
                return null;
            }
        }
        else
        {
            await Connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM public.con_integracion_asiento WHERE company_id = @CompanyId AND module = 'NOTAS'",
                new { CompanyId }, Transaction));
        }

        if (conPeriodo)
        {
            await Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_periodo_contable
                    (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
                SELECT @CompanyId, to_char(current_date, 'YYYYMM') || '-T5', 'Periodo test F5',
                       date_trunc('month', current_date), date_trunc('month', current_date) + interval '1 month' - interval '1 second',
                       0, 'OPEN', now(), 'test-f5'
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.con_periodo_contable p
                    WHERE p.company_id = @CompanyId
                      AND COALESCE(p.status_id, 2) = 0
                      AND current_date BETWEEN p.start_date::date AND p.end_date::date)",
                new { CompanyId }, Transaction));
        }
        else
        {
            await Connection.ExecuteAsync(new CommandDefinition(@"
                UPDATE public.con_periodo_contable
                SET status_id = 2, status = 'CLOSED'
                WHERE company_id = @CompanyId
                  AND COALESCE(status_id, 2) = 0
                  AND current_date BETWEEN start_date::date AND end_date::date",
                new { CompanyId }, Transaction));
        }

        var caiId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT cai_id FROM public.adm_cai_facturacion
            WHERE company_id = @CompanyId
              AND tipo_documento_fiscal_id = @Tipo
              AND correlativo_actual < rango_hasta
            ORDER BY cai_id LIMIT 1",
            new { CompanyId, Tipo = tipoFiscal }, Transaction));
        if (caiId is null) return null;

        // El SP valida vigencia del CAI: forzarla dentro de la transacción.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.adm_cai_facturacion
            SET status_id = 1, estado_id = 1,
                vigencia_desde = current_date - 1,
                vigencia_hasta = current_date + 365,
                fecha_limite_emision = current_date + 365
            WHERE company_id = @CompanyId AND cai_id = @CaiId",
            new { CompanyId, CaiId = caiId }, Transaction));

        // Factura activa con cliente y detalle solo positivo (una línea
        // negativa invertiría el lado y rompería las aserciones por código).
        var facturaId = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
            SELECT f.id FROM public.factura f
            WHERE f.company_id = @CompanyId
              AND COALESCE(f.estado, '') <> 'N'
              AND COALESCE(f.saldototal, 0) >= 1
              AND EXISTS (SELECT 1 FROM public.cliente_maestro cm
                          WHERE cm.company_id = f.company_id
                            AND cm.maestro_cliente_clave = f.clientecodigo)
              AND EXISTS (SELECT 1 FROM public.factura_detalle fd
                          WHERE fd.factura_id = f.id AND COALESCE(fd.montovalor, 0) > 0)
              AND NOT EXISTS (SELECT 1 FROM public.factura_detalle fd
                              WHERE fd.factura_id = f.id AND COALESCE(fd.montovalor, 0) < 0)
            ORDER BY f.id LIMIT 1",
            new { CompanyId }, Transaction));
        if (facturaId is null) return null;

        return (facturaId.Value, caiId.Value);
    }

    private Task<NotaResult> EmitirNcAsync(int facturaId, long caiId, decimal? monto = null, string? lineasJson = null) =>
        Connection.QueryFirstAsync<NotaResult>(new CommandDefinition(@"
            SELECT * FROM public.sp_adm_emitir_nota_credito(
                p_company_id := @CompanyId,
                p_factura_origen_id := @FacturaId,
                p_motivo_anulacion_id := 1::smallint,
                p_motivo_detalle := 'test posteo config F5',
                p_monto_disminuir := @Monto::numeric,
                p_lineas := @Lineas::jsonb,
                p_usuario_emisor := 'TEST',
                p_cai_id := @CaiId)",
            new
            {
                CompanyId,
                FacturaId = facturaId,
                CaiId = caiId,
                Monto = (object?)monto ?? DBNull.Value,
                Lineas = (object?)lineasJson ?? DBNull.Value
            }, Transaction));

    private Task<PartidaRow> PartidaDeNotaAsync(string docType, long notaId) =>
        Connection.QueryFirstAsync<PartidaRow>(new CommandDefinition(@"
            SELECT poliza_id, module, document_type, document_id, status, total_debit, total_credit
            FROM public.con_partida_hdr
            WHERE company_id = @CompanyId AND module = 'NOTAS'
              AND document_type = @DocType AND document_id = @NotaId",
            new { CompanyId, DocType = docType, NotaId = notaId }, Transaction));

    private async Task<List<(long account, string code, decimal debit, decimal credit)>> LineasAsync(long polizaId)
    {
        var rows = await Connection.QueryAsync<(long, string, decimal, decimal)>(new CommandDefinition(@"
            SELECT d.account_id, pc.code, d.debit_amount, d.credit_amount
            FROM public.con_partida_dtl d
            JOIN public.con_plan_cuentas pc ON pc.account_id = d.account_id
            WHERE d.company_id = @CompanyId AND d.poliza_id = @PolizaId
            ORDER BY d.line_number",
            new { CompanyId, PolizaId = polizaId }, Transaction));
        return rows.ToList();
    }

    /// <summary>
    /// Recomputa por SQL independiente las cuentas analíticas que la partida
    /// espejo debería usar para la factura (INGRESO/CXC por servicio del
    /// detalle × snapshot dimensional × modos de la config) — detecta una
    /// regresión que colapse la resolución al fallback general.
    /// </summary>
    private async Task<(HashSet<long> Ingresos, HashSet<long> Cxc)> CuentasEsperadasAsync(int facturaId)
    {
        var rows = (await Connection.QueryAsync<(long ingreso, long cxc)>(new CommandDefinition(@"
            SELECT DISTINCT
                public.fn_con_resolver_cuenta_modo(@CompanyId, 'INGRESO', cfg.modo_ventas,
                    s.servicio_id, f.categoria_servicio_id, f.con_medicion),
                public.fn_con_resolver_cuenta_modo(@CompanyId, 'CXC', cfg.modo_cxc,
                    s.servicio_id, f.categoria_servicio_id, f.con_medicion)
            FROM public.factura f
            JOIN public.factura_detalle fd ON fd.factura_id = f.id AND COALESCE(fd.montovalor, 0) <> 0
            LEFT JOIN public.adm_servicio s ON s.company_id = @CompanyId AND s.codigo = fd.tiposervicio
            CROSS JOIN public.con_integracion_config cfg
            WHERE f.company_id = @CompanyId AND f.id = @FacturaId AND cfg.company_id = @CompanyId",
            new { CompanyId, FacturaId = facturaId }, Transaction))).ToList();
        return (rows.Select(r => r.ingreso).ToHashSet(), rows.Select(r => r.cxc).ToHashSet());
    }

    [SkippableFact]
    public async Task Emitir_NC_genera_partida_espejo_balanceada_y_posteada()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        var nota = await EmitirNcAsync(facturaId, caiId);
        Assert.True(nota.success);

        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT poliza_id FROM public.adm_nota_credito WHERE nota_credito_id = @Id",
            new { Id = nota.nota_credito_id }, Transaction));
        Assert.NotNull(polizaId);

        var partida = await PartidaDeNotaAsync("NC", nota.nota_credito_id);
        Assert.Equal(polizaId, partida.poliza_id);
        Assert.Equal(1, partida.status);                       // posteada por el motor único
        Assert.True(partida.total_debit > 0);
        Assert.Equal(partida.total_debit, partida.total_credit); // balanceada

        // La partida cuadra con lo asentado en el estado de cuenta del cliente.
        var totalNota = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT round(total_nota, 2) FROM public.adm_nota_credito WHERE nota_credito_id = @Id",
            new { Id = nota.nota_credito_id }, Transaction));
        Assert.Equal(totalNota, partida.total_debit);

        // Espejo ERSAPS: Debe = Ingresos (5.x), Haber = CxC abonados (113x).
        var lineas = await LineasAsync(partida.poliza_id);
        Assert.True(lineas.Count >= 2);
        Assert.All(lineas.Where(l => l.debit > 0), l => Assert.StartsWith("5", l.code));
        Assert.All(lineas.Where(l => l.credit > 0), l => Assert.StartsWith("113", l.code));

        // Espejo ANALÍTICO exacto: las cuentas posteadas son las que resuelve
        // la config para las líneas de la factura (no el fallback general).
        var esperadas = await CuentasEsperadasAsync(facturaId);
        Assert.Equal(esperadas.Ingresos, lineas.Where(l => l.debit > 0).Select(l => l.account).ToHashSet());
        Assert.Equal(esperadas.Cxc, lineas.Where(l => l.credit > 0).Select(l => l.account).ToHashSet());
    }

    [SkippableFact]
    public async Task Emitir_NC_parcial_minima_prorratea_sin_invertir_lineas()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        // Caso límite del prorrateo por restos mayores: total mucho menor que
        // la suma del detalle (20 líneas de 1.00 → total 0.10). Con redondeo
        // por línea + residual en una sola línea, la línea mayor quedaba en
        // -0.09 e invertía el lado (bruto 0.28 en vez de 0.10).
        var lineas20 = "[" + string.Join(",", Enumerable.Repeat("{\"monto_total\": 1.00}", 20)) + "]";
        var nota = await EmitirNcAsync(facturaId, caiId, monto: 0.10m, lineasJson: lineas20);
        Assert.True(nota.success);

        var partida = await PartidaDeNotaAsync("NC", nota.nota_credito_id);
        Assert.Equal(1, partida.status);
        Assert.Equal(0.10m, partida.total_debit);
        Assert.Equal(0.10m, partida.total_credit);

        // Sin inversión de lados: los débitos son solo de ingresos (5.x) y los
        // haberes solo de CxC (113x).
        var lineas = await LineasAsync(partida.poliza_id);
        Assert.All(lineas.Where(l => l.debit > 0), l => Assert.StartsWith("5", l.code));
        Assert.All(lineas.Where(l => l.credit > 0), l => Assert.StartsWith("113", l.code));
    }

    [SkippableFact]
    public async Task Emitir_ND_genera_partida_inversa_posteada()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 7);
        Skip.If(arranged is null, "Faltan fixtures (CAI ND, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        var nota = await Connection.QueryFirstAsync<NotaResult>(new CommandDefinition(@"
            SELECT * FROM public.sp_adm_emitir_nota_debito(
                p_company_id := @CompanyId,
                p_factura_origen_id := @FacturaId,
                p_motivo_aumento_id := 1::smallint,
                p_motivo_detalle := 'test posteo config F5',
                p_monto_aumentar := 100.00::numeric,
                p_lineas := NULL::jsonb,
                p_usuario_emisor := 'TEST',
                p_cai_id := @CaiId)",
            new { CompanyId, FacturaId = facturaId, CaiId = caiId }, Transaction));
        Assert.True(nota.success);

        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT poliza_id FROM public.adm_nota_debito WHERE nota_debito_id = @Id",
            new { Id = nota.nota_debito_id }, Transaction));
        Assert.NotNull(polizaId);

        var partida = await PartidaDeNotaAsync("ND", nota.nota_debito_id);
        Assert.Equal(polizaId, partida.poliza_id);
        Assert.Equal(1, partida.status);
        Assert.Equal(100.00m, partida.total_debit);
        Assert.Equal(100.00m, partida.total_credit);

        // Inverso de la NC: Debe = CxC abonados (113x), Haber = Ingresos (5.x).
        var lineas = await LineasAsync(partida.poliza_id);
        Assert.All(lineas.Where(l => l.debit > 0), l => Assert.StartsWith("113", l.code));
        Assert.All(lineas.Where(l => l.credit > 0), l => Assert.StartsWith("5", l.code));
    }

    [SkippableFact]
    public async Task Emitir_NC_con_activo_notas_apagado_emite_sin_postear()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6, activoNotas: false);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        var nota = await EmitirNcAsync(facturaId, caiId);
        Assert.True(nota.success);

        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT poliza_id FROM public.adm_nota_credito WHERE nota_credito_id = @Id",
            new { Id = nota.nota_credito_id }, Transaction));
        Assert.Null(polizaId);

        var partidas = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM public.con_partida_hdr
            WHERE company_id = @CompanyId AND module = 'NOTAS'
              AND document_type = 'NC' AND document_id = @NotaId",
            new { CompanyId, NotaId = nota.nota_credito_id }, Transaction));
        Assert.Equal(0, partidas);
    }

    [SkippableFact]
    public async Task Emitir_NC_con_devolucion_configurada_debita_esa_cuenta()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        // Uso DEVOLUCION_NC (opcional, F2): fila general apuntando a la cuenta
        // de descuento del perfil — al emitir reemplaza al espejo de ingresos.
        var devolucionAccount = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'DESCUENTO', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_integracion_cuenta (company_id, uso, account_id, created_by)
            VALUES (@CompanyId, 'DEVOLUCION_NC', @Account, 'test-f5')",
            new { CompanyId, Account = devolucionAccount }, Transaction));

        var nota = await EmitirNcAsync(facturaId, caiId);
        Assert.True(nota.success);

        var partida = await PartidaDeNotaAsync("NC", nota.nota_credito_id);
        Assert.Equal(1, partida.status);

        var debitos = await Connection.QueryAsync<long>(new CommandDefinition(@"
            SELECT DISTINCT account_id FROM public.con_partida_dtl
            WHERE company_id = @CompanyId AND poliza_id = @PolizaId AND debit_amount > 0",
            new { CompanyId, PolizaId = partida.poliza_id }, Transaction));
        var cuenta = Assert.Single(debitos);
        Assert.Equal(devolucionAccount, cuenta);
    }

    [SkippableFact]
    public async Task Emitir_NC_con_devolucion_parcial_no_bloquea_y_espeja_lo_no_cubierto()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        // DEVOLUCION_NC configurada SOLO para un servicio de la factura
        // (permitido por el modelo F1): la emisión no debe bloquearse — las
        // líneas cubiertas usan la cuenta de devolución y el resto cae al
        // espejo de ingresos.
        var servicios = (await Connection.QueryAsync<long>(new CommandDefinition(@"
            SELECT DISTINCT s.servicio_id
            FROM public.factura_detalle fd
            JOIN public.adm_servicio s ON s.company_id = @CompanyId AND s.codigo = fd.tiposervicio
            WHERE fd.factura_id = @FacturaId AND COALESCE(fd.montovalor, 0) <> 0",
            new { CompanyId, FacturaId = facturaId }, Transaction))).ToList();
        Skip.If(servicios.Count == 0, "La factura elegida no tiene servicios mapeados a adm_servicio.");

        var devolucionAccount = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'DESCUENTO', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_integracion_cuenta (company_id, uso, servicio_id, account_id, created_by)
            VALUES (@CompanyId, 'DEVOLUCION_NC', @ServicioId, @Account, 'test-f5')",
            new { CompanyId, ServicioId = servicios[0], Account = devolucionAccount }, Transaction));

        var nota = await EmitirNcAsync(facturaId, caiId);
        Assert.True(nota.success);

        var partida = await PartidaDeNotaAsync("NC", nota.nota_credito_id);
        Assert.Equal(1, partida.status);

        var lineas = await LineasAsync(partida.poliza_id);
        var cuentasDebito = lineas.Where(l => l.debit > 0).Select(l => l.account).ToHashSet();
        Assert.Contains(devolucionAccount, cuentasDebito);
        if (servicios.Count > 1)
        {
            // Los servicios no cubiertos siguen espejando sus cuentas de ingreso.
            Assert.Contains(cuentasDebito, c => c != devolucionAccount);
        }
    }

    [SkippableFact]
    public async Task Emitir_NC_sin_asiento_notas_falla_atomicamente()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6, conAsientoNotas: false);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        var notasAntes = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.adm_nota_credito WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));
        var correlativoAntes = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT correlativo_actual FROM public.adm_cai_facturacion WHERE company_id = @CompanyId AND cai_id = @CaiId",
            new { CompanyId, CaiId = caiId }, Transaction));

        await Transaction.SaveAsync("antes_emision");
        var ex = await Assert.ThrowsAsync<PostgresException>(() => EmitirNcAsync(facturaId, caiId));
        Assert.Contains("diario y tipo de partida", ex.MessageText);
        await Transaction.RollbackAsync("antes_emision");

        // Atomicidad: la falla del posteo revirtió también la emisión
        // (ni nota nueva ni correlativo consumido).
        var notasDespues = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.adm_nota_credito WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));
        var correlativoDespues = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT correlativo_actual FROM public.adm_cai_facturacion WHERE company_id = @CompanyId AND cai_id = @CaiId",
            new { CompanyId, CaiId = caiId }, Transaction));
        Assert.Equal(notasAntes, notasDespues);
        Assert.Equal(correlativoAntes, correlativoDespues);
    }

    [SkippableFact]
    public async Task Emitir_NC_sin_periodo_encola_y_reproceso_puebla_poliza()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6, conPeriodo: false);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        var nota = await EmitirNcAsync(facturaId, caiId);
        Assert.True(nota.success);

        // Sin período abierto y encolar_sin_periodo=true: la emisión sale bien
        // pero la partida queda en la cola (poliza_id NULL).
        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT poliza_id FROM public.adm_nota_credito WHERE nota_credito_id = @Id",
            new { Id = nota.nota_credito_id }, Transaction));
        Assert.Null(polizaId);

        var pendiente = await Connection.QueryFirstAsync<(long id, short status)>(new CommandDefinition(@"
            SELECT partida_pendiente_id, status_id
            FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = 'NOTAS'
              AND origen_tipo = 'NC' AND origen_id = @NotaId",
            new { CompanyId, NotaId = nota.nota_credito_id }, Transaction));
        Assert.Equal(1, pendiente.status);

        // Abrir período y reprocesar: postea, marca PROCESADA y la nota
        // recupera su poliza_id (v2 de sp_con_procesar_partida_pendiente).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            VALUES (@CompanyId, to_char(current_date, 'YYYYMM') || '-T5R', 'Periodo test F5 reproceso',
                    date_trunc('month', current_date), date_trunc('month', current_date) + interval '1 month' - interval '1 second',
                    0, 'OPEN', now(), 'test-f5')",
            new { CompanyId }, Transaction));

        var polizaReproceso = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT public.sp_con_procesar_partida_pendiente(@CompanyId, @PendienteId, 'test-f5')",
            new { CompanyId, PendienteId = pendiente.id }, Transaction));
        Assert.NotNull(polizaReproceso);

        var procesada = await Connection.QueryFirstAsync<(short status, long? poliza)>(new CommandDefinition(
            "SELECT status_id, poliza_id FROM public.con_partida_pendiente WHERE partida_pendiente_id = @Id",
            new { Id = pendiente.id }, Transaction));
        Assert.Equal(2, procesada.status);
        Assert.Equal(polizaReproceso, procesada.poliza);

        var polizaNota = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT poliza_id FROM public.adm_nota_credito WHERE nota_credito_id = @Id",
            new { Id = nota.nota_credito_id }, Transaction));
        Assert.Equal(polizaReproceso, polizaNota);

        var partida = await PartidaDeNotaAsync("NC", nota.nota_credito_id);
        Assert.Equal(1, partida.status);
        Assert.Equal(partida.total_debit, partida.total_credit);
    }

    [SkippableFact]
    public async Task Emitir_NC_sin_periodo_y_sin_encolar_rechaza_la_emision()
    {
        var arranged = await ArrangeAsync(tipoFiscal: 6, conPeriodo: false, encolarSinPeriodo: false);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura con detalle positivo, diario/tipo o plan ERSAPS).");
        var (facturaId, caiId) = arranged.Value;

        var notasAntes = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.adm_nota_credito WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));

        await Transaction.SaveAsync("antes_emision");
        var ex = await Assert.ThrowsAsync<PostgresException>(() => EmitirNcAsync(facturaId, caiId));
        Assert.Contains("no permite encolar", ex.MessageText);
        await Transaction.RollbackAsync("antes_emision");

        var notasDespues = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.adm_nota_credito WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));
        Assert.Equal(notasAntes, notasDespues);
    }
}
