using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación del servicio de períodos contables.
/// </summary>
public sealed class PeriodoContableService : IPeriodoContableService
{
    private readonly SiadDbContext _context;

    public PeriodoContableService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        var periodo = await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId && p.status == "ABIERTO")
            .OrderByDescending(p => p.end_date)
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
        {
            return null;
        }

        return MapearDto(periodo);
    }

    public async Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        return await _context.con_periodo_contables
            .AsNoTracking()
            .AnyAsync(p => p.company_id == companyId && p.status == "ABIERTO", ct);
    }

    public async Task<PeriodoContableDto> ObtenerOCrearPeriodoInicialAsync(long companyId, DateTime? fechaInicio = null,
        CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        // Intentar obtener período existente
        var periodoExistente = await ObtenerPeriodoActivoAsync(companyId, ct);
        if (periodoExistente is not null)
        {
            return periodoExistente;
        }

        // Crear período inicial
        var ahora = DateTime.UtcNow;
        var fecha = (fechaInicio.HasValue ? DateTime.SpecifyKind(fechaInicio.Value, DateTimeKind.Utc) : new DateTime(ahora.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var fechaFin = new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month), 23, 59, 59, DateTimeKind.Utc);

        var nombrePeriodo = $"{fecha.Year}-{fecha.Month:D2}";
        var codigoPeriodo = nombrePeriodo;

        var nuevoPeriodo = new con_periodo_contable
        {
            company_id = companyId,
            code = codigoPeriodo,
            name = nombrePeriodo,
            start_date = fecha,
            end_date = fechaFin,
            status = "ABIERTO",
            created_at = ahora,
            created_by = "system",
            updated_at = ahora,
            updated_by = "system",
            closed_at = null,
            closed_by = null
        };

        _context.con_periodo_contables.Add(nuevoPeriodo);
        await _context.SaveChangesAsync(ct);

        return MapearDto(nuevoPeriodo);
    }

    private static PeriodoContableDto MapearDto(con_periodo_contable periodo)
    {
        return new PeriodoContableDto
        {
            PeriodoId = periodo.period_id,
            CompanyId = periodo.company_id,
            Año = periodo.start_date.Year,
            Mes = (byte)periodo.start_date.Month,
            FechaInicio = periodo.start_date,
            FechaFin = periodo.end_date,
            Estado = periodo.status ?? "ABIERTO"
        };
    }
}
