using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Bancos;

public sealed class ChequesService : IChequesService
{
    public const string EstadoEmitido = "E";
    public const string EstadoAnulado = "A";

    private const int MaxFilasBusqueda = 5000;

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IAccountFormatService _accountFormatService;

    public ChequesService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IAccountFormatService accountFormatService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
        _accountFormatService = accountFormatService;
    }

    public async Task<(long ChequeId, decimal NumeroCheque)> EmitirChequeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long bancoCuentaId,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        long? partidaId,
        string usuario,
        DateTime fechaEmision,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var companyId = EnsureCompanyId();

        var numeracion = await LockAndComputeNumeracionAsync(connection, transaction, companyId, bancoCuentaId, ct);

        long chequeId;
        try
        {
            chequeId = await InsertChequeRowAsync(
                connection, transaction, companyId, bancoCuentaId, numeracion.NumeroAsignado,
                fechaEmision, monto, beneficiario, concepto, origen, origenDocumento,
                banKardexId, partidaId, EstadoEmitido, usuario,
                motivoAnulacion: null, usuarioAnulacion: null, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                                           ex.ConstraintName == "uq_ban_cheque_numero")
        {
            // Error de negocio, no 500: en abonos ademas evita caer en el catch de carrera
            // de uq_prv_compromiso_abono_numero ("Reintente el abono").
            throw new InvalidOperationException(
                $"El número de cheque {numeracion.NumeroAsignado:N0} ya existe en la cuenta bancaria. " +
                "Revise el próximo cheque en la gestión de la cuenta.", ex);
        }

        // Evento EMITIDO en la bitacora append-only, misma transaccion del pago.
        await InsertBitacoraEventoAsync(
            connection, transaction, companyId, chequeId, bancoCuentaId, numeracion.NumeroAsignado,
            ChequeAccion.Emitido, usuario, monto, beneficiario, concepto,
            motivo: null, origen, origenDocumento, banKardexId, ct);

        await UpdateProximoChequeAsync(connection, transaction, companyId, bancoCuentaId, numeracion.SiguienteProximo, ct);

        return (chequeId, numeracion.NumeroAsignado);
    }

    public async Task<bool> AnularPorKardexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long banKardexIdOriginal,
        long banKardexIdReverso,
        string motivo,
        string usuario,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var companyId = EnsureCompanyId();

        var motivoAnulacion = Trunc(motivo, 250) ?? "Reverso del movimiento bancario";
        var usuarioAnulacion = Trunc(usuario, 100) ?? "sistema";

        // RETURNING: datos del cheque anulado para el evento ANULADO de la bitacora.
        var anulados = new List<(long ChequeId, long BancoCuentaId, decimal NumeroCheque, decimal Monto,
            string? Beneficiario, string? Concepto, string Origen, string? OrigenDocumento)>();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = @"
UPDATE public.ban_cheque
   SET estado = @estado_anulado,
       motivo_anulacion = @motivo,
       usuario_anulacion = @usuario,
       fecha_anulacion = now(),
       ban_kardex_id_reverso = @kardex_reverso
 WHERE company_id = @company_id
   AND ban_kardex_id = @kardex_original
   AND estado = @estado_emitido
