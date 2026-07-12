using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de bodegas (almacenes físicos).</summary>
public sealed class BodegaService : IBodegaService
{
    private readonly SiadDbContext _context;

    public BodegaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<BodegaListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ClasificacionFilterDto();
        var query = _context.alm_bodegas.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(b => b.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(b => EF.Functions.ILike(b.codigo, like) || EF.Functions.ILike(b.nombre, like))
                : query.Where(b => b.codigo.ToLower().Contains(term.ToLower()) || b.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(b => b.codigo)
            .Select(b => new BodegaListItemDto
            {
                Id = b.id,
                Codigo = b.codigo,
                Nombre = b.nombre,
                Responsable = b.responsable,
                Activo = b.activo
            })
            .ToListAsync(ct);
    }

    public async Task<BodegaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_bodegas.AsNoTracking()
            .Where(b => b.id == id)
            .Select(b => new BodegaEditDto
            {
                Id = b.id,
                Codigo = b.codigo,
                Nombre = b.nombre,
                Direccion = b.direccion,
                Responsable = b.responsable,
                Activo = b.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<BodegaLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_bodegas.AsNoTracking()
            .Where(b => b.activo)
            .OrderBy(b => b.codigo)
            .Select(b => new BodegaLookupDto { Id = b.id, Codigo = b.codigo, Nombre = b.nombre })
            .ToListAsync(ct);
    }

    public async Task<BodegaEditDto> CreateAsync(BodegaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe una bodega con el código {codigo}.");
        }

        var entity = new alm_bodega
        {
            codigo = codigo,
            nombre = nombre,
            direccion = ClasificacionNormalizer.Opcional(dto.Direccion, 200),
            responsable = ClasificacionNormalizer.Opcional(dto.Responsable, 100),
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_bodegas.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<BodegaEditDto> UpdateAsync(int id, BodegaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_bodegas.FirstOrDefaultAsync(b => b.id == id, ct)
                     ?? throw new KeyNotFoundException("La bodega no existe.");

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.codigo == codigo && b.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe una bodega con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.direccion = ClasificacionNormalizer.Opcional(dto.Direccion, 200);
        entity.responsable = ClasificacionNormalizer.Opcional(dto.Responsable, 100);
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
        var entity = await _context.alm_bodegas.FirstOrDefaultAsync(b => b.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
