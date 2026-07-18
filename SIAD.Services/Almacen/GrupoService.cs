using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Mantenimiento del catálogo de categorías de artículo (alm_grupo). Desde la
/// unificación línea→tipo (2026-07-16) cada categoría cuelga de un tipo de
/// artículo (alm_tipo_articulo).
/// </summary>
public sealed class GrupoService : IGrupoService
{
    private readonly SiadDbContext _context;

    public GrupoService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<GrupoListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ClasificacionFilterDto();
        var query = _context.alm_grupos.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(g => g.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(g => EF.Functions.ILike(g.codigo, like) || EF.Functions.ILike(g.nombre, like))
                : query.Where(g => g.codigo.ToLower().Contains(term.ToLower()) || g.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(g => g.codigo)
            .Select(g => new GrupoListItemDto
            {
                Id = g.id,
                Codigo = g.codigo,
                Nombre = g.nombre,
                TipoArticuloId = g.tipo_articulo_id,
                TipoArticuloNombre = g.tipo_articulo != null ? g.tipo_articulo.nombre : null,
                Activo = g.activo
            })
            .ToListAsync(ct);
    }

    public async Task<GrupoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_grupos.AsNoTracking()
            .Where(g => g.id == id)
            .Select(g => new GrupoEditDto
            {
                Id = g.id,
                Codigo = g.codigo,
                Nombre = g.nombre,
                TipoArticuloId = g.tipo_articulo_id,
                Activo = g.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<GrupoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_grupos.AsNoTracking()
            .Where(g => g.activo)
            .OrderBy(g => g.codigo)
            .Select(g => new GrupoLookupDto { Id = g.id, Codigo = g.codigo, Nombre = g.nombre, TipoArticuloId = g.tipo_articulo_id })
            .ToListAsync(ct);
    }

    public async Task<GrupoEditDto> CreateAsync(GrupoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 6, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_grupos.AsNoTracking().AnyAsync(g => g.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe una categoría con el código {codigo}.");
        }

        await ValidarTipoArticuloAsync(dto.TipoArticuloId, ct);

        var entity = new alm_grupo
        {
            codigo = codigo,
            nombre = nombre,
            tipo_articulo_id = dto.TipoArticuloId,
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_grupos.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<GrupoEditDto> UpdateAsync(int id, GrupoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_grupos.FirstOrDefaultAsync(g => g.id == id, ct)
                     ?? throw new KeyNotFoundException("La categoría no existe.");

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 6, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_grupos.AsNoTracking().AnyAsync(g => g.codigo == codigo && g.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe una categoría con el código {codigo}.");
        }

        await ValidarTipoArticuloAsync(dto.TipoArticuloId, ct);

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.tipo_articulo_id = dto.TipoArticuloId;
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
        var entity = await _context.alm_grupos.FirstOrDefaultAsync(g => g.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>El tipo es opcional en la categoría, pero si viene debe existir (en la empresa actual).</summary>
    private async Task ValidarTipoArticuloAsync(int? tipoArticuloId, CancellationToken ct)
    {
        if (!tipoArticuloId.HasValue) return;
        var existe = await _context.alm_tipo_articulos.AsNoTracking()
            .AnyAsync(t => t.id == tipoArticuloId.Value, ct);
        if (!existe)
        {
            throw new InvalidOperationException("El tipo de artículo seleccionado no existe.");
        }
    }
}
