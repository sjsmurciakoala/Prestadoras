using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de estanterías (nivel 2 de ubicación, dentro de una bodega).</summary>
public sealed class EstanteriaService : IEstanteriaService
{
    private readonly SiadDbContext _context;

    public EstanteriaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EstanteriaListItemDto>> GetAsync(UbicacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new UbicacionFilterDto();
        var query = _context.alm_estanterias.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(e => e.activo == filtro.Activo.Value);
        }

        if (filtro.BodegaId.HasValue)
        {
            query = query.Where(e => e.bodega_id == filtro.BodegaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(e => EF.Functions.ILike(e.codigo, like) || EF.Functions.ILike(e.nombre ?? string.Empty, like))
                : query.Where(e => e.codigo.ToLower().Contains(term.ToLower()) || (e.nombre ?? string.Empty).ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(e => e.codigo)
            .Select(e => new EstanteriaListItemDto
            {
                Id = e.id,
                BodegaId = e.bodega_id,
                BodegaNombre = e.bodega != null ? e.bodega.nombre : null,
                Codigo = e.codigo,
                Nombre = e.nombre,
                Activo = e.activo
            })
            .ToListAsync(ct);
    }

    public async Task<EstanteriaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_estanterias.AsNoTracking()
            .Where(e => e.id == id)
            .Select(e => new EstanteriaEditDto
            {
                Id = e.id,
                BodegaId = e.bodega_id,
                Codigo = e.codigo,
                Nombre = e.nombre,
                Activo = e.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<EstanteriaLookupDto>> GetLookupAsync(int bodegaId, CancellationToken ct = default)
    {
        return await _context.alm_estanterias.AsNoTracking()
            .Where(e => e.activo && e.bodega_id == bodegaId)
            .OrderBy(e => e.codigo)
            .Select(e => new EstanteriaLookupDto { Id = e.id, BodegaId = e.bodega_id, Codigo = e.codigo, Nombre = e.nombre })
            .ToListAsync(ct);
    }

    public async Task<EstanteriaEditDto> CreateAsync(EstanteriaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarBodegaAsync(dto.BodegaId, ct);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Opcional(dto.Nombre, 100);

        if (await _context.alm_estanterias.AsNoTracking().AnyAsync(e => e.bodega_id == dto.BodegaId && e.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe una estantería con el código {codigo} en esa bodega.");
        }

        var entity = new alm_estanteria
        {
            bodega_id = dto.BodegaId,
            codigo = codigo,
            nombre = nombre,
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_estanterias.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<EstanteriaEditDto> UpdateAsync(int id, EstanteriaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_estanterias.FirstOrDefaultAsync(e => e.id == id, ct)
                     ?? throw new KeyNotFoundException("La estantería no existe.");

        await ValidarBodegaAsync(dto.BodegaId, ct);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Opcional(dto.Nombre, 100);

        if (await _context.alm_estanterias.AsNoTracking().AnyAsync(e => e.bodega_id == dto.BodegaId && e.codigo == codigo && e.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe una estantería con el código {codigo} en esa bodega.");
        }

        entity.bodega_id = dto.BodegaId;
        entity.codigo = codigo;
        entity.nombre = nombre;
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
        var entity = await _context.alm_estanterias.FirstOrDefaultAsync(e => e.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValidarBodegaAsync(int bodegaId, CancellationToken ct)
    {
        var existe = await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.id == bodegaId && b.activo, ct);
        if (!existe)
        {
            throw new InvalidOperationException("La bodega seleccionada no existe o está inactiva.");
        }
    }
}
