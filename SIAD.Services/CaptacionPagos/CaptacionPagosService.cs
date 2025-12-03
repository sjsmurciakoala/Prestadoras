using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.CaptacionPagos;

public class CaptacionPagosService : ICaptacionPagosService
{
    private readonly SiadDbContext _context;

    public CaptacionPagosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CajaDto>> ListarCatalogoCajasAsync(CancellationToken ct = default)
    {
        return await _context.catalogo_cajas
            .AsNoTracking()
            .OrderBy(c => c.nombre)
            .Select(c => new CajaDto
            {
                Id = c.id,
                Nombre = c.nombre ?? $"Caja {c.id}",
                Estado = c.estado ?? string.Empty,
                FechaApertura = c.fecha_apertura,
                Usuario = c.usuario
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ArqueoDto>> ListarArqueosAsync(CaptacionArqueoFilterDto? filtro, CancellationToken ct = default)
    {
        var query = _context.pagos_hdrs
            .AsNoTracking()
            .Include(p => p.caja)
            .Where(p => p.fecha != null);

        if (filtro?.CajaId is int cajaId && cajaId > 0)
        {
            query = query.Where(p => p.caja_id == cajaId);
        }

        if (filtro?.FechaInicio is DateTime inicio)
        {
            var inicioUtc = NormalizeDateStartUtc(inicio);
            query = query.Where(p => p.fecha >= inicioUtc);
        }

        if (filtro?.FechaFin is DateTime fin)
        {
            var finUtc = NormalizeDateStartUtc(fin).AddDays(1);
            query = query.Where(p => p.fecha < finUtc);
        }

        return await query
            .GroupBy(p => new
            {
                p.caja_id,
                Fecha = p.fecha!.Value.Date,
                CajaNombre = p.caja != null ? p.caja.nombre : null
            })
            .OrderByDescending(g => g.Key.Fecha)
            .ThenBy(g => g.Key.CajaNombre)
            .Select(g => new ArqueoDto
            {
                CajaId = g.Key.caja_id ?? 0,
                CajaNombre = g.Key.CajaNombre ?? (g.Key.caja_id.HasValue ? $"Caja {g.Key.caja_id}" : "Sin caja"),
                Fecha = g.Key.Fecha,
                TotalPagos = g.Sum(p => p.total),
                ConteoPagos = g.Count()
            })
            .ToListAsync(ct);
    }

    public async Task<CaptacionHeaderDto?> ObtenerDetallePagoAsync(string numFactura, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(numFactura))
        {
            return null;
        }

        numFactura = numFactura.Trim();

        return await _context.pagos_hdrs
            .AsNoTracking()
            .Include(p => p.caja)
            .Where(p => p.numfactura == numFactura)
            .Select(p => new CaptacionHeaderDto
            {
                NumFactura = p.numfactura,
                ClienteClave = p.clienteclave ?? string.Empty,
                Fecha = p.fecha ?? DateTime.MinValue,
                Total = p.total,
                Estado = p.estado ?? string.Empty,
                CajaId = p.caja_id,
                CajaNombre = p.caja != null ? p.caja.nombre : null,
                Banco = p.banco,
                Usuario = p.usuario
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CaptacionPagoResponseDto?> ObtenerPagoAsync(string numFactura, CancellationToken ct = default)
    {
        var header = await ObtenerDetallePagoAsync(numFactura, ct);
        if (header is null)
        {
            return null;
        }

        var detalles = await ObtenerDetallePagoLineasAsync(numFactura, ct);
        return new CaptacionPagoResponseDto
        {
            Header = header,
            Detalles = detalles
        };
    }

    public async Task<IReadOnlyList<CaptacionDetailDto>> ObtenerDetallePagoLineasAsync(string numFactura, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(numFactura))
        {
            return Array.Empty<CaptacionDetailDto>();
        }

        numFactura = numFactura.Trim();

        return await _context.pagos_dtls
            .AsNoTracking()
            .Where(d => d.numfactura == numFactura)
            .OrderBy(d => d.linea)
            .Select(d => new CaptacionDetailDto
            {
                Linea = d.linea,
                Servicio = d.servicio ?? string.Empty,
                MontoValor = d.montovalor ?? d.monto ?? 0
            })
            .ToListAsync(ct);
    }

    public async Task<ResponseModelDto> RegistrarPagoAsync(PagoCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.NumFactura))
        {
            return ResponseModelDto.Fail("La factura es requerida.");
        }

        dto.NumFactura = dto.NumFactura.Trim();
        dto.ClienteClave = dto.ClienteClave.Trim();

        var existe = await _context.pagos_hdrs.AnyAsync(p => p.numfactura == dto.NumFactura, ct);
        if (existe)
        {
            return ResponseModelDto.Fail($"Ya existe un pago registrado con el número {dto.NumFactura}.");
        }

        var cajaExiste = await _context.catalogo_cajas.AnyAsync(c => c.id == dto.CajaId, ct);
        if (!cajaExiste)
        {
            return ResponseModelDto.Fail($"La caja {dto.CajaId} no existe.");
        }

        var detallesValidos = (dto.Detalles ?? new List<PagoCrearDetalleDto>())
            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Servicio))
            .Select(d => new PagoCrearDetalleDto { Servicio = d.Servicio.Trim(), MontoValor = d.MontoValor })
            .Where(d => d.MontoValor > 0)
            .ToList();

        if (detallesValidos.Count == 0)
        {
            detallesValidos.Add(new PagoCrearDetalleDto
            {
                Servicio = "Pago manual",
                MontoValor = dto.Monto > 0 ? dto.Monto : 0
            });
        }

        var totalDetalles = detallesValidos.Sum(d => d.MontoValor);
        if (totalDetalles <= 0)
        {
            return ResponseModelDto.Fail("El monto total debe ser mayor a cero.");
        }

        dto.Monto = totalDetalles;

        var entity = new pagos_hdr
        {
            numfactura = dto.NumFactura,
            clienteclave = dto.ClienteClave,
            fecha = DateTime.UtcNow,
            total = dto.Monto,
            estado = "C",
            caja_id = dto.CajaId,
            banco = dto.Banco,
            usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim()
        };

        foreach (var (detalle, indice) in detallesValidos.Select((detalle, indice) => (detalle, indice)))
        {
            entity.detalles.Add(new pagos_dtl
            {
                numfactura = entity.numfactura,
                linea = indice + 1,
                servicio = detalle.Servicio,
                montovalor = detalle.MontoValor,
                monto = detalle.MontoValor
            });
        }

        _context.pagos_hdrs.Add(entity);
        await _context.SaveChangesAsync(ct);

        return ResponseModelDto.Ok(new { entity.numfactura }, "Pago registrado correctamente.");
    }

