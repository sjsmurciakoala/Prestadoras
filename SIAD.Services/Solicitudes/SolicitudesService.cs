using System.Linq;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Solicitudes;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Solicitudes;

/// <summary>
/// Servicio para gestión completa de solicitudes de servicio.
/// </summary>
public class SolicitudesService : ISolicitudesService
{
    private readonly SiadDbContext _context;
    private readonly IMapper _mapper;

    public SolicitudesService(SiadDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    /// Obtiene listado de solicitudes, opcionalmente filtradas por identidad del cliente.
    /// </summary>
    public async Task<IReadOnlyList<SolicitudListDto>> GetSolicitudesAsync(string? clienteIdentidad = null, CancellationToken ct = default)
    {
        var query = _context.solicitud_servicios
            .Include(s => s.categoria_servicio)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(clienteIdentidad))
        {
            query = query.Where(s => s.cliente_identidad == clienteIdentidad);
        }

        return await query
            .OrderByDescending(s => s.fechacreacion)
            .Select(s => _mapper.Map<SolicitudListDto>(s))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Obtiene el detalle completo de una solicitud por ID.
    /// </summary>
    public async Task<SolicitudDetailDto?> GetSolicitudAsync(int id, CancellationToken ct = default)
    {
        var entity = await _context.solicitud_servicios
            .Include(s => s.categoria_servicio)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.solicitud_servicio_id == id, ct);

        return entity != null ? _mapper.Map<SolicitudDetailDto>(entity) : null;
    }

    /// <summary>
    /// Obtiene listado de categorías de servicio activas.
    /// </summary>
    public async Task<IReadOnlyList<SolicitudCategoriaDto>> GetCategoriasAsync(CancellationToken ct = default)
    {
        return await _context.categoria_servicios
            .AsNoTracking()
            .Where(c => c.estado)
            .OrderBy(c => c.descripcion)
            .Select(c => new SolicitudCategoriaDto
            {
                Id = c.categoria_servicio_id,
                Nombre = c.descripcion,
                Activa = c.estado
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Crea una nueva solicitud de servicio.
    /// </summary>
    public async Task<int> CreateSolicitudAsync(SolicitudCreateDto dto, string usuarioCreacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(usuarioCreacion);

        var entity = _mapper.Map<solicitud_servicio>(dto);
        entity.usuariocreacion = usuarioCreacion;
        entity.fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        _context.solicitud_servicios.Add(entity);
        await _context.SaveChangesAsync(ct);
        
        return entity.solicitud_servicio_id;
    }

    /// <summary>
    /// Actualiza una solicitud de servicio existente.
    /// </summary>
    public async Task UpdateSolicitudAsync(SolicitudUpdateDto dto, string usuarioModificacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(usuarioModificacion);

        var entity = await _context.solicitud_servicios
            .FirstOrDefaultAsync(s => s.solicitud_servicio_id == dto.Id, ct)
            ?? throw new InvalidOperationException($"Solicitud con ID {dto.Id} no encontrada.");

        _mapper.Map(dto, entity);
        entity.usuariomodificacion = usuarioModificacion;
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        _context.solicitud_servicios.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Inactiva una solicitud (cambia estado a false).
    /// </summary>
    public async Task InactivateSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioModificacion);

        var entity = await _context.solicitud_servicios
            .FirstOrDefaultAsync(s => s.solicitud_servicio_id == id, ct)
            ?? throw new InvalidOperationException($"Solicitud con ID {id} no encontrada.");

        entity.estado = false;
        entity.usuariomodificacion = usuarioModificacion;
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        _context.solicitud_servicios.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Marca una solicitud como asignada.
    /// </summary>
    public async Task AsignarSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioModificacion);

        var entity = await _context.solicitud_servicios
            .FirstOrDefaultAsync(s => s.solicitud_servicio_id == id, ct)
            ?? throw new InvalidOperationException($"Solicitud con ID {id} no encontrada.");

        entity.asiginada = true;
        entity.usuariomodificacion = usuarioModificacion;
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        _context.solicitud_servicios.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Desasigna una solicitud (marca como no asignada).
    /// </summary>
    public async Task DesasignarSolicitudAsync(int id, string usuarioModificacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioModificacion);

        var entity = await _context.solicitud_servicios
            .FirstOrDefaultAsync(s => s.solicitud_servicio_id == id, ct)
            ?? throw new InvalidOperationException($"Solicitud con ID {id} no encontrada.");

        entity.asiginada = false;
        entity.usuariomodificacion = usuarioModificacion;
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        _context.solicitud_servicios.Update(entity);
        await _context.SaveChangesAsync(ct);
    }
}
