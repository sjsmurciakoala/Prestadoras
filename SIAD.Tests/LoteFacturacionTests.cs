using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 3 del plan de integración contable-comercial (2026-07-02):
// lote manual de partidas de facturación (sp_con_generar_partidas_facturacion),
// snapshot dimensional en factura y cola de regularización. Requiere los
// scripts de F1/F2/F3 aplicados y facturas de lectura en la BD de pruebas.
[Collection("Postgres")]
public sealed class LoteFacturacionTests : IntegrationTestBase
{
    public LoteFacturacionTests(PostgresFixture fixture) : base(fixture) { }

    private sealed class LoteResult
    {
        public long? lote_id { get; set; }
        public int polizas { get; set; }
        public int facturas { get; set; }
        public int encoladas { get; set; }
        public decimal total { get; set; }
    }

    /// <summary>
    /// Deja lista la configuración mínima del lote dentro de la transacción:
    /// perfil ERSAPS, asiento VENTAS (diario+tipo) y período abierto que cubra
    /// el rango de las facturas piloto. Devuelve el rango (min, max) de
    /// fechaemision de las facturas de lectura activas, o null si no hay.
    /// </summary>
    private async Task<(DateTime desde, DateTime hasta)?> ArrangeAsync(bool encolarSinPeriodo = true)
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-f3')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET encolar_sin_periodo = @Encolar
            WHERE company_id = @CompanyId",
            new { CompanyId, Encolar = encolarSinPeriodo }, Transaction));

        var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            SELECT @CompanyId, 'VENTAS',
                   (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                   (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                   'test-f3'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId }, Transaction));
        if (!asientoOk)
        {
            return null;
        }

        var rango = await Connection.QueryFirstOrDefaultAsync<(DateTime? desde, DateTime? hasta)>(new CommandDefinition(@"
            SELECT MIN(fechaemision)::timestamp, MAX(fechaemision)::timestamp
            FROM public.factura
            WHERE company_id = @CompanyId AND tipofacturacion = 'S' AND tipofactura = 'F'
              AND COALESCE(estado_id, 1) <> 3",
            new { CompanyId }, Transaction));
        if (rango.desde is null)
        {
            return null;
        }

        // Período abierto que cubra todo el rango (si el existente no alcanza).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            SELECT @CompanyId, 'F3-TEST', 'Periodo test lote F3',
                   @Desde::date - 1, @Hasta::date + 1, 0, 'OPEN', now(), 'test-f3'
            WHERE NOT EXISTS (
                SELECT 1 FROM public.con_periodo_contable p
                WHERE p.company_id = @CompanyId AND COALESCE(p.status_id, 2) = 0
                  AND @Desde::date >= p.start_date::date AND @Hasta::date <= p.end_date::date)",
            new { CompanyId, rango.desde, rango.hasta }, Transaction));

        return (rango.desde.Value, rango.hasta!.Value);
    }

    private Task<LoteResult> GenerarAsync(DateTime desde, DateTime hasta, string modo = "PERIODO") =>
        Connection.QueryFirstAsync<LoteResult>(new CommandDefinition(@"
            SELECT * FROM public.sp_con_generar_partidas_facturacion(
                @CompanyId, @Desde::date, @Hasta::date, @Modo, 'test-f3')",
            new { CompanyId, Desde = desde, Hasta = hasta, Modo = modo }, Transaction));

    [SkippableFact]
    public async Task Lote_genera_partida_balanceada_y_posteada()
    {
        var rango = await ArrangeAsync();
        Skip.If(rango is null, "Faltan facturas de lectura o diario/tipo en la BD de pruebas.");

        var resultado = await GenerarAsync(rango.Value.desde, rango.Value.hasta);

        Assert.True(resultado.polizas > 0, "El lote no generó pólizas.");
        Assert.True(resultado.facturas > 0);
        Assert.Equal(0, resultado.encoladas);

        var partida = await Connection.QueryFirstAsync<(short status, decimal debe, decimal haber)>(new CommandDefinition(@"
            SELECT h.status,
                   (SELECT COALESCE(SUM(d.debit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                   (SELECT COALESCE(SUM(d.credit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id)
            FROM public.con_partida_hdr h
            WHERE h.company_id = @CompanyId AND h.document_type = 'LOTE_FAC' AND h.document_id = @LoteId
            LIMIT 1",
            new { CompanyId, LoteId = resultado.lote_id }, Transaction));

        Assert.Equal(1, partida.status);                    // posteada por el motor único
        Assert.Equal(partida.debe, partida.haber);          // balanceada
        Assert.Equal(resultado.total, partida.debe);

        var marcadas = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.con_partida_factura WHERE company_id = @CompanyId AND lote_id = @LoteId",
            new { CompanyId, LoteId = resultado.lote_id }, Transaction));
        Assert.Equal(resultado.facturas, marcadas);
    }

    [SkippableFact]
    public async Task Lote_es_idempotente_segunda_corrida_no_toma_nada()
    {
        var rango = await ArrangeAsync();
        Skip.If(rango is null, "Faltan facturas de lectura o diario/tipo en la BD de pruebas.");

        var primera = await GenerarAsync(rango.Value.desde, rango.Value.hasta);
        Assert.True(primera.facturas > 0);

        var segunda = await GenerarAsync(rango.Value.desde, rango.Value.hasta);
        Assert.Null(segunda.lote_id);
        Assert.Equal(0, segunda.facturas);
        Assert.Equal(0, segunda.polizas);
    }

    [SkippableFact]
    public async Task Lote_excluye_facturas_anuladas()
    {
        var rango = await ArrangeAsync();
        Skip.If(rango is null, "Faltan facturas de lectura o diario/tipo en la BD de pruebas.");

        var anulada = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            UPDATE public.factura SET estado = 'N', estado_id = 3
            WHERE id = (SELECT id FROM public.factura
                        WHERE company_id = @CompanyId AND tipofacturacion = 'S' AND tipofactura = 'F'
                          AND COALESCE(estado_id, 1) <> 3
                        ORDER BY id LIMIT 1)
            RETURNING id",
            new { CompanyId }, Transaction));

        await GenerarAsync(rango.Value.desde, rango.Value.hasta);

        var enPuente = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.con_partida_factura WHERE company_id = @CompanyId AND factura_id = @Id",
            new { CompanyId, Id = anulada }, Transaction));
        Assert.Equal(0, enPuente);
    }

    [SkippableFact]
    public async Task Sin_periodo_encola_y_reproceso_marca_procesada()
    {
        var rango = await ArrangeAsync();
        Skip.If(rango is null, "Faltan facturas de lectura o diario/tipo en la BD de pruebas.");

        // Mover una factura a una fecha sin período contable.
        var facturaId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            UPDATE public.factura SET fechaemision = DATE '2031-01-15'
            WHERE id = (SELECT id FROM public.factura
                        WHERE company_id = @CompanyId AND tipofacturacion = 'S' AND tipofactura = 'F'
                          AND COALESCE(estado_id, 1) <> 3
                        ORDER BY id LIMIT 1)
            RETURNING id",
            new { CompanyId }, Transaction));

        // Modo DIA para que la fecha del grupo (y de la pendiente) sea la de emisión.
        var encolado = await GenerarAsync(new DateTime(2031, 1, 1), new DateTime(2031, 1, 31), "DIA");
        Assert.Equal(0, encolado.polizas);
        Assert.True(encolado.encoladas > 0);

        var pendientes = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = 'VENTAS'
              AND origen_tipo = 'LOTE_FACTURACION' AND status_id = 1
              AND fecha_documento = DATE '2031-01-15'",
            new { CompanyId }, Transaction));
        Assert.Equal(1, pendientes);

        // Abrir período y reprocesar: la pendiente queda PROCESADA con póliza.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            VALUES (@CompanyId, '203101-T', 'Periodo test reproceso', DATE '2031-01-01', DATE '2031-01-31', 0, 'OPEN', now(), 'test-f3')",
            new { CompanyId }, Transaction));

        var reproceso = await GenerarAsync(new DateTime(2031, 1, 1), new DateTime(2031, 1, 31), "DIA");
        Assert.True(reproceso.polizas > 0);

        var procesada = await Connection.QueryFirstAsync<(short status, long? poliza)>(new CommandDefinition(@"
            SELECT status_id, poliza_id FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = 'VENTAS'
              AND origen_tipo = 'LOTE_FACTURACION' AND fecha_documento = DATE '2031-01-15'
            ORDER BY partida_pendiente_id DESC LIMIT 1",
            new { CompanyId }, Transaction));
        Assert.Equal(2, procesada.status);
        Assert.NotNull(procesada.poliza);
    }

    [SkippableFact]
    public async Task Sin_periodo_y_sin_encolar_lanza_excepcion()
    {
        var rango = await ArrangeAsync(encolarSinPeriodo: false);
        Skip.If(rango is null, "Faltan facturas de lectura o diario/tipo en la BD de pruebas.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.factura SET fechaemision = DATE '2031-02-10'
            WHERE id = (SELECT id FROM public.factura
                        WHERE company_id = @CompanyId AND tipofacturacion = 'S' AND tipofactura = 'F'
                          AND COALESCE(estado_id, 1) <> 3
                        ORDER BY id LIMIT 1)",
            new { CompanyId }, Transaction));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            GenerarAsync(new DateTime(2031, 2, 1), new DateTime(2031, 2, 28)));
        Assert.Contains("período contable abierto", ex.MessageText);
    }

    [SkippableFact]
    public async Task Snapshot_trigger_llena_categoria_y_medicion_al_emitir()
    {
        var cliente = await Connection.QueryFirstOrDefaultAsync<(string clave, int? categoria, bool medidor)>(new CommandDefinition(@"
            SELECT maestro_cliente_clave, categoria_servicio_id, maestro_cliente_tiene_medidor
            FROM public.cliente_maestro
            WHERE company_id = @CompanyId AND categoria_servicio_id IS NOT NULL
            ORDER BY maestro_cliente_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(cliente.clave is null, "No hay clientes con categoría en la BD de pruebas.");

        var snapshot = await Connection.QueryFirstAsync<(int? categoria, bool? medicion)>(new CommandDefinition(@"
            INSERT INTO public.factura
                (company_id, numfactura, clientecodigo, tipofactura, ano, mes, fechaemision,
                 periodo, saldototal, usuario, estado, estado_id, tipofacturacion)
            VALUES (@CompanyId, 'TEST-F3-SNAP', @Clave, 'F', '2026', '6', current_date,
                    '2026/6', 100, 'test-f3', 'A', 1, 'S')
            RETURNING categoria_servicio_id, con_medicion",
            new { CompanyId, Clave = cliente.clave }, Transaction));

        Assert.Equal(cliente.categoria, snapshot.categoria);
        Assert.Equal(cliente.medidor, snapshot.medicion);
    }
}
