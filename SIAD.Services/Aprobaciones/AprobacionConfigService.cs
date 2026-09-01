using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Aprobaciones;
using SIAD.Core.Security;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Implementación de <see cref="IAprobacionConfigService"/>. Ver la interfaz para el contrato.
/// <para>
/// <b>Sin LINQ</b> (regla <c>hodsoft-sin-linq</c>): SQL explícito con Dapper sobre las tablas
/// <c>cfg_aprobacion_*</c>, y los recorridos en memoria son bucles.
/// </para>
/// </summary>
public sealed class AprobacionConfigService : IAprobacionConfigService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly ICurrentUserService _usuario;

    public AprobacionConfigService(
        SiadDbContext context, ICurrentCompanyService company, ICurrentUserService usuario)
    {
        _context = context;
        _company = company;
        _usuario = usuario;
    }

    public async Task<AprobacionConfiguracionDto> ObtenerAsync(
        string documento, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        var control = await conn.QueryFirstOrDefaultAsync<ControlRow>(new CommandDefinition(
            @"SELECT modo AS Modo, permite_autoaprobacion AS PermiteAutoaprobacion
                FROM public.cfg_aprobacion_control
               WHERE company_id = @companyId::bigint AND documento = @documento::varchar;",
            new { companyId, documento }, tx, cancellationToken: ct));

        var config = new AprobacionConfiguracionDto
        {
            Documento = documento,
            DocumentoDescripcion = DescribirDocumento(documento),
            Modo = control?.Modo ?? ModoAprobacion.Apagado,
            PermiteAutoaprobacion = control?.PermiteAutoaprobacion ?? false
        };

        var niveles = (await conn.QueryAsync<AprobacionNivelConfigDto>(new CommandDefinition(
            @"SELECT id AS Id, nivel AS Nivel, descripcion AS Descripcion,
                     monto_hasta AS MontoHasta, activo AS Activo
                FROM public.cfg_aprobacion_nivel
               WHERE company_id = @companyId::bigint AND documento = @documento::varchar
               ORDER BY nivel;",
            new { companyId, documento }, tx, cancellationToken: ct))).AsList();

        var aprobadores = (await conn.QueryAsync<AprobacionAprobadorConfigDto>(new CommandDefinition(
            @"SELECT a.id AS Id, a.nivel_id AS NivelId, a.tipo AS Tipo,
                     a.valor AS Valor, a.activo AS Activo
                FROM public.cfg_aprobacion_aprobador a
                JOIN public.cfg_aprobacion_nivel n
                  ON n.company_id = a.company_id AND n.id = a.nivel_id
               WHERE a.company_id = @companyId::bigint AND n.documento = @documento::varchar
               ORDER BY a.tipo, a.valor;",
            new { companyId, documento }, tx, cancellationToken: ct))).AsList();

        foreach (var nivel in niveles)
        {
            foreach (var aprobador in aprobadores)
            {
                if (aprobador.NivelId != nivel.Id) continue;

                aprobador.TipoDescripcion = DescribirTipo(aprobador.Tipo);
                nivel.Aprobadores.Add(aprobador);
            }

            if (nivel.Activo && nivel.Aprobadores.Count == 0)
            {
                config.Advertencias.Add(
                    $"El nivel «{nivel.Descripcion}» no tiene aprobadores: ningún documento que lo " +
                    "exija podrá enviarse a aprobación.");
            }
            else if (nivel.Activo && nivel.Aprobadores.Count == 1)
            {
                // No impide operar, pero es la causa número uno de órdenes trabadas: con un solo
                // aprobador, sus vacaciones detienen las compras, y si además es quien las captura,
                // la regla de autoaprobación lo deja sin poder firmar.
                config.Advertencias.Add(
                    $"El nivel «{nivel.Descripcion}» tiene un solo aprobador: si esa persona no está " +
                    "disponible, los documentos de ese nivel quedan detenidos.");
            }

            config.Niveles.Add(nivel);
        }

        if (config.Modo == ModoAprobacion.Encendido && config.Niveles.Count == 0)
        {
            config.Advertencias.Add(
                "El control está encendido pero no hay ningún nivel configurado: no se podrá enviar " +
                "ningún documento a aprobación.");
        }

        return config;
    }

    public async Task GuardarControlAsync(
        string documento, short modo, bool permiteAutoaprobacion, CancellationToken ct = default)
    {
        ValidarDocumento(documento);

        if (modo is not (ModoAprobacion.Apagado or ModoAprobacion.Encendido))
        {
            throw new InvalidOperationException("El modo del control solo puede ser Apagado o Encendido.");
        }

        var (conn, tx) = Conexion();

        await conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO public.cfg_aprobacion_control
                     (company_id, documento, modo, permite_autoaprobacion, usuariomodificacion, fechamodificacion)
              VALUES (@companyId::bigint, @documento::varchar, @modo::smallint, @auto,
                      @usuario::varchar, (now() AT TIME ZONE 'utc'))
              ON CONFLICT (company_id, documento) DO UPDATE
                 SET modo                   = EXCLUDED.modo,
                     permite_autoaprobacion = EXCLUDED.permite_autoaprobacion,
                     usuariomodificacion    = EXCLUDED.usuariomodificacion,
                     fechamodificacion      = EXCLUDED.fechamodificacion;",
            new
            {
                companyId = EmpresaActual(),
                documento,
                modo,
                auto = permiteAutoaprobacion,
                usuario = UsuarioAuditoria()
            }, tx, cancellationToken: ct));
    }

    public async Task<AprobacionNivelConfigDto> GuardarNivelAsync(
        string documento, AprobacionNivelConfigDto dto, CancellationToken ct = default)
    {
        ValidarDocumento(documento);
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Descripcion))
        {
            throw new InvalidOperationException("Escriba una descripción para el nivel.");
        }
        if (dto.Nivel is < 1 or > 9)
        {
            throw new InvalidOperationException("El nivel debe estar entre 1 y 9.");
        }
        if (dto.MontoHasta is < 0)
        {
            throw new InvalidOperationException("El límite de aprobación no puede ser negativo.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();
        var descripcion = dto.Descripcion.Trim();

        await ValidarLimitesCrecientesAsync(conn, tx, companyId, documento, dto, ct);

        if (dto.Id > 0)
        {
            var filas = await conn.ExecuteAsync(new CommandDefinition(
                @"UPDATE public.cfg_aprobacion_nivel
                     SET nivel = @nivel::smallint, descripcion = @descripcion::varchar,
                         monto_hasta = @monto::numeric, activo = @activo,
                         usuariomodificacion = @usuario::varchar,
                         fechamodificacion = (now() AT TIME ZONE 'utc')
                   WHERE company_id = @companyId::bigint AND id = @id::int;",
                new
                {
                    companyId, id = dto.Id, nivel = dto.Nivel, descripcion,
                    monto = dto.MontoHasta, activo = dto.Activo, usuario = UsuarioAuditoria()
                }, tx, cancellationToken: ct));

            if (filas == 0) throw new InvalidOperationException("El nivel ya no existe.");
        }
        else
        {
            dto.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                @"INSERT INTO public.cfg_aprobacion_nivel
                         (company_id, documento, nivel, descripcion, monto_hasta, activo, usuariocreacion)
                  VALUES (@companyId::bigint, @documento::varchar, @nivel::smallint,
                          @descripcion::varchar, @monto::numeric, @activo, @usuario::varchar)
                  RETURNING id;",
                new
                {
                    companyId, documento, nivel = dto.Nivel, descripcion,
                    monto = dto.MontoHasta, activo = dto.Activo, usuario = UsuarioAuditoria()
                }, tx, cancellationToken: ct));
        }

        dto.Descripcion = descripcion;
        return dto;
    }

    public async Task<bool> EliminarNivelAsync(int nivelId, CancellationToken ct = default)
    {
        if (nivelId <= 0) throw new ArgumentOutOfRangeException(nameof(nivelId));

        var (conn, tx) = Conexion();

        // Los aprobadores se van con él (ON DELETE CASCADE). Los flujos abiertos no se tocan:
        // guardan un snapshot de la descripción, así que su historia no se reescribe.
        var filas = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.cfg_aprobacion_nivel WHERE company_id = @companyId::bigint AND id = @id::int;",
            new { companyId = EmpresaActual(), id = nivelId }, tx, cancellationToken: ct));

        return filas > 0;
    }

    public async Task<AprobacionAprobadorConfigDto> AgregarAprobadorAsync(
        int nivelId, AprobacionAprobadorConfigDto dto, CancellationToken ct = default)
    {
        if (nivelId <= 0) throw new ArgumentOutOfRangeException(nameof(nivelId));
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Tipo is not (TipoAprobador.Usuario or TipoAprobador.Rol))
        {
            throw new InvalidOperationException("El aprobador debe ser un usuario o un rol.");
        }
        if (string.IsNullOrWhiteSpace(dto.Valor))
        {
            throw new InvalidOperationException("Elija un usuario o un rol.");
        }

        var (conn, tx) = Conexion();
        var companyId = EmpresaActual();

        var existeNivel = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM public.cfg_aprobacion_nivel WHERE company_id = @companyId::bigint AND id = @id::int;",
            new { companyId, id = nivelId }, tx, cancellationToken: ct));

        if (existeNivel == 0) throw new InvalidOperationException("El nivel ya no existe.");

        // El usuario se normaliza a minúsculas porque así compara el motor (y así lo exige el
        // CHECK de la tabla). El rol conserva sus mayúsculas: es el nombre de Identity.
        var valor = dto.Tipo == TipoAprobador.Usuario
            ? dto.Valor.Trim().ToLowerInvariant()
            : dto.Valor.Trim();

        var duplicado = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT count(*) FROM public.cfg_aprobacion_aprobador
               WHERE company_id = @companyId::bigint AND nivel_id = @nivelId::int
                 AND tipo = @tipo::smallint AND lower(valor) = lower(@valor::varchar);",
            new { companyId, nivelId, tipo = dto.Tipo, valor }, tx, cancellationToken: ct));

        if (duplicado > 0)
        {
            throw new InvalidOperationException(
                dto.Tipo == TipoAprobador.Usuario
                    ? "Ese usuario ya es aprobador de este nivel."
                    : "Ese rol ya es aprobador de este nivel.");
        }

        dto.Id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO public.cfg_aprobacion_aprobador
                     (company_id, nivel_id, tipo, valor, activo, usuariocreacion)
              VALUES (@companyId::bigint, @nivelId::int, @tipo::smallint, @valor::varchar, true, @usuario::varchar)
              RETURNING id;",
            new { companyId, nivelId, tipo = dto.Tipo, valor, usuario = UsuarioAuditoria() },
            tx, cancellationToken: ct));

        dto.NivelId = nivelId;
        dto.Valor = valor;
        dto.Activo = true;
        dto.TipoDescripcion = DescribirTipo(dto.Tipo);
        return dto;
    }

    public async Task<bool> EliminarAprobadorAsync(int aprobadorId, CancellationToken ct = default)
    {
        if (aprobadorId <= 0) throw new ArgumentOutOfRangeException(nameof(aprobadorId));

        var (conn, tx) = Conexion();

        var filas = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.cfg_aprobacion_aprobador WHERE company_id = @companyId::bigint AND id = @id::int;",
            new { companyId = EmpresaActual(), id = aprobadorId }, tx, cancellationToken: ct));

        return filas > 0;
    }

    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Los límites crecen con el número de tramo: el tramo 1 es el de menor capacidad.
    /// <para>
    /// No es cosmético. Es lo que da sentido a «el tramo más bajo que cubre este monto», que es
    /// lo que la pantalla muestra y lo que se registra como límite utilizado; con tramos
    /// desordenados, ese mínimo dejaría de ser el que el usuario espera.
    /// </para>
    /// <para><c>null</c> es «sin tope», o sea el límite más alto posible.</para>
    /// </summary>
    private async Task ValidarLimitesCrecientesAsync(
        DbConnection conn, DbTransaction? tx, long companyId, string documento,
        AprobacionNivelConfigDto dto, CancellationToken ct)
    {
        // Dapper no mapea ValueTuple por nombre de columna: hace falta un tipo con propiedades.
        var otros = await conn.QueryAsync<NivelUmbralRow>(new CommandDefinition(
            @"SELECT nivel AS Nivel, monto_hasta AS MontoHasta
                FROM public.cfg_aprobacion_nivel
               WHERE company_id = @companyId::bigint AND documento = @documento::varchar
                 AND id <> @id::int
               ORDER BY nivel;",
            new { companyId, documento, id = dto.Id }, tx, cancellationToken: ct));

        foreach (var otro in otros)
        {
            if (otro.Nivel == dto.Nivel)
            {
                throw new InvalidOperationException($"Ya existe un nivel {dto.Nivel} para este documento.");
            }

            var mio = dto.MontoHasta;
            var suyo = otro.MontoHasta;

            if (otro.Nivel < dto.Nivel && EsMayor(suyo, mio))
            {
                throw new InvalidOperationException(
                    $"El nivel {dto.Nivel} no puede autorizar menos que el nivel {otro.Nivel} " +
                    $"({Describir(suyo)}): los límites crecen con el nivel.");
            }
            if (otro.Nivel > dto.Nivel && EsMayor(mio, suyo))
            {
                throw new InvalidOperationException(
                    $"El nivel {dto.Nivel} no puede autorizar más que el nivel {otro.Nivel} " +
                    $"({Describir(suyo)}): los límites crecen con el nivel.");
            }
        }
    }

    /// <summary>Compara dos límites tratando <c>null</c> (sin tope) como el mayor posible.</summary>
    private static bool EsMayor(decimal? a, decimal? b)
    {
        if (a is null) return b is not null;   // sin tope > cualquier monto
        if (b is null) return false;
        return a.Value > b.Value;
    }

    private static string Describir(decimal? limite)
        => limite.HasValue ? limite.Value.ToString("N2") : "sin tope";

    private static string DescribirDocumento(string documento) => documento switch
    {
        DocumentosAprobacion.OrdenCompra => "Orden de compra",
        DocumentosAprobacion.FacturaCompra => "Factura de compra",
        DocumentosAprobacion.PagoProveedor => "Pago a proveedor",
        DocumentosAprobacion.Requisicion => "Requisición de materiales",
        _ => documento
    };

    private static string DescribirTipo(short tipo) => tipo switch
    {
        TipoAprobador.Usuario => "Usuario",
        TipoAprobador.Rol => "Rol",
        _ => "—"
    };

    private static void ValidarDocumento(string documento)
    {
        if (string.IsNullOrWhiteSpace(documento))
        {
            throw new ArgumentException("El documento es obligatorio.", nameof(documento));
        }
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

    /// <summary>
    /// Quién dejó la configuración así, para las columnas de auditoría de la tabla. Cambiar quién
    /// puede autorizar compras tiene que quedar con nombre: nunca un literal genérico.
    /// </summary>
    private string UsuarioAuditoria()
    {
        var usuario = _usuario.GetUserName();
        return string.IsNullOrWhiteSpace(usuario) ? "system" : usuario;
    }

    private (DbConnection Conn, DbTransaction? Tx) Conexion()
        => (_context.Database.GetDbConnection(), _context.Database.CurrentTransaction?.GetDbTransaction());

    private sealed class ControlRow
    {
        public short Modo { get; set; }
        public bool PermiteAutoaprobacion { get; set; }
    }

    /// <summary>Límite de los otros tramos, para validar que crezcan con el nivel.</summary>
    private sealed class NivelUmbralRow
    {
        public short Nivel { get; set; }
        public decimal? MontoHasta { get; set; }
    }
}
