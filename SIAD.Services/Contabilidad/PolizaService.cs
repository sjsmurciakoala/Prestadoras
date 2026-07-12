using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementacion de IPolizaService.
/// Gestiona polizas contables respetando estructura DB multiempresa.
/// </summary>
public sealed class PolizaService : IPolizaService
{
    private const short StatusDraft = 0;
    private const short StatusPosted = 1;
    private const short StatusVoid = 2;

    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompany;

    public PolizaService(SiadDbContext context, ICurrentCompanyService currentCompany)
    {
        _context = context;
        _currentCompany = currentCompany;
    }

    public async Task<long> CrearAsync(
        long companyId,
        long typeId,
        long? periodId,
        long? journalId,
        DateTime polizaDate,
        string module,
        string documentType,
        long? documentId,
        string? documentNumber,
        string description,
        List<PolizaLineaCrearDto> lineas,
        string userId,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(userId);

        if (lineas.Count == 0)
            throw new InvalidOperationException("Debe registrar al menos una linea en la partida.");

        var safeUser = string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
        var normalizedModule = NormalizeRequiredText(module, "Debe indicar el modulo del comprobante.").ToUpperInvariant();
        var normalizedDocumentType = NormalizeRequiredText(documentType, "Debe indicar el tipo de documento.").ToUpperInvariant();
        var normalizedDescription = NormalizeRequiredText(description, "Debe indicar la descripcion del comprobante.");
        var requestedDocumentNumber = NormalizeOptionalText(documentNumber);

        foreach (var (linea, index) in lineas.Select((item, idx) => (item, idx + 1)))
        {
            if (linea.AccountId <= 0)
                throw new InvalidOperationException($"Linea {index}: debe indicar una cuenta contable valida.");

            if (linea.DebitAmount < 0 || linea.CreditAmount < 0)
                throw new InvalidOperationException($"Linea {index}: debito y credito no pueden ser negativos.");

            if ((linea.DebitAmount <= 0 && linea.CreditAmount <= 0) ||
                (linea.DebitAmount > 0 && linea.CreditAmount > 0))
            {
                throw new InvalidOperationException($"Linea {index}: registre debito o credito, pero no ambos.");
            }
        }

        var totalDebit = lineas.Sum(x => x.DebitAmount);
        var totalCredit = lineas.Sum(x => x.CreditAmount);
        if (Math.Abs(totalDebit - totalCredit) >= 0.01m)
            throw new InvalidOperationException("La partida no esta balanceada. Los debitos y creditos deben ser iguales.");

        var periodo = await ResolvePeriodoAsync(companyId, polizaDate, periodId, ct);
        var templateId = await ResolveTemplateIdAsync(companyId, normalizedModule, normalizedDocumentType, ct);
        var polizaDateUtc = ConvertToUtc(polizaDate);
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var diario = await ResolveDiarioAsync(companyId, journalId, ct);
            var tipo = await LockTipoTransaccionAsync(companyId, typeId, ct);
            var sequenceNumber = await GenerarSecuenciaDiarioAsync(companyId, diario, safeUser, ct);
            await LockPolizaNumberScopeAsync(companyId, polizaDate.Date.Year, polizaDate.Date.Month, ct);
            var polizaNumber = await GenerarNumeroPolizaAsync(companyId, polizaDate.Date.Year, polizaDate.Date.Month, ct);
            var resolvedDocumentNumber = IsManualModule(normalizedModule)
                ? await GenerarDocumentoManualAsync(tipo, safeUser, ct)
                : ResolveDocumentNumber(requestedDocumentNumber, lineas, polizaNumber);
            var sourceReference = IsManualModule(normalizedModule)
                ? ResolveSourceReference(lineas)
                : ResolveSourceReference(resolvedDocumentNumber, lineas);

            var poliza = new con_partida_hdr
            {
                company_id = companyId,
                type_id = typeId,
                period_id = periodo.period_id,
                journal_id = diario.journal_id,
                template_id = templateId,
                module = normalizedModule,
                document_type = normalizedDocumentType,
                document_id = documentId is > 0 ? documentId : null,
                document_number = resolvedDocumentNumber,
                poliza_number = polizaNumber,
                sequence_number = sequenceNumber,
                poliza_date = polizaDateUtc,
                description = normalizedDescription,
                status = StatusDraft,
                source_reference = sourceReference,
                created_at = now,
                created_by = safeUser,
                updated_at = now,
                updated_by = safeUser,
                total_debit = totalDebit,
                total_credit = totalCredit
            };

            _context.con_partida_hdrs.Add(poliza);
            await _context.SaveChangesAsync(ct);

            if (!poliza.document_id.HasValue || poliza.document_id <= 0)
            {
                poliza.document_id = poliza.poliza_id;
                poliza.updated_at = DateTime.UtcNow;
                poliza.updated_by = safeUser;
                await _context.SaveChangesAsync(ct);
            }

            foreach (var linea in lineas)
            {
                await AgregarLineaAsync(companyId, poliza.poliza_id, linea, safeUser, ct);
            }

            await transaction.CommitAsync(ct);
            return poliza.poliza_id;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PolizaConLineasDto> ObtenerAsync(long companyId, long polizaId, CancellationToken ct = default)
    {
        var poliza = await _context.con_partida_hdrs
            .AsNoTracking()
            .Include(p => p.lineas)
            .ThenInclude(l => l.account)
            .Include(p => p.lineas)
            .ThenInclude(l => l.cost_center)
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        var totalDebit = poliza.lineas?.Sum(x => x.debit_amount) ?? 0;
        var totalCredit = poliza.lineas?.Sum(x => x.credit_amount) ?? 0;
        var isBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m;

        return new PolizaConLineasDto(
            poliza.poliza_id,
            poliza.company_id,
            poliza.period_id,
            poliza.journal_id,
            poliza.type_id,
            poliza.poliza_number,
            poliza.poliza_date,
            poliza.module,
            poliza.document_type,
            poliza.document_number,
            poliza.description,
            poliza.status,
            totalDebit,
            totalCredit,
            isBalanced,
            poliza.created_at,
            poliza.created_by,
            poliza.updated_at,
            poliza.updated_by,
            poliza.lineas?.Select(l => new PolizaLineaConDetallesDto(
                l.poliza_line_id,
                l.poliza_id,
                l.account_id,
                l.account?.code ?? "",
                l.account?.name ?? "",
                l.cost_center_id,
                l.cost_center?.code,
                l.third_party_id,
                l.debit_amount,
                l.credit_amount,
                l.description,
                l.currency_code
            )).ToList() ?? new()
        );
    }

    public async Task<List<PolizaListaDto>> ListarPorPeriodoAsync(
        long companyId,
        long periodId,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default
    )
    {
        return await _context.con_partida_hdrs
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.period_id == periodId)
            .OrderByDescending(x => x.poliza_date)
            .Skip(skip)
            .Take(take)
            .Select(p => new PolizaListaDto(
                p.poliza_id,
                p.type_id,
                p.poliza_number,
                p.poliza_date,
                p.module,
                p.document_type,
                p.document_number,
                p.description,
                p.status,
                p.lineas!.Sum(l => l.debit_amount),
                p.lineas!.Sum(l => l.credit_amount),
                Math.Abs(p.lineas!.Sum(l => l.debit_amount) - p.lineas!.Sum(l => l.credit_amount)) < 0.01m,
                p.created_at,
                p.created_by
            ))
            .ToListAsync(ct);
    }

