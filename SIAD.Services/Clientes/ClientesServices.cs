using System;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Clientes;
using SIAD.Data;

namespace SIAD.Services.Clientes;

public class ClientesService : IClientesService
{
    private readonly SiadDbContext _context;

    public ClientesService(SiadDbContext context)
    {
        _context = context;
    }
    public async Task<IReadOnlyList<ClienteListItemDto>> SearchClientesAsync(ClienteFilterDto filtro, CancellationToken cancellationToken = default)
    {
        var clientes = _context.cliente_maestros.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            var patron = $"%{filtro.Codigo.Trim()}%";
            clientes = clientes.Where(c => EF.Functions.ILike(c.maestro_cliente_clave, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Nombre))
        {
            var patron = $"%{filtro.Nombre.Trim()}%";
            clientes = clientes.Where(c => EF.Functions.ILike(c.maestro_cliente_nombre, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Barrio))
        {
            var patron = $"%{filtro.Barrio.Trim()}%";
            clientes = clientes.Where(c => c.barrio_codigo != null && EF.Functions.ILike(c.barrio_codigo, patron));
        }

        if (filtro.SoloActivos)
        {
            clientes = clientes.Where(c => c.estado);
        }

        return await clientes
            .Select(c => new ClienteListItemDto(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.barrio_codigo,
                c.estado))
            .ToListAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<ClienteListItemDto>> GetClientesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.cliente_maestros
            .AsNoTracking()
            .Select(c => new ClienteListItemDto(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.barrio_codigo,
                c.estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClienteDetailDto?> GetClienteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == id)
            .Select(c => new ClienteDetailDto(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.maestro_cliente_identidad,
                c.maestro_cliente_rtn,
                c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault(),
                c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_telefono)
                    .FirstOrDefault(),
                c.barrio_codigo,
                c.ciclos != null ? c.ciclos.ciclos_descripcioncorta : null,
                c.estado))
            .FirstOrDefaultAsync(cancellationToken);
    }
    // Implementación del nuevo método GetTarifasAsync
    public async Task<IReadOnlyList<ClienteTarifaDto>> GetTarifasAsync(int clienteId, CancellationToken cancellationToken = default)
    {
        return await _context.configuracion_tasas
            .AsNoTracking()
            .Where(t => t.maestro_cliente_id == clienteId)
            .Include(t => t.tarifa_catalogo)
            .Include(t => t.configuracion_tasas_detalles)
            .OrderByDescending(t => t.fechamodificacion ?? t.fechacreacion)
            .Select(t => new ClienteTarifaDto(
                t.configuracion_tasas_id,
                t.tarifa_catalogo_id,
                t.tarifa_catalogo != null ? t.tarifa_catalogo.nombre : null,
                t.tarifa_catalogo != null ? t.tarifa_catalogo.precio_base : null,
                t.tarifa_catalogo != null ? t.tarifa_catalogo.cargo_fijo : null,
                t.configuracion_tasas_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => (decimal?)d.configuracion_tasas_detalle_monto)
                    .FirstOrDefault(),
                t.fechacreacion,
                t.fechamodificacion,
                t.estado))
            .ToListAsync(cancellationToken);
    }
    public async Task<ClienteEstadoCuentaDto> GetEstadoCuentaAsync(int clienteId, CancellationToken ct = default)
    {
        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return new ClienteEstadoCuentaDto(null, null, null, null, null);
        }

        var movimientosQuery = _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clave);

        var ultimoMovimiento = await movimientosQuery
            .OrderByDescending(t => t.fecha_docu)
            .Select(t => new { t.fecha_docu, t.saldo })
            .FirstOrDefaultAsync(ct);

        var ultimoPago = await movimientosQuery
            .Where(t => t.tipotransaccion != null && EF.Functions.ILike(t.tipotransaccion, "%PAGO%"))
            .OrderByDescending(t => t.fecha_docu)
            .Select(t => new { t.fecha_docu, t.creditos, t.debitos, t.saldo })
            .FirstOrDefaultAsync(ct);

        var saldoActual = ultimoMovimiento?.saldo ?? ultimoPago?.saldo ?? 0m;
        DateTime? fechaPago = ultimoPago?.fecha_docu.HasValue == true 
            ? ultimoPago.fecha_docu.Value.ToDateTime(TimeOnly.MinValue) 
            : null;
        decimal? montoPago = ultimoPago?.creditos ?? ultimoPago?.debitos ?? 0m;

        var consumos = await _context.historicomedicions
            .AsNoTracking()
            .Where(h => h.clave == clave && h.consumo.HasValue)
            .OrderByDescending(h => h.fecha)
            .Select(h => h.consumo!.Value)
            .Take(6)
            .ToArrayAsync(ct);

        decimal? consumoPromedio = consumos.Length > 0 ? consumos.Average() : 0m;

        return new ClienteEstadoCuentaDto(
            saldoActual,
            fechaPago,
            montoPago,
            consumoPromedio,
            null);
    }

    public async Task<IReadOnlyList<ClienteMovimientoDto>> GetMovimientosAsync(int clienteId, CancellationToken ct = default)
    {
        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return Array.Empty<ClienteMovimientoDto>();
        }

        return await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clave)
            .OrderByDescending(t => t.fecha_docu)
            .Select(t => new ClienteMovimientoDto(
                t.ide,
                t.fecha_docu.HasValue ? t.fecha_docu.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                t.tipotransaccion ?? string.Empty,
                t.descripcion,
                (t.creditos ?? 0) - (t.debitos ?? 0),
                t.saldo ?? 0))
            .ToListAsync(ct);
    }



}
