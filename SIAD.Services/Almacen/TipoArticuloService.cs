using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>Mantenimiento del catálogo de tipos de artículo (clasificación por uso).</summary>
public sealed class TipoArticuloService : ITipoArticuloService
{
    private readonly SiadDbContext _context;

    /// <summary>Código del impuesto (cfg_impuesto) cuyas tasas se ofrecen para el ISV en compras.</summary>
    private const string CodigoIsv = "ISV";

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

        // La tasa (tipo/porcentaje) se trae cruda y el display se arma en memoria: interpolar
        // con formato no lo traduce EF a SQL.
        var rows = await query
            .OrderBy(t => t.codigo)
            .Select(t => new
            {
                t.id,
                t.codigo,
                t.nombre,
                t.descripcion,
                t.maneja_inventario,
                t.activo,
                TasaTipo = t.impuesto_tasa != null ? t.impuesto_tasa.tipo : null,
                TasaPorc = t.impuesto_tasa != null ? (decimal?)t.impuesto_tasa.porcentaje : null
            })
            .ToListAsync(ct);

        return rows.Select(r => new TipoArticuloListItemDto
        {
            Id = r.id,
            Codigo = r.codigo,
            Nombre = r.nombre,
            Descripcion = r.descripcion,
            ManejaInventario = r.maneja_inventario,
            IsvDisplay = IsvDisplay(r.TasaTipo, r.TasaPorc),
            Activo = r.activo
        }).ToList();
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
                ManejaInventario = t.maneja_inventario,
                ImpuestoTasaId = t.impuesto_tasa_id,
                Activo = t.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TipoArticuloLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var rows = await _context.alm_tipo_articulos.AsNoTracking()
            .Where(t => t.activo)
            .OrderBy(t => t.codigo)
            .Select(t => new
            {
                t.id,
                t.codigo,
                t.nombre,
                t.maneja_inventario,
                t.cuenta_inventario,
                t.cuenta_costo_ventas,
                t.cuenta_ventas,
                t.cuenta_ajustes,
                t.cuenta_devoluciones,
                t.impuesto_tasa_id,
                TasaTipo = t.impuesto_tasa != null ? t.impuesto_tasa.tipo : null,
                TasaPorc = t.impuesto_tasa != null ? (decimal?)t.impuesto_tasa.porcentaje : null
            })
            .ToListAsync(ct);

