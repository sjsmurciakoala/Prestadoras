using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Cobranza;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Cobros;

/// <summary>
/// Motor único de cobro (unificación cobranza F2 — plan §4). Absorbe la lógica
/// compartida de los caminos legacy (captación 201, abono 202): aplicación FIFO
/// por documento, estados de factura A→B→C, contabilidad por configuración,
/// kardex bancario, cancelación de cortes, y el modelo nuevo adm_pago +
/// adm_pago_aplicacion con folio por empresa. Dual-write hacia
/// transaccion_abonado hasta F7 para que arqueos, reportes y asientos sigan
/// cuadrando durante la transición.
/// </summary>
public class CobroService : ICobroService
{
    private readonly SiadDbContext _context;
    private readonly IBanTransaccionesService _banTransaccionesService;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly ICorteMasivoService _corteMasivoService;

    private const string BancoMarkerPrefix = "BANCO_CUENTA:";
    private const string TipoTransaccionBancoDeposito = "DEP";

    public CobroService(
        SiadDbContext context,
        IBanTransaccionesService banTransaccionesService,
        ICurrentCompanyService currentCompanyService,
        ICorteMasivoService corteMasivoService)
    {
        _context = context;
        _banTransaccionesService = banTransaccionesService;
        _currentCompanyService = currentCompanyService;
        _corteMasivoService = corteMasivoService;
    }

