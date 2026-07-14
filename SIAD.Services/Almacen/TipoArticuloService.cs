using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de tipos de artículo (clasificación por uso).</summary>
public sealed class TipoArticuloService : ITipoArticuloService
{
    private readonly SiadDbContext _context;

    public TipoArticuloService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TipoArticuloListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ClasificacionFilterDto();
        var query = _context.alm_tipo_articulos.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(t => t.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(t => EF.Functions.ILike(t.codigo, like) || EF.Functions.ILike(t.nombre, like))
                : query.Where(t => t.codigo.ToLower().Contains(term.ToLower()) || t.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(t => t.codigo)
            .Select(t => new TipoArticuloListItemDto
            {
                Id = t.id,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Descripcion = t.descripcion,
                Activo = t.activo
            })
            .ToListAsync(ct);
    }

    public async Task<TipoArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_tipo_articulos.AsNoTracking()
            .Where(t => t.id == id)
            .Select(t => new TipoArticuloEditDto
            {
                Id = t.id,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Descripcion = t.descripcion,
                CuentaInventario = t.cuenta_inventario,
                CuentaCostoVentas = t.cuenta_costo_ventas,
                CuentaVentas = t.cuenta_ventas,
                CuentaAjustes = t.cuenta_ajustes,
                CuentaDevoluciones = t.cuenta_devoluciones,
                Activo = t.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TipoArticuloLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_tipo_articulos.AsNoTracking()
            .Where(t => t.activo)
            .OrderBy(t => t.codigo)
            .Select(t => new TipoArticuloLookupDto { Id = t.id, Codigo = t.codigo, Nombre = t.nombre })
            .ToListAsync(ct);
    }

    public async Task<TipoArticuloEditDto> CreateAsync(TipoArticuloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 60, "nombre");

        if (await _context.alm_tipo_articulos.AsNoTracking().AnyAsync(t => t.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de artículo con el código {codigo}.");
        }

        var entity = new alm_tipo_articulo
        {
            codigo = codigo,
            nombre = nombre,
            descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 200),
            cuenta_inventario = ClasificacionNormalizer.Opcional(dto.CuentaInventario, 20),
            cuenta_costo_ventas = ClasificacionNormalizer.Opcional(dto.CuentaCostoVentas, 20),
            cuenta_ventas = ClasificacionNormalizer.Opcional(dto.CuentaVentas, 20),
            cuenta_ajustes = ClasificacionNormalizer.Opcional(dto.CuentaAjustes, 20),
            cuenta_devoluciones = ClasificacionNormalizer.Opcional(dto.CuentaDevoluciones, 20),
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_tipo_articulos.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<TipoArticuloEditDto> UpdateAsync(int id, TipoArticuloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_tipo_articulos.FirstOrDefaultAsync(t => t.id == id, ct)
                     ?? throw new KeyNotFoundException("El tipo de artículo no existe.");

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 60, "nombre");

        if (await _context.alm_tipo_articulos.AsNoTracking().AnyAsync(t => t.codigo == codigo && t.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de artículo con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 200);
        entity.cuenta_inventario = ClasificacionNormalizer.Opcional(dto.CuentaInventario, 20);
        entity.cuenta_costo_ventas = ClasificacionNormalizer.Opcional(dto.CuentaCostoVentas, 20);
        entity.cuenta_ventas = ClasificacionNormalizer.Opcional(dto.CuentaVentas, 20);
        entity.cuenta_ajustes = ClasificacionNormalizer.Opcional(dto.CuentaAjustes, 20);
        entity.cuenta_devoluciones = ClasificacionNormalizer.Opcional(dto.CuentaDevoluciones, 20);
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
        var entity = await _context.alm_tipo_articulos.FirstOrDefaultAsync(t => t.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