    public async Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.NumFactura))
        {
            return ResponseModelDto.Fail("El número de factura es requerido.");
        }

        var numFactura = dto.NumFactura.Trim();
        var pago = await _context.pagos_hdrs.FirstOrDefaultAsync(p => p.numfactura == numFactura, ct);

        if (pago is null)
        {
            return ResponseModelDto.Fail($"No se encontró el pago {numFactura}.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ClienteClave) &&
            !string.Equals(pago.clienteclave, dto.ClienteClave.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("La clave del cliente no coincide con el pago seleccionado.");
        }

        if (string.Equals(pago.estado, "R", StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("El pago ya fue reversado anteriormente.");
        }

        pago.estado = "R";
        if (!string.IsNullOrWhiteSpace(dto.Usuario))
        {
            pago.usuario = dto.Usuario.Trim();
        }

        await _context.SaveChangesAsync(ct);

        return ResponseModelDto.Ok(new { pago.numfactura }, "Pago reversado correctamente.");
    }

    public async Task<IReadOnlyList<ReciboMiscelaneoDto>> ListarPagosMiscelaneosAsync(string? clienteClave, CancellationToken ct = default)
    {
        var query = _context.pagos_miscelaneos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(clienteClave))
        {
            var clave = $"%{clienteClave.Trim()}%";
            query = query.Where(p => p.cliente != null && EF.Functions.ILike(p.cliente, clave));
        }

        return await query
            .OrderByDescending(p => p.fecha)
            .Take(200)
            .Select(p => new ReciboMiscelaneoDto
            {
                Recibo = p.recibo,
                Cliente = p.cliente ?? string.Empty,
                Fecha = p.fecha ?? DateTime.MinValue,
                Total = p.total,
                Estado = p.estado ?? string.Empty
            })
            .ToListAsync(ct);
    }

    private static DateTime NormalizeDateStartUtc(DateTime value)
    {
        DateTime normalized = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTime(normalized.Year, normalized.Month, normalized.Day, 0, 0, 0, DateTimeKind.Utc);
    }
}
