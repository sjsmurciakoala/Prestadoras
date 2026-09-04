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
/// Implementación de <see cref="IAprobacionService"/>. Ver la interfaz para el contrato y para la
/// regla de negocio (autorización por monto, sin cascada).
/// <para>
/// <b>Por qué toma la conexión del <see cref="SiadDbContext"/> y no abre la suya:</b> autorizar
/// tiene que ser atómico con el cambio de estado del documento. Con una conexión propia, un fallo
/// posterior dejaría una autorización viva sobre un documento que se quedó en Borrador.
/// </para>
/// <para>
/// <b>Un documento, una tabla de flujo.</b> Cada documento enganchado tiene su tabla gemela
/// (<c>alm_orden_compra_aprobacion</c>, <c>alm_requisicion_aprobacion</c>), resuelta desde
/// <c>Mapa</c>. Los que aún no están enganchados lanzan <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// <b>Sin LINQ</b> (regla <c>hodsoft-sin-linq</c>): la capacidad y la elegibilidad viven en las
/// funciones <c>fn_apr_*</c>; aquí solo hay SQL explícito y las reglas de negocio.
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
    // proyecto no activa MatchNamesWithUnderscores. Los casts ::bigint/::varchar/::numeric evitan
    // que Npgsql infiera text y Postgres no resuelva la firma de la función (42883).
    // ---------------------------------------------------------------------------------------

    private const string SqlControl = @"
SELECT documento              AS Documento,
       modo                   AS Modo,
       permite_autoaprobacion AS PermiteAutoaprobacion
  FROM public.cfg_aprobacion_control
 WHERE company_id = @companyId::bigint
   AND documento  = @documento::varchar;";

    private const string SqlAutorizadores = @"
SELECT nivel             AS Nivel,
       descripcion       AS Descripcion,
       monto_hasta       AS MontoHasta,
       tiene_aprobadores AS TieneAprobadores
  FROM public.fn_apr_autorizadores(@companyId::bigint, @documento::varchar, @total::numeric);";

    private const string SqlPuedeAutorizar = @"
SELECT public.fn_apr_puede_autorizar(
       @companyId::bigint, @documento::varchar, @total::numeric, @usuario::varchar, @roles::varchar[]);";

    private const string SqlTramoDe = @"
SELECT nivel       AS Nivel,
       descripcion AS Descripcion,
       monto_hasta AS MontoHasta
  FROM public.fn_apr_tramo_de(
       @companyId::bigint, @documento::varchar, @total::numeric, @usuario::varchar, @roles::varchar[]);";

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

    private const string SqlCapacidad = @"
SELECT documento_id             AS DocumentoId,
       hay_aprobador_capaz      AS HayAprobadorCapaz,
       limite_minimo_suficiente AS LimiteMinimoSuficiente,
       tramo_minimo             AS TramoMinimo
  FROM public.fn_apr_oc_capacidad(@companyId::bigint);";

    private const string SqlBitacora = @"
INSERT INTO public.apr_bitacora
       (company_id, documento, documento_id, documento_numero, nivel, accion, usuario, comentario,
        total_documento, limite_utilizado, estado_anterior, estado_nuevo)
