using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 4 del plan de integración contable-comercial (2026-07-02):
// captación/abonos/misceláneos sobre la configuración única. Cubre el
// comprobante por config (sp_con_generar_comprobante_config), su reverso,
// la cola de pendientes y la resolución por modos (fn_con_resolver_cuenta_modo,
// restaurada en el script de F4). Requiere los scripts de F1/F2/F3/F4 aplicados.
[Collection("Postgres")]
public sealed class CaptacionConfigContabilidadTests : IntegrationTestBase
{
    public CaptacionConfigContabilidadTests(PostgresFixture fixture) : base(fixture) { }

    private const string Modulo = "CAJA";
    private const string DocumentType = "ABO";
    private const long DocumentoBase = 940_000_000L;

    /// <summary>
    /// Deja lista la config mínima dentro de la transacción: perfil ERSAPS,
    /// asiento del módulo CAJA (diario + tipo) y, si se pide, un período
    /// contable abierto que cubra la fecha dada. Devuelve false si la BD de
    /// pruebas no tiene diario/tipo para armar el asiento.
    /// </summary>
    private async Task<bool> ArrangeAsync(DateTime? fechaPeriodo, bool encolarSinPeriodo = true)
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-f4')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET encolar_sin_periodo = @Encolar, activo_caja = true, activo_miscelaneos = true
            WHERE company_id = @CompanyId",
            new { CompanyId, Encolar = encolarSinPeriodo }, Transaction));

        var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            SELECT @CompanyId, @Modulo,
                   (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                   (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                   'test-f4'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId, Modulo }, Transaction));
        if (!asientoOk)
        {
            return false;
        }

        if (fechaPeriodo.HasValue)
        {
            await Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_periodo_contable
                    (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
                SELECT @CompanyId, 'F4-TEST', 'Periodo test F4',
                       @Fecha::date - 1, @Fecha::date + 1, 0, 'OPEN', now(), 'test-f4'
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.con_periodo_contable p
                    WHERE p.company_id = @CompanyId AND COALESCE(p.status_id, 2) = 0
                      AND @Fecha::date BETWEEN p.start_date::date AND p.end_date::date)",
                new { CompanyId, Fecha = fechaPeriodo.Value }, Transaction));
        }

        return true;
    }

    private async Task<(long CuentaCaja, long CuentaCxc)> ResolverCuentasAsync()
    {
        var caja = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'CAJA', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));
        var cxc = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'CXC', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));
        return (caja, cxc);
    }

    private Task<long?> GenerarAsync(long documentId, DateTime fecha, long cuentaCaja, long cuentaCxc, decimal monto = 150.75m) =>
        Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT public.sp_con_generar_comprobante_config(
                @CompanyId, @Modulo, @DocumentType, @DocumentId, @DocumentNumber,
                @Fecha::date, 'Comprobante test F4', 'test-f4',
                jsonb_build_array(
                    jsonb_build_object('account_id', @CuentaCaja, 'debe', @Monto, 'haber', 0, 'descripcion', 'Caja test'),
                    jsonb_build_object('account_id', @CuentaCxc, 'debe', 0, 'haber', @Monto, 'descripcion', 'CxC test')))",
            new
            {
                CompanyId,
                Modulo,
                DocumentType,
                DocumentId = documentId,
                DocumentNumber = $"ABO-{documentId}",
                Fecha = fecha,
                CuentaCaja = cuentaCaja,
                CuentaCxc = cuentaCxc,
                Monto = monto
            }, Transaction));

    [SkippableFact]
    public async Task Comprobante_config_genera_partida_balanceada_y_posteada()
    {
        Skip.IfNot(await ArrangeAsync(DateTime.Today), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();

        var polizaId = await GenerarAsync(DocumentoBase + 1, DateTime.Today, caja, cxc);
        Assert.NotNull(polizaId);

        var partida = await Connection.QueryFirstAsync<(short status, string module, string docType, decimal debe, decimal haber)>(
            new CommandDefinition(@"
                SELECT h.status, h.module, h.document_type,
                       (SELECT COALESCE(SUM(d.debit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                       (SELECT COALESCE(SUM(d.credit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id)
                FROM public.con_partida_hdr h
                WHERE h.company_id = @CompanyId AND h.poliza_id = @PolizaId",
                new { CompanyId, PolizaId = polizaId }, Transaction));

        Assert.Equal(1, partida.status);            // posteada por el motor único
        Assert.Equal(Modulo, partida.module);
        Assert.Equal(DocumentType, partida.docType);
        Assert.Equal(partida.debe, partida.haber);  // balanceada
        Assert.Equal(150.75m, partida.debe);
    }

    [SkippableFact]
    public async Task Comprobante_config_es_idempotente_por_documento()
    {
        Skip.IfNot(await ArrangeAsync(DateTime.Today), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();

        var primera = await GenerarAsync(DocumentoBase + 2, DateTime.Today, caja, cxc);
        var segunda = await GenerarAsync(DocumentoBase + 2, DateTime.Today, caja, cxc);

        Assert.NotNull(primera);
        Assert.Equal(primera, segunda);

        var partidas = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM public.con_partida_hdr
            WHERE company_id = @CompanyId AND module = @Modulo
              AND document_type = @DocumentType AND document_id = @DocumentId",
            new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 2 }, Transaction));
        Assert.Equal(1, partidas);
    }

    [SkippableFact]
    public async Task Comprobante_config_sin_asiento_lanza_error_claro()
    {
        Skip.IfNot(await ArrangeAsync(DateTime.Today), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();

        await Connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.con_integracion_asiento WHERE company_id = @CompanyId AND module = @Modulo",
            new { CompanyId, Modulo }, Transaction));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            GenerarAsync(DocumentoBase + 3, DateTime.Today, caja, cxc));
        Assert.Contains("diario y tipo de partida", ex.MessageText);
    }

    [SkippableFact]
    public async Task Comprobante_config_desbalanceado_lanza()
    {
        Skip.IfNot(await ArrangeAsync(DateTime.Today), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
                SELECT public.sp_con_generar_comprobante_config(
                    @CompanyId, @Modulo, @DocumentType, @DocumentId, 'ABO-X',
                    current_date, 'Desbalanceado', 'test-f4',
                    jsonb_build_array(
                        jsonb_build_object('account_id', @CuentaCaja, 'debe', 100, 'haber', 0),
                        jsonb_build_object('account_id', @CuentaCxc, 'debe', 0, 'haber', 99)))",
                new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 4, CuentaCaja = caja, CuentaCxc = cxc },
                Transaction)));
        Assert.Contains("no está balanceado", ex.MessageText);
    }

    [SkippableFact]
    public async Task Sin_periodo_encola_y_reproceso_postea_pendiente()
    {
        Skip.IfNot(await ArrangeAsync(fechaPeriodo: null), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();
        var fechaSinPeriodo = new DateTime(2032, 1, 15);

        var encolada = await GenerarAsync(DocumentoBase + 5, fechaSinPeriodo, caja, cxc);
        Assert.Null(encolada);

        var pendiente = await Connection.QueryFirstAsync<(long id, short status, int intentos)>(new CommandDefinition(@"
            SELECT partida_pendiente_id, status_id, intentos
            FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = @Modulo
              AND origen_tipo = @DocumentType AND origen_id = @DocumentId",
            new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 5 }, Transaction));
        Assert.Equal(1, pendiente.status);

        // Reintento sin período: no duplica cola, solo incrementa intentos.
        await GenerarAsync(DocumentoBase + 5, fechaSinPeriodo, caja, cxc);
        var pendientes = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = @Modulo
              AND origen_tipo = @DocumentType AND origen_id = @DocumentId AND status_id = 1",
            new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 5 }, Transaction));
        Assert.Equal(1, pendientes);

        // Abrir período y reprocesar: postea y marca PROCESADA.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            VALUES (@CompanyId, '203201-T', 'Periodo test F4 reproceso', DATE '2032-01-01', DATE '2032-01-31', 0, 'OPEN', now(), 'test-f4')",
            new { CompanyId }, Transaction));

        var polizaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT public.sp_con_procesar_partida_pendiente(@CompanyId, @PendienteId, 'test-f4')",
            new { CompanyId, PendienteId = pendiente.id }, Transaction));
        Assert.NotNull(polizaId);

        var procesada = await Connection.QueryFirstAsync<(short status, long? poliza)>(new CommandDefinition(@"
            SELECT status_id, poliza_id FROM public.con_partida_pendiente
            WHERE partida_pendiente_id = @PendienteId",
            new { PendienteId = pendiente.id }, Transaction));
        Assert.Equal(2, procesada.status);
        Assert.Equal(polizaId, procesada.poliza);

        var status = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT status FROM public.con_partida_hdr WHERE company_id = @CompanyId AND poliza_id = @PolizaId",
            new { CompanyId, PolizaId = polizaId }, Transaction));
        Assert.Equal(1, status);
    }

    [SkippableFact]
    public async Task Sin_periodo_y_sin_encolar_lanza_excepcion()
    {
        Skip.IfNot(await ArrangeAsync(fechaPeriodo: null, encolarSinPeriodo: false), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            GenerarAsync(DocumentoBase + 6, new DateTime(2032, 2, 10), caja, cxc));
        Assert.Contains("no permite encolar", ex.MessageText);
    }

    [SkippableFact]
    public async Task Revertir_comprobante_config_revierte_y_descarta_pendiente()
    {
        Skip.IfNot(await ArrangeAsync(DateTime.Today), "Falta diario/tipo en la BD de pruebas.");
        var (caja, cxc) = await ResolverCuentasAsync();

        // Documento con partida posteada → reverso la deja en DRAFT.
        var polizaId = await GenerarAsync(DocumentoBase + 7, DateTime.Today, caja, cxc);
        Assert.NotNull(polizaId);

        var revertida = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT public.sp_con_revertir_comprobante_config(
                @CompanyId, @Modulo, ARRAY[@DocumentType]::varchar[], @DocumentId, 'test-f4')",
            new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 7 }, Transaction));
        Assert.Equal(polizaId, revertida);

        var status = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT status FROM public.con_partida_hdr WHERE company_id = @CompanyId AND poliza_id = @PolizaId",
            new { CompanyId, PolizaId = polizaId }, Transaction));
        Assert.Equal(0, status);

        // Re-cobro del mismo documento tras el reverso: la idempotencia ignora el
        // draft revertido y genera una partida NUEVA posteada.
        var polizaNueva = await GenerarAsync(DocumentoBase + 7, DateTime.Today, caja, cxc);
        Assert.NotNull(polizaNueva);
        Assert.NotEqual(polizaId, polizaNueva);

        var statusNueva = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT status FROM public.con_partida_hdr WHERE company_id = @CompanyId AND poliza_id = @PolizaId",
            new { CompanyId, PolizaId = polizaNueva }, Transaction));
        Assert.Equal(1, statusNueva);

        // Documento encolado (sin período) → reverso descarta la pendiente y devuelve NULL.
        var encolada = await GenerarAsync(DocumentoBase + 8, new DateTime(2032, 3, 10), caja, cxc);
        Assert.Null(encolada);

        var reversoEncolada = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT public.sp_con_revertir_comprobante_config(
                @CompanyId, @Modulo, ARRAY[@DocumentType]::varchar[], @DocumentId, 'test-f4')",
            new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 8 }, Transaction));
        Assert.Null(reversoEncolada);

        var descartada = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(@"
            SELECT status_id FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = @Modulo
              AND origen_tipo = @DocumentType AND origen_id = @DocumentId",
            new { CompanyId, Modulo, DocumentType, DocumentId = DocumentoBase + 8 }, Transaction));
        Assert.Equal(3, descartada);
    }

    [SkippableFact]
    public async Task Resolver_cuenta_modo_general_ignora_dimensiones_y_por_servicio_categoria_detalla()
    {
        Skip.IfNot(await ArrangeAsync(fechaPeriodo: null), "Falta diario/tipo en la BD de pruebas.");

        var dims = await Connection.QueryFirstOrDefaultAsync<(long servicioId, int categoriaId)>(new CommandDefinition(@"
            SELECT ic.servicio_id, ic.categoria_servicio_id
            FROM public.con_integracion_cuenta ic
            WHERE ic.company_id = @CompanyId AND ic.uso = 'CXC'
              AND ic.servicio_id IS NOT NULL AND ic.categoria_servicio_id IS NOT NULL
              AND ic.con_medicion = true
            LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(dims.servicioId == 0, "El perfil ERSAPS no llenó la matriz servicio × categoría en la BD de pruebas.");

        var general = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'CXC', NULL, NULL, NULL)",
            new { CompanyId }, Transaction));

        // GENERAL ignora las dimensiones aunque se le pasen.
        var modoGeneral = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT public.fn_con_resolver_cuenta_modo(
                @CompanyId, 'CXC', 'GENERAL', @ServicioId, @CategoriaId, true)",
            new { CompanyId, ServicioId = dims.servicioId, CategoriaId = dims.categoriaId }, Transaction));
        Assert.Equal(general, modoGeneral);

        // POR_SERVICIO_CATEGORIA devuelve la fila específica, distinta del fallback.
        var modoDetalle = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT public.fn_con_resolver_cuenta_modo(
                @CompanyId, 'CXC', 'POR_SERVICIO_CATEGORIA', @ServicioId, @CategoriaId, true)",
            new { CompanyId, ServicioId = dims.servicioId, CategoriaId = dims.categoriaId }, Transaction));
        Assert.NotEqual(general, modoDetalle);
    }
}