    public async Task<ResponseModelDto> RegistrarCobroAsync(CobroCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
            return ResponseModelDto.Fail("Debe indicar el cliente.");
        if (dto.Aplicaciones is null || dto.Aplicaciones.Count == 0)
            return ResponseModelDto.Fail("El cobro debe aplicar al menos a un documento.");
        if (dto.Aplicaciones.Any(a => a.Monto <= 0))
            return ResponseModelDto.Fail("Todos los montos aplicados deben ser mayores a cero.");
        if (dto.Aplicaciones.Any(a => a.DocumentoTipo != DocumentoCobroTipo.Factura))
            return ResponseModelDto.Fail("En esta fase el motor solo cobra facturas (las cuotas de plan llegan en F6).");
        if (dto.Aplicaciones.Any(a => !a.FacturaId.HasValue))
            return ResponseModelDto.Fail("Cada aplicación debe indicar la factura.");
        if (dto.FormaPago != "EFECTIVO" && dto.FormaPago != "BANCO")
            return ResponseModelDto.Fail("Forma de pago inválida (EFECTIVO o BANCO).");

        var facturaIds = dto.Aplicaciones.Select(a => a.FacturaId!.Value).ToList();
        if (facturaIds.Distinct().Count() != facturaIds.Count)
            return ResponseModelDto.Fail("Hay aplicaciones duplicadas a la misma factura.");

        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var fechaPago = dto.FechaPago?.Date ?? DateTime.UtcNow.Date;
        var fechaHoy = DateOnly.FromDateTime(fechaPago);
        var montoTotal = dto.Aplicaciones.Sum(a => a.Monto);
        var referencia = string.IsNullOrWhiteSpace(dto.ReferenciaExterna) ? null : dto.ReferenciaExterna.Trim();

        // ---------- Regla única (plan §4): canal caja exige sesión ABIERTA ----------
        int? sesionCajaId = null;
        if (dto.Canal == CanalCobro.Caja)
        {
            sesionCajaId = await _context.sesion_cajas
                .AsNoTracking()
                .Where(s => s.company_id == companyId && s.usuario_apertura == usuario && s.estado == "ABIERTA")
                .Select(s => (int?)s.id)
                .FirstOrDefaultAsync(ct);
            if (sesionCajaId is null)
                return ResponseModelDto.Fail(
                    $"El usuario {usuario} no tiene una sesión de caja abierta; abra la sesión antes de cobrar.");
        }

        // ---------- Idempotencia — chequeo rápido pre-tx (barato, sin lock) ----------
        if (referencia is not null)
        {
            var previo = await BuscarCobroPorReferenciaAsync(companyId, referencia, ct);
            if (previo is not null) return previo;
        }

        var periodo = await ObtenerPeriodoActualCodigoAsync(ct);
        var integrarBancos = dto.FormaPago == "BANCO" && dto.BancoCuentaId is > 0;
        var banco = await ResolverBancoCodigoAsync(dto.BancoCuentaId, dto.Banco, ct);

        if (dto.ReciboPendienteId.HasValue)
        {
            var existePendiente = await _context.transaccion_abonados
                .AnyAsync(t => t.company_id == companyId && t.ide == dto.ReciboPendienteId.Value && t.estado == "P", ct);
            if (!existePendiente)
                return ResponseModelDto.Fail("El recibo pendiente indicado no existe o ya fue procesado.");
        }

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

        // ---------- Plan pre-tx (solo lectura): validación y derrame proyectado ----------
        // Base del kardex bancario (que postea su propia transacción, patrón legacy)
        // y validación temprana. El derrame definitivo se recalcula DENTRO de la
        // transacción con las filas bloqueadas.
        var planContable = new List<(string? ServicioCodigo, decimal Monto)>();
        int? categoriaServicioId = null;
        bool? conMedicion = null;
        string? numFacturaPrincipal = null;
        var numReciboPrincipal = 0;
        foreach (var apl in dto.Aplicaciones)
        {
            var vista = await _context.facturas
                .AsNoTracking()
                .Where(f => f.company_id == companyId && f.id == apl.FacturaId!.Value)
                .Select(f => new { f.id, f.estado, f.numfactura, f.numrecibo, f.saldototal, f.categoria_servicio_id, f.con_medicion })
                .FirstOrDefaultAsync(ct);
            if (vista is null)
                return ResponseModelDto.Fail($"No se encontró la factura {apl.FacturaId}.");
            if (vista.estado == "N")
                return ResponseModelDto.Fail($"La factura {vista.numfactura ?? vista.numrecibo.ToString()} está anulada.");

            if (numFacturaPrincipal is null)
            {
                numFacturaPrincipal = vista.numfactura;
                numReciboPrincipal = vista.numrecibo;
                categoriaServicioId = vista.categoria_servicio_id;
                conMedicion = vista.con_medicion;
            }

            var lineas = await _context.factura_detalles
                .AsNoTracking()
                .Where(d => d.factura_id == vista.id)
                .OrderBy(d => d.id)
                .Select(d => new { d.montovalor_saldo, d.montovalor, d.tiposervicio, d.codigo })
                .ToListAsync(ct);

            var saldoDetalles = lineas.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m);
            var saldoPendiente = saldoDetalles > 0 ? saldoDetalles : (vista.saldototal ?? 0m);
            if (saldoPendiente <= 0)
                return ResponseModelDto.Fail(
                    $"La factura {vista.numfactura ?? vista.numrecibo.ToString()} no tiene saldo pendiente.");
            if (apl.Monto > saldoPendiente)
                return ResponseModelDto.Fail(
                    $"El monto aplicado ({apl.Monto:N2}) excede el saldo pendiente ({saldoPendiente:N2}) de la factura {vista.numfactura ?? vista.numrecibo.ToString()}.");

            var restantePlan = apl.Monto;
            foreach (var linea in lineas)
            {
                if (restantePlan <= 0) break;
                var lineSaldo = linea.montovalor_saldo ?? linea.montovalor ?? 0m;
                if (lineSaldo <= 0) continue;
                var aplicado = Math.Min(restantePlan, lineSaldo);
                planContable.Add((
                    string.IsNullOrWhiteSpace(linea.tiposervicio) ? linea.codigo : linea.tiposervicio,
                    aplicado));
                restantePlan -= aplicado;
            }
            if (restantePlan > 0)
                planContable.Add((null, restantePlan));
        }

        var documentoContable = ResolverDocumentoContable(dto.TipoLegacy);
        var referenciaContable = string.IsNullOrWhiteSpace(numFacturaPrincipal)
            ? $"{documentoContable.Documento}-{numReciboPrincipal}"
            : numFacturaPrincipal.Trim();
        var descripcionContable = $"Cobro factura {referenciaContable}";

