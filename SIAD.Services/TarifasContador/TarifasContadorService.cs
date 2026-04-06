using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TarifasContador;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.TarifasContador;

public sealed class TarifasContadorService : ITarifasContadorService
{
    private readonly SiadDbContext _context;

    public TarifasContadorService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TarifaContadorListItemDto>> GetAsync(TarifaContadorFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(t => t.tipo)
            .ThenBy(t => t.categoria_id)
            .ThenBy(t => t.codigo)
            .Select(t => new TarifaContadorListItemDto
            {
                Id = t.ide,
                Tipo = t.tipo,
                CategoriaId = t.categoria_id,
                Codigo = t.codigo,
                Descripcion = t.descripcion,
                Minimo = t.minimo,
                Maximo = t.maximo,
                Cuota = t.cuota,
                ValorBase = t.valor_base,
                Alquiler = t.alquiler
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<TarifaContadorListItemDto>> GetPagedAsync(
        TarifaContadorFilterDto? filtro,
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
            .Select(t => new TarifaContadorListItemDto
            {
                Id = t.ide,
                Tipo = t.tipo,
                CategoriaId = t.categoria_id,
                Codigo = t.codigo,
                Descripcion = t.descripcion,
                Minimo = t.minimo,
                Maximo = t.maximo,
                Cuota = t.cuota,
                ValorBase = t.valor_base,
                Alquiler = t.alquiler
            })
            .ToListAsync(ct);

        return new PagedResult<TarifaContadorListItemDto>(items, total);
    }

    public async Task<TarifaContadorEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.tarifas_contadors
            .AsNoTracking()
            .Where(t => t.ide == id)
            .Select(t => new TarifaContadorEditDto
            {
                Id = t.ide,
                Tipo = t.tipo,
                CategoriaId = t.categoria_id,
                Codigo = t.codigo,
                Descripcion = t.descripcion,
                Minimo = t.minimo,
                Maximo = t.maximo,
                Cuota = t.cuota,
                ValorBase = t.valor_base,
                Alquiler = t.alquiler
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TarifaContadorEditDto> CreateAsync(TarifaContadorEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidarRangos(dto);

        if (dto.Tipo <= 0)
        {
            throw new ArgumentException("El tipo no es valido.", nameof(dto.Tipo));
        }

        if (!string.IsNullOrWhiteSpace(dto.Codigo) && !dto.CategoriaId.HasValue)
        {
            throw new ArgumentException("Debe seleccionar categoria cuando se especifica un codigo.", nameof(dto.CategoriaId));
        }

        var codigoNormalizado = NormalizeOptional(dto.Codigo);
        if (!string.IsNullOrWhiteSpace(codigoNormalizado) && dto.CategoriaId.HasValue)
        {
            var existeTarifa = await _context.tarifas
                .AsNoTracking()
                .AnyAsync(t => t.tipo == dto.Tipo && t.categoria_id == dto.CategoriaId.Value && t.codigo == codigoNormalizado, ct);

            if (!existeTarifa)
            {
                throw new InvalidOperationException("No existe una tarifa base para el tipo, categoria y letra seleccionados.");
            }
        }

        var descripcionNormalizada = NormalizeOptional(dto.Descripcion);
        var entity = new tarifas_contador
        {
            tipo = dto.Tipo,
            categoria_id = dto.CategoriaId,
            codigo = codigoNormalizado,
            descripcion = descripcionNormalizada,
            minimo = dto.Minimo,
            maximo = dto.Maximo,
            cuota = dto.Cuota,
            valor_base = dto.ValorBase,
            alquiler = dto.Alquiler
        };

        _context.tarifas_contadors.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.ide;
        dto.Codigo = codigoNormalizado;
        dto.Descripcion = descripcionNormalizada;
        return dto;
    }

    public async Task<TarifaContadorEditDto> UpdateAsync(int id, TarifaContadorEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El registro no es valido.");
        }

        ValidarRangos(dto);

        var entity = await _context.tarifas_contadors.FirstOrDefaultAsync(t => t.ide == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("La tarifa de contador no existe.");
        }

        if (dto.Tipo <= 0)
        {
            throw new ArgumentException("El tipo no es valido.", nameof(dto.Tipo));
        }

        if (!string.IsNullOrWhiteSpace(dto.Codigo) && !dto.CategoriaId.HasValue)
        {
            throw new ArgumentException("Debe seleccionar categoria cuando se especifica un codigo.", nameof(dto.CategoriaId));
        }

        var codigoNormalizado = NormalizeOptional(dto.Codigo);
        if (!string.IsNullOrWhiteSpace(codigoNormalizado) && dto.CategoriaId.HasValue)
        {
            var existeTarifa = await _context.tarifas
                .AsNoTracking()
                .AnyAsync(t => t.tipo == dto.Tipo && t.categoria_id == dto.CategoriaId.Value && t.codigo == codigoNormalizado, ct);

            if (!existeTarifa)
            {
                throw new InvalidOperationException("No existe una tarifa base para el tipo, categoria y letra seleccionados.");
            }
        }

        entity.tipo = dto.Tipo;
        var descripcionNormalizada = NormalizeOptional(dto.Descripcion);
        entity.categoria_id = dto.CategoriaId;
        entity.codigo = codigoNormalizado;
        entity.descripcion = descripcionNormalizada;
        entity.minimo = dto.Minimo;
        entity.maximo = dto.Maximo;
        entity.cuota = dto.Cuota;
        entity.valor_base = dto.ValorBase;
        entity.alquiler = dto.Alquiler;

        await _context.SaveChangesAsync(ct);
        dto.Id = entity.ide;
        dto.Codigo = codigoNormalizado;
        dto.Descripcion = descripcionNormalizada;
        return dto;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El registro no es valido.");
        }

        var entity = await _context.tarifas_contadors.FirstOrDefaultAsync(t => t.ide == id, ct);
        if (entity is null)
        {
            return false;
        }

        _context.tarifas_contadors.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<tarifas_contador> BuildQuery(TarifaContadorFilterDto? filtro)
    {
        filtro ??= new TarifaContadorFilterDto();

        var query = _context.tarifas_contadors.AsNoTracking().AsQueryable();

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

    private static IQueryable<tarifas_contador> ApplySort(IQueryable<tarifas_contador> query, string? sortField, bool sortDesc)
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
            "minimo" => sortDesc ? query.OrderByDescending(t => t.minimo) : query.OrderBy(t => t.minimo),
            "maximo" => sortDesc ? query.OrderByDescending(t => t.maximo) : query.OrderBy(t => t.maximo),
            "cuota" => sortDesc ? query.OrderByDescending(t => t.cuota) : query.OrderBy(t => t.cuota),
            "valorbase" => sortDesc ? query.OrderByDescending(t => t.valor_base) : query.OrderBy(t => t.valor_base),
            "alquiler" => sortDesc ? query.OrderByDescending(t => t.alquiler) : query.OrderBy(t => t.alquiler),
            _ => query.OrderBy(t => t.tipo).ThenBy(t => t.categoria_id).ThenBy(t => t.codigo)
        };
    }

    private static void ValidarRangos(TarifaContadorEditDto dto)
    {
        if (dto.Minimo.HasValue && dto.Maximo.HasValue && dto.Minimo.Value > dto.Maximo.Value)
        {
            throw new ArgumentException("El minimo no puede ser mayor que el maximo.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
