using Dapper;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

/// <summary>
/// Emisión de la factura de una lectura desde el PORTAL (2026-09-03).
///
/// Fija el camino que reproduce <c>EmisionLecturaService</c>, que es el mismo que ya usa la app
/// de campo pero con el folio puesto por el servidor:
///
///   bloque de la "ruta" PORTAL → consumir correlativo → preparar → sp_lectura_v3 → confirmar
///
/// Lo que se protege aquí:
///
///   1. El portal saca folios de un bloque PROPIO, disjunto del de los teléfonos. Es el riesgo
///      caro: si el portal emitiera sobre el mismo rango que reparten los equipos, un teléfono
///      con facturas en la cola imprimiría folios ya usados.
///   2. Emitir dos veces el mismo período está prohibido…
///   3. …salvo que la anterior esté ANULADA, que es exactamente la refacturación: anular con
///      nota de crédito y volver a facturar con la lectura corregida.
///
/// Todo corre dentro de la transacción con ROLLBACK del IntegrationTestBase: ni la factura ni el
/// correlativo consumido sobreviven al test. Se comprobó que ningún SP de este camino usa dblink
/// ni transacciones autónomas, así que el ROLLBACK realmente los deshace.
/// </summary>
[Collection("Postgres")]
public sealed class EmisionLecturaPortalTests : IntegrationTestBase
{
    private const string RutaPortal = "PORTAL";
    private const short TipoDocumentoFactura = 1;

    public EmisionLecturaPortalTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record Bloque
    {
        public long cai_bloque_id { get; init; }
        public long cai_id { get; init; }
        public string? codigo_cai { get; init; }
        public string? prefijo_documento { get; init; }
        public long correlativo_desde { get; init; }
        public long correlativo_hasta { get; init; }
        public long correlativo_actual { get; init; }
        public long correlativo_siguiente { get; init; }
        public DateTime? fecha_expiracion { get; init; }
        public string? estado_codigo { get; init; }
    }

    private sealed record Folio
    {
        public long cai_id { get; init; }
        public long correlativo { get; init; }
        public string numero_factura { get; init; } = string.Empty;
        public string? prefijo_documento { get; init; }
        public string? codigo_cai { get; init; }
    }

    // Por propiedades y no posicional: el record posicional obliga a Dapper a encontrar un
    // constructor con los tipos EXACTOS de las columnas, y maestro_cliente_id llega como int32.
    private sealed record Candidato
    {
        public long cliente_id { get; init; }
        public string clave { get; init; } = string.Empty;
        public string? contador { get; init; }
    }

    // -------------------------------------------------------------------------

    private async Task<Bloque> ObtenerBloqueAsync(string ruta)
    {
        var bloque = await Connection.QueryFirstOrDefaultAsync<Bloque>(new CommandDefinition(@"
            select * from public.sp_adm_obtener_o_reservar_bloque_cai_ruta(
                p_company_id => @c, p_ruta_codigo => @ruta, p_cantidad => 50,
                p_usuario => 'test', p_tipo_documento_fiscal_id => @tipo)",
            new { c = CompanyId, ruta, tipo = TipoDocumentoFactura }, Transaction));

        Skip.If(bloque is null, "No hay CAI vigente de facturación en esta BD.");
        return bloque!;
    }

    /// <summary>
    /// Abonado activo, con servicios base y SIN factura viva en el período: es el único caso en
    /// el que la emisión puede prosperar.
    /// </summary>
    private async Task<Candidato> BuscarCandidatoAsync(int anio, int mes)
    {
        var candidato = await Connection.QueryFirstOrDefaultAsync<Candidato>(new CommandDefinition(@"
            select cm.maestro_cliente_id as cliente_id,
                   cm.maestro_cliente_clave as clave,
                   cm.contador
            from public.cliente_maestro cm
            where cm.company_id = @c
              and cm.estado = true
              and exists (select 1 from public.adm_cliente_servicio s
                          where s.company_id = @c and s.cliente_id = cm.maestro_cliente_id)
              and not exists (select 1 from public.factura f
                              where f.company_id = @c
                                and f.clientecodigo = cm.maestro_cliente_clave
                                and f.ano = @anio::text and f.mes = @mes::text
                                and coalesce(f.estado, '') <> 'N')
            limit 1",
            new { c = CompanyId, anio, mes }, Transaction));

        Skip.If(candidato is null,
            $"No hay ningún abonado sin factura viva en {mes:00}/{anio} en esta BD.");
        return candidato!;
    }