        // ---------- Kardex bancario (pre-tx: BanTransaccionesService postea aparte) ----------
        (long BanKardexId, decimal SaldoResultante)? movimientoBanco = null;
        if (integrarBancos)
        {
            var periodoAbiertoId = await _context.con_periodo_contables
                .AsNoTracking()
                .Where(p => p.company_id == companyId
                    && p.status_id == 0
                    && p.start_date <= fechaPago
                    && p.end_date >= fechaPago)
                .Select(p => (long?)p.period_id)
                .FirstOrDefaultAsync(ct);
            if (periodoAbiertoId is null)
                return ResponseModelDto.Fail(
                    $"No hay período contable abierto para la fecha {fechaHoy:dd/MM/yyyy}; los cobros por banco requieren período abierto.");

            try
            {
                var contraCuentas = await IntegracionContableConfigSql.ConstruirContraCuentasCxcAsync(
                    _context.Database.GetDbConnection(),
                    companyId,
                    planContable,
                    categoriaServicioId,
                    conMedicion,
                    descripcionContable,
                    referenciaContable,
                    ct);

                movimientoBanco = await _banTransaccionesService.RegistrarMovimientoAsync(
                    dto.BancoCuentaId!.Value,
                    TipoTransaccionBancoDeposito,
                    fechaHoy,
                    descripcionContable,
                    referenciaContable,
                    referenciaContable,
                    1m,
                    montoTotal,
                    contraCuentas,
                    usuario,
                    ct);
            }
            catch (Exception ex)
            {
                return ResponseModelDto.Fail($"Error al registrar movimiento bancario: {ex.Message}");
            }
        }

        // Transacción propia solo si no hay una ambiente (tests con rollback y
        // fachadas que ya traen transacción abierta la reutilizan).
        var ownsTx = _context.Database.CurrentTransaction is null;
        await using var tx = ownsTx ? await _context.Database.BeginTransactionAsync(ct) : null;
        var connection = _context.Database.GetDbConnection();
        var dbTransaction = _context.Database.CurrentTransaction!.GetDbTransaction();
        try
        {
            // ---------- Idempotencia definitiva (advisory lock + re-chequeo) ----------
            if (referencia is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "SELECT pg_advisory_xact_lock(hashtextextended(@Key, 0))",
                    new { Key = $"cobro:{companyId}:{referencia}" },
                    dbTransaction, cancellationToken: ct));

