using System.Linq;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Solicitudes;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Solicitudes;

public class SolicitudesService : ISolicitudesService
{
    private readonly SiadDbContext _context;

    public SolicitudesService(SiadDbContext context) => _context = context;

    public async Task<IReadOnlyList<SolicitudListDto>> GetSolicitudesAsync(string? clienteIdentidad, CancellationToken ct = default)
    {
        var query = _context.solicitud_servicios
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(clienteIdentidad))
        {
            query = query.Where(s => s.cliente_identidad == clienteIdentidad);
        }

        return await query
            .OrderByDescending(s => s.fechacreacion)
            .Select(s => new SolicitudListDto(
                s.solicitud_servicio_id,
                s.cliente_identidad,
                s.cliente_nombre,
                s.categoria_servicio_id,
                s.categoria_servicio.descripcion,
                s.fechacreacion ?? DateTime.MinValue,
                s.estado))
            .ToListAsync(ct);
    }

    public async Task<SolicitudDetailDto?> GetSolicitudAsync(int id, CancellationToken ct = default)
    {
        return await _context.solicitud_servicios
            .AsNoTracking()
            .Where(s => s.solicitud_servicio_id == id)
            .Select(s => new SolicitudDetailDto(
                s.solicitud_servicio_id,
                s.cliente_identidad,
                s.cliente_nombre,
                s.categoria_servicio_id,
                s.cliente_telefono ?? s.cliente_movil,
                s.cliente_direccion,
                s.cliente_email,
                s.observacion,
                s.fechacreacion ?? DateTime.MinValue,
                s.estado,
                s.categoria_servicio.descripcion,
                s.empresa_nombre,
                s.empresa_telefono,
                s.empresa_direccion,
                s.negocio_nombre,
                s.negocio_telefono,
                s.negocio_clave_catastral))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SolicitudCategoriaDto>> GetCategoriasAsync(CancellationToken ct = default)
    {
        return await _context.categoria_servicios
            .AsNoTracking()
            .Where(c => c.estado)
            .OrderBy(c => c.descripcion)
            .Select(c => new SolicitudCategoriaDto(
                c.categoria_servicio_id,
                c.descripcion,
                c.estado))
            .ToListAsync(ct);
    }

    public async Task<int> CreateSolicitudAsync(SolicitudDetailDto dto, CancellationToken ct = default)
    {
        var entity = new solicitud_servicio
        {
            cliente_identidad = dto.IdentificacionCliente,
            cliente_nombre = dto.NombreCliente,
            categoria_servicio_id = dto.CategoriaServicioId,
            cliente_telefono = dto.Telefono,
            cliente_movil = dto.Telefono,
            cliente_email = dto.Correo,
            cliente_direccion = dto.Direccion,
            observacion = dto.Observacion,
            empresa_nombre = dto.EmpresaNombre,
            empresa_telefono = dto.EmpresaTelefono,
            empresa_direccion = dto.EmpresaDireccion,
            negocio_nombre = dto.NegocioNombre,
            negocio_telefono = dto.NegocioTelefono,
            negocio_clave_catastral = dto.NegocioClaveCatastral,
            estado = dto.Estado,
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            usuariocreacion = "api"
        };

        _context.solicitud_servicios.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.solicitud_servicio_id;
    }
}
