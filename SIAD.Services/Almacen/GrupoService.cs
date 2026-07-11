using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de grupos de producto (alm_grupo).</summary>
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
                LineaId = g.linea_id,
                LineaNombre = g.linea != null ? g.linea.nombre : null,
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
                LineaId = g.linea_id,
                Activo = g.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<GrupoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_grupos.AsNoTracking()
            .Where(g => g.activo)
            .OrderBy(g => g.codigo)
            .Select(g => new GrupoLookupDto { Id = g.id, Codigo = g.codigo, Nombre = g.nombre, LineaId = g.linea_id })
            .ToListAsync(ct);
    }

    public async Task<GrupoEditDto> CreateAsync(GrupoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 6, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_grupos.AsNoTracking().AnyAsync(g => g.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe un grupo con el código {codigo}.");
        }

        var lineaCodigo = await ResolverLineaCodigoAsync(dto.LineaId, ct);

        var entity = new alm_grupo
        {
            codigo = codigo,
            nombre = nombre,
            linea_id = dto.LineaId,
            linea_codigo = lineaCodigo,
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
                     ?? throw new KeyNotFoundException("El grupo no existe.");

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 6, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_grupos.AsNoTracking().AnyAsync(g => g.codigo == codigo && g.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un grupo con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.linea_id = dto.LineaId;
        entity.linea_codigo = await ResolverLineaCodigoAsync(dto.LineaId, ct);
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

    private async Task<string?> ResolverLineaCodigoAsync(int? lineaId, CancellationToken ct)
    {
        if (!lineaId.HasValue) return null;
        var linea = await _context.alm_lineas.AsNoTracking()
            .Where(l => l.id == lineaId.Value)
            .Select(l => l.codigo)
            .FirstOrDefaultAsync(ct);
        if (linea is null)
        {
            throw new InvalidOperationException("La línea seleccionada no existe.");
        }
        return linea;
    }
}
