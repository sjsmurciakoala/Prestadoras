using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TarifasBase;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.TarifasBase;

public sealed class TarifasBaseService : ITarifasBaseService
{
    private readonly SiadDbContext _context;

    public TarifasBaseService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TarifaBaseListItemDto>> GetAsync(TarifaBaseFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(t => t.tipo)
            .ThenBy(t => t.categoria_id)
            .ThenBy(t => t.codigo)
            .Select(t => new TarifaBaseListItemDto
            {
                Tipo = t.tipo,
                CategoriaId = t.categoria_id,
                Codigo = t.codigo ?? string.Empty,
                Descripcion = t.descripcion,
                Valor = t.valor
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<TarifaBaseListItemDto>> GetPagedAsync(
        TarifaBaseFilterDto? filtro,
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
            .Select(t => new TarifaBaseListItemDto
            {
                Tipo = t.tipo,
                CategoriaId = t.categoria_id,
                Codigo = t.codigo ?? string.Empty,
                Descripcion = t.descripcion,
                Valor = t.valor
            })
            .ToListAsync(ct);

        return new PagedResult<TarifaBaseListItemDto>(items, total);
    }

    public async Task<TarifaBaseEditDto?> GetByIdAsync(int tipo, int categoriaId, string codigo, CancellationToken ct = default)
    {
        if (tipo <= 0 || categoriaId <= 0 || string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var keyCodigo = codigo.Trim();

        return await _context.tarifas
            .AsNoTracking()
            .Where(t => t.tipo == tipo && t.categoria_id == categoriaId && t.codigo == keyCodigo)
            .Select(t => new TarifaBaseEditDto
            {
                Tipo = t.tipo,
                CategoriaId = t.categoria_id,
                Codigo = t.codigo ?? string.Empty,
                Descripcion = t.descripcion,
                Valor = t.valor
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TarifaBaseEditDto> CreateAsync(TarifaBaseEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var tipo = dto.Tipo;
        var categoria = dto.CategoriaId;
        var codigo = NormalizeRequired(dto.Codigo, "codigo");

        if (tipo <= 0)
        {
            throw new ArgumentException("El tipo no es valido.", nameof(dto.Tipo));
        }

        if (categoria <= 0)
        {
            throw new ArgumentException("La categoria no es valida.", nameof(dto.CategoriaId));
        }

        var exists = await _context.tarifas
            .AsNoTracking()
            .AnyAsync(t => t.tipo == tipo && t.categoria_id == categoria && t.codigo == codigo, ct);

        if (exists)
        {
            throw new InvalidOperationException("Ya existe una tarifa con esa clave.");
        }

        var descripcion = NormalizeOptional(dto.Descripcion);
        var entity = new tarifa
        {
            tipo = tipo,
            categoria_id = categoria,
            codigo = codigo,
            descripcion = descripcion,
            valor = dto.Valor
        };

        _context.tarifas.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Codigo = codigo;
        dto.Descripcion = descripcion;
        return dto;
    }

    public async Task<TarifaBaseEditDto> UpdateAsync(int tipo, int categoriaId, string codigo, TarifaBaseEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (tipo <= 0 || categoriaId <= 0 || string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("La tarifa no es valida.");
        }

        var keyCodigo = codigo.Trim();
        if (!string.Equals(dto.Codigo?.Trim(), keyCodigo, StringComparison.OrdinalIgnoreCase)
            || dto.Tipo != tipo
            || dto.CategoriaId != categoriaId)
        {
            throw new InvalidOperationException("No se permite cambiar la clave de la tarifa.");
        }

        var entity = await _context.tarifas
            .FirstOrDefaultAsync(t => t.tipo == tipo && t.categoria_id == categoriaId && t.codigo == keyCodigo, ct);

        if (entity is null)
        {
            throw new KeyNotFoundException("La tarifa no existe.");
        }

        var descripcion = NormalizeOptional(dto.Descripcion);
        entity.descripcion = descripcion;
        entity.valor = dto.Valor;

        await _context.SaveChangesAsync(ct);
        dto.Descripcion = descripcion;
        return dto;
    }

    public async Task<bool> DeleteAsync(int tipo, int categoriaId, string codigo, CancellationToken ct = default)
    {
        if (tipo <= 0 || categoriaId <= 0 || string.IsNullOrWhiteSpace(codigo))
        {
            throw new ArgumentException("La tarifa no es valida.");
        }

        var keyCodigo = codigo.Trim();
        var entity = await _context.tarifas
            .FirstOrDefaultAsync(t => t.tipo == tipo && t.categoria_id == categoriaId && t.codigo == keyCodigo, ct);

        if (entity is null)
        {
            return false;
        }

        _context.tarifas.Remove(entity);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            throw new InvalidOperationException("No se puede eliminar la tarifa porque tiene relacion con otras tablas.");
        }
        return true;
    }

    private IQueryable<tarifa> BuildQuery(TarifaBaseFilterDto? filtro)
    {
        filtro ??= new TarifaBaseFilterDto();

        var query = _context.tarifas.AsNoTracking().AsQueryable();

        if (filtro.Tipo.HasValue)
        {
            query = query.Where(t => t.tipo == filtro.Tipo.Value);
        }

        if (filtro.CategoriaId.HasValue)
        {
            query = query.Where(t => t.categoria_id == filtro.CategoriaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(t =>
                    EF.Functions.ILike(t.codigo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(t.descripcion ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(t =>
                    (t.codigo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (t.descripcion ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return query;
    }

    private static IQueryable<tarifa> ApplySort(IQueryable<tarifa> query, string? sortField, bool sortDesc)
    {
        var field = sortField?.Trim();
        if (string.IsNullOrWhiteSpace(field))
        {
            return query.OrderBy(t => t.tipo).ThenBy(t => t.categoria_id).ThenBy(t => t.codigo);
        }

        return field.ToLowerInvariant() switch
        {
            "tipo" => sortDesc ? query.OrderByDescending(t => t.tipo) : query.OrderBy(t => t.tipo),
            "categoriaid" => sortDesc ? query.OrderByDescending(t => t.categoria_id) : query.OrderBy(t => t.categoria_id),
            "codigo" => sortDesc ? query.OrderByDescending(t => t.codigo) : query.OrderBy(t => t.codigo),
            "descripcion" => sortDesc ? query.OrderByDescending(t => t.descripcion) : query.OrderBy(t => t.descripcion),
            "valor" => sortDesc ? query.OrderByDescending(t => t.valor) : query.OrderBy(t => t.valor),
            _ => query.OrderBy(t => t.tipo).ThenBy(t => t.categoria_id).ThenBy(t => t.codigo)
        };
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool IsForeignKeyViolation(DbUpdateException ex)
    {
        if (ex.GetBaseException() is not PostgresException pg)
        {
            return false;
        }

        return pg.SqlState == PostgresErrorCodes.ForeignKeyViolation;
    }
}
