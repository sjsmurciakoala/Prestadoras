using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Ciclos;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Ciclos;

public sealed class CiclosService : ICiclosService
{
    private readonly SiadDbContext _context;

    public CiclosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CicloListItemDto>> GetAsync(CicloFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(c => c.ciclos_descripcioncorta)
            .ThenBy(c => c.ciclos_codigo)
            .Select(c => new CicloListItemDto
            {
                Id = c.ciclos_id,
                Codigo = c.ciclos_codigo ?? string.Empty,
                DescripcionCorta = c.ciclos_descripcioncorta ?? string.Empty,
                DescripcionLarga = c.ciclos_descripcionlarga ?? string.Empty,
                Activo = c.estado
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<CicloListItemDto>> GetPagedAsync(
        CicloFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        if (take <= 0)
        {
            take = 50;
        }

        if (take > 500)
        {
            take = 500;
        }

        if (skip < 0)
        {
            skip = 0;
        }

        var query = BuildQuery(filtro);
        query = ApplySort(query, sortField, sortDesc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(c => new CicloListItemDto
            {
                Id = c.ciclos_id,
                Codigo = c.ciclos_codigo ?? string.Empty,
                DescripcionCorta = c.ciclos_descripcioncorta ?? string.Empty,
                DescripcionLarga = c.ciclos_descripcionlarga ?? string.Empty,
                Activo = c.estado
            })
            .ToListAsync(ct);

        return new PagedResult<CicloListItemDto>(items, total);
    }

    public async Task<CicloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.ciclos
            .AsNoTracking()
            .Where(c => c.ciclos_id == id)
            .Select(c => new CicloEditDto
            {
                Id = c.ciclos_id,
                Codigo = c.ciclos_codigo,
                DescripcionCorta = c.ciclos_descripcioncorta,
                DescripcionLarga = c.ciclos_descripcionlarga,
                Activo = c.estado
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CicloEditDto> CreateAsync(CicloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeRequired(dto.Codigo, 50, "codigo", uppercase: true);
        var descripcionCorta = NormalizeRequired(dto.DescripcionCorta, 100, "descripcion corta");
        var descripcionLarga = NormalizeRequired(dto.DescripcionLarga, 300, "descripcion larga");

        var exists = await _context.ciclos
            .AsNoTracking()
            .AnyAsync(c => c.ciclos_codigo == codigo, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un ciclo con el codigo {codigo}.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var entity = new ciclo
        {
            ciclos_codigo = codigo,
            ciclos_descripcioncorta = descripcionCorta,
            ciclos_descripcionlarga = descripcionLarga,
            estado = dto.Activo,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.ciclos.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.ciclos_id;
        dto.Codigo = codigo;
        dto.DescripcionCorta = descripcionCorta;
        dto.DescripcionLarga = descripcionLarga;
        return dto;
    }

    public async Task<CicloEditDto> UpdateAsync(int id, CicloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El ciclo no es valido.");
        }

        var entity = await _context.ciclos.FirstOrDefaultAsync(c => c.ciclos_id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("El ciclo no existe.");
        }

        var codigo = NormalizeRequired(dto.Codigo, 50, "codigo", uppercase: true);
        var descripcionCorta = NormalizeRequired(dto.DescripcionCorta, 100, "descripcion corta");
        var descripcionLarga = NormalizeRequired(dto.DescripcionLarga, 300, "descripcion larga");

        var exists = await _context.ciclos
            .AsNoTracking()
            .AnyAsync(c => c.ciclos_codigo == codigo && c.ciclos_id != id, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un ciclo con el codigo {codigo}.");
        }

        entity.ciclos_codigo = codigo;
        entity.ciclos_descripcioncorta = descripcionCorta;
        entity.ciclos_descripcionlarga = descripcionLarga;
        entity.estado = dto.Activo;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.ciclos_id;
        dto.Codigo = codigo;
        dto.DescripcionCorta = descripcionCorta;
        dto.DescripcionLarga = descripcionLarga;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El ciclo no es valido.");
        }

        var entity = await _context.ciclos.FirstOrDefaultAsync(c => c.ciclos_id == id, ct);
        if (entity is null)
        {
            return false;
        }

        if (!entity.estado)
        {
            return true;
        }

        entity.estado = false;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<ciclo> BuildQuery(CicloFilterDto? filtro)
    {
        filtro ??= new CicloFilterDto();

        var query = _context.ciclos.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(c => c.estado == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(c =>
                    EF.Functions.ILike(c.ciclos_codigo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.ciclos_descripcioncorta ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.ciclos_descripcionlarga ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(c =>
                    (c.ciclos_codigo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.ciclos_descripcioncorta ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.ciclos_descripcionlarga ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return query;
    }

    private static IQueryable<ciclo> ApplySort(IQueryable<ciclo> query, string? sortField, bool sortDesc)
    {
        var field = sortField?.Trim();
        if (string.IsNullOrWhiteSpace(field))
        {
            return query.OrderBy(c => c.ciclos_descripcioncorta).ThenBy(c => c.ciclos_codigo);
        }

        return field.ToLowerInvariant() switch
        {
            "codigo" => sortDesc ? query.OrderByDescending(c => c.ciclos_codigo) : query.OrderBy(c => c.ciclos_codigo),
            "descripcioncorta" => sortDesc ? query.OrderByDescending(c => c.ciclos_descripcioncorta) : query.OrderBy(c => c.ciclos_descripcioncorta),
            "descripcionlarga" => sortDesc ? query.OrderByDescending(c => c.ciclos_descripcionlarga) : query.OrderBy(c => c.ciclos_descripcionlarga),
            "activo" => sortDesc ? query.OrderByDescending(c => c.estado) : query.OrderBy(c => c.estado),
            _ => query.OrderBy(c => c.ciclos_descripcioncorta).ThenBy(c => c.ciclos_codigo)
        };
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName, bool uppercase = false)
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

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static string NormalizeUser(string user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }
}
