using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Mantenimiento del catálogo de categorías de unidades de medida (alm_categoria_unidad).
/// Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// </summary>
public sealed class CategoriaUnidadService : ICategoriaUnidadService
{
    private readonly SiadDbContext _context;

    public CategoriaUnidadService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CategoriaUnidadListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ClasificacionFilterDto();
        var query = _context.alm_categoria_unidads.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(c => c.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(c => EF.Functions.ILike(c.nombre, like) || EF.Functions.ILike(c.descripcion ?? string.Empty, like))
                : query.Where(c => c.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(c => c.nombre)
            .Select(c => new CategoriaUnidadListItemDto
            {
                Id = c.id,
                Nombre = c.nombre,
                Descripcion = c.descripcion,
                Activo = c.activo
            })
            .ToListAsync(ct);
    }

    public async Task<CategoriaUnidadEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_categoria_unidads.AsNoTracking()
            .Where(c => c.id == id)
            .Select(c => new CategoriaUnidadEditDto
            {
                Id = c.id,
                Nombre = c.nombre,
                Descripcion = c.descripcion,
                Activo = c.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CategoriaUnidadLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_categoria_unidads.AsNoTracking()
            .Where(c => c.activo)
            .OrderBy(c => c.nombre)
            .Select(c => new CategoriaUnidadLookupDto { Id = c.id, Nombre = c.nombre })
            .ToListAsync(ct);
    }

    public async Task<CategoriaUnidadEditDto> CreateAsync(CategoriaUnidadEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 30, "nombre");
        var nombreLower = nombre.ToLower();

        if (await _context.alm_categoria_unidads.AsNoTracking().AnyAsync(c => c.nombre.ToLower() == nombreLower, ct))
        {
            throw new InvalidOperationException($"Ya existe una categoría con el nombre {nombre}.");
        }

        var entity = new alm_categoria_unidad
        {
            nombre = nombre,
            descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 100),
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_categoria_unidads.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<CategoriaUnidadEditDto> UpdateAsync(int id, CategoriaUnidadEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_categoria_unidads.FirstOrDefaultAsync(c => c.id == id, ct)
                     ?? throw new KeyNotFoundException("La categoría no existe.");

        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 30, "nombre");
        var nombreLower = nombre.ToLower();

        if (await _context.alm_categoria_unidads.AsNoTracking().AnyAsync(c => c.nombre.ToLower() == nombreLower && c.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe una categoría con el nombre {nombre}.");
        }

        entity.nombre = nombre;
        entity.descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 100);
        entity.activo = dto.Activo;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_categoria_unidads.FirstOrDefaultAsync(c => c.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
