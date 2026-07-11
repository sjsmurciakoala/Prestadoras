using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de estantes (nivel 3, ubicación direccionable dentro de una estantería).</summary>
public sealed class EstanteService : IEstanteService
{
    private readonly SiadDbContext _context;

    public EstanteService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EstanteListItemDto>> GetAsync(UbicacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new UbicacionFilterDto();
        var query = _context.alm_estantes.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(e => e.activo == filtro.Activo.Value);
        }

        if (filtro.EstanteriaId.HasValue)
        {
            query = query.Where(e => e.estanteria_id == filtro.EstanteriaId.Value);
        }

        if (filtro.BodegaId.HasValue)
        {
            query = query.Where(e => e.estanteria != null && e.estanteria.bodega_id == filtro.BodegaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(e => EF.Functions.ILike(e.codigo, like) || EF.Functions.ILike(e.descripcion ?? string.Empty, like))
                : query.Where(e => e.codigo.ToLower().Contains(term.ToLower()) || (e.descripcion ?? string.Empty).ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(e => e.codigo)
            .Select(e => new EstanteListItemDto
            {
                Id = e.id,
                EstanteriaId = e.estanteria_id,
                EstanteriaCodigo = e.estanteria != null ? e.estanteria.codigo : null,
                BodegaNombre = e.estanteria != null && e.estanteria.bodega != null ? e.estanteria.bodega.nombre : null,
                Codigo = e.codigo,
                Descripcion = e.descripcion,
                Activo = e.activo
            })
            .ToListAsync(ct);
    }

    public async Task<EstanteEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_estantes.AsNoTracking()
            .Where(e => e.id == id)
            .Select(e => new EstanteEditDto
            {
                Id = e.id,
                EstanteriaId = e.estanteria_id,
                Codigo = e.codigo,
                Descripcion = e.descripcion,
                Activo = e.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<EstanteLookupDto>> GetLookupAsync(int estanteriaId, CancellationToken ct = default)
    {
        return await _context.alm_estantes.AsNoTracking()
            .Where(e => e.activo && e.estanteria_id == estanteriaId)
            .OrderBy(e => e.codigo)
            .Select(e => new EstanteLookupDto
            {
                Id = e.id,
                EstanteriaId = e.estanteria_id,
                Codigo = e.codigo,
                UbicacionCodigo = e.estanteria != null && e.estanteria.bodega != null
                    ? e.estanteria.bodega.codigo + "-" + e.estanteria.codigo + "-" + e.codigo
                    : e.codigo
            })
            .ToListAsync(ct);
    }

    public async Task<EstanteEditDto> CreateAsync(EstanteEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarEstanteriaAsync(dto.EstanteriaId, ct);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 150);

        if (await _context.alm_estantes.AsNoTracking().AnyAsync(e => e.estanteria_id == dto.EstanteriaId && e.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe un estante con el código {codigo} en esa estantería.");
        }

        var entity = new alm_estante
        {
            estanteria_id = dto.EstanteriaId,
            codigo = codigo,
            descripcion = descripcion,
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_estantes.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<EstanteEditDto> UpdateAsync(int id, EstanteEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_estantes.FirstOrDefaultAsync(e => e.id == id, ct)
                     ?? throw new KeyNotFoundException("El estante no existe.");

        await ValidarEstanteriaAsync(dto.EstanteriaId, ct);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 150);

        if (await _context.alm_estantes.AsNoTracking().AnyAsync(e => e.estanteria_id == dto.EstanteriaId && e.codigo == codigo && e.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un estante con el código {codigo} en esa estantería.");
        }

        entity.estanteria_id = dto.EstanteriaId;
        entity.codigo = codigo;
        entity.descripcion = descripcion;
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
        var entity = await _context.alm_estantes.FirstOrDefaultAsync(e => e.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValidarEstanteriaAsync(int estanteriaId, CancellationToken ct)
    {
        var existe = await _context.alm_estanterias.AsNoTracking().AnyAsync(e => e.id == estanteriaId && e.activo, ct);
        if (!existe)
        {
            throw new InvalidOperationException("La estantería seleccionada no existe o está inactiva.");
        }
    }
}
