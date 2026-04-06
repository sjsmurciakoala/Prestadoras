using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.AppLectores;

public sealed class UsuariosAppService : IUsuariosAppService
{
    private readonly SiadDbContext _context;

    public UsuariosAppService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UsuarioAppListItemDto>> GetAsync(UsuarioAppFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(u => u.usuario)
            .ThenBy(u => u.nombre)
            .Select(u => new UsuarioAppListItemDto(
                u.ide,
                u.usuario ?? string.Empty,
                u.nombre,
                u.ruta,
                IsActive(u.estado)))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<UsuarioAppListItemDto>> GetPagedAsync(
        UsuarioAppFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);
        var total = await query.CountAsync(ct);
        query = ApplySort(query, sortField, sortDesc);

        if (skip < 0)
        {
            skip = 0;
        }

        if (take <= 0)
        {
            take = 50;
        }

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(u => new UsuarioAppListItemDto(
                u.ide,
                u.usuario ?? string.Empty,
                u.nombre,
                u.ruta,
                IsActive(u.estado)))
            .ToListAsync(ct);

        return new PagedResult<UsuarioAppListItemDto>(items, total);
    }

    public async Task<UsuarioAppEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.usuarioapcs
            .AsNoTracking()
            .Where(u => u.ide == id)
            .Select(u => new UsuarioAppEditDto
            {
                Id = u.ide,
                Usuario = u.usuario ?? string.Empty,
                Clave = u.clave ?? string.Empty,
                Nombre = u.nombre ?? string.Empty,
                Ruta = u.ruta,
                Activo = IsActive(u.estado)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UsuarioAppEditDto> CreateAsync(UsuarioAppEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = new usuarioapc
        {
            usuario = NormalizeRequired(dto.Usuario, 25, "usuario"),
            clave = NormalizeRequired(dto.Clave, 30, "contrasena"),
            nombre = NormalizeRequired(dto.Nombre, 50, "nombre"),
            ruta = NormalizeOptional(dto.Ruta, 6, "ruta"),
            estado = ToEstado(dto.Activo)
        };

        _context.usuarioapcs.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new UsuarioAppEditDto
        {
            Id = entity.ide,
            Usuario = entity.usuario ?? string.Empty,
            Clave = entity.clave ?? string.Empty,
            Nombre = entity.nombre ?? string.Empty,
            Ruta = entity.ruta,
            Activo = IsActive(entity.estado)
        };
    }

    public async Task<UsuarioAppEditDto> UpdateAsync(int id, UsuarioAppEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.usuarioapcs.FirstOrDefaultAsync(u => u.ide == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro el usuario {id}.");
        }

        entity.usuario = NormalizeRequired(dto.Usuario, 25, "usuario");
        entity.clave = NormalizeRequired(dto.Clave, 30, "contrasena");
        entity.nombre = NormalizeRequired(dto.Nombre, 50, "nombre");
        entity.ruta = NormalizeOptional(dto.Ruta, 6, "ruta");
        entity.estado = ToEstado(dto.Activo);

        await _context.SaveChangesAsync(ct);

        return new UsuarioAppEditDto
        {
            Id = entity.ide,
            Usuario = entity.usuario ?? string.Empty,
            Clave = entity.clave ?? string.Empty,
            Nombre = entity.nombre ?? string.Empty,
            Ruta = entity.ruta,
            Activo = IsActive(entity.estado)
        };
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El ID del usuario no es valido.");
        }

        var entity = await _context.usuarioapcs.FirstOrDefaultAsync(u => u.ide == id, ct);
        if (entity is null)
        {
            return false;
        }

        if (!IsActive(entity.estado))
        {
            return true;
        }

        entity.estado = EstadoInactivo;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<usuarioapc> BuildQuery(UsuarioAppFilterDto? filtro)
    {
        filtro ??= new UsuarioAppFilterDto();

        var query = _context.usuarioapcs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(u =>
                    EF.Functions.ILike(u.usuario ?? string.Empty, likePattern)
                    || EF.Functions.ILike(u.nombre ?? string.Empty, likePattern)
                    || EF.Functions.ILike(u.ruta ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(u =>
                    (u.usuario ?? string.Empty).ToLower().Contains(lowered)
                    || (u.nombre ?? string.Empty).ToLower().Contains(lowered)
                    || (u.ruta ?? string.Empty).ToLower().Contains(lowered));
            }
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ruta))
        {
            var ruta = filtro.Ruta.Trim();
            query = query.Where(u => u.ruta == ruta);
        }

        if (filtro.Activo.HasValue)
        {
            if (filtro.Activo.Value)
            {
                query = query.Where(u => u.estado == null || u.estado == string.Empty || u.estado == EstadoActivo || u.estado == "1");
            }
            else
            {
                query = query.Where(u => u.estado != null && u.estado != string.Empty && u.estado != EstadoActivo && u.estado != "1");
            }
        }

        return query;
    }

    private static IQueryable<usuarioapc> ApplySort(IQueryable<usuarioapc> query, string? sortField, bool sortDesc)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return query.OrderBy(u => u.usuario).ThenBy(u => u.nombre);
        }

        var field = sortField.Trim();
        return field switch
        {
            nameof(UsuarioAppListItemDto.Usuario) or "usuario" => sortDesc
                ? query.OrderByDescending(u => u.usuario).ThenByDescending(u => u.nombre)
                : query.OrderBy(u => u.usuario).ThenBy(u => u.nombre),
            nameof(UsuarioAppListItemDto.Nombre) or "nombre" => sortDesc
                ? query.OrderByDescending(u => u.nombre).ThenByDescending(u => u.usuario)
                : query.OrderBy(u => u.nombre).ThenBy(u => u.usuario),
            nameof(UsuarioAppListItemDto.Ruta) or "ruta" => sortDesc
                ? query.OrderByDescending(u => u.ruta).ThenByDescending(u => u.usuario)
                : query.OrderBy(u => u.ruta).ThenBy(u => u.usuario),
            nameof(UsuarioAppListItemDto.Activo) or "activo" or "estado" => sortDesc
                ? query.OrderByDescending(u => u.estado).ThenBy(u => u.usuario)
                : query.OrderBy(u => u.estado).ThenBy(u => u.usuario),
            _ => query.OrderBy(u => u.usuario).ThenBy(u => u.nombre)
        };
    }

    private static bool IsActive(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return true;
        }

        var normalized = estado.Trim();
        return normalized.Equals(EstadoActivo, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToEstado(bool activo)
    {
        return activo ? EstadoActivo : EstadoInactivo;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} es obligatorio.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} no puede superar {maxLength} caracteres.", nameof(value));
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
            throw new ArgumentException($"{fieldName} no puede superar {maxLength} caracteres.", nameof(value));
        }

        return normalized;
    }

    private const string EstadoActivo = "A";
    private const string EstadoInactivo = "I";
}
