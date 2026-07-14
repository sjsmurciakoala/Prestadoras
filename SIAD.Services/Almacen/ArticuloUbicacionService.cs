using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Ubicaciones físicas de un artículo por bodega (bodega + ubicación manual + principal).
/// La ubicación es texto libre: cinco campos de 20 caracteres (ubicacion1..5).
/// Las ubicaciones no se eliminan: se DESHABILITAN (activo=false) para conservar el
/// histórico. El rollup de existencia del artículo suma solo las filas activas.
/// Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// </summary>
public sealed class ArticuloUbicacionService : IArticuloUbicacionService
{
    private readonly SiadDbContext _context;

    public ArticuloUbicacionService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ArticuloUbicacionDto>> GetAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        if (articuloId <= 0)
        {
            return Array.Empty<ArticuloUbicacionDto>();
        }

        var query = _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId);

        if (!incluirInactivas)
        {
            query = query.Where(u => u.activo);
        }

        return await query
            .OrderByDescending(u => u.activo)
            .ThenByDescending(u => u.principal)
            .ThenBy(u => u.bodega != null ? u.bodega.codigo : string.Empty)
            .Select(u => new ArticuloUbicacionDto
            {
                Id = u.id,
                BodegaId = u.bodega_id,
                BodegaDisplay = u.bodega != null ? u.bodega.codigo + " — " + u.bodega.nombre : null,
                Ubicacion1 = u.ubicacion1,
                Ubicacion2 = u.ubicacion2,
                Ubicacion3 = u.ubicacion3,
                Ubicacion4 = u.ubicacion4,
                Ubicacion5 = u.ubicacion5,
                Existencia = u.existencia,
                ExistenciaMinima = u.existencia_minima,
                ExistenciaMaxima = u.existencia_maxima,
                PuntoReorden = u.punto_reorden,
                // Campos del motor de movimientos: se leen para mostrarlos, nunca se escriben desde el DTO.
                ExistenciaComprometida = u.existencia_comprometida,
                ExistenciaTransito = u.existencia_transito,
                CostoPromedio = u.costo_promedio,
                UltimoCosto = u.ultimo_costo,
                Principal = u.principal,
                Activo = u.activo
            })
            .ToListAsync(ct);
    }

    public async Task<ArticuloUbicacionDto> AddAsync(int articuloId, ArticuloUbicacionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarArticuloAsync(articuloId, ct);
        await ValidarBodegaAsync(dto.BodegaId, ct);

        // Si ya existe una fila para esa bodega: si está activa es un duplicado;
        // si está deshabilitada, se reactiva (respeta el único (company, articulo, bodega)).
        var existente = await _context.alm_articulo_bodegas
            .FirstOrDefaultAsync(u => u.articulo_id == articuloId && u.bodega_id == dto.BodegaId, ct);

        if (existente is not null && existente.activo)
        {
            throw new InvalidOperationException("El artículo ya tiene una ubicación activa en esa bodega.");
        }

        if (dto.Principal)
        {
            await DesmarcarPrincipalAsync(articuloId, existente?.id, ct);
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var usuario = ClasificacionNormalizer.Usuario(user);

        if (existente is not null)
        {
            existente.activo = true;
            existente.ubicacion1 = ClasificacionNormalizer.Opcional(dto.Ubicacion1, 20);
            existente.ubicacion2 = ClasificacionNormalizer.Opcional(dto.Ubicacion2, 20);
            existente.ubicacion3 = ClasificacionNormalizer.Opcional(dto.Ubicacion3, 20);
            existente.ubicacion4 = ClasificacionNormalizer.Opcional(dto.Ubicacion4, 20);
            existente.ubicacion5 = ClasificacionNormalizer.Opcional(dto.Ubicacion5, 20);
            existente.existencia = dto.Existencia;
            existente.existencia_minima = dto.ExistenciaMinima;
            existente.existencia_maxima = dto.ExistenciaMaxima;
            existente.punto_reorden = dto.PuntoReorden;
            // existencia_comprometida, existencia_transito, costo_promedio y ultimo_costo
            // NO se escriben desde el DTO: los mantiene el motor de posteo (Fase 2). Se
            // conserva lo que ya tiene la fila.
            existente.principal = dto.Principal;
            existente.usuariomodificacion = usuario;
            existente.fechamodificacion = ahora;
            await _context.SaveChangesAsync(ct);
            await RecomputeArticuloAsync(articuloId, ct);
            dto.Id = existente.id;
            dto.Activo = true;
            CopiarCamposDelMotor(existente, dto);
            return dto;
        }

        var entity = new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = dto.BodegaId,
            ubicacion1 = ClasificacionNormalizer.Opcional(dto.Ubicacion1, 20),
            ubicacion2 = ClasificacionNormalizer.Opcional(dto.Ubicacion2, 20),
            ubicacion3 = ClasificacionNormalizer.Opcional(dto.Ubicacion3, 20),
            ubicacion4 = ClasificacionNormalizer.Opcional(dto.Ubicacion4, 20),
            ubicacion5 = ClasificacionNormalizer.Opcional(dto.Ubicacion5, 20),
            existencia = dto.Existencia,
            existencia_minima = dto.ExistenciaMinima,
            existencia_maxima = dto.ExistenciaMaxima,
            punto_reorden = dto.PuntoReorden,
            // Los 4 campos del motor (comprometida, tránsito, costo promedio, último costo)
            // nacen en 0 por DEFAULT y sólo los mueve el motor de posteo: no se toman del DTO.
            principal = dto.Principal,
            activo = true,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        _context.alm_articulo_bodegas.Add(entity);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        dto.Id = entity.id;
        dto.Activo = true;
        CopiarCamposDelMotor(entity, dto);
        return dto;
    }

    public async Task<ArticuloUbicacionDto> UpdateAsync(int articuloId, int id, ArticuloUbicacionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        await ValidarArticuloAsync(articuloId, ct);

        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct)
                     ?? throw new KeyNotFoundException("La ubicación no existe.");

        if (!entity.activo)
        {
            throw new InvalidOperationException("No se puede editar una ubicación deshabilitada. Reactívela primero.");
        }

        await ValidarBodegaAsync(dto.BodegaId, ct);

        if (await _context.alm_articulo_bodegas.AsNoTracking()
                .AnyAsync(u => u.articulo_id == articuloId && u.bodega_id == dto.BodegaId && u.id != id, ct))
        {
            throw new InvalidOperationException("El artículo ya tiene una ubicación en esa bodega.");
        }

        if (dto.Principal)
        {
            await DesmarcarPrincipalAsync(articuloId, id, ct);
        }

        entity.bodega_id = dto.BodegaId;
        entity.ubicacion1 = ClasificacionNormalizer.Opcional(dto.Ubicacion1, 20);
        entity.ubicacion2 = ClasificacionNormalizer.Opcional(dto.Ubicacion2, 20);
        entity.ubicacion3 = ClasificacionNormalizer.Opcional(dto.Ubicacion3, 20);
        entity.ubicacion4 = ClasificacionNormalizer.Opcional(dto.Ubicacion4, 20);
        entity.ubicacion5 = ClasificacionNormalizer.Opcional(dto.Ubicacion5, 20);
        entity.existencia = dto.Existencia;
        entity.existencia_minima = dto.ExistenciaMinima;
        entity.existencia_maxima = dto.ExistenciaMaxima;
        entity.punto_reorden = dto.PuntoReorden;
        // existencia_comprometida, existencia_transito, costo_promedio y ultimo_costo NO se
        // escriben desde el DTO (aunque el cliente los mande): son del motor de posteo.
        entity.principal = dto.Principal;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        dto.Id = entity.id;
        dto.Activo = true;
        CopiarCamposDelMotor(entity, dto);
        return dto;
    }

    /// <summary>
    /// Devuelve al cliente los 4 campos del motor con el valor REAL de la fila, para que la
    /// respuesta no le refleje de vuelta lo que él haya mandado (que se ignora al escribir).
    /// </summary>
    private static void CopiarCamposDelMotor(alm_articulo_bodega entity, ArticuloUbicacionDto dto)
    {
        dto.ExistenciaComprometida = entity.existencia_comprometida;
        dto.ExistenciaTransito = entity.existencia_transito;
        dto.CostoPromedio = entity.costo_promedio;
        dto.UltimoCosto = entity.ultimo_costo;
    }

    public async Task<bool> DeshabilitarAsync(int articuloId, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        if (entity.principal)
        {
            throw new InvalidOperationException("No se puede deshabilitar la bodega principal. Marque otra bodega como principal primero.");
        }

        var otrasActivas = await _context.alm_articulo_bodegas.AsNoTracking()
            .CountAsync(u => u.articulo_id == articuloId && u.activo && u.id != id, ct);
        if (otrasActivas == 0)
        {
            throw new InvalidOperationException("El artículo debe conservar al menos una bodega activa.");
        }

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        return true;
    }

    public async Task<bool> ReactivarAsync(int articuloId, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct);
        if (entity is null) return false;
        if (entity.activo) return true;

        if (!await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.id == entity.bodega_id && b.activo, ct))
        {
            throw new InvalidOperationException("No se puede reactivar: la bodega está inactiva.");
        }

        entity.activo = true;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        return true;
    }

    /// <summary>
    /// Recalcula el rollup de existencia y mínimo del artículo como la suma de sus
    /// filas por bodega ACTIVAS. alm_articulo.existencia deja de ser fuente de verdad.
    /// </summary>
    private async Task RecomputeArticuloAsync(int articuloId, CancellationToken ct)
    {
        var totales = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId && u.activo)
            .GroupBy(u => u.articulo_id)
            .Select(g => new { Existencia = g.Sum(x => x.existencia), Minima = g.Sum(x => x.existencia_minima) })
            .FirstOrDefaultAsync(ct);

        var articulo = await _context.alm_articulos.FirstOrDefaultAsync(a => a.id == articuloId, ct);
        if (articulo is null)
        {
            return;
        }

        articulo.existencia = totales?.Existencia ?? 0m;
        articulo.existencia_minima = totales?.Minima ?? 0m;
        articulo.cantidad = articulo.existencia;
        await _context.SaveChangesAsync(ct);
    }

    private async Task ValidarArticuloAsync(int articuloId, CancellationToken ct)
    {
        if (!await _context.alm_articulos.AsNoTracking().AnyAsync(a => a.id == articuloId, ct))
        {
            throw new KeyNotFoundException("El artículo no existe.");
        }
    }

    private async Task ValidarBodegaAsync(int bodegaId, CancellationToken ct)
    {
        if (!await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.id == bodegaId && b.activo, ct))
        {
            throw new InvalidOperationException("La bodega seleccionada no existe o está inactiva.");
        }
    }

    private async Task DesmarcarPrincipalAsync(int articuloId, int? exceptId, CancellationToken ct)
    {
        var principales = await _context.alm_articulo_bodegas
            .Where(u => u.articulo_id == articuloId && u.principal && (exceptId == null || u.id != exceptId.Value))
            .ToListAsync(ct);

        if (principales.Count == 0)
        {
            return;
        }

        foreach (var p in principales)
        {
            p.principal = false;
        }
        await _context.SaveChangesAsync(ct);
    }
}
