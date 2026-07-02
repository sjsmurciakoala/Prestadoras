using System.Data;
using System.Text;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class BanTransaccionesService : IBanTransaccionesService
{
    private readonly SiadDbContext context;
    private readonly IMapper mapper;
    private readonly ICurrentCompanyService currentCompanyService;

    public BanTransaccionesService(
        SiadDbContext context,
        IMapper mapper,
        ICurrentCompanyService currentCompanyService)
    {
        this.context = context;
        this.mapper = mapper;
        this.currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(
        long companyId,
        long? bancoId = null,
        long? bancoCuentaId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        bool incluirAnuladas = false,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        if (bancoCuentaId.HasValue)
        {
            var cuentaInfo = await context.ban_cuenta
                .Where(c => c.company_id == companyId && c.banco_cuenta_id == bancoCuentaId.Value)
                .Select(c => new { c.banco_cuenta_id, c.ban_banco_id })
                .FirstOrDefaultAsync(ct);

            if (cuentaInfo is null)
            {
                // LOG: Cuenta no encontrada
                Console.WriteLine($"âŒ Cuenta {bancoCuentaId} no encontrada para company {companyId}");
                return Array.Empty<BanTransaccionListDto>();
            }

            var bancoIdToUse = bancoId.HasValue && bancoId.Value > 0
                ? bancoId.Value
                : (cuentaInfo.ban_banco_id ?? 0);

            if (bancoIdToUse <= 0)
            {
                // LOG: Banco ID invÃ¡lido
                Console.WriteLine($"âŒ BancoId invÃ¡lido: bancoId={bancoId}, ban_banco_id={cuentaInfo.ban_banco_id}");
                return Array.Empty<BanTransaccionListDto>();
            }

            var fechaDesdeValue = fechaDesde ?? GetFirstDayOfCurrentMonth();
            var fechaHastaValue = fechaHasta ?? GetLastDayOfCurrentMonth();

            // LOG: Llamada al SP
            Console.WriteLine($"âœ… Llamando SP: banco={bancoIdToUse}, cuenta={cuentaInfo.banco_cuenta_id}, desde={fechaDesdeValue}, hasta={fechaHastaValue}");

            var transaccionesSp = await GetTransaccionesFromProcedureAsync(
                bancoIdToUse,
                cuentaInfo.banco_cuenta_id,
                fechaDesdeValue,
                fechaHastaValue,
                ct);

            await CompletarCamposDesdeKardexAsync(companyId, transaccionesSp, ct);

            // LOG: Resultado del SP
            Console.WriteLine($"ðŸ“Š SP retornÃ³ {transaccionesSp.Count} registros");

            var filtradas = AplicarFiltroFechas(transaccionesSp, fechaDesde, fechaHasta);
            if (incluirAnuladas)
            {
                await AsignarEstadoTransaccionesAsync(filtradas, companyId, ct);
                return filtradas;
            }

            var sinAnuladas = await ExcluirTransaccionesAnuladasAsync(filtradas, companyId, ct);
            AsignarEstadoActivas(sinAnuladas);
            return sinAnuladas;
        }

        var query = context.ban_kardex
            .Include(k => k.banco_cuenta)
            .Where(k => k.company_id == companyId);

        if (bancoCuentaId.HasValue)
        {
            query = query.Where(k => k.banco_cuenta_id == bancoCuentaId.Value);
        }

        if (fechaDesde.HasValue)
        {
            query = query.Where(k => k.fecha_movimiento >= fechaDesde.Value);
        }

        if (fechaHasta.HasValue)
        {
            query = query.Where(k => k.fecha_movimiento <= fechaHasta.Value);
        }

        var transacciones = await (
            from k in query
            join ttLookup in context.ban_tipos_transacciones.Where(t => t.company_id == companyId)
                on k.id_tipo_transaccion equals ttLookup.ban_tipo_transaccion_id into ttJoin
            from tt in ttJoin.DefaultIfEmpty()
            orderby k.fecha_movimiento descending, k.ban_kardex_id descending
            select new BanTransaccionListDto
            {
                BanKardexId = k.ban_kardex_id,
                BancoCuentaId = k.banco_cuenta_id,
                BancoNombre = k.banco_cuenta.banco_nombre
                    ?? (k.banco_cuenta.ban_banco != null ? k.banco_cuenta.ban_banco.nombre : null),
                CuentaNombre = k.banco_cuenta.nombre,
                NumeroCuenta = k.banco_cuenta.numero_cuenta,
                MonedaCodigo = k.ban_moneda != null
                    ? k.ban_moneda.codigo
                    : k.banco_cuenta.currency_code,
                IdTipoTransaccion = tt != null ? tt.tipo_transaccion : k.id_tipo_transaccion.ToString(),
                FechaMovimiento = k.fecha_movimiento,
                FechaRegistro = k.fecha_registro,
                Descripcion = k.descripcion ?? string.Empty,
                Referencia = k.referencia,
                Monto = k.monto,
                SaldoResultante = k.saldo,
                CreadoPor = k.created_by,
                CreadoEn = k.created_at,
                ActualizadoPor = k.updated_by,
                ActualizadoEn = k.updated_at
            })
            .ToListAsync(ct);

        await CompletarCamposDesdeKardexAsync(companyId, transacciones, ct);

        if (incluirAnuladas)
        {
            await AsignarEstadoTransaccionesAsync(transacciones, companyId, ct);
            return transacciones;
        }

        var transaccionesActivas = await ExcluirTransaccionesAnuladasAsync(transacciones, companyId, ct);
        AsignarEstadoActivas(transaccionesActivas);
        return transaccionesActivas;
    }

    public async Task<BanTransaccionListDto?> GetTransaccionByIdAsync(
        long banKardexId,
        long companyId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(banKardexId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var transaccion = await (
            from k in context.ban_kardex
            where k.ban_kardex_id == banKardexId && k.company_id == companyId
            join ttLookup in context.ban_tipos_transacciones.Where(t => t.company_id == companyId)
                on k.id_tipo_transaccion equals ttLookup.ban_tipo_transaccion_id into ttJoin
            from tt in ttJoin.DefaultIfEmpty()
            select new BanTransaccionListDto
            {
                BanKardexId = k.ban_kardex_id,
                BancoCuentaId = k.banco_cuenta_id,
                BancoNombre = k.banco_cuenta.banco_nombre
                    ?? (k.banco_cuenta.ban_banco != null ? k.banco_cuenta.ban_banco.nombre : null),
                CuentaNombre = k.banco_cuenta.nombre,
                NumeroCuenta = k.banco_cuenta.numero_cuenta,
                MonedaCodigo = k.ban_moneda != null
                    ? k.ban_moneda.codigo
                    : k.banco_cuenta.currency_code,
                IdTipoTransaccion = tt != null ? tt.tipo_transaccion : k.id_tipo_transaccion.ToString(),
                FechaMovimiento = k.fecha_movimiento,
                FechaRegistro = k.fecha_registro,
                Descripcion = k.descripcion ?? string.Empty,
                Referencia = k.referencia,
                Monto = k.monto,
                SaldoResultante = k.saldo,
                CreadoPor = k.created_by,
                CreadoEn = k.created_at,
                ActualizadoPor = k.updated_by,
                ActualizadoEn = k.updated_at
            })
            .FirstOrDefaultAsync(ct);

        return transaccion;
    }

    public async Task<BanTransaccionDetalleDto?> GetTransaccionDetalleAsync(
        long banKardexId,
        long companyId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(banKardexId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(companyId);

        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = context.Database.GetDbConnection().ConnectionString;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var cursorName = "sp_ban_kardex_detalle_cursor";
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "public.sp_ban_kardex_detalle";

                command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
                command.Parameters.AddWithValue("p_ban_kardex_id", NpgsqlDbType.Bigint, banKardexId);

                var cursorParam = new NpgsqlParameter("p_result", NpgsqlDbType.Refcursor)
                {
                    Direction = ParameterDirection.InputOutput,
                    Value = cursorName
                };
                command.Parameters.Add(cursorParam);

                await command.ExecuteNonQueryAsync(ct);

                if (cursorParam.Value is string value && !string.IsNullOrWhiteSpace(value))
                {
                    cursorName = value;
                }
            }

            BanTransaccionDetalleDto? detalle = null;
            var lineas = new List<BanTransaccionContraLineaDto>();

            await using (var fetch = connection.CreateCommand())
            {
                fetch.Transaction = transaction;
                fetch.CommandText = $"FETCH ALL IN \"{cursorName}\";";

                await using var reader = await fetch.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    if (detalle is null)
                    {
                        var fechaMovimiento = TryReadDateOnly(reader, 3) ?? DateOnly.MinValue;
                        var descripcion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                        var referencia = reader.IsDBNull(5) ? null : reader.GetString(5);
                        var tasaCambio = reader.IsDBNull(6) ? 1m : reader.GetDecimal(6);
                        var monto = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7);

                        detalle = new BanTransaccionDetalleDto
                        {
                            BanKardexId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                            BancoCuentaId = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                            IdTipoTransaccion = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            FechaMovimiento = fechaMovimiento,
                            Descripcion = descripcion,
                            Referencia = referencia,
                            Monto = Math.Abs(monto),
                            TasaCambio = tasaCambio <= 0m ? 1m : tasaCambio
                        };
                    }

                    if (!reader.IsDBNull(10))
                    {
                        var accountId = reader.GetInt64(10);
                        var lineDescription = reader.IsDBNull(11) ? null : reader.GetString(11);
                        var debit = reader.IsDBNull(12) ? 0m : reader.GetDecimal(12);
                        var credit = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13);
                        var source = reader.IsDBNull(14) ? null : reader.GetString(14);

                        var montoLinea = Math.Abs(debit != 0m ? debit : credit);
                        if (accountId > 0 && montoLinea > 0m)
                        {
                            lineas.Add(new BanTransaccionContraLineaDto
                            {
                                CuentaId = accountId,
                                Monto = montoLinea,
                                Descripcion = string.IsNullOrWhiteSpace(lineDescription)
                                    ? detalle?.Descripcion
                                    : lineDescription.Trim(),
                                SourceDocument = string.IsNullOrWhiteSpace(source)
                                    ? detalle?.Referencia
                                    : source.Trim()
                            });
                        }
                    }
                }
            }

            await using (var close = connection.CreateCommand())
            {
                close.Transaction = transaction;
                close.CommandText = $"CLOSE \"{cursorName}\";";
                await close.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);

            if (detalle is null)
            {
                return null;
            }

            detalle.ContraCuentas = lineas;
            return detalle;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoAsync(
        long bancoCuentaId,
        string idTipoTransaccion,
        DateOnly fechaMovimiento,
        string descripcion,
        string? referencia,
        string? sourceDocument,
        decimal tasaCambio,
        decimal monto,
        IReadOnlyList<BanTransaccionContraLineaDto> contraCuentas,
        string usuario,
        CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(idTipoTransaccion);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(descripcion);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(referencia);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(usuario);

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (fechaMovimiento > today)
        {
            throw new ArgumentException("No se permiten transacciones futuras.", nameof(fechaMovimiento));
        }

        if (contraCuentas is null || contraCuentas.Count == 0)
        {
            throw new ArgumentException("Debe especificar al menos una contracuenta.", nameof(contraCuentas));
        }

        var contraLineas = contraCuentas
            .Where(l => l is not null && l.CuentaId > 0 && l.Monto > 0)
            .Select(l => new BanTransaccionContraLineaDto
            {
                CuentaId = l.CuentaId,
                Monto = l.Monto,
                Descripcion = string.IsNullOrWhiteSpace(l.Descripcion) ? null : l.Descripcion.Trim(),
                SourceDocument = NormalizeSourceDocument(l.SourceDocument)
            })
            .ToList();

        if (contraLineas.Count == 0)
        {
            throw new ArgumentException("Debe especificar al menos una contracuenta vÃ¡lida.", nameof(contraCuentas));
        }
        if (contraLineas.Any(l => string.IsNullOrWhiteSpace(l.Descripcion)))
        {
            throw new ArgumentException("La descripciÃ³n de la partida es obligatoria.", nameof(contraCuentas));
        }
        if (contraLineas.Any(l => string.IsNullOrWhiteSpace(l.SourceDocument)))
        {
            throw new ArgumentException("La referencia de la partida es obligatoria.", nameof(contraCuentas));
        }

        var totalContra = contraLineas.Sum(l => l.Monto);
        if (totalContra <= 0)
        {
            throw new ArgumentException("El total de las contracuentas debe ser un valor positivo.", nameof(contraCuentas));
        }

        if (monto > 0 && Math.Abs(monto - totalContra) > 0.01m)
        {
            throw new ArgumentException("El monto no coincide con el total de las contracuentas.", nameof(monto));
        }

        var tasaCambioNormalizada = NormalizeExchangeRate(tasaCambio);
        var monedaCuenta = await context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.banco_cuenta_id == bancoCuentaId)
            .Select(c => c.currency_code)
            .FirstOrDefaultAsync(ct);

        var moneda = string.IsNullOrWhiteSpace(monedaCuenta)
            ? "HNL"
            : monedaCuenta.Trim().ToUpperInvariant();

        if (!string.Equals(moneda, "USD", StringComparison.OrdinalIgnoreCase))
        {
            tasaCambioNormalizada = 1m;
        }
        else if (tasaCambioNormalizada <= 0m)
        {
            throw new ArgumentException("La tasa de cambio es obligatoria para cuentas en USD.", nameof(tasaCambio));
        }

        var partidaId = await RegistrarPartidaContableAsync(
            companyId,
            bancoCuentaId,
            contraLineas,
            idTipoTransaccion,
            fechaMovimiento,
            descripcion,
            referencia,
            sourceDocument,
            totalContra,
            usuario,
            ct);

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "public.sp_ban_kardex_registrar_movimiento";

            command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
            command.Parameters.AddWithValue("p_movimiento_id", NpgsqlDbType.Bigint, 0);
            command.Parameters.AddWithValue("p_id_tipo_transaccion", NpgsqlDbType.Varchar, idTipoTransaccion.Trim());
            command.Parameters.AddWithValue("p_fecha_movimiento", NpgsqlDbType.Date, fechaMovimiento);
            command.Parameters.AddWithValue("p_descripcion", NpgsqlDbType.Varchar, descripcion.Trim());
            command.Parameters.Add(new NpgsqlParameter("p_referencia", NpgsqlDbType.Varchar)
            {
                Value = string.IsNullOrWhiteSpace(referencia) ? DBNull.Value : referencia.Trim()
            });
            command.Parameters.AddWithValue("p_tasa_cambio", NpgsqlDbType.Numeric, tasaCambioNormalizada);
            command.Parameters.AddWithValue("p_monto", NpgsqlDbType.Numeric, totalContra);
            command.Parameters.AddWithValue("p_usuario", NpgsqlDbType.Varchar, usuario.Trim());

            var kardexParam = new NpgsqlParameter("p_ban_kardex_id", NpgsqlDbType.Bigint)
            {
                Direction = ParameterDirection.Output
            };
            var saldoParam = new NpgsqlParameter("p_saldo_resultante", NpgsqlDbType.Numeric)
            {
                Direction = ParameterDirection.Output
            };

            command.Parameters.Add(kardexParam);
            command.Parameters.Add(saldoParam);

            await command.ExecuteNonQueryAsync(ct);

            var kardexId = kardexParam.Value is DBNull ? 0 : Convert.ToInt64(kardexParam.Value);
            var saldoResultante = saldoParam.Value is DBNull ? 0 : Convert.ToDecimal(saldoParam.Value);

            if (kardexId <= 0)
            {
                throw new InvalidOperationException("No fue posible registrar la transacciÃ³n bancaria.");
            }

            if (partidaId.HasValue && partidaId.Value > 0)
            {
                await VincularPartidaEnKardexAsync(
                    connection,
                    companyId,
                    kardexId,
                    partidaId.Value,
                    ct);
            }

            return (BanKardexId: kardexId, SaldoResultante: saldoResultante);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<long?> RegistrarPartidaContableAsync(
        long companyId,
        long bancoCuentaId,
        IReadOnlyList<BanTransaccionContraLineaDto> contraLineas,
        string idTipoTransaccion,
        DateOnly fechaMovimiento,
        string descripcion,
        string? referencia,
        string? sourceDocument,
        decimal monto,
        string usuario,
        CancellationToken ct)
    {
        var cuentaBanco = await context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.banco_cuenta_id == bancoCuentaId)
            .Select(c => new { c.cont_account_id, c.currency_code })
            .FirstOrDefaultAsync(ct);

        if (cuentaBanco is null)
        {
            throw new InvalidOperationException("La cuenta bancaria no existe o no pertenece a la empresa actual.");
        }

        if (!cuentaBanco.cont_account_id.HasValue || cuentaBanco.cont_account_id.Value <= 0)
        {
            throw new InvalidOperationException("La cuenta bancaria no tiene una cuenta contable asociada.");
        }

        if (contraLineas is null || contraLineas.Count == 0)
        {
            throw new InvalidOperationException("Debe especificar al menos una contracuenta.");
        }

        var contraIds = contraLineas
            .Select(l => l.CuentaId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (contraIds.Count == 0)
        {
            throw new InvalidOperationException("Debe especificar al menos una contracuenta vÃ¡lida.");
        }

        var cuentasContra = await context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && contraIds.Contains(c.account_id))
            .Select(c => new { c.account_id, c.allows_posting, c.status, c.code, c.name })
            .ToListAsync(ct);

        if (cuentasContra.Count != contraIds.Count)
        {
            throw new InvalidOperationException("Una o mÃ¡s contracuentas no existen en la empresa actual.");
        }

        if (cuentasContra.Any(c => !c.allows_posting))
        {
            throw new InvalidOperationException("Una o mÃ¡s contracuentas no permiten movimientos.");
        }

        if (cuentasContra.Any(c => !IsCuentaActiva(c.status)))
        {
            throw new InvalidOperationException("Una o mÃ¡s contracuentas estÃ¡n inactivas.");
        }

        var cuentasContraLookup = cuentasContra.ToDictionary(c => c.account_id, c => (c.code, c.name));

        var tipoInfo = await context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.tipo_transaccion == idTipoTransaccion)
            .Select(t => new { t.entra_sale, t.cod_tipopartida, t.cod_centrocosto })
            .FirstOrDefaultAsync(ct);

        if (tipoInfo is null)
        {
            throw new InvalidOperationException("El tipo de transacciÃ³n no estÃ¡ configurado.");
        }

        var entraSale = string.IsNullOrWhiteSpace(tipoInfo.entra_sale)
            ? string.Empty
            : tipoInfo.entra_sale.Trim().ToUpperInvariant();

        if (entraSale != "E" && entraSale != "S")
        {
            throw new InvalidOperationException("El tipo de transacciÃ³n no tiene configuraciÃ³n vÃ¡lida de entrada/salida.");
        }

        var periodId = await ResolvePeriodoIdAsync(companyId, fechaMovimiento, ct);
        var journalId = await ResolveJournalIdAsync(companyId, ct);
        var typeId = await ResolveTipoPartidaIdAsync(companyId, tipoInfo.cod_tipopartida, ct);

        var totalContra = contraLineas.Sum(l => l.Monto);
        if (totalContra <= 0)
        {
            throw new InvalidOperationException("El total de las contracuentas debe ser un valor positivo.");
        }

        var amount = Math.Abs(monto);
        if (Math.Abs(amount - totalContra) > 0.01m)
        {
            throw new InvalidOperationException("El monto de la transacciÃ³n no coincide con el total de las contracuentas.");
        }

        var bankDebit = entraSale == "E" ? totalContra : 0m;
        var bankCredit = entraSale == "S" ? totalContra : 0m;

        var currency = string.IsNullOrWhiteSpace(cuentaBanco.currency_code)
            ? "HNL"
            : cuentaBanco.currency_code.Trim().ToUpperInvariant();

        var descripcionNormalizada = descripcion.Trim();
        if (descripcionNormalizada.Length > 500)
        {
            descripcionNormalizada = descripcionNormalizada[..500];
        }

        var documento = BuildDocumentNumber(idTipoTransaccion, referencia);
        var sourceDocumentNormalizado = NormalizeSourceDocument(sourceDocument);
        var partidaDate = DateTime.SpecifyKind(
            fechaMovimiento.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        var costCenterId = tipoInfo.cod_centrocosto.HasValue && tipoInfo.cod_centrocosto.Value > 0
            ? tipoInfo.cod_centrocosto.Value
            : (long?)null;

        var lineas = new List<PartidaLinea>
        {
            new PartidaLinea(
                cuentaBanco.cont_account_id.Value,
                null,
                descripcionNormalizada,
                bankDebit,
                bankCredit,
                null,
                currency,
                1m)
        };
        var sourceDocuments = new List<string?> { sourceDocumentNormalizado };

        foreach (var linea in contraLineas)
        {
            var lineAmount = Math.Abs(linea.Monto);
            if (lineAmount <= 0)
            {
                continue;
            }

            var descripcionLinea = BuildContraDescripcion(linea, cuentasContraLookup);
            if (descripcionLinea.Length > 500)
            {
                descripcionLinea = descripcionLinea[..500];
            }

            var lineSource = NormalizeSourceDocument(linea.SourceDocument) ?? sourceDocumentNormalizado;
            sourceDocuments.Add(lineSource);

            lineas.Add(new PartidaLinea(
                linea.CuentaId,
                costCenterId,
                descripcionLinea,
                entraSale == "S" ? lineAmount : 0m,
                entraSale == "E" ? lineAmount : 0m,
                null,
                currency,
                1m));
        }

        if (lineas.Count < 2)
        {
            throw new InvalidOperationException("La partida contable requiere al menos dos lÃ­neas.");
        }

        var lineasSql = BuildLineasSql(lineas.Count);

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        long? partidaId = null;

        try
        {
            await using var dbTransaction = await connection.BeginTransactionAsync(ct);

            await ValidarYAplicarPresupuestoCreditosAsync(
                connection,
                dbTransaction,
                companyId,
                fechaMovimiento,
                lineas,
                ct);

            var supportsSourceDocuments = await SupportsSourceDocumentsAsync(connection, dbTransaction as NpgsqlTransaction, ct);
            var partidaNumber = await GenerateMonthlyPartidaNumberAsync(connection, dbTransaction as NpgsqlTransaction, companyId, partidaDate, ct);

            await using var command = connection.CreateCommand();
            command.Transaction = dbTransaction;
            command.CommandType = CommandType.Text;
            command.CommandText = supportsSourceDocuments
                ? $@"
CALL public.sp_registrar_partida_contable(
    @p_company_id,
    @p_journal_id,
    @p_period_id,
    @p_module,
    @p_document_type,
    @p_document_number,
    @p_partida_number,
    @p_partida_date,
    @p_description,
    @p_created_by,
    @p_type_id,
    @p_source_documents,
    {lineasSql}
);"
                : $@"
CALL public.sp_registrar_partida_contable(
    @p_company_id,
    @p_journal_id,
    @p_period_id,
    @p_module,
    @p_document_type,
    @p_document_number,
    @p_partida_number,
    @p_partida_date,
    @p_description,
    @p_created_by,
    @p_type_id,
    {lineasSql}
);";

            command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("p_journal_id", NpgsqlDbType.Bigint, journalId);
            command.Parameters.AddWithValue("p_period_id", NpgsqlDbType.Bigint, periodId);
            command.Parameters.AddWithValue("p_module", NpgsqlDbType.Varchar, "BANCOS");
            command.Parameters.AddWithValue("p_document_type", NpgsqlDbType.Varchar, idTipoTransaccion.Trim());
            command.Parameters.AddWithValue("p_document_number", NpgsqlDbType.Varchar, documento);
            command.Parameters.AddWithValue("p_partida_number", NpgsqlDbType.Varchar, partidaNumber);
            command.Parameters.AddWithValue("p_partida_date", NpgsqlDbType.TimestampTz, partidaDate);
            command.Parameters.AddWithValue("p_description", NpgsqlDbType.Varchar, descripcionNormalizada);
            command.Parameters.AddWithValue("p_created_by", NpgsqlDbType.Varchar, usuario.Trim());
            command.Parameters.AddWithValue("p_type_id", NpgsqlDbType.Bigint, typeId);
            if (supportsSourceDocuments)
            {
                command.Parameters.Add(new NpgsqlParameter("p_source_documents", NpgsqlDbType.Array | NpgsqlDbType.Varchar)
                {
                    Value = sourceDocuments.ToArray()
                });
            }

            for (var i = 0; i < lineas.Count; i++)
            {
                var linea = lineas[i];
                command.Parameters.AddWithValue($"acc_{i}", NpgsqlDbType.Bigint, linea.AccountId);
                command.Parameters.Add(new NpgsqlParameter($"cc_{i}", NpgsqlDbType.Bigint)
                {
                    Value = linea.CostCenterId.HasValue ? linea.CostCenterId.Value : DBNull.Value
                });
                command.Parameters.AddWithValue($"desc_{i}", NpgsqlDbType.Varchar, linea.Description);
                command.Parameters.AddWithValue($"deb_{i}", NpgsqlDbType.Numeric, linea.Debit);
                command.Parameters.AddWithValue($"cred_{i}", NpgsqlDbType.Numeric, linea.Credit);
                command.Parameters.Add(new NpgsqlParameter($"third_{i}", NpgsqlDbType.Bigint)
                {
                    Value = linea.ThirdPartyId.HasValue ? linea.ThirdPartyId.Value : DBNull.Value
                });
                command.Parameters.AddWithValue($"curr_{i}", NpgsqlDbType.Varchar, linea.CurrencyCode);
                command.Parameters.AddWithValue($"rate_{i}", NpgsqlDbType.Numeric, linea.ExchangeRate);
            }

            await command.ExecuteNonQueryAsync(ct);

            partidaId = await TryResolvePartidaIdAsync(
                connection,
                dbTransaction,
                companyId,
                idTipoTransaccion,
                documento,
                usuario,
                ct);

            if (!partidaId.HasValue || partidaId.Value <= 0)
            {
                throw new InvalidOperationException("No fue posible resolver la poliza contable generada para ejecutar el posteo.");
            }

            await PostearPolizaAsync(
                connection,
                companyId,
                partidaId.Value,
                usuario,
                ct);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return partidaId;
    }

    private static async Task PostearPolizaAsync(
        NpgsqlConnection connection,
        long companyId,
        long polizaId,
        string usuario,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = "SELECT public.sp_con_postear_poliza(@p_company_id, @p_poliza_id, @p_user);";
        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_poliza_id", NpgsqlDbType.Bigint, polizaId);
        command.Parameters.AddWithValue("p_user", NpgsqlDbType.Varchar, usuario.Trim());

        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> SupportsSourceDocumentsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT 1
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public'
  AND p.proname = 'sp_registrar_partida_contable'
  AND p.proargnames IS NOT NULL
  AND 'p_source_documents' = ANY(p.proargnames)
LIMIT 1;";

        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && result is not DBNull;
    }

    private static async Task VincularPartidaEnKardexAsync(
        NpgsqlConnection connection,
        long companyId,
        long banKardexId,
        long partidaId,
        CancellationToken ct,
        NpgsqlTransaction? transaction = null)
    {
        var (hasPolizaId, hasPartidaCuentaId) = await DetectarColumnasPartidaEnKardexAsync(connection, ct, transaction);

        if (!hasPolizaId && !hasPartidaCuentaId)
        {
            return;
        }

        string setClause;
        if (hasPolizaId && hasPartidaCuentaId)
        {
            setClause = "poliza_id = @partida_id, partida_cuenta_id = @partida_id";
        }
        else if (hasPolizaId)
        {
            setClause = "poliza_id = @partida_id";
        }
        else
        {
            setClause = "partida_cuenta_id = @partida_id";
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandType = CommandType.Text;
        updateCommand.CommandText = $@"
UPDATE public.ban_kardex
   SET {setClause}
 WHERE company_id = @company_id
   AND ban_kardex_id = @ban_kardex_id;";

        updateCommand.Parameters.AddWithValue("partida_id", NpgsqlDbType.Bigint, partidaId);
        updateCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        updateCommand.Parameters.AddWithValue("ban_kardex_id", NpgsqlDbType.Bigint, banKardexId);

        await updateCommand.ExecuteNonQueryAsync(ct);
    }

    private static async Task<(bool HasPolizaId, bool HasPartidaCuentaId)> DetectarColumnasPartidaEnKardexAsync(
        NpgsqlConnection connection,
        CancellationToken ct,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'ban_kardex'
  AND column_name IN ('poliza_id', 'partida_cuenta_id');";

        var hasPolizaId = false;
        var hasPartidaCuentaId = false;

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var columnName = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (string.Equals(columnName, "poliza_id", StringComparison.OrdinalIgnoreCase))
            {
                hasPolizaId = true;
            }
            else if (string.Equals(columnName, "partida_cuenta_id", StringComparison.OrdinalIgnoreCase))
            {
                hasPartidaCuentaId = true;
            }
        }

        return (hasPolizaId, hasPartidaCuentaId);
    }

    private static async Task<long?> TryResolvePartidaIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string idTipoTransaccion,
        string documentNumber,
        string createdBy,
        CancellationToken ct)
    {
        var normalizedType = idTipoTransaccion.Trim();
        var normalizedDocument = documentNumber.Trim();
        var normalizedUser = createdBy.Trim();

        if (await TableExistsAsync(connection, transaction, "con_partida_hdr", ct))
        {
            var partidaId = await TryResolvePartidaIdInConPartidaHdrAsync(
                connection,
            transaction,
                companyId,
                normalizedType,
                normalizedDocument,
                normalizedUser,
                ct);

            if (partidaId.HasValue)
            {
                return partidaId;
            }
        }

        if (await TableExistsAsync(connection, transaction, "con_poliza", ct))
        {
            var partidaId = await TryResolvePartidaIdInConPolizaAsync(
                connection,
            transaction,
                companyId,
                normalizedType,
                normalizedDocument,
                normalizedUser,
                ct);

            if (partidaId.HasValue)
            {
                return partidaId;
            }
        }

        return null;
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT 1
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @table_name
LIMIT 1;";

        command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && result is not DBNull;
    }

    private static async Task<long?> TryResolvePartidaIdInConPartidaHdrAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string documentType,
        string documentNumber,
        string createdBy,
        CancellationToken ct)
    {
        const string sql = @"
SELECT poliza_id
 FROM public.con_poliza
WHERE company_id = @company_id
  AND ""module"" = 'BANCOS'
  AND document_type = @document_type
  AND (
        btrim(document_number) = btrim(@document_number)
     OR btrim(poliza_number) = btrim(@document_number)
  )
  AND created_by = @created_by
ORDER BY poliza_id DESC
LIMIT 1;";

        var partidaId = await ExecuteScalarInt64Async(
            connection,
            transaction,
            sql,
            companyId,
            documentType,
            documentNumber,
            createdBy,
            ct);

        if (partidaId.HasValue)
        {
            return partidaId;
        }

        const string sqlSinUsuario = @"
SELECT poliza_id
 FROM public.con_poliza
WHERE company_id = @company_id
  AND ""module"" = 'BANCOS'
  AND document_type = @document_type
  AND (
        btrim(document_number) = btrim(@document_number)
     OR btrim(poliza_number) = btrim(@document_number)
  )
ORDER BY poliza_id DESC
LIMIT 1;";

        return await ExecuteScalarInt64Async(
            connection,
            transaction,
            sqlSinUsuario,
            companyId,
            documentType,
            documentNumber,
            null,
            ct);
    }

    private static async Task<long?> TryResolvePartidaIdInConPolizaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string documentType,
        string documentNumber,
        string createdBy,
        CancellationToken ct)
    {
        const string sql = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = 'BANCOS'
  AND document_type = @document_type
  AND (
        btrim(document_number) = btrim(@document_number)
     OR btrim(poliza_number) = btrim(@document_number)
     OR btrim(coalesce(source_reference, '')) = btrim(@document_number)
  )
  AND created_by = @created_by
ORDER BY poliza_id DESC
LIMIT 1;";

        var partidaId = await ExecuteScalarInt64Async(
            connection,
            transaction,
            sql,
            companyId,
            documentType,
            documentNumber,
            createdBy,
            ct);

        if (partidaId.HasValue)
        {
            return partidaId;
        }

        const string sqlSinUsuario = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = 'BANCOS'
  AND document_type = @document_type
  AND (
        btrim(document_number) = btrim(@document_number)
     OR btrim(poliza_number) = btrim(@document_number)
     OR btrim(coalesce(source_reference, '')) = btrim(@document_number)
  )
ORDER BY poliza_id DESC
LIMIT 1;";

        return await ExecuteScalarInt64Async(
            connection,
            transaction,
            sqlSinUsuario,
            companyId,
            documentType,
            documentNumber,
            null,
            ct);
    }

    private static async Task<long?> ExecuteScalarInt64Async(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        long companyId,
        string documentType,
        string documentNumber,
        string? createdBy,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("document_type", NpgsqlDbType.Varchar, documentType);
        command.Parameters.AddWithValue("document_number", NpgsqlDbType.Varchar, documentNumber);
        if (createdBy is not null)
        {
            command.Parameters.AddWithValue("created_by", NpgsqlDbType.Varchar, createdBy);
        }

        var result = await command.ExecuteScalarAsync(ct);
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt64(result);
    }

    private static async Task ValidarYAplicarPresupuestoCreditosAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        DateOnly fechaMovimiento,
        IReadOnlyList<PartidaLinea> lineas,
        CancellationToken ct)
    {
        var cuentasDisplay = await ObtenerDisplayCuentasAsync(
            connection,
            transaction,
            companyId,
            lineas.Select(l => l.AccountId),
            ct);

        foreach (var linea in lineas)
        {
            var credito = Math.Abs(linea.Credit);
            if (credito <= 0m)
            {
                continue;
            }

            var cuentaDisplay = cuentasDisplay.TryGetValue(linea.AccountId, out var display)
                ? display
                : linea.AccountId.ToString();

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.Text;
            command.CommandText = @"
SELECT public.fn_pst_afectar_saldo_real_credito(
    @p_company_id,
    @p_account_id,
    @p_poliza_date,
    @p_credito
);";

            command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("p_account_id", NpgsqlDbType.Bigint, linea.AccountId);
            command.Parameters.AddWithValue("p_poliza_date", NpgsqlDbType.Date, fechaMovimiento);
            command.Parameters.AddWithValue("p_credito", NpgsqlDbType.Numeric, credito);

            var result = await command.ExecuteScalarAsync(ct);
            var estado = result is DBNull || result is null ? 1 : Convert.ToInt32(result);
            if (estado == 0)
            {
                throw new ArgumentException(
                    $"La transacciÃ³n excede el saldo proyectado del presupuesto para la cuenta {cuentaDisplay}.");
            }

            if (estado == 2)
            {
                throw new ArgumentException(
                    $"No se puede registrar la transacciÃ³n porque el presupuesto de la cuenta {cuentaDisplay} no estÃ¡ aprobado.");
            }
        }
    }

    private static async Task<Dictionary<long, string>> ObtenerDisplayCuentasAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        IEnumerable<long> accountIds,
        CancellationToken ct)
    {
        var ids = accountIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<long, string>();
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = @"
SELECT c.account_id, c.code, c.name
FROM public.con_plan_cuentas c
WHERE c.company_id = @p_company_id
  AND c.account_id = ANY(@p_account_ids);";

        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.Add(new NpgsqlParameter("p_account_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
        {
            Value = ids
        });

        var cuentas = new Dictionary<long, string>();

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var accountId = reader.GetInt64(0);
            var code = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
            var name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim();

            cuentas[accountId] = string.IsNullOrWhiteSpace(code)
                ? (string.IsNullOrWhiteSpace(name) ? accountId.ToString() : name)
                : string.IsNullOrWhiteSpace(name)
                    ? code
                    : $"{code} - {name}";
        }

        return cuentas;
    }

    private async Task<long> ResolveJournalIdAsync(long companyId, CancellationToken ct)
    {
        var journalId = await context.con_diarios
            .AsNoTracking()
            .Where(d => d.company_id == companyId
                        && d.is_active
                        && d.code.ToUpper() == "BAN")
            .Select(d => d.journal_id)
            .FirstOrDefaultAsync(ct);

        if (journalId > 0)
        {
            return journalId;
        }

        journalId = await context.con_diarios
            .AsNoTracking()
            .Where(d => d.company_id == companyId && d.is_active)
            .OrderBy(d => d.journal_id)
            .Select(d => d.journal_id)
            .FirstOrDefaultAsync(ct);

        if (journalId <= 0)
        {
            throw new InvalidOperationException("No se encontrÃ³ un diario contable activo para registrar la partida.");
        }

        return journalId;
    }

    private async Task<long> ResolvePeriodoIdAsync(long companyId, DateOnly fechaMovimiento, CancellationToken ct)
    {
        var fecha = DateTime.SpecifyKind(
            fechaMovimiento.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        var periodos = await context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId
                        && p.start_date <= fecha
                        && p.end_date >= fecha)
            .OrderByDescending(p => p.end_date)
            .Select(p => new { p.period_id, p.status_id })
            .ToListAsync(ct);

        var periodoAbierto = periodos.FirstOrDefault(p => EstadoPeriodoHelper.IsOpen(p.status_id));
        if (periodoAbierto is not null && periodoAbierto.period_id > 0)
        {
            return periodoAbierto.period_id;
        }

        var periodo = periodos.FirstOrDefault();
        if (periodo is not null && periodo.period_id > 0)
        {
            return periodo.period_id;
        }

        throw new InvalidOperationException("No se encontro un periodo contable valido para la fecha del movimiento.");
    }

    private async Task<long> ResolveTipoPartidaIdAsync(long companyId, string? codTipopartida, CancellationToken ct)
    {
        var tipos = await context.con_tipo_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId)
            .OrderBy(t => t.type_id)
            .Select(t => new { t.type_id, t.code, t.is_default, t.status_id, t.status })
            .ToListAsync(ct);

        var activos = tipos
            .Where(t => IsTipoTransaccionActiva(t.status_id, t.status))
            .ToList();

        if (activos.Count == 0)
        {
            throw new InvalidOperationException("No hay un tipo de transaccion contable activo configurado.");
        }

        if (!string.IsNullOrWhiteSpace(codTipopartida)
            && long.TryParse(codTipopartida.Trim(), out var parsed)
            && parsed > 0)
        {
            var porId = activos.FirstOrDefault(t => t.type_id == parsed);
            if (porId is not null)
            {
                return porId.type_id;
            }
        }

        var tipoDefault = activos.FirstOrDefault(t => t.is_default);
        if (tipoDefault is not null)
        {
            return tipoDefault.type_id;
        }

        var tipoDiario = activos.FirstOrDefault(t => string.Equals(t.code, "DIARIO", StringComparison.OrdinalIgnoreCase));
        if (tipoDiario is not null)
        {
            return tipoDiario.type_id;
        }

        return activos[0].type_id;
    }

    private static bool IsTipoTransaccionActiva(short? statusId, string? status)
    {
        if (statusId.HasValue)
        {
            return statusId.Value == 1;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GenerateMonthlyPartidaNumberAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long companyId,
        DateTime partidaDate,
        CancellationToken ct)
    {
        var year = partidaDate.Year;
        var month = partidaDate.Month;
        var prefix = $"{companyId}-{year:D4}-{month:D2}-";
        var periodKey = checked(year * 100 + month);
        var advisoryKey = unchecked((companyId << 32) ^ (uint)periodKey);

        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(@advisory_key);";
            lockCommand.Parameters.AddWithValue("advisory_key", NpgsqlDbType.Bigint, advisoryKey);
            await lockCommand.ExecuteNonQueryAsync(ct);
        }

        var existingNumbers = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = @"
SELECT poliza_number
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND poliza_number LIKE @prefix_pattern;";
            command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("prefix_pattern", NpgsqlDbType.Varchar, prefix + "%");

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                {
                    existingNumbers.Add(reader.GetString(0));
                }
            }
        }

        var lastSequence = 0;
        foreach (var existingNumber in existingNumbers)
        {
            if (existingNumber.Length <= prefix.Length)
                continue;

            if (int.TryParse(existingNumber[prefix.Length..], out var currentSequence) &&
                currentSequence > lastSequence)
            {
                lastSequence = currentSequence;
            }
        }

        return $"{prefix}{lastSequence + 1:D6}";
    }

    private static string BuildDocumentNumber(string idTipoTransaccion, string? referencia)
    {
        var baseValue = !string.IsNullOrWhiteSpace(referencia)
            ? referencia.Trim()
            : $"{idTipoTransaccion.Trim()}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        return baseValue.Length <= 50 ? baseValue : baseValue[..50];
    }

    private static string? NormalizeSourceDocument(string? sourceDocument)
    {
        if (string.IsNullOrWhiteSpace(sourceDocument))
        {
            return null;
        }

        var normalized = sourceDocument.Trim();
        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private static decimal NormalizeExchangeRate(decimal tasaCambio)
    {
        if (tasaCambio <= 0m)
        {
            return 0m;
        }

        return Math.Round(tasaCambio, 4, MidpointRounding.AwayFromZero);
    }

    private static string TrimToLength(string? value, int maxLength, string fallback = "")
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    public async Task<(long BanKardexIdAnulacion, decimal SaldoResultante)> AnularMovimientoAsync(
        long bancoCuentaId,
        long banKardexIdOriginal,
        string motivo,
        string usuario,
        CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(banKardexIdOriginal);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(usuario);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("El motivo es obligatorio.", nameof(motivo));
        }

        var motivoNormalizado = motivo.Trim();
        if (motivoNormalizado.Length > 500)
        {
            throw new ArgumentException("El motivo no puede superar 500 caracteres.", nameof(motivo));
        }

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var dbTransaction = await connection.BeginTransactionAsync(ct);

            await ValidarMovimientoNoConciliadoAsync(
                connection,
                dbTransaction,
                companyId,
                bancoCuentaId,
                banKardexIdOriginal,
                ct);

            var partidaOriginal = await TryObtenerPartidaContableOriginalAsync(
                connection,
                dbTransaction,
                companyId,
                bancoCuentaId,
                banKardexIdOriginal,
                ct);

            await using var command = connection.CreateCommand();
            command.Transaction = dbTransaction;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "public.sp_ban_kardex_anular_movimiento_recalcular";

            command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
            command.Parameters.AddWithValue("p_ban_kardex_id_original", NpgsqlDbType.Bigint, banKardexIdOriginal);
            command.Parameters.AddWithValue("p_motivo", NpgsqlDbType.Varchar, motivoNormalizado);
            command.Parameters.AddWithValue("p_usuario", NpgsqlDbType.Varchar, usuario.Trim());

            var kardexParam = new NpgsqlParameter("p_ban_kardex_id_anulacion", NpgsqlDbType.Bigint)
            {
                Direction = ParameterDirection.Output
            };
            var saldoParam = new NpgsqlParameter("p_saldo_resultante", NpgsqlDbType.Numeric)
            {
                Direction = ParameterDirection.Output
            };

            command.Parameters.Add(kardexParam);
            command.Parameters.Add(saldoParam);

            await command.ExecuteNonQueryAsync(ct);

            var kardexId = kardexParam.Value is DBNull ? 0 : Convert.ToInt64(kardexParam.Value);
            var saldoResultante = saldoParam.Value is DBNull ? 0 : Convert.ToDecimal(saldoParam.Value);

            if (kardexId <= 0)
            {
                throw new InvalidOperationException("No fue posible anular la transacciÃ³n bancaria.");
            }

            if (partidaOriginal is not null)
            {
                var partidaAnulacionId = await RegistrarPartidaContableReversaAsync(
                    connection,
                    dbTransaction,
                    companyId,
                    banKardexIdOriginal,
                    kardexId,
                    motivoNormalizado,
                    usuario,
                    partidaOriginal,
                    ct);

                if (partidaAnulacionId.HasValue && partidaAnulacionId.Value > 0)
                {
                    await VincularPartidaEnKardexAsync(
                        connection,
                        companyId,
                        kardexId,
                        partidaAnulacionId.Value,
                        ct,
                        dbTransaction);
                }
            }

            await dbTransaction.CommitAsync(ct);
            return (kardexId, saldoResultante);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ValidarMovimientoNoConciliadoAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long companyId,
        long bancoCuentaId,
        long banKardexIdOriginal,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = @"
SELECT UPPER(COALESCE(estado_conciliacion, 'NOC'))
FROM public.ban_kardex
WHERE company_id = @p_company_id
  AND banco_cuenta_id = @p_banco_cuenta_id
  AND ban_kardex_id = @p_ban_kardex_id
LIMIT 1;";

        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        command.Parameters.AddWithValue("p_ban_kardex_id", NpgsqlDbType.Bigint, banKardexIdOriginal);

        var estadoConciliacion = await command.ExecuteScalarAsync(ct) as string;
        if (string.Equals(estadoConciliacion, "CON", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "No se puede anular una transaccion bancaria que ya esta conciliada.",
                nameof(banKardexIdOriginal));
        }
    }

    private static async Task<PartidaContableOriginal?> TryObtenerPartidaContableOriginalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        long banKardexIdOriginal,
        CancellationToken ct)
    {
        var partidaId = await TryObtenerPartidaVinculadaAsync(
            connection,
            transaction,
            companyId,
            bancoCuentaId,
            banKardexIdOriginal,
            ct);

        if (!partidaId.HasValue || partidaId.Value <= 0)
        {
            return null;
        }

        var hasPartidaHdr = await TableExistsAsync(connection, transaction, "con_partida_hdr", ct);
        var hasPartidaDtl = await TableExistsAsync(connection, transaction, "con_partida_dtl", ct);
        if (!hasPartidaHdr || !hasPartidaDtl)
        {
            throw new InvalidOperationException("No estan disponibles las tablas contables requeridas para generar la contrapartida.");
        }

        await using var headerCommand = connection.CreateCommand();
        headerCommand.Transaction = transaction;
        headerCommand.CommandType = CommandType.Text;
        headerCommand.CommandText = @"
SELECT
    h.poliza_id,
    h.journal_id,
    h.period_id,
    h.type_id,
    h.document_type,
    h.document_number,
    h.poliza_number,
    h.description
FROM public.con_partida_hdr h
WHERE h.company_id = @p_company_id
  AND h.poliza_id = @p_poliza_id
LIMIT 1;";

        headerCommand.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        headerCommand.Parameters.AddWithValue("p_poliza_id", NpgsqlDbType.Bigint, partidaId.Value);

        await using var headerReader = await headerCommand.ExecuteReaderAsync(ct);
        if (!await headerReader.ReadAsync(ct))
        {
            throw new InvalidOperationException(
                $"La transaccion bancaria {banKardexIdOriginal} tiene una partida contable vinculada que no existe.");
        }

        var partidaOriginal = new PartidaContableOriginal(
            PartidaId: headerReader.GetInt64(0),
            JournalId: headerReader.IsDBNull(1) ? null : headerReader.GetInt64(1),
            PeriodId: headerReader.IsDBNull(2) ? null : headerReader.GetInt64(2),
            TypeId: headerReader.IsDBNull(3) ? null : headerReader.GetInt64(3),
            DocumentType: headerReader.IsDBNull(4) ? string.Empty : headerReader.GetString(4),
            DocumentNumber: headerReader.IsDBNull(5) ? null : headerReader.GetString(5),
            PolizaNumber: headerReader.IsDBNull(6) ? null : headerReader.GetString(6),
            Description: headerReader.IsDBNull(7) ? null : headerReader.GetString(7),
            Lineas: new List<PartidaContableOriginalLinea>());

        await headerReader.CloseAsync();

        await using var detailCommand = connection.CreateCommand();
        detailCommand.Transaction = transaction;
        detailCommand.CommandType = CommandType.Text;
        detailCommand.CommandText = @"
SELECT
    d.account_id,
    d.cost_center_id,
    d.description,
    COALESCE(d.debit_amount, 0),
    COALESCE(d.credit_amount, 0),
    d.third_party_id,
    d.currency_code,
    d.exchange_rate,
    d.source_document
FROM public.con_partida_dtl d
WHERE d.company_id = @p_company_id
  AND d.poliza_id = @p_poliza_id
ORDER BY d.line_number;";

        detailCommand.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        detailCommand.Parameters.AddWithValue("p_poliza_id", NpgsqlDbType.Bigint, partidaId.Value);

        await using var detailReader = await detailCommand.ExecuteReaderAsync(ct);
        while (await detailReader.ReadAsync(ct))
        {
            partidaOriginal.Lineas.Add(new PartidaContableOriginalLinea(
                AccountId: detailReader.GetInt64(0),
                CostCenterId: detailReader.IsDBNull(1) ? null : detailReader.GetInt64(1),
                Description: detailReader.IsDBNull(2) ? null : detailReader.GetString(2),
                Debit: detailReader.GetDecimal(3),
                Credit: detailReader.GetDecimal(4),
                ThirdPartyId: detailReader.IsDBNull(5) ? null : detailReader.GetInt64(5),
                CurrencyCode: detailReader.IsDBNull(6) ? null : detailReader.GetString(6),
                ExchangeRate: detailReader.IsDBNull(7) ? null : detailReader.GetDecimal(7),
                SourceDocument: detailReader.IsDBNull(8) ? null : detailReader.GetString(8)));
        }

        if (partidaOriginal.Lineas.Count == 0)
        {
            throw new InvalidOperationException(
                $"La partida contable {partidaOriginal.PartidaId} no tiene lineas para generar la contrapartida.");
        }

        return partidaOriginal;
    }

    private static async Task<long?> TryObtenerPartidaVinculadaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long bancoCuentaId,
        long banKardexIdOriginal,
        CancellationToken ct)
    {
        var (hasPolizaId, hasPartidaCuentaId) = await DetectarColumnasPartidaEnKardexAsync(connection, ct, transaction);
        if (!hasPolizaId && !hasPartidaCuentaId)
        {
            return null;
        }

        var partidaColumn = hasPolizaId && hasPartidaCuentaId
            ? "COALESCE(k.partida_cuenta_id, k.poliza_id)"
            : hasPartidaCuentaId
                ? "k.partida_cuenta_id"
                : "k.poliza_id";

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = $@"
SELECT {partidaColumn}
FROM public.ban_kardex k
WHERE k.company_id = @p_company_id
  AND k.banco_cuenta_id = @p_banco_cuenta_id
  AND k.ban_kardex_id = @p_ban_kardex_id
LIMIT 1;";

        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        command.Parameters.AddWithValue("p_ban_kardex_id", NpgsqlDbType.Bigint, banKardexIdOriginal);

        var result = await command.ExecuteScalarAsync(ct);
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt64(result);
    }

    private async Task<long?> RegistrarPartidaContableReversaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long banKardexIdOriginal,
        long banKardexIdAnulacion,
        string motivo,
        string usuario,
        PartidaContableOriginal partidaOriginal,
        CancellationToken ct)
    {
        var kardexAnulacion = await ObtenerDatosKardexAnulacionAsync(
            connection,
            transaction,
            companyId,
            banKardexIdAnulacion,
            ct);

        var fechaAnulacion = kardexAnulacion.FechaMovimiento;
        var journalId = partidaOriginal.JournalId.HasValue && partidaOriginal.JournalId.Value > 0
            ? partidaOriginal.JournalId.Value
            : await ResolveJournalIdAsync(companyId, ct);
        var periodId = await ResolvePeriodoIdAsync(companyId, fechaAnulacion, ct);
        var typeId = partidaOriginal.TypeId.HasValue && partidaOriginal.TypeId.Value > 0
            ? partidaOriginal.TypeId.Value
            : await ResolveTipoPartidaIdAsync(companyId, null, ct);

        var documentType = string.IsNullOrWhiteSpace(partidaOriginal.DocumentType)
            ? "BAN"
            : partidaOriginal.DocumentType.Trim();
        var documentNumber = BuildDocumentNumber(documentType, kardexAnulacion.Referencia);
        var partidaNumber = await GenerateMonthlyPartidaNumberAsync(connection, transaction, companyId, DateTime.SpecifyKind(fechaAnulacion.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc), ct);
        var description = TrimToLength(
            string.IsNullOrWhiteSpace(kardexAnulacion.Descripcion)
                ? $"ANULACION PARTIDA kardex={banKardexIdOriginal} {motivo}"
                : kardexAnulacion.Descripcion,
            500);
        var partidaDate = DateTime.SpecifyKind(
            fechaAnulacion.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        var lineas = partidaOriginal.Lineas
            .Select(linea => new PartidaLinea(
                linea.AccountId,
                linea.CostCenterId,
                TrimToLength(string.IsNullOrWhiteSpace(linea.Description) ? description : linea.Description, 300),
                linea.Credit,
                linea.Debit,
                linea.ThirdPartyId,
                string.IsNullOrWhiteSpace(linea.CurrencyCode) ? "HNL" : linea.CurrencyCode.Trim().ToUpperInvariant(),
                linea.ExchangeRate.HasValue && linea.ExchangeRate.Value > 0m ? linea.ExchangeRate.Value : 1m))
            .ToList();

        var fallbackSource = NormalizeSourceDocument(partidaOriginal.DocumentNumber)
            ?? NormalizeSourceDocument(partidaOriginal.PolizaNumber)
            ?? NormalizeSourceDocument(kardexAnulacion.Referencia)
            ?? documentNumber;
        var sourceDocuments = partidaOriginal.Lineas
            .Select(linea => NormalizeSourceDocument(linea.SourceDocument) ?? fallbackSource)
            .ToList();

        return await RegistrarPartidaContableReversaAsync(
            connection,
            transaction,
            companyId,
            journalId,
            periodId,
            documentType,
            documentNumber,
            partidaNumber,
            partidaDate,
            description,
            usuario,
            typeId,
            lineas,
            sourceDocuments,
            ct);
    }

    private static async Task<long?> RegistrarPartidaContableReversaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long journalId,
        long periodId,
        string documentType,
        string documentNumber,
        string partidaNumber,
        DateTime partidaDate,
        string description,
        string usuario,
        long typeId,
        IReadOnlyList<PartidaLinea> lineas,
        IReadOnlyList<string?> sourceDocuments,
        CancellationToken ct)
    {
        if (lineas.Count == 0)
        {
            return null;
        }

        var lineasSql = BuildLineasSql(lineas.Count);
        var supportsSourceDocuments = await SupportsSourceDocumentsAsync(connection, transaction, ct);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = supportsSourceDocuments
            ? $@"
CALL public.sp_registrar_partida_contable(
    @p_company_id,
    @p_journal_id,
    @p_period_id,
    @p_module,
    @p_document_type,
    @p_document_number,
    @p_partida_number,
    @p_partida_date,
    @p_description,
    @p_created_by,
    @p_type_id,
    @p_source_documents,
    {lineasSql}
);"
            : $@"
CALL public.sp_registrar_partida_contable(
    @p_company_id,
    @p_journal_id,
    @p_period_id,
    @p_module,
    @p_document_type,
    @p_document_number,
    @p_partida_number,
    @p_partida_date,
    @p_description,
    @p_created_by,
    @p_type_id,
    {lineasSql}
);";

        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_journal_id", NpgsqlDbType.Bigint, journalId);
        command.Parameters.AddWithValue("p_period_id", NpgsqlDbType.Bigint, periodId);
        command.Parameters.AddWithValue("p_module", NpgsqlDbType.Varchar, "BANCOS");
        command.Parameters.AddWithValue("p_document_type", NpgsqlDbType.Varchar, documentType);
        command.Parameters.AddWithValue("p_document_number", NpgsqlDbType.Varchar, documentNumber);
        command.Parameters.AddWithValue("p_partida_number", NpgsqlDbType.Varchar, partidaNumber);
        command.Parameters.AddWithValue("p_partida_date", NpgsqlDbType.TimestampTz, partidaDate);
        command.Parameters.AddWithValue("p_description", NpgsqlDbType.Varchar, description);
        command.Parameters.AddWithValue("p_created_by", NpgsqlDbType.Varchar, usuario.Trim());
        command.Parameters.AddWithValue("p_type_id", NpgsqlDbType.Bigint, typeId);
        if (supportsSourceDocuments)
        {
            command.Parameters.Add(new NpgsqlParameter("p_source_documents", NpgsqlDbType.Array | NpgsqlDbType.Varchar)
            {
                Value = sourceDocuments.ToArray()
            });
        }

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            command.Parameters.AddWithValue($"acc_{i}", NpgsqlDbType.Bigint, linea.AccountId);
            command.Parameters.Add(new NpgsqlParameter($"cc_{i}", NpgsqlDbType.Bigint)
            {
                Value = linea.CostCenterId.HasValue ? linea.CostCenterId.Value : DBNull.Value
            });
            command.Parameters.AddWithValue($"desc_{i}", NpgsqlDbType.Varchar, linea.Description);
            command.Parameters.AddWithValue($"deb_{i}", NpgsqlDbType.Numeric, linea.Debit);
            command.Parameters.AddWithValue($"cred_{i}", NpgsqlDbType.Numeric, linea.Credit);
            command.Parameters.Add(new NpgsqlParameter($"third_{i}", NpgsqlDbType.Bigint)
            {
                Value = linea.ThirdPartyId.HasValue ? linea.ThirdPartyId.Value : DBNull.Value
            });
            command.Parameters.AddWithValue($"curr_{i}", NpgsqlDbType.Varchar, linea.CurrencyCode);
            command.Parameters.AddWithValue($"rate_{i}", NpgsqlDbType.Numeric, linea.ExchangeRate);
        }

        await command.ExecuteNonQueryAsync(ct);

        return await TryResolvePartidaIdAsync(
            connection,
            transaction,
            companyId,
            documentType,
            documentNumber,
            usuario,
            ct);
    }

    private static async Task<KardexAnulacionInfo> ObtenerDatosKardexAnulacionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long banKardexIdAnulacion,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = @"
