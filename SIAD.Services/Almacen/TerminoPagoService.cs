using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Mantenimiento del catálogo de términos de pago del proveedor (alm_termino_pago).
/// Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// </summary>
public sealed class TerminoPagoService : ITerminoPagoService
{
    private readonly SiadDbContext _context;

    public TerminoPagoService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TerminoPagoListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ClasificacionFilterDto();
        var query = _context.alm_termino_pagos.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(t => t.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(t => EF.Functions.ILike(t.nombre, like))
                : query.Where(t => t.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderByDescending(t => t.es_default)
            .ThenBy(t => t.dias)
            .ThenBy(t => t.nombre)
            .Select(t => new TerminoPagoListItemDto
            {
                Id = t.id,
                Nombre = t.nombre,
                Dias = t.dias,
                EsDefault = t.es_default,
                Activo = t.activo
            })
            .ToListAsync(ct);
    }

    public async Task<TerminoPagoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_termino_pagos.AsNoTracking()
            .Where(t => t.id == id)
            .Select(t => new TerminoPagoEditDto
            {
                Id = t.id,
                Nombre = t.nombre,
                Dias = t.dias,
                EsDefault = t.es_default,
                Activo = t.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TerminoPagoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        return await _context.alm_termino_pagos.AsNoTracking()
            .Where(t => t.activo)
            .OrderByDescending(t => t.es_default)
            .ThenBy(t => t.dias)
            .ThenBy(t => t.nombre)
            .Select(t => new TerminoPagoLookupDto
            {
                Id = t.id,
                Nombre = t.nombre,
                Dias = t.dias,
                EsDefault = t.es_default
            })
            .ToListAsync(ct);
    }

    public async Task<TerminoPagoEditDto> CreateAsync(TerminoPagoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 60, "nombre");
        var nombreLower = nombre.ToLower();

        if (dto.Dias < 0)
        {
            throw new InvalidOperationException("Los días de crédito no pueden ser negativos.");
        }
        if (await _context.alm_termino_pagos.AsNoTracking().AnyAsync(t => t.nombre.ToLower() == nombreLower, ct))
        {
            throw new InvalidOperationException($"Ya existe un término de pago con el nombre {nombre}.");
        }

        // Solo un predeterminado por empresa: se limpia el anterior antes de insertar el nuevo,
        // para no chocar con el índice único parcial uq_alm_termino_pago_default.
        if (dto.EsDefault)
        {
            await LimpiarDefaultAsync(null, user, ct);
        }

        var entity = new alm_termino_pago
        {
            nombre = nombre,
            dias = dto.Dias,
            es_default = dto.EsDefault,
            activo = dto.Activo,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_termino_pagos.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        return dto;
    }

    public async Task<TerminoPagoEditDto> UpdateAsync(int id, TerminoPagoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_termino_pagos.FirstOrDefaultAsync(t => t.id == id, ct)
                     ?? throw new KeyNotFoundException("El término de pago no existe.");

        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 60, "nombre");
        var nombreLower = nombre.ToLower();

        if (dto.Dias < 0)
        {
            throw new InvalidOperationException("Los días de crédito no pueden ser negativos.");
        }
        if (await _context.alm_termino_pagos.AsNoTracking().AnyAsync(t => t.nombre.ToLower() == nombreLower && t.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un término de pago con el nombre {nombre}.");
        }

        if (dto.EsDefault)
        {
            await LimpiarDefaultAsync(id, user, ct);
        }

        entity.nombre = nombre;
        entity.dias = dto.Dias;
        entity.es_default = dto.EsDefault;
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
        var entity = await _context.alm_termino_pagos.FirstOrDefaultAsync(t => t.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.es_default = false;   // un término inactivo no puede ser el predeterminado
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Quita la marca de predeterminado a los demás términos de la empresa (solo uno puede serlo).</summary>
    private async Task LimpiarDefaultAsync(int? exceptoId, string user, CancellationToken ct)
    {
        var actuales = await _context.alm_termino_pagos
            .Where(t => t.es_default && (exceptoId == null || t.id != exceptoId.Value))
            .ToListAsync(ct);
        if (actuales.Count == 0) return;

        foreach (var t in actuales)
        {
            t.es_default = false;
            t.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
            t.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        }
        await _context.SaveChangesAsync(ct);
    }
}
