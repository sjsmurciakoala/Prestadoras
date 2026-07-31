using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// F8 — WS bancario (contrato SIMAFI congelado): pago FIFO, idempotencia por
// referencia, encolado sin período y reversión, directo sobre los SPs
// sp_ban_ws_pagar / sp_ban_ws_reversar (Database/ddl_v3/20260704_ci_fase8_ws_bancario.sql).
// Requiere los scripts F1..F8 aplicados en la BD de pruebas.
[Collection("Postgres")]
public sealed class BancosWsSqlTests : IntegrationTestBase
{
    public BancosWsSqlTests(PostgresFixture fixture) : base(fixture) { }

    private const string Banco = "098";
    private const string Clave = "099999901";

    /// <summary>
    /// Config mínima dentro de la transacción: perfil ERSAPS, activo_bancos,
    /// asiento del módulo BANCOS y (opcional) período contable abierto.
    /// Devuelve la cuenta bancaria usable o null si la BD no da para el arrange.
    /// </summary>
    private async Task<long?> ArrangeAsync(bool conPeriodo = true, bool encolarSinPeriodo = true)
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-f8')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET activo_bancos = true, encolar_sin_periodo = @Encolar
            WHERE company_id = @CompanyId",
            new { CompanyId, Encolar = encolarSinPeriodo }, Transaction));

        var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            SELECT @CompanyId, 'BANCOS',
                   (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                   (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                   'test-f8'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId }, Transaction));
        if (!asientoOk)
        {
            return null;
        }

        if (conPeriodo)
        {
            await Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_periodo_contable
                    (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
                SELECT @CompanyId, 'F8-TEST', 'Periodo test F8',
                       current_date - 5, current_date + 25, 0, 'OPEN', now(), 'test-f8'
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.con_periodo_contable p
                    WHERE p.company_id = @CompanyId AND COALESCE(p.status_id, 2) = 0
                      AND current_date BETWEEN p.start_date::date AND p.end_date::date)",
                new { CompanyId }, Transaction));
        }
        else
        {
            // Cierra cualquier período que cubra hoy para forzar el encolado.
            await Connection.ExecuteAsync(new CommandDefinition(@"
                UPDATE public.con_periodo_contable
                SET status_id = 2, status = 'CLOSED'
                WHERE company_id = @CompanyId AND COALESCE(status_id, 2) = 0
                  AND current_date BETWEEN start_date::date AND end_date::date",
                new { CompanyId }, Transaction));
        }

        var cuentaId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT banco_cuenta_id FROM public.ban_cuenta
            WHERE company_id = @CompanyId AND cont_account_id IS NOT NULL AND activo
            ORDER BY banco_cuenta_id LIMIT 1",
            new { CompanyId }, Transaction));

        var tieneDep = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM public.ban_tipos_transacciones WHERE company_id = @CompanyId AND tipo_transaccion = 'DEP')",
            new { CompanyId }, Transaction));

        return tieneDep ? cuentaId : null;
    }

    private readonly List<int> _numRecibos = [];

    /// <summary>Fabrica un cliente con facturas pendientes (montos por factura, FIFO por fecha).</summary>
    private async Task<long[]> CrearClienteConFacturasAsync(params decimal[] montos)
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@CompanyId, @Clave, '0000000000000', 'CLIENTE TEST F8', true)",
            new { CompanyId, Clave }, Transaction));

        var facturas = new long[montos.Length];
        for (var i = 0; i < montos.Length; i++)
        {
            // numrecibo es identity GENERATED ALWAYS: lo asigna la BD.
            var fila = await Connection.QueryFirstAsync<(long id, int numrecibo)>(new CommandDefinition(@"
                INSERT INTO public.factura
                    (company_id, numfactura, clientecodigo, tipofactura, tipofacturacion,
                     fechaemision, periodo, saldototal, usuario, estado, estado_id)
                VALUES (@CompanyId, @NumFactura, @Clave, 'F', 'S',
                        current_date - @Antiguedad, '2026/6', @Total, 'test-f8', 'A', 1)
                RETURNING id, numrecibo",
                new
                {
                    CompanyId,
                    NumFactura = $"TEST-F8-{i:D3}",
                    Clave,
                    Antiguedad = montos.Length - i,   // la primera es la más vieja
                    Total = montos[i],
                }, Transaction));
            facturas[i] = fila.id;
            _numRecibos.Add(fila.numrecibo);

            // Dos líneas por factura (70% agua / 30% alcantarillado) para que el
            // derrame y la resolución analítica crucen líneas y servicios.
            var agua = Math.Round(montos[i] * 0.7m, 2);
            var alca = montos[i] - agua;
            await Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.factura_detalle
                    (company_id, factura_id, codigo, tiposervicio, descripcion, montovalor, montovalor_saldo)
                VALUES (@CompanyId, @FacturaId, '', 'AGUA_POTABLE', 'Agua Potable', @Agua, @Agua),
                       (@CompanyId, @FacturaId, '', 'ALCANTARILLADO', 'Alcantarillado', @Alca, @Alca)",
                new { CompanyId, FacturaId = facturas[i], Agua = agua, Alca = alca }, Transaction));
        }

        return facturas;
    }

    private Task<(string status, long? pagoId, long? polizaId, long? kardexId, decimal? total)> PagarAsync(
        string referencia, decimal monto, long? cuentaId, bool validarMonto = true, string clave = Clave) =>
        Connection.QueryFirstAsync<(string, long?, long?, long?, decimal?)>(new CommandDefinition(@"
            SELECT status, pago_id, poliza_id, ban_kardex_id, total_pendiente
            FROM public.sp_ban_ws_pagar(
                @CompanyId, @Banco, @Referencia, @Clave, @Monto,
                current_date, '10:30:00'::time, current_date, '207', '2138',
                @CuentaId, 'S', @ValidarMonto, 'test-f8')",
            new { CompanyId, Banco, Referencia = referencia, Clave = clave, Monto = monto, CuentaId = cuentaId, ValidarMonto = validarMonto },
            Transaction));

    private Task<(string status, long? pagoId, long? polizaRev, long? kardexRev)> ReversarAsync(string referencia) =>
        Connection.QueryFirstAsync<(string, long?, long?, long?)>(new CommandDefinition(@"
            SELECT status, pago_id, poliza_reverso_id, ban_kardex_reverso_id
            FROM public.sp_ban_ws_reversar(@CompanyId, @Referencia, 'test-f8')",
            new { CompanyId, Referencia = referencia }, Transaction));

    [SkippableFact]
    public async Task Pago_total_aplica_fifo_marca_facturas_y_postea_partida_balanceada()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        var facturas = await CrearClienteConFacturasAsync(100.00m, 50.00m);

        var resultado = await PagarAsync("F8REF001", 150.00m, cuentaId);

        Assert.Equal("OK", resultado.status);
        Assert.NotNull(resultado.pagoId);
        Assert.NotNull(resultado.polizaId);
        Assert.NotNull(resultado.kardexId);
        Assert.Equal(150.00m, resultado.total);

        // Ambas facturas quedan cobradas, con banco y fecha de pago.
        var estados = (await Connection.QueryAsync<(string estado, System.DateTime? fechapago, string recolectora)>(new CommandDefinition(
            "SELECT estado, fechapago, recolectora FROM public.factura WHERE id = ANY(@Ids) ORDER BY id",
            new { Ids = facturas }, Transaction))).ToList();
        Assert.All(estados, e =>
        {
            Assert.Equal("C", e.estado);
            Assert.NotNull(e.fechapago);
            Assert.Equal(Banco, e.recolectora);
        });

        // Saldos de detalle en cero.
        var saldoRestante = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(montovalor_saldo), -1) FROM public.factura_detalle WHERE factura_id = ANY(@Ids)",
            new { Ids = facturas }, Transaction));
        Assert.Equal(0m, saldoRestante);

        // F7 H2c: sin espejo 202 — las aplicaciones del documento del motor
        // cubren ambas facturas por el total cobrado.
        var aplicado = (await Connection.QueryAsync<(int facturaId, decimal monto)>(new CommandDefinition(@"
            SELECT a.factura_id, a.monto_aplicado
            FROM public.adm_pago_aplicacion a
            JOIN public.ban_ws_pago b ON b.adm_pago_id = a.pago_id
            WHERE b.company_id = @CompanyId AND b.pago_id = @PagoId",
            new { CompanyId, PagoId = resultado.pagoId }, Transaction))).ToList();
        Assert.Equal(150.00m, aplicado.Sum(a => a.monto));
        Assert.Equal(2, aplicado.Select(a => a.facturaId).Distinct().Count());
        var espejos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM public.transaccion_abonado WHERE company_id = @CompanyId AND cliente_clave = @Clave",
            new { CompanyId, Clave }, Transaction));
        Assert.Equal(0L, espejos);

        // Partida BANCOS/PGB posteada y balanceada; Debe = cuenta de la ban_cuenta.
        var partida = await Connection.QueryFirstAsync<(short status, decimal debe, decimal haber, long cuentaDebe)>(new CommandDefinition(@"
            SELECT h.status,
                   (SELECT COALESCE(SUM(d.debit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                   (SELECT COALESCE(SUM(d.credit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                   (SELECT d.account_id FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id AND d.debit_amount > 0 LIMIT 1)
            FROM public.con_partida_hdr h
            WHERE h.company_id = @CompanyId AND h.module = 'BANCOS' AND h.document_type = 'PGB'
              AND h.document_id = @PagoId",
            new { CompanyId, PagoId = resultado.pagoId }, Transaction));
        Assert.Equal(1, partida.status);
        Assert.Equal(150.00m, partida.debe);
        Assert.Equal(150.00m, partida.haber);

        var cuentaBanco = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT cont_account_id FROM public.ban_cuenta WHERE banco_cuenta_id = @CuentaId",
            new { CuentaId = cuentaId }, Transaction));
        Assert.Equal(cuentaBanco, partida.cuentaDebe);

        // Kardex DEP por el total, con la partida vinculada.
        var kardex = await Connection.QueryFirstAsync<(decimal monto, long? partidaId)>(new CommandDefinition(
            "SELECT monto, partida_cuenta_id FROM public.ban_kardex WHERE company_id = @CompanyId AND ban_kardex_id = @KardexId",
            new { CompanyId, KardexId = resultado.kardexId }, Transaction));
        Assert.Equal(150.00m, kardex.monto);
        Assert.Equal(resultado.polizaId, kardex.partidaId);

        // Bitácora del canal.
        var pagoRow = await Connection.QueryFirstAsync<(short status, string clave, decimal monto)>(new CommandDefinition(
            "SELECT status_id, clave, monto FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F8REF001'",
            new { CompanyId }, Transaction));
        Assert.Equal(1, pagoRow.status);
        Assert.Equal(Clave, pagoRow.clave);
        Assert.Equal(150.00m, pagoRow.monto);
    }

    [SkippableFact]
    public async Task Pago_replay_devuelve_lo_mismo_y_no_duplica_nada()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(80.00m);

        var primero = await PagarAsync("F8REF002", 80.00m, cuentaId);
        Assert.Equal("OK", primero.status);

        // Replay con CUALQUIER monto: manda la idempotencia, no la validación.
        var replay = await PagarAsync("F8REF002", 999.99m, cuentaId);

        Assert.Equal("IDEMPOTENTE", replay.status);
        Assert.Equal(primero.pagoId, replay.pagoId);
        Assert.Equal(primero.polizaId, replay.polizaId);
        Assert.Equal(primero.kardexId, replay.kardexId);

        var conteos = await Connection.QueryFirstAsync<(long pagos, long transacciones, long polizas, long kardex)>(new CommandDefinition(@"
            SELECT (SELECT COUNT(*) FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F8REF002'),
                   (SELECT COUNT(*) FROM public.adm_pago p2 JOIN public.ban_ws_pago b2 ON b2.adm_pago_id = p2.pago_id WHERE b2.company_id = @CompanyId AND b2.pago_id = @PagoId),
                   (SELECT COUNT(*) FROM public.con_partida_hdr WHERE company_id = @CompanyId AND module = 'BANCOS' AND document_type = 'PGB' AND document_id = @PagoId),
                   (SELECT COUNT(*) FROM public.ban_kardex WHERE company_id = @CompanyId AND ban_kardex_id = @KardexId)",
            new { CompanyId, PagoId = primero.pagoId, KardexId = primero.kardexId }, Transaction));
        Assert.Equal(1, conteos.pagos);
        Assert.Equal(1, conteos.transacciones);   // F7: un solo documento del motor, sin duplicar
        Assert.Equal(1, conteos.polizas);
        Assert.Equal(1, conteos.kardex);
    }

    [SkippableFact]
    public async Task Pago_con_monto_distinto_rechaza_sin_escribir()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        var facturas = await CrearClienteConFacturasAsync(120.00m);

        var resultado = await PagarAsync("F8REF003", 100.00m, cuentaId);

        Assert.Equal("MONTO_NO_COINCIDE", resultado.status);
        Assert.Equal(120.00m, resultado.total);
        Assert.Null(resultado.pagoId);

        var intacto = await Connection.QueryFirstAsync<(string estado, decimal saldo, long registros)>(new CommandDefinition(@"
            SELECT (SELECT estado FROM public.factura WHERE id = @FacturaId),
                   (SELECT SUM(montovalor_saldo) FROM public.factura_detalle WHERE factura_id = @FacturaId),
                   (SELECT COUNT(*) FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F8REF003')",
            new { CompanyId, FacturaId = facturas[0] }, Transaction));
        Assert.Equal("A", intacto.estado);
        Assert.Equal(120.00m, intacto.saldo);
        Assert.Equal(0, intacto.registros);
    }

    [SkippableFact]
    public async Task Pago_parcial_sin_validacion_derrama_fifo_y_deja_parcial_la_ultima()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        var facturas = await CrearClienteConFacturasAsync(100.00m, 50.00m);

        // Paga 120 de 150 (parcial = abono, ruta /pago/otros sin validación de monto).
        var resultado = await PagarAsync("F8REF004", 120.00m, cuentaId, validarMonto: false);
        Assert.Equal("OK", resultado.status);

        var estadoVieja = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT estado FROM public.factura WHERE id = @Id", new { Id = facturas[0] }, Transaction));
        var (estadoNueva, saldoNueva) = await Connection.QueryFirstAsync<(string, decimal)>(new CommandDefinition(@"
            SELECT f.estado, (SELECT SUM(d.montovalor_saldo) FROM public.factura_detalle d WHERE d.factura_id = f.id)
            FROM public.factura f WHERE f.id = @Id", new { Id = facturas[1] }, Transaction));

        Assert.Equal("C", estadoVieja);
        Assert.Equal("B", estadoNueva);
        Assert.Equal(30.00m, saldoNueva);

        // F7 H2c: los montos aplicados salen del documento del motor.
        var aplicadoPorFactura = (await Connection.QueryAsync<decimal>(new CommandDefinition(@"
            SELECT SUM(a.monto_aplicado)
            FROM public.adm_pago_aplicacion a
            JOIN public.ban_ws_pago b ON b.adm_pago_id = a.pago_id
            WHERE b.company_id = @CompanyId AND b.pago_id = @PagoId
            GROUP BY a.factura_id
            ORDER BY SUM(a.monto_aplicado) DESC",
            new { CompanyId, PagoId = resultado.pagoId }, Transaction))).ToList();
        Assert.Equal([100.00m, 20.00m], aplicadoPorFactura);
    }

    [SkippableFact]
    public async Task Pago_sin_periodo_abierto_encola_en_partida_pendiente()
    {
        var cuentaId = await ArrangeAsync(conPeriodo: false);
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(60.00m);

        var resultado = await PagarAsync("F8REF005", 60.00m, cuentaId);

        // El pago del banco NUNCA se rechaza por falta de período: la partida
        // queda en la cola (encolar_sin_periodo, F4/D10).
        Assert.Equal("OK", resultado.status);
        Assert.Null(resultado.polizaId);
        Assert.NotNull(resultado.kardexId);

        var pendiente = await Connection.QueryFirstAsync<(short status, string modulo)>(new CommandDefinition(@"
            SELECT status_id, module FROM public.con_partida_pendiente
            WHERE company_id = @CompanyId AND module = 'BANCOS' AND origen_id = @PagoId
            ORDER BY partida_pendiente_id DESC LIMIT 1",
            new { CompanyId, PagoId = resultado.pagoId }, Transaction));
        Assert.Equal(1, pendiente.status);
        Assert.Equal("BANCOS", pendiente.modulo);
    }

    [SkippableFact]
    public async Task Pago_cliente_inexistente_o_sin_pendientes_rechaza()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");

        var sinRegistro = await PagarAsync("F8REF006", 10.00m, cuentaId, clave: "000000000");
        Assert.Equal("SIN_REGISTRO", sinRegistro.status);

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@CompanyId, @Clave, '0000000000000', 'CLIENTE SIN DEUDA F8', true)",
            new { CompanyId, Clave }, Transaction));

        var sinPendientes = await PagarAsync("F8REF007", 10.00m, cuentaId);
        Assert.Equal("SIN_PENDIENTES", sinPendientes.status);
    }

    [SkippableFact]
    public async Task Pago_resuelve_la_cuenta_desde_la_credencial()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(40.00m);

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.ban_ws_credencial (company_id, banco, nombre, llave, banco_cuenta_id, created_by)
            VALUES (@CompanyId, @Banco, 'BANCO TEST F8', 'LLAVETESTF8XX', @CuentaId, 'test-f8')
            ON CONFLICT (company_id, banco) DO UPDATE SET banco_cuenta_id = EXCLUDED.banco_cuenta_id, activo = true",
            new { CompanyId, Banco, CuentaId = cuentaId }, Transaction));

        // p_banco_cuenta_id NULL → el SP toma la cuenta de la credencial.
        var resultado = await PagarAsync("F8REF008", 40.00m, cuentaId: null);

        Assert.Equal("OK", resultado.status);
        Assert.NotNull(resultado.kardexId);

        var cuentaUsada = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT banco_cuenta_id FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F8REF008'",
            new { CompanyId }, Transaction));
        Assert.Equal(cuentaId, cuentaUsada);
    }

    [SkippableFact]
    public async Task Reversion_restituye_saldos_anula_transacciones_y_reversa_partida()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        var facturas = await CrearClienteConFacturasAsync(100.00m, 50.00m);

        var pago = await PagarAsync("F8REF009", 150.00m, cuentaId);
        Assert.Equal("OK", pago.status);

        var reverso = await ReversarAsync("F8REF009");

        Assert.Equal("OK", reverso.status);
        Assert.Equal(pago.pagoId, reverso.pagoId);
        Assert.Equal(pago.polizaId, reverso.polizaRev);
        Assert.NotNull(reverso.kardexRev);

        // Saldos de detalle restaurados EXACTOS y facturas de vuelta a abiertas.
        var facturasPost = (await Connection.QueryAsync<(string estado, System.DateTime? fechapago, decimal saldo, decimal total)>(new CommandDefinition(@"
            SELECT f.estado, f.fechapago,
                   (SELECT SUM(d.montovalor_saldo) FROM public.factura_detalle d WHERE d.factura_id = f.id),
                   (SELECT SUM(d.montovalor) FROM public.factura_detalle d WHERE d.factura_id = f.id)
            FROM public.factura f WHERE f.id = ANY(@Ids) ORDER BY f.id",
            new { Ids = facturas }, Transaction))).ToList();
        Assert.All(facturasPost, f =>
        {
            Assert.Equal("A", f.estado);
            Assert.Null(f.fechapago);
            Assert.Equal(f.total, f.saldo);
        });

        // F7 H2c: el documento del motor queda REVERSADO, con sus aplicaciones
        // conservadas como auditoría (ya no hay transacciones espejo).
        var (estadoDoc, aplicaciones) = await Connection.QueryFirstAsync<(short, long)>(new CommandDefinition(@"
            SELECT ap.estado_id,
                   (SELECT COUNT(*) FROM public.adm_pago_aplicacion a WHERE a.pago_id = ap.pago_id)
            FROM public.adm_pago ap
            JOIN public.ban_ws_pago b ON b.adm_pago_id = ap.pago_id
            WHERE b.company_id = @CompanyId AND b.pago_id = @PagoId",
            new { CompanyId, PagoId = pago.pagoId }, Transaction));
        Assert.Equal(4, estadoDoc);          // REVERSADO
        Assert.True(aplicaciones > 0);

        // La partida original quedó revertida (el motor la saca de status POSTED).
        var statusPartida = await Connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT status FROM public.con_partida_hdr WHERE company_id = @CompanyId AND poliza_id = @PolizaId",
            new { CompanyId, PolizaId = pago.polizaId }, Transaction));
        Assert.NotEqual(1, statusPartida);

        // Bitácora en estado REVERSADO con el contramovimiento de kardex.
        var pagoRow = await Connection.QueryFirstAsync<(short status, long? kardexRev)>(new CommandDefinition(
            "SELECT status_id, ban_kardex_reverso_id FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F8REF009'",
            new { CompanyId }, Transaction));
        Assert.Equal(2, pagoRow.status);
        Assert.Equal(reverso.kardexRev, pagoRow.kardexRev);
    }

    [SkippableFact]
    public async Task Reversion_inexistente_doble_reversion_y_repago_de_reversada()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(75.00m);

        var noExiste = await ReversarAsync("F8NOEXISTE");
        Assert.Equal("NO_EXISTE", noExiste.status);

        var pago = await PagarAsync("F8REF010", 75.00m, cuentaId);
        Assert.Equal("OK", pago.status);

        Assert.Equal("OK", (await ReversarAsync("F8REF010")).status);
        Assert.Equal("YA_REVERSADA", (await ReversarAsync("F8REF010")).status);

        // Una referencia reversada no se puede volver a pagar (intención del
        // existeReferencia(ref,'R') del WS viejo, acá sí efectiva).
        var repago = await PagarAsync("F8REF010", 75.00m, cuentaId);
        Assert.Equal("REFERENCIA_REVERSADA", repago.status);
    }

    [SkippableFact]
    public async Task Pago_otros_sobre_el_total_rechaza_sin_escribir()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(100.00m);

        // /pago/otros (validarMonto=false) permite parcial pero NO sobrepago: un
        // excedente descuadraría kardex vs contabilidad (no hay saldo a favor).
        var resultado = await PagarAsync("F8REF012", 150.00m, cuentaId, validarMonto: false);

        Assert.Equal("MONTO_NO_COINCIDE", resultado.status);
        Assert.Null(resultado.pagoId);
        var registros = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F8REF012'",
            new { CompanyId }, Transaction));
        Assert.Equal(0, registros);
    }

    [SkippableFact]
    public async Task Idempotencia_con_clave_distinta_no_liquida_al_cliente_equivocado()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(90.00m);

        var primero = await PagarAsync("F8DUPREF", 90.00m, cuentaId);
        Assert.Equal("OK", primero.status);

        // Un segundo cliente con la MISMA referencia (banco reusa el número): NO
        // debe responder "Pago exitoso" sin aplicar nada — se rechaza.
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@CompanyId, '099999903', '0000000000000', 'OTRO CLIENTE F8', true)",
            new { CompanyId }, Transaction));
        var facturaId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura, tipofacturacion,
                fechaemision, periodo, saldototal, usuario, estado, estado_id)
            VALUES (@CompanyId, 'TEST-F8-OTRO', '099999903', 'F', 'S', current_date, '2026/6', 70, 'test-f8', 'A', 1)
            RETURNING id", new { CompanyId }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, descripcion, montovalor, montovalor_saldo)
            VALUES (@CompanyId, @FacturaId, '', 'AGUA_POTABLE', 'Agua Potable', 70, 70)",
            new { CompanyId, FacturaId = facturaId }, Transaction));

        var duplicada = await PagarAsync("F8DUPREF", 70.00m, cuentaId, clave: "099999903");
        Assert.Equal("REFERENCIA_EN_USO", duplicada.status);

        // El segundo cliente sigue debiendo (no se liquidó).
        var estado = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT estado FROM public.factura WHERE id = @Id", new { Id = facturaId }, Transaction));
        Assert.Equal("A", estado);
    }

    [SkippableFact]
    public async Task Consulta_y_pago_comparten_la_fuente_unica_de_pendientes()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(33.33m, 66.67m);

        var totalConsulta = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(saldo), 0) FROM public.fn_ban_ws_pendientes(@CompanyId, @Clave)",
            new { CompanyId, Clave }, Transaction));

        // El monto que acepta el pago es EXACTAMENTE el que devuelve la consulta.
        var resultado = await PagarAsync("F8REF011", totalConsulta, cuentaId);
        Assert.Equal("OK", resultado.status);
        Assert.Equal(100.00m, totalConsulta);
    }

    // ------------------------------------------------------------------------
    // Unificación cobranza F5 (2026-07-28): el pago del WS queda ADEMÁS como
    // documento del motor único (adm_pago canal 2 + adm_pago_aplicacion por
    // línea, link 1:1 en ban_ws_pago.adm_pago_id). La fila legacy 202 sigue
    // hasta F7. El contrato XML no cambia (golden files verbatim).
    // ------------------------------------------------------------------------

    [SkippableFact]
    public async Task Pago_registra_documento_adm_pago_del_motor()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        var facturas = await CrearClienteConFacturasAsync(100.00m, 50.00m);

        var resultado = await PagarAsync("F5REF001", 150.00m, cuentaId);
        Assert.Equal("OK", resultado.status);

        // ban_ws_pago (bitácora del canal) enlaza 1:1 al documento del motor.
        var admPagoId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT adm_pago_id FROM public.ban_ws_pago WHERE company_id = @CompanyId AND referencia = 'F5REF001'",
            new { CompanyId }, Transaction));
        Assert.NotNull(admPagoId);

        // Cabecera del motor: canal banco, PAGO_BANCO, APLICADO, con folio,
        // kardex/póliza compartidos con el canal y la referencia del banco.
        var pago = await Connection.QueryFirstAsync<(short canal, short tipo, short estado, decimal monto,
            string forma, string numeroRecibo, string referencia, long? kardexId, long? polizaId)>(new CommandDefinition(@"
            SELECT canal_id, tipo_transaccion_id, estado_id, monto_total, forma_pago,
                   numero_recibo, referencia_externa, ban_kardex_id, poliza_id
            FROM public.adm_pago WHERE company_id = @CompanyId AND pago_id = @Id",
            new { CompanyId, Id = admPagoId }, Transaction));
        Assert.Equal(2, pago.canal);
        Assert.Equal(3, pago.tipo);         // PAGO_BANCO
        Assert.Equal(1, pago.estado);       // APLICADO
        Assert.Equal(150.00m, pago.monto);
        Assert.Equal("BANCO", pago.forma);
        Assert.False(string.IsNullOrWhiteSpace(pago.numeroRecibo));
        Assert.Equal("F5REF001", pago.referencia);
        Assert.Equal(resultado.kardexId, pago.kardexId);
        Assert.Equal(resultado.polizaId, pago.polizaId);

        // Una aplicación por LÍNEA cobrada (2 facturas × 2 líneas agua/alca del
        // helper) que suma el monto y referencia documento_tipo factura.
        var aplicaciones = (await Connection.QueryAsync<(short tipo, int facturaId, int? detalleId, decimal monto)>(new CommandDefinition(@"
            SELECT documento_tipo, factura_id, factura_detalle_id, monto_aplicado
            FROM public.adm_pago_aplicacion
            WHERE company_id = @CompanyId AND pago_id = @Id
            ORDER BY monto_aplicado DESC",
            new { CompanyId, Id = admPagoId }, Transaction))).ToList();
        Assert.Equal(4, aplicaciones.Count);
        Assert.All(aplicaciones, a => Assert.Equal(1, a.tipo));
        Assert.All(aplicaciones, a => Assert.NotNull(a.detalleId));
        Assert.Equal(150.00m, aplicaciones.Sum(a => a.monto));
        Assert.Equal(facturas.OrderBy(f => f).ToArray(),
            aplicaciones.Select(a => (long)a.facturaId).Distinct().OrderBy(f => f).ToArray());

        // Replay idempotente: NO duplica el documento del motor.
        var replay = await PagarAsync("F5REF001", 150.00m, cuentaId);
        Assert.Equal("IDEMPOTENTE", replay.status);
        var pagos = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.adm_pago WHERE company_id = @CompanyId AND referencia_externa = 'F5REF001'",
            new { CompanyId }, Transaction));
        Assert.Equal(1L, pagos);
    }

    [SkippableFact]
    public async Task Reversion_marca_adm_pago_reversado_conservando_aplicaciones()
    {
        var cuentaId = await ArrangeAsync();
        Skip.If(cuentaId is null, "Falta diario/tipo, cuenta bancaria o tipo DEP en la BD de pruebas.");
        await CrearClienteConFacturasAsync(80.00m);

        var pagoResult = await PagarAsync("F5REF002", 80.00m, cuentaId);
        Assert.Equal("OK", pagoResult.status);

        var reverso = await ReversarAsync("F5REF002");
        Assert.Equal("OK", reverso.status);

        var (estado, motivo) = await Connection.QueryFirstAsync<(short, string)>(new CommandDefinition(@"
            SELECT ap.estado_id, ap.motivo_reverso
            FROM public.adm_pago ap
            JOIN public.ban_ws_pago b ON b.adm_pago_id = ap.pago_id
            WHERE b.company_id = @CompanyId AND b.referencia = 'F5REF002'",
            new { CompanyId }, Transaction));
        Assert.Equal(4, estado);            // REVERSADO
        Assert.Contains("F5REF002", motivo);

        // Las aplicaciones se conservan como auditoría (regla del motor: el
        // reverso nunca borra). 1 factura × 2 líneas del helper = 2.
        var aplicaciones = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT COUNT(*) FROM public.adm_pago_aplicacion a
            JOIN public.ban_ws_pago b ON b.adm_pago_id = a.pago_id
            WHERE b.company_id = @CompanyId AND b.referencia = 'F5REF002'",
            new { CompanyId }, Transaction));
        Assert.Equal(2L, aplicaciones);
    }
}
