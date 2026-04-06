using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.NotasCreditoDebito;

public class NotasCreditoDebitoService : INotasCreditoDebitoService
{
    private const string NotaDebitoPrefix = "N/D";
    private const string NotaCreditoPrefix = "N/C";
    private readonly SiadDbContext _context;

    public NotasCreditoDebitoService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NotaClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<NotaClienteLookupDto>();
        }

        var filtro = $"%{query.Trim()}%";

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, filtro) ||
                EF.Functions.ILike(c.maestro_cliente_nombre, filtro) ||
                (c.maestro_cliente_rtn != null && EF.Functions.ILike(c.maestro_cliente_rtn, filtro)))
            .OrderBy(c => c.maestro_cliente_clave)
            .Take(25)
            .Select(ClienteProjection)
            .ToListAsync(ct);
    }

    public async Task<NotaClienteLookupDto?> ObtenerClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return null;
        }

        var clave = clienteClave.Trim();

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == clave)
            .Select(ClienteProjection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<NotaClienteConfiguracionDto?> ObtenerConfiguracionClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        var cliente = await ObtenerClienteAsync(clienteClave, ct);
        if (cliente is null)
        {
            return null;
        }

        var tarifas = await _context.configuracion_tasas
            .AsNoTracking()
            .Where(t => t.maestro_cliente.maestro_cliente_clave == cliente.Clave)
            .OrderByDescending(t => t.fechamodificacion ?? t.fechacreacion)
            .SelectMany(t => t.configuracion_tasas_detalles
                .Join(_context.servicios,
                    detalle => detalle.servicios_id,
                    servicio => servicio.servicios_id,
                    (detalle, servicio) => new NotaTarifaDto
                    {
                        ServiciosId = detalle.servicios_id,
                        ServicioCodigo = servicio.servicios_codigo,
                        ServicioDescripcion = servicio.servicios_descripcioncorta,
                        MontoSugerido = detalle.configuracion_tasas_detalle_monto
                    }))
            .ToListAsync(ct);

        var proximo = await ObtenerSiguienteDocumentoAsync(ct);

        return new NotaClienteConfiguracionDto
        {
            Cliente = cliente,
            Tarifas = tarifas,
            ProximoDocumento = proximo
        };
    }

    public async Task<IReadOnlyList<NotaMotivoDto>> ListarMotivosAsync(CancellationToken ct = default)
    {
        return await _context.causa_refacturacions
            .AsNoTracking()
            .OrderBy(m => m.descripcion)
            .Select(m => new NotaMotivoDto
            {
                Id = m.ide,
                Codigo = m.codigo ?? string.Empty,
                Descripcion = m.descripcion ?? string.Empty,
                Tipo = m.tipo
            })
            .ToListAsync(ct);
    }

    public async Task<NotaMotivoDto?> ObtenerMotivoAsync(int motivoId, CancellationToken ct = default)
    {
        return await _context.causa_refacturacions
            .AsNoTracking()
            .Where(m => m.ide == motivoId)
            .Select(m => new NotaMotivoDto
            {
                Id = m.ide,
                Codigo = m.codigo ?? string.Empty,
                Descripcion = m.descripcion ?? string.Empty,
                Tipo = m.tipo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ResponseModelDto> RegistrarNotaAsync(NotaCrearRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es obligatoria.");
        }

        var cliente = await _context.cliente_maestros
            .Include(c => c.cliente_detalles)
            .FirstOrDefaultAsync(c => c.maestro_cliente_clave == dto.ClienteClave.Trim(), ct);

        if (cliente is null)
        {
            return ResponseModelDto.Fail("El cliente indicado no existe.");
        }

        var detalles = dto.Detalles?
            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.ServicioCodigo))
            .Select(d => new
            {
                Codigo = d.ServicioCodigo.Trim(),
                Descripcion = string.IsNullOrWhiteSpace(d.ServicioDescripcion)
                    ? d.ServicioCodigo.Trim()
                    : d.ServicioDescripcion.Trim(),
                Monto = Math.Round(d.Monto, 2, MidpointRounding.AwayFromZero)
            })
            .Where(d => d.Monto > 0)
            .GroupBy(d => d.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Codigo = g.Key,
                Descripcion = g.Select(x => x.Descripcion).FirstOrDefault() ?? g.Key,
                Monto = g.Sum(x => x.Monto)
            })
            .Where(d => d.Monto > 0)
            .ToList();

        if (detalles is null || detalles.Count == 0)
        {
            return ResponseModelDto.Fail("Debe agregar al menos un ajuste con monto mayor a cero.");
        }

        var motivo = await ObtenerMotivoAsync(dto.MotivoId, ct);
        if (motivo is null)
        {
            return ResponseModelDto.Fail("El motivo indicado no existe.");
        }

        var periodo = string.IsNullOrWhiteSpace(dto.Periodo)
            ? await ObtenerPeriodoActualAsync(ct)
            : dto.Periodo.Trim();

        var totalNota = detalles.Sum(d => d.Monto);
        var ultimoSaldoAjuste = await ObtenerUltimoSaldoAjusteAsync(cliente.maestro_cliente_clave, ct);
        var saldoAjuste = CalcularSaldoAjuste(dto.TipoNota, totalNota, ultimoSaldoAjuste);

        var reciboReferencia = await ObtenerReciboAplicableAsync(cliente.maestro_cliente_clave, ct);
        if (reciboReferencia is null)
        {
            return ResponseModelDto.Fail("No existe un recibo facturado para asociar la nota.");
        }

        var saldoCuenta = await ObtenerSaldoClienteAsync(cliente.maestro_cliente_clave, ct);
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var ajuste = new ajuste
            {
                fecha = hoy,
                estado = "A",
                observacion = dto.Descripcion,
                total = totalNota,
                motivo = dto.MotivoId,
                tipo_nota = (int)dto.TipoNota,
                saldo = saldoAjuste,
                periodo = periodo,
                lectura = dto.Lectura,
                usuario = usuario,
                cliente_clave = cliente.maestro_cliente_clave,
                correlativo = dto.NumeroDocumento
            };

            _context.ajustes.Add(ajuste);
            await _context.SaveChangesAsync(ct);

            var ajustesDetalle = detalles.Select(d => new ajustes_detalle
            {
                documento = ajuste.documento,
                tipo_servicio = d.Codigo,
                monto = d.Monto
            }).ToList();

            if (ajustesDetalle.Count > 0)
            {
                _context.ajustes_detalles.AddRange(ajustesDetalle);
                await _context.SaveChangesAsync(ct);
            }

            var saldoActual = saldoCuenta;
            foreach (var detalle in detalles)
            {
                var efecto = dto.TipoNota == NotaTipoDto.Debito ? detalle.Monto : -detalle.Monto;
                saldoActual += efecto;

                var transaccion = new transaccion_abonado
                {
                    cliente_clave = cliente.maestro_cliente_clave,
                    recibo = reciboReferencia,
                    tipotransaccion = dto.TipoNota == NotaTipoDto.Debito ? "206" : "205",
                    docufuente = ajuste.documento,
                    fecha_docu = hoy,
                    tipo_partida = "01",
                    descripcion = $"{(dto.TipoNota == NotaTipoDto.Debito ? NotaDebitoPrefix : NotaCreditoPrefix)} {detalle.Descripcion}",
                    debitos = dto.TipoNota == NotaTipoDto.Debito ? detalle.Monto : 0,
                    creditos = dto.TipoNota == NotaTipoDto.Credito ? detalle.Monto : 0,
                    saldo = saldoActual,
                    tipo_servicio = detalle.Codigo,
                    periodo = periodo,
                    estado = "A",
                    fecha_registro = hoy,
                    usuario = usuario,
                    saldo_detalle = efecto
                };

                _context.transaccion_abonados.Add(transaccion);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ResponseModelDto.Ok(new NotaResponseDto
            {
                DocumentoId = ajuste.documento,
                Total = totalNota,
                SaldoAjuste = saldoAjuste,
                SaldoCuenta = saldoActual
            }, "Nota registrada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ResponseModelDto.Fail($"No se pudo registrar la nota: {ex.Message}");
        }
    }

    private static readonly Expression<Func<cliente_maestro, NotaClienteLookupDto>> ClienteProjection = c => new NotaClienteLookupDto
    {
        Clave = c.maestro_cliente_clave,
        Nombre = c.maestro_cliente_nombre,
        Direccion = c.cliente_detalles
            .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
            .Select(d => d.detalle_cliente_direccion)
            .FirstOrDefault(),
        Rtn = c.maestro_cliente_rtn,
        Categoria = c.categoria_servicio != null ? c.categoria_servicio.descripcion : null,
        CicloCodigo = c.ciclos != null ? c.ciclos.ciclos_codigo : null,
        CicloDescripcion = c.ciclos != null ? c.ciclos.ciclos_descripcioncorta : null
    };

    private async Task<int> ObtenerSiguienteDocumentoAsync(CancellationToken ct)
    {
        var ultimo = await _context.ajustes
            .AsNoTracking()
            .OrderByDescending(a => a.documento)
            .Select(a => (int?)a.documento)
            .FirstOrDefaultAsync(ct);

        return (ultimo ?? 0) + 1;
    }

    private async Task<decimal> ObtenerUltimoSaldoAjusteAsync(string clienteClave, CancellationToken ct)
    {
        return await _context.ajustes
            .AsNoTracking()
            .Where(a => a.cliente_clave == clienteClave)
            .OrderByDescending(a => a.documento)
            .Select(a => a.saldo ?? 0m)
            .FirstOrDefaultAsync(ct);
    }

    private static decimal CalcularSaldoAjuste(NotaTipoDto tipo, decimal totalNota, decimal saldoAnterior) =>
        tipo == NotaTipoDto.Debito
            ? saldoAnterior - totalNota
            : saldoAnterior + totalNota;

    private async Task<decimal?> ObtenerReciboAplicableAsync(string clienteClave, CancellationToken ct)
    {
        var reciboFacturaActiva = await _context.facturas
            .AsNoTracking()
            .Where(f =>
                f.clientecodigo == clienteClave &&
                f.numrecibo > 0 &&
                f.estado == "A" &&
                (f.saldototal ?? 0) > 0)
            .OrderByDescending(f => f.fechaemision)
            .ThenByDescending(f => f.numrecibo)
            .Select(f => (decimal?)f.numrecibo)
            .FirstOrDefaultAsync(ct);

        if (reciboFacturaActiva.HasValue)
        {
            return reciboFacturaActiva;
        }

        var ultimoReciboFactura = await _context.facturas
            .AsNoTracking()
            .Where(f => f.clientecodigo == clienteClave && f.numrecibo > 0)
            .OrderByDescending(f => f.fechaemision)
            .ThenByDescending(f => f.numrecibo)
            .Select(f => (decimal?)f.numrecibo)
            .FirstOrDefaultAsync(ct);

        if (ultimoReciboFactura.HasValue)
        {
            return ultimoReciboFactura;
        }

        return await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clienteClave && t.recibo != null && t.recibo > 0)
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Select(t => t.recibo)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        return await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clienteClave)
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Select(t => t.saldo ?? 0m)
            .FirstOrDefaultAsync(ct);
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
}
