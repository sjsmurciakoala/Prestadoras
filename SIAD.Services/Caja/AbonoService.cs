using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Caja;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Core.Utilities;
using SIAD.Services.Clientes;

namespace SIAD.Services.Caja;

public class AbonoService : IAbonoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IClientesService _clientesService;

    private readonly Cobros.ICobroService _cobroService;

    public AbonoService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IClientesService clientesService,
        Cobros.ICobroService cobroService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
        _clientesService = clientesService;
        _cobroService = cobroService;
    }

    public async Task<IReadOnlyList<FacturaConSaldoDto>> BuscarFacturasConSaldoAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<FacturaConSaldoDto>();
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var filtro = term.Trim();
        var filtroLike = $"%{filtro}%";
        var isNumero = int.TryParse(filtro, out var numero);

        // Con la cartera migrada (3.9M facturas) los ILIKE '%term%' sobre la
        // tabla tardaban 31.7 s. Regla: contra `factura` solo igualdades
        // indexables; lo difuso (nombre/clave parcial) se resuelve primero
        // contra `cliente_maestro` (26K filas) y de ahí a sus facturas.
        var baseQuery = _context.facturas.AsNoTracking()
            .Where(f => f.company_id == companyId && (f.estado == "A" || f.estado == "B" || f.estado == "C"));

        IQueryable<Core.Entities.factura> filtrada;
        if (isNumero)
        {
            // N° de recibo, clave de cliente o folio, exactos.
            filtrada = baseQuery.Where(f =>
                f.numrecibo == numero || f.clientecodigo == filtro || f.numfactura == filtro);
        }
        else
        {
            var claves = await _context.cliente_maestros.AsNoTracking()
                .Where(c => EF.Functions.ILike(c.maestro_cliente_nombre, filtroLike)
                            || EF.Functions.ILike(c.maestro_cliente_clave, filtroLike))
                .Select(c => c.maestro_cliente_clave)
                .Take(25)
                .ToListAsync(ct);

            filtrada = baseQuery.Where(f =>
                f.numfactura == filtro || (f.clientecodigo != null && claves.Contains(f.clientecodigo)));
        }

        var query = from f in filtrada
                    join c in _context.cliente_maestros.AsNoTracking()
                        on f.clientecodigo equals c.maestro_cliente_clave into clientes
                    from c in clientes.DefaultIfEmpty()
                    orderby f.fechaemision descending, f.numrecibo descending
                    select new
                    {
                        FacturaId = f.id,
                        NumFactura = f.numfactura ?? f.numrecibo.ToString(),
                        NumRecibo = f.numrecibo,
                        ClienteClave = f.clientecodigo ?? string.Empty,
                        ClienteNombre = c != null ? c.maestro_cliente_nombre : string.Empty,
                        FechaEmision = f.fechaemision,
                        SaldoTotal = f.saldototal ?? 0m,
                        f.estado
                    };

        var items = await query.Take(40).ToListAsync(ct);

        // Saldos en una sola consulta agrupada (antes: una por factura).
        var facturaIds = items.Select(x => x.FacturaId).ToList();
        var saldos = await _context.factura_detalles.AsNoTracking()
            .Where(d => d.factura_id != null && facturaIds.Contains(d.factura_id.Value))
            .GroupBy(d => d.factura_id!.Value)
            .Select(g => new { FacturaId = g.Key, Saldo = g.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m) })
            .ToDictionaryAsync(x => x.FacturaId, x => x.Saldo, ct);

        var response = new List<FacturaConSaldoDto>();

        foreach (var x in items)
        {
            var saldoPendiente = saldos.GetValueOrDefault(x.FacturaId);

            // Mostrar si tiene saldo pendiente o si es estado 'B' / 'A'
            if (saldoPendiente > 0 || x.estado == "A" || x.estado == "B")
            {
                response.Add(new FacturaConSaldoDto
                {
                    FacturaId = x.FacturaId,
                    NumFactura = x.NumFactura,
                    NumRecibo = x.NumRecibo,
                    ClienteClave = x.ClienteClave,
                    ClienteNombre = x.ClienteNombre,
                    FechaEmision = x.FechaEmision.HasValue ? x.FechaEmision.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                    SaldoTotal = x.SaldoTotal,
                    SaldoPendiente = saldoPendiente > 0 ? saldoPendiente : x.SaldoTotal,
                    Estado = x.estado ?? "A"
                });
            }
        }

        return response;
    }

    public async Task<IReadOnlyList<FacturaConSaldoDto>> ListarFacturasPendientesPorClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<FacturaConSaldoDto>();
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var clave = clienteClave.Trim();

        var query = from f in _context.facturas.AsNoTracking()
                    join c in _context.cliente_maestros.AsNoTracking()
                        on f.clientecodigo equals c.maestro_cliente_clave into clientes
                    from c in clientes.DefaultIfEmpty()
                    where f.company_id == companyId
                          && f.clientecodigo == clave
                          && (f.estado == "A" || f.estado == "B")
                    orderby f.fechaemision descending, f.numrecibo descending
                    select new
                    {
                        FacturaId = f.id,
                        NumFactura = f.numfactura ?? f.numrecibo.ToString(),
                        NumRecibo = f.numrecibo,
                        ClienteClave = f.clientecodigo ?? string.Empty,
                        ClienteNombre = c != null ? c.maestro_cliente_nombre : string.Empty,
                        FechaEmision = f.fechaemision,
                        SaldoTotal = f.saldototal ?? 0m,
                        f.estado
                    };

        var items = await query.Take(200).ToListAsync(ct);
        var response = new List<FacturaConSaldoDto>();

        foreach (var x in items)
        {
            var saldoPendiente = await _context.factura_detalles
                .Where(d => d.factura_id == x.FacturaId)
                .SumAsync(d => d.montovalor_saldo ?? d.montovalor ?? 0m, ct);

            response.Add(new FacturaConSaldoDto
            {
                FacturaId = x.FacturaId,
                NumFactura = x.NumFactura,
                NumRecibo = x.NumRecibo,
                ClienteClave = x.ClienteClave,
                ClienteNombre = x.ClienteNombre,
                FechaEmision = x.FechaEmision.HasValue ? x.FechaEmision.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                SaldoTotal = x.SaldoTotal,
                SaldoPendiente = saldoPendiente > 0 ? saldoPendiente : x.SaldoTotal,
                Estado = x.estado ?? "A"
            });
        }

        // F6 (2026-07-29): las cuotas vivas de planes ACTIVOS también son
        // documentos cobrables (documento_tipo = 2 del motor único).
        var cuotas = await (
            from d in _context.cln_plan_pago_dtls.AsNoTracking()
            join h in _context.cln_plan_pago_hdrs.AsNoTracking() on d.idhdr equals h.id
            join c in _context.cliente_maestros.AsNoTracking() on h.clienteid equals (int?)c.maestro_cliente_id
            where c.company_id == companyId
                  && c.maestro_cliente_clave == clave
                  && h.estado_id == EstadoPlan.Activo
                  && (d.estado_id == EstadoDocumentoComercial.Activa
                      || d.estado_id == EstadoDocumentoComercial.ParcialmenteAbonada)
                  && d.saldo_cuota > 0
            orderby d.fechacuota
            select new
            {
                d.id,
                d.mes,
                d.fechacuota,
                d.valorcuota,
                d.saldo_cuota,
                d.estado_id,
                h.correlativo,
                ClienteNombre = c.maestro_cliente_nombre
            })
            .ToListAsync(ct);

        foreach (var q in cuotas)
        {
            response.Add(new FacturaConSaldoDto
            {
                FacturaId = 0,
                DocumentoTipo = DocumentoCobroTipo.CuotaPlan,
                PlanCuotaId = q.id,
                NumFactura = q.mes == 0
                    ? $"Prima plan {q.correlativo}"
                    : $"Cuota {q.mes} plan {q.correlativo}",
                NumRecibo = 0,
                ClienteClave = clave,
                ClienteNombre = q.ClienteNombre ?? string.Empty,
                FechaEmision = q.fechacuota ?? DateTime.MinValue,
                SaldoTotal = q.valorcuota ?? 0m,
                SaldoPendiente = q.saldo_cuota,
                Estado = q.estado_id == EstadoDocumentoComercial.ParcialmenteAbonada ? "B" : "A"
            });
        }

        // F7 H2b: las notas de débito vivas también son documentos cobrables
        // (documento_tipo = 3 del motor).
        var notas = await (
            from n in _context.adm_nota_debitos.AsNoTracking()
            join c in _context.cliente_maestros.AsNoTracking() on n.cliente_id equals (long?)c.maestro_cliente_id
            where c.company_id == companyId
                  && c.maestro_cliente_clave == clave
                  && n.estado_id != 3
                  && n.saldo_pendiente > 0
            orderby n.fecha_emision
            select new
            {
                n.nota_debito_id,
                n.numero_documento,
                n.fecha_emision,
                n.total_nota,
                n.saldo_pendiente,
                ClienteNombre = c.maestro_cliente_nombre
            })
            .ToListAsync(ct);

        foreach (var n in notas)
        {
            response.Add(new FacturaConSaldoDto
            {
                FacturaId = 0,
                DocumentoTipo = DocumentoCobroTipo.NotaDebito,
                NotaDebitoId = n.nota_debito_id,
                NumFactura = $"ND {n.numero_documento}",
                NumRecibo = 0,
                ClienteClave = clave,
                ClienteNombre = n.ClienteNombre ?? string.Empty,
                FechaEmision = n.fecha_emision.ToLocalTime(),
                SaldoTotal = n.total_nota,
                SaldoPendiente = n.saldo_pendiente,
                Estado = n.saldo_pendiente < n.total_nota ? "B" : "A"
            });
        }

        return response;
    }

    // ------------------------------------------------------------------------
    // FACHADA (unificación cobranza F2b): el registro del abono delega en el
    // motor único de cobro (CobroService). Se conservan las validaciones
    // tempranas con sus mensajes exactos; la aplicación, el dual-write, la
    // contabilidad (REC único) y el kardex viven en el motor. Cambio de regla
    // aprobado en el plan §4: el abono ahora exige sesión de caja ABIERTA.
    // ------------------------------------------------------------------------
    public async Task<ResponseModelDto> RegistrarAbonoAsync(AbonoCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Monto <= 0)
        {
            return ResponseModelDto.Fail("El monto del abono debe ser mayor a cero.");
        }

        var companyId = _currentCompanyService.GetCompanyId();

        var factura = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.company_id == companyId && (f.numfactura == dto.NumFactura || f.numrecibo.ToString() == dto.NumFactura), ct);

        if (factura is null)
        {
            return ResponseModelDto.Fail("No se encontró la factura indicada.");
        }

        var detalles = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == factura.id)
            .ToListAsync(ct);

        if (detalles.Count == 0)
        {
            return ResponseModelDto.Fail("La factura no tiene detalles asociados.");
        }

        var saldoDetalles = detalles.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m);
        var saldoPendiente = saldoDetalles > 0 ? saldoDetalles : (factura.saldototal ?? 0m);
        if (saldoPendiente <= 0)
        {
            return ResponseModelDto.Fail("La factura seleccionada no tiene saldo pendiente.");
        }

        if (dto.Monto > saldoPendiente)
        {
            return ResponseModelDto.Fail($"El monto del abono ({dto.Monto:N2}) no puede exceder el saldo pendiente de la factura ({saldoPendiente:N2}).");
        }

        var cobro = await _cobroService.RegistrarCobroAsync(new SIAD.Core.DTOs.Cobros.CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = dto.ClienteClave,
            FormaPago = dto.FormaPago,
            BancoCuentaId = dto.BancoCuentaId,
            Banco = dto.Banco,
            FechaPago = dto.FechaPago,
            Usuario = dto.Usuario,
            ReciboPendienteId = dto.ReciboPendienteId,
            TipoLegacy = "202",
            TipoPartidaLegacy = "002",
            Aplicaciones =
            [
                new SIAD.Core.DTOs.Cobros.CobroAplicacionDto
                {
                    DocumentoTipo = DocumentoCobroTipo.Factura,
                    FacturaId = factura.id,
                    Monto = dto.Monto
                }
            ]
        }, ct);

        if (!cobro.Success)
        {
            return cobro;
        }

        var resultado = (SIAD.Core.DTOs.Cobros.CobroResultadoDto)cobro.Data!;
        return ResponseModelDto.Ok(new AbonoResponseDto
        {
            NumFactura = factura.numfactura ?? factura.numrecibo.ToString(),
            NumRecibo = factura.numrecibo,
            MontoAbonado = dto.Monto,
            NuevoSaldo = resultado.Aplicaciones.Count > 0 ? resultado.Aplicaciones[0].SaldoRestante : 0m,
            PolizaId = resultado.PolizaId,
            PagoId = resultado.PagoId
        }, "Abono registrado correctamente.");
    }

    public async Task<IReadOnlyList<ArqueoDto>> ListarAbonosDelDiaAsync(string? usuario, DateTime? fecha, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var targetFecha = fecha?.Date ?? DateTime.UtcNow.Date;
        var targetDateOnly = DateOnly.FromDateTime(targetFecha);

        // Excluye los pagos del canal bancario (F8, marker WSBANCO:): no son abonos
        // de ventanilla y no deben sumar al arqueo de caja ni ofrecerse para reverso
        // desde esta pantalla (su reverso va por sp_ban_ws_reversar).
        var query = _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.tipotransaccion == "202" && t.fecha_docu == targetDateOnly && t.estado != "P")
            .Where(t => t.trans_aplicar == null || !t.trans_aplicar.StartsWith("WSBANCO:"));

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            query = query.Where(t => t.usuario == usuario.Trim());
        }

        var data = await (from p in query
                          join f in _context.facturas on p.recibo equals (decimal?)f.numrecibo into facturaJoin
                          from f in facturaJoin.DefaultIfEmpty()
                          select new
                          {
                              p.ide,
                              p.fecha_docu,
                              p.recibo,
                              NumFactura = p.docufuente2 ?? f.numfactura,
                              p.cliente_clave,
                              p.banco,
                              p.usuario,
                              p.estado,
                              Monto = p.creditos ?? 0m
                          }).ToListAsync(ct);

        return data.Select(item => new ArqueoDto
        {
            Id = item.ide,
            Fecha = item.fecha_docu.HasValue ? item.fecha_docu.Value.ToDateTime(TimeOnly.MinValue) : targetFecha,
            NumFactura = string.IsNullOrWhiteSpace(item.NumFactura) ? item.recibo?.ToString("F0") ?? string.Empty : item.NumFactura.Trim(),
            ClienteClave = item.cliente_clave ?? string.Empty,
            Banco = item.banco,
            Usuario = item.usuario,
            Estado = item.estado == "A" ? "ANULADO" : "POSTED",
            Monto = item.Monto,
            RowKey = item.ide.ToString()
        }).ToList();
    }

    public async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        // Firma de 2 args (fix vigencia 2026-07-16): filtra company_id y suma los
        // movimientos vigentes. La de 1 arg leia el saldo corrido del ultimo movimiento
        // con estado 'A' e ignoraba los abonos vigentes 'C', por lo que cada abono se
        // grababa restando del mismo saldo base (sin encadenar) y corrompia la columna.
        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@CompanyId, @ClienteClave)";
        var saldo = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(sql, new { CompanyId = companyId, ClienteClave = clienteClave }, cancellationToken: ct));

        return saldo ?? 0m;
    }

    private async Task<string> ObtenerPeriodoActualCodigoAsync(CancellationToken ct)
    {
        // F7: el período comercial vive en adm_periodo_comercial (tenant-scoped);
        // historialmes queda como espejo de solo lectura.
        var periodo = await _context.adm_periodo_comercials
            .AsNoTracking()
            .Where(p => p.status_id == EstadoPeriodoComercial.Abierto)
            .OrderByDescending(p => p.anio)
            .ThenByDescending(p => p.mes)
            .Select(p => new { p.anio, p.mes })
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
        {
            return DateTime.UtcNow.ToString("yyyyMM");
        }

        return $"{periodo.anio:D4}{periodo.mes:D2}";
    }

    private async Task<string?> ResolverBancoCodigoAsync(long? bancoCuentaId, string? bancoFallback, CancellationToken ct)
    {
        var bancoNormalizado = string.IsNullOrWhiteSpace(bancoFallback) ? null : bancoFallback.Trim();
        if (!bancoCuentaId.HasValue || bancoCuentaId.Value <= 0)
        {
            return bancoNormalizado;
        }

        var cuenta = await _context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.banco_cuenta_id == bancoCuentaId.Value && c.activo)
            .Select(c => new
            {
                c.code,
                BancoCode = c.ban_banco != null ? c.ban_banco.code : null
            })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null) return bancoNormalizado;
        return cuenta.BancoCode?.Trim() ?? cuenta.code?.Trim() ?? bancoNormalizado;
    }

    /// <summary>
    /// F7 H2c (2026-07-30): el recibo se arma desde el DOCUMENTO del motor
    /// (adm_pago + aplicaciones), no desde la fila espejo de
    /// transaccion_abonado. Para el papel "pendiente de pago" (aún no cobrado)
    /// existe <see cref="GenerarDatosReciboPendienteAsync"/>.
    /// </summary>
    public async Task<ReciboAbonoDto?> GenerarDatosReciboAsync(long pagoId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        var pago = await _context.adm_pagos
            .AsNoTracking()
            .Include(p => p.aplicaciones)
            .FirstOrDefaultAsync(p => p.company_id == companyId && p.pago_id == pagoId, ct);

        if (pago is null)
            return null;

        // Factura principal del cobro (la de mayor monto aplicado); si el pago
        // fue solo a cuotas/ND se toma la última factura del cliente para los
        // datos de encabezado (período, RTN).
        var facturaId = pago.aplicaciones
            .Where(a => a.factura_id.HasValue)
            .GroupBy(a => a.factura_id!.Value)
            .OrderByDescending(g => g.Sum(a => a.monto_aplicado))
            .Select(g => (int?)g.Key)
            .FirstOrDefault();

        var factura = facturaId.HasValue
            ? await _context.facturas.AsNoTracking()
                .FirstOrDefaultAsync(f => f.company_id == companyId && f.id == facturaId.Value, ct)
            : await _context.facturas.AsNoTracking()
                .Where(f => f.company_id == companyId && f.clientecodigo == pago.cliente_clave)
                .OrderByDescending(f => f.fechaemision).ThenByDescending(f => f.numrecibo)
                .FirstOrDefaultAsync(ct);

        if (factura is null)
            return null;

        var numRecibo = factura.numrecibo;
        var transaccion = new transaccion_abonado
        {
            cliente_clave = pago.cliente_clave,
            creditos = pago.monto_total,
            usuario = pago.usuario,
            fecha_docu = pago.fecha,
            recibo = numRecibo
        };
        var transaccionId = (int)pago.pago_id;

        var detalles = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == factura.id)
            .OrderBy(d => d.id)
            .ToListAsync(ct);

        var clienteConDetalle = await _context.cliente_maestros
            .AsNoTracking()
            .Include(m => m.cliente_detalles)
            .FirstOrDefaultAsync(c => c.maestro_cliente_clave == transaccion.cliente_clave, ct);

        var company = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        var lineas = detalles.Select(d => new ReciboAbonoLineaDto
        {
            Descripcion = RotuloLineaRecibo(d.tiposervicio, d.descripcion),
            Moneda = "L.",
            Monto = d.montovalor ?? 0m
        }).ToList();

        // El total del recibo es el monto del abono, no el total de la factura
        var total = transaccion.creditos ?? lineas.Sum(l => l.Monto);
        var direccion = clienteConDetalle?.cliente_detalles.FirstOrDefault()?.detalle_cliente_direccion ?? string.Empty;

        var esPendiente = false;   // este recibo SIEMPRE es de un cobro aplicado

        // Desglose del saldo del cliente (deuda / % de distribución / saldo), el
        // mismo que muestra el estado de cuenta. Refleja el estado ACTUAL, ya con
        // este abono aplicado cuando el recibo se imprime tras el cobro.
        var desgloseSaldo = string.IsNullOrWhiteSpace(transaccion.cliente_clave)
            ? Array.Empty<SIAD.Core.DTOs.Clientes.SaldoServicioDto>()
            : await _clientesService.GetDesglosePorServicioAsync(transaccion.cliente_clave, ct);

        return new ReciboAbonoDto
        {
            EmpresaNombre = company?.commercial_name ?? string.Empty,
            EmpresaLogo = company?.logo,
            EmpresaLogoMime = company?.logo_mime,

            NumRecibo = numRecibo,
            NumFactura = factura.numfactura ?? numRecibo.ToString(),
            Periodo = factura.periodo ?? string.Empty,
            FechaEmision = factura.fechaemision?.ToString("dd/MM/yy") ?? string.Empty,
            RtnCliente = factura.rtn ?? "0",
            CuentaNo = transaccion.cliente_clave ?? string.Empty,
            Propietario = clienteConDetalle?.maestro_cliente_nombre ?? string.Empty,
            Direccion = direccion,

            Lineas = lineas,
            Total = total,
            TotalEnLetras = NumerosALetras.Convertir(total),

            Cajero = esPendiente ? "PENDIENTE DE PAGO" : (transaccion.usuario ?? string.Empty),
            FechaPago = esPendiente ? string.Empty : (transaccion.fecha_docu?.ToString("dd/MM/yy") ?? string.Empty),
            NumeroTransaccion = transaccionId,
            GeneradoPor = transaccion.usuario ?? string.Empty,
            EsPendiente = esPendiente,
            DesgloseSaldo = desgloseSaldo.ToList()
        };
    }

    /// <summary>
    /// F7 H2c: recibo del papel "para pagar en banco" AÚN NO COBRADO, desde
    /// adm_recibo_banco_pendiente (antes salía de la fila espejo 202/'P').
    /// </summary>
    public async Task<ReciboAbonoDto?> GenerarDatosReciboPendienteAsync(long pendienteId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        var pendiente = await _context.adm_recibo_banco_pendientes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.recibo_pendiente_id == pendienteId, ct);
        if (pendiente is null)
            return null;

        var factura = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.company_id == companyId && f.id == pendiente.factura_id, ct);
        if (factura is null)
            return null;

        var detalles = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == factura.id)
            .OrderBy(d => d.id)
            .ToListAsync(ct);

        var clienteConDetalle = await _context.cliente_maestros
            .AsNoTracking()
            .Include(m => m.cliente_detalles)
            .FirstOrDefaultAsync(c => c.maestro_cliente_clave == pendiente.cliente_clave, ct);

        var company = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        var desgloseSaldo = await _clientesService.GetDesglosePorServicioAsync(pendiente.cliente_clave, ct);

        return new ReciboAbonoDto
        {
            EmpresaNombre = company?.commercial_name ?? string.Empty,
            EmpresaLogo = company?.logo,
            EmpresaLogoMime = company?.logo_mime,

            NumRecibo = pendiente.numrecibo,
            NumFactura = factura.numfactura ?? pendiente.numrecibo.ToString(),
            Periodo = factura.periodo ?? string.Empty,
            FechaEmision = factura.fechaemision?.ToString("dd/MM/yy") ?? string.Empty,
            RtnCliente = factura.rtn ?? "0",
            CuentaNo = pendiente.cliente_clave,
            Propietario = clienteConDetalle?.maestro_cliente_nombre ?? string.Empty,
            Direccion = clienteConDetalle?.cliente_detalles.FirstOrDefault()?.detalle_cliente_direccion ?? string.Empty,

            Lineas = detalles.Select(d => new ReciboAbonoLineaDto
            {
                Descripcion = RotuloLineaRecibo(d.tiposervicio, d.descripcion),
                Moneda = "L.",
                Monto = d.montovalor ?? 0m
            }).ToList(),
            Total = pendiente.monto,
            TotalEnLetras = NumerosALetras.Convertir(pendiente.monto),

            Cajero = "PENDIENTE DE PAGO",
            FechaPago = string.Empty,
            NumeroTransaccion = (int)pendiente.recibo_pendiente_id,
            GeneradoPor = pendiente.generado_por,
            EsPendiente = true,
            DesgloseSaldo = desgloseSaldo.ToList()
        };
    }

    public async Task<IReadOnlyList<AbonoHistorialItemDto>> ListarHistorialPorClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
            return Array.Empty<AbonoHistorialItemDto>();

        var companyId = _currentCompanyService.GetCompanyId();
        var clave = clienteClave.Trim();

        var transacciones = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.cliente_clave == clave && t.tipotransaccion == "202" && t.estado != "P")
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Take(100)
            .ToListAsync(ct);

        if (transacciones.Count == 0)
            return Array.Empty<AbonoHistorialItemDto>();

        var numRecibos = transacciones
            .Where(t => t.recibo.HasValue)
            .Select(t => (int)t.recibo!.Value)
            .Distinct()
            .ToList();

        var facturasMap = await _context.facturas
            .AsNoTracking()
            .Where(f => f.company_id == companyId && numRecibos.Contains(f.numrecibo))
            .ToDictionaryAsync(f => f.numrecibo, ct);

        var facturaIds = facturasMap.Values.Select(f => f.id).ToList();

        var saldosPorFacturaId = await _context.factura_detalles
            .Where(d => d.factura_id != null && facturaIds.Contains(d.factura_id.Value))
            .GroupBy(d => d.factura_id!.Value)
            .Select(g => new { FacturaId = g.Key, Saldo = g.Sum(d => d.montovalor_saldo ?? 0m) })
            .ToDictionaryAsync(x => x.FacturaId, x => x.Saldo, ct);

        return transacciones.Select(t =>
        {
            var numRecibo = (int)(t.recibo ?? 0);
            facturasMap.TryGetValue(numRecibo, out var factura);
            saldosPorFacturaId.TryGetValue(factura?.id ?? 0, out var saldoRestante);

            return new AbonoHistorialItemDto
            {
                NumFactura = factura?.numfactura ?? numRecibo.ToString(),
                NumRecibo = numRecibo,
                FechaPago = t.fecha_docu?.ToString("dd/MM/yyyy") ?? string.Empty,
                MontoAbonado = t.creditos ?? 0m,
                Cajero = t.usuario ?? string.Empty,
                EstadoFactura = factura?.estado switch
                {
                    "A" => "Abierta",
                    "B" => "Parcial",
                    "C" => "Cobrada",
                    _ => "—"
                },
                SaldoRestante = saldoRestante
            };
        }).ToList();
    }

    public async Task<ResponseModelDto> GenerarReciboPendienteAsync(GenerarReciboDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Monto <= 0)
            return ResponseModelDto.Fail("El monto debe ser mayor a cero.");

        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();

        var factura = await _context.facturas
            .FirstOrDefaultAsync(f => f.company_id == companyId
                && (f.numfactura == dto.NumFactura || f.numrecibo.ToString() == dto.NumFactura), ct);

        if (factura is null)
            return ResponseModelDto.Fail("No se encontró la factura indicada.");

        if (factura.estado == "C")
            return ResponseModelDto.Fail("La factura ya está completamente pagada.");

        var saldoDetalles = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .SumAsync(d => d.montovalor_saldo ?? d.montovalor ?? 0m, ct);

        var saldoPendiente = saldoDetalles > 0 ? saldoDetalles : (factura.saldototal ?? 0m);

        if (saldoPendiente <= 0)
            return ResponseModelDto.Fail("La factura no tiene saldo pendiente.");

        if (dto.Monto > saldoPendiente)
            return ResponseModelDto.Fail($"El monto ({dto.Monto:N2}) excede el saldo pendiente ({saldoPendiente:N2}).");

        // Control de recibos (revisión 2026-07-27): los pendientes vigentes de la
        // factura se descuentan del disponible — no se puede "recibir" dos veces
        // el mismo saldo en papeles distintos. F7 H1: la fuente de verdad es
        // adm_recibo_banco_pendiente.
        var pendientesVigentes = await _context.adm_recibo_banco_pendientes
            .AsNoTracking()
            .Where(r => r.factura_id == factura.id && r.estado_id == 2)
            .SumAsync(r => r.monto, ct);

        var disponible = saldoPendiente - pendientesVigentes;
        if (dto.Monto > disponible)
            return ResponseModelDto.Fail(
                $"Ya existen recibos pendientes por {pendientesVigentes:N2} sobre esta factura; " +
                $"el monto disponible para nuevos recibos es {Math.Max(disponible, 0m):N2}. " +
                "Anule un recibo pendiente si necesita re-emitirlo.");

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var periodo = await ObtenerPeriodoActualCodigoAsync(ct);

        var clienteInfo = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == dto.ClienteClave)
            .Select(c => new
            {
                c.ciclos_id,
                c.maestro_cliente_indicativo_ruta,
                c.maestro_cliente_secuencia,
                c.maestro_cliente_tiene_medidor
            })
            .FirstOrDefaultAsync(ct);

        // F7 H2c: el papel "para banco" vive SOLO en adm_recibo_banco_pendiente
        // (se acabó la fila espejo 202/'P').
        var pendiente = new adm_recibo_banco_pendiente
        {
            cliente_clave = dto.ClienteClave.Trim(),
            factura_id = factura.id,
            numrecibo = factura.numrecibo,
            monto = dto.Monto,
            estado_id = 2,
            descripcion = $"Recibo pendiente de pago - Factura: {factura.numfactura ?? factura.numrecibo.ToString()}",
            generado_por = usuario
        };
        _context.adm_recibo_banco_pendientes.Add(pendiente);
        await _context.SaveChangesAsync(ct);

        return ResponseModelDto.Ok(new GenerarReciboResponseDto
        {
            PendienteId = pendiente.recibo_pendiente_id,
            NumFactura = factura.numfactura ?? factura.numrecibo.ToString()
        }, "Recibo generado. El cliente puede presentarlo en ventanilla o banco.");
    }

    public async Task<IReadOnlyList<ReciboPendienteDto>> ListarRecibosPendientesPorFacturaAsync(string numFactura, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(numFactura))
            return Array.Empty<ReciboPendienteDto>();

        var companyId = _currentCompanyService.GetCompanyId();

        var factura = await _context.facturas
            .AsNoTracking()
            .Where(f => f.company_id == companyId && (f.numfactura == numFactura || f.numrecibo.ToString() == numFactura))
            .Select(f => new { f.numrecibo, f.numfactura })
            .FirstOrDefaultAsync(ct);

        if (factura is null)
            return Array.Empty<ReciboPendienteDto>();

        var numRecibo = factura.numrecibo;

        // F7 H1: fuente de verdad adm_recibo_banco_pendiente.
        var filas = await _context.adm_recibo_banco_pendientes
            .AsNoTracking()
            .Where(r => r.numrecibo == numRecibo && r.estado_id == 2)
            .OrderByDescending(r => r.generado_en)
            .ToListAsync(ct);

        return filas.Select(r => new ReciboPendienteDto
        {
            PendienteId = r.recibo_pendiente_id,
            NumFactura = factura.numfactura ?? numRecibo.ToString(),
            NumRecibo = numRecibo,
            Monto = r.monto,
            FechaGenerado = r.generado_en.ToLocalTime().ToString("dd/MM/yyyy"),
            Operador = r.generado_por
        }).ToList();
    }

    public async Task<IReadOnlyList<ReciboPendienteDto>> ListarRecibosPendientesPorClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
            return Array.Empty<ReciboPendienteDto>();

        var companyId = _currentCompanyService.GetCompanyId();
        var clave = clienteClave.Trim();

        // F7 H1: fuente de verdad adm_recibo_banco_pendiente (join directo a la
        // factura por id — se acabó el join decimal↔int del modelo legacy).
        var pendientes = await (
            from r in _context.adm_recibo_banco_pendientes.AsNoTracking()
            join f in _context.facturas.AsNoTracking() on r.factura_id equals f.id
            where r.cliente_clave == clave && r.estado_id == 2
            orderby r.generado_en descending
            select new
            {
                r.recibo_pendiente_id,
                r.numrecibo,
                r.monto,
                r.generado_en,
                r.generado_por,
                f.numfactura
            })
            .ToListAsync(ct);

        return pendientes.Select(p => new ReciboPendienteDto
        {
            PendienteId = p.recibo_pendiente_id,
            NumRecibo = p.numrecibo,
            NumFactura = string.IsNullOrWhiteSpace(p.numfactura) ? p.numrecibo.ToString() : p.numfactura,
            Monto = p.monto,
            FechaGenerado = p.generado_en.ToLocalTime().ToString("dd/MM/yyyy"),
            Operador = p.generado_por
        }).ToList();
    }

    public async Task<IReadOnlyList<AbonoHistorialItemDto>> ListarAbonosPorFacturaAsync(string numFactura, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(numFactura))
            return Array.Empty<AbonoHistorialItemDto>();

        var companyId = _currentCompanyService.GetCompanyId();

        var factura = await _context.facturas
            .AsNoTracking()
            .Where(f => f.company_id == companyId && (f.numfactura == numFactura || f.numrecibo.ToString() == numFactura))
            .Select(f => new { f.id, f.numrecibo, f.numfactura, f.estado })
            .FirstOrDefaultAsync(ct);

        if (factura is null)
            return Array.Empty<AbonoHistorialItemDto>();

        var numRecibo = factura.numrecibo;

        var transacciones = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId
                && t.recibo == numRecibo
                && t.tipotransaccion == "202"
                && t.estado != "P")
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .ToListAsync(ct);

        if (transacciones.Count == 0)
            return Array.Empty<AbonoHistorialItemDto>();

        var saldoRestante = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .SumAsync(d => d.montovalor_saldo ?? d.montovalor ?? 0m, ct);

        var estadoFacturaStr = factura.estado switch
        {
            "C" => "Cobrada",
            "B" => "Parcial",
            _ => "Abierta"
        };

        return transacciones.Select(t => new AbonoHistorialItemDto
        {
            NumFactura = factura.numfactura ?? numRecibo.ToString(),
            NumRecibo = numRecibo,
            FechaPago = t.fecha_docu?.ToString("dd/MM/yyyy") ?? string.Empty,
            MontoAbonado = t.creditos ?? 0m,
            Cajero = t.usuario ?? string.Empty,
            EstadoFactura = estadoFacturaStr,
            SaldoRestante = saldoRestante
        }).ToList();
    }

    public async Task<ResponseModelDto> AnularReciboPendienteAsync(AnularReciboPendienteDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.PendienteId <= 0)
            return ResponseModelDto.Fail("Debe indicar el recibo pendiente a anular.");

        var companyId = _currentCompanyService.GetCompanyId();

        // F7 H1: fuente de verdad adm_recibo_banco_pendiente, siempre por PendienteId.
        var pendiente = await _context.adm_recibo_banco_pendientes
            .FirstOrDefaultAsync(r => r.recibo_pendiente_id == dto.PendienteId && r.estado_id == 2, ct);

        if (pendiente is null)
            return ResponseModelDto.Fail("El recibo pendiente no existe o ya fue procesado/anulado.");

        pendiente.estado_id = 3; // ANULADO
        pendiente.anulado_por = dto.Usuario;
        pendiente.anulado_en = DateTime.UtcNow;
        pendiente.motivo_anulacion = dto.Motivo;

        // Solo pendientes PRE-corte tienen fila espejo 'P' en transaccion_abonado
        // (congelada en F7 H4): se marca 'A' para que la consulta de abonos
        // especiales no los siga mostrando como "No aplicado". Los pendientes
        // nuevos nacen sin espejo y este bloque no aplica.
        if (pendiente.transaccion_abonado_ide.HasValue)
        {
            var transaccion = await _context.transaccion_abonados
                .FirstOrDefaultAsync(t => t.company_id == companyId
                    && t.ide == pendiente.transaccion_abonado_ide.Value && t.estado == "P", ct);
            if (transaccion is not null)
            {
                transaccion.estado = "A";
                transaccion.motivo = dto.Motivo;
                transaccion.descripcion = $"ANULADO: {dto.Motivo} — {transaccion.descripcion}";
                transaccion.usuario = dto.Usuario;
            }
        }

        await _context.SaveChangesAsync(ct);
        return ResponseModelDto.Ok("Recibo pendiente anulado correctamente.");
    }

    // ───────────────────────── Consulta de abonos especiales ─────────────────────────

    public async Task<PagedResult<AbonoEspecialListItemDto>> ListarAbonosEspecialesAsync(AbonoEspecialFiltroDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var companyId = _currentCompanyService.GetCompanyId();

        var baseQuery = BuildAbonosEspecialesQuery(companyId, filtro);

        var total = await baseQuery.CountAsync(ct);
        if (total == 0)
            return new PagedResult<AbonoEspecialListItemDto>(Array.Empty<AbonoEspecialListItemDto>(), 0);

        var skip = Math.Max(filtro.Skip, 0);
        var take = filtro.Take <= 0 ? 15 : filtro.Take;

        // Página de transacciones; los datos de cliente/factura se resuelven por
        // lote (mismo patrón que ListarHistorialPorClienteAsync) para evitar joins
        // con cast decimal→int que EF no traduce.
        var pageTx = await OrdenarAbonosEspeciales(baseQuery, filtro)
            .Skip(skip)
            .Take(take)
            .Select(t => new
            {
                t.ide,
                t.cliente_clave,
                t.recibo,
                t.creditos,
                t.fecha_docu,
                t.periodo,
                t.usuario,
                t.banco,
                t.descripcion,
                t.estado
            })
            .ToListAsync(ct);

        var claves = pageTx
            .Where(t => !string.IsNullOrWhiteSpace(t.cliente_clave))
            .Select(t => t.cliente_clave!)
            .Distinct()
            .ToList();

        var clientesMap = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => claves.Contains(c.maestro_cliente_clave))
            .Select(c => new { c.maestro_cliente_clave, c.maestro_cliente_id, c.maestro_cliente_nombre })
            .ToDictionaryAsync(c => c.maestro_cliente_clave, ct);

        var recibos = pageTx
            .Where(t => t.recibo.HasValue)
            .Select(t => (int)t.recibo!.Value)
            .Distinct()
            .ToList();

        var facturasMap = await _context.facturas
            .AsNoTracking()
            .Where(f => recibos.Contains(f.numrecibo))
            .Select(f => new { f.numrecibo, f.numfactura })
            .ToDictionaryAsync(f => f.numrecibo, f => f.numfactura, ct);

        // La tabla legacy está congelada (F7 H4): las acciones de la vista van
        // contra el modelo nuevo. Se resuelven por lote el pago del motor
        // (imprimir recibo) y el pendiente vigente (cobrar) enlazados por
        // transaccion_abonado_ide.
        var ides = pageTx.Select(t => t.ide).ToList();

        var pagosMap = (await _context.adm_pagos
            .AsNoTracking()
            .Where(p => p.transaccion_abonado_ide != null && ides.Contains(p.transaccion_abonado_ide.Value))
            .Select(p => new { Ide = p.transaccion_abonado_ide!.Value, p.pago_id })
            .ToListAsync(ct))
            .GroupBy(p => p.Ide)
            .ToDictionary(g => g.Key, g => g.First().pago_id);

        var pendientesMap = (await _context.adm_recibo_banco_pendientes
            .AsNoTracking()
            .Where(r => r.estado_id == 2 && r.transaccion_abonado_ide != null && ides.Contains(r.transaccion_abonado_ide.Value))
            .Select(r => new { Ide = r.transaccion_abonado_ide!.Value, r.recibo_pendiente_id })
            .ToListAsync(ct))
            .GroupBy(r => r.Ide)
            .ToDictionary(g => g.Key, g => g.First().recibo_pendiente_id);

        var items = pageTx.Select(t =>
        {
            var numRecibo = (int)(t.recibo ?? 0);
            clientesMap.TryGetValue(t.cliente_clave ?? string.Empty, out var cli);
            facturasMap.TryGetValue(numRecibo, out var numFactura);

            return new AbonoEspecialListItemDto
            {
                TransaccionAbonadoIde = t.ide,
                PagoId = pagosMap.TryGetValue(t.ide, out var pagoId) ? pagoId : (long?)null,
                PendienteId = pendientesMap.TryGetValue(t.ide, out var pendienteId) ? pendienteId : (long?)null,
                ClienteId = cli?.maestro_cliente_id,
                ClienteClave = t.cliente_clave ?? string.Empty,
                ClienteNombre = cli?.maestro_cliente_nombre ?? string.Empty,
                NumFactura = string.IsNullOrWhiteSpace(numFactura) ? numRecibo.ToString() : numFactura!,
                NumRecibo = numRecibo,
                Monto = t.creditos ?? 0m,
                Fecha = t.fecha_docu?.ToDateTime(TimeOnly.MinValue),
                Periodo = t.periodo ?? string.Empty,
                Cajero = t.usuario ?? string.Empty,
                Banco = t.banco,
                Descripcion = t.descripcion,
                Estado = t.estado ?? string.Empty,
                EstadoDescripcion = DescribirEstadoAbono(t.estado)
            };
        }).ToList();

        return new PagedResult<AbonoEspecialListItemDto>(items, total);
    }

    public async Task<AbonoEspecialResumenDto> ObtenerResumenAbonosEspecialesAsync(AbonoEspecialFiltroDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var companyId = _currentCompanyService.GetCompanyId();

        // El resumen ignora el filtro de estado (queremos el conteo de CADA estado)
        // pero respeta la búsqueda y el rango de fechas.
        var filtroSinEstado = new AbonoEspecialFiltroDto
        {
            Search = filtro.Search,
            Desde = filtro.Desde,
            Hasta = filtro.Hasta
        };

        var agregados = await BuildAbonosEspecialesQuery(companyId, filtroSinEstado)
            .GroupBy(t => t.estado)
            .Select(g => new { Estado = g.Key, Count = g.Count(), Monto = g.Sum(x => x.creditos ?? 0m) })
            .ToListAsync(ct);

        var resumen = new AbonoEspecialResumenDto();
        foreach (var a in agregados)
        {
            switch (a.Estado)
            {
                case "C": resumen.PagadosCount = a.Count; resumen.PagadosMonto = a.Monto; break;
                case "P": resumen.NoAplicadosCount = a.Count; resumen.NoAplicadosMonto = a.Monto; break;
                case "A": resumen.AnuladosCount = a.Count; resumen.AnuladosMonto = a.Monto; break;
            }
            resumen.TotalRegistros += a.Count;
        }

        return resumen;
    }

    private IQueryable<transaccion_abonado> BuildAbonosEspecialesQuery(long companyId, AbonoEspecialFiltroDto filtro)
    {
        var query = _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.tipotransaccion == "202");

        var estado = string.IsNullOrWhiteSpace(filtro.Estado) ? null : filtro.Estado.Trim().ToUpperInvariant();
        if (estado is "C" or "P" or "A")
            query = query.Where(t => t.estado == estado);

        if (filtro.Desde.HasValue)
            query = query.Where(t => t.fecha_docu >= filtro.Desde.Value);
        if (filtro.Hasta.HasValue)
            query = query.Where(t => t.fecha_docu <= filtro.Hasta.Value);

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var termino = filtro.Search.Trim();
            var like = $"%{termino}%";
            var isNum = int.TryParse(termino, out var num);

            query = query.Where(t =>
                (t.cliente_clave != null && EF.Functions.ILike(t.cliente_clave, like))
                || (isNum && t.recibo == num)
                || _context.cliente_maestros.Any(c =>
                        c.maestro_cliente_clave == t.cliente_clave
                        && EF.Functions.ILike(c.maestro_cliente_nombre, like))
                || _context.facturas.Any(f =>
                        (decimal?)f.numrecibo == t.recibo
                        && f.numfactura != null
                        && EF.Functions.ILike(f.numfactura, like)));
        }

        return query;
    }

    private static IQueryable<transaccion_abonado> OrdenarAbonosEspeciales(
        IQueryable<transaccion_abonado> query, AbonoEspecialFiltroDto filtro)
    {
        var desc = filtro.SortDesc;
        return filtro.SortField switch
        {
            "Monto" => desc
                ? query.OrderByDescending(t => t.creditos).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.creditos).ThenBy(t => t.ide),
            "NumRecibo" => desc
                ? query.OrderByDescending(t => t.recibo).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.recibo).ThenBy(t => t.ide),
            "ClienteClave" => desc
                ? query.OrderByDescending(t => t.cliente_clave).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.cliente_clave).ThenBy(t => t.ide),
            "Cajero" => desc
                ? query.OrderByDescending(t => t.usuario).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.usuario).ThenBy(t => t.ide),
            "Estado" => desc
                ? query.OrderByDescending(t => t.estado).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.estado).ThenBy(t => t.ide),
            "Periodo" => desc
                ? query.OrderByDescending(t => t.periodo).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.periodo).ThenBy(t => t.ide),
            "Banco" => desc
                ? query.OrderByDescending(t => t.banco).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.banco).ThenBy(t => t.ide),
            // "Fecha" y por defecto: más recientes primero.
            "Fecha" when !desc => query.OrderBy(t => t.fecha_docu).ThenBy(t => t.ide),
            _ => query.OrderByDescending(t => t.fecha_docu).ThenByDescending(t => t.ide),
        };
    }

    private static string DescribirEstadoAbono(string? estado) => estado switch
    {
        "C" => "Pagado",
        "P" => "No aplicado",
        "A" => "Anulado",
        _ => "—"
    };

    // Rotulado de las líneas del recibo. La cartera migrada de SIMAFI trae la
    // MISMA descripción en todas las líneas de un recibo ("Factura Periodo
    // AAAA/MM"), así que el rótulo principal es el SERVICIO y la descripción
    // queda de complemento solo cuando aporta algo distinto.
    private static readonly Dictionary<string, string> NombresServicio = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AGUA_POTABLE"] = "Agua Potable",
        ["ALCANTARILLADO"] = "Alcantarillado",
        ["TASA_AMBIENTAL"] = "Tasa Ambiental",
        ["TASA_SVA_ERSAPS"] = "Tasa SVA ERSAPS",
        ["CORTE_RECONEXION"] = "Corte y Reconexión",
        ["OTROS_COLATERALES"] = "Otros Colaterales",
        ["SALDO_ANTERIOR"] = "Saldo Anterior",
        ["MISC"] = "Misceláneo",
    };

    private static string RotuloLineaRecibo(string? tipoServicio, string? descripcion)
    {
        var servicio = string.IsNullOrWhiteSpace(tipoServicio)
            ? null
            : NombresServicio.TryGetValue(tipoServicio.Trim(), out var nombre) ? nombre : tipoServicio.Trim();
        var desc = descripcion?.Trim();

        if (servicio is null) return desc ?? string.Empty;
        if (string.IsNullOrEmpty(desc) || string.Equals(desc, servicio, StringComparison.OrdinalIgnoreCase))
            return servicio;
        return $"{servicio} · {desc}";
    }
}
