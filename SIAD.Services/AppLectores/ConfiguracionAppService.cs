using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.AppLectores;

public sealed class ConfiguracionAppService : IConfiguracionAppService
{
    private readonly SiadDbContext _context;

    public ConfiguracionAppService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ConfiguracionAppListItemDto>> GetAsync(ConfiguracionAppFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(c => c.ide)
            .Select(c => new ConfiguracionAppListItemDto(c.ide, c.descripcion, c.valor_numeros, c.valor_letras))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ConfiguracionAppListItemDto>> GetPagedAsync(
        ConfiguracionAppFilterDto? filtro,
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
            .Select(c => new ConfiguracionAppListItemDto(c.ide, c.descripcion, c.valor_numeros, c.valor_letras))
            .ToListAsync(ct);

        return new PagedResult<ConfiguracionAppListItemDto>(items, total);
    }

    public async Task<ConfiguracionAppEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.configuracion_app_lectura_medidores
            .AsNoTracking()
            .Where(c => c.ide == id)
            .Select(c => new ConfiguracionAppEditDto
            {
                Id = c.ide,
                Descripcion = c.descripcion ?? string.Empty,
                ValorNumeros = c.valor_numeros,
                ValorLetras = c.valor_letras
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ConfiguracionAppEditDto> CreateAsync(ConfiguracionAppEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = new configuracion_app_lectura_medidore
        {
            descripcion = NormalizeRequired(dto.Descripcion, 200, "descripcion"),
            valor_numeros = dto.ValorNumeros,
            valor_letras = NormalizeOptional(dto.ValorLetras, 100, "valor en letras")
        };

        _context.configuracion_app_lectura_medidores.Add(entity);
        await _context.SaveChangesAsync(ct);

        return new ConfiguracionAppEditDto
        {
            Id = entity.ide,
            Descripcion = entity.descripcion ?? string.Empty,
            ValorNumeros = entity.valor_numeros,
            ValorLetras = entity.valor_letras
        };
    }

    public async Task<ConfiguracionAppEditDto> UpdateAsync(int id, ConfiguracionAppEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.configuracion_app_lectura_medidores.FirstOrDefaultAsync(c => c.ide == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro la configuracion {id}.");
        }

        entity.descripcion = NormalizeRequired(dto.Descripcion, 200, "descripcion");
        entity.valor_numeros = dto.ValorNumeros;
        entity.valor_letras = NormalizeOptional(dto.ValorLetras, 100, "valor en letras");

        await _context.SaveChangesAsync(ct);

        return new ConfiguracionAppEditDto
        {
            Id = entity.ide,
            Descripcion = entity.descripcion ?? string.Empty,
            ValorNumeros = entity.valor_numeros,
            ValorLetras = entity.valor_letras
        };
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El ID de configuracion no es valido.");
        }

        if (EsProtegida(id))
        {
            throw new InvalidOperationException("No se puede eliminar la configuracion requerida para limites de descuento.");
        }

        var entity = await _context.configuracion_app_lectura_medidores.FirstOrDefaultAsync(c => c.ide == id, ct);
        if (entity is null)
        {
            return false;
        }

        _context.configuracion_app_lectura_medidores.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<configuracion_app_lectura_medidore> BuildQuery(ConfiguracionAppFilterDto? filtro)
    {
        filtro ??= new ConfiguracionAppFilterDto();

        var query = _context.configuracion_app_lectura_medidores.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            if (int.TryParse(term, out var id))
            {
                query = query.Where(c => c.ide == id);
            }
            else
            {
                var likePattern = $"%{term}%";
                if (_context.Database.IsRelational())
                {
                    query = query.Where(c =>
                        EF.Functions.ILike(c.descripcion ?? string.Empty, likePattern)
                        || EF.Functions.ILike(c.valor_letras ?? string.Empty, likePattern));
                }
                else
                {
                    var lowered = term.ToLowerInvariant();
                    query = query.Where(c =>
                        (c.descripcion ?? string.Empty).ToLower().Contains(lowered)
                        || (c.valor_letras ?? string.Empty).ToLower().Contains(lowered));
                }
            }
        }

        return query;
    }

    private static IQueryable<configuracion_app_lectura_medidore> ApplySort(
        IQueryable<configuracion_app_lectura_medidore> query,
        string? sortField,
        bool sortDesc)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return query.OrderBy(c => c.ide);
        }

        var field = sortField.Trim();
        return field switch
        {
            nameof(ConfiguracionAppListItemDto.Id) or "id" or "ide" => sortDesc
                ? query.OrderByDescending(c => c.ide)
                : query.OrderBy(c => c.ide),
            nameof(ConfiguracionAppListItemDto.Descripcion) or "descripcion" => sortDesc
                ? query.OrderByDescending(c => c.descripcion).ThenByDescending(c => c.ide)
                : query.OrderBy(c => c.descripcion).ThenBy(c => c.ide),
            nameof(ConfiguracionAppListItemDto.ValorNumeros) or "valorNumeros" or "valor_numeros" => sortDesc
                ? query.OrderByDescending(c => c.valor_numeros).ThenByDescending(c => c.ide)
                : query.OrderBy(c => c.valor_numeros).ThenBy(c => c.ide),
            nameof(ConfiguracionAppListItemDto.ValorLetras) or "valorLetras" or "valor_letras" => sortDesc
                ? query.OrderByDescending(c => c.valor_letras).ThenByDescending(c => c.ide)
                : query.OrderBy(c => c.valor_letras).ThenBy(c => c.ide),
            _ => query.OrderBy(c => c.ide)
        };
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"La {fieldName} es obligatoria.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"La {fieldName} no puede superar {maxLength} caracteres.", nameof(value));
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

    private static bool EsProtegida(int id) => id == 5 || id == 6;
}