RETURNING cheque_id, banco_cuenta_id, numero_cheque, monto, beneficiario, concepto, origen, origen_documento;";
            command.Parameters.AddWithValue("estado_anulado", NpgsqlDbType.Char, EstadoAnulado);
            command.Parameters.AddWithValue("motivo", NpgsqlDbType.Varchar, motivoAnulacion);
            command.Parameters.AddWithValue("usuario", NpgsqlDbType.Varchar, usuarioAnulacion);
            command.Parameters.AddWithValue("kardex_reverso", NpgsqlDbType.Bigint, banKardexIdReverso);
            command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("kardex_original", NpgsqlDbType.Bigint, banKardexIdOriginal);
            command.Parameters.AddWithValue("estado_emitido", NpgsqlDbType.Char, EstadoEmitido);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                anulados.Add((
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }

        // Evento ANULADO por cheque (bitacora append-only); ban_kardex_id = el reverso.
        foreach (var cheque in anulados)
        {
            await InsertBitacoraEventoAsync(
                connection, transaction, companyId, cheque.ChequeId, cheque.BancoCuentaId, cheque.NumeroCheque,
                ChequeAccion.Anulado, usuarioAnulacion, cheque.Monto, cheque.Beneficiario, cheque.Concepto,
                motivo: motivoAnulacion, cheque.Origen, cheque.OrigenDocumento, banKardexIdReverso, ct);
        }

        return anulados.Count > 0;
    }

    public async Task<decimal> AnularSiguienteNumeroAsync(
        long bancoCuentaId,
        string motivo,
        string usuario,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var companyId = EnsureCompanyId();
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();

        // Reusar la transaccion ambiente (tests) o abrir una propia (produccion).
        var ambient = _context.Database.CurrentTransaction;
        var ownsConnection = false;
        NpgsqlTransaction tx;
        var ownsTx = false;
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
            tx = await connection.BeginTransactionAsync(ct);
            ownsTx = true;
        }

        try
        {
            var numeracion = await LockAndComputeNumeracionAsync(connection, tx, companyId, bancoCuentaId, ct);

            var chequeId = await InsertChequeRowAsync(
                connection, tx, companyId, bancoCuentaId, numeracion.NumeroAsignado,
                fechaEmision: DateTime.Now, monto: 0m, beneficiario: null,
                concepto: "Anulacion manual de numero de cheque",
                origen: ChequeOrigen.Manual, origenDocumento: null,
                banKardexId: null, partidaId: null, estado: EstadoAnulado, usuario,
                motivoAnulacion: Trunc(motivo, 250), usuarioAnulacion: Trunc(usuario, 100), ct);

            // Un unico evento ANULADO: el cheque nunca se emitio (numero consumido/danado).
            await InsertBitacoraEventoAsync(
                connection, tx, companyId, chequeId, bancoCuentaId, numeracion.NumeroAsignado,
                ChequeAccion.Anulado, usuario, monto: 0m, beneficiario: null,
                concepto: "Anulacion manual de numero de cheque",
                motivo: Trunc(motivo, 250), origen: ChequeOrigen.Manual,
                origenDocumento: null, banKardexId: null, ct);

            await UpdateProximoChequeAsync(connection, tx, companyId, bancoCuentaId, numeracion.SiguienteProximo, ct);

            if (ownsTx) await tx.CommitAsync(ct);
            return numeracion.NumeroAsignado;
        }
        catch
        {
            if (ownsTx) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
            if (ownsConnection) await connection.CloseAsync();
        }
    }

    public async Task<ProximoChequeDto?> GetProximoAsync(long bancoCuentaId, CancellationToken ct = default)
    {
        // Filtro global multi-tenant aplicado por el contexto.
        var cuenta = await _context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.banco_cuenta_id == bancoCuentaId)
            .Select(c => new { c.banco_cuenta_id, c.proximo_cheque, c.cheque_maximo })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null)
        {
            return null;
        }

        var numeracion = ChequeNumeracionCalculator.Compute(cuenta.proximo_cheque, cuenta.cheque_maximo);
        return new ProximoChequeDto
        {
            BancoCuentaId = cuenta.banco_cuenta_id,
            ProximoCheque = numeracion.NumeroAsignado,
            ChequeMaximo = decimal.Truncate(cuenta.cheque_maximo),
            Agotado = numeracion.Agotado
        };
    }

    public async Task<IReadOnlyList<ChequeListItemDto>> BuscarAsync(
        ChequeFilterDto filtro,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var query =
            from ch in _context.ban_cheques.AsNoTracking()
            join cu in _context.ban_cuenta.AsNoTracking() on ch.banco_cuenta_id equals cu.banco_cuenta_id
            join bb in _context.ban_banco.AsNoTracking() on cu.ban_banco_id equals (long?)bb.ban_banco_id into bbj
            from bb in bbj.DefaultIfEmpty()
            select new { ch, cu, bb };

        if (filtro.BancoId.HasValue)
        {
            var bancoId = filtro.BancoId.Value;
            query = bancoId > 0
                ? query.Where(x => x.cu.ban_banco_id == bancoId)
                : query.Where(x => x.cu.ban_banco_id == null);
        }

        if (filtro.BancoCuentaId is > 0)
        {
            query = query.Where(x => x.ch.banco_cuenta_id == filtro.BancoCuentaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            var estado = filtro.Estado.Trim().ToUpperInvariant();
            query = query.Where(x => x.ch.estado == estado);
        }

        if (filtro.Desde.HasValue)
        {
            var desde = filtro.Desde.Value.Date;
            query = query.Where(x => x.ch.fecha_emision >= desde);
        }

        if (filtro.Hasta.HasValue)
        {
            var hasta = filtro.Hasta.Value.Date.AddDays(1);
            query = query.Where(x => x.ch.fecha_emision < hasta);
        }

        if (filtro.NumeroCheque is > 0)
        {
            query = query.Where(x => x.ch.numero_cheque == filtro.NumeroCheque.Value);
        }

        return await query
            .OrderByDescending(x => x.ch.fecha_emision)
            .ThenByDescending(x => x.ch.numero_cheque)
            .Take(MaxFilasBusqueda)
            .Select(x => new ChequeListItemDto
            {
                ChequeId = x.ch.cheque_id,
                BancoCuentaId = x.ch.banco_cuenta_id,
                NumeroCuenta = x.cu.numero_cuenta,
                BancoNombre = x.bb != null ? x.bb.nombre : string.Empty,
                NumeroCheque = x.ch.numero_cheque,
                FechaEmision = x.ch.fecha_emision,
                Monto = x.ch.monto,
                Beneficiario = x.ch.beneficiario,
                Concepto = x.ch.concepto,
                Origen = x.ch.origen,
                OrigenDocumento = x.ch.origen_documento,
                BanKardexId = x.ch.ban_kardex_id,
                Estado = x.ch.estado,
                UsuarioEmision = x.ch.usuario_emision,
                MotivoAnulacion = x.ch.motivo_anulacion,
                UsuarioAnulacion = x.ch.usuario_anulacion,
                FechaAnulacion = x.ch.fecha_anulacion
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChequeBitacoraListItemDto>> BuscarBitacoraAsync(
        ChequeBitacoraFilterDto filtro,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var query =
            from ev in _context.ban_cheque_bitacoras.AsNoTracking()
            join cu in _context.ban_cuenta.AsNoTracking() on ev.banco_cuenta_id equals cu.banco_cuenta_id
            join bb in _context.ban_banco.AsNoTracking() on cu.ban_banco_id equals (long?)bb.ban_banco_id into bbj
            from bb in bbj.DefaultIfEmpty()
            select new { ev, cu, bb };

        if (filtro.BancoId.HasValue)
        {
            var bancoId = filtro.BancoId.Value;
            query = bancoId > 0
                ? query.Where(x => x.cu.ban_banco_id == bancoId)
                : query.Where(x => x.cu.ban_banco_id == null);
        }

        if (filtro.BancoCuentaId is > 0)
        {
            query = query.Where(x => x.ev.banco_cuenta_id == filtro.BancoCuentaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Accion))
        {
            var accion = filtro.Accion.Trim().ToUpperInvariant();
            query = query.Where(x => x.ev.accion == accion);
        }

        if (filtro.Desde.HasValue)
        {
            var desde = filtro.Desde.Value.Date;
            query = query.Where(x => x.ev.fecha >= desde);
        }

        if (filtro.Hasta.HasValue)
        {
            var hasta = filtro.Hasta.Value.Date.AddDays(1);
            query = query.Where(x => x.ev.fecha < hasta);
        }

        if (filtro.NumeroCheque is > 0)
        {
            query = query.Where(x => x.ev.numero_cheque == filtro.NumeroCheque.Value);
        }

        return await query
            .OrderByDescending(x => x.ev.fecha)
            .ThenByDescending(x => x.ev.bitacora_id)
            .Take(MaxFilasBusqueda)
            .Select(x => new ChequeBitacoraListItemDto
            {
                BitacoraId = x.ev.bitacora_id,
                ChequeId = x.ev.cheque_id,
                BancoCuentaId = x.ev.banco_cuenta_id,
                NumeroCuenta = x.cu.numero_cuenta,
                BancoNombre = x.bb != null ? x.bb.nombre : string.Empty,
                NumeroCheque = x.ev.numero_cheque,
                Accion = x.ev.accion,
                Fecha = x.ev.fecha,
                Usuario = x.ev.usuario,
                Monto = x.ev.monto,
                Beneficiario = x.ev.beneficiario,
                Concepto = x.ev.concepto,
                Motivo = x.ev.motivo,
                Origen = x.ev.origen,
                OrigenDocumento = x.ev.origen_documento,
                BanKardexId = x.ev.ban_kardex_id
            })
            .ToListAsync(ct);
    }

    public async Task<ChequeImpresionDto?> GetDatosImpresionAsync(
        long chequeId,
        string impresoPor,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chequeId);

        // Filtro global multi-tenant aplicado por el contexto.
        var datos = await (
            from ch in _context.ban_cheques.AsNoTracking()
            join cu in _context.ban_cuenta.AsNoTracking() on ch.banco_cuenta_id equals cu.banco_cuenta_id
            join bb in _context.ban_banco.AsNoTracking() on cu.ban_banco_id equals (long?)bb.ban_banco_id into bbj
            from bb in bbj.DefaultIfEmpty()
            where ch.cheque_id == chequeId
            select new { ch, cu, bb })
            .FirstOrDefaultAsync(ct);

        if (datos is null)
        {
            return null;
        }

        var companyId = EnsureCompanyId();
        var company = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        // La ciudad de emision impresa en el cheque sale de la configuracion de la empresa.
        var config = await _context.con_empresa_configuracions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        var format = await _accountFormatService.GetFormatAsync(ct);

        var dto = new ChequeImpresionDto
        {
            EmpresaNombre = company?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = company?.legal_name,
            EmpresaRtn = company?.tax_id,
            EmpresaDireccion = company?.address,
            EmpresaTelefono = company?.phone,
            EmpresaEmail = company?.email,
            EmpresaLogo = company?.logo,
            Ciudad = config?.ciudad?.Trim() ?? string.Empty,
            ChequeId = datos.ch.cheque_id,
            NumeroCheque = datos.ch.numero_cheque,
            FechaEmision = datos.ch.fecha_emision,
            Beneficiario = datos.ch.beneficiario,
            Concepto = datos.ch.concepto,
            Monto = datos.ch.monto,
            MontoEnLetras = $"{NumerosALetras.Convertir(datos.ch.monto)} LEMPIRAS",
            BancoNombre = datos.bb != null ? datos.bb.nombre : string.Empty,
            NumeroCuenta = datos.cu.numero_cuenta,
            Estado = datos.ch.estado,
            MotivoAnulacion = datos.ch.motivo_anulacion,
            OrigenDocumento = datos.ch.origen_documento,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),
            FormatoCuentas = format.Mask,
            SeparadorCodigo = format.Separator
        };

        // Distribucion contable = lineas de la partida ligada al cheque (si la hay).
        if (datos.ch.partida_id is > 0)
        {
            var hdr = await _context.con_partida_hdrs
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.poliza_id == datos.ch.partida_id.Value, ct);

            if (hdr is not null)
            {
                dto.ComprobanteNumero = hdr.poliza_number;
                dto.PartidaFecha = hdr.poliza_date;
                dto.PartidaDescripcion = hdr.description;

                dto.Distribucion = await (
                    from d in _context.con_partida_dtls.AsNoTracking()
                    join a in _context.con_plan_cuentas.AsNoTracking() on d.account_id equals a.account_id into aj
                    from a in aj.DefaultIfEmpty()
                    join cc in _context.con_centro_costos.AsNoTracking() on d.cost_center_id equals (long?)cc.cost_center_id into ccj
                    from cc in ccj.DefaultIfEmpty()
                    where d.poliza_id == hdr.poliza_id
                    orderby d.line_number
                    select new ChequeDistribucionLineaDto
                    {
                        CodigoCuenta = a != null ? a.code : string.Empty,
                        NombreCuenta = a != null ? a.name : string.Empty,
                        CentroCosto = cc != null ? cc.code : null,
                        Descripcion = d.description,
                        Cargo = d.debit_amount,
                        Credito = d.credit_amount
                    })
                    .ToListAsync(ct);
            }
        }

        return dto;
    }

    public async Task<long?> GetChequeIdVigentePorKardexAsync(long banKardexId, CancellationToken ct = default)
    {
        if (banKardexId <= 0)
        {
            return null;
        }

        // Filtro global multi-tenant aplicado por el contexto.
        return await _context.ban_cheques
            .AsNoTracking()
            .Where(ch => ch.ban_kardex_id == banKardexId && ch.estado == EstadoEmitido)
            .Select(ch => (long?)ch.cheque_id)
            .FirstOrDefaultAsync(ct);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<ChequeNumeracionResult> LockAndComputeNumeracionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT COALESCE(proximo_cheque, 0), COALESCE(cheque_maximo, 0)
  FROM public.ban_cuenta
 WHERE company_id = @company_id AND banco_cuenta_id = @banco_cuenta_id
 FOR UPDATE;";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);

        decimal proximo = 0m, maximo = 0m;
        var encontrada = false;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                encontrada = true;
                proximo = reader.GetDecimal(0);
                maximo = reader.GetDecimal(1);
            }
        }

        if (!encontrada)
        {
            throw new InvalidOperationException($"La cuenta bancaria {bancoCuentaId} no existe.");
        }

        var numeracion = ChequeNumeracionCalculator.Compute(proximo, maximo);
        if (numeracion.Agotado)
        {
            throw new InvalidOperationException(
                $"La cuenta bancaria agotó su numeración de cheques (máximo {decimal.Truncate(maximo):N0}). " +
                "Actualice la numeración en la gestión de la cuenta.");
        }

        return numeracion;
    }

    private static async Task<long> InsertChequeRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        decimal numeroCheque,
        DateTime fechaEmision,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        long? partidaId,
        string estado,
        string usuario,
        string? motivoAnulacion,
        string? usuarioAnulacion,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO public.ban_cheque
    (company_id, banco_cuenta_id, numero_cheque, fecha_emision, monto, beneficiario,
     concepto, origen, origen_documento, ban_kardex_id, partida_id, estado,
     usuario_emision, motivo_anulacion, usuario_anulacion, fecha_anulacion)
