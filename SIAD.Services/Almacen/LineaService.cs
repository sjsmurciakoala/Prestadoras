using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de líneas de inventario (alm_linea).</summary>
public sealed class LineaService : ILineaService
{
    private readonly SiadDbContext _context;

    public LineaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LineaListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ClasificacionFilterDto();
        var query = _context.alm_lineas.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(l => l.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(l => EF.Functions.ILike(l.codigo, like) || EF.Functions.ILike(l.nombre, like))
                : query.Where(l => l.codigo.ToLower().Contains(term.ToLower()) || l.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(l => l.codigo)
            .Select(l => new LineaListItemDto
            {
                Id = l.id,
                Codigo = l.codigo,
                Nombre = l.nombre,
                CuentaContable = l.cuenta_contable,
                Activo = l.activo
            })
            .ToListAsync(ct);
    }

    public async Task<LineaEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_lineas.AsNoTracking()
            .Where(l => l.id == id)
            .Select(l => new LineaEditDto
            {
                Id = l.id,
                Codigo = l.codigo,
                Nombre = l.nombre,
                CuentaContable = l.cuenta_contable,
                CuentaContableAnterior = l.cuenta_contable_anterior,
                Activo = l.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LineaLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_lineas.AsNoTracking()
            .Where(l => l.activo)
            .OrderBy(l => l.codigo)
            .Select(l => new LineaLookupDto { Id = l.id, Codigo = l.codigo, Nombre = l.nombre })
            .ToListAsync(ct);
    }

    public async Task<LineaEditDto> CreateAsync(LineaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 2, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_lineas.AsNoTracking().AnyAsync(l => l.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe una línea con el código {codigo}.");
        }

        var entity = new alm_linea
        {
            codigo = codigo,
            nombre = nombre,
            cuenta_contable = ClasificacionNormalizer.Opcional(dto.CuentaContable, 25),
            cuenta_contable_anterior = ClasificacionNormalizer.Opcional(dto.CuentaContableAnterior, 30),
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_lineas.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<LineaEditDto> UpdateAsync(int id, LineaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_lineas.FirstOrDefaultAsync(l => l.id == id, ct)
                     ?? throw new KeyNotFoundException("La línea no existe.");

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 2, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_lineas.AsNoTracking().AnyAsync(l => l.codigo == codigo && l.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe una línea con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.cuenta_contable = ClasificacionNormalizer.Opcional(dto.CuentaContable, 25);
        entity.cuenta_contable_anterior = ClasificacionNormalizer.Opcional(dto.CuentaContableAnterior, 30);
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
        var entity = await _context.alm_lineas.FirstOrDefaultAsync(l => l.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
