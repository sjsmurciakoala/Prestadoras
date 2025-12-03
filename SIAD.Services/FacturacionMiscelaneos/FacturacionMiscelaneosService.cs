using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.FacturacionMiscelaneos;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.FacturacionMiscelaneos;

public class FacturacionMiscelaneosService : IFacturacionMiscelaneosService
{
    private readonly SiadDbContext _context;

    public FacturacionMiscelaneosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ClienteLookupDto>();
        }

        var filtro = $"%{query.Trim()}%";

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, filtro) ||
                EF.Functions.ILike(c.maestro_cliente_nombre, filtro))
            .OrderBy(c => c.maestro_cliente_clave)
            .Take(20)
            .Select(c => new ClienteLookupDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault(),
                Rtn = c.maestro_cliente_rtn
            })
            .ToListAsync(ct);
    }

    public async Task<ClienteMiscelaneoDto?> ObtenerClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return null;
        }

        var clave = clienteClave.Trim();

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == clave)
            .Select(c => new ClienteMiscelaneoDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Rtn = c.maestro_cliente_rtn,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MiscelaneoCatalogoDto>> ListarCatalogoAsync(CancellationToken ct = default)
    {
        return await _context.miscelaneos_catalogos
            .AsNoTracking()
            .OrderBy(c => c.nombre)
            .Select(c => new MiscelaneoCatalogoDto
            {
                Id = c.ide,
                Codigo = c.codigo ?? string.Empty,
                Nombre = c.nombre ?? string.Empty,
                ValorUnitario = c.valor ?? 0m
            })
            .ToListAsync(ct);
    }

    public async Task<ResponseModelDto> CrearReciboAsync(FacturaMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es requerida.");
        }

        var detallesValidos = dto.Detalles?
            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Codigo))
            .Select(d => new MiscelaneoDetalleDto
            {
                Codigo = d.Codigo.Trim(),
                Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? d.Codigo.Trim() : d.Nombre.Trim(),
                Unidad = d.Unidad <= 0 ? 1 : d.Unidad,
                ValorUnitario = d.ValorUnitario,
                ValorTotal = d.ValorTotal > 0 ? d.ValorTotal : d.Unidad * d.ValorUnitario
            })
            .Where(d => d.ValorTotal > 0)
            .ToList() ?? new List<MiscelaneoDetalleDto>();

        if (detallesValidos.Count == 0)
        {
            return ResponseModelDto.Fail("Debe agregar al menos un concepto misceláneo con valores mayores a cero.");
        }

        var cliente = await ObtenerClienteAsync(dto.ClienteClave, ct);
        if (cliente is null)
        {
            return ResponseModelDto.Fail("El cliente indicado no existe.");
        }

        var periodo = !string.IsNullOrWhiteSpace(dto.Periodo)
            ? dto.Periodo.Trim()
            : await ObtenerPeriodoActualAsync(ct);

        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var total = detallesValidos.Sum(d => d.ValorTotal);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var factura = new factura
        {
            numfactura = string.Empty,
            clientecodigo = cliente.Clave,
            tipofactura = "R",
            ano = hoy.Year.ToString(),
            mes = hoy.Month.ToString(),
            fechaemision = hoy,
            fechavence = hoy,
            rtn = dto.Rtn ?? cliente.Rtn,
            periodo = periodo,
            numdei = string.Empty,
            saldototal = total,
            usuario = usuario,
            identidad = null,
            estado = "A"
        };

        _context.facturas.Add(factura);
        await _context.SaveChangesAsync(ct);

        var facturaDetalles = detallesValidos.Select(d => new factura_detalle
        {
            factura_id = factura.id,
            numrecibo = factura.numrecibo,
            codigo = d.Codigo,
            descripcion = d.Nombre,
            tiposervicio = "MISC",
            montovalor = d.ValorTotal,
            montovalor_saldo = d.ValorTotal
        }).ToList();

        _context.factura_detalles.AddRange(facturaDetalles);

        var saldoActual = await ObtenerSaldoClienteAsync(cliente.Clave, ct);
        var transacciones = new List<transaccion_abonado>();

        foreach (var detalle in detallesValidos)
        {
            saldoActual += detalle.ValorTotal;
            transacciones.Add(new transaccion_abonado
            {
                cliente_clave = cliente.Clave,
                recibo = factura.numrecibo,
                tipotransaccion = detalle.Codigo,
                docufuente = factura.id,
                docufuente2 = factura.numfactura,
                fecha_docu = hoy,
                tipo_partida = "01",
                descripcion = detalle.Nombre,
                debitos = detalle.ValorTotal,
                creditos = 0,
                saldo = saldoActual,
                tipo_servicio = "E",
                periodo = periodo,
                estado = "A",
                fecha_registro = hoy,
                usuario = usuario,
                saldo_detalle = detalle.ValorTotal
            });
        }

        if (transacciones.Count > 0)
        {
            _context.transaccion_abonados.AddRange(transacciones);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ResponseModelDto.Ok(
            new
            {
                factura.id,
                factura.numrecibo,
                factura.clientecodigo,
                Total = total
            },
            "Recibo misceláneo generado correctamente.");
    }

    public async Task<FacturaMiscelaneoResponseDto?> ObtenerReciboAsync(int numeroRecibo, CancellationToken ct = default)
    {
        var recibo = await _context.facturas
            .AsNoTracking()
            .Where(f => f.numrecibo == numeroRecibo)
            .Select(f => new FacturaMiscelaneoResponseDto
            {
                FacturaId = f.id,
                NumeroRecibo = f.numrecibo,
                NumFactura = f.numfactura ?? string.Empty,
                ClienteClave = f.clientecodigo ?? string.Empty,
                FechaEmision = f.fechaemision.HasValue
                    ? f.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                    : DateTime.MinValue,
                Total = f.saldototal ?? 0m
            })
            .FirstOrDefaultAsync(ct);

        if (recibo is null)
        {
            return null;
        }

        var detalles = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == recibo.FacturaId)
            .OrderBy(d => d.id)
            .Select(d => new MiscelaneoDetalleDto
            {
                Codigo = d.codigo ?? string.Empty,
                Nombre = d.descripcion ?? string.Empty,
                Unidad = 1,
                ValorUnitario = d.montovalor ?? 0m,
                ValorTotal = d.montovalor ?? 0m
            })
            .ToListAsync(ct);

        recibo.Detalles = detalles;

        return recibo;
    }

    private async Task<string> ObtenerPeriodoActualAsync(CancellationToken ct)
    {
        var periodo = await _context.historialmes
            .AsNoTracking()
            .Where(p => p.cerrarperiodo == 'P')
            .OrderByDescending(p => p.ano)
            .ThenByDescending(p => p.mes)
            .Select(p => new { p.ano, p.mes })
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
        {
            return DateTime.UtcNow.ToString("yyyyMM");
        }

        var ano = Convert.ToInt32(periodo.ano);
        var mes = Convert.ToInt32(periodo.mes);
        return $"{ano:D4}{mes:D2}";
    }

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        var saldo = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clienteClave)
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Select(t => t.saldo ?? 0m)
            .FirstOrDefaultAsync(ct);

        return saldo;
    }
}
