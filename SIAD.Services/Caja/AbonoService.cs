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
using SIAD.Core.DTOs.Caja;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Core.Utilities;
using SIAD.Services.Bancos;
using SIAD.Services.Cobranza;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Caja;

public class AbonoService : IAbonoService
{
    private readonly SiadDbContext _context;
    private readonly IBanTransaccionesService _banTransaccionesService;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly ICorteMasivoService _corteMasivoService;
    private const string ContabilidadModuloCaja = "CAJA";
    private const string ContabilidadDocumentoAbono = "ABO";
    private const string BancoMarkerPrefix = "BANCO_CUENTA:";
    private const string TipoTransaccionBancoDeposito = "DEP";

    public AbonoService(
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

        var query = from f in _context.facturas.AsNoTracking()
                    join c in _context.cliente_maestros.AsNoTracking()
                        on f.clientecodigo equals c.maestro_cliente_clave into clientes
                    from c in clientes.DefaultIfEmpty()
                    where f.company_id == companyId && (f.estado == "A" || f.estado == "B" || f.estado == "C")
                    where (f.numfactura != null && EF.Functions.ILike(f.numfactura, filtroLike))
                          || (isNumero && f.numrecibo == numero)
                          || (f.clientecodigo != null && EF.Functions.ILike(f.clientecodigo, filtroLike))
                          || (c != null && EF.Functions.ILike(c.maestro_cliente_nombre, filtroLike))
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
        var response = new List<FacturaConSaldoDto>();

        foreach (var x in items)
        {
            var saldoPendiente = await _context.factura_detalles
                .Where(d => d.factura_id == x.FacturaId)
                .SumAsync(d => d.montovalor_saldo ?? d.montovalor ?? 0m, ct);

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

        return response;
    }

    public async Task<ResponseModelDto> RegistrarAbonoAsync(AbonoCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Monto <= 0)
        {
            return ResponseModelDto.Fail("El monto del abono debe ser mayor a cero.");
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var fechaPago = dto.FechaPago?.Date ?? DateTime.UtcNow.Date;
        var fechaHoy = DateOnly.FromDateTime(fechaPago);

        var factura = await _context.facturas
            .FirstOrDefaultAsync(f => f.company_id == companyId && (f.numfactura == dto.NumFactura || f.numrecibo.ToString() == dto.NumFactura), ct);

        if (factura is null)
        {
            return ResponseModelDto.Fail("No se encontró la factura indicada.");
        }

        var detalles = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .OrderBy(d => d.id)
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

        // El período contable ya no se valida acá: sp_con_generar_comprobante_config
        // encola en con_partida_pendiente o rechaza según encolar_sin_periodo (F4).
        var periodo = await ObtenerPeriodoActualCodigoAsync(ct);

        var integrarBancos = dto.FormaPago == "BANCO" && dto.BancoCuentaId.HasValue && dto.BancoCuentaId.Value > 0;
        var banco = await ResolverBancoCodigoAsync(dto.BancoCuentaId, dto.Banco, ct);

        if (dto.ReciboPendienteId.HasValue)
        {
            var existePendiente = await _context.transaccion_abonados
                .AnyAsync(t => t.company_id == companyId
                    && t.ide == dto.ReciboPendienteId.Value
                    && t.estado == "P", ct);
            if (!existePendiente)
                return ResponseModelDto.Fail("El recibo pendiente indicado no existe o ya fue procesado.");
        }

        var contabilidadDocumentNumber = string.IsNullOrWhiteSpace(factura.numfactura)
            ? $"ABO-{factura.numrecibo}"
            : factura.numfactura.Trim();
        var contabilidadDescription = $"Abono en Caja factura {contabilidadDocumentNumber}";

        // Derrame planificado del abono sobre el detalle (mismo orden FIFO que se
        // aplica dentro de la transacción): base de las líneas CxC analíticas de
        // la integración por config (F4 — reemplaza a con_regla_integracion).
        var aplicacionesContables = new List<(string? ServicioCodigo, decimal Monto)>();
        var restantePlan = dto.Monto;
        foreach (var detalle in detalles)
        {
            if (restantePlan <= 0) break;
            var lineSaldo = detalle.montovalor_saldo ?? detalle.montovalor ?? 0m;
            if (lineSaldo <= 0) continue;

            var aplicado = Math.Min(restantePlan, lineSaldo);
            aplicacionesContables.Add((
                string.IsNullOrWhiteSpace(detalle.tiposervicio) ? detalle.codigo : detalle.tiposervicio,
                aplicado));
            restantePlan -= aplicado;
        }

        // Facturas legacy con saldo solo en el encabezado (detalles en cero o
        // desfasados): el remanente va a la CxC general para que el comprobante
        // y las contracuentas cubran el monto completo del abono.
        if (restantePlan > 0)
        {
            aplicacionesContables.Add((null, restantePlan));
        }

        (long BanKardexId, decimal SaldoResultante)? movimientoBanco = null;
        if (integrarBancos)
        {
            // El movimiento bancario postea su partida sin cola de pendientes
            // (BanTransaccionesService cae a períodos cerrados): se conserva la
            // validación de período que el flujo tenía antes de F4.
            var periodoAbiertoId = await _context.con_periodo_contables
                .AsNoTracking()
                .Where(p => p.company_id == companyId
                    && p.status_id == 0
                    && p.start_date <= fechaPago
                    && p.end_date >= fechaPago)
                .Select(p => (long?)p.period_id)
                .FirstOrDefaultAsync(ct);
            if (periodoAbiertoId is null)
            {
                return ResponseModelDto.Fail(
                    $"No hay período contable abierto para la fecha {fechaHoy:dd/MM/yyyy}; los abonos por banco requieren período abierto.");
            }

            try
            {
                var contraCuentasBanco = await ConstruirContraCuentasCxcAsync(
                    companyId,
                    aplicacionesContables,
                    factura.categoria_servicio_id,
                    factura.con_medicion,
                    contabilidadDescription,
                    contabilidadDocumentNumber,
                    ct);

                movimientoBanco = await RegistrarMovimientoBancarioCaptacionAsync(
                    dto.BancoCuentaId!.Value,
                    fechaHoy,
                    contabilidadDescription,
                    contabilidadDocumentNumber,
                    contabilidadDocumentNumber,
                    dto.Monto,
                    contraCuentasBanco,
                    usuario,
                    ct);
            }
            catch (Exception ex)
            {
                return ResponseModelDto.Fail($"Error al registrar movimiento bancario: {ex.Message}");
            }
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Derramar el abono en el detalle de la factura
            var remainingMonto = dto.Monto;
            foreach (var detalle in detalles)
            {
                if (remainingMonto <= 0) break;
                var lineSaldo = detalle.montovalor_saldo ?? detalle.montovalor ?? 0m;
                if (lineSaldo <= 0) continue;

                var applied = Math.Min(remainingMonto, lineSaldo);
                detalle.montovalor_saldo = lineSaldo - applied;
                remainingMonto -= applied;
            }

            var nuevoSaldo = saldoPendiente - dto.Monto;
            if (nuevoSaldo <= 0)
            {
                factura.estado = "C"; // Completamente pagado
                factura.fechapago = fechaHoy;
                factura.usuario = usuario;
                factura.recolectora = banco;
            }
            else
            {
                factura.estado = "B"; // Parcialmente abonado
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

            var saldoActualCliente = await ObtenerSaldoClienteAsync(dto.ClienteClave, ct);

            transaccion_abonado transaccion;
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
                pendiente.creditos = dto.Monto;
                pendiente.saldo = saldoActualCliente - dto.Monto;
                pendiente.saldo_detalle = dto.Monto;
                pendiente.descripcion = $"Abono parcial factura :Recibo # :{factura.numrecibo}";
                pendiente.usuario = usuario;
                pendiente.ciclo = clienteInfo?.ciclos_id?.ToString();
                pendiente.ruta = clienteInfo?.maestro_cliente_indicativo_ruta;
                pendiente.secuencia = clienteInfo?.maestro_cliente_secuencia;
                pendiente.tiene_med = clienteInfo?.maestro_cliente_tiene_medidor == true ? "S" : "N";
                transaccion = pendiente;
            }
            else
            {
                transaccion = new transaccion_abonado
                {
                    company_id = companyId,
                    caja_id = null,
                    cliente_clave = dto.ClienteClave,
                    recibo = factura.numrecibo,
                    tipotransaccion = "202",
                    fecha_docu = fechaHoy,
                    tipo_partida = "002",
                    banco = banco,
                    descripcion = $"Abono parcial factura :Recibo # :{factura.numrecibo}",
                    debitos = 0,
                    creditos = dto.Monto,
                    saldo = saldoActualCliente - dto.Monto,
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
                    saldo_detalle = dto.Monto
                };
                _context.transaccion_abonados.Add(transaccion);
            }

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                AplicarReferenciaMovimientoBancario(transaccion, dto.BancoCuentaId.Value, movimientoBanco.Value.BanKardexId);
            }

            await _context.SaveChangesAsync(ct);

            // Si el cliente queda sin saldo tras el abono, cancela cualquier orden de
            // corte (33) pendiente y la saca de la lista de reimpresión del corte.
            if (saldoActualCliente - dto.Monto <= 0m)
            {
                await _corteMasivoService.CancelarOrdenesCorteClienteAsync(dto.ClienteClave, usuario, ct);
            }

            long? polizaId = null;
            if (!integrarBancos)
            {
                // Comprobante por configuración (F4): Debe caja / Haber CxC
                // analítica, resuelto vía fn_con_resolver_cuenta[_modo] y posteado
                // por el motor único; NULL = encolado en con_partida_pendiente.
                var connection = _context.Database.GetDbConnection();
                var dbTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();

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
                        factura.categoria_servicio_id,
                        factura.con_medicion,
                        dbTransaction,
                        ct);

                    var lineas = IntegracionContableConfigSql.ArmarLineasCobro(
                        cuentaCaja,
                        aplicacionesCxc,
                        contabilidadDescription);

                    polizaId = await IntegracionContableConfigSql.GenerarComprobanteAsync(
                        connection,
                        companyId,
                        ContabilidadModuloCaja,
                        ContabilidadDocumentoAbono,
                        transaccion.ide,
                        $"ABO-{transaccion.ide}",
                        fechaHoy,
                        contabilidadDescription,
                        usuario,
                        lineas,
                        dbTransaction,
                        ct);
                }
            }

            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(new AbonoResponseDto
            {
                NumFactura = factura.numfactura ?? factura.numrecibo.ToString(),
                NumRecibo = factura.numrecibo,
                MontoAbonado = dto.Monto,
                NuevoSaldo = nuevoSaldo,
                PolizaId = polizaId,
                TransaccionId = transaccion.ide
            }, "Abono registrado correctamente.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                await TryCompensarMovimientoBancarioAsync(dto.BancoCuentaId.Value, movimientoBanco.Value.BanKardexId, usuario, ct);
            }
            return ResponseModelDto.Fail($"Error al registrar el abono: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> ReversarAbonoAsync(ReversoAbonoRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();

        var transaccion = await _context.transaccion_abonados
            .FirstOrDefaultAsync(t => t.company_id == companyId && t.ide == dto.TransaccionId && t.tipotransaccion == "202", ct);

        if (transaccion is null)
        {
            return ResponseModelDto.Fail("No se encontró la transacción de abono indicada.");
        }

        if (transaccion.estado == "A")
        {
            return ResponseModelDto.Fail("El abono ya se encuentra anulado o reversado.");
        }

        var factura = await _context.facturas
            .FirstOrDefaultAsync(f => f.company_id == companyId && f.numrecibo == (int)transaccion.recibo!, ct);

        if (factura is null)
        {
            return ResponseModelDto.Fail("No se encontró la factura relacionada con el abono.");
        }

        var detalles = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .OrderBy(d => d.id)
            .ToListAsync(ct);

        var tieneMovimientoBanco = TryObtenerReferenciaMovimientoBancario(transaccion, out var bancoCuentaId, out var banKardexIdOriginal);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Restituir los saldos en los detalles de la factura
            var remainingMonto = transaccion.creditos ?? 0m;
            foreach (var detalle in detalles)
            {
                if (remainingMonto <= 0) break;
                var maxRevertible = (detalle.montovalor ?? 0m) - (detalle.montovalor_saldo ?? 0m);
                if (maxRevertible <= 0) continue;

                var applied = Math.Min(remainingMonto, maxRevertible);
                detalle.montovalor_saldo = (detalle.montovalor_saldo ?? 0m) + applied;
                remainingMonto -= applied;
            }

            // Anular o eliminar transaccion_abonado
            transaccion.estado = "A"; // Anulado
            transaccion.usuario = usuario;
            transaccion.descripcion = $"REVERSADO: {dto.Motivo}";

            // Validar estado de la factura
            var saldoRestante = detalles.Sum(d => d.montovalor_saldo ?? 0m);
            var totalFactura = detalles.Sum(d => d.montovalor ?? 0m);

            if (saldoRestante == totalFactura)
            {
                factura.estado = "A"; // Regresa a Abierto si no quedan abonos
                factura.fechapago = null;
                factura.recolectora = null;
            }
            else
            {
                factura.estado = "B"; // Sigue siendo Parcial si hay otros abonos vigentes
            }

            long? polizaId = null;
            if (tieneMovimientoBanco)
            {
                await _banTransaccionesService.AnularMovimientoAsync(
                    bancoCuentaId,
                    banKardexIdOriginal,
                    $"Reverso abono recibo {factura.numrecibo}",
                    usuario,
                    ct);
            }
            else
            {
                // Reverso por documento (F4): localiza la partida del abono, la
                // revierte vía motor único y descarta la pendiente encolada si
                // la hubiera. (La versión anterior invocaba una sobrecarga de
                // sp_con_revertir_poliza por documento que no existe en la BD.)
                var connection = _context.Database.GetDbConnection();
                var dbTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();

                polizaId = await IntegracionContableConfigSql.RevertirComprobanteAsync(
                    connection,
                    companyId,
                    ContabilidadModuloCaja,
                    new[] { ContabilidadDocumentoAbono },
                    transaccion.ide,
                    usuario,
                    dbTransaction,
                    ct);
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(null, "Abono reversado correctamente.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return ResponseModelDto.Fail($"Error al reversar el abono: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<ArqueoDto>> ListarAbonosDelDiaAsync(string? usuario, DateTime? fecha, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var targetFecha = fecha?.Date ?? DateTime.UtcNow.Date;
        var targetDateOnly = DateOnly.FromDateTime(targetFecha);

        var query = _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.tipotransaccion == "202" && t.fecha_docu == targetDateOnly && t.estado != "P");

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

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sql = "SELECT * FROM sp_obtener_cliente_saldo(@ClienteClave)";
        var saldo = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(sql, new { ClienteClave = clienteClave }, cancellationToken: ct));

        return saldo ?? 0m;
    }

    private async Task<string> ObtenerPeriodoActualCodigoAsync(CancellationToken ct)
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

        return $"{Convert.ToInt32(periodo.ano):D4}{Convert.ToInt32(periodo.mes):D2}";
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

    private async Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoBancarioCaptacionAsync(
        long bancoCuentaId,
        DateOnly fechaMovimiento,
        string descripcion,
        string referencia,
        string sourceDocument,
        decimal monto,
        IReadOnlyList<BanTransaccionContraLineaDto> contraCuentas,
        string usuario,
        CancellationToken ct)
    {
        return await _banTransaccionesService.RegistrarMovimientoAsync(
            bancoCuentaId,
            TipoTransaccionBancoDeposito,
            fechaMovimiento,
            descripcion,
            referencia,
            sourceDocument,
            1m,
            monto,
            contraCuentas,
            usuario,
            ct);
    }

    private async Task TryCompensarMovimientoBancarioAsync(long bancoCuentaId, long banKardexId, string usuario, CancellationToken ct)
    {
        try
        {
            await _banTransaccionesService.AnularMovimientoAsync(bancoCuentaId, banKardexId, "Compensación de abono fallido", usuario, ct);
        }
        catch
        {
            // Fail silent
        }
    }

    private static void AplicarReferenciaMovimientoBancario(transaccion_abonado transaccion, long bancoCuentaId, long banKardexId)
    {
        transaccion.docuaplicar = Convert.ToDecimal(banKardexId, CultureInfo.InvariantCulture);
        transaccion.trans_aplicar = $"{BancoMarkerPrefix}{bancoCuentaId.ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool TryObtenerReferenciaMovimientoBancario(transaccion_abonado transaccion, out long bancoCuentaId, out long banKardexId)
    {
        bancoCuentaId = 0;
        banKardexId = 0;
        if (string.IsNullOrWhiteSpace(transaccion.trans_aplicar) || !transaccion.docuaplicar.HasValue) return false;

        if (transaccion.trans_aplicar.StartsWith(BancoMarkerPrefix))
        {
            var suffix = transaccion.trans_aplicar[BancoMarkerPrefix.Length..];
            if (long.TryParse(suffix, out bancoCuentaId) && bancoCuentaId > 0)
            {
                banKardexId = Convert.ToInt64(transaccion.docuaplicar.Value);
                return true;
            }
        }
        return false;
    }

    public async Task<ReciboAbonoDto?> GenerarDatosReciboAsync(int transaccionId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        var transaccion = await _context.transaccion_abonados
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.company_id == companyId && t.ide == transaccionId, ct);

        if (transaccion is null)
            return null;

        var numRecibo = (int)(transaccion.recibo ?? 0);

        var factura = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.company_id == companyId && f.numrecibo == numRecibo, ct);

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
            .FirstOrDefaultAsync(c => c.maestro_cliente_clave == transaccion.cliente_clave, ct);

        var company = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        var lineas = detalles.Select(d => new ReciboAbonoLineaDto
        {
            Descripcion = d.descripcion ?? string.Empty,
            Moneda = "L.",
            Monto = d.montovalor ?? 0m
        }).ToList();

        // El total del recibo es el monto del abono, no el total de la factura
        var total = transaccion.creditos ?? lineas.Sum(l => l.Monto);
        var direccion = clienteConDetalle?.cliente_detalles.FirstOrDefault()?.detalle_cliente_direccion ?? string.Empty;

        var esPendiente = transaccion.estado == "P";

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
            EsPendiente = esPendiente
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
                TransaccionId = t.ide,
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

    /// <summary>
    /// Contracuentas CxC analíticas del movimiento bancario de un abono,
    /// resueltas por la matriz de integración según el modo configurado
    /// (F4 — reemplaza a la cuenta única de con_regla_integracion).
    /// </summary>
    private Task<IReadOnlyList<BanTransaccionContraLineaDto>> ConstruirContraCuentasCxcAsync(
        long companyId,
        IReadOnlyList<(string? ServicioCodigo, decimal Monto)> aplicaciones,
        int? categoriaServicioId,
        bool? conMedicion,
        string descripcion,
        string sourceDocument,
        CancellationToken ct)
    {
        return IntegracionContableConfigSql.ConstruirContraCuentasCxcAsync(
            _context.Database.GetDbConnection(),
            companyId,
            aplicaciones,
            categoriaServicioId,
            conMedicion,
            descripcion,
            sourceDocument,
            ct);
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

        var transaccion = new transaccion_abonado
        {
            company_id = companyId,
            cliente_clave = dto.ClienteClave.Trim(),
            recibo = factura.numrecibo,
            tipotransaccion = "202",
            estado = "P",
            fecha_docu = hoy,
            fecha_registro = hoy,
            tipo_partida = "002",
            creditos = dto.Monto,
            debitos = 0m,
            descripcion = $"Recibo pendiente de pago - Factura: {factura.numfactura ?? factura.numrecibo.ToString()}",
            usuario = usuario,
            periodo = periodo,
            tipo_servicio = "E",
            tasa = "0",
            ciclo = clienteInfo?.ciclos_id?.ToString(),
            ruta = clienteInfo?.maestro_cliente_indicativo_ruta,
            secuencia = clienteInfo?.maestro_cliente_secuencia,
            tiene_med = clienteInfo?.maestro_cliente_tiene_medidor == true ? "S" : "N"
        };

        _context.transaccion_abonados.Add(transaccion);
        await _context.SaveChangesAsync(ct);

        return ResponseModelDto.Ok(new GenerarReciboResponseDto
        {
            TransaccionId = transaccion.ide,
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

        return await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId
                && t.recibo == numRecibo
                && t.tipotransaccion == "202"
                && t.estado == "P")
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Select(t => new ReciboPendienteDto
            {
                TransaccionId = t.ide,
                NumFactura = factura.numfactura ?? numRecibo.ToString(),
                NumRecibo = numRecibo,
                Monto = t.creditos ?? 0m,
                FechaGenerado = t.fecha_docu.HasValue ? t.fecha_docu.Value.ToString("dd/MM/yyyy") : string.Empty,
                Operador = t.usuario ?? string.Empty
            })
            .ToListAsync(ct);
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
            TransaccionId = t.ide,
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

        var companyId = _currentCompanyService.GetCompanyId();

        var transaccion = await _context.transaccion_abonados
            .FirstOrDefaultAsync(t => t.company_id == companyId && t.ide == dto.TransaccionId && t.estado == "P", ct);

        if (transaccion is null)
            return ResponseModelDto.Fail("El recibo pendiente no existe o ya fue procesado/anulado.");

        transaccion.estado = "A";
        transaccion.motivo = dto.Motivo;
        transaccion.descripcion = $"ANULADO: {dto.Motivo} — {transaccion.descripcion}";
        transaccion.usuario = dto.Usuario;

        await _context.SaveChangesAsync(ct);
        return ResponseModelDto.Ok("Recibo pendiente anulado correctamente.");
    }
}