VALUES (@companyId::bigint, @documento::varchar, @documentoId::bigint, @numero::varchar,
        @nivel::smallint, @accion::varchar, @usuario::varchar, @comentario::varchar,
        @total::numeric, @limite::numeric, @estadoAnterior::smallint, @estadoNuevo::smallint);";

    // ---------------------------------------------------------------------------------------

    public async Task<AprobacionControlDto> ObtenerControlAsync(
        string documento, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var (conn, tx) = Conexion();

        var control = await conn.QueryFirstOrDefaultAsync<AprobacionControlDto>(
            new CommandDefinition(SqlControl,
                new { companyId = EmpresaActual(), documento }, tx, cancellationToken: ct));

        // Empresa sin fila de configuración = control apagado. La ausencia de configuración nunca
        // enciende nada.
        return control ?? new AprobacionControlDto
        {
            Documento = documento,
            Modo = ModoAprobacion.Apagado,
            PermiteAutoaprobacion = false
        };
    }

    public async Task<bool> RequiereAprobacionAsync(string documento, CancellationToken ct = default)
        => (await ObtenerControlAsync(documento, ct)).Encendido;

    public async Task<IReadOnlyList<TramoAutorizacionDto>> ResolverAutorizadoresAsync(
        string documento, decimal total, CancellationToken ct = default)
    {
        ValidarDocumento(documento);

        if (!await RequiereAprobacionAsync(documento, ct))
        {
            return Array.Empty<TramoAutorizacionDto>();
        }

        var (conn, tx) = Conexion();
        var tramos = await conn.QueryAsync<TramoAutorizacionDto>(
            new CommandDefinition(SqlAutorizadores,
                new { companyId = EmpresaActual(), documento, total }, tx, cancellationToken: ct));

        return tramos.AsList();
    }

    public async Task IniciarAsync(
        string documento, long documentoId, string? numero, decimal total, string creadoPor,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default)
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

        // Un documento ya autorizado o rechazado no se reenvía sin devolverlo antes a borrador.
        var resuelto = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int;",
            new { companyId = EmpresaActual(), documentoId }, tx, cancellationToken: ct));

        if (resuelto > 0)
        {
            throw new InvalidOperationException("El documento ya está en proceso de aprobación.");
        }

        // No se materializa ningún escalón: el documento queda esperando UNA autorización de
        // quien tenga capacidad. Si hoy nadie la tiene, la pantalla lo dice y el documento espera.
        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, numero, null, AccionAprobacion.Enviada,
            $"Enviada a aprobación por {creadoPor}", total, null, estadoAnterior, estadoNuevo, ct);
    }

    public async Task<FirmaResultadoDto> AutorizarAsync(
        string documento, long documentoId, decimal total, string? comentario,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default)
    {
        var mapa = Ubicar(documento);
        var control = await ExigirControlEncendidoAsync(documento, ct);

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();
        var usuario = UsuarioActual();

        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new InvalidOperationException("No se pudo identificar al usuario que autoriza.");
        }

        await ExigirSinResolverAsync(conn, tx, mapa, companyId, documentoId, ct);
        await ExigirNoEsSuyoAsync(conn, tx, mapa, control, companyId, documentoId, usuario, ct);

        // La única pregunta que decide: ¿su tramo alcanza el monto? No hay secuencia que respetar.
        var tramo = await ResolverTramoAsync(conn, tx, documento, total, usuario, ct);
        if (tramo is null)
        {
            throw new InvalidOperationException(
                $"Su límite de aprobación no alcanza el monto de este documento ({total:N2}).");
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await conn.ExecuteAsync(new CommandDefinition(
            $@"INSERT INTO {mapa.TablaFlujo}
                     (company_id, {mapa.ColumnaDocumento}, nivel, descripcion, estado,
                      usuario_firma, fecha_firma, comentario, total_documento, limite_utilizado,
                      usuariocreacion)
              VALUES (@companyId::bigint, @documentoId::int, @nivel::smallint, @descripcion::varchar,
                      @estado::smallint, @usuario::varchar, @fecha, @comentario::varchar,
                      @total::numeric, @limite::numeric, @usuario::varchar);",
            new
            {
                companyId, documentoId,
                nivel = tramo.Nivel,
                descripcion = tramo.Descripcion,
                estado = EstadoAprobacionNivel.Aprobado,
                usuario,
                fecha = ahora,
                comentario = Recortar(comentario, 500),
                total,
                limite = tramo.MontoHasta
            }, tx, cancellationToken: ct));

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, null, tramo.Nivel, AccionAprobacion.Aprobada,
            comentario, total, tramo.MontoHasta, estadoAnterior, estadoNuevo, ct);

        return new FirmaResultadoDto
        {
            Nivel = tramo.Nivel,
            DescripcionNivel = tramo.Descripcion,
            LimiteUtilizado = tramo.MontoHasta,
            MontoAprobado = total,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo
        };
    }

    public async Task RechazarAsync(
        string documento, long documentoId, decimal total, string motivo,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default)
    {
        var mapa = Ubicar(documento);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("El motivo del rechazo es obligatorio.");
        }

        var control = await ExigirControlEncendidoAsync(documento, ct);

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();
        var usuario = UsuarioActual();

        await ExigirSinResolverAsync(conn, tx, mapa, companyId, documentoId, ct);
        await ExigirNoEsSuyoAsync(conn, tx, mapa, control, companyId, documentoId, usuario, ct);

        // Rechazar exige la misma capacidad que aprobar: quien no podría autorizar el monto
        // tampoco puede tumbar el documento.
        var tramo = await ResolverTramoAsync(conn, tx, documento, total, usuario, ct);
        if (tramo is null)
        {
            throw new InvalidOperationException(
                $"Su límite de aprobación no alcanza el monto de este documento ({total:N2}).");
        }

        await conn.ExecuteAsync(new CommandDefinition(
            $@"INSERT INTO {mapa.TablaFlujo}
                     (company_id, {mapa.ColumnaDocumento}, nivel, descripcion, estado,
                      usuario_firma, fecha_firma, comentario, total_documento, limite_utilizado,
                      usuariocreacion)
              VALUES (@companyId::bigint, @documentoId::int, @nivel::smallint, @descripcion::varchar,
                      @estado::smallint, @usuario::varchar, (now() AT TIME ZONE 'utc'),
                      @motivo::varchar, @total::numeric, @limite::numeric, @usuario::varchar);",
            new
            {
                companyId, documentoId,
                nivel = tramo.Nivel,
                descripcion = tramo.Descripcion,
                estado = EstadoAprobacionNivel.Rechazado,
                usuario,
                motivo = Recortar(motivo, 500),
                total,
                limite = tramo.MontoHasta
            }, tx, cancellationToken: ct));

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, null, tramo.Nivel, AccionAprobacion.Rechazada,
            motivo, total, tramo.MontoHasta, estadoAnterior, estadoNuevo, ct);
    }

    public async Task ReiniciarAsync(
        string documento, long documentoId, string motivo,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default)
    {
        var mapa = Ubicar(documento);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("El motivo de la devolución es obligatorio.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        // Devolver borra la autorización si la hubo; lo borrado sobrevive en la bitácora, que es
        // append-only.
        await conn.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        await RegistrarBitacoraAsync(
            conn, tx, documento, documentoId, null, null, AccionAprobacion.Devuelta, motivo,
            null, null, estadoAnterior, estadoNuevo, ct);
    }

    public async Task RegistrarEventoAsync(
        string documento, long documentoId, string? numero, string accion, string? comentario,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var (conn, tx) = Conexion();
        await RegistrarBitacoraAsync(conn, tx, documento, documentoId, numero, null, accion,
            comentario, null, null, estadoAnterior, estadoNuevo, ct);
    }

    public async Task<AprobacionEstadoDto> ObtenerEstadoAsync(
        string documento, long documentoId, decimal total, CancellationToken ct = default)
    {
        var mapa = Ubicar(documento);
        var control = await ObtenerControlAsync(documento, ct);
        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        var estado = new AprobacionEstadoDto
        {
            ControlEncendido = control.Encendido,
            MontoRequerido = total
        };

        estado.Firma = await conn.QueryFirstOrDefaultAsync<FirmaAprobacionDto>(new CommandDefinition(
            $@"SELECT nivel            AS Nivel,
                      descripcion      AS Descripcion,
                      estado           AS Estado,
                      usuario_firma    AS UsuarioFirma,
                      fecha_firma      AS FechaFirma,
                      comentario       AS Comentario,
                      total_documento  AS TotalDocumento,
                      limite_utilizado AS LimiteUtilizado
                 FROM {mapa.TablaFlujo}
                WHERE company_id = @companyId::bigint
                  AND {mapa.ColumnaDocumento} = @documentoId::int
                ORDER BY id DESC
                LIMIT 1;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        if (estado.Firma is not null)
        {
            estado.Firma.EstadoDescripcion = DescribirEstado(estado.Firma.Estado);
            return estado;   // ya resuelto: no hay nada que autorizar
        }

        if (!control.Encendido) return estado;

        // Sin autorización todavía: quién puede darla y si este usuario es uno de ellos.
        foreach (var tramo in await ResolverAutorizadoresAsync(documento, total, ct))
        {
            if (!tramo.TieneAprobadores) continue;

            estado.HayAprobadorCapaz = true;
            estado.TramoMinimo = tramo.Descripcion;
            break;
        }

        estado.PuedoAutorizar = await PuedeAutorizarAsync(conn, tx, documento, total, UsuarioActual(), ct)
            && await NoEsSuyoAsync(conn, tx, mapa, control, companyId, documentoId, UsuarioActual(), ct);

        return estado;
    }

    public async Task<IReadOnlyList<string>> CorreosAutorizadoresAsync(
        string documento, decimal total, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        Ubicar(documento);

        var (conn, tx) = Conexion();

        // Los aprobadores tipo 1 (usuario) de cualquier tramo capaz. Los de tipo 2 (rol) viven en
        // Identity y no se resuelven desde aquí: a ellos les llega por la copia al área.
        const string sql = @"
SELECT DISTINCT a.valor
  FROM public.cfg_aprobacion_nivel n
  JOIN public.cfg_aprobacion_aprobador a
    ON a.company_id = n.company_id
   AND a.nivel_id   = n.id
   AND a.activo
   AND a.tipo       = 1
 WHERE n.company_id = @companyId::bigint
   AND n.documento  = @documento::varchar
   AND n.activo
   AND (n.monto_hasta IS NULL OR n.monto_hasta >= @total::numeric);";

        var correos = await conn.QueryAsync<string>(new CommandDefinition(
            sql, new { companyId = EmpresaActual(), documento, total }, tx, cancellationToken: ct));

        return correos.AsList();
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

    public async Task<IReadOnlyList<CapacidadAprobacionDto>> CapacidadOrdenesCompraAsync(
        CancellationToken ct = default)
    {
        var (conn, tx) = Conexion();

        var filas = await conn.QueryAsync<CapacidadAprobacionDto>(
            new CommandDefinition(SqlCapacidad, new { companyId = EmpresaActual() },
                tx, cancellationToken: ct));

        return filas.AsList();
    }

    // ---------------------------------------------------------------------------------------

    private async Task<AprobacionControlDto> ExigirControlEncendidoAsync(
        string documento, CancellationToken ct)
    {
        var control = await ObtenerControlAsync(documento, ct);
        if (!control.Encendido)
        {
            throw new InvalidOperationException(
                "La aprobación por niveles no está activada para este documento.");
        }
        return control;
    }

    /// <summary>Un documento ya autorizado o rechazado no se vuelve a resolver.</summary>
    private static async Task ExigirSinResolverAsync(
        DbConnection conn, DbTransaction? tx, MapaDocumento mapa, long companyId, long documentoId,
        CancellationToken ct)
    {
        var resuelto = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM {mapa.TablaFlujo} " +
            $"WHERE company_id = @companyId::bigint AND {mapa.ColumnaDocumento} = @documentoId::int;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        if (resuelto > 0)
        {
            throw new InvalidOperationException("Este documento ya fue resuelto.");
        }
    }

    private async Task ExigirNoEsSuyoAsync(
        DbConnection conn, DbTransaction? tx, MapaDocumento mapa, AprobacionControlDto control,
        long companyId, long documentoId, string usuario, CancellationToken ct)
    {
        if (await NoEsSuyoAsync(conn, tx, mapa, control, companyId, documentoId, usuario, ct)) return;

        throw new InvalidOperationException("No puede aprobar un documento que usted mismo creó.");
    }

    /// <summary>D5: nadie autoriza lo suyo, salvo que la empresa lo permita.</summary>
    private static async Task<bool> NoEsSuyoAsync(
        DbConnection conn, DbTransaction? tx, MapaDocumento mapa, AprobacionControlDto control,
        long companyId, long documentoId, string usuario, CancellationToken ct)
    {
        if (control.PermiteAutoaprobacion) return true;

        var creador = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT lower(btrim(coalesce({mapa.ColumnaCreador}, ''))) FROM {mapa.TablaDocumento} " +
            "WHERE company_id = @companyId::bigint AND id = @documentoId::int;",
            new { companyId, documentoId }, tx, cancellationToken: ct));

        return string.IsNullOrEmpty(creador)
            || !string.Equals(creador, usuario, StringComparison.OrdinalIgnoreCase);
    }

    private Task<bool> PuedeAutorizarAsync(
        DbConnection conn, DbTransaction? tx, string documento, decimal total, string usuario,
        CancellationToken ct)
        => conn.ExecuteScalarAsync<bool>(new CommandDefinition(SqlPuedeAutorizar, new
        {
            companyId = EmpresaActual(), documento, total, usuario, roles = RolesActuales()
        }, tx, cancellationToken: ct));

    /// <summary>El tramo más bajo con el que este usuario autorizaría el monto; null si no llega.</summary>
    private Task<TramoAutorizacionDto?> ResolverTramoAsync(
        DbConnection conn, DbTransaction? tx, string documento, decimal total, string usuario,
        CancellationToken ct)
        => conn.QueryFirstOrDefaultAsync<TramoAutorizacionDto>(new CommandDefinition(SqlTramoDe, new
        {
            companyId = EmpresaActual(), documento, total, usuario, roles = RolesActuales()
        }, tx, cancellationToken: ct));

    private Task RegistrarBitacoraAsync(
        DbConnection conn, DbTransaction? tx, string documento, long documentoId, string? numero,
        short? nivel, string accion, string? comentario, decimal? total, decimal? limite,
        short estadoAnterior, short estadoNuevo, CancellationToken ct)
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
            total,
            limite,
            estadoAnterior,
            estadoNuevo
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
    /// Dónde vive el flujo de cada documento y cómo se llama a su creador. Cada documento tiene su
    /// tabla gemela para conservar la FK compuesta tenant-safe y el <c>ON DELETE CASCADE</c>; el
    /// motor es genérico y solo cambian estos cuatro nombres, que salen de una lista fija del
    /// código, nunca de una entrada del usuario.
    /// </summary>
    private sealed record MapaDocumento(
        string TablaFlujo, string ColumnaDocumento, string TablaDocumento, string ColumnaCreador);

    private static readonly Dictionary<string, MapaDocumento> Mapa = new(StringComparer.Ordinal)
    {
        [DocumentosAprobacion.OrdenCompra] = new(
            "public.alm_orden_compra_aprobacion", "orden_compra_id",
            "public.alm_orden_compra", "usuariocreacion"),

        // La requisición identifica a su autor por `usuario_solicita` (el login), que es el campo
        // que el módulo trata como dueño del documento.
        [DocumentosAprobacion.Requisicion] = new(
            "public.alm_requisicion_aprobacion", "requisicion_id",
            "public.alm_requisicion_hdr", "usuario_solicita")
    };

    private static MapaDocumento Ubicar(string documento)
    {
        ValidarDocumento(documento);

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
}
