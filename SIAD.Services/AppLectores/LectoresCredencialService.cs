using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.AppLectores;

/// <summary>
/// Mantenimiento de <c>adm_lector_credencial</c> vía Dapper (la tabla no es
/// entidad EF; la app móvil la consume por SQL). La contraseña se guarda con
/// bcrypt de pgcrypto (<c>crypt(clave, gen_salt('bf'))</c>) — el mismo esquema
/// que valida el login de la MobileApi. Todo scoped a <see cref="ICurrentCompanyService"/>.
/// </summary>
public sealed class LectoresCredencialService : ILectoresCredencialService
{
    private static readonly HashSet<string> Sortables =
        new(StringComparer.OrdinalIgnoreCase) { "codigo", "nombre", "ruta", "codciclo", "activo" };

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public LectoresCredencialService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    private long CompanyId => _company.GetCompanyId();

    public async Task<IReadOnlyList<LectorCredencialListItemDto>> GetAsync(
        LectorCredencialFilterDto? filtro, CancellationToken ct = default)
    {
        var (where, args) = BuildWhere(filtro);
        var conn = _context.Database.GetDbConnection();
        var rows = await conn.QueryAsync<LectorCredencialListItemDto>(new CommandDefinition(
            $@"SELECT credencial_id AS CredencialId, codigo AS Codigo, lector_nombre AS Nombre,
                      ruta AS Ruta, codciclo AS CodCiclo, activo AS Activo
               FROM public.adm_lector_credencial
               WHERE {where}
               ORDER BY codigo",
            args, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<PagedResult<LectorCredencialListItemDto>> GetPagedAsync(
        LectorCredencialFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc,
        CancellationToken ct = default)
    {
        if (skip < 0) skip = 0;
        if (take <= 0) take = 50;
        if (take > 500) take = 500;

        var (where, args) = BuildWhere(filtro);
        var conn = _context.Database.GetDbConnection();

        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) FROM public.adm_lector_credencial WHERE {where}", args, cancellationToken: ct));

        var orderBy = ResolveOrder(sortField, sortDesc);
        var pageArgs = new DynamicParameters(args);
        pageArgs.Add("skip", skip);
        pageArgs.Add("take", take);

        var rows = await conn.QueryAsync<LectorCredencialListItemDto>(new CommandDefinition(
            $@"SELECT credencial_id AS CredencialId, codigo AS Codigo, lector_nombre AS Nombre,
                      ruta AS Ruta, codciclo AS CodCiclo, activo AS Activo
               FROM public.adm_lector_credencial
               WHERE {where}
               ORDER BY {orderBy}
               OFFSET @skip LIMIT @take",
            pageArgs, cancellationToken: ct));

        return new PagedResult<LectorCredencialListItemDto>(rows.ToList(), total);
    }

    public async Task<LectorCredencialEditDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var conn = _context.Database.GetDbConnection();
        return await conn.QueryFirstOrDefaultAsync<LectorCredencialEditDto>(new CommandDefinition(
            @"SELECT credencial_id AS Id, codigo AS Codigo, lector_nombre AS Nombre,
                     ruta AS Ruta, codciclo AS CodCiclo, activo AS Activo
              FROM public.adm_lector_credencial
              WHERE company_id = @co AND credencial_id = @id",
            new { co = CompanyId, id }, cancellationToken: ct));
    }

    public async Task<LectorCredencialEditDto> CreateAsync(LectorCredencialEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = NormalizeRequired(dto.Codigo, 50, "código");
        var clave = (dto.Clave ?? string.Empty).Trim();
        if (clave.Length == 0)
        {
            throw new ArgumentException("La contraseña es obligatoria al crear un lector.", nameof(dto));
        }

        await EnsureCodigoLibreAsync(codigo, null, ct);

        var conn = _context.Database.GetDbConnection();
        var id = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            @"INSERT INTO public.adm_lector_credencial
                (company_id, codigo, clave_hash, lector_nombre, ruta, codciclo, activo, created_by, created_at)
              VALUES
                (@co, @codigo, crypt(@clave, gen_salt('bf')), @nombre, @ruta, @codciclo, @activo, @user, now())
              RETURNING credencial_id",
            new
            {
                co = CompanyId,
                codigo,
                clave,
                nombre = NormalizeOptional(dto.Nombre, 100, "nombre"),
                ruta = NormalizeOptional(dto.Ruta, 20, "ruta"),
                codciclo = dto.CodCiclo,
                activo = dto.Activo,
                user = string.IsNullOrWhiteSpace(user) ? "system" : user
            }, cancellationToken: ct));

