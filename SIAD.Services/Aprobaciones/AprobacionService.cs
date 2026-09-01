using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Aprobaciones;
using SIAD.Core.Security;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Implementación de <see cref="IAprobacionService"/>. Ver la interfaz para el contrato.
/// <para>
/// <b>Por qué toma la conexión del <see cref="SiadDbContext"/> y no abre la suya:</b> abrir y
/// cerrar el flujo tiene que ser atómico con el cambio de estado del documento. Con una conexión
/// propia, un fallo posterior dejaría firmas vivas sobre un documento que se quedó en Borrador.
/// Es el mismo patrón de <c>PresupuestoCompromisoService</c> y <c>CompraContabilidad</c>.
/// </para>
/// <para>
/// <b>Sin LINQ</b> (regla <c>hodsoft-sin-linq</c>): la escalera y la elegibilidad viven en
/// <c>fn_apr_escalera</c> / <c>fn_apr_es_aprobador</c>; aquí solo hay SQL explícito y las reglas
/// de secuencia, que son de flujo y no de datos.
/// </para>
/// <para>
/// <b>Un documento, una tabla de flujo.</b> Cada documento enganchado tiene su tabla gemela
/// (<c>alm_orden_compra_aprobacion</c>, <c>alm_requisicion_aprobacion</c>), y el motor la resuelve
/// desde <c>Mapa</c>. Los documentos que aún no están enganchados lanzan
/// <see cref="NotSupportedException"/> en vez de escribir en la tabla equivocada.
/// </para>
/// </summary>
public sealed class AprobacionService : IAprobacionService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly ICurrentUserService _usuario;

    public AprobacionService(
        SiadDbContext context, ICurrentCompanyService company, ICurrentUserService usuario)
    {
        _context = context;
        _company = company;
        _usuario = usuario;

        // Dapper no mapea DateOnly sin este handler (lo usa PendienteAprobacionDto.Fecha).
        DapperTypeHandlers.EnsureRegistered();
    }

    // ---------------------------------------------------------------------------------------
    // Consultas. Los alias explícitos NO son adorno: Dapper mapea por nombre exacto y este
    // proyecto no activa MatchNamesWithUnderscores, así que `documento_id` no llegaría a
    // `DocumentoId`. Los casts ::bigint/::varchar/::numeric evitan que Npgsql infiera text y
    // Postgres no resuelva la firma de la función (42883).
    // ---------------------------------------------------------------------------------------

    private const string SqlControl = @"
SELECT documento              AS Documento,
       modo                   AS Modo,
       permite_autoaprobacion AS PermiteAutoaprobacion
  FROM public.cfg_aprobacion_control
 WHERE company_id = @companyId::bigint
   AND documento  = @documento::varchar;";

    private const string SqlEscalera = @"
SELECT nivel             AS Nivel,
       descripcion       AS Descripcion,
       monto_desde       AS MontoDesde,
       tiene_aprobadores AS TieneAprobadores
  FROM public.fn_apr_escalera(@companyId::bigint, @documento::varchar, @total::numeric);";

    // Las consultas del flujo se arman con el nombre de tabla/columna del documento (ver Mapa):
    // son valores de una lista fija del código, no entrada del usuario.
    private static string SqlFlujo(MapaDocumento m) => $@"
SELECT nivel           AS Nivel,
       descripcion     AS Descripcion,
       estado          AS Estado,
       usuario_firma   AS UsuarioFirma,
       fecha_firma     AS FechaFirma,
       comentario      AS Comentario,
       total_documento AS TotalDocumento
  FROM {m.TablaFlujo}
 WHERE company_id          = @companyId::bigint
   AND {m.ColumnaDocumento} = @documentoId::int
 ORDER BY nivel;";

    // FOR UPDATE: dos aprobadores del mismo nivel pulsando a la vez tienen que serializarse aquí,
    // o los dos leerían "pendiente" y el segundo firmaría un nivel ya cerrado.
    private static string SqlNivelPendienteBloqueado(MapaDocumento m) => $@"
SELECT id, nivel, descripcion, total_documento
  FROM {m.TablaFlujo}
 WHERE company_id          = @companyId::bigint
   AND {m.ColumnaDocumento} = @documentoId::int
   AND estado              = 2
 ORDER BY nivel
 FOR UPDATE;";

    private const string SqlEsAprobador = @"
