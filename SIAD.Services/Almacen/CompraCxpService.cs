using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Almacen;

/// <summary>
/// Pagos a proveedores sobre las cuentas por pagar de compra. El pago mueve el saldo bancario
/// de verdad (kardex de la cuenta vía <c>sp_ban_kardex_registrar_movimiento</c> + emisión de
/// cheque), pero la póliza contable se difiere (Fase 2): el kardex se registra con
/// <c>partida_id</c> nulo, igual que el módulo OPD cuando el motor encola. El saldo de la CxP
/// es materializado y se mantiene bajo <c>FOR UPDATE</c>.
/// </summary>
public sealed class CompraCxpService : ICompraCxpService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IChequesService _cheques;

    public CompraCxpService(SiadDbContext context, ICurrentCompanyService company, IChequesService cheques)
    {
        _context = context;
        _company = company;
        _cheques = cheques;
    }

    // ── Consultas ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CompraCxpListItemDto>> ListarAsync(CompraCxpFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new CompraCxpFilterDto();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.alm_compra_cxps.AsNoTracking()
            .Where(c => c.estado_id != EstadoCompraCxp.Anulada);

        if (!string.IsNullOrWhiteSpace(filtro.CodProveedor))
        {
            var cod = filtro.CodProveedor.Trim();
            query = query.Where(c => c.cod_proveedor == cod);
        }
        if (filtro.EstadoId.HasValue)
        {
            query = query.Where(c => c.estado_id == filtro.EstadoId.Value);
        }
        if (filtro.SoloVencidas)
        {
            query = query.Where(c => c.fecha_vencimiento < hoy && c.estado_id != EstadoCompraCxp.Pagada);
        }
        if (filtro.VenceDesde.HasValue)
        {
            query = query.Where(c => c.fecha_vencimiento >= filtro.VenceDesde.Value);
        }
        if (filtro.VenceHasta.HasValue)
        {
            query = query.Where(c => c.fecha_vencimiento <= filtro.VenceHasta.Value);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var like = $"%{filtro.Search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.cod_proveedor, like) ||
                EF.Functions.ILike(c.proveedor ?? string.Empty, like) ||
                EF.Functions.ILike(c.numero_factura_sar ?? string.Empty, like));
        }

        return await query
            .OrderBy(c => c.fecha_vencimiento)
            .ThenBy(c => c.cod_proveedor)
            .Select(c => new CompraCxpListItemDto
            {
                Id = c.id,
                CompraHdrId = c.compra_hdr_id,
                Numero = c.compra == null ? 0 : c.compra.numero,
                CodProveedor = c.cod_proveedor,
                Proveedor = c.proveedor,
                NumeroFacturaSar = c.numero_factura_sar,
                Fecha = c.fecha,
                FechaVencimiento = c.fecha_vencimiento,
                CondicionPago = c.condicion_pago,
                Monto = c.monto,
                Abonado = c.monto - c.saldo,
                Saldo = c.saldo,
                EstadoId = c.estado_id
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CuentaBancariaLookupDto>> ListarCuentasBancariasAsync(CancellationToken ct = default)
    {
        return await _context.ban_cuenta.AsNoTracking()
            .Where(x => x.activo)
            .OrderBy(x => x.nombre)
            .Select(x => new CuentaBancariaLookupDto
            {
                BancoCuentaId = x.banco_cuenta_id,
                BancoId = x.ban_banco_id,
                Nombre = x.nombre,
                Banco = x.banco_nombre,
                NumeroCuenta = x.numero_cuenta,
                Moneda = x.currency_code
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompraCuentaContableLookupDto>> ListarCuentasContablesAsync(CancellationToken ct = default)
    {
        // Todas las cuentas del plan que permiten movimiento: la contrapartida de un pago en
        // efectivo (la caja de donde sale el dinero) la elige el usuario en el combo.
        return await _context.con_plan_cuentas.AsNoTracking()
            .Where(c => c.allows_posting && (c.status.ToUpper() == "ACTIVE" || c.status.ToUpper() == "ACTIVO"))
            .OrderBy(c => c.code)
            .Select(c => new CompraCuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Name = c.name
            })
            .ToListAsync(ct);
    }

    /// <summary>True si la contabilidad de compras (activo_compras) está encendida para la empresa.
    /// Cuando lo está, la cuenta contable del pago en efectivo pasa a ser obligatoria.</summary>
    public async Task<bool> ObtenerContabilidadActivaAsync(CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) return false;

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var ownsConnection = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            ownsConnection = true;
        }
        try
        {
            var config = await IntegracionContableConfigSql.ObtenerConfigAsync(connection, companyId, transaction: null, ct);
            return config?.ActivoCompras ?? false;
        }
        finally
        {
            if (ownsConnection) await connection.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<CompraCxpAbonoListItemDto>> ListarAbonosAsync(int cxpId, CancellationToken ct = default)
    {
        return await _context.alm_compra_cxp_abonos.AsNoTracking()
            .Where(a => a.cxp_id == cxpId)
            .OrderBy(a => a.numero_abono)
            .Select(a => new CompraCxpAbonoListItemDto
            {
                NumeroAbono = a.numero_abono,
                Fecha = a.fecha,
                Monto = a.monto,
                MetodoPago = a.metodo_pago,
                NumCheque = a.num_cheque,
                Estado = a.estado,
                PartidaId = a.partida_id
            })
            .ToListAsync(ct);
    }

    /// <summary>Devuelve la partida contable de un pago (cabecera + líneas Debe/Haber) para mostrarla
    /// en pantalla, o <c>null</c> si el pago no generó asiento (contabilidad apagada al registrarlo).</summary>
    public async Task<CompraCxpPartidaDto?> ObtenerPartidaAbonoAsync(int cxpId, int numeroAbono, CancellationToken ct = default)
    {
        var partidaId = await _context.alm_compra_cxp_abonos.AsNoTracking()
            .Where(a => a.cxp_id == cxpId && a.numero_abono == numeroAbono)
            .Select(a => a.partida_id)
            .FirstOrDefaultAsync(ct);
        if (partidaId is not > 0) return null;

        var hdr = await _context.con_partida_hdrs.AsNoTracking()
            .Where(h => h.poliza_id == partidaId.Value)
            .Select(h => new { h.poliza_id, h.poliza_number, h.poliza_date, h.description, h.total_debit, h.total_credit })
            .FirstOrDefaultAsync(ct);
        if (hdr is null) return null;

        var lineas = await _context.con_partida_dtls.AsNoTracking()
            .Where(d => d.poliza_id == partidaId.Value)
            .OrderBy(d => d.line_number)
            .Select(d => new CompraCxpPartidaLineaDto
            {
                Cuenta = d.account!.code,
                Nombre = d.account!.name,
                Debe = d.debit_amount,
                Haber = d.credit_amount
            })
            .ToListAsync(ct);

        return new CompraCxpPartidaDto
        {
            PolizaId = hdr.poliza_id,
            PolizaNumero = hdr.poliza_number,
            Fecha = DateOnly.FromDateTime(hdr.poliza_date),
            Descripcion = hdr.description,
            TotalDebe = hdr.total_debit,
            TotalHaber = hdr.total_credit,
            Lineas = lineas
        };
    }

    // ── Impresión de comprobantes (PDF) ─────────────────────────────────────────

    /// <summary>Arma los datos del comprobante de PAGO (empresa + egreso + saldos + total en letras).
    /// Devuelve <c>null</c> si el abono o la CxP no existen.</summary>
    public async Task<PagoCompraImpresionDto?> GetDatosImpresionPagoAsync(
        int cxpId, int numeroAbono, string impresoPor, CancellationToken ct = default)
    {
        var abono = await _context.alm_compra_cxp_abonos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.cxp_id == cxpId && a.numero_abono == numeroAbono, ct);
        if (abono is null) return null;

        var cxp = await _context.alm_compra_cxps.AsNoTracking()
            .FirstOrDefaultAsync(c => c.id == cxpId, ct);
        if (cxp is null) return null;

        var numeroInterno = await _context.alm_compra_hdrs.AsNoTracking()
            .Where(h => h.id == cxp.compra_hdr_id)
            .Select(h => (int?)h.numero)
            .FirstOrDefaultAsync(ct);
        var numeroFactura = !string.IsNullOrWhiteSpace(cxp.numero_factura_sar)
            ? cxp.numero_factura_sar!.Trim()
            : (numeroInterno ?? 0).ToString("00000");

        // Saldo restante tras este abono = total de la factura − Σ(abonos vigentes con numero ≤ N).
        var abonadoHasta = await _context.alm_compra_cxp_abonos.AsNoTracking()
            .Where(a => a.cxp_id == cxpId && a.estado == "V" && a.numero_abono <= numeroAbono)
            .SumAsync(a => (decimal?)a.monto, ct) ?? 0m;
        var saldoRestante = cxp.monto - abonadoHasta;
        if (saldoRestante < 0m) saldoRestante = 0m;
        var saldoAnterior = saldoRestante + abono.monto;

        string? banco = null, cuentaBancaria = null;
        if (abono.banco_cuenta_id is > 0)
        {
            var bc = (long)abono.banco_cuenta_id.Value;
            var cuenta = await _context.ban_cuenta.AsNoTracking()
                .Where(x => x.banco_cuenta_id == bc)
                .Select(x => new { x.banco_nombre, x.nombre, x.numero_cuenta })
                .FirstOrDefaultAsync(ct);
            if (cuenta is not null)
            {
                banco = cuenta.banco_nombre;
                cuentaBancaria = string.IsNullOrWhiteSpace(cuenta.numero_cuenta)
                    ? cuenta.nombre
                    : $"{cuenta.nombre} · {cuenta.numero_cuenta}";
            }
        }

        string? numeroPartida = null;
        if (abono.partida_id is > 0)
        {
            numeroPartida = await _context.con_partida_hdrs.AsNoTracking()
                .Where(h => h.poliza_id == abono.partida_id!.Value)
                .Select(h => h.poliza_number)
                .FirstOrDefaultAsync(ct);
        }

        var empresa = await CargarEmpresaAsync(ct);
        var anulada = abono.estado == "A";

        var dto = new PagoCompraImpresionDto
        {
            NumeroAbono = abono.numero_abono,
            Fecha = abono.fecha,
            CodProveedor = cxp.cod_proveedor,
            Proveedor = cxp.proveedor,
            NumeroFactura = numeroFactura,
            MontoFactura = cxp.monto,
            Monto = abono.monto,
            SaldoAnterior = saldoAnterior,
            SaldoRestante = saldoRestante,
            MetodoPago = FormatearMetodo(abono.metodo_pago),
            Banco = banco,
            CuentaBancaria = cuentaBancaria,
            NumCheque = abono.num_cheque,
            NumeroPartida = numeroPartida,
            Observaciones = abono.observaciones,
            Anulada = anulada,
            EstadoTexto = anulada ? "ANULADO" : "VIGENTE",
            MontoEnLetras = abono.monto > 0m ? NumerosALetras.Convertir(abono.monto) : string.Empty
        };
        AplicarEmpresa(dto, empresa, impresoPor);
        return dto;
    }

    /// <summary>Arma los datos del comprobante de la PARTIDA CONTABLE del pago. Devuelve <c>null</c> si el
    /// pago no generó asiento (contabilidad de compras apagada al registrarlo).</summary>
    public async Task<PartidaContableImpresionDto?> GetDatosImpresionPartidaPagoAsync(
        int cxpId, int numeroAbono, string impresoPor, CancellationToken ct = default)
    {
        var partidaId = await _context.alm_compra_cxp_abonos.AsNoTracking()
            .Where(a => a.cxp_id == cxpId && a.numero_abono == numeroAbono)
            .Select(a => a.partida_id)
            .FirstOrDefaultAsync(ct);
        if (partidaId is not > 0) return null;

        var hdr = await _context.con_partida_hdrs.AsNoTracking()
            .Where(h => h.poliza_id == partidaId.Value)
            .Select(h => new { h.poliza_number, h.poliza_date, h.description, h.status, h.total_debit, h.total_credit })
            .FirstOrDefaultAsync(ct);
        if (hdr is null) return null;

        var lineas = await _context.con_partida_dtls.AsNoTracking()
            .Where(d => d.poliza_id == partidaId.Value)
            .OrderBy(d => d.line_number)
            .Select(d => new PartidaContableLineaImpresionDto
            {
                CuentaCodigo = d.account!.code,
                CuentaNombre = d.account!.name,
                Debe = d.debit_amount,
                Haber = d.credit_amount,
                Descripcion = d.description
            })
            .ToListAsync(ct);

        var empresa = await CargarEmpresaAsync(ct);
        var anulada = hdr.status == 2;

        var dto = new PartidaContableImpresionDto
        {
            Numero = hdr.poliza_number,
            Fecha = hdr.poliza_date,
            Descripcion = hdr.description,
            DocumentoReferencia = $"Pago No. {numeroAbono:00000}",
            Anulada = anulada,
            EstadoTexto = anulada ? "ANULADA" : "REGISTRADA",
            TotalDebe = hdr.total_debit,
            TotalHaber = hdr.total_credit,
            Lineas = lineas,
            TotalEnLetras = hdr.total_debit > 0m ? NumerosALetras.Convertir(hdr.total_debit) : string.Empty
        };
        AplicarEmpresa(dto, empresa, impresoPor);
        return dto;
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }

    private static void AplicarEmpresa(ComprobanteAlmacenImpresionBase dto, cfg_company? empresa, string impresoPor)
    {
        dto.EmpresaNombre = empresa?.commercial_name ?? string.Empty;
        dto.EmpresaRazonSocial = empresa?.legal_name;
        dto.EmpresaRtn = empresa?.tax_id;
        dto.EmpresaDireccion = empresa?.address;
        dto.EmpresaTelefono = empresa?.phone;
        dto.EmpresaEmail = empresa?.email;
        dto.EmpresaLogo = empresa?.logo;
        dto.ImpresoPor = ClasificacionNormalizer.Usuario(impresoPor);
    }

    private static string FormatearMetodo(string? metodo)
    {
        if (string.IsNullOrWhiteSpace(metodo)) return "-";
        var m = metodo.Trim();
        return char.ToUpperInvariant(m[0]) + m[1..].ToLowerInvariant();
    }

    // ── Registrar pago ─────────────────────────────────────────────────────────

    public async Task<CompraCxpAbonoResultadoDto> RegistrarAbonoAsync(
        int cxpId, CompraCxpAbonoUpsertDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");
        if (dto.Monto <= 0m) throw new InvalidOperationException("El monto del pago debe ser mayor que cero.");

        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        var metodo = (dto.MetodoPago ?? string.Empty).Trim().ToUpperInvariant();
        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var esBancario = MetodoPagoCompra.EsBancario(metodo);

        // El tipo de transacción bancaria se resuelve ANTES de la transacción (sólo lectura).
        string? tipoTransaccion = null;
        if (esBancario)
        {
            if (dto.BancoCuentaId is not > 0)
            {
                throw new InvalidOperationException("Seleccione la cuenta bancaria de origen del pago.");
            }
            tipoTransaccion = await ResolverTipoTransaccionAsync(metodo, companyId, ct);
        }

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var ambient = _context.Database.CurrentTransaction;
        NpgsqlTransaction tx;
        var ownsTx = false;
        var ownsConnection = false;
        if (ambient is not null)
        {
            tx = (NpgsqlTransaction)ambient.GetDbTransaction();
        }
        else
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                ownsConnection = true;
            }
            tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            ownsTx = true;
        }

        try
        {
            var cxp = await BloquearCxpAsync(connection, tx, companyId, cxpId, ct)
                ?? throw new InvalidOperationException("La cuenta por pagar no existe.");

            if (cxp.EstadoId == EstadoCompraCxp.Anulada)
            {
                throw new InvalidOperationException("La cuenta por pagar está anulada.");
            }
            if (cxp.Saldo <= 0m)
            {
                throw new InvalidOperationException("La cuenta por pagar ya está pagada; no admite más pagos.");
            }
            if (dto.Monto - cxp.Saldo > 0.01m)
            {
                throw new InvalidOperationException(
                    $"El monto ({dto.Monto:N2}) supera el saldo pendiente ({cxp.Saldo:N2}).");
            }

            var numeroAbono = await SiguienteNumeroAbonoAsync(connection, tx, companyId, cxpId, ct);
            var descripcion = $"Pago factura {cxp.NumeroFacturaSar ?? cxp.Numero.ToString("00000")} · {cxp.CodProveedor}";

            long? banKardexId = null;
            long? chequeId = null;
            decimal? numeroCheque = null;

            if (esBancario)
            {
                var referencia = $"CXP-{cxpId}-{MetodoPagoCompra.Abrev(metodo)}-{numeroAbono}";
                banKardexId = await RegistrarKardexBancarioAsync(
                    connection, tx, companyId, dto.BancoCuentaId!.Value, tipoTransaccion!, fecha,
                    descripcion, referencia, dto.Monto, usuario, ct);

                if (metodo == MetodoPagoCompra.Cheque)
                {
                    // La póliza se difiere (Fase 2): el cheque va sin partida, como el kardex.
                    (chequeId, numeroCheque) = await _cheques.EmitirChequeAsync(
                        connection, tx, dto.BancoCuentaId.Value, dto.Monto,
                        cxp.Proveedor ?? cxp.CodProveedor, descripcion, ChequeOrigen.CompraCxp, $"CXP-{cxpId}",
                        banKardexId, partidaId: null, usuario, fecha.ToDateTime(TimeOnly.MinValue), ct);
                }
            }

            await InsertarAbonoAsync(connection, tx, companyId, cxpId, numeroAbono, fecha, dto.Monto,
                metodo, dto.BancoCuentaId, numeroCheque?.ToString("0"), banKardexId, dto.Observaciones, usuario, ct);

            var nuevoSaldo = cxp.Saldo - dto.Monto;
            var estado = nuevoSaldo <= 0.01m ? EstadoCompraCxp.Pagada : EstadoCompraCxp.Parcial;
            if (nuevoSaldo < 0m) nuevoSaldo = 0m;
            await ActualizarCxpAsync(connection, tx, companyId, cxpId, nuevoSaldo, estado, usuario, ct);

            // Fase 2: asiento contable del pago (DEBE proveedor / HABER banco o caja), módulo COMPRAS,
            // gated por activo_compras (Compras es su propio módulo contable, separado de PROV/inventario).
            // Bancario → contrapartida = cuenta contable del banco; efectivo → cuenta contable elegida
            // por el usuario. No-op si el módulo COMPRAS está inactivo.
            await ContabilizarPagoAsync(connection, tx, companyId, cxp, dto.Monto, esBancario,
                dto.BancoCuentaId, dto.CuentaContableId, cxpId, numeroAbono, fecha, usuario, ct);

            if (ownsTx) await tx.CommitAsync(ct);

            return new CompraCxpAbonoResultadoDto
            {
                Success = true,
                CxpId = cxpId,
                NumeroAbono = numeroAbono,
                Saldo = nuevoSaldo,
                EstadoId = estado,
                NumeroCheque = numeroCheque,
                ChequeId = chequeId
            };
        }
        catch
        {
            if (ownsTx) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownsConnection) await connection.CloseAsync();
        }
    }

    // ── Anular pago ─────────────────────────────────────────────────────────────

    public async Task<bool> AnularAbonoAsync(int cxpId, int numeroAbono, string motivo, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new InvalidOperationException("El motivo de la anulación es obligatorio.");
        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        var motivoNorm = motivo.Trim();

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var ambient = _context.Database.CurrentTransaction;
        NpgsqlTransaction tx;
        var ownsTx = false;
        var ownsConnection = false;
        if (ambient is not null)
        {
            tx = (NpgsqlTransaction)ambient.GetDbTransaction();
        }
        else
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(ct);
                ownsConnection = true;
            }
            tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            ownsTx = true;
        }

        try
        {
            var abono = await BloquearAbonoAsync(connection, tx, companyId, cxpId, numeroAbono, ct);
            if (abono is null || abono.Estado == "A")
            {
                if (ownsTx) await tx.RollbackAsync(ct);
                return abono is not null;   // ya anulado: idempotente
            }

            // Sólo se anula el último abono vigente (los pagos se deshacen en orden inverso).
            var ultimoVigente = await UltimoAbonoVigenteAsync(connection, tx, companyId, cxpId, ct);
            if (numeroAbono != ultimoVigente)
            {
                throw new InvalidOperationException(
                    "Sólo se puede anular el último pago vigente de la cuenta.");
            }

            // Reversa del movimiento bancario: kardex de anulación (SP) + anular el cheque si lo hubo.
            long? banKardexReverso = null;
            if (abono.BancoCuentaId is > 0 && abono.BanKardexId is > 0)
            {
                banKardexReverso = await ReversarKardexBancarioAsync(
                    connection, tx, companyId, abono.BancoCuentaId.Value, abono.BanKardexId.Value, motivoNorm, usuario, ct);
                await _cheques.AnularPorKardexAsync(
                    connection, tx, abono.BanKardexId.Value, banKardexReverso.Value, motivoNorm, usuario, ct);
            }

            // Fase 2: revierte el asiento contable del pago (módulo COMPRAS; no-op si no tenía póliza).
            if (abono.PartidaId is not null)
            {
                await IntegracionContableConfigSql.RevertirComprobanteAsync(
                    connection, companyId, IntegracionContableModulos.Compras, new[] { $"CXP-ABO{numeroAbono}" }, cxpId, usuario, tx, ct);
            }

            await MarcarAbonoAnuladoAsync(connection, tx, companyId, cxpId, numeroAbono, banKardexReverso, motivoNorm, usuario, ct);

            // Reabre la CxP: el saldo sube por el monto revertido.
            var cxp = await BloquearCxpAsync(connection, tx, companyId, cxpId, ct)!;
            var nuevoSaldo = cxp!.Saldo + abono.Monto;
            var estado = nuevoSaldo >= cxp.Monto - 0.01m ? EstadoCompraCxp.Pendiente : EstadoCompraCxp.Parcial;
            await ActualizarCxpAsync(connection, tx, companyId, cxpId, nuevoSaldo, estado, usuario, ct);

            if (ownsTx) await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            if (ownsTx) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownsConnection) await connection.CloseAsync();
        }
    }

    // ── Helpers de datos (SQL directo, en la transacción del pago) ───────────────

    private sealed record CxpLock(decimal Saldo, short EstadoId, decimal Monto, string CodProveedor, string? Proveedor, string? NumeroFacturaSar, int Numero);

    private async Task<CxpLock?> BloquearCxpAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
