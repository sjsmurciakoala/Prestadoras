using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación de IPolizaService
/// Gestiona pólizas contables respetando estructura DB multiempresa
/// </summary>
public sealed class PolizaService : IPolizaService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompany;
    private readonly ISaldosService _saldosService;

    public PolizaService(SiadDbContext context, ICurrentCompanyService currentCompany, ISaldosService saldosService)
    {
        _context = context;
        _currentCompany = currentCompany;
        _saldosService = saldosService;
    }

    public async Task<long> CrearAsync(
        long companyId,
        long? periodId,
        long? journalId,
        DateTime polizaDate,
        string module,
        string documentType,
        string description,
        List<PolizaLineaCrearDto> lineas,
        string userId,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(documentType);
        ArgumentNullException.ThrowIfNull(userId);

        // Validar período si se proporciona
        if (periodId.HasValue)
        {
            var periodo = await _context.con_periodo_contables
                .Where(x => x.period_id == periodId && x.company_id == companyId)
                .FirstOrDefaultAsync(ct);

            if (periodo == null)
                throw new InvalidOperationException($"Período {periodId} no encontrado para empresa {companyId}");

            if (periodo.status != "OPEN")
                throw new InvalidOperationException($"Período debe estar en estado OPEN para registrar pólizas");
        }

        // Validar diario si se proporciona
        if (journalId.HasValue)
        {
            var diario = await _context.con_diarios
                .Where(x => x.journal_id == journalId && x.company_id == companyId)
                .FirstOrDefaultAsync(ct);

            if (diario == null)
                throw new InvalidOperationException($"Diario {journalId} no encontrado para empresa {companyId}");
        }

        // Generar número de póliza único por empresa
        var siguienteNumero = await GenerarNumeroPolizaAsync(companyId, ct);

        // Crear póliza en estado DRAFT
        var poliza = new con_poliza
        {
            company_id = companyId,
            period_id = periodId,
            journal_id = journalId,
            poliza_number = siguienteNumero,
            poliza_date = polizaDate,
            module = module,
            document_type = documentType,
            description = description,
            status = "DRAFT",
            created_at = DateTime.UtcNow,
            created_by = userId
        };

        _context.con_polizas.Add(poliza);
        await _context.SaveChangesAsync(ct);

        // Agregar líneas
        if (lineas != null && lineas.Count > 0)
        {
            short lineNumber = 1;
            foreach (var linea in lineas)
            {
                await AgregarLineaAsync(companyId, poliza.poliza_id, linea, userId, ct);
                lineNumber++;
            }
        }

        return poliza.poliza_id;
    }

    public async Task<PolizaConLineasDto> ObtenerAsync(long companyId, long polizaId, CancellationToken ct = default)
    {
        var poliza = await _context.con_polizas
            .AsNoTracking()
            .Include(p => p.lineas)
            .ThenInclude(l => l.account)
            .Include(p => p.lineas)
            .ThenInclude(l => l.cost_center)
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        var totalDebit = poliza.lineas?.Sum(x => x.debit_amount) ?? 0;
        var totalCredit = poliza.lineas?.Sum(x => x.credit_amount) ?? 0;
        var isBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m;

        return new PolizaConLineasDto(
            poliza.poliza_id,
            poliza.company_id,
            poliza.period_id,
            poliza.journal_id,
            poliza.poliza_number,
            poliza.poliza_date,
            poliza.module,
            poliza.document_type,
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
        return await _context.con_polizas
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.period_id == periodId)
            .OrderByDescending(x => x.poliza_date)
            .Skip(skip)
            .Take(take)
            .Select(p => new PolizaListaDto(
                p.poliza_id,
                p.poliza_number,
                p.poliza_date,
                p.module,
                p.document_type,
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
        return await _context.con_polizas
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.journal_id == journalId)
            .OrderByDescending(x => x.poliza_date)
            .Skip(skip)
            .Take(take)
            .Select(p => new PolizaListaDto(
                p.poliza_id,
                p.poliza_number,
                p.poliza_date,
                p.module,
                p.document_type,
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
        var poliza = await _context.con_polizas
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        if (poliza.status != "DRAFT")
            throw new InvalidOperationException("Solo se pueden actualizar pólizas en estado DRAFT");

        poliza.poliza_date = dto.PolizaDate;
        poliza.description = dto.Description;
        poliza.updated_at = DateTime.UtcNow;
        poliza.updated_by = userId;

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
        var poliza = await _context.con_polizas
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        if (poliza.status != "DRAFT")
            throw new InvalidOperationException("Solo se pueden agregar líneas a pólizas en estado DRAFT");

        // Validar cuenta
        var cuenta = await _context.con_plan_cuentas
            .Where(x => x.account_id == linea.AccountId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Cuenta {linea.AccountId} no encontrada");

        // Obtener próximo número de línea
        var proximoNumero = (short)((_context.con_poliza_lineas
            .Where(x => x.poliza_id == polizaId)
            .Max(x => (short?)x.line_number) ?? 0) + 1);

        var polizaLinea = new con_poliza_linea
        {
            company_id = companyId,
            poliza_id = polizaId,
            line_number = proximoNumero,
            account_id = linea.AccountId,
            cost_center_id = linea.CostCenterId,
            debit_amount = linea.DebitAmount,
            credit_amount = linea.CreditAmount,
            description = linea.Description,
            currency_code = linea.CurrencyCode ?? "HNL",
            exchange_rate = linea.ExchangeRate ?? 1m,
            source_document = linea.SourceDocument
        };

        _context.con_poliza_lineas.Add(polizaLinea);
        await _context.SaveChangesAsync(ct);
    }

    public async Task EliminarLineaAsync(long companyId, long lineaId, CancellationToken ct = default)
    {
        var linea = await _context.con_poliza_lineas
            .Include(x => x.poliza)
            .Where(x => x.poliza_line_id == lineaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Línea {lineaId} no encontrada");

        if (linea.poliza?.status != "DRAFT")
            throw new InvalidOperationException("Solo se pueden eliminar líneas de pólizas en estado DRAFT");

        _context.con_poliza_lineas.Remove(linea);
        await _context.SaveChangesAsync(ct);
    }

    public async Task EliminarAsync(long companyId, long polizaId, CancellationToken ct = default)
    {
        var poliza = await _context.con_polizas
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        if (poliza.status != "DRAFT")
            throw new InvalidOperationException("Solo se pueden eliminar pólizas en estado DRAFT");

        _context.con_polizas.Remove(poliza);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(bool balanceado, decimal debitTotal, decimal creditTotal)> ValidarBalanceAsync(
        long companyId,
        long polizaId,
        CancellationToken ct = default
    )
    {
        var poliza = await _context.con_polizas
            .AsNoTracking()
            .Include(p => p.lineas)
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        var totalDebit = poliza.lineas?.Sum(x => x.debit_amount) ?? 0;
        var totalCredit = poliza.lineas?.Sum(x => x.credit_amount) ?? 0;

        return (Math.Abs(totalDebit - totalCredit) < 0.01m, totalDebit, totalCredit);
    }

    public async Task RegistrarAsync(long companyId, long polizaId, string userId, CancellationToken ct = default)
    {
        var poliza = await _context.con_polizas
            .Include(p => p.lineas)
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        if (poliza.status != "DRAFT")
            throw new InvalidOperationException($"Póliza debe estar en estado DRAFT para registrarse. Estado actual: {poliza.status}");

        // Validar balance
        var (balanceado, debit, credit) = await ValidarBalanceAsync(companyId, polizaId, ct);
        if (!balanceado)
            throw new InvalidOperationException($"Póliza no está balanceada. Débito: {debit}, Crédito: {credit}");

        // Cambiar estado a POSTED
        poliza.status = "POSTED";
        poliza.updated_at = DateTime.UtcNow;
        poliza.updated_by = userId;

        await _context.SaveChangesAsync(ct);

        // Actualizar saldos
        await _saldosService.ActualizarSaldosPorPolizaAsync(companyId, polizaId, sumar: true, ct);
    }

    public async Task RevertirAsync(long companyId, long polizaId, string userId, CancellationToken ct = default)
    {
        var poliza = await _context.con_polizas
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        if (poliza.status != "POSTED")
            throw new InvalidOperationException($"Solo se pueden revertir pólizas en estado POSTED. Estado actual: {poliza.status}");

        poliza.status = "DRAFT";
        poliza.updated_at = DateTime.UtcNow;
        poliza.updated_by = userId;

        await _context.SaveChangesAsync(ct);

        // Revertir saldos
        await _saldosService.ActualizarSaldosPorPolizaAsync(companyId, polizaId, sumar: false, ct);
    }

    private async Task<string> GenerarNumeroPolizaAsync(long companyId, CancellationToken ct = default)
    {
        // Estrategia simple: empresa-año-número secuencial
        var year = DateTime.Now.Year;
        var proximoNumero = await _context.con_polizas
            .Where(x => x.company_id == companyId)
            .CountAsync(ct) + 1;

        return $"{companyId}-{year}-{proximoNumero:D6}";
    }
}
