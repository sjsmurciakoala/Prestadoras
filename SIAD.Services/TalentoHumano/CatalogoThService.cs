using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.TalentoHumano;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.TalentoHumano;

/// <summary>
/// Mantenimiento de los catálogos simples de Talento Humano (th_cargo, th_departamento).
/// <para>
/// Un solo servicio sirve a los dos catálogos: el nombre de tabla y la FK del empleado se
/// resuelven desde una lista blanca por <see cref="CatalogoTh"/> (nunca desde entrada del usuario),
/// así que interpolar el identificador en el SQL es seguro. Acceso por Dapper (sin LINQ);
/// <see cref="ICurrentCompanyService"/> resuelve la empresa.
/// </para>
/// </summary>
public sealed class CatalogoThService : ICatalogoThService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public CatalogoThService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    /// <summary>Tabla del catálogo y columna FK del empleado, por tipo. Valores fijos del código.</summary>
    private static (string Tabla, string FkEmpleado, string Etiqueta) Meta(CatalogoTh tipo) => tipo switch
    {
        CatalogoTh.Cargo => ("th_cargo", "cargo_id", "cargo"),
        CatalogoTh.Departamento => ("th_departamento", "departamento_id", "departamento"),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo))
    };

    public async Task<IReadOnlyList<CatalogoThListItemDto>> GetAsync(CatalogoTh tipo, CatalogoThFilterDto? filtro, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var (tabla, fk, _) = Meta(tipo);

        filtro ??= new CatalogoThFilterDto();
        var search = string.IsNullOrWhiteSpace(filtro.Search) ? null : $"%{filtro.Search.Trim()}%";

        var sql = $@"
            SELECT t.id AS Id, t.nombre AS Nombre, t.activo AS Activo,
                   (SELECT count(*) FROM public.th_empleado e
                     WHERE e.company_id = t.company_id AND e.{fk} = t.id) AS Empleados
              FROM public.{tabla} t
             WHERE t.company_id = @CompanyId
               AND (@Activo::boolean IS NULL OR t.activo = @Activo::boolean)
               AND (@Search::text   IS NULL OR t.nombre ILIKE @Search::text)
             ORDER BY t.nombre";

        var filas = await connection.QueryAsync<CatalogoThListItemDto>(new CommandDefinition(sql,
            new { CompanyId = companyId, Activo = filtro.Activo, Search = search },
            TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<CatalogoThEditDto?> GetByIdAsync(CatalogoTh tipo, int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var (tabla, _, _) = Meta(tipo);

        return await connection.QuerySingleOrDefaultAsync<CatalogoThEditDto>(new CommandDefinition($@"
            SELECT id AS Id, nombre AS Nombre, activo AS Activo
              FROM public.{tabla}
             WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, TransaccionActual(), cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CatalogoThLookupDto>> GetLookupAsync(CatalogoTh tipo, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var (tabla, _, _) = Meta(tipo);

        var filas = await connection.QueryAsync<CatalogoThLookupDto>(new CommandDefinition($@"
            SELECT id AS Id, nombre AS Nombre
              FROM public.{tabla}
             WHERE company_id = @CompanyId AND activo = TRUE
             ORDER BY nombre",
            new { CompanyId = companyId }, TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<CatalogoThEditDto> CreateAsync(CatalogoTh tipo, CatalogoThEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();
        var (tabla, _, etiqueta) = Meta(tipo);

        var nombre = ValidarNombre(dto.Nombre, etiqueta);
        await ExigirNombreLibreAsync(connection, tx, tabla, companyId, nombre, null, ct);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition($@"
            INSERT INTO public.{tabla} (company_id, nombre, activo, usuariocreacion, fechacreacion)
            VALUES (@CompanyId, @Nombre, @Activo, @Usuario, @Ahora)
            RETURNING id",
            new { CompanyId = companyId, Nombre = nombre, dto.Activo, Usuario = Usuario(user), Ahora = Ahora() },
            tx, cancellationToken: ct));

        dto.Id = id;
        dto.Nombre = nombre;
        return dto;
    }

    public async Task<CatalogoThEditDto> UpdateAsync(CatalogoTh tipo, int id, CatalogoThEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();
        var (tabla, _, etiqueta) = Meta(tipo);

        var nombre = ValidarNombre(dto.Nombre, etiqueta);
        await ExigirNombreLibreAsync(connection, tx, tabla, companyId, nombre, id, ct);

        var filas = await connection.ExecuteAsync(new CommandDefinition($@"
            UPDATE public.{tabla}
               SET nombre = @Nombre, activo = @Activo,
                   usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id, Nombre = nombre, dto.Activo, Usuario = Usuario(user), Ahora = Ahora() },
            tx, cancellationToken: ct));

        if (filas == 0) throw new KeyNotFoundException($"El {etiqueta} no existe.");

        dto.Id = id;
        dto.Nombre = nombre;
        return dto;
    }

    public async Task<bool> DeactivateAsync(CatalogoTh tipo, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();
        var (tabla, _, _) = Meta(tipo);

        var filas = await connection.ExecuteAsync(new CommandDefinition($@"
            UPDATE public.{tabla}
               SET activo = false, usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id AND activo = TRUE",
            new { CompanyId = companyId, Id = id, Usuario = Usuario(user), Ahora = Ahora() },
            tx, cancellationToken: ct));

        if (filas > 0) return true;

        var existe = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT EXISTS (SELECT 1 FROM public.{tabla} WHERE company_id = @CompanyId AND id = @Id)",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

        return existe; // ya estaba inactivo: idempotente
    }

    // ── Validaciones ─────────────────────────────────────────────────────────

    private static string ValidarNombre(string? valor, string etiqueta)
    {
        var nombre = (valor ?? string.Empty).Trim();
        if (nombre.Length == 0) throw new InvalidOperationException($"El nombre del {etiqueta} es obligatorio.");
        if (nombre.Length > 80) throw new InvalidOperationException("El nombre no puede superar 80 caracteres.");
        return nombre;
    }

    private static async Task ExigirNombreLibreAsync(
        DbConnection connection, DbTransaction? tx, string tabla, long companyId, string nombre, int? excepto, CancellationToken ct)
    {
        var existe = await connection.ExecuteScalarAsync<bool>(new CommandDefinition($@"
            SELECT EXISTS (SELECT 1 FROM public.{tabla}
                            WHERE company_id = @CompanyId AND lower(nombre) = lower(@Nombre)
                              AND (@Excepto::integer IS NULL OR id <> @Excepto::integer))",
            new { CompanyId = companyId, Nombre = nombre, Excepto = excepto }, tx, cancellationToken: ct));

        if (existe) throw new InvalidOperationException($"Ya existe «{nombre}».");
    }

    // ── Infraestructura ──────────────────────────────────────────────────────

    private long EnsureCompanyId()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        return companyId;
    }

    private async Task<DbConnection> AbrirConexionAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        return connection;
    }

    private DbTransaction? TransaccionActual() => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string Usuario(string? usuario) => string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario.Trim();

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