    private async Task<int?> PeriodoAbiertoAsync()
    {
        // sp_lectura_v3 exige período abierto; sin uno, el test no prueba nada.
        var fila = await Connection.QueryFirstOrDefaultAsync<(int anio, int mes)?>(
            new CommandDefinition(@"
                select anio, mes from public.adm_periodo_comercial
                where company_id = @c and fecha_cierre is null
                order by anio desc, mes desc limit 1",
                new { c = CompanyId }, Transaction));

        if (fila is null)
        {
            return null;
        }

        _periodo = fila.Value;
        return fila.Value.mes;
    }

    private (int anio, int mes) _periodo;

    private async Task<Folio> ConsumirFolioAsync(long bloqueId, long clienteId, string uuid)
    {
        var folio = await Connection.QueryFirstOrDefaultAsync<Folio>(new CommandDefinition(@"
            select * from public.sp_adm_consumir_correlativo_bloque_cai(
                p_company_id => @c, p_cai_bloque_id => @b, p_cliente_id => @cli,
                p_lectura_uuid => @uuid, p_usuario => 'test')",
            new { c = CompanyId, b = bloqueId, cli = clienteId, uuid }, Transaction));

        Assert.NotNull(folio);
        return folio!;
    }

    private async Task<dynamic?> EmitirAsync(Candidato cliente, Folio folio, string uuid,
        int anio, int mes, decimal? lectura)
    {
        await Connection.QueryFirstOrDefaultAsync(new CommandDefinition(@"
            select * from public.sp_adm_prepare_correlativo_cai_sync(
                p_company_id => @c, p_cliente_id => @cli, p_id_cai => @cai,
                p_correlativo => @corr, p_numero_factura => @num,
                p_lectura_uuid => @uuid, p_usuario => 'test')",
            new
            {
                c = CompanyId,
                cli = cliente.cliente_id,
                cai = folio.cai_id,
                corr = folio.correlativo,
                num = folio.numero_factura,
                uuid,
            }, Transaction));

        return await Connection.QueryFirstOrDefaultAsync(new CommandDefinition(@"
            select * from public.sp_lectura_v3(
                p_company_id => @c, p_anio => @anio, p_mes => @mes, p_ciclo => NULL,
                p_clave => @clave, p_contador => @contador, p_fecha_lectura => current_date,
                p_usuario => 'test', p_lectura_actual => @lectura::numeric,
                p_ser3 => 'N', p_ser4 => 'N', p_observacion => 'prueba automatizada',
                p_condicion_lectura => 'N', p_lectura_promedio => NULL,
                p_numero_factura => @num, p_correlativo_cai => @corr::int,
                p_id_cai => @cai::int, p_tienemedidor => @medidor,
                p_informativo => 'N', p_imagen => NULL, p_categoria => '0',
                p_lectura_uuid => @uuid)",
            new
            {
                c = CompanyId,
                anio,
                mes,
                clave = cliente.clave,
                contador = cliente.contador,
                lectura,
                num = folio.numero_factura,
                corr = folio.correlativo,
                cai = folio.cai_id,
                medidor = string.IsNullOrWhiteSpace(cliente.contador) ? "N" : "S",
                uuid,
            }, Transaction));
    }

    // -------------------------------------------------------------------------
    // Lo que se protege
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task El_bloque_del_portal_no_se_solapa_con_el_de_ninguna_ruta()
    {
        var portal = await ObtenerBloqueAsync(RutaPortal);

        var solapados = await Connection.QueryAsync<string>(new CommandDefinition(@"
            select coalesce(b.ruta_codigo, '(sin ruta)')
            from public.adm_cai_bloque_reservado b
            where b.company_id = @c
              and b.cai_id = @cai
              and b.cai_bloque_id <> @bloque
              and coalesce(b.ruta_codigo, '') <> @ruta
              and b.correlativo_desde <= @hasta
              and b.correlativo_hasta >= @desde",
            new
            {
                c = CompanyId,
                cai = portal.cai_id,
                bloque = portal.cai_bloque_id,
                ruta = RutaPortal,
                desde = portal.correlativo_desde,
                hasta = portal.correlativo_hasta,
            }, Transaction));

        var lista = solapados.ToList();
        Assert.True(lista.Count == 0,
            "El bloque del portal (" + portal.correlativo_desde + "-" + portal.correlativo_hasta +
            ") se solapa con el de: " + string.Join(", ", lista) +
            ". Un teléfono con facturas en cola imprimiría folios que el portal ya usó.");
    }