SELECT c.saldo, c.estado_id, c.monto, c.cod_proveedor, c.proveedor, c.numero_factura_sar,
       COALESCE(h.numero, 0)
  FROM public.alm_compra_cxp c
  LEFT JOIN public.alm_compra_hdr h ON h.company_id = c.company_id AND h.id = c.compra_hdr_id
 WHERE c.company_id = @c AND c.id = @id
 FOR UPDATE OF c";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, cxpId);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new CxpLock(
            r.GetDecimal(0), r.GetInt16(1), r.GetDecimal(2), r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetInt32(6));
    }

    private static async Task<int> SiguienteNumeroAbonoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(MAX(numero_abono), 0) + 1 FROM public.alm_compra_cxp_abono WHERE company_id = @c AND cxp_id = @id";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, cxpId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private async Task<long> RegistrarKardexBancarioAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, long bancoCuentaId, string tipoTransaccion,
        DateOnly fecha, string descripcion, string referencia, decimal monto, string usuario, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "public.sp_ban_kardex_registrar_movimiento";
        cmd.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        cmd.Parameters.AddWithValue("p_movimiento_id", NpgsqlDbType.Bigint, 0L);
        cmd.Parameters.AddWithValue("p_id_tipo_transaccion", NpgsqlDbType.Varchar, tipoTransaccion);
        cmd.Parameters.AddWithValue("p_fecha_movimiento", NpgsqlDbType.Date, fecha);
        cmd.Parameters.AddWithValue("p_descripcion", NpgsqlDbType.Varchar, descripcion);
        cmd.Parameters.Add(new NpgsqlParameter("p_referencia", NpgsqlDbType.Varchar) { Value = referencia });
        cmd.Parameters.AddWithValue("p_tasa_cambio", NpgsqlDbType.Numeric, 1m);
        cmd.Parameters.AddWithValue("p_monto", NpgsqlDbType.Numeric, monto);
        cmd.Parameters.AddWithValue("p_usuario", NpgsqlDbType.Varchar, usuario);
        var kardexParam = new NpgsqlParameter("p_ban_kardex_id", NpgsqlDbType.Bigint) { Direction = ParameterDirection.Output };
        var saldoParam = new NpgsqlParameter("p_saldo_resultante", NpgsqlDbType.Numeric) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(kardexParam);
        cmd.Parameters.Add(saldoParam);

        await cmd.ExecuteNonQueryAsync(ct);
        var kardexId = kardexParam.Value is DBNull ? 0L : Convert.ToInt64(kardexParam.Value);
        if (kardexId <= 0)
        {
            throw new InvalidOperationException("No fue posible registrar el movimiento bancario del pago.");
        }
        return kardexId;
    }

    private static async Task<long> ReversarKardexBancarioAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, long bancoCuentaId, long banKardexIdOriginal,
        string motivo, string usuario, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "public.sp_ban_kardex_anular_movimiento_recalcular";
        cmd.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        cmd.Parameters.AddWithValue("p_ban_kardex_id_original", NpgsqlDbType.Bigint, banKardexIdOriginal);
        cmd.Parameters.AddWithValue("p_motivo", NpgsqlDbType.Varchar, motivo);
        cmd.Parameters.AddWithValue("p_usuario", NpgsqlDbType.Varchar, usuario);
        var kardexParam = new NpgsqlParameter("p_ban_kardex_id_anulacion", NpgsqlDbType.Bigint) { Direction = ParameterDirection.Output };
        var saldoParam = new NpgsqlParameter("p_saldo_resultante", NpgsqlDbType.Numeric) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(kardexParam);
        cmd.Parameters.Add(saldoParam);

        await cmd.ExecuteNonQueryAsync(ct);
        var kardexId = kardexParam.Value is DBNull ? 0L : Convert.ToInt64(kardexParam.Value);
        if (kardexId <= 0)
        {
            throw new InvalidOperationException("No fue posible reversar el movimiento bancario del pago.");
        }
        return kardexId;
    }

    private static async Task InsertarAbonoAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, int numeroAbono, DateOnly fecha,
        decimal monto, string metodo, long? bancoCuentaId, string? numCheque, long? banKardexId,
        string? observaciones, string usuario, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
