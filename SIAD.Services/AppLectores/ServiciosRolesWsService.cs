using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.AppLectores;

public sealed class ServiciosRolesWsService : IServiciosRolesWsService
{
    private readonly SiadDbContext _context;

    public ServiciosRolesWsService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServicioRolWsListItemDto>> GetAsync(ServicioRolWsFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(r => r.Rol)
            .ThenBy(r => r.Codigo)
            .Select(r => new ServicioRolWsListItemDto(
                r.Rol,
                r.Codigo,
                r.ServicioDescripcion,
                r.Activo,
                r.Descripcion))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ServicioRolWsListItemDto>> GetPagedAsync(
        ServicioRolWsFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);
        var total = await query.CountAsync(ct);

        if (skip < 0)
        {
            skip = 0;
        }

        if (take <= 0)
        {
            take = 50;
        }

        query = ApplySort(query, sortField, sortDesc);

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(r => new ServicioRolWsListItemDto(
                r.Rol,
                r.Codigo,
                r.ServicioDescripcion,
                r.Activo,
                r.Descripcion))
            .ToListAsync(ct);

        return new PagedResult<ServicioRolWsListItemDto>(items, total);
    }

    public async Task<ServicioRolWsEditDto?> GetByIdAsync(string rol, string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rol) || string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var entity = await _context.servicios_roles_ws
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.rol == rol && r.servicios_codigo == codigo, ct);

        return entity is null
            ? null
            : new ServicioRolWsEditDto
            {
                Rol = entity.rol,
                Codigo = entity.servicios_codigo,
                Activo = entity.activo,
                Descripcion = entity.descripcion
            };
    }

    public async Task<ServicioRolWsEditDto> CreateAsync(ServicioRolWsEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var rol = NormalizeRequired(dto.Rol, 50, "rol");
        var codigo = NormalizeRequired(dto.Codigo, 50, "codigo de servicio");

        var exists = await _context.servicios_roles_ws
            .AnyAsync(r => r.rol == rol && r.servicios_codigo == codigo, ct);
        if (exists)
        {
            throw new ArgumentException("Ya existe un registro con ese rol y codigo.");
        }

        var entity = new servicios_roles_ws
        {
            rol = rol,
            servicios_codigo = codigo,
            activo = dto.Activo,
            descripcion = NormalizeOptional(dto.Descripcion, 200, "descripcion")
        };

        _context.servicios_roles_ws.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new ServicioRolWsEditDto
        {
            Rol = entity.rol,
            Codigo = entity.servicios_codigo,
            Activo = entity.activo,
            Descripcion = entity.descripcion
        };
    }

    public async Task<ServicioRolWsEditDto> UpdateAsync(
        string rol,
        string codigo,
        ServicioRolWsEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.servicios_roles_ws
            .FirstOrDefaultAsync(r => r.rol == rol && r.servicios_codigo == codigo, ct);

        if (entity is null)
        {
            throw new KeyNotFoundException("No se encontro el registro.");
        }

        var nuevoRol = NormalizeRequired(dto.Rol, 50, "rol");
        var nuevoCodigo = NormalizeRequired(dto.Codigo, 50, "codigo de servicio");
        var descripcion = NormalizeOptional(dto.Descripcion, 200, "descripcion");

        var cambioKey = !string.Equals(entity.rol, nuevoRol, StringComparison.Ordinal)
                        || !string.Equals(entity.servicios_codigo, nuevoCodigo, StringComparison.Ordinal);

        if (cambioKey)
        {
            var existe = await _context.servicios_roles_ws
                .AnyAsync(r => r.rol == nuevoRol && r.servicios_codigo == nuevoCodigo, ct);
            if (existe)
            {
                throw new ArgumentException("Ya existe un registro con ese rol y codigo.");
            }

            _context.servicios_roles_ws.Remove(entity);
            var nuevo = new servicios_roles_ws
            {
                rol = nuevoRol,
                servicios_codigo = nuevoCodigo,
                activo = dto.Activo,
                descripcion = descripcion
            };
            _context.servicios_roles_ws.Add(nuevo);
        }
        else
        {
            entity.activo = dto.Activo;
            entity.descripcion = descripcion;
        }

        await _context.SaveChangesAsync(ct);

        return new ServicioRolWsEditDto
        {
            Rol = nuevoRol,
            Codigo = nuevoCodigo,
            Activo = dto.Activo,
            Descripcion = descripcion
        };
    }

    public async Task<bool> DeleteAsync(string rol, string codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rol) || string.IsNullOrWhiteSpace(codigo))
        {
            return false;
        }

        var entity = await _context.servicios_roles_ws
            .FirstOrDefaultAsync(r => r.rol == rol && r.servicios_codigo == codigo, ct);

        if (entity is null)
        {
            return false;
        }

        _context.servicios_roles_ws.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<ServicioRolQueryRow> BuildQuery(ServicioRolWsFilterDto? filtro)
    {
        filtro ??= new ServicioRolWsFilterDto();

        var baseQuery = from r in _context.servicios_roles_ws.AsNoTracking()
                        join s in _context.servicios.AsNoTracking()
                            on r.servicios_codigo equals s.servicios_codigo into srv
                        from s in srv.DefaultIfEmpty()
                        select new ServicioRolQueryRow
                        {
                            Rol = r.rol,
                            Codigo = r.servicios_codigo,
                            Activo = r.activo,
                            Descripcion = r.descripcion,
                            ServicioDescripcion = s != null ? s.servicios_descripcioncorta : null
                        };

        if (filtro.Activo.HasValue)
        {
            baseQuery = baseQuery.Where(r => r.Activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Rol))
        {
            var rol = filtro.Rol.Trim();
            baseQuery = baseQuery.Where(r => r.Rol == rol);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                baseQuery = baseQuery.Where(r =>
                    EF.Functions.ILike(r.Rol, like)
                    || EF.Functions.ILike(r.Codigo, like)
                    || EF.Functions.ILike(r.Descripcion ?? string.Empty, like)
                    || EF.Functions.ILike(r.ServicioDescripcion ?? string.Empty, like));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                baseQuery = baseQuery.Where(r =>
                    r.Rol.ToLower().Contains(lowered)
                    || r.Codigo.ToLower().Contains(lowered)
                    || (r.Descripcion ?? string.Empty).ToLower().Contains(lowered)
                    || (r.ServicioDescripcion ?? string.Empty).ToLower().Contains(lowered));
            }
        }

        return baseQuery;
    }

    private static IQueryable<ServicioRolQueryRow> ApplySort(
        IQueryable<ServicioRolQueryRow> query,
        string? sortField,
        bool sortDesc)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return query.OrderBy(r => r.Rol).ThenBy(r => r.Codigo);
        }

        var field = sortField.Trim();
        return field switch
        {
            nameof(ServicioRolWsListItemDto.Rol) or "rol" => sortDesc
                ? query.OrderByDescending(r => r.Rol).ThenByDescending(r => r.Codigo)
                : query.OrderBy(r => r.Rol).ThenBy(r => r.Codigo),
            nameof(ServicioRolWsListItemDto.Codigo) or "codigo" => sortDesc
                ? query.OrderByDescending(r => r.Codigo).ThenByDescending(r => r.Rol)
                : query.OrderBy(r => r.Codigo).ThenBy(r => r.Rol),
            nameof(ServicioRolWsListItemDto.ServicioDescripcion) or "servicioDescripcion" => sortDesc
                ? query.OrderByDescending(r => r.ServicioDescripcion).ThenByDescending(r => r.Rol)
                : query.OrderBy(r => r.ServicioDescripcion).ThenBy(r => r.Rol),
            nameof(ServicioRolWsListItemDto.Activo) or "activo" => sortDesc
                ? query.OrderByDescending(r => r.Activo).ThenByDescending(r => r.Rol)
                : query.OrderBy(r => r.Activo).ThenBy(r => r.Rol),
            _ => query.OrderBy(r => r.Rol).ThenBy(r => r.Codigo)
        };
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

    private sealed class ServicioRolQueryRow
    {
        public string Rol { get; init; } = string.Empty;
        public string Codigo { get; init; } = string.Empty;
        public bool Activo { get; init; }
        public string? Descripcion { get; init; }
        public string? ServicioDescripcion { get; init; }
    }
}
