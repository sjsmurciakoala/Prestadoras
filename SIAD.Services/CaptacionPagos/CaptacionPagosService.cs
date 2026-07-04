using System.Data;
using System.Globalization;
using Npgsql;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Cobranza;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.CaptacionPagos;

public class CaptacionPagosService : ICaptacionPagosService
{
    private readonly SiadDbContext _context;
    private readonly IBanTransaccionesService _banTransaccionesService;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly ICorteMasivoService _corteMasivoService;
    private const string ContabilidadModuloVentas = "VENTAS";
    private const string ContabilidadDocumentoRecibo = "REC";
    private const string ContabilidadDocumentoFactura = "FAC";
    private const long CaptacionMiscelaneoDocumentOffset = 1_000_000_000L;
    private const long CaptacionManualDocumentOffset = 2_000_000_000L;
    private const string BancoMarkerPrefix = "BANCO_CUENTA:";
    private const string TipoTransaccionBancoDeposito = "DEP";

    public CaptacionPagosService(
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
        var query = BuildArqueosBaseQuery(filtro);

        var items = await query
            .OrderByDescending(a => a.Fecha)
            .ToListAsync(ct);

        return items
            .Select(item => new ArqueoDto
            {
                Id = item.Id,
                Fecha = item.Fecha.ToDateTime(TimeOnly.MinValue),
                NumFactura = FormatearNumFactura(item),
                ClienteClave = item.ClienteClave,
                Banco = item.Banco,
                Usuario = item.Usuario,
                Estado = item.Estado,
                Monto = item.Monto,
                RowKey = item.Id.ToString()
            })
            .ToList();
    }

    public async Task<PagedResult<ArqueoDto>> ListarArqueosPagedAsync(
        CaptacionArqueoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var baseQuery = BuildArqueosBaseQuery(filtro);

        var totalCount = await baseQuery.CountAsync(ct);

        var sortKey = sortField?.Trim().ToLowerInvariant();
        var orderedQuery = sortKey switch
        {
            "fecha" => sortDesc
                ? baseQuery.OrderByDescending(a => a.Fecha)
                : baseQuery.OrderBy(a => a.Fecha),
            "numfactura" => sortDesc
                ? baseQuery.OrderByDescending(a => a.NumFactura ?? string.Empty)
                    .ThenByDescending(a => a.Recibo ?? 0m)
                : baseQuery.OrderBy(a => a.NumFactura ?? string.Empty)
                    .ThenByDescending(a => a.Recibo ?? 0m),
            "recibo" => sortDesc
                ? baseQuery.OrderByDescending(a => a.Recibo ?? 0m)
                : baseQuery.OrderBy(a => a.Recibo ?? 0m),
            "clienteclave" => sortDesc
                ? baseQuery.OrderByDescending(a => a.ClienteClave)
                : baseQuery.OrderBy(a => a.ClienteClave),
            "banco" => sortDesc
                ? baseQuery.OrderByDescending(a => a.Banco)
                : baseQuery.OrderBy(a => a.Banco),
            "usuario" => sortDesc
                ? baseQuery.OrderByDescending(a => a.Usuario)
                : baseQuery.OrderBy(a => a.Usuario),
            "estado" => sortDesc
                ? baseQuery.OrderByDescending(a => a.Estado)
                : baseQuery.OrderBy(a => a.Estado),
            "monto" => sortDesc
                ? baseQuery.OrderByDescending(a => a.Monto).ThenByDescending(a => a.Fecha)
                : baseQuery.OrderBy(a => a.Monto).ThenByDescending(a => a.Fecha),
            _ => baseQuery.OrderByDescending(a => a.Fecha)
        };

        skip = Math.Max(0, skip);
        take = take <= 0 ? 50 : Math.Min(take, 500);

        var items = await orderedQuery
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var resultItems = items
            .Select(item => new ArqueoDto
            {
                Id = item.Id,
                Fecha = item.Fecha.ToDateTime(TimeOnly.MinValue),
                NumFactura = FormatearNumFactura(item),
                ClienteClave = item.ClienteClave,
                Banco = item.Banco,
                Usuario = item.Usuario,
                Estado = item.Estado,
                Monto = item.Monto,
                RowKey = item.Id.ToString()
            })
            .ToList();

        return new PagedResult<ArqueoDto>(resultItems, totalCount);
    }

    private IQueryable<ArqueoBaseRow> BuildArqueosBaseQuery(CaptacionArqueoFilterDto? filtro)
    {
        var pagos = _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => (t.tipotransaccion == "201" || t.tipotransaccion == "202")
                        && t.estado != "P"
                        && (t.fecha_docu != null || t.fecha_registro != null));

        if (filtro?.FechaInicio is DateTime inicio)
        {
            var inicioDate = DateOnly.FromDateTime(inicio);
            pagos = pagos.Where(p => (p.fecha_docu ?? p.fecha_registro) >= inicioDate);
        }

        if (filtro?.FechaFin is DateTime fin)
        {
            var finDate = DateOnly.FromDateTime(fin).AddDays(1);
            pagos = pagos.Where(p => (p.fecha_docu ?? p.fecha_registro) < finDate);
        }

