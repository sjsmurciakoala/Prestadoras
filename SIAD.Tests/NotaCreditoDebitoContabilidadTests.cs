using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Posteo contable automático de NC/ND (20260702_nc_nd_posteo_contable.sql):
// al emitir una nota, el SP debe generar y postear la partida vía
// sp_con_generar_comprobante dentro de la misma transacción.
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
        public short status { get; set; }
        public decimal total_debit { get; set; }
        public decimal total_credit { get; set; }
    }

    /// <summary>
    /// Arma dentro de la transacción: período abierto que cubra hoy,
    /// plantilla VENTAS/&lt;doc&gt; con dos cuentas posteables, y devuelve
    /// (facturaId, caiId, debitAccountId, creditAccountId).
    /// </summary>
    private async Task<(int facturaId, long caiId, long debitAccount, long creditAccount)?>
        ArrangeAsync(string documentType, short tipoFiscal)
    {
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

        var facturaId = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
            SELECT f.id FROM public.factura f
            WHERE f.company_id = @CompanyId
              AND COALESCE(f.estado, '') <> 'N'
              AND COALESCE(f.saldototal, 0) > 0
              AND EXISTS (SELECT 1 FROM public.cliente_maestro cm
                          WHERE cm.company_id = f.company_id
                            AND cm.maestro_cliente_clave = f.clientecodigo)
            ORDER BY f.id LIMIT 1",
            new { CompanyId }, Transaction));
        if (facturaId is null) return null;

        // Período abierto que cubra la fecha de emisión (hoy).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            SELECT @CompanyId, to_char(current_date, 'YYYYMM') || '-T', 'Periodo test NC/ND',
                   date_trunc('month', current_date), date_trunc('month', current_date) + interval '1 month' - interval '1 second',
                   0, 'OPEN', now(), 'TEST'
            WHERE NOT EXISTS (
                SELECT 1 FROM public.con_periodo_contable p
                WHERE p.company_id = @CompanyId
                  AND COALESCE(p.status_id, 2) = 0
                  AND current_date BETWEEN p.start_date::date AND p.end_date::date)",
            new { CompanyId }, Transaction));

        // Dos cuentas posteables distintas para la plantilla.
        var cuentas = (await Connection.QueryAsync<long>(new CommandDefinition(@"
            SELECT account_id FROM public.con_plan_cuentas
            WHERE company_id = @CompanyId AND allows_posting = true
            ORDER BY account_id LIMIT 2",
            new { CompanyId }, Transaction))).ToList();
        if (cuentas.Count < 2) return null;

        var templateId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.con_plantilla_partida_hdr
                (company_id, module, document_type, name, description, is_active,
                 created_at, created_by, updated_at, updated_by)
            VALUES (@CompanyId, 'VENTAS', @Doc, 'TEST VENTAS ' || @Doc, 'plantilla test', true,
                    now(), 'TEST', now(), 'TEST')
            RETURNING template_id",
            new { CompanyId, Doc = documentType }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_plantilla_partida_dtl
                (company_id, template_id, line_number, account_id, debit_formula, credit_formula, description)
            VALUES (@CompanyId, @TemplateId, 1, @Debit, '{total}', NULL, 'debe test'),
                   (@CompanyId, @TemplateId, 2, @Credit, NULL, '{total}', 'haber test')",
            new { CompanyId, TemplateId = templateId, Debit = cuentas[0], Credit = cuentas[1] }, Transaction));

        return (facturaId.Value, caiId.Value, cuentas[0], cuentas[1]);
    }

    [SkippableFact]
    public async Task Emitir_NC_genera_partida_contable_posteada_y_balanceada()
    {
        var arranged = await ArrangeAsync("NC", tipoFiscal: 6);
        Skip.If(arranged is null, "Faltan fixtures (CAI NC, factura activa o plan de cuentas).");
        var (facturaId, caiId, debitAccount, creditAccount) = arranged.Value;

        var nota = await Connection.QueryFirstAsync<NotaResult>(new CommandDefinition(@"
            SELECT * FROM public.sp_adm_emitir_nota_credito(
                p_company_id := @CompanyId,
                p_factura_origen_id := @FacturaId,
                p_motivo_anulacion_id := 1::smallint,
                p_motivo_detalle := 'test posteo contable',
                p_monto_disminuir := NULL::numeric,
                p_lineas := NULL::jsonb,
                p_usuario_emisor := 'TEST',
                p_cai_id := @CaiId)",
            new { CompanyId, FacturaId = facturaId, CaiId = caiId }, Transaction));

        Assert.True(nota.success);

        // La nota debe quedar enlazada a su partida.
        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT poliza_id FROM public.adm_nota_credito WHERE nota_credito_id = @Id",
            new { Id = nota.nota_credito_id }, Transaction));
        Assert.NotNull(polizaId);

        // Partida posteada (status=1), balanceada y con el monto de la NC.
        var partida = await Connection.QueryFirstAsync<PartidaRow>(new CommandDefinition(@"
            SELECT poliza_id, status, total_debit, total_credit
            FROM public.con_partida_hdr
            WHERE company_id = @CompanyId AND module = 'VENTAS'
              AND document_type = 'NC' AND document_id = @NotaId",
            new { CompanyId, NotaId = nota.nota_credito_id }, Transaction));

        Assert.Equal(polizaId, partida.poliza_id);
        Assert.Equal(1, partida.status);
        Assert.True(partida.total_debit > 0);
        Assert.Equal(partida.total_debit, partida.total_credit);

        // Dirección contable: DEBE en línea 1 (ingresos), HABER en línea 2 (CxC).
        var lineas = (await Connection.QueryAsync<(long account_id, decimal debit_amount, decimal credit_amount)>(
            new CommandDefinition(@"
                SELECT account_id, debit_amount, credit_amount
                FROM public.con_partida_dtl
                WHERE poliza_id = @PolizaId ORDER BY line_number",
                new { PolizaId = polizaId }, Transaction))).ToList();

        Assert.Equal(2, lineas.Count);
        Assert.Equal(debitAccount, lineas[0].account_id);
        Assert.True(lineas[0].debit_amount > 0);
        Assert.Equal(0, lineas[0].credit_amount);
        Assert.Equal(creditAccount, lineas[1].account_id);
        Assert.True(lineas[1].credit_amount > 0);
        Assert.Equal(0, lineas[1].debit_amount);
    }

    [SkippableFact]
    public async Task Emitir_ND_genera_partida_contable_posteada()
    {
        var arranged = await ArrangeAsync("ND", tipoFiscal: 7);
        Skip.If(arranged is null, "Faltan fixtures (CAI ND, factura activa o plan de cuentas).");
        var (facturaId, caiId, _, _) = arranged.Value;

        var nota = await Connection.QueryFirstAsync<NotaResult>(new CommandDefinition(@"
            SELECT * FROM public.sp_adm_emitir_nota_debito(
                p_company_id := @CompanyId,
                p_factura_origen_id := @FacturaId,
                p_motivo_aumento_id := 1::smallint,
                p_motivo_detalle := 'test posteo contable',
                p_monto_aumentar := 100.00::numeric,
                p_lineas := NULL::jsonb,
                p_usuario_emisor := 'TEST',
                p_cai_id := @CaiId)",
            new { CompanyId, FacturaId = facturaId, CaiId = caiId }, Transaction));

        Assert.True(nota.success);

        var partida = await Connection.QueryFirstAsync<PartidaRow>(new CommandDefinition(@"
            SELECT h.poliza_id, h.status, h.total_debit, h.total_credit
            FROM public.con_partida_hdr h
            JOIN public.adm_nota_debito nd ON nd.poliza_id = h.poliza_id
            WHERE nd.nota_debito_id = @NotaId",
            new { NotaId = nota.nota_debito_id }, Transaction));

        Assert.Equal(1, partida.status);
        Assert.Equal(100.00m, partida.total_debit);
        Assert.Equal(100.00m, partida.total_credit);
    }

    [SkippableFact]
    public async Task Emitir_NC_sin_plantilla_contable_falla_atomicamente()
    {
        // Arrange igual que el caso feliz pero SIN crear la plantilla:
        // desactivar cualquier plantilla VENTAS/NC existente.
        var caiId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT cai_id FROM public.adm_cai_facturacion
            WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
              AND correlativo_actual < rango_hasta
            ORDER BY cai_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(caiId is null, "No hay CAI tipo NC en esta company.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.adm_cai_facturacion
            SET status_id = 1, estado_id = 1,
                vigencia_desde = current_date - 1,
                vigencia_hasta = current_date + 365,
                fecha_limite_emision = current_date + 365
            WHERE company_id = @CompanyId AND cai_id = @CaiId",
            new { CompanyId, CaiId = caiId }, Transaction));

        var facturaId = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
            SELECT f.id FROM public.factura f
            WHERE f.company_id = @CompanyId AND COALESCE(f.estado,'') <> 'N'
              AND COALESCE(f.saldototal, 0) > 0
              AND EXISTS (SELECT 1 FROM public.cliente_maestro cm
                          WHERE cm.company_id = f.company_id
                            AND cm.maestro_cliente_clave = f.clientecodigo)
            ORDER BY f.id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(facturaId is null, "No hay factura activa con cliente.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_plantilla_partida_hdr SET is_active = false
            WHERE company_id = @CompanyId AND module = 'VENTAS' AND document_type = 'NC'",
            new { CompanyId }, Transaction));

        var exception = await Record.ExceptionAsync(async () =>
        {
            await Connection.QueryAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_adm_emitir_nota_credito(
                    p_company_id := @CompanyId,
                    p_factura_origen_id := @FacturaId,
                    p_motivo_anulacion_id := 1::smallint,
                    p_motivo_detalle := 'test sin plantilla',
                    p_monto_disminuir := NULL::numeric,
                    p_lineas := NULL::jsonb,
                    p_usuario_emisor := 'TEST',
                    p_cai_id := @CaiId)",
                new { CompanyId, FacturaId = facturaId, CaiId = caiId }, Transaction));
        });

        // Sin plantilla contable la emisión completa debe fallar (atomicidad):
        // el error viene del motor central y aborta también la nota.
        Assert.NotNull(exception);
        Assert.Contains("plantilla", exception!.Message, StringComparison.OrdinalIgnoreCase);
    }
}