        dto.Id = id;
        dto.Clave = null; // nunca devolver la contraseña
        return dto;
    }

    public async Task<LectorCredencialEditDto> UpdateAsync(long id, LectorCredencialEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = NormalizeRequired(dto.Codigo, 50, "código");
        await EnsureCodigoLibreAsync(codigo, id, ct);

        var clave = (dto.Clave ?? string.Empty).Trim();
        var cambiaClave = clave.Length > 0;

        var conn = _context.Database.GetDbConnection();
        var afectadas = await conn.ExecuteAsync(new CommandDefinition(
            $@"UPDATE public.adm_lector_credencial
               SET codigo = @codigo,
                   lector_nombre = @nombre,
                   ruta = @ruta,
                   codciclo = @codciclo,
                   activo = @activo,
                   {(cambiaClave ? "clave_hash = crypt(@clave, gen_salt('bf'))," : string.Empty)}
                   updated_by = @user,
                   updated_at = now()
               WHERE company_id = @co AND credencial_id = @id",
            new
            {
                co = CompanyId,
                id,
                codigo,
                nombre = NormalizeOptional(dto.Nombre, 100, "nombre"),
                ruta = NormalizeOptional(dto.Ruta, 20, "ruta"),
                codciclo = dto.CodCiclo,
                activo = dto.Activo,
                clave,
                user = string.IsNullOrWhiteSpace(user) ? "system" : user
            }, cancellationToken: ct));

        if (afectadas == 0)
        {
            throw new KeyNotFoundException($"No se encontró el lector {id}.");
        }

        dto.Id = id;
        dto.Clave = null;
        return dto;
    }

    public async Task<bool> DeactivateAsync(long id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El ID del lector no es válido.");
        }

        var conn = _context.Database.GetDbConnection();
        var afectadas = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE public.adm_lector_credencial
              SET activo = false, updated_by = @user, updated_at = now()
              WHERE company_id = @co AND credencial_id = @id AND activo = true",
            new { co = CompanyId, id, user = string.IsNullOrWhiteSpace(user) ? "system" : user },
            cancellationToken: ct));

        // 0 filas = no existe o ya estaba inactivo; ambos casos se consideran OK
        // salvo que el registro no exista en absoluto.
        if (afectadas == 0)
        {
            var existe = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM public.adm_lector_credencial WHERE company_id=@co AND credencial_id=@id)",
                new { co = CompanyId, id }, cancellationToken: ct));
            return existe;
        }

        return true;
    }

    private async Task EnsureCodigoLibreAsync(string codigo, long? excluirId, CancellationToken ct)
    {
        var conn = _context.Database.GetDbConnection();
        var existe = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT EXISTS(
                SELECT 1 FROM public.adm_lector_credencial
                WHERE company_id = @co AND lower(codigo) = lower(@codigo)
                  AND (@excluir IS NULL OR credencial_id <> @excluir))",
            new { co = CompanyId, codigo, excluir = excluirId }, cancellationToken: ct));

        if (existe)
        {
            throw new ArgumentException($"Ya existe un lector con el código '{codigo}'.");
        }
    }

    private (string where, DynamicParameters args) BuildWhere(LectorCredencialFilterDto? filtro)
    {
        filtro ??= new LectorCredencialFilterDto();
        var args = new DynamicParameters();
        args.Add("co", CompanyId);
        var clauses = new List<string> { "company_id = @co" };

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            args.Add("search", $"%{filtro.Search.Trim()}%");
            clauses.Add("(codigo ILIKE @search OR COALESCE(lector_nombre,'') ILIKE @search OR COALESCE(ruta,'') ILIKE @search)");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ruta))
        {
            args.Add("ruta", filtro.Ruta.Trim());
            clauses.Add("ruta = @ruta");
        }

        if (filtro.Activo.HasValue)
        {
            args.Add("activo", filtro.Activo.Value);
            clauses.Add("activo = @activo");
        }

        return (string.Join(" AND ", clauses), args);
    }

    private static string ResolveOrder(string? sortField, bool sortDesc)
    {
        var field = (sortField ?? string.Empty).Trim().ToLowerInvariant();
        var col = field switch
        {
            "nombre" => "lector_nombre",
            "ruta" => "ruta",
            "codciclo" => "codciclo",
            "activo" => "activo",
            _ => "codigo"
        };
        if (!Sortables.Contains(field) && field.Length > 0)
        {
            col = "codigo";
        }
        var dir = sortDesc ? "DESC" : "ASC";
        return $"{col} {dir}, codigo ASC";
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} no puede superar {maxLength} caracteres.", nameof(value));
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} no puede superar {maxLength} caracteres.", nameof(value));
        }
        return normalized;
    }
}