        return (from p in pagos
                join f in _context.facturas on p.recibo equals (decimal?)f.numrecibo into facturaJoin
                from f in facturaJoin.DefaultIfEmpty()
                select new ArqueoBaseRow
                {
                    Id = p.ide,
                    Fecha = (p.fecha_docu ?? p.fecha_registro)!.Value,
                    Recibo = p.recibo,
                    NumFactura = p.docufuente2 ?? f.numfactura,
                    ClienteClave = p.cliente_clave ?? string.Empty,
                    Banco = p.banco,
                    Usuario = p.usuario,
                    Estado = p.estado,
                    Monto = p.creditos ?? 0m
                });
    }

    private sealed class ArqueoBaseRow
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public decimal? Recibo { get; set; }
        public string? NumFactura { get; set; }
        public string ClienteClave { get; set; } = string.Empty;
        public string? Banco { get; set; }
        public string? Usuario { get; set; }
        public string? Estado { get; set; }
        public decimal Monto { get; set; }
    }

    private static string FormatearNumFactura(ArqueoBaseRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.NumFactura))
        {
            return row.NumFactura!.Trim();
        }

        if (row.Recibo.HasValue)
        {
            return row.Recibo.Value.ToString("0", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    public async Task<CaptacionHeaderDto?> ObtenerDetallePagoAsync(string numFactura, CancellationToken ct = default)
    {
        var factura = await ObtenerFacturaAsync(numFactura, ct);
        if (factura is null)
        {
            return null;
        }

        return new CaptacionHeaderDto
        {
            NumFactura = factura.numfactura ?? factura.numrecibo.ToString(),
            ClienteClave = factura.clientecodigo ?? string.Empty,
            Fecha = factura.fechaemision.HasValue
                ? factura.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                : DateTime.MinValue,
            Total = factura.saldototal ?? 0m,
            Estado = factura.estado ?? string.Empty,
            Banco = factura.recolectora,
            Usuario = factura.usuario
        };
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
        var factura = await ObtenerFacturaAsync(numFactura, ct);
        if (factura is null)
        {
            return Array.Empty<CaptacionDetailDto>();
        }

        var detalles = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == factura.id)
            .OrderBy(d => d.id)
            .Select(d => new
            {
                d.id,
                d.descripcion,
                d.montovalor,
                d.montovalor_saldo
            })
            .ToListAsync(ct);

        if (detalles.Count == 0)
        {
            return Array.Empty<CaptacionDetailDto>();
        }

        var response = new List<CaptacionDetailDto>();
        var saldoPendiente = detalles.Sum(d => d.montovalor_saldo ?? 0m);
        if (saldoPendiente > 0)
        {
            response.Add(new CaptacionDetailDto
            {
                Linea = 0,
                Servicio = "SALDO ANTERIOR",
                MontoValor = saldoPendiente
            });
        }

        var index = 1;
        foreach (var detalle in detalles)
        {
            response.Add(new CaptacionDetailDto
            {
                Linea = index++,
                Servicio = detalle.descripcion ?? string.Empty,
                MontoValor = detalle.montovalor ?? 0m
            });
        }

        return response;
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

        var factura = await ObtenerFacturaAsync(dto.NumFactura, ct);
        if (factura is null)
        {
            return ResponseModelDto.Fail("No se encontro la factura indicada.");
        }

        var clienteClave = factura.clientecodigo ?? dto.ClienteClave;
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return ResponseModelDto.Fail("La factura no tiene cliente asociado.");
        }

        var existe = await ExisteRegistroPagoAsync(dto.NumFactura, ct);
        if (existe)
        {
            return ResponseModelDto.Fail($"La factura {dto.NumFactura} ya tiene un pago registrado.");
        }

        var detalles = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .ToListAsync(ct);

        if (detalles.Count == 0)
        {
            return ResponseModelDto.Fail("La factura no tiene detalles asociados.");
        }

        var total = detalles.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m);
        if (total <= 0)
        {
            return ResponseModelDto.Fail("La factura no tiene saldo pendiente.");
        }

        // Montos por servicio ANTES de saldar el detalle: son la base de las
        // líneas CxC analíticas (efectivo y banco) de la integración por config.
        var aplicacionesContables = detalles
            .Select(d => (
                ServicioCodigo: ResolveServicioCodigoDetalle(d.tiposervicio, d.codigo),
                Monto: d.montovalor_saldo ?? d.montovalor ?? 0m))
            .Where(a => a.Monto > 0)
            .ToList();

        var integrarBancos = dto.BancoCuentaId.HasValue && dto.BancoCuentaId.Value > 0;
        var companyId = EnsureCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var banco = await ResolverBancoCodigoAsync(dto.BancoCuentaId, dto.Banco, ct);
        var fechaPago = dto.FechaPago?.Date ?? DateTime.UtcNow.Date;
        if (integrarBancos && fechaPago > DateTime.Today)
        {
            return ResponseModelDto.Fail("No se permiten pagos bancarios con fecha futura.");
        }

        var fechaHoy = DateOnly.FromDateTime(fechaPago);
        var contabilidadDocumentId = BuildContabilidadDocumentIdLectora(factura.numrecibo);
        var contabilidadDocumentNumber = string.IsNullOrWhiteSpace(factura.numfactura)
            ? $"REC-{factura.numrecibo}"
            : factura.numfactura.Trim();
        var contabilidadDescription = $"Cobro captacion factura {contabilidadDocumentNumber}";

        (long BanKardexId, decimal SaldoResultante)? movimientoBanco = null;
        if (integrarBancos)
        {
            try
            {
                var contraCuentasBanco = await ConstruirContraCuentasBancariasAsync(
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
                    total,
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
            foreach (var detalle in detalles)
            {
                var saldoActual = detalle.montovalor_saldo ?? detalle.montovalor ?? 0m;
                detalle.montovalor_saldo = saldoActual - saldoActual;
            }

            factura.estado = "C";
            factura.recolectora = banco;
            factura.fechapago = fechaHoy;
            factura.usuario = usuario;

            var clienteInfo = await _context.cliente_maestros
                .AsNoTracking()
                .Where(c => c.maestro_cliente_clave == clienteClave)
                .Select(c => new
                {
                    c.ciclos_id,
                    c.maestro_cliente_indicativo_ruta,
                    c.maestro_cliente_secuencia,
                    c.maestro_cliente_tiene_medidor
                })
                .FirstOrDefaultAsync(ct);

            var saldoActualCliente = await ObtenerSaldoClienteAsync(clienteClave, ct);
            var sesionCajaId = await _context.sesion_cajas
                .Where(s => s.usuario_apertura == usuario && s.estado == "ABIERTA")
                .Select(s => (int?)s.id)
                .FirstOrDefaultAsync(ct);

            var transaccion = new transaccion_abonado
            {
                company_id = _currentCompanyService.GetCompanyId(),
                caja_id = sesionCajaId,
                cliente_clave = clienteClave,
                recibo = factura.numrecibo,
                tipotransaccion = "201",
                fecha_docu = fechaHoy,
                tipo_partida = "002",
                banco = banco,
                descripcion = $"Pago comprobante de banco # {banco} :Recibo # :{factura.numrecibo}",
                debitos = 0,
                creditos = total,
                saldo = saldoActualCliente - total,
                tipo_servicio = "E",
                periodo = await ObtenerPeriodoActualCodigoAsync(ct),
                tasa = "0",
                estado = "C",
                fecha_registro = fechaHoy,
                ciclo = clienteInfo?.ciclos_id?.ToString(),
                ruta = clienteInfo?.maestro_cliente_indicativo_ruta,
                secuencia = clienteInfo?.maestro_cliente_secuencia,
                tiene_med = clienteInfo?.maestro_cliente_tiene_medidor == true ? "S" : "N",
                usuario = usuario,
                saldo_detalle = total
            };

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                AplicarReferenciaMovimientoBancario(
                    transaccion,
                    dto.BancoCuentaId.Value,
                    movimientoBanco.Value.BanKardexId);
            }

            _context.transaccion_abonados.Add(transaccion);
            await _context.SaveChangesAsync(ct);

            long? polizaId = null;
            int? polizaStatus = null;
            string? polizaEstado = null;

            if (!integrarBancos)
            {
                (polizaId, var encolada) = await GenerarComprobanteCaptacionConfigAsync(
                    companyId,
                    contabilidadDocumentId,
                    contabilidadDocumentNumber,
                    fechaHoy,
                    contabilidadDescription,
                    usuario,
                    aplicacionesContables,
                    factura.categoria_servicio_id,
                    factura.con_medicion,
                    cxcGeneral: false,
                    _context.Database.CurrentTransaction?.GetDbTransaction(),
                    ct);

                polizaStatus = polizaId.HasValue ? 1 : null;
                polizaEstado = polizaId.HasValue ? "POSTED" : (encolada ? "ENCOLADA" : null);
            }

            // Si el cliente queda sin saldo tras el pago, cancela las OT de corte (33)
            // pendientes y las saca de la reimpresión del corte.
            if (saldoActualCliente - total <= 0m)
            {
                await _corteMasivoService.CancelarOrdenesCorteClienteAsync(clienteClave, usuario, ct);
            }

            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(
                new
                {
                    factura.numfactura,
                    factura.numrecibo,
                    PolizaId = polizaId,
                    PolizaStatus = polizaStatus,
                    PolizaEstado = polizaEstado,
                    ContabilidadDocumentId = integrarBancos ? (long?)null : contabilidadDocumentId,
                    BancoCuentaId = dto.BancoCuentaId,
                    BanKardexId = movimientoBanco.HasValue ? movimientoBanco.Value.BanKardexId : (long?)null
                },
                "Pago registrado correctamente.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                var compensacionError = await TryCompensarMovimientoBancarioAsync(
                    dto.BancoCuentaId.Value,
                    movimientoBanco.Value.BanKardexId,
                    usuario,
                    "Rollback captacion lectora",
                    ct);

                if (!string.IsNullOrWhiteSpace(compensacionError))
                {
                    return ResponseModelDto.Fail(
                        $"Error al registrar el pago: {ex.Message}. " +
                        $"Adicionalmente, no se pudo compensar el movimiento bancario {movimientoBanco.Value.BanKardexId}: {compensacionError}");
                }
            }

            return ResponseModelDto.Fail($"Error al registrar el pago: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.NumFactura))
        {
            return ResponseModelDto.Fail("El numero de factura es requerido.");
        }

        var factura = await ObtenerFacturaAsync(dto.NumFactura.Trim(), ct);
        if (factura is null)
        {
            return ResponseModelDto.Fail("No se encontro la factura indicada.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ClienteClave) &&
            !string.Equals(factura.clientecodigo, dto.ClienteClave.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("La clave del cliente no coincide con la factura seleccionada.");
        }

        var detalles = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .ToListAsync(ct);

        if (detalles.Count == 0)
        {
            return ResponseModelDto.Fail("La factura no tiene detalles asociados.");
        }

        var companyId = EnsureCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var contabilidadDocumentId = BuildContabilidadDocumentIdLectora(factura.numrecibo);
        var transaccionPago = await _context.transaccion_abonados
            .Where(t => t.recibo == factura.numrecibo && t.tipotransaccion == "201")
            .OrderByDescending(t => t.ide)
            .FirstOrDefaultAsync(ct);
        var tieneMovimientoBanco = TryObtenerReferenciaMovimientoBancario(
            transaccionPago,
            out var bancoCuentaId,
            out var banKardexIdOriginal);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var detalle in detalles)
            {
                detalle.montovalor_saldo = detalle.montovalor ?? 0m;
            }

            factura.estado = "A";
            factura.recolectora = null;
            factura.fechapago = null;
            if (!string.IsNullOrWhiteSpace(dto.Usuario))
            {
                factura.usuario = dto.Usuario.Trim();
            }

            if (transaccionPago is not null)
            {
                _context.transaccion_abonados.Remove(transaccionPago);
            }

            long? polizaId = null;
            (long BanKardexIdAnulacion, decimal SaldoResultante)? anulacionBanco = null;

            if (tieneMovimientoBanco)
            {
                anulacionBanco = await _banTransaccionesService.AnularMovimientoAsync(
                    bancoCuentaId,
                    banKardexIdOriginal,
                    $"Reverso captacion recibo {factura.numrecibo}",
                    usuario,
                    ct);
            }
            else
            {
                polizaId = await RevertirComprobanteContableCaptacionAsync(
                    companyId,
                    contabilidadDocumentId,
                    usuario,
                    _context.Database.CurrentTransaction?.GetDbTransaction(),
                    ct);
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(
                new
                {
                    factura.numfactura,
                    factura.numrecibo,
                    PolizaId = polizaId,
                    PolizaStatus = polizaId.HasValue ? 0 : (int?)null,
                    PolizaEstado = polizaId.HasValue ? "DRAFT" : null,
                    ContabilidadDocumentId = polizaId.HasValue ? (long?)contabilidadDocumentId : null,
                    BancoCuentaId = tieneMovimientoBanco ? (long?)bancoCuentaId : null,
                    BanKardexIdOriginal = tieneMovimientoBanco ? (long?)banKardexIdOriginal : null,
                    BanKardexIdAnulacion = anulacionBanco.HasValue ? anulacionBanco.Value.BanKardexIdAnulacion : (long?)null
                },
                "Pago reversado correctamente.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return ResponseModelDto.Fail($"Error al reversar el pago: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<ReciboMiscelaneoDto>> ListarPagosMiscelaneosAsync(string? clienteClave, CancellationToken ct = default)
    {
        var query = _context.facturas
            .AsNoTracking()
            .Where(f => f.tipofactura == "R");

        if (!string.IsNullOrWhiteSpace(clienteClave))
        {
            var clave = clienteClave.Trim();
            query = query.Where(f => f.clientecodigo == clave);
        }

        return await query
            .OrderByDescending(f => f.fechaemision)
            .Take(200)
            .Select(f => new ReciboMiscelaneoDto
            {
                Recibo = f.numrecibo,
                Cliente = f.clientecodigo ?? string.Empty,
                Fecha = f.fechaemision.HasValue
                    ? f.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                    : DateTime.MinValue,
                Total = f.saldototal ?? 0m,
                Estado = f.estado ?? string.Empty
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ReciboMiscelaneoDto>> ListarPagosMiscelaneosPagedAsync(
        string? clienteClave,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var query = _context.facturas
            .AsNoTracking()
            .Where(f => f.tipofactura == "R");

        if (!string.IsNullOrWhiteSpace(clienteClave))
        {
            var clave = clienteClave.Trim();
            query = query.Where(f => f.clientecodigo == clave);
        }

        var totalCount = await query.CountAsync(ct);

        var sortKey = sortField?.Trim().ToLowerInvariant();
        query = sortKey switch
        {
            "recibo" => sortDesc ? query.OrderByDescending(f => f.numrecibo) : query.OrderBy(f => f.numrecibo),
            "fecha" => sortDesc ? query.OrderByDescending(f => f.fechaemision) : query.OrderBy(f => f.fechaemision),
            "cliente" => sortDesc ? query.OrderByDescending(f => f.clientecodigo) : query.OrderBy(f => f.clientecodigo),
            "total" => sortDesc ? query.OrderByDescending(f => f.saldototal) : query.OrderBy(f => f.saldototal),
            "estado" => sortDesc ? query.OrderByDescending(f => f.estado) : query.OrderBy(f => f.estado),
            _ => query.OrderByDescending(f => f.fechaemision).ThenByDescending(f => f.numrecibo)
        };

        skip = Math.Max(0, skip);
        take = take <= 0 ? 50 : Math.Min(take, 500);

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(f => new ReciboMiscelaneoDto
            {
                Recibo = f.numrecibo,
                Cliente = f.clientecodigo ?? string.Empty,
                Fecha = f.fechaemision.HasValue
                    ? f.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                    : DateTime.MinValue,
                Total = f.saldototal ?? 0m,
                Estado = f.estado ?? string.Empty
            })
            .ToListAsync(ct);

        return new PagedResult<ReciboMiscelaneoDto>(items, totalCount);
    }

    public async Task<IReadOnlyList<BusquedaFacturaDto>> BuscarFacturasAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<BusquedaFacturaDto>();
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sql = @"
            SELECT
                numfactura AS ""NumFactura"",
                cliente_clave AS ""ClienteClave"",
                cliente_nombre AS ""ClienteNombre"",
                fecha AS ""Fecha"",
                total AS ""Total"",
                estado AS ""Estado""
            FROM get_matching_invoices(@Term)
        ";

        try
        {
            var data = await connection.QueryAsync<BusquedaFacturaDto>(
                new CommandDefinition(sql, new { Term = term.Trim() }, cancellationToken: ct));

            var result = data
                .Where(item => !string.IsNullOrWhiteSpace(item.NumFactura))
                .ToList();

            if (result.Count > 0)
            {
                return result;
            }
        }
        catch (PostgresException)
        {
            // Fallback to EF query below when legacy function/columns are missing.
        }

        var filtro = term.Trim();
        var filtroLike = $"%{filtro}%";
        var isNumero = int.TryParse(filtro, out var numero);

        var query = from f in _context.facturas.AsNoTracking()
                    join c in _context.cliente_maestros.AsNoTracking()
                        on f.clientecodigo equals c.maestro_cliente_clave into clientes
                    from c in clientes.DefaultIfEmpty()
                    where (f.numfactura != null && EF.Functions.ILike(f.numfactura, filtroLike))
                          || (isNumero && f.numrecibo == numero)
                          || (f.clientecodigo != null && EF.Functions.ILike(f.clientecodigo, filtroLike))
                          || (c != null && EF.Functions.ILike(c.maestro_cliente_nombre, filtroLike))
                    orderby f.fechaemision descending, f.numrecibo descending
                    select new BusquedaFacturaDto
                    {
                        NumFactura = f.numfactura ?? f.numrecibo.ToString(),
                        ClienteClave = f.clientecodigo ?? string.Empty,
                        ClienteNombre = c != null ? c.maestro_cliente_nombre : string.Empty,
                        Fecha = f.fechaemision.HasValue
                            ? f.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                            : DateTime.MinValue,
                        Total = f.saldototal ?? 0m,
                        Estado = f.estado ?? string.Empty
                    };

        return await query.Take(20).ToListAsync(ct);
    }

    public async Task<bool> ExisteRegistroPagoAsync(string numFactura, CancellationToken ct = default)
    {
        var factura = await ObtenerFacturaAsync(numFactura, ct);
        if (factura is null)
        {
            return false;
        }

        return await _context.transaccion_abonados
            .AsNoTracking()
            .AnyAsync(t => t.recibo == factura.numrecibo && t.tipotransaccion == "201", ct);
    }

    // ==================== POSTEO MANUAL ====================
    public async Task<IReadOnlyList<SaldoPosteoManualDto>> ObtenerSaldosPosteoManualAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<SaldoPosteoManualDto>();
        }

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sql = @"
            SELECT
                r_ide AS ""Ide"",
                r_recibo AS ""Recibo"",
                r_recibo_anterior AS ""ReciboAnterior"",
                r_tiposervicio AS ""TipoServicio"",
                r_monto AS ""Monto"",
                r_monto_distribuido AS ""MontoDistribuido""
            FROM fn_getclientesaldos_posteomanual(@ClienteClave)
        ";

        var rows = await connection.QueryAsync<SaldoPosteoManualLegacyRow>(
            new CommandDefinition(sql, new { ClienteClave = clienteClave.Trim() }, cancellationToken: ct));

        var result = new List<SaldoPosteoManualDto>();
        foreach (var group in rows.GroupBy(r => r.Recibo))
        {
            var item = new SaldoPosteoManualDto
            {
                ReciboActual = group.Key,
                ReciboAnterior = group.First().ReciboAnterior
            };

            decimal total = 0m;
            foreach (var row in group)
            {
                var monto = row.MontoDistribuido ?? row.Monto ?? 0m;
                total += monto;
                switch (MapTipoServicio(row.TipoServicio))
                {
                    case "AGUA":
                        item.DistribucionAgua += monto;
                        item.DetalleAguaId ??= row.Ide;
                        break;
                    case "ALCANTARILLADO":
                        item.DistribucionAlcantarillado += monto;
                        item.DetalleAlcantarilladoId ??= row.Ide;
                        break;
                    default:
                        item.DistribucionOtros += monto;
                        item.DetalleOtrosId ??= row.Ide;
                        break;
                }
            }

            item.Valor = total;
            item.DetalleId = group.First().Ide;
            result.Add(item);
        }

        return result;
    }

    public async Task<ResponseModelDto> RegistrarPagoManualAsync(PagoManualCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es requerida.");
        }

        if (dto.NumRecibo <= 0)
        {
            return ResponseModelDto.Fail("El recibo es requerido.");
        }

        var reciboDecimal = Convert.ToDecimal(dto.NumRecibo, CultureInfo.InvariantCulture);
        var yaPosteado = await _context.transaccion_abonados
            .AsNoTracking()
            .AnyAsync(t => t.recibo == reciboDecimal && t.tipotransaccion == "201", ct);
        if (yaPosteado)
        {
            return ResponseModelDto.Fail("El recibo ya tiene un posteo aplicado.");
        }

        var distribuciones = (dto.Distribucion ?? new List<PagoManualDistribucionDto>())
            .Where(d => d != null && d.Id > 0 && d.ValorDistribuido > 0)
            .ToList();

        if (distribuciones.Count == 0)
        {
            return ResponseModelDto.Fail("Debe indicar al menos una distribucion con monto mayor a cero.");
        }

        var totalDistribucion = distribuciones.Sum(d => d.ValorDistribuido);
        if (totalDistribucion <= 0)
        {
            return ResponseModelDto.Fail("El valor distribuido debe ser mayor a cero.");
        }

        if (dto.Valor <= 0)
        {
            dto.Valor = totalDistribucion;
        }

        var clienteClave = dto.ClienteClave.Trim();
        var facturaPrevia = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.numrecibo == dto.NumRecibo, ct);
        if (facturaPrevia is null)
        {
            return ResponseModelDto.Fail("No se encontro el recibo indicado.");
        }

        if (!string.Equals(facturaPrevia.clientecodigo, clienteClave, StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("El recibo no corresponde al cliente indicado.");
        }

        if (string.Equals(facturaPrevia.estado, "C", StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("El recibo ya esta marcado como pagado.");
        }

        var detalleIds = distribuciones
            .Select(d => Convert.ToInt32(d.Id, CultureInfo.InvariantCulture))
            .Distinct()
            .ToList();

        var detallesDistribucion = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == facturaPrevia.id && detalleIds.Contains(d.id))
            .Select(d => new
            {
                d.id,
                d.tiposervicio,
                d.codigo,
                d.descripcion,
                d.montovalor,
                d.montovalor_saldo
            })
            .ToListAsync(ct);

        if (detallesDistribucion.Count != detalleIds.Count)
        {
            return ResponseModelDto.Fail("No se pudo resolver la distribucion del recibo para registrar el posteo manual.");
        }

        var detalleLookup = detallesDistribucion.ToDictionary(d => Convert.ToInt64(d.id, CultureInfo.InvariantCulture));
        foreach (var distribucion in distribuciones)
        {
            if (!detalleLookup.TryGetValue(distribucion.Id, out var detalle))
            {
                return ResponseModelDto.Fail($"No se encontro el detalle {distribucion.Id} para el posteo manual.");
            }

            var saldoDisponible = detalle.montovalor_saldo ?? detalle.montovalor ?? 0m;
            if (distribucion.ValorDistribuido > saldoDisponible)
            {
                return ResponseModelDto.Fail(
                    $"La distribucion del detalle {distribucion.Id} excede el saldo pendiente del recibo.");
            }
        }

        var saldoPendienteRecibo = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == facturaPrevia.id)
            .Select(d => d.montovalor_saldo ?? d.montovalor ?? 0m)
            .ToListAsync(ct);

        if (Math.Abs(totalDistribucion - saldoPendienteRecibo.Sum()) > 0.01m)
        {
            return ResponseModelDto.Fail("La distribucion debe cubrir el saldo total del recibo.");
        }

        var integrarBancos = dto.BancoCuentaId.HasValue && dto.BancoCuentaId.Value > 0;
        var companyId = EnsureCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var fechaContable = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var contabilidadDocumentId = BuildContabilidadDocumentIdManual(dto.NumRecibo);
        var contabilidadDocumentNumber = $"MAN-{dto.NumRecibo}";
        var contabilidadDescription = $"Cobro manual captacion recibo {dto.NumRecibo}";
        var bancoCodigo = await ResolverBancoCodigoAsync(dto.BancoCuentaId, dto.Banco, ct) ?? "EFECTIVO";

        // Montos por servicio de la distribución: base de las líneas CxC
        // analíticas (efectivo y banco) de la integración por config.
        var aplicacionesContables = distribuciones
            .Select(d => (
                ServicioCodigo: ResolveServicioCodigoDetalle(
                    detalleLookup[d.Id].tiposervicio,
                    detalleLookup[d.Id].codigo),
                Monto: d.ValorDistribuido))
            .Where(a => a.Monto > 0)
            .ToList();

        (long BanKardexId, decimal SaldoResultante)? movimientoBanco = null;
        if (integrarBancos)
        {
            try
            {
                var contraCuentasBanco = await ConstruirContraCuentasBancariasAsync(
                    aplicacionesContables,
                    facturaPrevia.categoria_servicio_id,
                    facturaPrevia.con_medicion,
                    contabilidadDescription,
                    contabilidadDocumentNumber,
                    ct);

                movimientoBanco = await RegistrarMovimientoBancarioCaptacionAsync(
                    dto.BancoCuentaId!.Value,
                    fechaContable,
                    contabilidadDescription,
                    contabilidadDocumentNumber,
                    contabilidadDocumentNumber,
                    dto.Valor,
                    contraCuentasBanco,
                    usuario,
                    ct);
            }
            catch (Exception ex)
            {
                return ResponseModelDto.Fail($"Error al registrar movimiento bancario manual: {ex.Message}");
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Lock factura row to prevent concurrent duplicate postings.
            var factura = await _context.facturas
                .FromSqlInterpolated($@"SELECT * FROM factura WHERE numrecibo = {dto.NumRecibo} AND clientecodigo = {clienteClave} FOR UPDATE")
                .FirstOrDefaultAsync(ct);

            if (factura is null)
            {
                throw new InvalidOperationException("No se pudo bloquear el recibo para registrar el posteo manual.");
            }

            if (string.Equals(factura.estado, "C", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El recibo ya esta marcado como pagado.");
            }

            var posteoExistente = await _context.transaccion_abonados
                .AnyAsync(t => t.recibo == reciboDecimal && t.tipotransaccion == "201", ct);
            if (posteoExistente)
            {
                throw new InvalidOperationException("El recibo ya tiene un posteo aplicado.");
            }

            // Apply distribution to detail balances.
            var detallesTracked = await _context.factura_detalles
                .Where(d => d.factura_id == factura.id && detalleIds.Contains(d.id))
                .ToListAsync(ct);
            foreach (var dist in distribuciones)
            {
                var det = detallesTracked.FirstOrDefault(d => d.id == Convert.ToInt32(dist.Id, CultureInfo.InvariantCulture));
                if (det is not null)
                {
                    det.montovalor_saldo = (det.montovalor_saldo ?? det.montovalor ?? 0m) - dist.ValorDistribuido;
                }
            }

            // Mark invoice as paid.
            factura.estado = "C";
            factura.fechapago = fechaContable;
            factura.recolectora = bancoCodigo;
            factura.usuario = usuario;

            // Build transaccion_abonado directly (replaces sp_registrar_posteo_manual).
            var clienteInfo = await _context.cliente_maestros
                .AsNoTracking()
                .Where(c => c.maestro_cliente_clave == clienteClave)
                .Select(c => new
                {
                    c.ciclos_id,
                    c.maestro_cliente_indicativo_ruta,
                    c.maestro_cliente_secuencia,
                    c.maestro_cliente_tiene_medidor
                })
                .FirstOrDefaultAsync(ct);

            var saldoActualCliente = await ObtenerSaldoClienteAsync(clienteClave, ct);
            var descripcion = $"Pago comprobante de banco # {bancoCodigo} :Recibo # :{dto.NumRecibo}";
            var sesionCajaId = await _context.sesion_cajas
                .Where(s => s.usuario_apertura == usuario && s.estado == "ABIERTA")
                .Select(s => (int?)s.id)
                .FirstOrDefaultAsync(ct);

            var transaccion = new transaccion_abonado
            {
                company_id = _currentCompanyService.GetCompanyId(),
                caja_id = sesionCajaId,
                cliente_clave = clienteClave,
                recibo = dto.NumRecibo,
                tipotransaccion = "201",
                fecha_docu = fechaContable,
                tipo_partida = "002",
                banco = bancoCodigo,
                descripcion = descripcion,
                debitos = 0,
                creditos = dto.Valor,
                saldo = saldoActualCliente - dto.Valor,
                tipo_servicio = "E",
                periodo = await ObtenerPeriodoActualCodigoAsync(ct),
                tasa = "0",
                estado = "C",
                fecha_registro = fechaContable,
                ciclo = clienteInfo?.ciclos_id?.ToString(),
                ruta = clienteInfo?.maestro_cliente_indicativo_ruta,
                secuencia = clienteInfo?.maestro_cliente_secuencia,
                tiene_med = clienteInfo?.maestro_cliente_tiene_medidor == true ? "S" : "N",
                usuario = usuario,
                saldo_detalle = dto.Valor
            };

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                AplicarReferenciaMovimientoBancario(
                    transaccion,
                    dto.BancoCuentaId.Value,
                    movimientoBanco.Value.BanKardexId);
            }

            _context.transaccion_abonados.Add(transaccion);
            await _context.SaveChangesAsync(ct);

            long? polizaId = null;
            int? polizaStatus = null;
            string? polizaEstado = null;
            if (!integrarBancos)
            {
                (polizaId, var encolada) = await GenerarComprobanteCaptacionConfigAsync(
                    companyId,
                    contabilidadDocumentId,
                    contabilidadDocumentNumber,
                    fechaContable,
                    contabilidadDescription,
                    usuario,
                    aplicacionesContables,
                    facturaPrevia.categoria_servicio_id,
                    facturaPrevia.con_medicion,
                    cxcGeneral: false,
                    _context.Database.CurrentTransaction?.GetDbTransaction(),
                    ct);

                polizaStatus = polizaId.HasValue ? 1 : null;
                polizaEstado = polizaId.HasValue ? "POSTED" : (encolada ? "ENCOLADA" : null);
            }

            // Si el cliente queda sin saldo tras el posteo, cancela las OT de corte (33)
            // pendientes y las saca de la reimpresión del corte.
            if (saldoActualCliente - dto.Valor <= 0m)
            {
                await _corteMasivoService.CancelarOrdenesCorteClienteAsync(clienteClave, usuario, ct);
            }

            await transaction.CommitAsync(ct);

            return ResponseModelDto.Ok(
                new
                {
                    dto.ClienteClave,
                    dto.NumRecibo,
                    PolizaId = polizaId,
                    PolizaStatus = polizaStatus,
                    PolizaEstado = polizaEstado,
                    ContabilidadDocumentId = polizaId.HasValue ? (long?)contabilidadDocumentId : null,
                    BancoCuentaId = dto.BancoCuentaId,
                    BanKardexId = movimientoBanco.HasValue ? movimientoBanco.Value.BanKardexId : (long?)null
                },
                "Posteo manual registrado correctamente.");
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(ct);
            }
            catch
            {
                // Ignore rollback failures to preserve original error.
            }

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                var compensacionError = await TryCompensarMovimientoBancarioAsync(
                    dto.BancoCuentaId.Value,
                    movimientoBanco.Value.BanKardexId,
                    usuario,
                    "Rollback captacion manual",
                    ct);

                if (!string.IsNullOrWhiteSpace(compensacionError))
                {
                    return ResponseModelDto.Fail(
                        $"Error al registrar posteo manual: {ex.Message}. " +
                        $"Adicionalmente, no se pudo compensar el movimiento bancario {movimientoBanco.Value.BanKardexId}: {compensacionError}");
                }
            }

            return ResponseModelDto.Fail($"Error al registrar posteo manual: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> ReversarPagoManualAsync(ReversoManualRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es requerida.");
        }

        if (dto.Recibo <= 0)
        {
            return ResponseModelDto.Fail("El numero de recibo es requerido.");
        }

        var companyId = EnsureCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var contabilidadDocumentId = BuildContabilidadDocumentIdManual(dto.Recibo);
        var reciboDecimal = Convert.ToDecimal(dto.Recibo, CultureInfo.InvariantCulture);
        var factura = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.numrecibo == dto.Recibo, ct);
        if (factura is null)
        {
            return ResponseModelDto.Fail("No se encontro el recibo indicado.");
        }

        if (!string.Equals(factura.clientecodigo, dto.ClienteClave.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("El recibo no corresponde al cliente indicado.");
        }

        var transaccionPago = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.recibo == reciboDecimal && t.tipotransaccion == "201")
            .OrderByDescending(t => t.ide)
            .FirstOrDefaultAsync(ct);
        if (transaccionPago is null)
        {
            return ResponseModelDto.Fail("No existe un posteo manual registrado para el recibo indicado.");
        }

        var tieneMovimientoBanco = TryObtenerReferenciaMovimientoBancario(
            transaccionPago,
            out var bancoCuentaId,
            out var banKardexIdOriginal);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Lock factura row to prevent concurrent reversal.
            var facturaTracked = await _context.facturas
                .FromSqlInterpolated($@"SELECT * FROM factura WHERE numrecibo = {dto.Recibo} AND clientecodigo = {dto.ClienteClave.Trim()} FOR UPDATE")
                .FirstOrDefaultAsync(ct);

            if (facturaTracked is null)
            {
                throw new InvalidOperationException("No se pudo bloquear el recibo para reversar el posteo manual.");
            }

            // Remove payment transactions directly (replaces sp_reversar_posteo_manual).
            var transaccionesPago = await _context.transaccion_abonados
                .Where(t => t.recibo == reciboDecimal && t.tipotransaccion == "201")
                .ToListAsync(ct);
            if (transaccionesPago.Count > 0)
            {
                _context.transaccion_abonados.RemoveRange(transaccionesPago);
            }

            // Restore detail balances.
            var detalles = await _context.factura_detalles
                .Where(d => d.factura_id == facturaTracked.id)
                .ToListAsync(ct);
            foreach (var detalle in detalles)
            {
                detalle.montovalor_saldo = detalle.montovalor ?? 0m;
            }

            // Reopen invoice.
            facturaTracked.estado = "A";
            facturaTracked.fechapago = null;
            facturaTracked.recolectora = null;
            facturaTracked.usuario = usuario;

            long? polizaId = null;
            (long BanKardexIdAnulacion, decimal SaldoResultante)? anulacionBanco = null;

            if (tieneMovimientoBanco)
            {
                anulacionBanco = await _banTransaccionesService.AnularMovimientoAsync(
                    bancoCuentaId,
                    banKardexIdOriginal,
                    $"Reverso captacion manual recibo {dto.Recibo}",
                    usuario,
                    ct);
            }
            else
            {
                polizaId = await RevertirComprobanteContableCaptacionAsync(
                    companyId,
                    contabilidadDocumentId,
                    usuario,
                    _context.Database.CurrentTransaction?.GetDbTransaction(),
                    ct);
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(
                new
                {
                    dto.ClienteClave,
                    dto.Recibo,
                    PolizaId = polizaId,
                    PolizaStatus = polizaId.HasValue ? 0 : (int?)null,
                    PolizaEstado = polizaId.HasValue ? "DRAFT" : null,
                    ContabilidadDocumentId = polizaId.HasValue ? (long?)contabilidadDocumentId : null,
                    BancoCuentaId = tieneMovimientoBanco ? (long?)bancoCuentaId : null,
                    BanKardexIdOriginal = tieneMovimientoBanco ? (long?)banKardexIdOriginal : null,
                    BanKardexIdAnulacion = anulacionBanco.HasValue ? anulacionBanco.Value.BanKardexIdAnulacion : (long?)null
                },
                "Posteo manual reversado correctamente."
            );
        }
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync(ct);
            }
            catch
            {
                // Ignore rollback failures to preserve original error.
            }

            return ResponseModelDto.Fail($"Error al reversar posteo manual: {ex.Message}");
        }
    }

    // ==================== POSTEO MISCELANEOS ====================
    public async Task<IReadOnlyList<ReciboMiscelaneoDetalleDto>> ObtenerDetalleReciboMiscelaneoAsync(long recibo, CancellationToken ct = default)
    {
        if (recibo <= 0)
        {
            return Array.Empty<ReciboMiscelaneoDetalleDto>();
        }

        var factura = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.numrecibo == recibo, ct);

        if (factura is null)
        {
            return Array.Empty<ReciboMiscelaneoDetalleDto>();
        }

        return await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == factura.id)
            .OrderBy(d => d.id)
            .Select(d => new ReciboMiscelaneoDetalleDto
            {
                Linea = d.id,
                Concepto = d.descripcion ?? string.Empty,
                Monto = d.montovalor ?? 0m
            })
            .ToListAsync(ct);
    }

    public async Task<ResponseModelDto> RegistrarPagoMiscelaneoAsync(PagoMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es requerida.");
        }

        if (dto.Recibo <= 0)
        {
            return ResponseModelDto.Fail("El numero de recibo es requerido.");
        }

        var integrarBancos = dto.BancoCuentaId.HasValue && dto.BancoCuentaId.Value > 0;
        var clienteClave = dto.ClienteClave.Trim();
        var companyId = EnsureCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var banco = await ResolverBancoCodigoAsync(dto.BancoCuentaId, dto.Banco, ct);
        var fechaHoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var reciboDecimal = Convert.ToDecimal(dto.Recibo, CultureInfo.InvariantCulture);
        var contabilidadDocumentId = BuildContabilidadDocumentIdMiscelaneo(dto.Recibo);
        var contabilidadDocumentNumber = $"MIS-{dto.Recibo}";
        var contabilidadDescription = $"Cobro miscelaneo captacion recibo {dto.Recibo}";
        (long BanKardexId, decimal SaldoResultante)? movimientoBanco = null;

        var facturaPrevia = await _context.facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.numrecibo == dto.Recibo, ct);

        if (facturaPrevia is null)
        {
            return ResponseModelDto.Fail("No se encontro el recibo indicado.");
        }

        if (!string.Equals(facturaPrevia.clientecodigo, clienteClave, StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("El recibo no corresponde al cliente indicado.");
        }

        if (string.Equals(facturaPrevia.estado, "C", StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("El recibo ya esta marcado como pagado.");
        }

        var yaPosteado = await _context.transaccion_abonados
            .AsNoTracking()
            .AnyAsync(t => t.recibo == reciboDecimal && t.tipotransaccion == "201", ct);
        if (yaPosteado)
        {
            return ResponseModelDto.Fail($"El recibo {dto.Recibo} ya tiene un pago registrado.");
        }

        var detallesPrevios = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == facturaPrevia.id)
            .ToListAsync(ct);

        if (detallesPrevios.Count == 0)
        {
            return ResponseModelDto.Fail("El recibo no tiene detalles asociados.");
        }

        var total = facturaPrevia.saldototal ?? 0m;
        if (total <= 0)
        {
            return ResponseModelDto.Fail("El recibo no tiene saldo pendiente.");
        }

        if (integrarBancos)
        {
            try
            {
                // El cobro de un misceláneo salda la CxC que dejó su emisión
                // (Banco / CxC): contracuenta CxC general de la matriz de
                // integración — el ingreso ya se reconoció al emitir el recibo.
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                var cuentaCxc = await IntegracionContableConfigSql.ResolverCuentaAsync(
                    connection, companyId, "CXC", transaction: null, ct);

                var contraCuentasBanco = new List<BanTransaccionContraLineaDto>
                {
                    new()
                    {
                        CuentaId = cuentaCxc,
                        Monto = total,
                        Descripcion = contabilidadDescription,
                        SourceDocument = contabilidadDocumentNumber
                    }
                };

                movimientoBanco = await RegistrarMovimientoBancarioCaptacionAsync(
                    dto.BancoCuentaId!.Value,
                    fechaHoy,
                    contabilidadDescription,
                    contabilidadDocumentNumber,
                    contabilidadDocumentNumber,
                    total,
                    contraCuentasBanco,
                    usuario,
                    ct);
            }
            catch (Exception ex)
            {
                return ResponseModelDto.Fail($"Error al registrar movimiento bancario miscelaneo: {ex.Message}");
            }
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Lock the invoice row to avoid concurrent duplicate postings for the same receipt.
            var factura = await _context.facturas
                .FromSqlInterpolated($@"SELECT * FROM factura WHERE numrecibo = {dto.Recibo} FOR UPDATE")
                .FirstOrDefaultAsync(ct);

            if (factura is null)
            {
                throw new InvalidOperationException("No se encontro el recibo indicado.");
            }

            if (!string.Equals(factura.clientecodigo, clienteClave, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El recibo no corresponde al cliente indicado.");
            }

            if (string.Equals(factura.estado, "C", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El recibo ya esta marcado como pagado.");
            }

            yaPosteado = await _context.transaccion_abonados
                .AnyAsync(t => t.recibo == reciboDecimal && t.tipotransaccion == "201", ct);
            if (yaPosteado)
            {
                throw new InvalidOperationException($"El recibo {dto.Recibo} ya tiene un pago registrado.");
            }

            var detallesFactura = await _context.factura_detalles
                .Where(d => d.factura_id == factura.id)
                .ToListAsync(ct);

            if (detallesFactura.Count == 0)
            {
                throw new InvalidOperationException("El recibo no tiene detalles asociados.");
            }

            total = factura.saldototal ?? 0m;
            if (total <= 0)
            {
                throw new InvalidOperationException("El recibo no tiene saldo pendiente.");
            }

            foreach (var detalle in detallesFactura)
            {
                var saldoDetalleActual = detalle.montovalor_saldo ?? detalle.montovalor ?? 0m;
                detalle.montovalor_saldo = saldoDetalleActual - saldoDetalleActual;
            }

            factura.estado = "C";
            factura.recolectora = banco;
            factura.fechapago = fechaHoy;
            factura.usuario = usuario;

            var saldoActual = await ObtenerSaldoClienteAsync(clienteClave, ct);
            var sesionCajaId = await _context.sesion_cajas
                .Where(s => s.usuario_apertura == usuario && s.estado == "ABIERTA")
                .Select(s => (int?)s.id)
                .FirstOrDefaultAsync(ct);

            var transaccion = new transaccion_abonado
            {
                company_id = _currentCompanyService.GetCompanyId(),
                caja_id = sesionCajaId,
                cliente_clave = clienteClave,
                recibo = factura.numrecibo,
                tipotransaccion = "201",
                docufuente = factura.id,
                docufuente2 = factura.numfactura,
                fecha_docu = fechaHoy,
                tipo_partida = "01",
                banco = banco,
                descripcion = $"Pago comprobante de banco # {banco} :Recibo # :{factura.numrecibo}",
                plazo = 30,
                debitos = 0,
                creditos = total,
                saldo = saldoActual - total,
                tipo_servicio = "E",
                periodo = await ObtenerPeriodoActualCodigoAsync(ct),
                tasa = "",
                estado = "C",
                fecha_registro = fechaHoy,
                usuario = usuario,
                saldo_detalle = total
            };

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                AplicarReferenciaMovimientoBancario(
                    transaccion,
                    dto.BancoCuentaId.Value,
                    movimientoBanco.Value.BanKardexId);
            }

            _context.transaccion_abonados.Add(transaccion);
            await _context.SaveChangesAsync(ct);

            long? polizaId = null;
            int? polizaStatus = null;
            string? polizaEstado = null;
            if (!integrarBancos)
            {
                (polizaId, var encolada) = await GenerarComprobanteCaptacionConfigAsync(
                    companyId,
                    contabilidadDocumentId,
                    contabilidadDocumentNumber,
                    fechaHoy,
                    contabilidadDescription,
                    usuario,
                    new List<(string? ServicioCodigo, decimal Monto)> { (null, total) },
                    categoriaServicioId: null,
                    conMedicion: null,
                    cxcGeneral: true,
                    _context.Database.CurrentTransaction?.GetDbTransaction(),
                    ct);

                polizaStatus = polizaId.HasValue ? 1 : null;
                polizaEstado = polizaId.HasValue ? "POSTED" : (encolada ? "ENCOLADA" : null);
            }

            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(
                new
                {
                    factura.numrecibo,
                    factura.clientecodigo,
                    PolizaId = polizaId,
                    PolizaStatus = polizaStatus,
                    PolizaEstado = polizaEstado,
                    ContabilidadDocumentId = polizaId.HasValue ? (long?)contabilidadDocumentId : null,
                    BancoCuentaId = dto.BancoCuentaId,
                    BanKardexId = movimientoBanco.HasValue ? movimientoBanco.Value.BanKardexId : (long?)null
                },
                "Pago miscelaneo registrado correctamente.");
        }
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync(ct);
            }
            catch
            {
                // Ignore rollback failures to preserve original error.
            }

            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                var compensacionError = await TryCompensarMovimientoBancarioAsync(
                    dto.BancoCuentaId.Value,
                    movimientoBanco.Value.BanKardexId,
                    usuario,
                    "Rollback captacion miscelaneo",
                    ct);

                if (!string.IsNullOrWhiteSpace(compensacionError))
                {
                    return ResponseModelDto.Fail(
                        $"Error al registrar pago miscelaneo: {ex.Message}. " +
                        $"Adicionalmente, no se pudo compensar el movimiento bancario {movimientoBanco.Value.BanKardexId}: {compensacionError}");
                }
            }

            return ResponseModelDto.Fail($"Error al registrar pago miscelaneo: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> ReversarPagoMiscelaneoAsync(ReversoMiscelaneoRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Recibo <= 0)
        {
            return ResponseModelDto.Fail("El numero de recibo es requerido.");
        }

        var factura = await _context.facturas
            .FirstOrDefaultAsync(f => f.numrecibo == dto.Recibo, ct);

        if (factura is null)
        {
            return ResponseModelDto.Fail($"No se encontro el recibo {dto.Recibo}.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ClienteClave) &&
            !string.Equals(factura.clientecodigo, dto.ClienteClave.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ResponseModelDto.Fail("La clave del cliente no coincide con el recibo seleccionado.");
        }

        var companyId = EnsureCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var contabilidadDocumentId = BuildContabilidadDocumentIdMiscelaneo(dto.Recibo);
        var detalles = await _context.factura_detalles
            .Where(d => d.factura_id == factura.id)
            .ToListAsync(ct);

        if (detalles.Count == 0)
        {
            return ResponseModelDto.Fail("El recibo no tiene detalles asociados.");
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var transaccionesPago = await _context.transaccion_abonados
                .Where(t => t.recibo == factura.numrecibo && t.tipotransaccion == "201")
                .ToListAsync(ct);

            var tieneMovimientoBanco = false;
            long bancoCuentaId = 0;
            long banKardexIdOriginal = 0;
            foreach (var transaccion in transaccionesPago.OrderByDescending(t => t.ide))
            {
                if (TryObtenerReferenciaMovimientoBancario(transaccion, out bancoCuentaId, out banKardexIdOriginal))
                {
                    tieneMovimientoBanco = true;
                    break;
                }
            }

            if (transaccionesPago.Count > 0)
            {
                _context.transaccion_abonados.RemoveRange(transaccionesPago);
            }

            foreach (var detalle in detalles)
            {
                detalle.montovalor_saldo = detalle.montovalor ?? 0m;
            }

            factura.estado = "A";
            factura.recolectora = null;
            factura.fechapago = null;
            factura.usuario = usuario;

            long? polizaId = null;
            (long BanKardexIdAnulacion, decimal SaldoResultante)? anulacionBanco = null;
            if (tieneMovimientoBanco)
            {
                anulacionBanco = await _banTransaccionesService.AnularMovimientoAsync(
                    bancoCuentaId,
                    banKardexIdOriginal,
                    $"Reverso captacion miscelaneo recibo {dto.Recibo}",
                    usuario,
                    ct);
            }
            else
            {
                polizaId = await RevertirComprobanteContableCaptacionAsync(
                    companyId,
                    contabilidadDocumentId,
                    usuario,
                    _context.Database.CurrentTransaction?.GetDbTransaction(),
                    ct);
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(
                new
                {
                    factura.numrecibo,
                    PolizaId = polizaId,
                    PolizaStatus = polizaId.HasValue ? 0 : (int?)null,
                    PolizaEstado = polizaId.HasValue ? "DRAFT" : null,
                    ContabilidadDocumentId = polizaId.HasValue ? (long?)contabilidadDocumentId : null,
                    BancoCuentaId = tieneMovimientoBanco ? (long?)bancoCuentaId : null,
                    BanKardexIdOriginal = tieneMovimientoBanco ? (long?)banKardexIdOriginal : null,
                    BanKardexIdAnulacion = anulacionBanco.HasValue ? anulacionBanco.Value.BanKardexIdAnulacion : (long?)null
                },
                "Pago miscelaneo reversado correctamente.");
        }
        catch (Exception ex)
        {
            try
            {
                await tx.RollbackAsync(ct);
            }
            catch
            {
                // Ignore rollback failures to preserve original error.
            }

            return ResponseModelDto.Fail($"Error al reversar pago miscelaneo: {ex.Message}");
        }
    }

    // ==================== COMBOS Y AUXILIARES ====================
    public async Task<IReadOnlyList<ClienteComboDto>> ListarClientesAsync(string? query = null, int? maxResults = null, CancellationToken ct = default)
    {
        var clientes = _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.estado)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            clientes = clientes.Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, term)
                || EF.Functions.ILike(c.maestro_cliente_nombre, term));
        }

        var take = maxResults ?? (string.IsNullOrWhiteSpace(query) ? 10 : 100);
        return await clientes
            .OrderBy(c => c.maestro_cliente_nombre)
            .Take(take)
            .Select(c => new ClienteComboDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BancoDto>> ListarBancosAsync(CancellationToken ct = default)
    {
        var cuentasBancarias = await _context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.activo)
            .Select(c => new
            {
                c.banco_cuenta_id,
                c.ban_banco_id,
                c.code,
                c.nombre,
                c.banco_nombre,
                c.numero_cuenta,
                BancoCode = c.ban_banco != null ? c.ban_banco.code : null,
                BancoNombre = c.ban_banco != null ? c.ban_banco.nombre : null
            })
            .ToListAsync(ct);

        if (cuentasBancarias.Count > 0)
        {
            return cuentasBancarias
                .Select(c =>
                {
                    var codigo = !string.IsNullOrWhiteSpace(c.BancoCode)
                        ? c.BancoCode.Trim()
                        : !string.IsNullOrWhiteSpace(c.code)
                            ? c.code.Trim()
                            : c.banco_cuenta_id.ToString(CultureInfo.InvariantCulture);

                    var nombreBanco = !string.IsNullOrWhiteSpace(c.banco_nombre)
                        ? c.banco_nombre.Trim()
                        : !string.IsNullOrWhiteSpace(c.BancoNombre)
                            ? c.BancoNombre.Trim()
                            : !string.IsNullOrWhiteSpace(c.nombre)
                                ? c.nombre.Trim()
                                : codigo;

                    var nombre = string.IsNullOrWhiteSpace(c.numero_cuenta)
                        ? $"{codigo} - {nombreBanco}"
                        : $"{codigo} - {nombreBanco} ({c.numero_cuenta.Trim()})";

                    return new BancoDto
                    {
                        BancoCuentaId = c.banco_cuenta_id,
                        BancoId = c.ban_banco_id,
                        Codigo = codigo,
                        Nombre = nombre
                    };
                })
                .OrderBy(b => b.Nombre)
                .ToList();
        }

        // Fallback legacy mientras no haya catalogo bancario configurado.
        return await _context.recolectoras
            .AsNoTracking()
            .OrderBy(b => b.descripcion)
            .Select(b => new BancoDto
            {
                Codigo = b.codigo,
                Nombre = string.IsNullOrWhiteSpace(b.descripcion) ? b.codigo : $"{b.codigo} - {b.descripcion}"
            })
            .ToListAsync(ct);
    }

    public async Task<PeriodoActualDto> ObtenerPeriodoActualAsync(CancellationToken ct = default)
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
            var ahora = DateTime.UtcNow;
            return new PeriodoActualDto
            {
                Periodo = $"{ahora:yyyyMM}",
                Anio = $"{ahora:yyyy}",
                Mes = $"{ahora:MM}"
            };
        }

        var ano = Convert.ToInt32(periodo.ano);
        var mes = Convert.ToInt32(periodo.mes);
        return new PeriodoActualDto
        {
            Periodo = $"{ano:D4}{mes:D2}",
            Anio = $"{ano:D4}",
            Mes = $"{mes:D2}"
        };
    }


    private async Task<factura?> ObtenerFacturaAsync(string? numFactura, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(numFactura))
        {
            return null;
        }

        var valor = numFactura.Trim();
        if (int.TryParse(valor, out var recibo))
        {
            return await _context.facturas
                .FirstOrDefaultAsync(f => f.numrecibo == recibo || f.numfactura == valor, ct);
        }

        return await _context.facturas
            .FirstOrDefaultAsync(f => f.numfactura == valor, ct);
    }

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        // Don't dispose the shared EF connection; disposing here breaks the DbContext.
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

        var ano = Convert.ToInt32(periodo.ano);
        var mes = Convert.ToInt32(periodo.mes);
        return $"{ano:D4}{mes:D2}";
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

        if (cuenta is null)
        {
            throw new InvalidOperationException($"No se encontro una cuenta bancaria activa para BancoCuentaId={bancoCuentaId.Value}.");
        }

        if (!string.IsNullOrWhiteSpace(cuenta.BancoCode))
        {
            return cuenta.BancoCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(cuenta.code))
        {
            return cuenta.code.Trim();
        }

        return bancoNormalizado ?? bancoCuentaId.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string? ResolveServicioCodigoDetalle(string? tipoServicio, string? codigo)
    {
        if (!string.IsNullOrWhiteSpace(tipoServicio))
        {
            return tipoServicio.Trim();
        }

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            return codigo.Trim();
        }

        return null;
    }

    private static string BuildBancoMarker(long bancoCuentaId) =>
        $"{BancoMarkerPrefix}{bancoCuentaId.ToString(CultureInfo.InvariantCulture)}";

    private static bool TryParseBancoMarker(string? marker, out long bancoCuentaId)
    {
        bancoCuentaId = 0;
        if (string.IsNullOrWhiteSpace(marker))
        {
            return false;
        }

        var value = marker.Trim();
        if (!value.StartsWith(BancoMarkerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = value[BancoMarkerPrefix.Length..];
        return long.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out bancoCuentaId)
               && bancoCuentaId > 0;
    }

    private static bool TryObtenerReferenciaMovimientoBancario(
        transaccion_abonado? transaccion,
        out long bancoCuentaId,
        out long banKardexId)
    {
        bancoCuentaId = 0;
        banKardexId = 0;

        if (transaccion is null)
        {
            return false;
        }

        if (!TryParseBancoMarker(transaccion.trans_aplicar, out bancoCuentaId))
        {
            return false;
        }

        if (!transaccion.docuaplicar.HasValue)
        {
            return false;
        }

        var banKardexDecimal = transaccion.docuaplicar.Value;
        if (banKardexDecimal <= 0
            || decimal.Truncate(banKardexDecimal) != banKardexDecimal
            || banKardexDecimal > long.MaxValue)
        {
            return false;
        }

        banKardexId = Convert.ToInt64(banKardexDecimal, CultureInfo.InvariantCulture);
        return banKardexId > 0;
    }

    private static void AplicarReferenciaMovimientoBancario(
        transaccion_abonado transaccion,
        long bancoCuentaId,
        long banKardexId)
    {
        transaccion.docuaplicar = Convert.ToDecimal(banKardexId, CultureInfo.InvariantCulture);
        transaccion.trans_aplicar = BuildBancoMarker(bancoCuentaId);
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
        if (bancoCuentaId <= 0)
        {
            throw new InvalidOperationException("BancoCuentaId invalido para registrar movimiento bancario.");
        }

        if (monto <= 0)
        {
            throw new InvalidOperationException("El monto del movimiento bancario debe ser mayor a cero.");
        }

        if (contraCuentas.Count == 0)
        {
            throw new InvalidOperationException("Debe existir al menos una contracuenta para registrar movimiento bancario.");
        }

        var descripcionNormalizada = string.IsNullOrWhiteSpace(descripcion) ? "Captacion bancaria" : descripcion.Trim();
        if (descripcionNormalizada.Length > 500)
        {
            descripcionNormalizada = descripcionNormalizada[..500];
        }

        var referenciaNormalizada = string.IsNullOrWhiteSpace(referencia) ? sourceDocument : referencia.Trim();
        if (referenciaNormalizada.Length > 100)
        {
            referenciaNormalizada = referenciaNormalizada[..100];
        }

        var sourceDocumentNormalizado = string.IsNullOrWhiteSpace(sourceDocument)
            ? referenciaNormalizada
            : sourceDocument.Trim();
        if (sourceDocumentNormalizado.Length > 120)
        {
            sourceDocumentNormalizado = sourceDocumentNormalizado[..120];
        }

        return await _banTransaccionesService.RegistrarMovimientoAsync(
            bancoCuentaId,
            TipoTransaccionBancoDeposito,
            fechaMovimiento,
            descripcionNormalizada,
            referenciaNormalizada,
            sourceDocumentNormalizado,
            1m,
            monto,
            contraCuentas,
            string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim(),
            ct);
    }

    /// <summary>
    /// Contracuentas del movimiento bancario de un cobro: CxC analítica resuelta
    /// por la matriz de integración según el modo configurado (Banco / CxC —
    /// reemplaza al mapeo legacy servicios.cont_account_id, que acreditaba
    /// ingresos directo y dejaba la CxC de la facturación sin saldar).
    /// </summary>
    private async Task<IReadOnlyList<BanTransaccionContraLineaDto>> ConstruirContraCuentasBancariasAsync(
        IReadOnlyList<(string? ServicioCodigo, decimal Monto)> aplicaciones,
        int? categoriaServicioId,
        bool? conMedicion,
        string descripcion,
        string sourceDocument,
        CancellationToken ct)
    {
        var entradas = aplicaciones.Where(a => a.Monto > 0).ToList();
        if (entradas.Count == 0)
        {
            throw new InvalidOperationException("No se recibieron detalles con monto para construir contracuentas bancarias.");
        }

        var sourceDocumentNormalizado = string.IsNullOrWhiteSpace(sourceDocument)
            ? "CAPTACION"
            : sourceDocument.Trim();
        if (sourceDocumentNormalizado.Length > 120)
        {
            sourceDocumentNormalizado = sourceDocumentNormalizado[..120];
        }

        var companyId = EnsureCompanyId();
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var config = await IntegracionContableConfigSql.ObtenerConfigAsync(connection, companyId, transaction: null, ct)
            ?? throw new InvalidOperationException(
                "La empresa no tiene configuración de integración contable (pantalla Integración Contable / perfil ERSAPS).");

        var cuentasCxc = await IntegracionContableConfigSql.ResolverCuentasCxcPorServicioAsync(
            connection,
            companyId,
            config.ModoCxc,
            entradas.Select(a => a.ServicioCodigo),
            categoriaServicioId,
            conMedicion,
            transaction: null,
            ct);

        var montosPorCuenta = new Dictionary<long, decimal>();
        foreach (var entrada in entradas)
        {
            var cuentaId = cuentasCxc[(entrada.ServicioCodigo ?? string.Empty).Trim().ToUpperInvariant()];
            montosPorCuenta[cuentaId] = montosPorCuenta.GetValueOrDefault(cuentaId) + entrada.Monto;
        }

        return montosPorCuenta
            .OrderBy(x => x.Key)
            .Select(x => new BanTransaccionContraLineaDto
            {
                CuentaId = x.Key,
                Monto = Math.Round(x.Value, 2, MidpointRounding.AwayFromZero),
                Descripcion = descripcion,
                SourceDocument = sourceDocumentNormalizado
            })
            .ToList();
    }

    private async Task<string?> TryCompensarMovimientoBancarioAsync(
        long bancoCuentaId,
        long banKardexId,
        string usuario,
        string motivo,
        CancellationToken ct)
    {
        try
        {
            await _banTransaccionesService.AnularMovimientoAsync(
                bancoCuentaId,
                banKardexId,
                motivo,
                string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim(),
                ct);

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual para generar comprobante contable.");
        }

        return companyId;
    }

    private static long BuildContabilidadDocumentIdLectora(int recibo)
    {
        if (recibo <= 0)
        {
            throw new InvalidOperationException("El recibo debe ser mayor a cero para generar documento contable (lectora).");
        }

        return recibo;
    }

    private static long BuildContabilidadDocumentIdMiscelaneo(long recibo)
    {
        if (recibo <= 0)
        {
            throw new InvalidOperationException("El recibo debe ser mayor a cero para generar documento contable (miscelaneo).");
        }

        return CaptacionMiscelaneoDocumentOffset + recibo;
    }

    private static long BuildContabilidadDocumentIdManual(int recibo)
    {
        if (recibo <= 0)
        {
            throw new InvalidOperationException("El recibo debe ser mayor a cero para generar documento contable (manual).");
        }

        return CaptacionManualDocumentOffset + recibo;
    }

    /// <summary>
    /// Genera el comprobante del cobro (Debe caja / Haber CxC analítica) sobre la
    /// configuración de integración contable (plan F4, D2). Devuelve
    /// (null, false) si la integración de caja no está activa para la empresa y
    /// (null, true) si quedó encolado en con_partida_pendiente por falta de período.
    /// </summary>
    private async Task<(long? PolizaId, bool Encolada)> GenerarComprobanteCaptacionConfigAsync(
        long companyId,
        long documentId,
        string documentNumber,
        DateOnly polizaDate,
        string description,
        string usuario,
        IReadOnlyList<(string? ServicioCodigo, decimal Monto)> aplicaciones,
        int? categoriaServicioId,
        bool? conMedicion,
        bool cxcGeneral,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var config = await IntegracionContableConfigSql.ObtenerConfigAsync(connection, companyId, transaction, ct);
        if (config is null || !config.ActivoCaja)
        {
            return (null, false);
        }

        var cuentaCaja = await IntegracionContableConfigSql.ResolverCuentaAsync(
            connection, companyId, "CAJA", transaction, ct);

        List<(long CuentaCxcId, decimal Monto)> aplicacionesCxc;
        if (cxcGeneral)
        {
            var cuentaCxc = await IntegracionContableConfigSql.ResolverCuentaAsync(
                connection, companyId, "CXC", transaction, ct);
            aplicacionesCxc = aplicaciones
                .Where(a => a.Monto > 0)
                .Select(a => (cuentaCxc, a.Monto))
                .ToList();
        }
        else
        {
            var cuentasCxc = await IntegracionContableConfigSql.ResolverCuentasCxcPorServicioAsync(
                connection,
                companyId,
                config.ModoCxc,
                aplicaciones.Select(a => a.ServicioCodigo),
                categoriaServicioId,
                conMedicion,
                transaction,
                ct);

            aplicacionesCxc = aplicaciones
                .Where(a => a.Monto > 0)
                .Select(a => (cuentasCxc[(a.ServicioCodigo ?? string.Empty).Trim().ToUpperInvariant()], a.Monto))
                .ToList();
        }

        var lineas = IntegracionContableConfigSql.ArmarLineasCobro(
            cuentaCaja,
            aplicacionesCxc,
            description,
            description);

        var polizaId = await IntegracionContableConfigSql.GenerarComprobanteAsync(
            connection,
            companyId,
            ContabilidadModuloVentas,
            ContabilidadDocumentoRecibo,
            documentId,
            documentNumber,
            polizaDate,
            description,
            usuario,
            lineas,
            transaction,
            ct);

        return (polizaId, polizaId is null);
    }

    private async Task<long?> RevertirComprobanteContableCaptacionAsync(
        long companyId,
        long documentId,
        string usuario,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        // REC es lo que estampa la integración por config; FAC cubre comprobantes
        // legacy generados por el esquema de plantillas.
        return await IntegracionContableConfigSql.RevertirComprobanteAsync(
            connection,
            companyId,
            ContabilidadModuloVentas,
            new[] { ContabilidadDocumentoRecibo, ContabilidadDocumentoFactura },
            documentId,
            usuario,
            transaction,
            ct);
    }

    private sealed class SaldoPosteoManualLegacyRow
    {
        public long? Ide { get; init; }
        public int Recibo { get; init; }
        public int? ReciboAnterior { get; init; }
        public string? TipoServicio { get; init; }
        public decimal? Monto { get; init; }
        public decimal? MontoDistribuido { get; init; }
    }

    private static string MapTipoServicio(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return "OTROS";
        }

        var normalized = tipo.Trim().ToUpperInvariant();
        return normalized switch
        {
            "101" => "AGUA",
            "AGUA" => "AGUA",
            "AGUA_POTABLE" => "AGUA",
            "102" => "ALCANTARILLADO",
            "ALCANTARILLADO" => "ALCANTARILLADO",
            _ => "OTROS"
        };
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