    [SkippableFact]
    public async Task Emite_la_factura_y_consume_un_folio_del_bloque_del_portal()
    {
        Skip.If(await PeriodoAbiertoAsync() is null, "No hay período comercial abierto.");
        var (anio, mes) = _periodo;

        var cliente = await BuscarCandidatoAsync(anio, mes);
        var bloque = await ObtenerBloqueAsync(RutaPortal);
        var uuid = Guid.NewGuid().ToString();

        var folio = await ConsumirFolioAsync(bloque.cai_bloque_id, cliente.cliente_id, uuid);

        // El folio sale DEL bloque del portal, no de cualquier parte del rango CAI.
        Assert.InRange(folio.correlativo, bloque.correlativo_desde, bloque.correlativo_hasta);

        var fila = await EmitirAsync(cliente, folio, uuid, anio, mes, lectura: null);
        Assert.NotNull(fila);

        var resultado = (IDictionary<string, object?>)fila!;
        Assert.True((bool)(resultado["success"] ?? false),
            "sp_lectura_v3 no emitió: " + resultado["mensaje"]);

        var facturaId = Convert.ToInt64(resultado["factura_id"]);
        Assert.True(facturaId > 0);

        // La factura quedó realmente escrita, con el número del folio y activa.
        var guardada = await Connection.QueryFirstOrDefaultAsync<(string numfactura, string estado)?>(
            new CommandDefinition(
                "select numfactura, coalesce(estado,'A') from public.factura where id = @id and company_id = @c",
                new { id = facturaId, c = CompanyId }, Transaction));

        Assert.NotNull(guardada);
        Assert.Equal(folio.numero_factura, guardada!.Value.numfactura);
        Assert.Equal("A", guardada.Value.estado);
    }

    [SkippableFact]
    public async Task No_deja_facturar_dos_veces_el_mismo_periodo_pero_si_tras_anular()
    {
        Skip.If(await PeriodoAbiertoAsync() is null, "No hay período comercial abierto.");
        var (anio, mes) = _periodo;

        var cliente = await BuscarCandidatoAsync(anio, mes);
        var bloque = await ObtenerBloqueAsync(RutaPortal);

        // --- primera emisión: pasa
        var uuid1 = Guid.NewGuid().ToString();
        var folio1 = await ConsumirFolioAsync(bloque.cai_bloque_id, cliente.cliente_id, uuid1);
        var primera = (IDictionary<string, object?>?)await EmitirAsync(cliente, folio1, uuid1, anio, mes, null);
        Skip.If(primera is null || !(bool)(primera["success"] ?? false),
            "La primera emisión no prosperó; el resto del test no aplica.");
        var facturaId = Convert.ToInt64(primera!["factura_id"]);

        // --- segunda emisión sobre el mismo período: debe rechazarse
        //
        // El RAISE aborta la transacción del test, y sin un SAVEPOINT todo lo que viene después
        // moriría con 25P02. En el servicio no ocurre: cada llamada a un SP va en su propia
        // transacción implícita, así que un rechazo no envenena nada.
        var uuid2 = Guid.NewGuid().ToString();
        var folio2 = await ConsumirFolioAsync(bloque.cai_bloque_id, cliente.cliente_id, uuid2);

        await Connection.ExecuteAsync(new CommandDefinition(
            "savepoint antes_de_la_repetida", transaction: Transaction));

        var repetida = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => EmitirAsync(cliente, folio2, uuid2, anio, mes, null));
        Assert.Contains("FACTURA_YA_EMITIDA", repetida.MessageText ?? repetida.Message);

        await Connection.ExecuteAsync(new CommandDefinition(
            "rollback to savepoint antes_de_la_repetida", transaction: Transaction));

        // --- se anula la primera (es lo que hace la nota de crédito por el total)…
        await Connection.ExecuteAsync(new CommandDefinition(
            "update public.factura set estado = 'N', estado_id = 3 where id = @id and company_id = @c",
            new { id = facturaId, c = CompanyId }, Transaction));

        // …y ahora sí se puede refacturar con la lectura corregida, con folio NUEVO.
        var uuid3 = Guid.NewGuid().ToString();
        var folio3 = await ConsumirFolioAsync(bloque.cai_bloque_id, cliente.cliente_id, uuid3);
        Assert.NotEqual(folio1.correlativo, folio3.correlativo);   // el folio no se recicla

        var refacturada = (IDictionary<string, object?>?)await EmitirAsync(cliente, folio3, uuid3, anio, mes, null);
        Assert.NotNull(refacturada);
        Assert.True((bool)(refacturada!["success"] ?? false),
            "Tras anular, la refacturación debería prosperar: " + refacturada["mensaje"]);
        Assert.NotEqual(facturaId, Convert.ToInt64(refacturada["factura_id"]));
    }
}
