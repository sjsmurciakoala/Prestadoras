using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Cobranza;

public class CobranzaService : ICobranzaService
{
    private readonly SiadDbContext _context;

    public CobranzaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CobranzaSaldoDetalleDto>> ObtenerSaldosClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<CobranzaSaldoDetalleDto>();
        }

        var cliente = await ObtenerClienteBaseAsync(clienteClave.Trim(), ct);
        if (cliente is null)
        {
            return Array.Empty<CobranzaSaldoDetalleDto>();
        }

        var reciboReferencia = await ObtenerReciboAplicableAsync(cliente.Clave, ct);

        var movimientos = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == cliente.Clave && t.saldo_detalle != null && t.saldo_detalle != 0)
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Take(50)
            .Select(t => new
            {
                t.descripcion,
                t.saldo_detalle,
                t.periodo,
                t.tipo_servicio,
                t.recibo
            })
            .ToListAsync(ct);

        if (movimientos.Count == 0)
        {
            return new List<CobranzaSaldoDetalleDto>
            {
                new()
                {
                    ClienteClave = cliente.Clave,
                    ClienteNombre = cliente.Nombre,
                    Descripcion = "Sin movimientos pendientes",
                    SaldoDetalle = 0,
                    Recibo = reciboReferencia,
                    Direccion = cliente.Direccion,
                    CicloId = cliente.CicloId,
                    CicloDescripcion = cliente.CicloDescripcion,
                    Bloqueado = cliente.Bloqueado
                }
            };
        }

        return movimientos
            .Select(m => new CobranzaSaldoDetalleDto
            {
                ClienteClave = cliente.Clave,
                ClienteNombre = cliente.Nombre,
                Descripcion = string.IsNullOrWhiteSpace(m.descripcion) ? "Movimiento" : m.descripcion,
                SaldoDetalle = m.saldo_detalle ?? 0,
                Recibo = reciboReferencia ?? m.recibo,
                Direccion = cliente.Direccion,
                CicloId = cliente.CicloId,
                CicloDescripcion = cliente.CicloDescripcion,
                Bloqueado = cliente.Bloqueado,
                Periodo = m.periodo,
                TipoServicio = m.tipo_servicio
            })
            .ToList();
    }

    public async Task<bool> EstaBloqueadoAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return false;
        }

        var cliente = await ObtenerClienteBaseAsync(clienteClave.Trim(), ct);
        return cliente?.Bloqueado ?? false;
    }

    public async Task<string?> NumeroALetrasAsync(decimal numero, CancellationToken ct = default)
    {
        return await _context.Database.SqlQueryRaw<string>("SELECT public.fn_numero_letras({0})", numero)
            .FirstOrDefaultAsync(ct);
    }

    public Task<CobranzaPlanPreviewDto> CalcularCuotasAsync(CobranzaPlanPreviewRequestDto dto, CancellationToken ct = default)
    {
        if (dto.Meses <= 0 || dto.MontoFinanciar <= 0)
        {
            return Task.FromResult(new CobranzaPlanPreviewDto());
        }

        var fechaBase = dto.FechaPrimerPago == DateTime.MinValue
            ? DateTime.Today
            : dto.FechaPrimerPago;

        var cuotas = new List<CobranzaCuotaDto>();
        var cuotaBase = Math.Round(dto.MontoFinanciar / dto.Meses, 2, MidpointRounding.AwayFromZero);
        var ajuste = Math.Round(dto.MontoFinanciar - (cuotaBase * dto.Meses), 2, MidpointRounding.AwayFromZero);

        for (int i = 0; i < dto.Meses; i++)
        {
            var valor = cuotaBase;
            if (i == dto.Meses - 1)
            {
                valor += ajuste;
            }

            cuotas.Add(new CobranzaCuotaDto
            {
                Numero = i + 1,
                Fecha = fechaBase.AddMonths(i),
                Valor = valor
            });
        }

        return Task.FromResult(new CobranzaPlanPreviewDto
        {
            ValorCuota = cuotaBase,
            Cuotas = cuotas
        });
    }

    public async Task<ResponseModelDto> GuardarPlanPagoAsync(CobranzaPlanGuardarDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es requerida.");
        }

        if (dto.Meses <= 0)
        {
            return ResponseModelDto.Fail("Debe especificar la cantidad de meses del plan.");
        }

        if (dto.MontoFinanciar <= 0)
        {
            return ResponseModelDto.Fail("El monto a financiar debe ser mayor a cero.");
        }

        var cliente = await ObtenerClienteBaseAsync(dto.ClienteClave.Trim(), ct);
        if (cliente is null)
        {
            return ResponseModelDto.Fail("El cliente indicado no existe.");
        }

        var correlativo = await GenerarCorrelativoAsync(ct);
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var fechaPlan = dto.Fecha == DateTime.MinValue ? DateTime.UtcNow : dto.Fecha;
        var fechaPrimerPago = dto.FechaPrimerPago == DateTime.MinValue
            ? new DateTime(fechaPlan.Year, fechaPlan.Month, 1).AddMonths(1)
            : dto.FechaPrimerPago;

        var montoPrima = dto.ValorPrima > 0
            ? dto.ValorPrima
            : Math.Round(dto.Total * (dto.PorcentajePrima / 100m), 2, MidpointRounding.AwayFromZero);

        var recibo = dto.Recibo ?? await ObtenerReciboAplicableAsync(cliente.Clave, ct);
        if (recibo is null)
        {
            return ResponseModelDto.Fail("No existe un recibo (transacción 101) asociado al cliente.");
        }

        var cuotaBase = Math.Round(dto.MontoFinanciar / dto.Meses, 2, MidpointRounding.AwayFromZero);
        var ajuste = Math.Round(dto.MontoFinanciar - cuotaBase * dto.Meses, 2, MidpointRounding.AwayFromZero);
        var saldoCliente = await ObtenerSaldoClienteAsync(cliente.Clave, ct);
        var tieneMedidor = cliente.TieneMedidor ? "S" : "N";
        var ciclo = dto.CicloId?.ToString() ?? cliente.CicloId?.ToString();
        var periodoActual = fechaPlan.ToString("yyyy/MM");

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var header = new cln_plan_pago_hdr
            {
                correlativo = correlativo,
                clienteid = cliente.Id,
                monto = Math.Round(dto.Total, 2, MidpointRounding.AwayFromZero),
                direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? cliente.Direccion : dto.Direccion,
                representante = dto.NomRepresentante,
                docrepresentante = dto.DocRepresentante,
                numrepresentante = dto.NumRepresentante,
                fecha = fechaPlan,
                fechappago = fechaPrimerPago,
                comentario = dto.Comentario,
                porcprima = dto.PorcentajePrima,
                vprima = montoPrima,
                montofinanc = dto.MontoFinanciar,
                meses = dto.Meses,
                estadopago = "Pendiente",
                usuariocreacion = usuario,
                usuariomodificacion = usuario,
                fechacreacion = DateTime.UtcNow,
                fechamodificacion = DateTime.UtcNow
            };

            _context.cln_plan_pago_hdrs.Add(header);
            await _context.SaveChangesAsync(ct);

            var cuotasDetalle = new List<cln_plan_pago_dtl>();
            var fechaCuotaBase = fechaPrimerPago;
            for (int i = 0; i < dto.Meses; i++)
            {
                var valor = cuotaBase;
                if (i == dto.Meses - 1)
                {
                    valor += ajuste;
                }

                cuotasDetalle.Add(new cln_plan_pago_dtl
                {
                    idhdr = header.id,
                    valorcuota = valor,
                    fechacuota = fechaCuotaBase.AddMonths(i),
                    mes = i + 1,
                    estadopago = "Pendiente",
                    usuariocreacion = usuario,
                    usuariomodificacion = usuario,
                    fechacreacion = DateTime.UtcNow,
                    fechamodificacion = DateTime.UtcNow
                });
            }

            if (cuotasDetalle.Count > 0)
            {
                _context.cln_plan_pago_dtls.AddRange(cuotasDetalle);
            }

            var trasladoFondos = new transaccion_abonado
            {
                cliente_clave = cliente.Clave,
                recibo = recibo,
                tipotransaccion = "PLAN",
                docufuente = header.id,
                fecha_docu = DateOnly.FromDateTime(fechaPlan),
                tipo_partida = "01",
                descripcion = "Traslado de Fondos",
                debitos = 0,
                creditos = dto.MontoFinanciar,
                saldo = saldoCliente - (dto.MontoFinanciar + montoPrima),
                periodo = periodoActual,
                estado = "C",
                fecha_registro = DateOnly.FromDateTime(DateTime.UtcNow),
                ciclo = ciclo,
                tiene_med = tieneMedidor,
                usuario = usuario,
                saldo_detalle = 0
            };

            var prima = new transaccion_abonado
            {
                cliente_clave = cliente.Clave,
                recibo = recibo,
                tipotransaccion = "PLAN-PR",
                docufuente = header.id,
                fecha_docu = DateOnly.FromDateTime(fechaPlan),
                tipo_partida = "01",
                descripcion = "Concepto de Prima",
                debitos = montoPrima,
                creditos = 0,
                saldo = saldoCliente + montoPrima,
                periodo = periodoActual,
                estado = "A",
                fecha_registro = DateOnly.FromDateTime(DateTime.UtcNow),
                ciclo = ciclo,
                tiene_med = tieneMedidor,
                usuario = usuario,
                saldo_detalle = montoPrima
            };

            var transaccionesCuotas = new List<transaccion_abonado>();
            decimal saldoCuotas = 0m;
            for (int i = 0; i < dto.Meses; i++)
            {
                var valor = cuotaBase;
                if (i == dto.Meses - 1)
                {
                    valor += ajuste;
                }

                saldoCuotas += valor;
                var fechaCuota = fechaPrimerPago.AddMonths(i);

                transaccionesCuotas.Add(new transaccion_abonado
                {
                    cliente_clave = cliente.Clave,
                    recibo = recibo,
                    tipotransaccion = "PLAN-CUOTA",
                    docufuente = header.id,
                    fecha_docu = DateOnly.FromDateTime(fechaCuota),
                    tipo_partida = "01",
                    descripcion = $"Cuota#{i + 1}",
                    debitos = valor,
                    creditos = 0,
                    saldo = saldoCuotas,
                    periodo = fechaCuota.ToString("yyyy/MM"),
                    estado = "A",
                    fecha_registro = DateOnly.FromDateTime(DateTime.UtcNow),
                    ciclo = ciclo,
                    tiene_med = tieneMedidor,
                    usuario = usuario,
                    saldo_detalle = valor
                });
            }

            _context.transaccion_abonados.AddRange(new[] { trasladoFondos, prima });
            if (transaccionesCuotas.Count > 0)
            {
                _context.transaccion_abonados.AddRange(transaccionesCuotas);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ResponseModelDto.Ok(new { header.correlativo }, "Plan de pago registrado correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return ResponseModelDto.Fail($"No se pudo guardar el plan: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<CobranzaPlanResumenDto>> ListarPlanesAsync(CancellationToken ct = default)
    {
        return await _context.vw_listaplanespagos
            .AsNoTracking()
            .OrderByDescending(p => p.fecha)
            .Select(p => new CobranzaPlanResumenDto
            {
                Correlativo = p.correlativo ?? string.Empty,
                Cliente = p.nombrecliente ?? string.Empty,
                Estado = p.estado ?? string.Empty,
                Total = p.total ?? 0,
                Fecha = p.fecha,
                EncabezadoId = p.idhdr,
                ClienteClave = p.codcliente ?? string.Empty
            })
            .ToListAsync(ct);
    }

    public async Task<CobranzaPlanDetalleDto?> ObtenerPlanAsync(string correlativo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correlativo))
        {
            return null;
        }

        var header = await _context.cln_plan_pago_hdrs
            .AsNoTracking()
            .Where(h => h.correlativo == correlativo.Trim())
            .Select(h => new
            {
                h,
                ClienteClave = h.cliente != null ? h.cliente.maestro_cliente_clave : string.Empty,
                ClienteNombre = h.cliente != null ? h.cliente.maestro_cliente_nombre : string.Empty
            })
            .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return null;
        }

        var cuotas = await _context.cln_plan_pago_dtls
            .AsNoTracking()
            .Where(d => d.idhdr == header.h.id)
            .OrderBy(d => d.mes)
            .Select(d => new CobranzaPlanDetalleCuotaDto
            {
                Numero = d.mes ?? 0,
                Fecha = d.fechacuota,
                Valor = d.valorcuota ?? 0,
                Estado = d.estadopago ?? "Pendiente"
            })
            .ToListAsync(ct);

        var recibo = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.docufuente == header.h.id && t.recibo != null)
            .OrderByDescending(t => t.ide)
            .Select(t => t.recibo)
            .FirstOrDefaultAsync(ct);

        return new CobranzaPlanDetalleDto
        {
            Correlativo = header.h.correlativo ?? string.Empty,
            ClienteClave = header.ClienteClave,
            ClienteNombre = header.ClienteNombre,
            Monto = header.h.monto ?? 0,
            Prima = header.h.vprima ?? 0,
            MontoFinanciar = header.h.montofinanc ?? 0,
            Meses = header.h.meses ?? 0,
            Fecha = header.h.fecha,
            FechaPrimerPago = header.h.fechappago,
            Direccion = header.h.direccion,
            Comentario = header.h.comentario,
            Representante = header.h.representante,
            DocRepresentante = header.h.docrepresentante,
            NumRepresentante = header.h.numrepresentante,
            Recibo = recibo,
            Estado = header.h.estadopago ?? "Pendiente",
            Cuotas = cuotas
        };
    }

    private async Task<ClienteBase?> ObtenerClienteBaseAsync(string clienteClave, CancellationToken ct)
    {
        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == clienteClave)
            .Select(c => new ClienteBase(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault(),
                c.ciclos_id,
                c.ciclos != null ? c.ciclos.ciclos_descripcioncorta : null,
                c.bloqueado_cobranza ?? false,
                c.maestro_cliente_tiene_medidor ?? false))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string> GenerarCorrelativoAsync(CancellationToken ct)
    {
        var ultimo = await _context.cln_plan_pago_hdrs
            .AsNoTracking()
            .Where(h => h.correlativo != null)
            .OrderByDescending(h => h.correlativo)
            .Select(h => h.correlativo)
            .FirstOrDefaultAsync(ct);

        if (int.TryParse(ultimo, out var numero))
        {
            return (numero + 1).ToString("D6");
        }

        return 1.ToString("D6");
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

    private async Task<decimal?> ObtenerReciboAplicableAsync(string clienteClave, CancellationToken ct)
    {
        return await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clienteClave && t.tipotransaccion == "101")
            .OrderByDescending(t => t.ide)
            .Select(t => t.recibo)
            .FirstOrDefaultAsync(ct);
    }

    private sealed record ClienteBase(
        int Id,
        string Clave,
        string Nombre,
        string? Direccion,
        int? CicloId,
        string? CicloDescripcion,
        bool Bloqueado,
        bool TieneMedidor);
}
