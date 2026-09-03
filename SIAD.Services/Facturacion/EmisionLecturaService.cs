using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIAD.Core.DTOs.Facturacion;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Facturacion;

/// <summary>
/// Emite facturas de lectura desde el portal.
///
/// Reproduce el mismo camino que ya usa la app de campo (<c>LectoresMobileService</c>): reservar
/// folio → preparar el correlativo → <c>sp_lectura_v3</c> → confirmar. La única diferencia es
/// quién pone el folio: el teléfono lo genera de su bloque descargado y el servidor lo valida;
/// aquí lo entrega el servidor, del bloque del portal.
///
/// El cálculo NO se reimplementa: <c>sp_lectura_v3</c> llama internamente a
/// <c>sp_adm_calcular_factura_lectura</c>, el mismo motor V3 contra el que está clavada la
/// paridad de la app. Un segundo motor aparecería en campo como diferencia de montos, no como
/// error.
/// </summary>
public class EmisionLecturaService : IEmisionLecturaService
{
    /// <summary>
    /// El portal pide folios como si fuera una ruta más. Es lo que evita el choque con los
    /// teléfonos: cada ruta tiene su bloque de correlativos y los bloques no se solapan, así que
    /// el portal nunca imprime un folio que un teléfono lleve en la cola sin subir.
    /// </summary>
    private const string RutaPortal = "PORTAL";

    /// <summary>Tipo de documento fiscal 1 = factura (los mismos rangos CAI que el campo).</summary>
    private const short TipoDocumentoFactura = 1;

    private const int FoliosPorBloque = 250;

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompany;
    private readonly ILogger<EmisionLecturaService> _log;

    public EmisionLecturaService(
        SiadDbContext context,
        ICurrentCompanyService currentCompany,
        ILogger<EmisionLecturaService> log)
    {
        _context = context;
        _currentCompany = currentCompany;
        _log = log;
    }