                var previo = await BuscarCobroPorReferenciaAsync(companyId, referencia, ct);
                if (previo is not null)
                {
                    if (tx is not null) await tx.RollbackAsync(ct);
                    if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
                        await TryCompensarMovimientoBancarioAsync(dto.BancoCuentaId.Value, movimientoBanco.Value.BanKardexId, usuario, ct);
                    return previo;
                }
            }

            // ---------- Bloqueo de filas + derrame definitivo ----------
            var aplicacionesContables = new List<(string? ServicioCodigo, decimal Monto)>();
            var lineasAplicadas = new List<(int FacturaId, int? DetalleId, decimal Monto)>();
            var resultados = new List<CobroAplicacionResultadoDto>();
            factura? facturaPrincipal = null;

            foreach (var apl in dto.Aplicaciones)
            {
                var factura = await _context.facturas
                    .FromSqlInterpolated($"SELECT * FROM public.factura WHERE company_id = {companyId} AND id = {apl.FacturaId!.Value} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
                if (factura is null)
                    return ResponseModelDto.Fail($"No se encontró la factura {apl.FacturaId}.");
                facturaPrincipal ??= factura;

                var detalles = await _context.factura_detalles
                    .Where(d => d.factura_id == factura.id)
                    .OrderBy(d => d.id)
                    .ToListAsync(ct);

                var saldoDetalles = detalles.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m);
                var saldoPendiente = saldoDetalles > 0 ? saldoDetalles : (factura.saldototal ?? 0m);
                if (saldoPendiente <= 0 || apl.Monto > saldoPendiente)
                    return ResponseModelDto.Fail(
                        $"El saldo de la factura {factura.numfactura ?? factura.numrecibo.ToString()} cambió; vuelva a consultar e intente de nuevo.");

                var restante = apl.Monto;
                foreach (var detalle in detalles)
                {
                    if (restante <= 0) break;
                    var lineSaldo = detalle.montovalor_saldo ?? detalle.montovalor ?? 0m;
                    if (lineSaldo <= 0) continue;

                    var aplicado = Math.Min(restante, lineSaldo);
                    detalle.montovalor_saldo = lineSaldo - aplicado;
                    restante -= aplicado;

                    lineasAplicadas.Add((factura.id, detalle.id, aplicado));
                    aplicacionesContables.Add((
                        string.IsNullOrWhiteSpace(detalle.tiposervicio) ? detalle.codigo : detalle.tiposervicio,
                        aplicado));
                }

                if (restante > 0)
                {
                    lineasAplicadas.Add((factura.id, null, restante));
                    aplicacionesContables.Add((null, restante));
                }

                var nuevoSaldoFactura = saldoPendiente - apl.Monto;
                if (nuevoSaldoFactura <= 0)
                {
                    factura.estado = "C";
                    factura.fechapago = fechaHoy;
                    factura.usuario = usuario;
                    factura.recolectora = banco;
                }
                else
                {
                    factura.estado = "B";
                }

                resultados.Add(new CobroAplicacionResultadoDto
                {
                    FacturaId = factura.id,
                    NumFactura = factura.numfactura ?? factura.numrecibo.ToString(),
                    NumRecibo = factura.numrecibo,
                    MontoAplicado = apl.Monto,
                    SaldoRestante = nuevoSaldoFactura,
                    EstadoFactura = factura.estado!
                });
            }

            // ---------- Fila espejo legacy (dual-write, muere en F7) ----------
            var saldoActualCliente = await ObtenerSaldoClienteAsync(dto.ClienteClave, ct);

            transaccion_abonado espejo;
            if (dto.ReciboPendienteId.HasValue)
            {
                var pendiente = await _context.transaccion_abonados
                    .FirstOrDefaultAsync(t => t.ide == dto.ReciboPendienteId.Value && t.estado == "P", ct);
                if (pendiente is null)
                    return ResponseModelDto.Fail("El recibo pendiente ya fue procesado o no existe.");

                pendiente.estado = "C";
                pendiente.fecha_docu = fechaHoy;
                pendiente.banco = banco;
                pendiente.debitos = 0m;
                pendiente.creditos = montoTotal;
                pendiente.saldo = saldoActualCliente - montoTotal;
                pendiente.saldo_detalle = montoTotal;
                pendiente.descripcion = $"Abono parcial factura :Recibo # :{numReciboPrincipal}";
                pendiente.usuario = usuario;
                pendiente.caja_id = sesionCajaId;
                pendiente.ciclo = clienteInfo?.ciclos_id?.ToString();
                pendiente.ruta = clienteInfo?.maestro_cliente_indicativo_ruta;
                pendiente.secuencia = clienteInfo?.maestro_cliente_secuencia;
                pendiente.tiene_med = clienteInfo?.maestro_cliente_tiene_medidor == true ? "S" : "N";
                espejo = pendiente;
            }
            else
            {
                espejo = new transaccion_abonado
                {
                    company_id = companyId,
                    caja_id = sesionCajaId,
                    cliente_clave = dto.ClienteClave,
                    recibo = numReciboPrincipal,
                    tipotransaccion = dto.TipoLegacy,
                    fecha_docu = fechaHoy,
                    tipo_partida = dto.TipoPartidaLegacy,
                    banco = banco,
                    descripcion = $"Abono parcial factura :Recibo # :{numReciboPrincipal}",
                    debitos = 0,
                    creditos = montoTotal,
                    saldo = saldoActualCliente - montoTotal,
                    tipo_servicio = "E",
                    periodo = periodo,
                    tasa = "0",
                    estado = "C",
                    fecha_registro = fechaHoy,
                    ciclo = clienteInfo?.ciclos_id?.ToString(),
                    ruta = clienteInfo?.maestro_cliente_indicativo_ruta,
                    secuencia = clienteInfo?.maestro_cliente_secuencia,
                    tiene_med = clienteInfo?.maestro_cliente_tiene_medidor == true ? "S" : "N",
                    usuario = usuario,
                    saldo_detalle = montoTotal
                };
                _context.transaccion_abonados.Add(espejo);
            }

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                espejo.docuaplicar = Convert.ToDecimal(movimientoBanco.Value.BanKardexId, CultureInfo.InvariantCulture);
                espejo.trans_aplicar = $"{BancoMarkerPrefix}{dto.BancoCuentaId.Value.ToString(CultureInfo.InvariantCulture)}";
            }

            await _context.SaveChangesAsync(ct);

            // ---------- Folio + adm_pago + aplicaciones (el modelo nuevo) ----------
            var numeroRecibo = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT public.fn_adm_siguiente_correlativo_documento(@CompanyId, @Tipo, @Canal)",
                new { CompanyId = companyId, Tipo = TipoDocumentoSecuencia.ReciboPago, Canal = dto.Canal },
                dbTransaction, cancellationToken: ct));
            if (string.IsNullOrWhiteSpace(numeroRecibo))
                return ResponseModelDto.Fail(
                    "No hay serie de folios RECIBO_PAGO activa para la empresa (adm_documento_secuencia).");

            var pago = new adm_pago
            {
                company_id = companyId,
                numero_recibo = numeroRecibo,
                cliente_clave = dto.ClienteClave,
                fecha = fechaHoy,
                canal_id = dto.Canal,
                tipo_transaccion_id = dto.Canal == CanalCobro.Banco ? TipoTransaccion.PagoBanco : TipoTransaccion.PagoCaja,
                estado_id = EstadoPago.Aplicado,
                monto_total = montoTotal,
                forma_pago = dto.FormaPago,
                banco_cuenta_id = dto.BancoCuentaId.HasValue ? (int?)dto.BancoCuentaId.Value : null,
                ban_kardex_id = movimientoBanco?.BanKardexId,
                sesion_caja_id = sesionCajaId,
                referencia_externa = referencia,
                transaccion_abonado_ide = espejo.ide,
                usuario = usuario
            };
            foreach (var (facturaId, detalleId, monto) in lineasAplicadas)
            {
                pago.aplicaciones.Add(new adm_pago_aplicacion
                {
                    company_id = companyId,
                    documento_tipo = DocumentoCobroTipo.Factura,
                    factura_id = facturaId,
                    factura_detalle_id = detalleId,
                    monto_aplicado = monto
                });
            }
            _context.adm_pagos.Add(pago);
            await _context.SaveChangesAsync(ct);

            // ---------- Cortes: cancelar órdenes si el cliente queda en cero ----------
            if (saldoActualCliente - montoTotal <= 0m)
            {
                await _corteMasivoService.CancelarOrdenesCorteClienteAsync(dto.ClienteClave, usuario, ct);
            }

            // ---------- Comprobante contable (efectivo; banco ya posteó su partida) ----------
            long? polizaId = null;
            if (!integrarBancos)
            {
                var config = await IntegracionContableConfigSql.ObtenerConfigAsync(connection, companyId, dbTransaction, ct);
                if (config is not null && config.ActivoCaja)
                {
                    var cuentaCaja = await IntegracionContableConfigSql.ResolverCuentaAsync(
                        connection, companyId, "CAJA", dbTransaction, ct);

                    var aplicacionesCxc = await IntegracionContableConfigSql.ResolverAplicacionesCxcAsync(
                        connection,
                        companyId,
                        config.ModoCxc,
                        aplicacionesContables,
                        facturaPrincipal?.categoria_servicio_id,
                        facturaPrincipal?.con_medicion,
                        dbTransaction,
                        ct);

                    var lineas = IntegracionContableConfigSql.ArmarLineasCobro(
                        cuentaCaja,
                        aplicacionesCxc,
                        descripcionContable);

                    polizaId = await IntegracionContableConfigSql.GenerarComprobanteAsync(
                        connection,
                        companyId,
                        documentoContable.Modulo,
                        documentoContable.Documento,
                        espejo.ide,
                        $"{documentoContable.Documento}-{espejo.ide}",
                        fechaHoy,
                        descripcionContable,
                        usuario,
                        lineas,
                        dbTransaction,
                        ct);

                    if (polizaId.HasValue)
                    {
                        pago.poliza_id = polizaId;
                        await _context.SaveChangesAsync(ct);
                    }
                }
            }

            if (tx is not null) await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(new CobroResultadoDto
            {
                PagoId = pago.pago_id,
                NumeroRecibo = numeroRecibo,
                MontoTotal = montoTotal,
                NuevoSaldoCliente = saldoActualCliente - montoTotal,
                PolizaId = polizaId,
                TransaccionId = espejo.ide,
                Aplicaciones = resultados
            }, "Cobro registrado correctamente.");
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                await TryCompensarMovimientoBancarioAsync(dto.BancoCuentaId.Value, movimientoBanco.Value.BanKardexId, usuario, ct);
            }
            return ResponseModelDto.Fail($"Error al registrar el cobro: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> ReversarCobroAsync(CobroReversoDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();

        var pago = await _context.adm_pagos
            .Include(p => p.aplicaciones)
            .FirstOrDefaultAsync(p => p.company_id == companyId && p.pago_id == dto.PagoId, ct);
        if (pago is null)
            return ResponseModelDto.Fail("No se encontró el cobro indicado.");
        if (pago.estado_id != EstadoPago.Aplicado)
            return ResponseModelDto.Fail("El cobro ya se encuentra anulado o reversado.");
        if (pago.canal_id == CanalCobro.Banco)
            return ResponseModelDto.Fail(
                "Este pago proviene del canal bancario y debe reversarse por ese canal (sp_ban_ws_reversar).");

        var facturaIds = pago.aplicaciones
            .Where(a => a.factura_id.HasValue)
            .Select(a => a.factura_id!.Value)
            .Distinct()
            .ToList();

        transaccion_abonado? espejo = null;
        if (pago.transaccion_abonado_ide.HasValue)
        {
            espejo = await _context.transaccion_abonados
                .FirstOrDefaultAsync(t => t.company_id == companyId && t.ide == pago.transaccion_abonado_ide.Value, ct);
        }

        var ownsTx = _context.Database.CurrentTransaction is null;
        await using var tx = ownsTx ? await _context.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Restituir saldos EXACTAMENTE en las líneas aplicadas (mejor que la
            // heurística legacy: la tabla de aplicación sabe qué se cobró dónde).
            foreach (var facturaId in facturaIds)
            {
                var factura = await _context.facturas
                    .FromSqlInterpolated($"SELECT * FROM public.factura WHERE company_id = {companyId} AND id = {facturaId} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
                if (factura is null)
                    return ResponseModelDto.Fail($"No se encontró la factura {facturaId} del cobro.");

                var detalles = await _context.factura_detalles
                    .Where(d => d.factura_id == facturaId)
                    .ToListAsync(ct);

                foreach (var apl in pago.aplicaciones.Where(a => a.factura_id == facturaId && a.factura_detalle_id.HasValue))
                {
                    var detalle = detalles.FirstOrDefault(d => d.id == apl.factura_detalle_id!.Value);
                    if (detalle is not null)
                    {
                        var tope = detalle.montovalor ?? 0m;
                        detalle.montovalor_saldo = Math.Min(tope, (detalle.montovalor_saldo ?? 0m) + apl.monto_aplicado);
                    }
                }
                // Aplicaciones sin línea (remanente al encabezado legacy): sin
                // restitución de detalle — mismo comportamiento del flujo viejo.

                var saldoRestante = detalles.Sum(d => d.montovalor_saldo ?? 0m);
                var totalFactura = detalles.Sum(d => d.montovalor ?? 0m);
                if (saldoRestante >= totalFactura && totalFactura > 0)
                {
                    factura.estado = "A";
                    factura.fechapago = null;
                    factura.recolectora = null;
                }
                else
                {
                    factura.estado = "B";
                }
            }

            // Espejo legacy: marcar anulado (NUNCA borrar — regla única del motor).
            if (espejo is not null)
            {
                espejo.estado = "A";
                espejo.usuario = usuario;
                espejo.descripcion = $"REVERSADO: {dto.Motivo}";
            }

            // Contabilidad / banco
            if (pago.ban_kardex_id.HasValue && pago.banco_cuenta_id.HasValue)
            {
                await _banTransaccionesService.AnularMovimientoAsync(
                    pago.banco_cuenta_id.Value,
                    pago.ban_kardex_id.Value,
                    $"Reverso cobro {pago.numero_recibo}",
                    usuario,
                    ct);
            }
            else if (espejo is not null)
            {
                var connection = _context.Database.GetDbConnection();
                var dbTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                var documentoContable = ResolverDocumentoContable(espejo.tipotransaccion ?? "202");

                await IntegracionContableConfigSql.RevertirComprobanteAsync(
                    connection,
                    companyId,
                    documentoContable.Modulo,
                    new[] { documentoContable.Documento },
                    espejo.ide,
                    usuario,
                    dbTransaction,
                    ct);
            }

            pago.estado_id = EstadoPago.Anulado;
            pago.motivo_reverso = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
            pago.actualizado_en = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(null, "Cobro reversado correctamente.");
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            return ResponseModelDto.Fail($"Error al reversar el cobro: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private async Task<ResponseModelDto?> BuscarCobroPorReferenciaAsync(long companyId, string referencia, CancellationToken ct)
    {
        var existente = await _context.adm_pagos
            .AsNoTracking()
            .Where(p => p.company_id == companyId && p.referencia_externa == referencia)
            .Select(p => new { p.pago_id, p.numero_recibo, p.monto_total, p.transaccion_abonado_ide, p.estado_id })
            .FirstOrDefaultAsync(ct);
        if (existente is null) return null;

        if (existente.estado_id != EstadoPago.Aplicado)
            return ResponseModelDto.Fail(
                $"La referencia {referencia} ya fue usada y su cobro está anulado/reversado; use una referencia nueva.");

        return ResponseModelDto.Ok(new CobroResultadoDto
        {
            PagoId = existente.pago_id,
            NumeroRecibo = existente.numero_recibo,
            MontoTotal = existente.monto_total,
            TransaccionId = existente.transaccion_abonado_ide ?? 0,
            Idempotente = true
        }, "Cobro ya aplicado con esa referencia (idempotente).");
    }

    /// <summary>
    /// Módulo/documento contable por compatibilidad legacy durante el dual-write:
    /// '201' (captación) → VENTAS/REC, '202' (abono) → CAJA/ABO. La unificación a
    /// un documento único queda pendiente de validación con el contador (plan §9.4).
    /// </summary>
    private static (string Modulo, string Documento) ResolverDocumentoContable(string tipoLegacy) =>
        tipoLegacy == "201" ? ("VENTAS", "REC") : ("CAJA", "ABO");

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@CompanyId, @ClienteClave)";
        var saldo = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            sql,
            new { CompanyId = companyId, ClienteClave = clienteClave },
            _context.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: ct));
        return saldo ?? 0m;
    }

    private async Task<string> ObtenerPeriodoActualCodigoAsync(CancellationToken ct)
    {
        var periodo = await _context.adm_periodo_comercials
            .AsNoTracking()
            .Where(p => p.status_id == EstadoPeriodoComercial.Abierto)
            .OrderByDescending(p => p.anio)
            .ThenByDescending(p => p.mes)
            .Select(p => new { p.anio, p.mes })
            .FirstOrDefaultAsync(ct);

        return periodo is null
            ? DateTime.UtcNow.ToString("yyyyMM")
            : $"{periodo.anio:D4}{periodo.mes:D2}";
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

    private async Task TryCompensarMovimientoBancarioAsync(long bancoCuentaId, long banKardexId, string usuario, CancellationToken ct)
    {
        try
        {
            await _banTransaccionesService.AnularMovimientoAsync(
                bancoCuentaId, banKardexId, "Compensación de cobro fallido", usuario, ct);
        }
        catch
        {
            // Fail silent: la compensación es best-effort tras un rollback.
        }
    }
}