SELECT public.fn_apr_es_aprobador(
       @companyId::bigint, @documento::varchar, @nivel::smallint, @usuario::varchar, @roles::varchar[]);";

    private const string SqlPendientes = @"
SELECT documento_id      AS DocumentoId,
       numero            AS Numero,
       fecha             AS Fecha,
       contraparte       AS Contraparte,
       total             AS Total,
       nivel             AS Nivel,
       descripcion_nivel AS DescripcionNivel,
       creado_por        AS CreadoPor,
       dias_en_espera    AS DiasEnEspera
  FROM public.fn_apr_oc_pendientes(@companyId::bigint, @usuario::varchar, @roles::varchar[]);";

    private const string SqlBitacora = @"
INSERT INTO public.apr_bitacora
       (company_id, documento, documento_id, documento_numero, nivel, accion, usuario, comentario, total_documento)
VALUES (@companyId::bigint, @documento::varchar, @documentoId::bigint, @numero::varchar,
        @nivel::smallint, @accion::varchar, @usuario::varchar, @comentario::varchar, @total::numeric);";

    // ---------------------------------------------------------------------------------------

    public async Task<AprobacionControlDto> ObtenerControlAsync(
        string documento, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var (conn, tx) = Conexion();

        var control = await conn.QueryFirstOrDefaultAsync<AprobacionControlDto>(
            new CommandDefinition(SqlControl,
                new { companyId = EmpresaActual(), documento }, tx, cancellationToken: ct));

        // Empresa sin fila de configuración = control apagado. Es lo mismo que decidió el
        // control presupuestario: la ausencia de configuración nunca enciende nada.
        return control ?? new AprobacionControlDto
        {
            Documento = documento,
            Modo = ModoAprobacion.Apagado,
            PermiteAutoaprobacion = false
        };
    }

    public async Task<bool> RequiereAprobacionAsync(string documento, CancellationToken ct = default)
        => (await ObtenerControlAsync(documento, ct)).Encendido;

    public async Task<IReadOnlyList<NivelExigidoDto>> ResolverEscaleraAsync(
        string documento, decimal total, CancellationToken ct = default)
    {
        ValidarDocumento(documento);

        if (!await RequiereAprobacionAsync(documento, ct))
        {
            return Array.Empty<NivelExigidoDto>();
        }

        var (conn, tx) = Conexion();
        var niveles = await conn.QueryAsync<NivelExigidoDto>(
            new CommandDefinition(SqlEscalera,
                new { companyId = EmpresaActual(), documento, total }, tx, cancellationToken: ct));

        return niveles.AsList();
    }

    public async Task IniciarAsync(
        string documento, long documentoId, string? numero, decimal total, string creadoPor,
        CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var mapa = Ubicar(documento);
        if (documentoId <= 0) throw new ArgumentOutOfRangeException(nameof(documentoId));

        if (!await RequiereAprobacionAsync(documento, ct))
        {
            throw new InvalidOperationException(
                "La aprobación por niveles no está activada para este documento.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        var abiertos = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        if (abiertos > 0)
        {
            throw new InvalidOperationException("El documento ya está en proceso de aprobación.");
        }

        var escalera = await ResolverEscaleraAsync(documento, total, ct);

        if (escalera.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay niveles de aprobación configurados para este monto. " +
                "Configure la escalera en Configuración → Aprobaciones antes de enviar el documento.");
        }

        // Un nivel exigido sin aprobadores deja el documento detenido para siempre. Se detecta
        // ANTES de abrir el flujo, no cuando alguien intenta firmarlo.
        // (Sin LINQ, regla hodsoft-sin-linq: también aplica a filtrar colecciones en memoria.)
        foreach (var candidato in escalera)
        {
            if (!candidato.TieneAprobadores)
            {
                throw new InvalidOperationException(
                    $"El nivel «{candidato.Descripcion}» no tiene aprobadores asignados: nadie podría firmarlo. " +
                    "Asigne al menos uno en Configuración → Aprobaciones.");
            }
        }

        // El primer nivel nace Pendiente; el resto, Bloqueado. La secuencia es del motor.
        var primero = true;
        foreach (var nivel in escalera)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                $@"INSERT INTO {mapa.TablaFlujo}
                         (company_id, {mapa.ColumnaDocumento}, nivel, descripcion, estado, total_documento, usuariocreacion)
                  VALUES (@companyId::bigint, @documentoId::int, @nivel::smallint, @descripcion::varchar,
                          @estado::smallint, @total::numeric, @usuario::varchar);",
                new
                {
                    companyId,
                    documentoId,
                    nivel = nivel.Nivel,
                    descripcion = nivel.Descripcion,
                    estado = primero ? EstadoAprobacionNivel.Pendiente : EstadoAprobacionNivel.Bloqueado,
                    total,
                    usuario = UsuarioActual()
                }, tx, cancellationToken: ct));

            primero = false;
        }

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, numero, null, AccionAprobacion.Enviada,
            $"Enviada a aprobación por {creadoPor}", total, ct);
    }

    public async Task<FirmaResultadoDto> FirmarAsync(
        string documento, long documentoId, string? comentario, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var mapa = Ubicar(documento);

        var control = await ObtenerControlAsync(documento, ct);
        if (!control.Encendido)
        {
            throw new InvalidOperationException(
                "La aprobación por niveles no está activada para este documento.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();
        var usuario = UsuarioActual();

        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new InvalidOperationException("No se pudo identificar al usuario que firma.");
        }

        var pendiente = await conn.QueryFirstOrDefaultAsync<NivelPendienteRow>(
            new CommandDefinition(SqlNivelPendienteBloqueado(mapa),
                new { companyId, documentoId }, tx, cancellationToken: ct));

        if (pendiente is null)
        {
            throw new InvalidOperationException(
                "El documento no tiene ningún nivel pendiente de firma.");
        }

        // D5: nadie firma lo suyo, salvo que la empresa lo permita.
        if (!control.PermiteAutoaprobacion)
        {
            var creador = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
                $"SELECT lower(btrim(coalesce({mapa.ColumnaCreador}, ''))) FROM {mapa.TablaDocumento} " +
                "WHERE company_id = @companyId::bigint AND id = @documentoId::int;",
                new { companyId, documentoId }, tx, cancellationToken: ct));

            if (!string.IsNullOrEmpty(creador) &&
                string.Equals(creador, usuario, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "No puede aprobar un documento que usted mismo creó.");
            }
        }

        // Separación de funciones: una firma por persona y documento, aunque sea elegible en
        // varios niveles.
        var yaFirmo = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int " +
            "  AND lower(coalesce(usuario_firma, '')) = @usuario::varchar;",
            new { companyId, documentoId, usuario }, tx, cancellationToken: ct));

        if (yaFirmo > 0)
        {
            throw new InvalidOperationException(
                "Usted ya firmó otro nivel de este documento: un mismo usuario no puede aprobar dos veces.");
        }

        var elegible = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(SqlEsAprobador,
            new { companyId, documento, nivel = pendiente.Nivel, usuario, roles = RolesActuales() },
            tx, cancellationToken: ct));

        if (!elegible)
        {
            throw new InvalidOperationException(
                $"No está autorizado para aprobar el nivel «{pendiente.Descripcion}».");
        }

        // ¿Es la primera firma del documento? Se mide ANTES de escribir la propia. Es el dato
        // con el que el enganche decide comprometer presupuesto (D2).
        var firmasPrevias = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int AND estado = 3;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(
            $@"UPDATE {mapa.TablaFlujo}
                 SET estado        = @aprobado::smallint,
                     usuario_firma = @usuario::varchar,
                     fecha_firma   = (now() AT TIME ZONE 'utc'),
                     comentario    = @comentario::varchar
               WHERE id = @id::int;",
            new
            {
                id = pendiente.Id,
                aprobado = EstadoAprobacionNivel.Aprobado,
                usuario,
                comentario = Recortar(comentario, 500)
            }, tx, cancellationToken: ct));

        // Habilita el siguiente escalón, si queda alguno.
        var siguiente = await conn.QueryFirstOrDefaultAsync<NivelPendienteRow>(new CommandDefinition(
            $"SELECT id, nivel, descripcion, total_documento FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int AND estado = 1 " +
            "ORDER BY nivel LIMIT 1;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        if (siguiente is not null)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                $"UPDATE {mapa.TablaFlujo} SET estado = @pendiente::smallint WHERE id = @id::int;",
                new { id = siguiente.Id, pendiente = EstadoAprobacionNivel.Pendiente },
                tx, cancellationToken: ct));
        }

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, null, pendiente.Nivel, AccionAprobacion.Aprobada,
            comentario, pendiente.TotalDocumento, ct);

        return new FirmaResultadoDto
        {
            NivelFirmado = pendiente.Nivel,
            EsPrimeraFirma = firmasPrevias == 0,
            FlujoCompleto = siguiente is null,
            NivelPendiente = siguiente?.Nivel,
            DescripcionPendiente = siguiente?.Descripcion
        };
    }

    public async Task RechazarAsync(
        string documento, long documentoId, string motivo, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var mapa = Ubicar(documento);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("El motivo del rechazo es obligatorio.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();
        var usuario = UsuarioActual();

        var pendiente = await conn.QueryFirstOrDefaultAsync<NivelPendienteRow>(
            new CommandDefinition(SqlNivelPendienteBloqueado(mapa),
                new { companyId, documentoId }, tx, cancellationToken: ct));

        if (pendiente is null)
        {
            throw new InvalidOperationException(
                "El documento no tiene ningún nivel pendiente de firma.");
        }

        var elegible = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(SqlEsAprobador,
            new { companyId, documento, nivel = pendiente.Nivel, usuario, roles = RolesActuales() },
            tx, cancellationToken: ct));

        if (!elegible)
        {
            throw new InvalidOperationException(
                $"No está autorizado para rechazar el nivel «{pendiente.Descripcion}».");
        }

        await conn.ExecuteAsync(new CommandDefinition(
            $@"UPDATE {mapa.TablaFlujo}
                 SET estado        = @rechazado::smallint,
                     usuario_firma = @usuario::varchar,
                     fecha_firma   = (now() AT TIME ZONE 'utc'),
                     comentario    = @motivo::varchar
               WHERE id = @id::int;",
            new
            {
                id = pendiente.Id,
                rechazado = EstadoAprobacionNivel.Rechazado,
                usuario,
                motivo = Recortar(motivo, 500)
            }, tx, cancellationToken: ct));

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, null, pendiente.Nivel, AccionAprobacion.Rechazada,
            motivo, pendiente.TotalDocumento, ct);
    }

    public async Task ReiniciarAsync(
        string documento, long documentoId, string motivo, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var mapa = Ubicar(documento);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("El motivo de la devolución es obligatorio.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        // D4: la devolución borra TODAS las firmas, sin comparar montos ni preguntar. Lo borrado
        // sobrevive en la bitácora, que es append-only.
        var borradas = await conn.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        if (borradas == 0)
        {
            throw new InvalidOperationException("El documento no está en proceso de aprobación.");
        }

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, null, null, AccionAprobacion.Devuelta, motivo, null, ct);
    }

    public async Task RegistrarEventoAsync(
        string documento, long documentoId, string? numero, string accion, string? comentario,
        CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var (conn, tx) = Conexion();
        await RegistrarBitacoraAsync(conn, tx, documento, documentoId, numero, null, accion, comentario, null, ct);
    }

    public async Task<AprobacionEstadoDto> ObtenerEstadoAsync(
        string documento, long documentoId, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var mapa = Ubicar(documento);

        var control = await ObtenerControlAsync(documento, ct);
        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        var filas = (await conn.QueryAsync<FlujoNivelDto>(
            new CommandDefinition(SqlFlujo(mapa),
                new { companyId, documentoId }, tx, cancellationToken: ct))).AsList();

        // Sin LINQ (regla hodsoft-sin-linq): el conteo y la búsqueda del pendiente van en un
        // solo recorrido, que además aprovecha para poner la etiqueta legible de cada estado.
        var firmados = 0;
        FlujoNivelDto? pendiente = null;

        foreach (var fila in filas)
        {
            fila.EstadoDescripcion = DescribirEstado(fila.Estado);

            if (fila.Estado == EstadoAprobacionNivel.Aprobado) firmados++;
            if (fila.Estado == EstadoAprobacionNivel.Pendiente) pendiente ??= fila;
        }

        var estado = new AprobacionEstadoDto
        {
            ControlEncendido = control.Encendido,
            Niveles = filas,
            Total = filas.Count,
            Firmados = firmados
        };

        if (pendiente is null)
        {
            return estado;
        }

        estado.NivelPendiente = pendiente.Nivel;

        // Puede firmar quien sea elegible, no sea el creador (salvo autoaprobación) y no haya
        // firmado ya. Se resuelve con la MISMA función que la bandeja, para que la pantalla y la
        // lista nunca discrepen.
        foreach (var candidato in await PendientesOrdenCompraAsync(ct))
        {
            if (candidato.DocumentoId == documentoId)
            {
                pendiente.PuedoFirmar = true;
                break;
            }
        }

        return estado;
    }

    public async Task<IReadOnlyList<string>> CorreosNivelPendienteAsync(
        string documento, long documentoId, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var mapa = Ubicar(documento);

        var (conn, tx) = Conexion();

        // El nivel pendiente sale del flujo del documento; los correos, de la configuración de ese
        // mismo nivel. Solo tipo 1 (usuario): un rol no sabe a qué buzón escribir.
        var sql = $@"
SELECT DISTINCT a.valor
  FROM {mapa.TablaFlujo} f
  JOIN public.cfg_aprobacion_nivel n
    ON n.company_id = f.company_id
   AND n.documento  = @documento::varchar
   AND n.nivel      = f.nivel
   AND n.activo
  JOIN public.cfg_aprobacion_aprobador a
    ON a.company_id = n.company_id
   AND a.nivel_id   = n.id
   AND a.activo
   AND a.tipo       = 1
 WHERE f.company_id          = @companyId::bigint
   AND f.{mapa.ColumnaDocumento} = @documentoId::int
   AND f.estado              = 2;";

        var correos = await conn.QueryAsync<string>(new CommandDefinition(
            sql, new { companyId = EmpresaActual(), documento, documentoId }, tx, cancellationToken: ct));

        return correos.AsList();
    }

    public async Task<IReadOnlyList<ProgresoAprobacionDto>> ProgresoOrdenesCompraAsync(
        CancellationToken ct = default)
    {
        var (conn, tx) = Conexion();

        // El nivel pendiente sale del mismo agregado con un MIN condicional: no hace falta una
        // segunda consulta ni un LATERAL para saber en qué escalón está detenida cada orden.
        const string sql = @"
SELECT f.orden_compra_id                                        AS DocumentoId,
       count(*) FILTER (WHERE f.estado = 3)::int                AS Firmados,
       count(*)::int                                            AS Total,
       min(f.nivel) FILTER (WHERE f.estado = 2)                 AS NivelPendiente,
       min(f.descripcion) FILTER (WHERE f.estado = 2)           AS DescripcionPendiente
  FROM public.alm_orden_compra_aprobacion f
 WHERE f.company_id = @companyId::bigint
 GROUP BY f.orden_compra_id;";

        var filas = await conn.QueryAsync<ProgresoAprobacionDto>(
            new CommandDefinition(sql, new { companyId = EmpresaActual() }, tx, cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<IReadOnlyList<PendienteAprobacionDto>> PendientesOrdenCompraAsync(
        CancellationToken ct = default)
    {
        var (conn, tx) = Conexion();

        var filas = await conn.QueryAsync<PendienteAprobacionDto>(
            new CommandDefinition(SqlPendientes,
                new { companyId = EmpresaActual(), usuario = UsuarioActual(), roles = RolesActuales() },
                tx, cancellationToken: ct));

        var lista = filas.AsList();
        foreach (var fila in lista)
        {
            fila.Documento = DocumentosAprobacion.OrdenCompra;
        }

        return lista;
    }

    // ---------------------------------------------------------------------------------------

    private Task RegistrarBitacoraAsync(
        DbConnection conn, DbTransaction? tx, string documento, long documentoId, string? numero,
        short? nivel, string accion, string? comentario, decimal? total, CancellationToken ct)
        => conn.ExecuteAsync(new CommandDefinition(SqlBitacora, new
        {
            companyId = EmpresaActual(),
            documento,
            documentoId,
            numero = Recortar(numero, 40),
            nivel,
            accion,
            usuario = UsuarioActual(),
            comentario = Recortar(comentario, 500),
            total
        }, tx, cancellationToken: ct));

    private static string DescribirEstado(short estado) => estado switch
    {
        EstadoAprobacionNivel.Bloqueado => "Bloqueado",
        EstadoAprobacionNivel.Pendiente => "Pendiente",
        EstadoAprobacionNivel.Aprobado => "Aprobado",
        EstadoAprobacionNivel.Rechazado => "Rechazado",
        _ => "Desconocido"
    };

    private static void ValidarDocumento(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            throw new ArgumentException("El documento es obligatorio.", nameof(documento));
        }
    }

    /// <summary>
    /// Dónde vive el flujo de cada documento y cómo se llama a su creador.
    /// <para>
    /// Cada documento tiene su <b>tabla gemela</b> de flujo, para conservar la FK compuesta
    /// tenant-safe y el <c>ON DELETE CASCADE</c> —que una tabla única no podría tener, porque
    /// apuntaría a documentos distintos—. El motor es genérico: lo único que cambia por documento
    /// son estos cuatro nombres, y salen de una lista fija del código, nunca de una entrada del
    /// usuario.
    /// </para>
    /// </summary>
    private sealed record MapaDocumento(
        string TablaFlujo, string ColumnaDocumento, string TablaDocumento, string ColumnaCreador);

    private static readonly Dictionary<string, MapaDocumento> Mapa = new(StringComparer.Ordinal)
    {
        [DocumentosAprobacion.OrdenCompra] = new(
            "public.alm_orden_compra_aprobacion", "orden_compra_id",
            "public.alm_orden_compra", "usuariocreacion"),

        // La requisición identifica a su autor por `usuario_solicita` (el login), no por
        // usuariocreacion: es el campo que el módulo trata como dueño del documento.
        [DocumentosAprobacion.Requisicion] = new(
            "public.alm_requisicion_aprobacion", "requisicion_id",
            "public.alm_requisicion_hdr", "usuario_solicita")
    };

    /// <summary>
    /// Resuelve dónde escribir el flujo del documento. Si no está enganchado, falla con un
    /// mensaje que dice exactamente qué falta, en vez de escribir en la tabla equivocada.
    /// </summary>
    private static MapaDocumento Ubicar(string documento)
    {
        if (!Mapa.TryGetValue(documento, out var mapa))
        {
            throw new NotSupportedException(
                $"El documento «{documento}» todavía no está enganchado al motor de aprobación.");
        }
        return mapa;
    }

    private static string? Recortar(string? valor, int largo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        return limpio.Length <= largo ? limpio : limpio[..largo];
    }

    private long EmpresaActual()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo resolver la empresa actual.");
        }
        return companyId;
    }

    /// <summary>Usuario de la sesión, normalizado. Ver <see cref="ICurrentUserService"/>.</summary>
    private string UsuarioActual() => (_usuario.GetUserName() ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Roles de la sesión como array para Postgres. Nunca null: la función espera un array.
    /// Se copia con <see cref="List{T}"/> y no con <c>ToArray()</c> de LINQ (regla hodsoft-sin-linq).
    /// </summary>
    private string[] RolesActuales()
    {
        var roles = _usuario.GetRoles();
        if (roles is null || roles.Count == 0) return Array.Empty<string>();

        var copia = new List<string>(roles.Count);
        foreach (var rol in roles)
        {
            if (!string.IsNullOrWhiteSpace(rol)) copia.Add(rol.Trim());
        }
        return copia.ToArray();
    }

    private (DbConnection Conn, DbTransaction? Tx) Conexion()
        => (_context.Database.GetDbConnection(), _context.Database.CurrentTransaction?.GetDbTransaction());

    /// <summary>Fila del flujo que se bloquea para firmar. Interna: no cruza la frontera del servicio.</summary>
    private sealed class NivelPendienteRow
    {
        public int Id { get; set; }
        public short Nivel { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal TotalDocumento { get; set; }
    }
}