INSERT INTO public.alm_compra_cxp_abono
    (company_id, cxp_id, numero_abono, fecha, monto, metodo_pago, banco_cuenta_id, num_cheque,
     ban_kardex_id, estado, observaciones, usuariocreacion)
VALUES (@c, @cxp, @num, @fecha, @monto, @metodo, @banco, @cheque, @kardex, 'V', @obs, @user)";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("cxp", NpgsqlDbType.Integer, cxpId);
        cmd.Parameters.AddWithValue("num", NpgsqlDbType.Integer, numeroAbono);
        cmd.Parameters.AddWithValue("fecha", NpgsqlDbType.Date, fecha);
        cmd.Parameters.AddWithValue("monto", NpgsqlDbType.Numeric, monto);
        cmd.Parameters.AddWithValue("metodo", NpgsqlDbType.Varchar, metodo);
        cmd.Parameters.Add(new NpgsqlParameter("banco", NpgsqlDbType.Integer) { Value = bancoCuentaId.HasValue ? (int)bancoCuentaId.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("cheque", NpgsqlDbType.Varchar) { Value = (object?)numCheque ?? DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("kardex", NpgsqlDbType.Bigint) { Value = banKardexId.HasValue ? banKardexId.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("obs", NpgsqlDbType.Varchar) { Value = (object?)observaciones ?? DBNull.Value });
        cmd.Parameters.AddWithValue("user", NpgsqlDbType.Varchar, usuario);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ActualizarCxpAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, decimal saldo, short estado, string usuario, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
UPDATE public.alm_compra_cxp
   SET saldo = @saldo, estado_id = @estado, usuariomodificacion = @user,
       fechamodificacion = (now() AT TIME ZONE 'utc')
 WHERE company_id = @c AND id = @id";
        cmd.Parameters.AddWithValue("saldo", NpgsqlDbType.Numeric, saldo);
        cmd.Parameters.AddWithValue("estado", NpgsqlDbType.Smallint, estado);
        cmd.Parameters.AddWithValue("user", NpgsqlDbType.Varchar, usuario);
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, cxpId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed record AbonoLock(string Estado, decimal Monto, long? BancoCuentaId, long? BanKardexId, long? PartidaId);

    private static async Task<AbonoLock?> BloquearAbonoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, int numeroAbono, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
SELECT estado, monto, banco_cuenta_id, ban_kardex_id, partida_id
  FROM public.alm_compra_cxp_abono
 WHERE company_id = @c AND cxp_id = @cxp AND numero_abono = @num
 FOR UPDATE";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("cxp", NpgsqlDbType.Integer, cxpId);
        cmd.Parameters.AddWithValue("num", NpgsqlDbType.Integer, numeroAbono);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new AbonoLock(
            r.GetString(0), r.GetDecimal(1),
            r.IsDBNull(2) ? null : r.GetInt64(2), r.IsDBNull(3) ? null : r.GetInt64(3),
            r.IsDBNull(4) ? null : r.GetInt64(4));
    }

    private static async Task<int> UltimoAbonoVigenteAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(MAX(numero_abono), 0) FROM public.alm_compra_cxp_abono WHERE company_id = @c AND cxp_id = @cxp AND estado = 'V'";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("cxp", NpgsqlDbType.Integer, cxpId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task MarcarAbonoAnuladoAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, int cxpId, int numeroAbono,
        long? banKardexReverso, string motivo, string usuario, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
UPDATE public.alm_compra_cxp_abono
   SET estado = 'A', ban_kardex_id_reverso = @rev, motivo_anulacion = @motivo,
       usuarioanulacion = @user, fechaanulacion = (now() AT TIME ZONE 'utc')
 WHERE company_id = @c AND cxp_id = @cxp AND numero_abono = @num";
        cmd.Parameters.Add(new NpgsqlParameter("rev", NpgsqlDbType.Bigint) { Value = banKardexReverso.HasValue ? banKardexReverso.Value : DBNull.Value });
        cmd.Parameters.AddWithValue("motivo", NpgsqlDbType.Varchar, motivo);
        cmd.Parameters.AddWithValue("user", NpgsqlDbType.Varchar, usuario);
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("cxp", NpgsqlDbType.Integer, cxpId);
        cmd.Parameters.AddWithValue("num", NpgsqlDbType.Integer, numeroAbono);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── Contabilidad del pago (Fase 2, módulo COMPRAS, gated por activo_compras) ──

    private static async Task ContabilizarPagoAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, CxpLock cxp, decimal monto, bool esBancario,
        long? bancoCuentaId, long? cuentaContableId, int cxpId, int numeroAbono, DateOnly fecha, string usuario, CancellationToken ct)
    {
        var config = await IntegracionContableConfigSql.ObtenerConfigAsync(cn, companyId, tx, ct);
        if (config is null || !config.ActivoCompras) return;

        var ctaProveedor = await ResolverCuentaProveedorAsync(cn, tx, companyId, cxp.CodProveedor, ct);

        long ctaContrapartida;
        string etiqueta;
        if (esBancario)
        {
            ctaContrapartida = await ResolverCuentaBancoAsync(cn, tx, companyId, bancoCuentaId!.Value, ct);
            etiqueta = "Banco";
        }
        else
        {
            if (cuentaContableId is not > 0)
            {
                throw new InvalidOperationException(
                    "Seleccione la cuenta contable de contrapartida del pago en efectivo (la contabilidad de compras está activa).");
            }
            ctaContrapartida = await ValidarCuentaPosteableAsync(cn, tx, companyId, cuentaContableId.Value, ct);
            etiqueta = "Caja";
        }

        var desc = $"Pago factura {cxp.NumeroFacturaSar ?? cxp.Numero.ToString("00000")} · {cxp.CodProveedor}";
        var lineas = new List<IntegracionContableConfigSql.ComprobanteLinea>
        {
            new(ctaProveedor, monto, 0m, "Proveedor"),
            new(ctaContrapartida, 0m, monto, etiqueta)
        };

        // El pago de la CxP de compra es del MÓDULO COMPRAS (no PROV): mismo motor, distinto diario/tipo.
        var polizaId = await IntegracionContableConfigSql.GenerarComprobanteAsync(
            cn, companyId, IntegracionContableModulos.Compras, $"CXP-ABO{numeroAbono}", cxpId, $"CXP-{cxpId}",
            fecha, desc, usuario, lineas, tx, ct);

        if (polizaId is not null)
        {
            await using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE public.alm_compra_cxp_abono SET partida_id = @p WHERE company_id = @c AND cxp_id = @cxp AND numero_abono = @n";
            cmd.Parameters.AddWithValue("p", NpgsqlDbType.Bigint, polizaId.Value);
            cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
            cmd.Parameters.AddWithValue("cxp", NpgsqlDbType.Integer, cxpId);
            cmd.Parameters.AddWithValue("n", NpgsqlDbType.Integer, numeroAbono);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<long> ResolverCuentaProveedorAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, string codProveedor, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
SELECT c.account_id
  FROM public.prv_proveedores p
  JOIN public.con_plan_cuentas c
    ON c.company_id = p.company_id AND btrim(c.code) = btrim(p.cuenta_contable) AND c.allows_posting
 WHERE p.company_id = @c AND p.cod_proveedor = @prov
 LIMIT 1";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("prov", NpgsqlDbType.Varchar, codProveedor);
        var v = await cmd.ExecuteScalarAsync(ct);
        if (v is null or DBNull)
        {
            throw new InvalidOperationException(
                $"El proveedor {codProveedor} no tiene una cuenta contable posteable para el asiento del pago.");
        }
        return Convert.ToInt64(v);
    }

    private static async Task<long> ValidarCuentaPosteableAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, long accountId, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT account_id FROM public.con_plan_cuentas WHERE company_id = @c AND account_id = @a AND allows_posting LIMIT 1";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("a", NpgsqlDbType.Bigint, accountId);
        var v = await cmd.ExecuteScalarAsync(ct);
        if (v is null or DBNull)
        {
            throw new InvalidOperationException(
                "La cuenta contable seleccionada para el pago en efectivo no existe o no permite movimiento.");
        }
        return Convert.ToInt64(v);
    }

    private static async Task<long> ResolverCuentaBancoAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long companyId, long bancoCuentaId, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT cont_account_id FROM public.ban_cuenta WHERE company_id = @c AND banco_cuenta_id = @bc";
        cmd.Parameters.AddWithValue("c", NpgsqlDbType.Bigint, companyId);
        cmd.Parameters.AddWithValue("bc", NpgsqlDbType.Bigint, bancoCuentaId);
        var v = await cmd.ExecuteScalarAsync(ct);
        if (v is null or DBNull)
        {
            throw new InvalidOperationException("La cuenta bancaria no tiene cuenta contable (cont_account_id) mapeada para el asiento del pago.");
        }
        return Convert.ToInt64(v);
    }

    private async Task<string> ResolverTipoTransaccionAsync(string metodo, long companyId, CancellationToken ct)
    {
        var query = _context.ban_tipos_transacciones.AsNoTracking()
            .Where(x => x.company_id == companyId
                     && x.entra_sale == "S"
                     && (x.estado == null || x.estado == string.Empty
                         || x.estado.ToUpper() == "ACTIVE" || x.estado.ToUpper() == "ACTIVO"));

        query = metodo switch
        {
            MetodoPagoCompra.Cheque => query.Where(x =>
                (x.emite_cheque != null && (x.emite_cheque == "S" || x.emite_cheque == "Y"
                    || x.emite_cheque == "1" || x.emite_cheque == "T" || x.emite_cheque.ToUpper() == "TRUE"))
                || EF.Functions.ILike(x.nombre, "%CHEQ%")
                || EF.Functions.ILike(x.tipo_transaccion, "%CHEQ%")),
            MetodoPagoCompra.Transferencia => query.Where(x =>
                EF.Functions.ILike(x.nombre, "%TRANSFER%")
                || EF.Functions.ILike(x.tipo_transaccion, "%TRF%")
                || EF.Functions.ILike(x.tipo_transaccion, "%TRANSFER%")),
            _ => throw new InvalidOperationException($"El método {metodo} no genera movimiento bancario.")
        };

        var tipo = await query.OrderBy(x => x.tipo_transaccion)
            .Select(x => x.tipo_transaccion)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(tipo))
        {
            throw new InvalidOperationException(
                $"No hay un tipo de transacción bancaria de salida configurado para {metodo}. Configúrelo en Bancos.");
        }
        return tipo;
    }
}
