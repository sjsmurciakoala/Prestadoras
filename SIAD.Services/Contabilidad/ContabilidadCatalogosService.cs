using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using ClosedXML.Excel;
namespace SIAD.Services.Contabilidad;

public class ContabilidadCatalogosService : IContabilidadCatalogosService
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public ContabilidadCatalogosService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual de la sesión.");
        }

        return companyId;
    }

    private static string NormalizeAccountCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var ch in raw.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private const string DefaultFormatoCuentas = "###-###-##";

    private async Task<string> GetFormatoCuentasAsync(long companyId, CancellationToken cancellationToken)
    {
        var formato = await _context.con_configuracion_sistemas
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .Select(c => c.formato_cuentas)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(formato))
        {
            return DefaultFormatoCuentas;
        }

        return formato;
    }

    private static List<int> ParseFormatoNiveles(string? formato)
    {
        if (string.IsNullOrWhiteSpace(formato))
        {
            return new List<int>();
        }

        var grupos = new List<int>();
        var current = 0;
        foreach (var ch in formato)
        {
            if (ch == '#' || ch == 'X' || ch == 'x')
            {
                current++;
            }
            else
            {
                if (current > 0)
                {
                    grupos.Add(current);
                    current = 0;
                }
            }
        }

        if (current > 0)
        {
            grupos.Add(current);
        }

        return grupos;
    }

    private static IReadOnlyList<int> GetFormatoNiveles(string? formato)
    {
        var niveles = ParseFormatoNiveles(formato);
        if (niveles.Count > 0)
        {
            return niveles;
        }

        return ParseFormatoNiveles(DefaultFormatoCuentas);
    }

    private static IReadOnlyList<string> BuildPrefixes(string normalized, IReadOnlyList<int> niveles)
    {
        var prefixes = new List<string>();
        var total = 0;
        foreach (var len in niveles)
        {
            total += len;
            if (normalized.Length < total)
            {
                break;
            }
            prefixes.Add(normalized.Substring(0, total));
        }
        return prefixes;
    }

    private static IQueryable<con_plan_cuenta> ApplySort(IQueryable<con_plan_cuenta> query, string? sortField, bool sortDesc)
    {
        return sortField switch
        {
            nameof(PlanCuentaCatalogoItemDto.Code) => sortDesc
                ? query.OrderByDescending(c => c.code)
                : query.OrderBy(c => c.code),
            nameof(PlanCuentaCatalogoItemDto.Name) => sortDesc
                ? query.OrderByDescending(c => c.name)
                : query.OrderBy(c => c.name),
            nameof(PlanCuentaCatalogoItemDto.Level) => sortDesc
                ? query.OrderByDescending(c => c.level)
                : query.OrderBy(c => c.level),
            _ => query.OrderBy(c => c.code)
        };
    }

    public async Task<IReadOnlyList<PlanCuentaDto>> GetPlanCuentasAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .OrderBy(c => c.code)
            .Select(c => new PlanCuentaDto(
                c.account_id,
                c.parent_account_id,
                c.code,
                c.name,
                c.account_type,
                c.category,
                c.level,
                c.allows_posting,
                c.allows_budget,
                c.allows_third,
                c.is_tax_base,
                c.allows_cost_center,
                c.allows_multi_currency,
                c.adjustment_account_id,
                c.correction_account_id,
                c.status,
                c.description,
                c.currency_code))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SavePlanCuentaAsync(PlanCuentaUpsertDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("El código de la cuenta es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("El nombre de la cuenta es obligatorio.", nameof(request));
        }

        var companyId = EnsureCompanyId();
        var isNew = !request.AccountId.HasValue;
        var normalizedCode = NormalizeAccountCode(request.Code).ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new ArgumentException("El cÐ˜digo de la cuenta es obligatorio.", nameof(request));
        }

        if (normalizedCode.Length > 30)
        {
            throw new ArgumentException("El c?digo de la cuenta supera 30 caracteres.", nameof(request));
        }

        if (isNew)
        {
            var exists = await _context.con_plan_cuentas
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.code == normalizedCode, cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException("Ya existe una cuenta con este codigo.");
            }
        }
        con_plan_cuenta entity;
        if (!isNew)
        {
            var accountId = request.AccountId
                ?? throw new InvalidOperationException("La cuenta contable no existe.");

            entity = await _context.con_plan_cuentas
                         .FirstOrDefaultAsync(c => c.account_id == accountId, cancellationToken)
                     ?? throw new InvalidOperationException("La cuenta contable no existe.");
        }
        else
        {
            entity = new con_plan_cuenta
            {
                company_id = companyId,
                created_at = DateTime.UtcNow,
                created_by = request.User
            };
        }

        var parentChanged = request.ParentAccountId.HasValue &&
                            (isNew || entity.parent_account_id != request.ParentAccountId.Value);

        if (request.ParentAccountId.HasValue && parentChanged)
        {
            var parent = await _context.con_plan_cuentas
                .AsNoTracking()
                .Where(c => c.account_id == request.ParentAccountId.Value)
                .Select(c => new { c.account_id, c.level, c.code })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null)
            {
                throw new InvalidOperationException("La cuenta padre indicada no existe.");
            }

            var parentCode = NormalizeAccountCode(parent.code).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(parentCode))
            {
                throw new InvalidOperationException("La cuenta padre indicada no tiene codigo valido.");
            }

            if (!normalizedCode.StartsWith(parentCode, StringComparison.OrdinalIgnoreCase) ||
                normalizedCode.Length <= parentCode.Length)
            {
                throw new InvalidOperationException("El codigo no es valido para la cuenta padre indicada.");
            }

            entity.parent_account_id = parent.account_id;
            entity.level = (short)(parent.level + 1);
        }
        else if (isNew)
        {
            var inferredParent = await FindParentByPrefixAsync(normalizedCode, companyId, cancellationToken);
            if (inferredParent is not null)
            {
                entity.parent_account_id = inferredParent.Value.AccountId;
                entity.level = (short)(inferredParent.Value.Level + 1);
            }
            else
            {
                entity.parent_account_id = null;
                entity.level = 1;
            }
        }

        if (!isNew && !string.Equals(entity.code, normalizedCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No se puede modificar el codigo de una cuenta existente.");
        }

        entity.code = normalizedCode;
        entity.name = request.Name.Trim();
        entity.description = request.Description?.Trim();
        entity.account_type = request.AccountType.Trim().ToUpperInvariant();
        entity.category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.allows_posting = request.AllowsPosting;
        entity.allows_budget = request.AllowsBudget;
        entity.allows_third = request.AllowsThird;
        entity.is_tax_base = request.IsTaxBase;
        entity.allows_cost_center = request.AllowsCostCenter;
        entity.allows_multi_currency = request.AllowsMultiCurrency;
        entity.currency_code = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? null
            : request.CurrencyCode.Trim().ToUpperInvariant();
        entity.status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status.Trim().ToUpperInvariant();
        entity.adjustment_account_id = request.AdjustmentAccountId;
        entity.correction_account_id = request.CorrectionAccountId;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        if (isNew)
        {
            await _context.con_plan_cuentas.AddAsync(entity, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return entity.account_id;
    }

    public async Task<PagedResult<PlanCuentaCatalogoItemDto>> GetPlanCuentasPagedAsync(
        PlanCuentaCatalogoFilterDto filter,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            take = 50;
        }

        if (take > 500)
        {
            take = 500;
        }

        if (skip < 0)
        {
            skip = 0;
        }

        var companyId = EnsureCompanyId();
        var query = _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId);

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var term = filter.Term.Trim();
            var termUpper = term.ToUpperInvariant();
            var normalized = NormalizeAccountCode(termUpper);

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                query = query.Where(c => c.code.Contains(normalized) || c.name.ToUpper().Contains(termUpper));
            }
            else
            {
                query = query.Where(c => c.name.ToUpper().Contains(termUpper));
            }
        }

        query = ApplySort(query, sortField, sortDesc);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(c => new PlanCuentaCatalogoItemDto(
                c.account_id,
                c.code,
                c.name,
                c.level))
            .ToListAsync(cancellationToken);

        return new PagedResult<PlanCuentaCatalogoItemDto>(items, total);
    }

    public async Task<PlanCuentaCatalogoLookupDto> BuscarPlanCuentaAsync(string cuenta, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        var normalized = NormalizeAccountCode(cuenta);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new PlanCuentaCatalogoLookupDto(null, null, null, null, null);
        }

        var formato = await GetFormatoCuentasAsync(companyId, cancellationToken);
        var niveles = GetFormatoNiveles(formato);
        var prefixes = BuildPrefixes(normalized, niveles);

        if (prefixes.Count == 0)
        {
            return new PlanCuentaCatalogoLookupDto(null, null, null, null, null);
        }

        var nombres = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && prefixes.Contains(c.code))
            .Select(c => new { c.code, c.name })
            .ToListAsync(cancellationToken);

        var nombresPorCodigo = nombres.ToDictionary(x => x.code, x => x.name);

        string? grupo = null;
        string? subgrupo = null;
        string? mayor = null;
        string? subcuenta = null;
        string? detalle = null;

        if (prefixes.Count > 0 && nombresPorCodigo.TryGetValue(prefixes[0], out var value))
        {
            grupo = value;
        }

        if (prefixes.Count > 1 && nombresPorCodigo.TryGetValue(prefixes[1], out value))
        {
            subgrupo = value;
        }

        if (prefixes.Count > 2 && nombresPorCodigo.TryGetValue(prefixes[2], out value))
        {
            mayor = value;
        }

        if (prefixes.Count > 3 && nombresPorCodigo.TryGetValue(prefixes[3], out value))
        {
            subcuenta = value;
        }

        if (prefixes.Count > 4 && nombresPorCodigo.TryGetValue(prefixes[4], out value))
        {
            detalle = value;
        }

        return new PlanCuentaCatalogoLookupDto(grupo, subgrupo, mayor, subcuenta, detalle);
    }

    private async Task<(long AccountId, short Level)?> FindParentByPrefixAsync(string normalizedCode, long companyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        var candidates = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .Select(c => new { c.account_id, c.code, c.level })
            .ToListAsync(cancellationToken);

        var best = candidates
            .Select(c => new
            {
                c.account_id,
                Code = NormalizeAccountCode(c.code),
                c.level
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Code)
                        && c.Code.Length < normalizedCode.Length
                        && normalizedCode.StartsWith(c.Code, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Code.Length)
            .FirstOrDefault();

        if (best is null)
        {
            return null;
        }

        return (best.account_id, best.level);
    }

    public async Task<IReadOnlyList<CentroCostoDto>> GetCentrosCostoAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_centro_costos
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .OrderBy(c => c.code)
            .Select(c => new CentroCostoDto(
                c.cost_center_id,
                c.code,
                c.name,
                c.description,
                c.status,
                c.allows_movement,
                c.is_periodic,
                c.start_date,
                c.end_date,
                c.legacy_status,
                c.legacy_type_trans,
                c.legacy_parent_code,
                c.legacy_key_cost,
                c.legacy_notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TipoPartidaDto>> GetTiposPartidaAsync(CancellationToken cancellationToken = default)
    {
        return await _context.cnt_tipopartida
            .AsNoTracking()
            .OrderBy(t => t.cod_tipopartida)
            .Select(t => new TipoPartidaDto(t.cod_tipopartida, t.nombre))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SaveCentroCostoAsync(CentroCostoUpsertDto request,
        CancellationToken cancellationToken = default)
    {
        static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("El código del centro de costo es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("El nombre del centro de costo es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("La descripcion del centro de costo es obligatoria.", nameof(request));
        }

        var rawCode = request.Code.Trim();
        if (rawCode.Length > 30)
        {
            throw new ArgumentException("El codigo del centro de costo supera 30 caracteres.", nameof(request));
        }

        var rawName = request.Name.Trim();
        if (rawName.Length > 150)
        {
            throw new ArgumentException("El nombre del centro de costo supera 150 caracteres.", nameof(request));
        }

        if (request.Description.Trim().Length > 300)
        {
            throw new ArgumentException("La descripcion supera 300 caracteres.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.LegacyParentCode) && request.LegacyParentCode.Trim().Length > 24)
        {
            throw new ArgumentException("El codigo de padre supera 24 caracteres.", nameof(request));
        }

        if (request.LegacyTypeTrans.HasValue &&
            (request.LegacyTypeTrans.Value < 0 || request.LegacyTypeTrans.Value > 5))
        {
            throw new ArgumentException("El tipo de transaccion debe estar entre 0 y 5.", nameof(request));
        }

        var companyId = EnsureCompanyId();
        var normalizedCode = rawCode.ToUpperInvariant();

        if (request.CostCenterId.HasValue)
        {
            var exists = await _context.con_centro_costos
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId
                              && c.code == normalizedCode
                              && c.cost_center_id != request.CostCenterId.Value, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Ya existe un centro de costo con este codigo.");
            }
        }
        else
        {
            var exists = await _context.con_centro_costos
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.code == normalizedCode, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Ya existe un centro de costo con este codigo.");
            }
        }

        var startUtc = request.StartDate.HasValue ? NormalizeUtc(request.StartDate.Value) : (DateTime?)null;
        var endUtc = request.EndDate.HasValue ? NormalizeUtc(request.EndDate.Value) : (DateTime?)null;

        if (request.IsPeriodic == true)
        {
            if (!startUtc.HasValue || !endUtc.HasValue)
            {
                throw new ArgumentException("Las fechas de inicio y fin son obligatorias cuando el centro es periodico.",
                    nameof(request));
            }
        }
        else if (request.IsPeriodic == false)
        {
            startUtc = null;
            endUtc = null;
        }

        if (startUtc.HasValue && endUtc.HasValue && startUtc.Value > endUtc.Value)
        {
            throw new ArgumentException("La fecha de inicio no puede ser mayor a la fecha fin.", nameof(request));
        }

        con_centro_costo entity;
        if (request.CostCenterId.HasValue)
        {
            entity = await _context.con_centro_costos
                         .FirstOrDefaultAsync(c => c.cost_center_id == request.CostCenterId.Value, cancellationToken)
                     ?? throw new InvalidOperationException("El centro de costo no existe.");
        }
        else
        {
            entity = new con_centro_costo
            {
                company_id = companyId,
                created_at = DateTime.UtcNow,
                created_by = request.User
            };
            await _context.con_centro_costos.AddAsync(entity, cancellationToken);
        }

        entity.code = normalizedCode;
        entity.name = rawName;
        entity.description = request.Description.Trim();

        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? "ACTIVE"
            : request.Status.Trim().ToUpperInvariant();
        entity.status = normalizedStatus;
        if (request.LegacyStatus.HasValue)
        {
            entity.legacy_status = request.LegacyStatus.Value;
        }
        else if (request.CostCenterId is null || !string.IsNullOrWhiteSpace(request.Status))
        {
            entity.legacy_status = string.Equals(normalizedStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);
        }

        if (request.AllowsMovement.HasValue)
        {
            entity.allows_movement = request.AllowsMovement.Value;
        }
        else if (request.CostCenterId is null)
        {
            entity.allows_movement = false;
        }

        if (request.IsPeriodic.HasValue)
        {
            entity.is_periodic = request.IsPeriodic.Value;
        }
        else if (request.CostCenterId is null)
        {
            entity.is_periodic = false;
        }

        entity.start_date = startUtc;
        entity.end_date = endUtc;

        if (request.LegacyTypeTrans.HasValue)
        {
            entity.legacy_type_trans = request.LegacyTypeTrans.Value;
        }
        else if (request.CostCenterId is null)
        {
            entity.legacy_type_trans = 0;
        }

        if (request.LegacyParentCode is not null)
        {
            entity.legacy_parent_code = string.IsNullOrWhiteSpace(request.LegacyParentCode)
                ? null
                : request.LegacyParentCode.Trim();
        }
        else if (request.CostCenterId is null)
        {
            entity.legacy_parent_code = null;
        }

        if (request.LegacyKeyCost.HasValue || request.CostCenterId is null)
        {
            entity.legacy_key_cost = request.LegacyKeyCost;
        }

        if (request.LegacyNotes is not null)
        {
            entity.legacy_notes = string.IsNullOrWhiteSpace(request.LegacyNotes)
                ? null
                : request.LegacyNotes.Trim();
        }
        else if (request.CostCenterId is null)
        {
            entity.legacy_notes = null;
        }

        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        await _context.SaveChangesAsync(cancellationToken);
        return entity.cost_center_id;
    }

    public async Task<bool> DeleteCentroCostoAsync(long costCenterId, CancellationToken cancellationToken = default)
    {
        if (costCenterId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costCenterId), "El centro de costo no es valido.");
        }

        var companyId = EnsureCompanyId();
        var entity = await _context.con_centro_costos
            .FirstOrDefaultAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var bloqueos = new List<string>();

        if (await _context.con_regla_integracions
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken))
        {
            bloqueos.Add("reglas de integracion");
        }

        if (await _context.con_plantilla_partida_dtls
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken))
        {
            bloqueos.Add("plantillas de poliza");
        }

        if (await _context.con_partida_dtls
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken))
        {
            bloqueos.Add("polizas registradas");
        }

        if (await _context.con_apertura_saldos
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken))
        {
            bloqueos.Add("aperturas de saldo");
        }

        if (await _context.con_balance_mensuales
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken))
        {
            bloqueos.Add("balances mensuales");
        }

        if (await _context.con_apertura_centro_costos
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.cost_center_id == costCenterId, cancellationToken))
        {
            bloqueos.Add("aperturas de centro de costo");
        }

        if (bloqueos.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se puede eliminar el centro de costo porque tiene {string.Join(", ", bloqueos)}.");
        }

        _context.con_centro_costos.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DiarioDto>> GetDiariosAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_diarios
            .AsNoTracking()
            .Where(d => d.company_id == companyId)
            .OrderBy(d => d.code)
            .Select(d => new DiarioDto(
                d.journal_id,
                d.code,
                d.name,
                d.description,
                d.sequence_prefix,
                d.last_sequence,
                d.is_active,
                d.allows_manual,
                d.is_default_manual))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SaveDiarioAsync(DiarioUpsertDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("El código del diario es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("El nombre del diario es obligatorio.", nameof(request));
        }

        var companyId = EnsureCompanyId();
        var otherJournals = await _context.con_diarios
            .Where(d => d.company_id == companyId && (!request.JournalId.HasValue || d.journal_id != request.JournalId.Value))
            .ToListAsync(cancellationToken);

        con_diario entity;
        if (request.JournalId.HasValue)
        {
            entity = await _context.con_diarios
                         .FirstOrDefaultAsync(d => d.journal_id == request.JournalId.Value, cancellationToken)
                     ?? throw new InvalidOperationException("El diario no existe.");
        }
        else
        {
            entity = new con_diario
            {
                company_id = companyId,
                created_at = DateTime.UtcNow,
                created_by = request.User
            };
            await _context.con_diarios.AddAsync(entity, cancellationToken);
        }

        if (request.IsDefaultManual && (!request.IsActive || !request.AllowsManual))
        {
            throw new InvalidOperationException("El diario manual por defecto debe estar activo y permitir captura manual.");
        }

        entity.code = request.Code.Trim().ToUpperInvariant();
        entity.name = request.Name.Trim();
        entity.description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.sequence_prefix = string.IsNullOrWhiteSpace(request.SequencePrefix)
            ? null
            : request.SequencePrefix.Trim().ToUpperInvariant();
        entity.is_active = request.IsActive;
        entity.allows_manual = request.AllowsManual;
        entity.is_default_manual = request.IsDefaultManual;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        if (request.IsDefaultManual)
        {
            foreach (var other in otherJournals.Where(d => d.is_default_manual))
            {
                other.is_default_manual = false;
                other.updated_at = entity.updated_at;
                other.updated_by = request.User;
            }
        }

        var activeManualCount = otherJournals.Count(d => d.is_active && d.allows_manual)
            + (entity.is_active && entity.allows_manual ? 1 : 0);
        var defaultManualCount = otherJournals.Count(d => d.is_active && d.allows_manual && d.is_default_manual)
            + (entity.is_active && entity.allows_manual && entity.is_default_manual ? 1 : 0);

        if (activeManualCount > 0 && defaultManualCount == 0)
        {
            throw new InvalidOperationException("Debe existir un diario manual por defecto activo para registrar partidas manuales.");
        }

        if (defaultManualCount > 1)
        {
            throw new InvalidOperationException("Solo puede existir un diario manual por defecto activo por empresa.");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return entity.journal_id;
    }

    public async Task<IReadOnlyList<PeriodoContableDto>> GetPeriodosAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        var periodos = await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId)
            .OrderByDescending(p => p.start_date)
            .Select(p => new
            {
                p.period_id,
                p.code,
                p.name,
                p.start_date,
                p.end_date,
                p.status_id,
                p.closed_at,
                p.closed_by
            })
            .ToListAsync(cancellationToken);

        return periodos
            .Select(p =>
            {
                var estadoId = EstadoPeriodoHelper.Require(p.status_id, $"con_periodo_contable.period_id={p.period_id}");
                return new PeriodoContableDto(
                    p.period_id,
                    p.code,
                    p.name,
                    NormalizeBusinessDate(p.start_date),
                    NormalizeBusinessDate(p.end_date),
                    EstadoPeriodoHelper.ToText(estadoId),
                    p.closed_at,
                    p.closed_by,
                    estadoId);
            })
            .ToList();
    }

    public async Task<long> SavePeriodoAsync(PeriodoContableUpsertDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("El codigo del periodo es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("El nombre del periodo es obligatorio.", nameof(request));
        }

        if (request.EndDate < request.StartDate)
        {
            throw new ArgumentException("La fecha de fin debe ser posterior a la fecha de inicio.", nameof(request));
        }

        var companyId = EnsureCompanyId();
        con_periodo_contable entity;
        if (request.PeriodId.HasValue)
        {
            entity = await _context.con_periodo_contables
                         .FirstOrDefaultAsync(p => p.period_id == request.PeriodId.Value, cancellationToken)
                     ?? throw new InvalidOperationException("El periodo contable no existe.");
        }
        else
        {
            entity = new con_periodo_contable
            {
                company_id = companyId,
                created_at = DateTime.UtcNow,
                created_by = request.User
            };
            await _context.con_periodo_contables.AddAsync(entity, cancellationToken);
        }

        var statusId = EstadoPeriodoHelper.Require(request.StatusId, nameof(request.StatusId));

        entity.code = request.Code.Trim().ToUpperInvariant();
        entity.name = request.Name.Trim();
        entity.start_date = NormalizeBusinessStartUtc(request.StartDate);
        entity.end_date = NormalizeBusinessEndUtc(request.EndDate);
        entity.status_id = statusId;
        entity.status = EstadoPeriodoHelper.ToText(statusId);
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        if (statusId == EstadoPeriodoHelper.CerradoId)
        {
            entity.closed_at ??= DateTime.UtcNow;
            entity.closed_by ??= request.User;
        }
        else
        {
            entity.closed_at = null;
            entity.closed_by = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return entity.period_id;
    }

    public async Task<bool> ClosePeriodoAsync(long periodId, string user, CancellationToken cancellationToken = default)
    {
        EnsureCompanyId();
        var entity = await _context.con_periodo_contables
            .FirstOrDefaultAsync(p => p.period_id == periodId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var statusId = EstadoPeriodoHelper.Require(entity.status_id, $"con_periodo_contable.period_id={entity.period_id}");
        if (statusId == EstadoPeriodoHelper.CerradoId)
        {
            return true;
        }

        entity.status_id = EstadoPeriodoHelper.CerradoId;
        entity.status = EstadoPeriodoHelper.ToText(EstadoPeriodoHelper.CerradoId);
        entity.closed_at = DateTime.UtcNow;
        entity.closed_by = user;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = user;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static readonly HashSet<string> ValidAccountTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVO", "PASIVO", "CAPITAL", "INGRESO", "GASTO", "MEMORANDA"
    };

    private static readonly HashSet<string> ValidStatus = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE"
    };

    private static short ResolveTipoStatusId(short? statusId, string? status)
    {
        if (statusId.HasValue)
        {
            return statusId.Value;
        }

        var normalized = string.IsNullOrWhiteSpace(status)
            ? "ACTIVE"
            : status.Trim().ToUpperInvariant();

        return normalized switch
        {
            "ACTIVE" => 1,
            "ACTIVO" => 1,
            "INACTIVE" => 0,
            "INACTIVO" => 0,
            _ => 1
        };
    }

    private static string ResolveTipoLegacyStatus(short statusId, string? status)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            return normalized switch
            {
                "ACTIVO" => "ACTIVE",
                "INACTIVO" => "INACTIVE",
                _ => normalized
            };
        }

        return statusId == 1 ? "ACTIVE" : "INACTIVE";
    }

    private static string ResolveTipoStatusText(short statusId, string? status)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToUpperInvariant();
            return normalized switch
            {
                "ACTIVE" => "ACTIVE",
                "ACTIVO" => "ACTIVE",
                "INACTIVE" => "INACTIVE",
                "INACTIVO" => "INACTIVE",
                _ => normalized
            };
        }

        return statusId == 1 ? "ACTIVE" : "INACTIVE";
    }


    public async Task<PlanCuentasImportResult> ImportPlanCuentasAsync(
    Stream fileStream,
    string user,
    bool dryRun,
    CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        var errors = new List<PlanCuentasImportError>();

        var workbook = new XLWorkbook(fileStream);
        try
        {
            var ws = workbook.Worksheets
                .FirstOrDefault(w => w.Name.Equals("PlanCuentas", StringComparison.OrdinalIgnoreCase));

            if (ws is null)
            {
                return new PlanCuentasImportResult(0, 0,
                    new[] { new PlanCuentasImportError(0, "", "No existe la hoja PlanCuentas.") });
            }

            var header = ws.FirstRowUsed();
            if (header is null)
            {
                return new PlanCuentasImportResult(0, 0,
                    new[] { new PlanCuentasImportError(0, "", "La hoja PlanCuentas esta vacia.") });
            }

            IReadOnlyList<string> SplitSegments(string raw)
            {
                var segments = new List<string>();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return segments;
                }

                var current = new StringBuilder();
                foreach (var ch in raw.Trim())
                {
                    if (char.IsLetterOrDigit(ch))
                    {
                        current.Append(ch);
                        continue;
                    }

                    if (current.Length > 0)
                    {
                        segments.Add(current.ToString());
                        current.Clear();
                    }
                }

                if (current.Length > 0)
                {
                    segments.Add(current.ToString());
                }

                return segments;
            }

            string NormalizeCode(string raw)
                => NormalizeAccountCode(raw).ToUpperInvariant();

            string? GetAccountTypeFromSegments(IReadOnlyList<string> segments)
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                var head = segments[0].Trim();
                if (head.Length == 0)
                {
                    return null;
                }

                var trimmed = head.TrimStart('0');
                var firstChar = trimmed.Length > 0 ? trimmed[0] : head[0];

                return firstChar switch
                {
                    '1' => "ACTIVO",
                    '2' => "PASIVO",
                    '3' => "CAPITAL",
                    '4' => "INGRESO",
                    '5' => "GASTO",
                    '6' => "MEMORANDA",
                    _ => null
                };
            }

            string? GetParentCode(IReadOnlyList<string> segments)
            {
                if (segments.Count <= 1)
                {
                    return null;
                }

                return string.Concat(segments.Take(segments.Count - 1));
            }

            var colMap = header.Cells()
                .ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            int? FindColumn(params string[] names)
            {
                foreach (var name in names)
                {
                    if (colMap.TryGetValue(name, out var col))
                    {
                        return col;
                    }
                }

                return null;
            }

            var codeCol = FindColumn("Codigo", "Code");
            var descCol = FindColumn("Descripcion", "Description", "Name");

            if (codeCol is null || descCol is null)
            {
                var missing = new List<string>();
                if (codeCol is null) missing.Add("Codigo");
                if (descCol is null) missing.Add("Descripcion");

                var msg = $"Faltan columnas obligatorias: {string.Join(", ", missing)}";
                return new PlanCuentasImportResult(0, 0, new[] { new PlanCuentasImportError(0, "", msg) });
            }

            string ReadString(int row, int col)
                => ws.Cell(row, col).GetString().Trim();

            var rowsByCode = new Dictionary<string, (int RowNumber, PlanCuentasImportRow Row)>(StringComparer.OrdinalIgnoreCase);
            var segmentsByCode = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var levelByCode = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? header.RowNumber();

            for (var r = header.RowNumber() + 1; r <= lastRow; r++)
            {
                var rawCode = ReadString(r, codeCol.Value);
                var name = ReadString(r, descCol.Value);

                if (string.IsNullOrWhiteSpace(rawCode) && string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var segments = SplitSegments(rawCode);
                if (segments.Count == 0)
                {
                    errors.Add(new PlanCuentasImportError(r, "", "Codigo es obligatorio."));
                    continue;
                }

                var normalizedCode = string.Concat(segments).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(normalizedCode))
                {
                    errors.Add(new PlanCuentasImportError(r, "", "Codigo es obligatorio."));
                    continue;
                }

                if (normalizedCode.Length > 30)
                {
                    errors.Add(new PlanCuentasImportError(r, normalizedCode, "El codigo supera 30 caracteres."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new PlanCuentasImportError(r, normalizedCode, "Descripcion es obligatoria."));
                    continue;
                }

                if (name.Length > 200)
                {
                    errors.Add(new PlanCuentasImportError(r, normalizedCode, "La descripcion supera 200 caracteres."));
                    continue;
                }

                var accountType = GetAccountTypeFromSegments(segments);
                if (string.IsNullOrWhiteSpace(accountType) || !ValidAccountTypes.Contains(accountType))
                {
                    errors.Add(new PlanCuentasImportError(r, normalizedCode, "No se pudo determinar el tipo de cuenta."));
                    continue;
                }

                if (rowsByCode.ContainsKey(normalizedCode))
                {
                    errors.Add(new PlanCuentasImportError(r, normalizedCode, "Codigo duplicado en el archivo."));
                    continue;
                }

                var parentCode = GetParentCode(segments);
                var row = new PlanCuentasImportRow(
                    normalizedCode,
                    name,
                    accountType,
                    parentCode,
                    null,
                    null,
                    "ACTIVE",
                    null,
                    name);

                rowsByCode[normalizedCode] = (r, row);
                segmentsByCode[normalizedCode] = segments;
                levelByCode[normalizedCode] = (short)segments.Count;
            }

            if (errors.Count > 0)
            {
                return new PlanCuentasImportResult(0, 0, errors);
            }

            var existingList = await _context.con_plan_cuentas
                .Where(c => c.company_id == companyId)
                .ToListAsync(cancellationToken);

            var existingByCode = existingList
                .Where(c => !string.IsNullOrWhiteSpace(c.code))
                .GroupBy(c => NormalizeCode(c.code), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var pending = new Queue<string>(rowsByCode.Keys);
            while (pending.Count > 0)
            {
                var code = pending.Dequeue();
                if (!segmentsByCode.TryGetValue(code, out var childSegments))
                {
                    continue;
                }

                var parentCode = GetParentCode(childSegments);
                if (string.IsNullOrWhiteSpace(parentCode))
                {
                    continue;
                }

                if (rowsByCode.ContainsKey(parentCode) || existingByCode.ContainsKey(parentCode))
                {
                    continue;
                }

                var parentSegments = childSegments.Take(childSegments.Count - 1).ToList();
                var parentAccountType = GetAccountTypeFromSegments(parentSegments);
                if (string.IsNullOrWhiteSpace(parentAccountType))
                {
                    errors.Add(new PlanCuentasImportError(0, parentCode, "No se pudo determinar el tipo de cuenta del padre."));
                    continue;
                }

                var autoDescription = $"Cuenta creada automaticamente (faltaba padre: {code})";
                var autoRow = new PlanCuentasImportRow(
                    parentCode,
                    autoDescription,
                    parentAccountType,
                    GetParentCode(parentSegments),
                    null,
                    null,
                    "ACTIVE",
                    null,
                    autoDescription);

                rowsByCode[parentCode] = (0, autoRow);
                segmentsByCode[parentCode] = parentSegments;
                levelByCode[parentCode] = (short)parentSegments.Count;
                pending.Enqueue(parentCode);
            }

            if (errors.Count > 0)
            {
                return new PlanCuentasImportResult(0, 0, errors);
            }

            var hasChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in rowsByCode.Values)
            {
                if (!string.IsNullOrWhiteSpace(item.Row.ParentCode))
                {
                    hasChildren.Add(item.Row.ParentCode);
                }
            }

            var orderedRows = rowsByCode.Values
                .OrderBy(r => levelByCode.TryGetValue(r.Row.Code, out var level) ? level : (short)1)
                .ThenBy(r => r.Row.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var inserted = 0;
            var updated = 0;

            foreach (var item in orderedRows)
            {
                var row = item.Row;
                var code = row.Code.Trim().ToUpperInvariant();

                var isNew = false;
                if (!existingByCode.TryGetValue(code, out var entity))
                {
                    entity = new con_plan_cuenta
                    {
                        company_id = companyId,
                        created_at = DateTime.UtcNow,
                        created_by = user,
                        code = code
                    };
                    existingByCode[code] = entity;
                    _context.con_plan_cuentas.Add(entity);
                    inserted++;
                    isNew = true;
                }
                else
                {
                    updated++;
                }

                if (isNew)
                {
                    entity.code = code;
                }
                entity.name = row.Name.Trim();
                entity.description = row.Description?.Trim();
                entity.account_type = row.AccountType.Trim().ToUpperInvariant();
                entity.category = null;
                entity.allows_posting = !hasChildren.Contains(code);
                entity.allows_budget = false;
                entity.allows_third = false;
                entity.is_tax_base = false;
                entity.allows_cost_center = false;
                entity.allows_multi_currency = false;
                entity.currency_code = null;
                entity.status = "ACTIVE";
                entity.updated_at = DateTime.UtcNow;
                entity.updated_by = user;

                if (!string.IsNullOrWhiteSpace(row.ParentCode))
                {
                    var parentCode = row.ParentCode.Trim().ToUpperInvariant();
                    if (existingByCode.TryGetValue(parentCode, out var parent))
                    {
                        entity.parent_account = parent;
                        entity.level = (short)(parent.level + 1);
                    }
                    else
                    {
                        entity.parent_account_id = null;
                        entity.level = levelByCode.TryGetValue(code, out var level) ? level : (short)1;
                    }
                }
                else
                {
                    entity.parent_account_id = null;
                    entity.level = 1;
                }
            }

            if (!dryRun)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return new PlanCuentasImportResult(inserted, updated, errors);
        }
        finally
        {
            (workbook as IDisposable)?.Dispose();
        }
    }
    private static string NormalizeSimpleCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static long NormalizeDocumentSequenceStart(long value)
    {
        return value < 1 ? 1 : value;
    }

    private static long GetNextDocumentNumber(long documentSequenceStart, long lastDocumentNumber)
    {
        return Math.Max(documentSequenceStart, lastDocumentNumber + 1);
    }

    private static DateTime NormalizeBusinessDate(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return value.Date;
        }

        return TimeZoneInfo.ConvertTime(value, BusinessTimeZone).Date;
    }

    private static DateTime NormalizeBusinessStartUtc(DateTime value)
    {
        var localDate = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDate, BusinessTimeZone);
    }

    private static DateTime NormalizeBusinessEndUtc(DateTime value)
    {
        var localDate = DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified)
            .AddHours(23)
            .AddMinutes(59)
            .AddSeconds(59);

        return TimeZoneInfo.ConvertTimeToUtc(localDate, BusinessTimeZone);
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

    public async Task<IReadOnlyList<TipoTransaccionDto>> GetTiposTransaccionAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        var tipos = await _context.con_tipo_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId)
            .OrderBy(t => t.code)
            .Select(t => new
            {
                t.type_id,
                t.company_id,
                t.code,
                t.name,
                t.description,
                t.category,
                t.type_trans,
                t.type_oper,
                t.frequency,
                t.max_entries,
                t.allows_cost_center,
                t.allows_third_party,
                t.allows_cash_flow,
                t.allows_account_limit,
                t.is_default,
                t.is_automatic,
                t.status_id,
                t.status,
                t.document_sequence_start,
                t.last_document_number,
                has_polizas = _context.con_partida_hdrs.Any(p =>
                    p.company_id == t.company_id &&
                    p.type_id == t.type_id),
                t.created_at,
                t.created_by
            })
            .ToListAsync(cancellationToken);

        var ultimoDocumentoPorTipo = (await _context.con_partida_hdrs
                .AsNoTracking()
                .Where(p => p.company_id == companyId &&
                            p.module == "MANUAL" &&
                            p.document_number != null)
                .Select(p => new { p.type_id, p.document_number })
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.type_id)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(p => TryParseDocumentNumber(p.document_number))
                    .Where(p => p.HasValue)
                    .Select(p => p!.Value)
                    .DefaultIfEmpty(0L)
                    .Max());

        return tipos
            .Select(t =>
            {
                var statusId = ResolveTipoStatusId(t.status_id, t.status);
                var ultimoDocumentoRegistrado = Math.Max(
                    t.last_document_number,
                    ultimoDocumentoPorTipo.GetValueOrDefault(t.type_id));

                return new TipoTransaccionDto(
                    t.type_id,
                    t.company_id,
                    t.code,
                    t.name,
                    t.description,
                    t.category,
                    t.type_trans,
                    t.type_oper,
                    t.frequency,
                    t.max_entries,
                    t.allows_cost_center,
                    t.allows_third_party,
                    t.allows_cash_flow,
                    t.allows_account_limit,
                    t.is_default,
                    t.is_automatic,
                    ResolveTipoStatusText(statusId, t.status),
                    t.created_at,
                    t.created_by,
                    statusId,
                    t.document_sequence_start,
                    ultimoDocumentoRegistrado,
                    GetNextDocumentNumber(t.document_sequence_start, ultimoDocumentoRegistrado),
                    t.has_polizas);
            })
            .ToList();
    }

    public async Task<long> SaveTipoTransaccionAsync(TipoTransaccionUpsertDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("El codigo es obligatorio.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(request));

        var companyId = EnsureCompanyId();
        var isNew = !request.TypeId.HasValue;
        var normalizedCode = NormalizeSimpleCode(request.Code);
        var normalizedDocumentSequenceStart = NormalizeDocumentSequenceStart(request.DocumentSequenceStart);

        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new ArgumentException("El codigo es obligatorio.", nameof(request));
        if (normalizedCode.Length > 20)
            throw new ArgumentException("El codigo supera 20 caracteres.", nameof(request));

        if (isNew)
        {
            var exists = await _context.con_tipo_transacciones
                .AsNoTracking()
                .AnyAsync(t => t.company_id == companyId && t.code == normalizedCode, cancellationToken);
            if (exists)
                throw new InvalidOperationException("Ya existe un tipo de transaccion con este codigo.");
        }

        con_tipo_transaccion entity;
        if (!isNew)
        {
            entity = await _context.con_tipo_transacciones
                .FirstOrDefaultAsync(t => t.company_id == companyId && t.type_id == request.TypeId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("El tipo de transaccion no existe.");
            if (!string.Equals(entity.code, normalizedCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("No se puede modificar el codigo de un tipo existente.");
        }
        else
        {
            entity = new con_tipo_transaccion
            {
                company_id = companyId,
                created_at = DateTime.UtcNow,
                created_by = request.User
            };
        }

        var hasPolizas = !isNew && await _context.con_partida_hdrs
            .AsNoTracking()
            .AnyAsync(p => p.company_id == companyId && p.type_id == entity.type_id, cancellationToken);

        if (hasPolizas && normalizedDocumentSequenceStart != entity.document_sequence_start)
        {
            throw new InvalidOperationException("No se puede modificar el numero inicial porque el tipo ya tiene pólizas registradas.");
        }

        if (request.IsDefault)
        {
            var defaults = await _context.con_tipo_transacciones
                .Where(t => t.company_id == companyId && t.is_default)
                .ToListAsync(cancellationToken);

            foreach (var item in defaults)
                item.is_default = false;
        }

        var tipoStatusId = ResolveTipoStatusId(request.StatusId, request.Status);

        entity.code = normalizedCode;
        entity.name = request.Name.Trim();
        entity.description = request.Description?.Trim();
        entity.category = request.Category.Trim().ToUpperInvariant();
        entity.type_trans = request.TypeTrans;
        entity.type_oper = request.TypeOper;
        entity.frequency = request.Frequency;
        entity.max_entries = request.MaxEntries;
        entity.document_sequence_start = normalizedDocumentSequenceStart;
        if (isNew || !hasPolizas)
        {
            entity.last_document_number = normalizedDocumentSequenceStart - 1;
        }
        entity.allows_cost_center = request.AllowsCostCenter;
        entity.allows_third_party = request.AllowsThirdParty;
        entity.allows_cash_flow = request.AllowsCashFlow;
        entity.allows_account_limit = request.AllowsAccountLimit;
        entity.is_default = request.IsDefault;
        entity.is_automatic = request.IsAutomatic;
        entity.status_id = tipoStatusId;
        entity.status = ResolveTipoLegacyStatus(tipoStatusId, request.Status);
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        if (isNew) _context.con_tipo_transacciones.Add(entity);

        await _context.SaveChangesAsync(cancellationToken);
        return entity.type_id;
    }

    public async Task<bool> DeleteTipoTransaccionAsync(long typeId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var entity = await _context.con_tipo_transacciones
            .FirstOrDefaultAsync(t => t.company_id == companyId && t.type_id == typeId, cancellationToken);
        if (entity is null) return false;

        var hasRules = await _context.con_tipo_transaccion_rules
            .AnyAsync(r => r.company_id == companyId && r.type_id == typeId, cancellationToken);
        if (hasRules)
            throw new InvalidOperationException("No se puede eliminar: tiene reglas asociadas.");

        _context.con_tipo_transacciones.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TipoTransaccionRuleDto>> GetTipoTransaccionRulesAsync(long typeId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_tipo_transaccion_rules
            .AsNoTracking()
            .Where(r => r.company_id == companyId && r.type_id == typeId)
            .OrderBy(r => r.line_number)
            .Select(r => new TipoTransaccionRuleDto(
                r.rule_id,
                r.company_id,
                r.type_id,
                r.line_number,
                r.account_code_from,
                r.account_code_to,
                r.cost_center_code_from,
                r.cost_center_code_to,
                r.third_party_code_from,
                r.third_party_code_to,
                r.is_active,
                r.created_at,
                r.created_by))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SaveTipoTransaccionRuleAsync(TipoTransaccionRuleUpsertDto request, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();

        var typeExists = await _context.con_tipo_transacciones
            .AsNoTracking()
            .AnyAsync(t => t.company_id == companyId && t.type_id == request.TypeId, cancellationToken);
        if (!typeExists)
            throw new InvalidOperationException("El tipo de transaccion no existe.");

        // Asignar n° de línea automático si es nuevo o viene vacío/<=0
        var lineNumber = request.LineNumber;
        if (!request.RuleId.HasValue || lineNumber <= 0)
        {
            var currentMax = await _context.con_tipo_transaccion_rules
                .Where(r => r.company_id == companyId && r.type_id == request.TypeId)
                .Select(r => (int?)r.line_number)
                .MaxAsync(cancellationToken);
            lineNumber = (currentMax ?? 0) + 1;
        }

        var existsLine = await _context.con_tipo_transaccion_rules
            .AnyAsync(r => r.company_id == companyId
                           && r.type_id == request.TypeId
                           && r.line_number == lineNumber
                           && (!request.RuleId.HasValue || r.rule_id != request.RuleId.Value),
                cancellationToken);
        if (existsLine)
            throw new InvalidOperationException("Ya existe una regla con ese numero de linea.");

        // Validar rangos contra catálogos (si se proporcionan)
        async Task ValidateAccountAsync(string? code)
        {
            var normalized = NormalizeSimpleCode(code);
            if (string.IsNullOrWhiteSpace(normalized)) return;
            var ok = await _context.con_plan_cuentas
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.code == normalized, cancellationToken);
            if (!ok) throw new InvalidOperationException($"La cuenta {normalized} no existe en el plan de cuentas.");
        }

        async Task ValidateCostCenterAsync(string? code)
        {
            var normalized = NormalizeSimpleCode(code);
            if (string.IsNullOrWhiteSpace(normalized)) return;
            var ok = await _context.con_centro_costos
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.code == normalized, cancellationToken);
            if (!ok) throw new InvalidOperationException($"El centro de costo {normalized} no existe.");
        }

        async Task ValidateThirdAsync(string? code)
        {
            var normalized = NormalizeSimpleCode(code);
            if (string.IsNullOrWhiteSpace(normalized)) return;
            var ok = await _context.con_terceros
                .AsNoTracking()
                .AnyAsync(c => c.company_id == companyId && c.code == normalized, cancellationToken);
            if (!ok) throw new InvalidOperationException($"El tercero {normalized} no existe.");
        }

        await ValidateAccountAsync(request.AccountCodeFrom);
        await ValidateAccountAsync(request.AccountCodeTo);
        await ValidateCostCenterAsync(request.CostCenterCodeFrom);
        await ValidateCostCenterAsync(request.CostCenterCodeTo);
        await ValidateThirdAsync(request.ThirdPartyCodeFrom);
        await ValidateThirdAsync(request.ThirdPartyCodeTo);

        con_tipo_transaccion_rule entity;
        if (request.RuleId.HasValue)
        {
            entity = await _context.con_tipo_transaccion_rules
                .FirstOrDefaultAsync(r => r.company_id == companyId && r.rule_id == request.RuleId.Value, cancellationToken)
                ?? throw new InvalidOperationException("La regla no existe.");
        }
        else
        {
            entity = new con_tipo_transaccion_rule
            {
                company_id = companyId,
                type_id = request.TypeId,
                created_at = DateTime.UtcNow,
                created_by = request.User
            };
        }

        entity.line_number = lineNumber;
        entity.account_code_from = NormalizeSimpleCode(request.AccountCodeFrom);
        entity.account_code_to = NormalizeSimpleCode(request.AccountCodeTo);
        entity.cost_center_code_from = NormalizeSimpleCode(request.CostCenterCodeFrom);
        entity.cost_center_code_to = NormalizeSimpleCode(request.CostCenterCodeTo);
        entity.third_party_code_from = NormalizeSimpleCode(request.ThirdPartyCodeFrom);
        entity.third_party_code_to = NormalizeSimpleCode(request.ThirdPartyCodeTo);
        entity.is_active = request.IsActive;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        if (!request.RuleId.HasValue)
            _context.con_tipo_transaccion_rules.Add(entity);

        await _context.SaveChangesAsync(cancellationToken);
        return entity.rule_id;
    }

    public async Task<bool> DeleteTipoTransaccionRuleAsync(long ruleId, CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        var entity = await _context.con_tipo_transaccion_rules
            .FirstOrDefaultAsync(r => r.company_id == companyId && r.rule_id == ruleId, cancellationToken);

        if (entity is null) return false;
        _context.con_tipo_transaccion_rules.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

}









