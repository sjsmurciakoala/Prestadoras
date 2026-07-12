using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Mantenimiento del catálogo de unidades de medida (alm_unidad_medida), con
/// soporte de conversión (unidad base + factor). Multiempresa vía SiadDbContext.
/// </summary>
public sealed class UnidadesMedidaService : IUnidadesMedidaService
{
    private readonly SiadDbContext _context;

    public UnidadesMedidaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UnidadMedidaListItemDto>> GetAsync(UnidadMedidaFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new UnidadMedidaFilterDto();

        var query = _context.alm_unidad_medidas.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(u => u.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            var categoria = filtro.Categoria.Trim();
            query = query.Where(u => u.categoria == categoria);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(u =>
                    EF.Functions.ILike(u.codigo, likePattern) ||
                    EF.Functions.ILike(u.nombre, likePattern) ||
                    EF.Functions.ILike(u.abreviatura ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(u =>
                    u.codigo.ToLowerInvariant().Contains(lowered) ||
                    u.nombre.ToLowerInvariant().Contains(lowered) ||
                    (u.abreviatura ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return await query
            .OrderBy(u => u.categoria)
            .ThenByDescending(u => u.factor_conversion)
            .ThenBy(u => u.codigo)
            .Select(u => new UnidadMedidaListItemDto
            {
                Id = u.id,
                Codigo = u.codigo,
                Nombre = u.nombre,
                Abreviatura = u.abreviatura,
                Categoria = u.categoria,
                PermiteDecimales = u.permite_decimales,
                Activo = u.activo,
                UnidadBaseId = u.unidad_base_id,
                UnidadBaseCodigo = u.unidad_base != null ? u.unidad_base.codigo : null,
                FactorConversion = u.factor_conversion
            })
            .ToListAsync(ct);
    }

    public async Task<UnidadMedidaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.alm_unidad_medidas
            .AsNoTracking()
            .Where(u => u.id == id)
            .Select(u => new UnidadMedidaEditDto
            {
                Id = u.id,
                Codigo = u.codigo,
                Nombre = u.nombre,
                Abreviatura = u.abreviatura,
                Categoria = u.categoria,
                PermiteDecimales = u.permite_decimales,
                Activo = u.activo,
                UnidadBaseId = u.unidad_base_id,
                FactorConversion = u.factor_conversion
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<UnidadMedidaLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_unidad_medidas
            .AsNoTracking()
            .Where(u => u.activo)
            .OrderBy(u => u.codigo)
            .Select(u => new UnidadMedidaLookupDto
            {
                Id = u.id,
                Codigo = u.codigo,
                Nombre = u.nombre,
                Abreviatura = u.abreviatura
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetCategoriasAsync(CancellationToken ct = default)
    {
        return await _context.alm_unidad_medidas
            .AsNoTracking()
            .Where(u => u.categoria != null && u.categoria != "")
            .Select(u => u.categoria!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<UnidadMedidaEditDto> CreateAsync(UnidadMedidaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeRequired(dto.Codigo, 10, "código", uppercase: true);
        var nombre = NormalizeRequired(dto.Nombre, 60, "nombre");

        var exists = await _context.alm_unidad_medidas
            .AsNoTracking()
            .AnyAsync(u => u.codigo == codigo, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe una unidad con el código {codigo}.");
        }

        var (baseId, factor) = await ResolverConversionAsync(dto, id: null, ct);

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var entity = new alm_unidad_medida
        {
            codigo = codigo,
            nombre = nombre,
            abreviatura = NormalizeOptional(dto.Abreviatura, 10),
            categoria = NormalizeOptional(dto.Categoria, 30),
            permite_decimales = dto.PermiteDecimales,
            activo = dto.Activo,
            unidad_base_id = baseId,
            factor_conversion = factor,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.alm_unidad_medidas.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    public async Task<UnidadMedidaEditDto> UpdateAsync(int id, UnidadMedidaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "La unidad no es válida.");
        }

        var entity = await _context.alm_unidad_medidas.FirstOrDefaultAsync(u => u.id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("La unidad no existe.");
        }

        var codigo = NormalizeRequired(dto.Codigo, 10, "código", uppercase: true);
        var nombre = NormalizeRequired(dto.Nombre, 60, "nombre");

        var exists = await _context.alm_unidad_medidas
            .AsNoTracking()
            .AnyAsync(u => u.codigo == codigo && u.id != id, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe una unidad con el código {codigo}.");
        }

        var (baseId, factor) = await ResolverConversionAsync(dto, id, ct);

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.abreviatura = NormalizeOptional(dto.Abreviatura, 10);
        entity.categoria = NormalizeOptional(dto.Categoria, 30);
        entity.permite_decimales = dto.PermiteDecimales;
        entity.activo = dto.Activo;
        entity.unidad_base_id = baseId;
        entity.factor_conversion = factor;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "La unidad no es válida.");
        }

        var entity = await _context.alm_unidad_medidas.FirstOrDefaultAsync(u => u.id == id, ct);
        if (entity is null)
        {
            return false;
        }

        if (!entity.activo)
        {
            return true;
        }

        entity.activo = false;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Valida y normaliza la unidad base y el factor. Una unidad base tiene
    /// base = null y factor = 1; no puede ser su propia base.
    /// </summary>
    private async Task<(int? baseId, decimal factor)> ResolverConversionAsync(UnidadMedidaEditDto dto, int? id, CancellationToken ct)
    {
        if (dto.UnidadBaseId is null)
        {
            return (null, 1m);
        }

        if (id.HasValue && dto.UnidadBaseId.Value == id.Value)
        {
            throw new InvalidOperationException("Una unidad no puede ser su propia unidad base.");
        }

        var baseExiste = await _context.alm_unidad_medidas
            .AsNoTracking()
            .AnyAsync(u => u.id == dto.UnidadBaseId.Value, ct);

        if (!baseExiste)
        {
            throw new InvalidOperationException("La unidad base seleccionada no existe.");
        }

        if (dto.FactorConversion <= 0)
        {
            throw new InvalidOperationException("El factor de conversión debe ser mayor que cero.");
        }

        return (dto.UnidadBaseId, dto.FactorConversion);
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} supera {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? trimmed.ToUpperInvariant() : trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }
}
