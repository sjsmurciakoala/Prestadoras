using System.Data;
using System.Text;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Cobranza;

public class CobranzaService : ICobranzaService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IDocumentoCobranzaGenerator _documentoGenerator;

    public CobranzaService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IDocumentoCobranzaGenerator documentoGenerator)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
        _documentoGenerator = documentoGenerator;
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
        var ahora = NormalizeTimestamp(DateTime.Now);
        var fechaPlan = dto.Fecha == DateTime.MinValue ? ahora.Date : NormalizeTimestamp(dto.Fecha).Date;
        var fechaPrimerPago = dto.FechaPrimerPago == DateTime.MinValue
            ? new DateTime(fechaPlan.Year, fechaPlan.Month, 1).AddMonths(1)
            : NormalizeTimestamp(dto.FechaPrimerPago).Date;

        var montoPrima = dto.ValorPrima > 0
            ? dto.ValorPrima
            : Math.Round(dto.Total * (dto.PorcentajePrima / 100m), 2, MidpointRounding.AwayFromZero);

        var recibo = dto.Recibo ?? await ObtenerReciboAplicableAsync(cliente.Clave, ct);
        if (recibo is null)
        {
            return ResponseModelDto.Fail("No existe un recibo facturado asociado al cliente.");
        }

        var cuotaBase = Math.Round(dto.MontoFinanciar / dto.Meses, 2, MidpointRounding.AwayFromZero);
        var ajuste = Math.Round(dto.MontoFinanciar - cuotaBase * dto.Meses, 2, MidpointRounding.AwayFromZero);
        var saldoCliente = await ObtenerSaldoClienteAsync(cliente.Clave, ct);
        var tieneMedidor = cliente.TieneMedidor ? "S" : "N";
        var ciclo = dto.CicloId?.ToString() ?? cliente.CicloId?.ToString();
        var periodoActual = fechaPlan.ToString("yyyy/MM");
        var fechaRegistro = DateOnly.FromDateTime(ahora);

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
                fechacreacion = ahora,
                fechamodificacion = ahora
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
                    fechacreacion = ahora,
                    fechamodificacion = ahora
                });
            }

            if (cuotasDetalle.Count > 0)
            {
                _context.cln_plan_pago_dtls.AddRange(cuotasDetalle);
            }

            var trasladoFondos = new transaccion_abonado
            {
                company_id = _currentCompanyService.GetCompanyId(),
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
                fecha_registro = fechaRegistro,
                ciclo = ciclo,
                tiene_med = tieneMedidor,
                usuario = usuario,
                saldo_detalle = 0
            };

            var prima = new transaccion_abonado
            {
                company_id = _currentCompanyService.GetCompanyId(),
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
                fecha_registro = fechaRegistro,
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
                    company_id = _currentCompanyService.GetCompanyId(),
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
                    fecha_registro = fechaRegistro,
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
            return ResponseModelDto.Fail($"No se pudo guardar el plan: {GetInnermostMessage(ex)}");
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

    private static DateTime NormalizeTimestamp(DateTime value)
    {
        if (value == DateTime.MinValue)
        {
            return value;
        }

        return value.Kind == DateTimeKind.Unspecified
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static string GetInnermostMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    public async Task<IReadOnlyList<AccionCobranzaDto>> ListarAccionesAsync(
        string clienteClave, CancellationToken ct = default)
    {
        return await _context.cln_accion_cobranzas
            .AsNoTracking()
            .Where(a => a.codigocliente == clienteClave)
            .OrderByDescending(a => a.fecha)
            .Select(a => new AccionCobranzaDto(
                a.id, a.fecha,
                a.cod_accion, a.accion,
                a.cod_observacion,
                // JOIN con catálogo para obtener el texto del resultado
                _context.axl_observacion_cobranzas
                    .Where(o => o.id == a.cod_observacion)
                    .Select(o => o.observacion)
                    .FirstOrDefault(),
                a.observacion,
                a.abogado_id, a.ejecutado_por,
                // id del snapshot de documento, si la acción generó uno
                _context.cln_accion_cobranza_documentos
                    .Where(doc => doc.accion_id == a.id)
                    .OrderByDescending(doc => doc.id)
                    .Select(doc => (int?)doc.id)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccionCobranzaHistorialDto>> ListarHistorialAccionesAsync(
        DateTime desde, DateTime hasta, int? codAccion, string? clienteClave,
        string? ejecutadoPor, CancellationToken ct = default)
    {
        // La columna fecha es timestamp sin zona horaria; Npgsql rechaza parámetros con Kind=Utc.
        var desdeInclusivo = DateTime.SpecifyKind(desde.Date, DateTimeKind.Unspecified);
        var hastaExclusivo = DateTime.SpecifyKind(hasta.Date, DateTimeKind.Unspecified).AddDays(1);

        var query = _context.cln_accion_cobranzas
            .AsNoTracking()
            .Where(a => a.fecha >= desdeInclusivo && a.fecha < hastaExclusivo);

        if (codAccion is not null)
        {
            query = query.Where(a => a.cod_accion == codAccion);
        }

        if (!string.IsNullOrWhiteSpace(clienteClave))
        {
            var clave = clienteClave.Trim();
            query = query.Where(a => a.codigocliente == clave);
        }

        if (!string.IsNullOrWhiteSpace(ejecutadoPor))
        {
            var patron = ejecutadoPor.Trim().ToLower();
            query = query.Where(a => a.ejecutado_por != null &&
                                     a.ejecutado_por.ToLower().Contains(patron));
        }

        return await query
            .OrderByDescending(a => a.fecha)
            .ThenByDescending(a => a.id)
            .Take(CobranzaHistorialConstantes.MaxFilas)
            .Select(a => new AccionCobranzaHistorialDto(
                a.id,
                a.fecha,
                a.codigocliente,
                _context.cliente_maestros
                    .Where(c => c.maestro_cliente_clave == a.codigocliente)
                    .Select(c => c.maestro_cliente_nombre)
                    .FirstOrDefault(),
                a.accion,
                _context.axl_observacion_cobranzas
                    .Where(o => o.id == a.cod_observacion)
                    .Select(o => o.observacion)
                    .FirstOrDefault(),
                a.observacion,
                _context.abogados
                    .Where(b => b.abogado_id == a.abogado_id)
                    .Select(b => b.abogado_nombrecorto)
                    .FirstOrDefault(),
                a.ejecutado_por))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccionCobranzaCatalogoDto>> ObtenerCatalogoAccionesAsync(
        CancellationToken ct = default)
    {
        return await _context.axl_accion_cobranzas
            .AsNoTracking()
            .Where(a => a.activo)
            .OrderBy(a => a.cod_accion)
            .Select(a => new AccionCobranzaCatalogoDto(a.cod_accion, a.nombre))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ObservacionCobranzaCatalogoDto>> ObtenerCatalogoObservacionesAsync(
        CancellationToken ct = default)
    {
        return await _context.axl_observacion_cobranzas
            .AsNoTracking()
            .Where(o => o.activo)
            .OrderBy(a => a.id)
            .Select(a => new ObservacionCobranzaCatalogoDto(a.id, a.observacion))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AbogadoCobranzaLookupDto>> ObtenerAbogadosAsync(
        CancellationToken ct = default)
    {
        return await _context.abogados
            .AsNoTracking()
            .Where(a => a.estado)
            .OrderBy(a => a.abogado_codigo)
            .Select(a => new AbogadoCobranzaLookupDto(
                a.abogado_id, a.abogado_codigo, a.abogado_nombrecorto))
            .ToListAsync(ct);
    }

    public async Task<RegistrarAccionResultadoDto> RegistrarAccionAsync(
        RegistrarAccionCobranzaRequest request,
        string ejecutadoPor,   // usuario de sesión; se usa como generado_por del documento
        CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        // Obtener nombre + configuración de documento de la acción del catálogo
        var accion = await _context.axl_accion_cobranzas
            .AsNoTracking()
            .Where(a => a.cod_accion == request.CodAccion)
            .Select(a => new { a.nombre, a.genera_documento, a.documento_codigo })
            .FirstOrDefaultAsync(ct);

        var accionNombre = accion?.nombre ?? request.CodAccion.ToString();

        var entity = new cln_accion_cobranza
        {
            company_id      = companyId,
            codigocliente   = request.ClienteClave,
            fecha           = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            accion          = accionNombre,
            observacion     = request.Observacion,
            cod_accion      = request.CodAccion,
            cod_observacion = request.CodObservacion,
            abogado_id      = request.AbogadoId,
            // ejecutado_por viene del formulario (tercero), no del usuario de sesión
            ejecutado_por   = request.EjecutadoPor
        };

        _context.cln_accion_cobranzas.Add(entity);
        await _context.SaveChangesAsync(ct);

        // Si la acción genera documento, se produce y archiva el snapshot.
        // Best-effort: si falla, la bitácora ya quedó registrada y se reporta el error.
        if (accion?.genera_documento == true && !string.IsNullOrWhiteSpace(accion.documento_codigo))
        {
            try
            {
                var documentoId = await GenerarYArchivarDocumentoAsync(
                    entity.id, accion.documento_codigo!, request.ClienteClave,
                    request.EjecutadoPor ?? ejecutadoPor, ejecutadoPor, ct);
                return new RegistrarAccionResultadoDto(entity.id, documentoId, true, null);
            }
            catch (Exception ex)
            {
                return new RegistrarAccionResultadoDto(entity.id, null, false, ex.Message);
            }
        }

        return new RegistrarAccionResultadoDto(entity.id, null, false, null);
    }

    public async Task<int?> RegenerarDocumentoAccionAsync(int accionId, string usuario, CancellationToken ct = default)
    {
        var accion = await _context.cln_accion_cobranzas
            .AsNoTracking()
            .Where(a => a.id == accionId)
            .Select(a => new { a.id, a.codigocliente, a.cod_accion, a.ejecutado_por })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Acción {accionId} no encontrada.");

        var codigo = await _context.axl_accion_cobranzas
            .AsNoTracking()
            .Where(a => a.cod_accion == accion.cod_accion && a.genera_documento)
            .Select(a => a.documento_codigo)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new InvalidOperationException("La acción no está configurada para generar documento.");
        }

        return await GenerarYArchivarDocumentoAsync(
            accion.id, codigo!, accion.codigocliente, accion.ejecutado_por ?? usuario, usuario, ct);
    }

    private async Task<int> GenerarYArchivarDocumentoAsync(
        int accionId, string documentoCodigo, string clienteClave,
        string? firmante, string? generadoPor, CancellationToken ct)
    {
        var cliente = await ObtenerClienteBaseAsync(clienteClave.Trim(), ct);
        var total = await ObtenerSaldoClienteAsync(clienteClave.Trim(), ct);

        var datos = new DocumentoCobranzaDatos(
            ClienteClave: clienteClave,
            ClienteNombre: cliente?.Nombre ?? clienteClave,
            Direccion: cliente?.Direccion,
            TotalAdeudado: total,
            Firmante: firmante,
            FechaEmision: DateTime.Today,
            PlazoDias: 8);

        var generado = _documentoGenerator.Generar(documentoCodigo, datos);

        var snapshot = new cln_accion_cobranza_documento
        {
            company_id       = _currentCompanyService.GetCompanyId(),
            accion_id        = accionId,
            documento_codigo = documentoCodigo,
            nombre_archivo   = generado.NombreArchivo,
            contenido        = generado.Contenido,
            content_type     = generado.ContentType,
            generado_en      = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            generado_por     = generadoPor
        };

        _context.cln_accion_cobranza_documentos.Add(snapshot);
        await _context.SaveChangesAsync(ct);
        return snapshot.id;
    }

    public async Task<DocumentoGenerado?> ObtenerDocumentoAccionAsync(int documentoId, CancellationToken ct = default)
    {
        return await _context.cln_accion_cobranza_documentos
            .AsNoTracking()
            .Where(d => d.id == documentoId)
            .Select(d => new DocumentoGenerado(d.nombre_archivo, d.contenido, d.content_type))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BloqueoClienteEstadoDto?> ObtenerEstadoBloqueoAsync(
        string clienteClave, CancellationToken ct = default)
    {
        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == clienteClave)
            .Select(c => new BloqueoClienteEstadoDto(
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.bloqueado_cobranza ?? false))
            .FirstOrDefaultAsync(ct);
    }

    public async Task BloquearDesbloquearAsync(
        string clienteClave, bool bloquear, string? motivo, string usuario, CancellationToken ct = default)
    {
        var cliente = await _context.cliente_maestros
            .FirstOrDefaultAsync(c => c.maestro_cliente_clave == clienteClave, ct)
            ?? throw new KeyNotFoundException($"Cliente {clienteClave} no encontrado.");

        var valorAnterior = cliente.bloqueado_cobranza;

        cliente.bloqueado_cobranza = bloquear;
        cliente.usuariomodificacion = usuario;
        cliente.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var companyId = _currentCompanyService.GetCompanyId();
        _context.cln_cliente_estado_logs.Add(new SIAD.Core.Entities.cln_cliente_estado_log
        {
            company_id     = companyId,
            codigocliente  = clienteClave,
            tipo           = "BLOQUEO_COBRO",
            valor_anterior = valorAnterior,
            valor_nuevo    = bloquear,
            motivo         = motivo,
            usuario        = usuario,
            fecha          = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LlamadaCobranzaDto>> ListarLlamadasAsync(string clienteClave, CancellationToken ct = default)
    {
        return await _context.cln_llamada_cobranzas
            .AsNoTracking()
            .Where(l => l.codigocliente == clienteClave)
            .OrderByDescending(l => l.fecha)
            .Select(l => new LlamadaCobranzaDto(
                l.id, l.fecha, l.numero_llamado,
                l.resultado, l.observacion, l.usuario))
            .ToListAsync(ct);
    }

    public async Task RegistrarLlamadaAsync(RegistrarLlamadaRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var llamada = new cln_llamada_cobranza
        {
            company_id    = companyId,
            codigocliente = request.ClienteClave,
            fecha         = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            numero_llamado = request.NumeroLlamado,
            resultado     = request.Resultado,
            observacion   = request.Observacion,
            usuario       = usuario,
            usuariocreacion = usuario,
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.cln_llamada_cobranzas.Add(llamada);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotaCobroDto>> ListarNotasCobroAsync(string clienteClave, CancellationToken ct = default)
    {
        return await _context.cln_nota_cobros
            .AsNoTracking()
            .Where(n => n.codigocliente == clienteClave)
            .OrderByDescending(n => n.fecha)
            .ThenByDescending(n => n.id)
            .Select(n => new NotaCobroDto(
                n.id,
                n.correlativo,
                n.fechacreacion ?? DateTime.MinValue,
                n.monto,
                n.descripcion,
                n.estado,
                n.usuario))
            .ToListAsync(ct);
    }

    public async Task<NotaCobroDto> EmitirNotaCobroAsync(EmitirNotaCobroRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var correlativo = await GenerarCorrelativoNotaCobroAsync(ct);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var nota = new cln_nota_cobro
        {
            company_id    = companyId,
            correlativo   = correlativo,
            codigocliente = request.ClienteClave,
            fecha         = DateOnly.FromDateTime(ahora),
            monto         = Math.Round(request.Monto, 2, MidpointRounding.AwayFromZero),
            descripcion   = request.Descripcion,
            estado        = "EMITIDA",
            usuario       = usuario,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        _context.cln_nota_cobros.Add(nota);
        await _context.SaveChangesAsync(ct);

        return new NotaCobroDto(nota.id, nota.correlativo,
            ahora, nota.monto, nota.descripcion, nota.estado, nota.usuario);
    }

    public async Task AnularNotaCobroAsync(int id, string motivo, CancellationToken ct = default)
    {
        var nota = await _context.cln_nota_cobros
            .FirstOrDefaultAsync(n => n.id == id, ct)
            ?? throw new KeyNotFoundException($"Nota de cobro {id} no encontrada.");

        nota.estado      = "ANULADA";
        nota.descripcion = string.IsNullOrWhiteSpace(nota.descripcion)
            ? $"ANULADA: {motivo}"
            : $"{nota.descripcion} | ANULADA: {motivo}";

        await _context.SaveChangesAsync(ct);
    }

    private async Task<string> GenerarCorrelativoNotaCobroAsync(CancellationToken ct)
    {
        var ultimo = await _context.cln_nota_cobros
            .AsNoTracking()
            .Where(n => n.correlativo != null)
            .OrderByDescending(n => n.correlativo)
            .Select(n => n.correlativo)
            .FirstOrDefaultAsync(ct);

        if (int.TryParse(ultimo, out var numero))
            return (numero + 1).ToString("D6");

        return 1.ToString("D6");
    }

    public async Task<IReadOnlyList<AccionCobranzaCrudDto>> ListarAccionesCrudAsync(CancellationToken ct = default)
        => await _context.axl_accion_cobranzas
            .AsNoTracking()
            .OrderBy(a => a.cod_accion)
            .Select(a => new AccionCobranzaCrudDto(
                a.cod_accion, a.nombre, a.activo, a.genera_documento, a.documento_codigo))
            .ToListAsync(ct);

    public async Task GuardarAccionAsync(AccionCobranzaSaveDto dto, CancellationToken ct = default)
    {
        // Solo se archiva el código de documento cuando la acción lo genera.
        var documentoCodigo = dto.GeneraDocumento ? dto.DocumentoCodigo?.Trim() : null;

        if (dto.CodAccion.HasValue)
        {
            var entity = await _context.axl_accion_cobranzas.FindAsync([dto.CodAccion.Value], ct)
                ?? throw new InvalidOperationException($"Acción {dto.CodAccion} no encontrada.");
            entity.nombre = dto.Nombre.Trim();
            entity.activo = dto.Activo;
            entity.genera_documento = dto.GeneraDocumento;
            entity.documento_codigo = documentoCodigo;
        }
        else
        {
            _context.axl_accion_cobranzas.Add(new axl_accion_cobranza
            {
                nombre = dto.Nombre.Trim(),
                activo = dto.Activo,
                genera_documento = dto.GeneraDocumento,
                documento_codigo = documentoCodigo
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ObservacionCobranzaCrudDto>> ListarObservacionesCrudAsync(CancellationToken ct = default)
        => await _context.axl_observacion_cobranzas
            .AsNoTracking()
            .OrderBy(o => o.id)
            .Select(o => new ObservacionCobranzaCrudDto(o.id, o.observacion, o.activo))
            .ToListAsync(ct);

    public async Task GuardarObservacionAsync(ObservacionCobranzaSaveDto dto, CancellationToken ct = default)
    {
        if (dto.Id.HasValue)
        {
            var entity = await _context.axl_observacion_cobranzas.FindAsync([dto.Id.Value], ct)
                ?? throw new InvalidOperationException($"Observación {dto.Id} no encontrada.");
            entity.observacion = dto.Observacion.Trim();
            entity.activo = dto.Activo;
        }
        else
        {
            _context.axl_observacion_cobranzas.Add(new axl_observacion_cobranza
            {
                observacion = dto.Observacion.Trim(),
                activo = dto.Activo
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    // ── Clientes para cobros (acciones en lote / cartas de cobro) ──────────────

    public async Task<IReadOnlyList<ClienteCobroDto>> ListarClientesCobroAsync(
        ClienteCobroFiltroDto filtro, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        var sb = new StringBuilder("""
            SELECT
                cm.maestro_cliente_clave            AS Clave,
                cm.maestro_cliente_nombre           AS Nombre,
                cd.direccion                        AS Direccion,
                cm.ciclos_id                        AS CicloId,
                cm.barrio_codigo                    AS BarrioCodigo,
                cm.categoria_servicio_id            AS CategoriaId,
                cm.maestro_cliente_indicativo_ruta  AS Ruta,
                COALESCE(ta_s.saldo, 0)             AS SaldoAdeudado,
                CASE
                    WHEN ta_p.ultima_pago IS NULL THEN NULL
                    ELSE CURRENT_DATE - ta_p.ultima_pago
                END                                 AS DiasMora,
                ta_p.ultima_pago                    AS UltimoPago,
                COALESCE(cm.bloqueado_cobranza, FALSE) AS Bloqueado,
                COALESCE(cm.no_cortable, FALSE)        AS NoCortable,
                cm.abogado                          AS AbogadoId
            FROM cliente_maestro cm
            LEFT JOIN LATERAL (
                SELECT d.detalle_cliente_direccion AS direccion
                FROM cliente_detalle d
                WHERE d.maestro_cliente_id = cm.maestro_cliente_id
                ORDER BY COALESCE(d.fechamodificacion, d.fechacreacion) DESC
                LIMIT 1
            ) cd ON TRUE
            LEFT JOIN LATERAL (
                SELECT ta.saldo
                FROM transaccion_abonado ta
                WHERE ta.company_id    = cm.company_id
                  AND ta.cliente_clave = cm.maestro_cliente_clave
                  AND ta.estado        = 'A'
                ORDER BY ta.ide DESC
                LIMIT 1
            ) ta_s ON TRUE
            LEFT JOIN LATERAL (
                SELECT MAX(ta.fecha_docu) AS ultima_pago
                FROM transaccion_abonado ta
                WHERE ta.company_id    = cm.company_id
                  AND ta.cliente_clave = cm.maestro_cliente_clave
                  AND ta.tipotransaccion ILIKE '%PAGO%'
            ) ta_p ON TRUE
            WHERE cm.company_id = @CompanyId
              AND cm.estado = TRUE
              AND COALESCE(ta_s.saldo, 0) > 0
            """);

        if (filtro.ValorMinimo.HasValue)
            sb.Append(" AND COALESCE(ta_s.saldo, 0) >= @ValorMinimo");

        if (filtro.CicloId.HasValue)
            sb.Append(" AND cm.ciclos_id = @CicloId");

        if (!string.IsNullOrWhiteSpace(filtro.BarrioCodigo))
            sb.Append(" AND cm.barrio_codigo = @BarrioCodigo");

        if (filtro.CategoriaId.HasValue)
            sb.Append(" AND cm.categoria_servicio_id = @CategoriaId");

        if (!string.IsNullOrWhiteSpace(filtro.Ruta))
            sb.Append(" AND cm.maestro_cliente_indicativo_ruta = @Ruta");

        if (filtro.DiasMoraMin.HasValue)
            sb.Append(" AND ta_p.ultima_pago IS NOT NULL AND (CURRENT_DATE - ta_p.ultima_pago) >= @DiasMoraMin");

        if (filtro.ExcluirBloqueados)
            sb.Append(" AND COALESCE(cm.bloqueado_cobranza, FALSE) = FALSE");

        if (filtro.ExcluirNoCortables)
            sb.Append(" AND COALESCE(cm.no_cortable, FALSE) = FALSE");

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            sb.Append(" AND (cm.maestro_cliente_clave ILIKE @Busqueda OR cm.maestro_cliente_nombre ILIKE @Busqueda)");

        sb.Append(" ORDER BY cm.maestro_cliente_nombre LIMIT 5000");

        var busqueda = string.IsNullOrWhiteSpace(filtro.Busqueda) ? null : $"%{filtro.Busqueda.Trim()}%";

        var rows = await connection.QueryAsync<ClienteCobroRow>(
            new CommandDefinition(sb.ToString(),
                new
                {
                    CompanyId    = companyId,
                    ValorMinimo  = filtro.ValorMinimo,
                    CicloId      = filtro.CicloId,
                    BarrioCodigo = filtro.BarrioCodigo,
                    CategoriaId  = filtro.CategoriaId,
                    Ruta         = filtro.Ruta,
                    DiasMoraMin  = filtro.DiasMoraMin,
                    Busqueda     = busqueda
                },
                cancellationToken: ct));

        return rows
            .Select(r => new ClienteCobroDto(
                r.Clave, r.Nombre, r.Direccion, r.CicloId, r.BarrioCodigo,
                r.CategoriaId, r.Ruta, r.SaldoAdeudado, r.DiasMora, r.UltimoPago,
                r.Bloqueado, r.NoCortable, r.AbogadoId))
            .ToList();
    }

    public async Task<IReadOnlyList<CarteraVencidaClienteDto>> ListarCarteraVencidaAsync(
        CarteraVencidaFiltroDto filtro, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var fechaCorte = filtro.FechaCorte ?? DateOnly.FromDateTime(DateTime.Today);

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        var sb = new StringBuilder("""
            SELECT
                f.clientecodigo                     AS Clave,
                cm.maestro_cliente_nombre           AS Nombre,
                cm.ciclos_id                        AS CicloId,
                cm.maestro_cliente_indicativo_ruta  AS Ruta,
                COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) <= 30), 0)             AS B0_30,
                COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) BETWEEN 31 AND 60), 0)  AS B31_60,
                COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) BETWEEN 61 AND 120), 0) AS B61_120,
                COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) > 120), 0)             AS BMas120,
                COALESCE(SUM(f.saldototal), 0)      AS TotalVencido,
                COUNT(*)::int                       AS FacturasVencidas,
                MAX(GREATEST(pc.corte - f.fechaemision, 0)) AS DiasMaxVencido,
                COALESCE(cm.bloqueado_cobranza, FALSE) AS Bloqueado,
                COALESCE(cm.no_cortable, FALSE)        AS NoCortable,
                cm.abogado                          AS AbogadoId
            FROM factura f
            JOIN cliente_maestro cm
                ON cm.company_id = f.company_id
               AND cm.maestro_cliente_clave = f.clientecodigo
            CROSS JOIN (SELECT CAST(@FechaCorte AS date) AS corte) pc
            WHERE f.company_id = @CompanyId
              AND f.fechaemision IS NOT NULL
              AND f.fechaemision <= pc.corte
              AND COALESCE(f.estado, 'A') <> 'N'
              AND COALESCE(f.saldototal, 0) <> 0
            """);

        // Antigüedad por fecha de emisión (no hay fechavence poblada); abierto = saldototal<>0;
        // 'N' = factura anulada. Consistente con public.rep_analisis_antiguedad_cobros.

        if (filtro.CicloId.HasValue)
            sb.Append(" AND cm.ciclos_id = @CicloId");

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            sb.Append(" AND (f.clientecodigo ILIKE @Busqueda OR cm.maestro_cliente_nombre ILIKE @Busqueda)");

        sb.Append("""
             GROUP BY f.clientecodigo, cm.maestro_cliente_nombre, cm.ciclos_id,
                      cm.maestro_cliente_indicativo_ruta, cm.bloqueado_cobranza,
                      cm.no_cortable, cm.abogado
            """);

        // Filtro por tramo (HAVING): solo clientes con saldo en el tramo elegido.
        var havingTramo = filtro.Tramo switch
        {
            1 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) <= 30), 0) <> 0",
            2 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) BETWEEN 31 AND 60), 0) <> 0",
            3 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) BETWEEN 61 AND 120), 0) <> 0",
            4 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE GREATEST(pc.corte - f.fechaemision, 0) > 120), 0) <> 0",
            _ => string.Empty
        };
        sb.Append(havingTramo);

        sb.Append(" ORDER BY cm.maestro_cliente_nombre LIMIT 5000");

        var busqueda = string.IsNullOrWhiteSpace(filtro.Busqueda) ? null : $"%{filtro.Busqueda.Trim()}%";

        var rows = await connection.QueryAsync<CarteraVencidaRow>(
            new CommandDefinition(sb.ToString(),
                new
                {
                    CompanyId = companyId,
                    FechaCorte = fechaCorte.ToDateTime(TimeOnly.MinValue),
                    CicloId = filtro.CicloId,
                    Busqueda = busqueda
                },
                cancellationToken: ct));

        return rows
            .Select(r => new CarteraVencidaClienteDto(
                r.Clave, r.Nombre, r.CicloId, r.Ruta,
                r.B0_30, r.B31_60, r.B61_120, r.BMas120,
                r.TotalVencido, r.FacturasVencidas, r.DiasMaxVencido,
                r.Bloqueado, r.NoCortable, r.AbogadoId))
            .ToList();
    }

    public async Task<int> RegistrarAccionLoteAsync(
        RegistrarAccionLoteRequest request, string ejecutadoPor, CancellationToken ct = default)
    {
        var claves = (request.Claves ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct()
            .ToList();

        if (claves.Count == 0)
        {
            return 0;
        }

        var companyId = _currentCompanyService.GetCompanyId();

        var accionNombre = await _context.axl_accion_cobranzas
            .Where(a => a.cod_accion == request.CodAccion)
            .Select(a => a.nombre)
            .FirstOrDefaultAsync(ct) ?? request.CodAccion.ToString();

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var acciones = claves
            .Select(clave => new cln_accion_cobranza
            {
                company_id      = companyId,
                codigocliente   = clave,
                fecha           = ahora,
                accion          = accionNombre,
                observacion     = request.Observacion,
                cod_accion      = request.CodAccion,
                cod_observacion = request.CodObservacion,
                abogado_id      = request.AbogadoId,
                ejecutado_por   = request.EjecutadoPor
            })
            .ToList();

        _context.cln_accion_cobranzas.AddRange(acciones);
        await _context.SaveChangesAsync(ct);

        return acciones.Count;
    }

    public async Task<CartaCobroHdrDto> GenerarCartasCobroAsync(
        GenerarCartasCobroRequest request, string usuario, CancellationToken ct = default)
    {
        var claves = (request.Claves ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct()
            .ToList();

        var companyId = _currentCompanyService.GetCompanyId();
        var correlativo = await GenerarCorrelativoCartaCobroAsync(ct);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var hoy = DateOnly.FromDateTime(ahora);

        var hdr = new cln_carta_cobro_hdr
        {
            company_id       = companyId,
            correlativo      = correlativo,
            fecha_generacion = hoy,
            total_clientes   = claves.Count,
            plazo_horas      = request.PlazoHoras,
            usuario          = usuario,
            usuariocreacion  = usuario,
            fechacreacion    = ahora
        };
        _context.cln_carta_cobro_hdrs.Add(hdr);
        await _context.SaveChangesAsync(ct);

        var detalles = new List<cln_carta_cobro_dtl>();
        foreach (var clave in claves)
        {
            var detalle = await ObtenerSaldosClienteAsync(clave, ct);
            var nombre = detalle.FirstOrDefault()?.ClienteNombre;
            var saldo = detalle.Sum(d => d.SaldoDetalle);

            detalles.Add(new cln_carta_cobro_dtl
            {
                hdr_id        = hdr.id,
                company_id    = companyId,
                cliente_clave = clave,
                nombre_cliente = nombre,
                saldo         = saldo,
                dias_mora     = null
            });
        }

        _context.cln_carta_cobro_dtls.AddRange(detalles);
        await _context.SaveChangesAsync(ct);

        return new CartaCobroHdrDto(hdr.id, hdr.correlativo, hdr.fecha_generacion, hdr.total_clientes, hdr.plazo_horas);
    }

    public async Task<CartaCobroLoteDto?> ObtenerCartaLoteAsync(int hdrId, CancellationToken ct = default)
    {
        var hdr = await _context.cln_carta_cobro_hdrs
            .AsNoTracking()
            .Where(h => h.id == hdrId)
            .Select(h => new CartaCobroHdrDto(h.id, h.correlativo, h.fecha_generacion, h.total_clientes, h.plazo_horas))
            .FirstOrDefaultAsync(ct);

        if (hdr is null) return null;

        var companyId = _currentCompanyService.GetCompanyId();
        var company = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        var empresa = company is null
            ? new CartaEmpresaDto(string.Empty, null, null, null, null, null, null, null)
            : new CartaEmpresaDto(
                company.commercial_name,
                company.legal_name,
                company.tax_id,
                company.address,
                company.phone,
                company.email,
                company.logo is { Length: > 0 } ? Convert.ToBase64String(company.logo) : null,
                company.logo_mime);

        var dtls = await _context.cln_carta_cobro_dtls
            .AsNoTracking()
            .Where(d => d.hdr_id == hdrId)
            .OrderBy(d => d.nombre_cliente)
            .Select(d => new { d.id, d.cliente_clave, d.nombre_cliente, d.saldo, d.dias_mora })
            .ToListAsync(ct);

        var clientes = new List<CartaCobroClienteDto>();
        foreach (var d in dtls)
        {
            var detalle = await ObtenerSaldosClienteAsync(d.cliente_clave, ct);
            var primero = detalle.FirstOrDefault();
            var datos = await ObtenerDatosRequerimientoAsync(d.cliente_clave, companyId, ct);
            var numeroRequerimiento = await _context.cln_carta_cobro_dtls
                .AsNoTracking()
                .CountAsync(x => x.company_id == companyId
                    && x.cliente_clave == d.cliente_clave
                    && x.id <= d.id, ct);

            clientes.Add(new CartaCobroClienteDto(
                d.cliente_clave,
                d.nombre_cliente ?? primero?.ClienteNombre,
                primero?.Direccion,
                primero?.CicloId,
                Ruta: null,
                SaldoTotal: d.saldo ?? detalle.Sum(x => x.SaldoDetalle),
                DiasMora: d.dias_mora,
                Detalle: detalle,
                Medidor: datos?.Medidor,
                Libreta: datos?.Libreta,
                Secuencia: datos?.Secuencia,
                Identidad: datos?.Identidad,
                NumeroRequerimiento: Math.Max(numeroRequerimiento, 1)));
        }

        return new CartaCobroLoteDto(hdr, empresa, clientes);
    }

    private async Task<string> GenerarCorrelativoCartaCobroAsync(CancellationToken ct)
    {
        var ultimo = await _context.cln_carta_cobro_hdrs
            .AsNoTracking()
            .Where(h => h.correlativo != null)
            .OrderByDescending(h => h.correlativo)
            .Select(h => h.correlativo)
            .FirstOrDefaultAsync(ct);

        if (int.TryParse(ultimo, out var numero))
            return (numero + 1).ToString("D6");

        return 1.ToString("D6");
    }

    private sealed class DatosReqRow
    {
        public string? Identidad { get; init; }
        public string? Secuencia { get; init; }
        public string? Libreta { get; init; }
        public string? Medidor { get; init; }
    }

    private async Task<DatosReqRow?> ObtenerDatosRequerimientoAsync(string clave, long companyId, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<DatosReqRow>(new CommandDefinition("""
            SELECT cm.maestro_cliente_identidad        AS Identidad,
                   cm.maestro_cliente_secuencia        AS Secuencia,
                   cm.maestro_cliente_indicativo_ruta  AS Libreta,
                   mm.maestro_medidor_numero           AS Medidor
            FROM cliente_maestro cm
            LEFT JOIN LATERAL (
                SELECT cd.maestro_medidor_id
                FROM cliente_detalle cd
                WHERE cd.maestro_cliente_id = cm.maestro_cliente_id
                  AND cd.maestro_medidor_id IS NOT NULL
                ORDER BY cd.detalle_cliente_id DESC
                LIMIT 1
            ) cd ON TRUE
            LEFT JOIN maestro_medidor mm
                ON mm.maestro_medidor_id = cd.maestro_medidor_id
               AND mm.company_id = cm.company_id
            WHERE cm.company_id = @CompanyId
              AND cm.maestro_cliente_clave = @Clave
            LIMIT 1
            """, new { CompanyId = companyId, Clave = clave }, cancellationToken: ct));
    }

    private sealed class ClienteCobroRow
    {
        public string Clave { get; init; } = string.Empty;
        public string? Nombre { get; init; }
        public string? Direccion { get; init; }
        public int? CicloId { get; init; }
        public string? BarrioCodigo { get; init; }
        public int? CategoriaId { get; init; }
        public string? Ruta { get; init; }
        public decimal SaldoAdeudado { get; init; }
        public int? DiasMora { get; init; }
        public DateOnly? UltimoPago { get; init; }
        public bool Bloqueado { get; init; }
        public bool NoCortable { get; init; }
        public int? AbogadoId { get; init; }
    }

    private sealed class CarteraVencidaRow
    {
        public string Clave { get; init; } = string.Empty;
        public string? Nombre { get; init; }
        public int? CicloId { get; init; }
        public string? Ruta { get; init; }
        public decimal B0_30 { get; init; }
        public decimal B31_60 { get; init; }
        public decimal B61_120 { get; init; }
        public decimal BMas120 { get; init; }
        public decimal TotalVencido { get; init; }
        public int FacturasVencidas { get; init; }
        public int? DiasMaxVencido { get; init; }
        public bool Bloqueado { get; init; }
        public bool NoCortable { get; init; }
        public int? AbogadoId { get; init; }
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
