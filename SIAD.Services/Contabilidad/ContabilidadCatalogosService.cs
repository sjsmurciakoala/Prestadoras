using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

public class ContabilidadCatalogosService : IContabilidadCatalogosService
{
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
                c.status,
                c.description,
                c.currency_code))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SavePlanCuentaAsync(PlanCuentaUpsertDto request, CancellationToken cancellationToken = default)
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
        con_plan_cuenta entity;
        if (request.AccountId.HasValue)
        {
            entity = await _context.con_plan_cuentas
                .FirstOrDefaultAsync(c => c.account_id == request.AccountId.Value, cancellationToken)
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
            await _context.con_plan_cuentas.AddAsync(entity, cancellationToken);
        }

        if (request.ParentAccountId.HasValue)
        {
            var parent = await _context.con_plan_cuentas
                .AsNoTracking()
                .Where(c => c.account_id == request.ParentAccountId.Value)
                .Select(c => new { c.account_id, c.level })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null)
            {
                throw new InvalidOperationException("La cuenta padre indicada no existe.");
            }

            entity.parent_account_id = parent.account_id;
            entity.level = (short)(parent.level + 1);
        }
        else
        {
            entity.parent_account_id = null;
            entity.level = 1;
        }

        entity.code = request.Code.Trim().ToUpperInvariant();
        entity.name = request.Name.Trim();
        entity.description = request.Description?.Trim();
        entity.account_type = request.AccountType.Trim().ToUpperInvariant();
        entity.category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.allows_posting = request.AllowsPosting;
        entity.currency_code = string.IsNullOrWhiteSpace(request.CurrencyCode) ? null : request.CurrencyCode.Trim().ToUpperInvariant();
        entity.status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status.Trim().ToUpperInvariant();
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        await _context.SaveChangesAsync(cancellationToken);
        return entity.account_id;
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
                c.status))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SaveCentroCostoAsync(CentroCostoUpsertDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("El código del centro de costo es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("El nombre del centro de costo es obligatorio.", nameof(request));
        }

        var companyId = EnsureCompanyId();
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

        entity.code = request.Code.Trim().ToUpperInvariant();
        entity.name = request.Name.Trim();
        entity.description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status.Trim().ToUpperInvariant();
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        await _context.SaveChangesAsync(cancellationToken);
        return entity.cost_center_id;
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
                d.allows_manual))
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

        entity.code = request.Code.Trim().ToUpperInvariant();
        entity.name = request.Name.Trim();
        entity.description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.sequence_prefix = string.IsNullOrWhiteSpace(request.SequencePrefix) ? null : request.SequencePrefix.Trim().ToUpperInvariant();
        entity.is_active = request.IsActive;
        entity.allows_manual = request.AllowsManual;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

        await _context.SaveChangesAsync(cancellationToken);
        return entity.journal_id;
    }

    public async Task<IReadOnlyList<PeriodoContableDto>> GetPeriodosAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId)
            .OrderByDescending(p => p.start_date)
            .Select(p => new PeriodoContableDto(
                p.period_id,
                p.code,
                p.name,
                p.start_date,
                p.end_date,
                p.status,
                p.closed_at,
                p.closed_by))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> SavePeriodoAsync(PeriodoContableUpsertDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("El código del período es obligatorio.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("El nombre del período es obligatorio.", nameof(request));
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
                ?? throw new InvalidOperationException("El período contable no existe.");
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

        entity.code = request.Code.Trim().ToUpperInvariant();
        entity.name = request.Name.Trim();
        entity.start_date = request.StartDate.Date;
        entity.end_date = request.EndDate.Date;
        entity.status = string.IsNullOrWhiteSpace(request.Status) ? "OPEN" : request.Status.Trim().ToUpperInvariant();
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = request.User;

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

        if (string.Equals(entity.status, "CLOSED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        entity.status = "CLOSED";
        entity.closed_at = DateTime.UtcNow;
        entity.closed_by = user;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = user;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