        return rows.Select(r => new TipoArticuloLookupDto
        {
            Id = r.id,
            Codigo = r.codigo,
            Nombre = r.nombre,
            // El formulario del artículo lo usa para bloquear la pestaña Existencias.
            ManejaInventario = r.maneja_inventario,
            // Cuentas del tipo: el form del artículo las muestra en solo lectura (herencia).
            CuentaInventario = r.cuenta_inventario,
            CuentaCostoVentas = r.cuenta_costo_ventas,
            CuentaVentas = r.cuenta_ventas,
            CuentaAjustes = r.cuenta_ajustes,
            CuentaDevoluciones = r.cuenta_devoluciones,
            ImpuestoTasaId = r.impuesto_tasa_id,
            ImpuestoTasaDisplay = r.impuesto_tasa_id == null ? null : IsvDisplay(r.TasaTipo, r.TasaPorc)
        }).ToList();
    }

    public async Task<TipoArticuloEditDto> CreateAsync(TipoArticuloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 10, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_tipo_articulos.AsNoTracking().AnyAsync(t => t.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de artículo con el código {codigo}.");
        }

        var impuestoTasaId = await ValidarTasaIsvAsync(dto.ImpuestoTasaId, ct);

        var entity = new alm_tipo_articulo
        {
            codigo = codigo,
            nombre = nombre,
            descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 200),
            cuenta_inventario = ClasificacionNormalizer.Opcional(dto.CuentaInventario, 25),
            cuenta_costo_ventas = ClasificacionNormalizer.Opcional(dto.CuentaCostoVentas, 25),
            cuenta_ventas = ClasificacionNormalizer.Opcional(dto.CuentaVentas, 25),
            cuenta_ajustes = ClasificacionNormalizer.Opcional(dto.CuentaAjustes, 25),
            cuenta_devoluciones = ClasificacionNormalizer.Opcional(dto.CuentaDevoluciones, 25),
            maneja_inventario = dto.ManejaInventario,
            impuesto_tasa_id = impuestoTasaId,
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
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 100, "nombre");

        if (await _context.alm_tipo_articulos.AsNoTracking().AnyAsync(t => t.codigo == codigo && t.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de artículo con el código {codigo}.");
        }

        var impuestoTasaId = await ValidarTasaIsvAsync(dto.ImpuestoTasaId, ct);

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.descripcion = ClasificacionNormalizer.Opcional(dto.Descripcion, 200);
        entity.cuenta_inventario = ClasificacionNormalizer.Opcional(dto.CuentaInventario, 25);
        entity.cuenta_costo_ventas = ClasificacionNormalizer.Opcional(dto.CuentaCostoVentas, 25);
        entity.cuenta_ventas = ClasificacionNormalizer.Opcional(dto.CuentaVentas, 25);
        entity.cuenta_ajustes = ClasificacionNormalizer.Opcional(dto.CuentaAjustes, 25);
        entity.cuenta_devoluciones = ClasificacionNormalizer.Opcional(dto.CuentaDevoluciones, 25);
        entity.maneja_inventario = dto.ManejaInventario;
        entity.impuesto_tasa_id = impuestoTasaId;
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

    public async Task<IReadOnlyList<TasaIsvOpcionDto>> GetTasasIsvAsync(CancellationToken ct = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var rows = await _context.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.impuesto!.codigo == CodigoIsv
                     && t.activo
                     && t.vigencia_desde <= hoy
                     && (t.vigencia_hasta == null || t.vigencia_hasta >= hoy))
            .OrderBy(t => t.tipo)
            .ThenBy(t => t.porcentaje)
            .Select(t => new { t.id, t.codigo, t.nombre, t.tipo, t.porcentaje })
            .ToListAsync(ct);

        return rows.Select(r => new TasaIsvOpcionDto
        {
            Id = r.id,
            Codigo = r.codigo,
            Nombre = r.nombre,
            Tipo = r.tipo,
            Porcentaje = r.porcentaje,
            EsGravada = TipoImpuestoTasa.ExigePorcentaje(r.tipo) && r.porcentaje > 0m,
            Display = SelectorDisplay(r.tipo, r.porcentaje, r.nombre)
        }).ToList();
    }

    /// <summary>
    /// Valida que la tasa (si se indicó) exista y pertenezca al ISV. Devuelve el id a guardar
    /// (o null si el tipo no aplica ISV). Es defensa de servidor: el selector ya restringe a
    /// tasas del ISV, pero un cliente podría mandar otro id.
    /// </summary>
    private async Task<int?> ValidarTasaIsvAsync(int? tasaId, CancellationToken ct)
    {
        if (tasaId is not int id) return null;

        var esIsv = await _context.cfg_impuesto_tasas.AsNoTracking()
            .AnyAsync(t => t.id == id && t.impuesto!.codigo == CodigoIsv, ct);
        if (!esIsv)
        {
            throw new InvalidOperationException("La tasa de ISV seleccionada no existe o no pertenece al ISV.");
        }
        return id;
    }

    /// <summary>Etiqueta corta para la lista/herencia: "ISV 15%", "Exento", "Exonerado" o "—".</summary>
    private static string IsvDisplay(string? tipo, decimal? porcentaje)
    {
        if (tipo is null) return "—";
        if (TipoImpuestoTasa.ExigePorcentaje(tipo)) return $"ISV {(porcentaje ?? 0m):0.##}%";
        if (string.Equals(tipo, TipoImpuestoTasa.Exento, StringComparison.OrdinalIgnoreCase)) return "Exento";
        if (string.Equals(tipo, TipoImpuestoTasa.Exonerado, StringComparison.OrdinalIgnoreCase)) return "Exonerado";
        return "—";
    }

    /// <summary>Etiqueta descriptiva para el combo del formulario.</summary>
    private static string SelectorDisplay(string? tipo, decimal porcentaje, string nombre)
    {
        if (TipoImpuestoTasa.ExigePorcentaje(tipo)) return $"ISV {porcentaje:0.##}% (gravado)";
        if (string.Equals(tipo, TipoImpuestoTasa.Exento, StringComparison.OrdinalIgnoreCase)) return "Exento (no registra ISV)";
        if (string.Equals(tipo, TipoImpuestoTasa.Exonerado, StringComparison.OrdinalIgnoreCase)) return "Exonerado";
        return string.IsNullOrWhiteSpace(nombre) ? "—" : nombre;
    }
}
