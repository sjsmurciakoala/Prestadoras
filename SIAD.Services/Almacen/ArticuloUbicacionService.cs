using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Ubicaciones físicas de un artículo por bodega (bodega + estante + principal).
/// Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// </summary>
public sealed class ArticuloUbicacionService : IArticuloUbicacionService
{
    private readonly SiadDbContext _context;

    public ArticuloUbicacionService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ArticuloUbicacionDto>> GetAsync(int articuloId, CancellationToken ct = default)
    {
        if (articuloId <= 0)
        {
            return Array.Empty<ArticuloUbicacionDto>();
        }

        return await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId)
            .OrderByDescending(u => u.principal)
            .ThenBy(u => u.bodega != null ? u.bodega.codigo : string.Empty)
            .Select(u => new ArticuloUbicacionDto
            {
                Id = u.id,
                BodegaId = u.bodega_id,
                BodegaDisplay = u.bodega != null ? u.bodega.codigo + " — " + u.bodega.nombre : null,
                EstanteriaId = u.estante != null ? u.estante.estanteria_id : (int?)null,
                EstanteId = u.estante_id,
                EstanteUbicacion = u.estante != null && u.estante.estanteria != null && u.estante.estanteria.bodega != null
                    ? u.estante.estanteria.bodega.codigo + "-" + u.estante.estanteria.codigo + "-" + u.estante.codigo
                    : null,
                Existencia = u.existencia,
                ExistenciaMinima = u.existencia_minima,
                Principal = u.principal
            })
            .ToListAsync(ct);
    }

    public async Task<ArticuloUbicacionDto> AddAsync(int articuloId, ArticuloUbicacionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarArticuloAsync(articuloId, ct);
        await ValidarBodegaAsync(dto.BodegaId, ct);
        await ValidarEstanteEnBodegaAsync(dto.EstanteId, dto.BodegaId, ct);

        if (await _context.alm_articulo_bodegas.AsNoTracking()
                .AnyAsync(u => u.articulo_id == articuloId && u.bodega_id == dto.BodegaId, ct))
        {
            throw new InvalidOperationException("El artículo ya tiene una ubicación en esa bodega.");
        }

        if (dto.Principal)
        {
            await DesmarcarPrincipalAsync(articuloId, null, ct);
        }

        var entity = new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = dto.BodegaId,
            estante_id = dto.EstanteId,
            existencia = dto.Existencia,
            existencia_minima = dto.ExistenciaMinima,
            principal = dto.Principal,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_articulo_bodegas.Add(entity);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<ArticuloUbicacionDto> UpdateAsync(int articuloId, int id, ArticuloUbicacionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        await ValidarArticuloAsync(articuloId, ct);

        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct)
                     ?? throw new KeyNotFoundException("La ubicación no existe.");

        await ValidarBodegaAsync(dto.BodegaId, ct);
        await ValidarEstanteEnBodegaAsync(dto.EstanteId, dto.BodegaId, ct);

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
        entity.estante_id = dto.EstanteId;
        entity.existencia = dto.Existencia;
        entity.existencia_minima = dto.ExistenciaMinima;
        entity.principal = dto.Principal;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<bool> DeleteAsync(int articuloId, int id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct);
        if (entity is null) return false;

        _context.alm_articulo_bodegas.Remove(entity);
        await _context.SaveChangesAsync(ct);
        await RecomputeArticuloAsync(articuloId, ct);
        return true;
    }

    /// <summary>
    /// Recalcula el rollup de existencia y mínimo del artículo como la suma de sus
    /// filas por bodega. alm_articulo.existencia deja de ser fuente de verdad.
    /// </summary>
    private async Task RecomputeArticuloAsync(int articuloId, CancellationToken ct)
    {
        var totales = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId)
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

    private async Task ValidarEstanteEnBodegaAsync(int? estanteId, int bodegaId, CancellationToken ct)
    {
        if (!estanteId.HasValue)
        {
            return;
        }

        var est = await _context.alm_estantes.AsNoTracking()
            .Where(e => e.id == estanteId.Value)
            .Select(e => new { BodegaId = e.estanteria != null ? e.estanteria.bodega_id : 0 })
            .FirstOrDefaultAsync(ct);

        if (est is null)
        {
            throw new InvalidOperationException("El estante seleccionado no existe.");
        }

        if (est.BodegaId != bodegaId)
        {
            throw new InvalidOperationException("El estante seleccionado no pertenece a la bodega indicada.");
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