SELECT
    k.fecha_movimiento,
    k.referencia,
    k.descripcion
FROM public.ban_kardex k
WHERE k.company_id = @p_company_id
  AND k.ban_kardex_id = @p_ban_kardex_id
LIMIT 1;";

        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_ban_kardex_id", NpgsqlDbType.Bigint, banKardexIdAnulacion);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new InvalidOperationException(
                $"No se pudo obtener el kardex de anulacion {banKardexIdAnulacion}.");
        }

        var fechaMovimiento = TryReadDateOnly(reader, 0)
            ?? throw new InvalidOperationException(
                $"El kardex de anulacion {banKardexIdAnulacion} no tiene fecha de movimiento.");

        return new KardexAnulacionInfo(
            FechaMovimiento: fechaMovimiento,
            Referencia: reader.IsDBNull(1) ? null : reader.GetString(1),
            Descripcion: reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static IReadOnlyList<BanTransaccionListDto> AplicarFiltroFechas(
        IReadOnlyList<BanTransaccionListDto> transacciones,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta)
    {
        if (!fechaDesde.HasValue && !fechaHasta.HasValue)
        {
            return transacciones;
        }

        var filtradas = transacciones
            .Where(t =>
                (!fechaDesde.HasValue || t.FechaMovimiento >= fechaDesde.Value) &&
                (!fechaHasta.HasValue || t.FechaMovimiento <= fechaHasta.Value))
            .ToList();

        return filtradas;
    }

    private async Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesFromProcedureAsync(
        long bancoId,
        long bancoCuentaId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken ct = default)
    {
        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = context.Database.GetDbConnection().ConnectionString;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var cursorName = "sp_ban_kardex_movimientos_cursor";
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "public.sp_ban_kardex_movimientos";

                command.Parameters.AddWithValue("p_id_banco", NpgsqlDbType.Bigint, bancoId);
                command.Parameters.AddWithValue("p_id_cuenta", NpgsqlDbType.Bigint, bancoCuentaId);
                command.Parameters.AddWithValue("p_fecha_inicial_txt", NpgsqlDbType.Text, fechaDesde.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("p_fecha_final_txt", NpgsqlDbType.Text, fechaHasta.ToString("yyyy-MM-dd"));

                var cursorParam = new NpgsqlParameter("p_result", NpgsqlDbType.Refcursor)
                {
                    Direction = ParameterDirection.InputOutput,
                    Value = cursorName
                };
                command.Parameters.Add(cursorParam);

                await command.ExecuteNonQueryAsync(ct);

                if (cursorParam.Value is string value && !string.IsNullOrWhiteSpace(value))
                {
                    cursorName = value;
                }
            }

            var transacciones = new List<BanTransaccionListDto>();
            await using (var fetch = connection.CreateCommand())
            {
                fetch.Transaction = transaction;
                fetch.CommandText = $"FETCH ALL IN \"{cursorName}\";";

                await using var reader = await fetch.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var fechaMovimiento = TryReadDateOnly(reader, 5) ?? DateOnly.MinValue;

                    var dto = new BanTransaccionListDto
                    {
                        BanKardexId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                        BancoCuentaId = bancoCuentaId,
                        BancoNombre = reader.IsDBNull(1) ? null : reader.GetString(1),
                        CuentaNombre = string.Empty,
                        NumeroCuenta = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        MonedaCodigo = reader.IsDBNull(3) ? null : reader.GetString(3),
                        IdTipoTransaccion = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        FechaMovimiento = fechaMovimiento,
                        FechaRegistro = fechaMovimiento == DateOnly.MinValue
                            ? DateTime.MinValue
                            : fechaMovimiento.ToDateTime(TimeOnly.MinValue),
                        Descripcion = reader.FieldCount > 9 && !reader.IsDBNull(9)
                            ? reader.GetString(9)
                            : string.Empty,
                        Referencia = reader.FieldCount > 8 && !reader.IsDBNull(8)
                            ? reader.GetString(8)
                            : null,
                        Monto = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                        SaldoResultante = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                        CreadoPor = string.Empty,
                        CreadoEn = DateTime.MinValue,
                        ActualizadoPor = null,
                        ActualizadoEn = null
                    };

                    transacciones.Add(dto);
                }
            }

            await using (var close = connection.CreateCommand())
            {
                close.Transaction = transaction;
                close.CommandText = $"CLOSE \"{cursorName}\";";
                await close.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return transacciones;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task CompletarCamposDesdeKardexAsync(
        long companyId,
        IReadOnlyList<BanTransaccionListDto> transacciones,
        CancellationToken ct = default)
    {
        if (transacciones.Count == 0)
        {
            return;
        }

        var ids = transacciones
            .Where(t => t.BanKardexId > 0)
            .Select(t => t.BanKardexId)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
SELECT
    k.ban_kardex_id,
    k.descripcion,
    k.referencia,
    UPPER(COALESCE(k.estado_conciliacion, 'NOC')) AS estado_conciliacion
FROM public.ban_kardex k
WHERE k.company_id = @p_company_id
  AND k.ban_kardex_id = ANY(@p_ids);";

            command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.Add(new NpgsqlParameter("p_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
            {
                Value = ids.ToArray()
            });

            var camposPorId = new Dictionary<long, (string? Descripcion, string? Referencia, string? EstadoConciliacion)>();

            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var banKardexId = reader.GetInt64(0);
                    var descripcion = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var referencia = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var estadoConciliacion = reader.IsDBNull(3) ? null : reader.GetString(3);
                    camposPorId[banKardexId] = (descripcion, referencia, estadoConciliacion);
                }
            }

            foreach (var transaccion in transacciones)
            {
                if (!camposPorId.TryGetValue(transaccion.BanKardexId, out var campos))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(transaccion.Descripcion))
                {
                    transaccion.Descripcion = campos.Descripcion ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(transaccion.Referencia))
                {
                    transaccion.Referencia = campos.Referencia;
                }

                transaccion.EstadoConciliacion = campos.EstadoConciliacion;
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DateOnly? TryReadDateOnly(IDataRecord record, int ordinal)
    {
        if (record.IsDBNull(ordinal))
        {
            return null;
        }

        var value = record.GetValue(ordinal);
        return value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.DateTime),
            string text when DateOnly.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private static DateOnly GetFirstDayOfCurrentMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new DateOnly(today.Year, today.Month, 1);
    }

    private static DateOnly GetLastDayOfCurrentMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var lastDay = DateTime.DaysInMonth(today.Year, today.Month);
        return new DateOnly(today.Year, today.Month, lastDay);
    }

    private static bool IsCuentaActiva(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
               || string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildContraDescripcion(
        BanTransaccionContraLineaDto linea,
        IReadOnlyDictionary<long, (string Code, string Name)> cuentasLookup)
    {
        if (!string.IsNullOrWhiteSpace(linea.Descripcion))
        {
            return linea.Descripcion.Trim();
        }

        if (cuentasLookup.TryGetValue(linea.CuentaId, out var cuenta))
        {
            var code = cuenta.Code.Trim();
            var name = cuenta.Name.Trim();
            var label = string.Join(" ", new[] { code, name }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (!string.IsNullOrWhiteSpace(label))
            {
                return $"Contra-cuenta: {label}";
            }
        }

        return "Contra-cuenta";
    }

    private static string BuildLineasSql(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var builder = new StringBuilder();
        builder.Append("ARRAY[");
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"ROW(@acc_{i}, @cc_{i}, @desc_{i}, @deb_{i}, @cred_{i}, @third_{i}, @curr_{i}, @rate_{i})");
        }

        builder.Append("]::public.tipo_linea_partida[]");
        return builder.ToString();
    }

    private sealed record PartidaLinea(
        long AccountId,
        long? CostCenterId,
        string Description,
        decimal Debit,
        decimal Credit,
        long? ThirdPartyId,
        string CurrencyCode,
        decimal ExchangeRate);

    private sealed record PartidaContableOriginal(
        long PartidaId,
        long? JournalId,
        long? PeriodId,
        long? TypeId,
        string DocumentType,
        string? DocumentNumber,
        string? PolizaNumber,
        string? Description,
        List<PartidaContableOriginalLinea> Lineas);

    private sealed record PartidaContableOriginalLinea(
        long AccountId,
        long? CostCenterId,
        string? Description,
        decimal Debit,
        decimal Credit,
        long? ThirdPartyId,
        string? CurrencyCode,
        decimal? ExchangeRate,
        string? SourceDocument);

    private sealed record KardexAnulacionInfo(
        DateOnly FechaMovimiento,
        string? Referencia,
        string? Descripcion);

    private async Task<IReadOnlyList<BanTransaccionListDto>> ExcluirTransaccionesAnuladasAsync(
        IReadOnlyList<BanTransaccionListDto> transacciones,
        long companyId,
        CancellationToken ct)
    {
        var (anuladasOriginales, anulacionIds, _, _) = await ObtenerAnuladasAsync(transacciones, companyId, ct);

        if (anuladasOriginales.Count == 0 && anulacionIds.Count == 0)
        {
            return transacciones;
        }

        return transacciones
            .Where(t => !anuladasOriginales.Contains(t.BanKardexId)
                        && !anulacionIds.Contains(t.BanKardexId))
            .ToList();
    }

    private async Task AsignarEstadoTransaccionesAsync(
        IReadOnlyList<BanTransaccionListDto> transacciones,
        long companyId,
        CancellationToken ct)
    {
        var (anuladasOriginales, anulacionIds, referenciaPorOriginal, referenciaPorAnulacionId)
            = await ObtenerAnuladasAsync(transacciones, companyId, ct);

        foreach (var transaccion in transacciones)
        {
            if (anulacionIds.Contains(transaccion.BanKardexId))
            {
                transaccion.Estado = "ANULACION";
                transaccion.ReferenciaAnulacion = referenciaPorAnulacionId.TryGetValue(transaccion.BanKardexId, out var referencia)
                    ? referencia
                    : transaccion.Referencia;
            }
            else if (anuladasOriginales.Contains(transaccion.BanKardexId))
            {
                transaccion.Estado = "ANULADA";
                transaccion.ReferenciaAnulacion = referenciaPorOriginal.TryGetValue(transaccion.BanKardexId, out var referencia)
                    ? referencia
                    : null;
            }
            else
            {
                transaccion.Estado = "ACTIVA";
                transaccion.ReferenciaAnulacion = null;
            }
        }
    }

    private static void AsignarEstadoActivas(IReadOnlyList<BanTransaccionListDto> transacciones)
    {
        foreach (var transaccion in transacciones)
        {
            transaccion.Estado = "ACTIVA";
            transaccion.ReferenciaAnulacion = null;
        }
    }

    private async Task<(HashSet<long> AnuladasOriginales, HashSet<long> AnulacionIds, Dictionary<long, string> ReferenciaPorOriginal, Dictionary<long, string> ReferenciaPorAnulacionId)> ObtenerAnuladasAsync(
        IReadOnlyList<BanTransaccionListDto> transacciones,
        long companyId,
        CancellationToken ct)
    {
        if (transacciones.Count == 0)
        {
            return (new HashSet<long>(), new HashSet<long>(), new Dictionary<long, string>(), new Dictionary<long, string>());
        }

        var ids = transacciones
            .Select(t => t.BanKardexId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return (new HashSet<long>(), new HashSet<long>(), new Dictionary<long, string>(), new Dictionary<long, string>());
        }

        var referenciasReversa = ids
            .Select(id => $"REV-{id}")
            .ToList();

        var reversasEnLista = await context.ban_kardex
            .AsNoTracking()
            .Where(k => k.company_id == companyId
                        && ids.Contains(k.ban_kardex_id)
                        && k.referencia != null
                        && EF.Functions.ILike(k.referencia, "REV-%"))
            .Select(k => new { k.ban_kardex_id, k.referencia })
            .ToListAsync(ct);

        var reversasOriginales = await context.ban_kardex
            .AsNoTracking()
            .Where(k => k.company_id == companyId
                        && k.referencia != null
                        && referenciasReversa.Contains(k.referencia))
            .Select(k => new { k.ban_kardex_id, k.referencia })
            .ToListAsync(ct);

        var anuladasOriginales = new HashSet<long>();
        var anulacionIds = new HashSet<long>();
        var referenciaPorOriginal = new Dictionary<long, string>();
        var referenciaPorAnulacionId = new Dictionary<long, string>();

        foreach (var item in reversasEnLista)
        {
            anulacionIds.Add(item.ban_kardex_id);
            if (!string.IsNullOrWhiteSpace(item.referencia))
            {
                referenciaPorAnulacionId[item.ban_kardex_id] = item.referencia;
            }
        }

        foreach (var item in reversasOriginales)
        {
            if (!string.IsNullOrWhiteSpace(item.referencia)
                && item.referencia.StartsWith("REV-", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(item.referencia[4..], out var originalId))
            {
                anuladasOriginales.Add(originalId);
                referenciaPorOriginal.TryAdd(originalId, item.referencia);
            }
        }

        foreach (var item in reversasOriginales)
        {
            anulacionIds.Add(item.ban_kardex_id);
            if (!string.IsNullOrWhiteSpace(item.referencia))
            {
                referenciaPorAnulacionId.TryAdd(item.ban_kardex_id, item.referencia);
            }
        }

        return (anuladasOriginales, anulacionIds, referenciaPorOriginal, referenciaPorAnulacionId);
    }

    private long EnsureCompanyId()
    {
        var companyId = currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual.");
        }

        return companyId;
    }
}