    public async Task<BloqueCaiPortalDto> ObtenerBloqueAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompany.GetCompanyId();
        var conn = await AbrirAsync(ct);
        return await ObtenerBloqueAsync(conn, companyId, "portal", ct);
    }

    public async Task<PreviewFacturaLecturaDto> PrevisualizarAsync(
        EmitirFacturaLecturaRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _currentCompany.GetCompanyId();
        var clave = (request.Clave ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clave))
        {
            return new PreviewFacturaLecturaDto { Encontrado = false, Mensaje = "Indique la clave del abonado." };
        }

        if (request.Mes is < 1 or > 12)
        {
            return new PreviewFacturaLecturaDto
            {
                Encontrado = false,
                Mensaje = "El mes debe estar entre 1 y 12.",
            };
        }

        var conn = await AbrirAsync(ct);
        var cliente = await ResolverClienteAsync(conn, companyId, clave, ct);
        if (cliente is null)
        {
            return new PreviewFacturaLecturaDto
            {
                Encontrado = false,
                Mensaje = $"No hay un abonado activo con la clave {clave}.",
            };
        }

        var vigente = await FacturaVigenteDelPeriodoAsync(conn, companyId, cliente.ClienteClave,
            request.Anio, request.Mes, ct);

        // Mismo SP que usa la emisión: lo que se ve aquí es lo que va a salir impreso. Se pasa
        // sin folio (NULL en los tres parámetros CAI) porque previsualizar no consume nada.
        const string sql = @"
            select coalesce(cliente_nombre, '')        as ""ClienteNombre"",
                   coalesce(contador, '')              as ""Contador"",
                   coalesce(ciclo, '')                 as ""Ciclo"",
                   coalesce(ruta, '')                  as ""Ruta"",
                   tiene_medidor                       as ""TieneMedidor"",
                   condicion_lectura_aplicada          as ""CondicionLecturaAplicada"",
                   lectura_anterior                    as ""LecturaAnterior"",
                   lectura_actual_efectiva             as ""LecturaActualEfectiva"",
                   consumo_facturable                  as ""ConsumoFacturable"",
                   subtotal_servicios                  as ""SubtotalServicios"",
                   subtotal_ajustes                    as ""SubtotalAjustes"",
                   saldos_anteriores                   as ""SaldosAnteriores"",
                   recargos                            as ""Recargos"",
                   total_factura                       as ""TotalFactura"",
                   fecha_vencimiento                   as ""FechaVencimiento"",
                   warnings_json::text                 as ""WarningsJson""
            from public.sp_adm_calcular_factura_lectura(
                @CompanyId, @Anio, @Mes, @ClienteId, @Contador::varchar, @Fecha::date,
                @LecturaActual::numeric, @Condicion::varchar, @Promedio::numeric,
                @Usuario::varchar, NULL::varchar, NULL::integer, NULL::integer, NULL::varchar,
                'S'::varchar);";

        PreviewCalculo? fila;
        try
        {
            fila = await conn.QueryFirstOrDefaultAsync<PreviewCalculo>(new CommandDefinition(sql, new
            {
                CompanyId = companyId,
                request.Anio,
                request.Mes,
                cliente.ClienteId,
                Contador = NuloSiVacio(request.Contador) ?? NuloSiVacio(cliente.Contador),
                Fecha = request.FechaLectura ?? DateTime.Today,
                request.LecturaActual,
                Condicion = CondicionODefecto(request.CondicionLectura),
                Promedio = request.LecturaPromedio,
                Usuario = "portal",
            }, cancellationToken: ct));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Npgsql.PostgresException ex)
        {
            // Período cerrado, cliente sin servicios, CAI vencido… son condiciones que la
            // pantalla debe explicar, no un 500.
            return new PreviewFacturaLecturaDto
            {
                Encontrado = false,
                ClienteId = cliente.ClienteId,
                ClienteClave = cliente.ClienteClave,
                ClienteNombre = cliente.ClienteNombre,
                FacturaVigente = vigente?.NumeroFactura,
                Mensaje = ex.MessageText ?? ex.Message,
            };
        }

        if (fila is null)
        {
            return new PreviewFacturaLecturaDto
            {
                Encontrado = false,
                ClienteId = cliente.ClienteId,
                ClienteClave = cliente.ClienteClave,
                ClienteNombre = cliente.ClienteNombre,
                FacturaVigente = vigente?.NumeroFactura,
                Mensaje = "El cálculo no devolvió resultado para ese abonado y período.",
            };
        }

        var preview = new PreviewFacturaLecturaDto
        {
            Encontrado = true,
            ClienteId = cliente.ClienteId,
            ClienteClave = cliente.ClienteClave,
            ClienteNombre = string.IsNullOrWhiteSpace(fila.ClienteNombre) ? cliente.ClienteNombre : fila.ClienteNombre,
            Contador = fila.Contador,
            Ciclo = fila.Ciclo,
            Ruta = fila.Ruta,
            TieneMedidor = fila.TieneMedidor,
            FacturaVigente = vigente?.NumeroFactura,
            CondicionLecturaAplicada = fila.CondicionLecturaAplicada,
            LecturaAnterior = fila.LecturaAnterior,
            LecturaActualEfectiva = fila.LecturaActualEfectiva,
            ConsumoFacturable = fila.ConsumoFacturable,
            SubtotalServicios = fila.SubtotalServicios,
            SubtotalAjustes = fila.SubtotalAjustes,
            SaldosAnteriores = fila.SaldosAnteriores,
            Recargos = fila.Recargos,
            TotalFactura = fila.TotalFactura,
            FechaVencimiento = fila.FechaVencimiento,
        };

        AgregarAvisos(preview.Warnings, fila.WarningsJson);
        return preview;
    }

    public async Task<EmitirFacturaLecturaResultado> EmitirAsync(
        EmitirFacturaLecturaRequest request, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await EmitirCoreAsync(request, usuario, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Npgsql.PostgresException ex)
        {
            // Las reglas de negocio de los SP viajan por RAISE. Sin esto salen por el catch-all
            // del controller como un 500 que no le dice nada a quien captura.
            _log.LogWarning(ex, "Emisión de lectura rechazada por la base para la clave {Clave}.", request.Clave);
            return Traducir(ex);
        }
    }

    private async Task<EmitirFacturaLecturaResultado> EmitirCoreAsync(
        EmitirFacturaLecturaRequest request, string usuario, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();

        var clave = (request.Clave ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Fallo("CLAVE_REQUERIDA", "Indique la clave del abonado.");
        }

        if (request.Mes is < 1 or > 12)
        {
            return Fallo("PERIODO_INVALIDO", "El mes debe estar entre 1 y 12.");
        }

        var conn = await AbrirAsync(ct);

        var cliente = await ResolverClienteAsync(conn, companyId, clave, ct);
        if (cliente is null)
        {
            return Fallo("CLIENTE_NO_ENCONTRADO", $"No hay un abonado activo con la clave {clave}.");
        }

        // Se comprueba ANTES de consumir el folio. El SP también lo valida, pero llegar hasta él
        // gastaría un correlativo que después queda emitido sin factura: el folio no se recicla.
        var vigente = await FacturaVigenteDelPeriodoAsync(conn, companyId, cliente.ClienteClave,
            request.Anio, request.Mes, ct);
        if (vigente is not null)
        {
            return Fallo("FACTURA_YA_EMITIDA",
                $"El abonado ya tiene la factura {vigente.NumeroFactura} en {request.Mes:00}/{request.Anio}. " +
                "Anúlela con una nota de crédito antes de volver a facturar.");
        }

        var bloque = await ObtenerBloqueAsync(conn, companyId, usuario, ct);

        // Identifica esta emisión de punta a punta: es la misma llave de idempotencia que usa la
        // app, así que un reintento no duplica ni la factura ni el correlativo.
        var lecturaUuid = Guid.NewGuid().ToString();

        var folio = await ConsumirFolioAsync(conn, companyId, bloque.CaiBloqueId, cliente.ClienteId,
            lecturaUuid, usuario, ct);
        if (folio is null)
        {
            return Fallo("CAI_SIN_FOLIO",
                "El bloque de folios del portal no entregó correlativo. Revise la vigencia del CAI.");
        }

        var preparado = await PrepararCorrelativoAsync(conn, companyId, cliente.ClienteId, folio,
            lecturaUuid, usuario, ct);
        if (!preparado.Success)
        {
            return Fallo(preparado.EstadoCodigo ?? "CAI_PREPARE_FALLIDO",
                preparado.Mensaje ?? "No se pudo reservar el correlativo CAI para esta factura.");
        }

        var resultado = await EjecutarLecturaV3Async(conn, companyId, cliente, request, folio,
            lecturaUuid, usuario, ct);

        if (resultado.Success && resultado.FacturaId > 0)
        {
            // La factura YA existe: que falle la confirmación del correlativo no puede tumbar la
            // respuesta. Se reporta como advertencia y queda en adm_cai_correlativo_emitido.
            var confirmado = await ConfirmarCorrelativoAsync(conn, companyId, cliente.ClienteId,
                folio, lecturaUuid, resultado.FacturaId, usuario, ct);
            if (!confirmado.Success)
            {
                resultado.Codigo = "OK_CON_CONFLICTO_CAI";
                resultado.Warnings.Add(
                    "La factura se emitió, pero falló la confirmación del correlativo CAI: " +
                    (confirmado.Mensaje ?? "sin detalle") + ".");
            }
        }

        return resultado;
    }

    // -------------------------------------------------------------------------
    // Pasos
    // -------------------------------------------------------------------------

    private static async Task<BloqueCaiPortalDto> ObtenerBloqueAsync(
        DbConnection conn, long companyId, string usuario, CancellationToken ct)
    {
        // Devuelve el bloque vivo del portal o reserva uno nuevo si se agotó. No hace falta
        // sembrarlo: la primera emisión lo crea.
        const string sql = @"
            select cai_bloque_id         as ""CaiBloqueId"",
                   cai_id                as ""CaiId"",
                   codigo_cai            as ""CodigoCai"",
                   prefijo_documento     as ""PrefijoDocumento"",
                   correlativo_desde     as ""CorrelativoDesde"",
                   correlativo_hasta     as ""CorrelativoHasta"",
                   correlativo_actual    as ""CorrelativoActual"",
                   correlativo_siguiente as ""CorrelativoSiguiente"",
                   fecha_expiracion      as ""FechaExpiracion"",
                   estado_codigo         as ""EstadoCodigo""
            from public.sp_adm_obtener_o_reservar_bloque_cai_ruta(
                p_company_id => @CompanyId,
                p_ruta_codigo => @Ruta,
                p_cantidad => @Cantidad,
                p_usuario => @Usuario,
                p_tipo_documento_fiscal_id => @Tipo);";

        return await conn.QueryFirstAsync<BloqueCaiPortalDto>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            Ruta = RutaPortal,
            Cantidad = FoliosPorBloque,
            Usuario = usuario,
            Tipo = TipoDocumentoFactura,
        }, cancellationToken: ct));
    }

    private static async Task<FolioCai?> ConsumirFolioAsync(
        DbConnection conn, long companyId, long bloqueId, long clienteId,
        string lecturaUuid, string usuario, CancellationToken ct)
    {
        const string sql = @"
            select cai_id            as ""CaiId"",
                   correlativo       as ""Correlativo"",
                   numero_factura    as ""NumeroFactura"",
                   prefijo_documento as ""PrefijoDocumento"",
                   codigo_cai        as ""CodigoCai""
            from public.sp_adm_consumir_correlativo_bloque_cai(
                p_company_id => @CompanyId, p_cai_bloque_id => @BloqueId,
                p_cliente_id => @ClienteId, p_lectura_uuid => @LecturaUuid,
                p_usuario => @Usuario);";

        return await conn.QueryFirstOrDefaultAsync<FolioCai>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            BloqueId = bloqueId,
            ClienteId = clienteId,
            LecturaUuid = lecturaUuid,
            Usuario = usuario,
        }, cancellationToken: ct));
    }

    private static async Task<CaiSyncRow> PrepararCorrelativoAsync(
        DbConnection conn, long companyId, long clienteId, FolioCai folio,
        string lecturaUuid, string usuario, CancellationToken ct)
    {
        const string sql = @"
            select success       as ""Success"",
                   estado_codigo as ""EstadoCodigo"",
                   cai_bloque_id as ""CaiBloqueId"",
                   factura_id    as ""FacturaId"",
                   mensaje       as ""Mensaje""
            from public.sp_adm_prepare_correlativo_cai_sync(
                p_company_id => @CompanyId, p_cliente_id => @ClienteId, p_id_cai => @IdCai,
                p_correlativo => @Correlativo, p_numero_factura => @NumeroFactura,
                p_lectura_uuid => @LecturaUuid, p_usuario => @Usuario);";

        return await conn.QueryFirstAsync<CaiSyncRow>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            ClienteId = clienteId,
            IdCai = folio.CaiId,
            Correlativo = folio.Correlativo,
            folio.NumeroFactura,
            LecturaUuid = lecturaUuid,
            Usuario = usuario,
        }, cancellationToken: ct));
    }

    private static async Task<CaiSyncRow> ConfirmarCorrelativoAsync(
        DbConnection conn, long companyId, long clienteId, FolioCai folio,
        string lecturaUuid, long facturaId, string usuario, CancellationToken ct)
    {
        const string sql = @"
            select success       as ""Success"",
                   estado_codigo as ""EstadoCodigo"",
                   cai_bloque_id as ""CaiBloqueId"",
                   factura_id    as ""FacturaId"",
                   mensaje       as ""Mensaje""
            from public.sp_adm_confirmar_correlativo_cai_sync(
                p_company_id => @CompanyId, p_cliente_id => @ClienteId, p_id_cai => @IdCai,
                p_correlativo => @Correlativo, p_numero_factura => @NumeroFactura,
                p_lectura_uuid => @LecturaUuid, p_factura_id => @FacturaId, p_usuario => @Usuario);";

        return await conn.QueryFirstAsync<CaiSyncRow>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            ClienteId = clienteId,
            IdCai = folio.CaiId,
            Correlativo = folio.Correlativo,
            folio.NumeroFactura,
            LecturaUuid = lecturaUuid,
            FacturaId = facturaId,
            Usuario = usuario,
        }, cancellationToken: ct));
    }

    private async Task<EmitirFacturaLecturaResultado> EjecutarLecturaV3Async(
        DbConnection conn, long companyId, ClienteIdentidad cliente,
        EmitirFacturaLecturaRequest request, FolioCai folio, string lecturaUuid,
        string usuario, CancellationToken ct)
    {
        const string sql = @"
            select * from public.sp_lectura_v3(
                p_company_id => @CompanyId, p_anio => @Anio, p_mes => @Mes, p_ciclo => NULL,
                p_clave => @Clave, p_contador => @Contador, p_fecha_lectura => @Fecha::date,
                p_usuario => @Usuario, p_lectura_actual => @LecturaActual::numeric,
                p_ser3 => 'N'::char, p_ser4 => 'N'::char, p_observacion => @Observacion,
                p_condicion_lectura => @Condicion, p_lectura_promedio => @Promedio::numeric,
                p_numero_factura => @NumeroFactura, p_correlativo_cai => @Correlativo::int,
                p_id_cai => @IdCai::int, p_tienemedidor => @TieneMedidor::char,
                p_informativo => 'N', p_imagen => @Imagen::bytea, p_categoria => '0'::char,
                p_lectura_uuid => @LecturaUuid);";

        var parametros = new DynamicParameters();
        parametros.Add("CompanyId", companyId, DbType.Int64);
        parametros.Add("Anio", request.Anio, DbType.Int32);
        parametros.Add("Mes", request.Mes, DbType.Int32);
        parametros.Add("Clave", cliente.ClienteClave, DbType.String);
        parametros.Add("Contador", NuloSiVacio(request.Contador) ?? NuloSiVacio(cliente.Contador), DbType.String);
        parametros.Add("Fecha", request.FechaLectura ?? DateTime.Today, DbType.Date);
        parametros.Add("Usuario", usuario, DbType.String);
        parametros.Add("LecturaActual", request.LecturaActual, DbType.Decimal);
        parametros.Add("Observacion", NuloSiVacio(request.Observacion), DbType.String);
        parametros.Add("Condicion", CondicionODefecto(request.CondicionLectura), DbType.String);
        parametros.Add("Promedio", request.LecturaPromedio, DbType.Decimal);
        parametros.Add("NumeroFactura", folio.NumeroFactura, DbType.String);
        parametros.Add("Correlativo", folio.Correlativo, DbType.Int32);
        parametros.Add("IdCai", folio.CaiId, DbType.Int32);
        parametros.Add("TieneMedidor", cliente.TieneMedidor ? "S" : "N", DbType.String);
        parametros.Add("Imagen", DecodificarImagen(request.ImagenBase64), DbType.Binary);
        parametros.Add("LecturaUuid", lecturaUuid, DbType.String);

        IDictionary<string, object?>? fila;
        try
        {
            fila = await conn.QueryFirstOrDefaultAsync(new CommandDefinition(sql, parametros, cancellationToken: ct))
                as IDictionary<string, object?>;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Npgsql.PostgresException ex)
        {
            _log.LogWarning(ex, "sp_lectura_v3 rechazó la emisión de {Clave} en {Mes}/{Anio}.",
                cliente.ClienteClave, request.Mes, request.Anio);
            return Traducir(ex);
        }

        if (fila is null)
        {
            return Fallo("ERROR_LECTURA_V3", "El procedimiento de emisión no devolvió resultado.");
        }

        var resultado = new EmitirFacturaLecturaResultado
        {
            Success = Booleano(fila, "success"),
            Codigo = Texto(fila, "codigo") ?? "OK",
            Mensaje = Texto(fila, "mensaje") ?? string.Empty,
            FacturaId = Entero(fila, "factura_id"),
            NumeroFactura = Texto(fila, "numero_factura"),
            CorrelativoCai = folio.Correlativo,
            IdCai = folio.CaiId,
            ClienteClave = Texto(fila, "cliente_clave") ?? cliente.ClienteClave,
            ClienteNombre = Texto(fila, "cliente_nombre") ?? cliente.ClienteNombre,
            Consumo = Numero(fila, "consumo"),
            Subtotal = Numero(fila, "subtotal"),
            SubtotalAjustes = Numero(fila, "subtotal_ajustes"),
            SaldosAnteriores = Numero(fila, "saldos_anteriores"),
            Recargos = Numero(fila, "recargos"),
            Total = Numero(fila, "total"),
        };

        AgregarAvisos(resultado.Warnings, Texto(fila, "warnings_json"));
        return resultado;
    }

    // -------------------------------------------------------------------------
    // Consultas de apoyo
    // -------------------------------------------------------------------------

    private async Task<DbConnection> AbrirAsync(CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        return conn;
    }

    private static async Task<ClienteIdentidad?> ResolverClienteAsync(
        DbConnection conn, long companyId, string clave, CancellationToken ct)
    {
        // Acotado al tenant: la clave del abonado puede repetirse entre empresas.
        const string sql = @"
            select maestro_cliente_id                   as ""ClienteId"",
                   maestro_cliente_clave                as ""ClienteClave"",
                   coalesce(maestro_cliente_nombre, '') as ""ClienteNombre"",
                   coalesce(contador, '')               as ""Contador"",
                   coalesce(contador, '') <> ''         as ""TieneMedidor""
            from public.cliente_maestro
            where company_id = @CompanyId
              and maestro_cliente_clave = @Clave
              and estado = true
            limit 1;";

        return await conn.QueryFirstOrDefaultAsync<ClienteIdentidad>(
            new CommandDefinition(sql, new { CompanyId = companyId, Clave = clave }, cancellationToken: ct));
    }

    private static async Task<FacturaVigente?> FacturaVigenteDelPeriodoAsync(
        DbConnection conn, long companyId, string clave, int anio, int mes, CancellationToken ct)
    {
        // Misma condición que usa sp_lectura_v3: una factura anulada NO estorba, que es
        // justamente lo que permite refacturar tras la nota de crédito.
        // Las 3.9M facturas migradas de SIMAFI no traen número fiscal: se identifican por su
        // número de recibo. Mismo fallback que usan los SP de nota de crédito/débito
        // (Database/2026-08-04_ncnd_factura_migrada_fallback_numrecibo.sql); sin él el aviso
        // saldría con el número en blanco.
        const string sql = @"
            select coalesce(nullif(btrim(numfactura), ''), numrecibo::text, '(sin número)')
                       as ""NumeroFactura"",
                   coalesce(estado, 'A') as ""Estado""
            from public.factura
            where company_id = @CompanyId
              and clientecodigo = @Clave
              and ano = @Anio::text
              and mes = @Mes::text
              and coalesce(estado, '') <> 'N'
            order by id desc
            limit 1;";

        return await conn.QueryFirstOrDefaultAsync<FacturaVigente>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            Clave = clave,
            Anio = anio,
            Mes = mes,
        }, cancellationToken: ct));
    }

    // -------------------------------------------------------------------------
    // Utilidades
    // -------------------------------------------------------------------------

    private static EmitirFacturaLecturaResultado Fallo(string codigo, string mensaje) =>
        new() { Success = false, Codigo = codigo, Mensaje = mensaje };

    /// <summary>
    /// Traduce el <c>RAISE</c> de Postgres al vocabulario de la pantalla. El texto del SP ya
    /// viene redactado para quien captura, así que se conserva; sólo se separa el código.
    /// </summary>
    private static EmitirFacturaLecturaResultado Traducir(Npgsql.PostgresException ex)
    {
        var mensaje = ex.MessageText ?? ex.Message;
        var codigo = "ERROR_EMISION";

        var separador = mensaje.IndexOf(':');
        if (separador > 0)
        {
            var prefijo = mensaje[..separador];
            if (prefijo.Length <= 40 && prefijo.All(c => char.IsAsciiLetterUpper(c) || c == '_'))
            {
                codigo = prefijo;
                mensaje = mensaje[(separador + 1)..].Trim();
            }
        }

        return Fallo(codigo, mensaje);
    }

    private static void AgregarAvisos(List<string> destino, string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return;
        }

        try
        {
            var avisos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            if (avisos is not null)
            {
                destino.AddRange(avisos);
                return;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // El SP puede devolver objetos en vez de cadenas: perder el aviso sería peor que
            // mostrarlo crudo, y desde luego no vale una excepción.
        }

        destino.Add(json);
    }

    private static byte[]? DecodificarImagen(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        // El navegador manda "data:image/jpeg;base64,…": se descarta la cabecera.
        var coma = base64.IndexOf(',');
        var carga = coma >= 0 ? base64[(coma + 1)..] : base64;

        try
        {
            return Convert.FromBase64String(carga);
        }
        catch (FormatException)
        {
            // Una foto ilegible no puede impedir la emisión: la factura es lo que importa.
            return null;
        }
    }

    private static string CondicionODefecto(string? condicion) =>
        string.IsNullOrWhiteSpace(condicion) ? "N" : condicion.Trim();

    private static string? NuloSiVacio(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static bool Booleano(IDictionary<string, object?> fila, string columna) =>
        fila.TryGetValue(columna, out var v) && v is bool b && b;

    private static string? Texto(IDictionary<string, object?> fila, string columna) =>
        fila.TryGetValue(columna, out var v) ? v?.ToString() : null;

    private static long Entero(IDictionary<string, object?> fila, string columna) =>
        fila.TryGetValue(columna, out var v) && v is not null ? Convert.ToInt64(v) : 0L;

    private static decimal Numero(IDictionary<string, object?> fila, string columna) =>
        fila.TryGetValue(columna, out var v) && v is not null ? Convert.ToDecimal(v) : 0m;

    private sealed record ClienteIdentidad
    {
        public long ClienteId { get; init; }
        public string ClienteClave { get; init; } = string.Empty;
        public string ClienteNombre { get; init; } = string.Empty;
        public string Contador { get; init; } = string.Empty;
        public bool TieneMedidor { get; init; }
    }

    private sealed record FolioCai
    {
        public long CaiId { get; init; }
        public long Correlativo { get; init; }
        public string NumeroFactura { get; init; } = string.Empty;
        public string? PrefijoDocumento { get; init; }
        public string? CodigoCai { get; init; }
    }

    private sealed record CaiSyncRow
    {
        public bool Success { get; init; }
        public string? EstadoCodigo { get; init; }
        public long? CaiBloqueId { get; init; }
        public long? FacturaId { get; init; }
        public string? Mensaje { get; init; }
    }

    private sealed record PreviewCalculo
    {
        public string ClienteNombre { get; init; } = string.Empty;
        public string Contador { get; init; } = string.Empty;
        public string Ciclo { get; init; } = string.Empty;
        public string Ruta { get; init; } = string.Empty;
        public bool TieneMedidor { get; init; }
        public string? CondicionLecturaAplicada { get; init; }
        public decimal LecturaAnterior { get; init; }
        public decimal LecturaActualEfectiva { get; init; }
        public decimal ConsumoFacturable { get; init; }
        public decimal SubtotalServicios { get; init; }
        public decimal SubtotalAjustes { get; init; }
        public decimal SaldosAnteriores { get; init; }
        public decimal Recargos { get; init; }
        public decimal TotalFactura { get; init; }
        public DateTime? FechaVencimiento { get; init; }
        public string? WarningsJson { get; init; }
    }

    private sealed record FacturaVigente
    {
        public string NumeroFactura { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
    }
}