    public async Task<List<PolizaListaDto>> ListarPorDiarioAsync(
        long companyId,
        long journalId,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default
    )
    {
        return await _context.con_partida_hdrs
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.journal_id == journalId)
            .OrderByDescending(x => x.poliza_date)
            .Skip(skip)
            .Take(take)
            .Select(p => new PolizaListaDto(
                p.poliza_id,
                p.type_id,
                p.poliza_number,
                p.poliza_date,
                p.module,
                p.document_type,
                p.document_number,
                p.description,
                p.status,
                p.lineas!.Sum(l => l.debit_amount),
                p.lineas!.Sum(l => l.credit_amount),
                Math.Abs(p.lineas!.Sum(l => l.debit_amount) - p.lineas!.Sum(l => l.credit_amount)) < 0.01m,
                p.created_at,
                p.created_by
            ))
            .ToListAsync(ct);
    }

    public async Task ActualizarAsync(
        long companyId,
        long polizaId,
        PolizaActualizarDto dto,
        string userId,
        CancellationToken ct = default
    )
    {
        var poliza = await _context.con_partida_hdrs
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        if (poliza.status != StatusDraft)
            throw new InvalidOperationException("Solo se pueden actualizar polizas en estado DRAFT");

        var periodo = await ResolvePeriodoAsync(companyId, dto.PolizaDate, poliza.period_id, ct);
        poliza.poliza_date = ConvertToUtc(dto.PolizaDate);
        poliza.period_id = periodo.period_id;
        poliza.description = NormalizeRequiredText(dto.Description, "Debe indicar la descripcion del comprobante.");
        poliza.updated_at = DateTime.UtcNow;
        poliza.updated_by = string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();

        await _context.SaveChangesAsync(ct);
    }

    public async Task AgregarLineaAsync(
        long companyId,
        long polizaId,
        PolizaLineaCrearDto linea,
        string userId,
        CancellationToken ct = default
    )
    {
        var poliza = await _context.con_partida_hdrs
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        if (poliza.status != StatusDraft)
            throw new InvalidOperationException("Solo se pueden agregar lineas a polizas en estado DRAFT");

        if (linea.AccountId <= 0)
            throw new InvalidOperationException("Debe indicar una cuenta contable valida.");

        if (linea.DebitAmount < 0 || linea.CreditAmount < 0)
            throw new InvalidOperationException("Debito y credito no pueden ser negativos.");

        if ((linea.DebitAmount <= 0 && linea.CreditAmount <= 0) ||
            (linea.DebitAmount > 0 && linea.CreditAmount > 0))
        {
            throw new InvalidOperationException("Cada linea debe registrar debito o credito, pero no ambos.");
        }

        var cuenta = await _context.con_plan_cuentas
            .Where(x => x.account_id == linea.AccountId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Cuenta {linea.AccountId} no encontrada");

        if (!cuenta.allows_posting)
            throw new InvalidOperationException($"La cuenta {cuenta.code} no permite movimiento.");

        var proximoNumero = (short)((_context.con_partida_dtls
            .Where(x => x.poliza_id == polizaId)
            .Max(x => (short?)x.line_number) ?? 0) + 1);

        var polizaLinea = new con_partida_dtl
        {
            company_id = companyId,
            poliza_id = polizaId,
            line_number = proximoNumero,
            account_id = linea.AccountId,
            cost_center_id = linea.CostCenterId,
            third_party_id = linea.ThirdPartyId,
            debit_amount = linea.DebitAmount,
            credit_amount = linea.CreditAmount,
            description = NormalizeOptionalText(linea.Description),
            currency_code = NormalizeOptionalText(linea.CurrencyCode) ?? "HNL",
            exchange_rate = linea.ExchangeRate ?? 1m,
            source_document = NormalizeOptionalText(linea.Reference)
        };

        _context.con_partida_dtls.Add(polizaLinea);
        await _context.SaveChangesAsync(ct);
        await ActualizarTotalesPolizaAsync(poliza, string.IsNullOrWhiteSpace(userId) ? null : userId.Trim(), ct);
    }

    public async Task EliminarLineaAsync(long companyId, long lineaId, CancellationToken ct = default)
    {
        var linea = await _context.con_partida_dtls
            .Include(x => x.poliza)
            .Where(x => x.poliza_line_id == lineaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Linea {lineaId} no encontrada");

        if (linea.poliza?.status != StatusDraft)
            throw new InvalidOperationException("Solo se pueden eliminar lineas de polizas en estado DRAFT");

        _context.con_partida_dtls.Remove(linea);
        await _context.SaveChangesAsync(ct);
        await ActualizarTotalesPolizaAsync(linea.poliza!, null, ct);
    }

    public async Task EliminarAsync(long companyId, long polizaId, CancellationToken ct = default)
    {
        var poliza = await _context.con_partida_hdrs
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        if (poliza.status != StatusDraft)
            throw new InvalidOperationException("Solo se pueden eliminar polizas en estado DRAFT");

        _context.con_partida_hdrs.Remove(poliza);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(bool balanceado, decimal debitTotal, decimal creditTotal)> ValidarBalanceAsync(
        long companyId,
        long polizaId,
        CancellationToken ct = default
    )
    {
        var poliza = await _context.con_partida_hdrs
            .AsNoTracking()
            .Include(p => p.lineas)
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        var totalDebit = poliza.lineas?.Sum(x => x.debit_amount) ?? 0;
        var totalCredit = poliza.lineas?.Sum(x => x.credit_amount) ?? 0;

        return (Math.Abs(totalDebit - totalCredit) < 0.01m, totalDebit, totalCredit);
    }

    public async Task RegistrarAsync(long companyId, long polizaId, string userId, CancellationToken ct = default)
    {
        var exists = await _context.con_partida_hdrs
            .AsNoTracking()
            .AnyAsync(x => x.poliza_id == polizaId && x.company_id == companyId, ct);

        if (!exists)
            throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        var safeUser = string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT public.sp_con_postear_poliza({companyId}, {polizaId}, {safeUser});",
            ct);
    }

    public async Task RevertirAsync(long companyId, long polizaId, string userId, CancellationToken ct = default)
    {
        var exists = await _context.con_partida_hdrs
            .AsNoTracking()
            .AnyAsync(x => x.poliza_id == polizaId && x.company_id == companyId, ct);

        if (!exists)
            throw new InvalidOperationException($"Poliza {polizaId} no encontrada");

        var safeUser = string.IsNullOrWhiteSpace(userId) ? "system" : userId.Trim();
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT public.sp_con_revertir_poliza({companyId}, {polizaId}, {safeUser});",
            ct);
    }

    private async Task<con_periodo_contable> ResolvePeriodoAsync(
        long companyId,
        DateTime polizaDate,
        long? requestedPeriodId,
        CancellationToken ct)
    {
        var targetDate = polizaDate.Date;

        if (requestedPeriodId.HasValue)
        {
            var periodo = await _context.con_periodo_contables
                .FirstOrDefaultAsync(x => x.company_id == companyId && x.period_id == requestedPeriodId.Value, ct)
                ?? throw new InvalidOperationException($"Periodo {requestedPeriodId.Value} no encontrado para empresa {companyId}.");

            if (!EstadoPeriodoHelper.IsOpen(periodo.status_id))
                throw new InvalidOperationException($"El periodo {periodo.code} no esta abierto.");

            if (!ContainsDate(periodo, targetDate))
            {
                throw new InvalidOperationException(
                    $"La fecha {targetDate:dd/MM/yyyy} debe estar dentro del periodo contable abierto {periodo.code} ({GetPeriodoRango(periodo)}).");
            }

            return periodo;
        }

        var periodosAbiertos = await _context.con_periodo_contables
            .Where(x => x.company_id == companyId && x.status_id == EstadoPeriodoHelper.AbiertoId)
            .OrderBy(x => x.start_date)
            .ToListAsync(ct);

        if (periodosAbiertos.Count == 0)
            throw new InvalidOperationException("No existe un periodo contable abierto para registrar la partida.");

        if (periodosAbiertos.Count > 1)
        {
            throw new InvalidOperationException(
                $"La empresa {companyId} tiene multiples periodos abiertos. Revise el calendario contable antes de continuar.");
        }

        var periodoActivo = periodosAbiertos[0];
        if (!ContainsDate(periodoActivo, targetDate))
        {
            throw new InvalidOperationException(
                $"La fecha {targetDate:dd/MM/yyyy} debe estar dentro del periodo contable abierto {periodoActivo.code} ({GetPeriodoRango(periodoActivo)}).");
        }

        return periodoActivo;
    }

    private async Task<con_diario> ResolveDiarioAsync(long companyId, long? requestedJournalId, CancellationToken ct)
    {
        if (requestedJournalId.HasValue)
        {
            var diario = await _context.con_diarios
                .FirstOrDefaultAsync(x => x.company_id == companyId && x.journal_id == requestedJournalId.Value, ct)
                ?? throw new InvalidOperationException($"Diario {requestedJournalId.Value} no encontrado para empresa {companyId}.");

            if (!diario.is_active)
                throw new InvalidOperationException($"El diario {diario.code} no esta activo.");

            if (!diario.allows_manual)
                throw new InvalidOperationException($"El diario {diario.code} no permite comprobantes manuales.");

            return diario;
        }

        var diarios = await _context.con_diarios
            .Where(x => x.company_id == companyId && x.is_active && x.allows_manual)
            .OrderBy(x => x.code)
            .ToListAsync(ct);

        if (diarios.Count == 0)
            throw new InvalidOperationException("No existe un diario manual activo configurado para comprobantes manuales.");

        var diariosPorDefecto = diarios.Where(x => x.is_default_manual).ToList();
        if (diariosPorDefecto.Count == 0)
            throw new InvalidOperationException("No existe un diario manual por defecto activo. Configure uno en el catálogo de diarios.");

        if (diariosPorDefecto.Count > 1)
            throw new InvalidOperationException("Existe más de un diario manual por defecto activo. Revise el catálogo de diarios.");

        return diariosPorDefecto[0];
    }

    private async Task<con_tipo_transaccion> LockTipoTransaccionAsync(long companyId, long typeId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT 1
               FROM public.con_tipo_transaccion
               WHERE company_id = {companyId} AND type_id = {typeId}
               FOR UPDATE;",
            ct);

        return await _context.con_tipo_transacciones
            .FirstOrDefaultAsync(x => x.company_id == companyId && x.type_id == typeId, ct)
            ?? throw new InvalidOperationException($"Tipo de transaccion {typeId} no encontrado para empresa {companyId}.");
    }

    private async Task LockPolizaNumberScopeAsync(long companyId, int year, int month, CancellationToken ct)
    {
        var periodKey = checked(year * 100 + month);
        var advisoryKey = unchecked((companyId << 32) ^ (uint)periodKey);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT pg_advisory_xact_lock({advisoryKey});",
            ct);
    }

    private async Task<string> GenerarDocumentoManualAsync(con_tipo_transaccion tipo, string userId, CancellationToken ct)
    {
        var ultimoDocumentoRegistrado = await ObtenerUltimoDocumentoManualRegistradoAsync(tipo.company_id, tipo.type_id, ct);
        var ultimoDocumento = Math.Max(tipo.last_document_number, ultimoDocumentoRegistrado);
        var siguiente = Math.Max(tipo.document_sequence_start, ultimoDocumento + 1);
        tipo.last_document_number = siguiente;
        tipo.updated_at = DateTime.UtcNow;
        tipo.updated_by = userId;
        await _context.SaveChangesAsync(ct);
        return siguiente.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<long> ObtenerUltimoDocumentoManualRegistradoAsync(long companyId, long typeId, CancellationToken ct)
    {
        var documentos = await _context.con_partida_hdrs
            .AsNoTracking()
            .Where(x => x.company_id == companyId &&
                        x.type_id == typeId &&
                        x.module == "MANUAL" &&
                        x.document_number != null)
            .Select(x => x.document_number!)
            .ToListAsync(ct);

        return documentos
            .Select(TryParseDocumentNumber)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(0L)
            .Max();
    }

    private async Task<long?> ResolveTemplateIdAsync(long companyId, string module, string documentType, CancellationToken ct)
    {
        return await _context.con_plantilla_partida_hdrs
            .AsNoTracking()
            .Where(x => x.company_id == companyId &&
                        x.is_active &&
                        x.module == module &&
                        x.document_type == documentType)
            .OrderBy(x => x.template_id)
            .Select(x => (long?)x.template_id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<long> GenerarSecuenciaDiarioAsync(
        long companyId,
        con_diario diario,
        string userId,
        CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT 1
               FROM public.con_diario
               WHERE company_id = {companyId} AND journal_id = {diario.journal_id}
               FOR UPDATE;",
            ct);

        await _context.Entry(diario).ReloadAsync(ct);

        var maxRegistrado = await _context.con_partida_hdrs
            .Where(x => x.company_id == companyId && x.journal_id == diario.journal_id)
            .MaxAsync(x => (long?)x.sequence_number, ct) ?? 0L;

        var siguiente = Math.Max(diario.last_sequence, maxRegistrado) + 1;
        diario.last_sequence = siguiente;
        diario.updated_at = DateTime.UtcNow;
        diario.updated_by = userId;
        await _context.SaveChangesAsync(ct);

        return siguiente;
    }

    private async Task<string> GenerarNumeroPolizaAsync(long companyId, int year, int month, CancellationToken ct = default)
    {
        var prefix = $"{companyId}-{year:D4}-{month:D2}-";
        var existingNumbers = await _context.con_partida_hdrs
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.poliza_number.StartsWith(prefix))
            .Select(x => x.poliza_number)
            .ToListAsync(ct);

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

        return $"{prefix}{(lastSequence + 1):D6}";
    }

    private async Task ActualizarTotalesPolizaAsync(con_partida_hdr poliza, string? updatedBy, CancellationToken ct)
    {
        var totalDebit = await _context.con_partida_dtls
            .Where(x => x.company_id == poliza.company_id && x.poliza_id == poliza.poliza_id)
            .SumAsync(x => (decimal?)x.debit_amount, ct) ?? 0m;

        var totalCredit = await _context.con_partida_dtls
            .Where(x => x.company_id == poliza.company_id && x.poliza_id == poliza.poliza_id)
            .SumAsync(x => (decimal?)x.credit_amount, ct) ?? 0m;

        poliza.total_debit = totalDebit;
        poliza.total_credit = totalCredit;
        poliza.updated_at = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(updatedBy))
        {
            poliza.updated_by = updatedBy;
        }

        await _context.SaveChangesAsync(ct);
    }

    private static string ResolveDocumentNumber(
        string? requestedDocumentNumber,
        IEnumerable<PolizaLineaCrearDto> lineas,
        string polizaNumber)
    {
        return NormalizeOptionalText(requestedDocumentNumber)
            ?? lineas.Select(x => NormalizeOptionalText(x.Reference)).FirstOrDefault(x => x is not null)
            ?? polizaNumber;
    }

    private static string? ResolveSourceReference(string? documentNumber, IEnumerable<PolizaLineaCrearDto> lineas)
    {
        return NormalizeOptionalText(documentNumber)
            ?? lineas.Select(x => NormalizeOptionalText(x.Reference)).FirstOrDefault(x => x is not null);
    }

    private static string? ResolveSourceReference(IEnumerable<PolizaLineaCrearDto> lineas)
    {
        return lineas.Select(x => NormalizeOptionalText(x.Reference)).FirstOrDefault(x => x is not null);
    }

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        var normalized = NormalizeOptionalText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException(errorMessage);

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ContainsDate(con_periodo_contable periodo, DateTime polizaDate)
    {
        var date = polizaDate.Date;
        return date >= NormalizeBusinessDate(periodo.start_date) && date <= NormalizeBusinessDate(periodo.end_date);
    }

    private static string GetPeriodoRango(con_periodo_contable periodo)
    {
        return $"{NormalizeBusinessDate(periodo.start_date):dd/MM/yyyy} - {NormalizeBusinessDate(periodo.end_date):dd/MM/yyyy}";
    }

    private static DateTime NormalizeBusinessDate(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return value.Date;
        }

        return TimeZoneInfo.ConvertTime(value, BusinessTimeZone).Date;
    }

    private static long? TryParseDocumentNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var timezoneId in new[] { "Central America Standard Time", "America/Tegucigalpa" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static bool IsManualModule(string module)
    {
        return string.Equals(module, "MANUAL", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetManualJournalPriority(string? code)
    {
        var normalized = NormalizeOptionalText(code)?.ToUpperInvariant();
        return normalized switch
        {
            "GEN" => 0,
            "DIARIO" => 1,
            "MANUAL" => 2,
            _ => 10
        };
    }

    private static DateTime ConvertToUtc(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return dateTime.ToUniversalTime();
    }
}
