using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Medidores;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Medidores;

public class MedidoresService : IMedidoresService
{
    private readonly SiadDbContext _context;

    public MedidoresService(SiadDbContext context) => _context = context;

    public async Task<IReadOnlyList<MedidorListDto>> SearchAsync(MedidorFilterDto filtro, CancellationToken ct = default)
    {
        var query = _context.maestro_medidors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
            query = query.Where(m => EF.Functions.ILike(m.maestro_medidor_numero, $"%{filtro.Numero}%"));

        if (!string.IsNullOrWhiteSpace(filtro.Marca))
            query = query.Where(m => EF.Functions.ILike(m.maestro_medidor_marca ?? string.Empty, $"%{filtro.Marca}%"));

        if (filtro.Estado.HasValue)
            query = query.Where(m => m.estado == filtro.Estado.Value);

        if (filtro.Asignado.HasValue)
        {
            query = filtro.Asignado.Value
                ? query.Where(m => m.cliente_detalles.Any())
                : query.Where(m => !m.cliente_detalles.Any());
        }

        if (!string.IsNullOrWhiteSpace(filtro.ClienteClave))
        {
            query = query.Where(m => m.cliente_detalles.Any(cd =>
                cd.maestro_cliente != null &&
                EF.Functions.ILike(cd.maestro_cliente.maestro_cliente_clave ?? string.Empty, $"%{filtro.ClienteClave}%")));
        }

        return await query
            .Select(m => new
            {
                Medidor = m,
                Cliente = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente)
                    .FirstOrDefault()
            })
            .OrderBy(x => x.Medidor.maestro_medidor_numero)
            .Select(x => new MedidorListDto(
                x.Medidor.maestro_medidor_id,
                x.Medidor.maestro_medidor_numero,
                x.Medidor.maestro_medidor_marca,
                x.Medidor.maestro_medidor_diametro,
                x.Medidor.maestro_medidor_fecha_instala,
                x.Medidor.maestro_medidor_acueducto,
                x.Medidor.estado,
                x.Cliente != null ? x.Cliente.maestro_cliente_clave : null,
                x.Cliente != null ? x.Cliente.maestro_cliente_nombre : null))
            .ToListAsync(ct);
    }

    public async Task<MedidorDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var medidor = await _context.maestro_medidors
            .AsNoTracking()
            .Where(m => m.maestro_medidor_id == id)
            .Select(m => new
            {
                Medidor = m,
                Cliente = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (medidor is null)
            return null;

        var cliente = medidor.Cliente;
        var historico = await GetHistorialAsync(id, 12, ct);

        var configuraciones = await _context.configuracion_app_lectura_medidores
            .AsNoTracking()
            .OrderBy(c => c.ide)
            .Select(c => c.descripcion ?? c.valor_letras ?? $"Config {c.ide}")
            .ToListAsync(ct);

        return new MedidorDetailDto(
            medidor.Medidor.maestro_medidor_id,
            medidor.Medidor.maestro_medidor_numero,
            medidor.Medidor.maestro_medidor_marca,
            medidor.Medidor.maestro_medidor_diametro,
            medidor.Medidor.maestro_medidor_fecha_instala,
            medidor.Medidor.maestro_medidor_acueducto,
            medidor.Medidor.estado,
            cliente?.maestro_cliente_clave,
            cliente?.maestro_cliente_nombre,
            cliente?.barrio_codigo,
            historico,
            configuraciones);
    }

    public async Task<IReadOnlyList<MedidorHistorialDto>> GetHistorialAsync(int medidorId, int take = 12, CancellationToken ct = default)
    {
        var medidor = await _context.maestro_medidors
            .AsNoTracking()
            .Where(m => m.maestro_medidor_id == medidorId)
            .Select(m => new
            {
                Numero = m.maestro_medidor_numero,
                Claves = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente != null ? cd.maestro_cliente.maestro_cliente_clave : null)
            })
            .FirstOrDefaultAsync(ct);

        if (medidor is null)
            return Array.Empty<MedidorHistorialDto>();

        var claves = medidor.Claves
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToArray();

        var query = _context.historicomedicions.AsNoTracking()
            .Where(h => h.contador == medidor.Numero ||
                        (claves.Length > 0 && h.clave != null && claves.Contains(h.clave)));

        return await query
            .OrderByDescending(h => h.fecha)
            .ThenByDescending(h => h.ide)
            .Take(take)
            .Select(h => new MedidorHistorialDto(
                h.fecha,
                h.lect_act,
                h.lect_ant,
                h.consumo,
                h.condicion,
                h.observacion,
                h.usuario))
            .ToListAsync(ct);
    }

    public async Task<bool> AssignToClienteAsync(int medidorId, int clienteId, CancellationToken ct = default)
    {
        var medidor = await _context.maestro_medidors
            .Include(m => m.cliente_detalles)
            .FirstOrDefaultAsync(m => m.maestro_medidor_id == medidorId, ct);

        if (medidor is null)
            return false;

        var cliente = await _context.cliente_maestros
            .Include(c => c.cliente_detalles)
            .FirstOrDefaultAsync(c => c.maestro_cliente_id == clienteId, ct);

        if (cliente is null)
            return false;

        var detalle = cliente.cliente_detalles
            .OrderBy(cd => cd.detalle_cliente_id)
            .FirstOrDefault();

        if (detalle is null)
        {
            detalle = new cliente_detalle
            {
                maestro_cliente_id = clienteId,
                detalle_cliente_direccion = cliente.cliente_detalles.FirstOrDefault()?.detalle_cliente_direccion,
                detalle_cliente_telefono = cliente.cliente_detalles.FirstOrDefault()?.detalle_cliente_telefono,
                estado = true
            };
            cliente.cliente_detalles.Add(detalle);
        }

        detalle.maestro_medidor_id = medidorId;
        cliente.maestro_cliente_tiene_medidor = true;
        medidor.estado = true;
        medidor.usuariomodificacion = "api";
        medidor.fechamodificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task RegistrarLecturaSinMedidorAsync(string clave, DateTime fecha, decimal lectura, string usuario, CancellationToken ct = default)
    {
        var historico = new historicosinmedidor
        {
            cuenta = clave,
            fecha = fecha,
            usuario = usuario,
            ano = fecha.Year,
            mes = fecha.Month,
            numerofactura = null,
            correlativocai = null,
            idcai = null
        };

        await _context.historicosinmedidors.AddAsync(historico, ct);
        await _context.SaveChangesAsync(ct);
    }
}