VALUES
    (@company_id, @banco_cuenta_id, @numero_cheque, @fecha_emision, @monto, @beneficiario,
     @concepto, @origen, @origen_documento, @ban_kardex_id, @partida_id, @estado,
     @usuario_emision, @motivo_anulacion, @usuario_anulacion,
     CASE WHEN @estado = 'A' THEN now() ELSE NULL END)
RETURNING cheque_id;";

        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        command.Parameters.AddWithValue("numero_cheque", NpgsqlDbType.Numeric, numeroCheque);
        // ban_cheque.fecha_emision es TIMESTAMP WITHOUT TIME ZONE: Npgsql rechaza DateTime con
        // Kind=Utc en NpgsqlDbType.Timestamp (MarkAsProcessedAsync manda la fecha de orden en Utc).
        command.Parameters.AddWithValue("fecha_emision", NpgsqlDbType.Timestamp, DateTime.SpecifyKind(fechaEmision, DateTimeKind.Unspecified));
        command.Parameters.AddWithValue("monto", NpgsqlDbType.Numeric, monto);
        command.Parameters.Add(new NpgsqlParameter("beneficiario", NpgsqlDbType.Varchar) { Value = (object?)Trunc(beneficiario, 200) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("concepto", NpgsqlDbType.Varchar) { Value = (object?)Trunc(concepto, 250) ?? DBNull.Value });
        command.Parameters.AddWithValue("origen", NpgsqlDbType.Varchar, origen);
        command.Parameters.Add(new NpgsqlParameter("origen_documento", NpgsqlDbType.Varchar) { Value = (object?)Trunc(origenDocumento, 50) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("ban_kardex_id", NpgsqlDbType.Bigint) { Value = banKardexId ?? (object)DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("partida_id", NpgsqlDbType.Bigint) { Value = partidaId ?? (object)DBNull.Value });
        command.Parameters.AddWithValue("estado", NpgsqlDbType.Char, estado);
        command.Parameters.AddWithValue("usuario_emision", NpgsqlDbType.Varchar, Trunc(usuario, 100) ?? "sistema");
        command.Parameters.Add(new NpgsqlParameter("motivo_anulacion", NpgsqlDbType.Varchar) { Value = (object?)motivoAnulacion ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("usuario_anulacion", NpgsqlDbType.Varchar) { Value = (object?)usuarioAnulacion ?? DBNull.Value });

        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// Inserta un evento en ban_cheque_bitacora (append-only), dentro de la MISMA
    /// transaccion de la operacion. Nunca se actualiza ni se borra un evento.
    /// </summary>
    private static async Task InsertBitacoraEventoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long chequeId,
        long bancoCuentaId,
        decimal numeroCheque,
        string accion,
        string usuario,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string? motivo,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO public.ban_cheque_bitacora
    (company_id, cheque_id, banco_cuenta_id, numero_cheque, accion, usuario, monto,
     beneficiario, concepto, motivo, origen, origen_documento, ban_kardex_id)
VALUES
    (@company_id, @cheque_id, @banco_cuenta_id, @numero_cheque, @accion, @usuario, @monto,
     @beneficiario, @concepto, @motivo, @origen, @origen_documento, @ban_kardex_id);";

        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("cheque_id", NpgsqlDbType.Bigint, chequeId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        command.Parameters.AddWithValue("numero_cheque", NpgsqlDbType.Numeric, numeroCheque);
        command.Parameters.AddWithValue("accion", NpgsqlDbType.Varchar, accion);
        command.Parameters.AddWithValue("usuario", NpgsqlDbType.Varchar, Trunc(usuario, 100) ?? "sistema");
        command.Parameters.AddWithValue("monto", NpgsqlDbType.Numeric, monto);
        command.Parameters.Add(new NpgsqlParameter("beneficiario", NpgsqlDbType.Varchar) { Value = (object?)Trunc(beneficiario, 200) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("concepto", NpgsqlDbType.Varchar) { Value = (object?)Trunc(concepto, 250) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("motivo", NpgsqlDbType.Varchar) { Value = (object?)Trunc(motivo, 250) ?? DBNull.Value });
        command.Parameters.AddWithValue("origen", NpgsqlDbType.Varchar, origen);
        command.Parameters.Add(new NpgsqlParameter("origen_documento", NpgsqlDbType.Varchar) { Value = (object?)Trunc(origenDocumento, 50) ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("ban_kardex_id", NpgsqlDbType.Bigint) { Value = banKardexId ?? (object)DBNull.Value });

        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateProximoChequeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        decimal siguiente,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
UPDATE public.ban_cuenta
   SET proximo_cheque = @siguiente
 WHERE company_id = @company_id AND banco_cuenta_id = @banco_cuenta_id;";
        command.Parameters.AddWithValue("siguiente", NpgsqlDbType.Numeric, siguiente);
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string? Trunc(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual.");
        }

        return companyId;
    }
}
