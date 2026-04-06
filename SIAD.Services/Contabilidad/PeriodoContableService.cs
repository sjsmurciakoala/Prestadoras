using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

using SIAD.Core.Constants;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementacion del servicio de periodos contables.
/// </summary>
public sealed class PeriodoContableService : IPeriodoContableService
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

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

        var periodos = await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId)
            .ToListAsync(ct);

        var periodosAbiertos = periodos
            .Where(p => EstadoPeriodoHelper.IsOpen(p.status_id))
            .ToList();

        if (periodosAbiertos.Count == 0)
        {
            return null;
        }

        if (periodosAbiertos.Count > 1)
        {
            throw new InvalidOperationException(
                $"La empresa {companyId} tiene multiples periodos en estado 0 (Abierto). Revisa el calendario contable antes de continuar.");
        }

        return MapearDto(periodosAbiertos[0]);
    }

    public async Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        var estados = await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId)
            .Select(p => p.status_id)
            .ToListAsync(ct);

        return estados.Any(EstadoPeriodoHelper.IsOpen);
    }

    public async Task<PeriodoContableDto> ObtenerOCrearPeriodoInicialAsync(
        long companyId,
        DateTime? fechaInicio = null,
        CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        var periodoExistente = await ObtenerPeriodoActivoAsync(companyId, ct);
        if (periodoExistente is not null)
        {
            return periodoExistente;
        }

        var ahora = DateTime.UtcNow;
        var fechaBase = fechaInicio?.Date ?? new DateTime(ahora.Year, 1, 1);
        var fecha = NormalizeBusinessStartUtc(fechaBase);
        var fechaFin = NormalizeBusinessEndUtc(new DateTime(fechaBase.Year, fechaBase.Month, DateTime.DaysInMonth(fechaBase.Year, fechaBase.Month)));

        var nombrePeriodo = $"{fechaBase.Year}-{fechaBase.Month:D2}";
        var codigoPeriodo = nombrePeriodo;

        var nuevoPeriodo = new con_periodo_contable
        {
            company_id = companyId,
            code = codigoPeriodo,
            name = nombrePeriodo,
            start_date = fecha,
            end_date = fechaFin,
            status = EstadoPeriodoHelper.ToText(EstadoPeriodoHelper.AbiertoId),
            status_id = EstadoPeriodoHelper.AbiertoId,
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
        var estadoId = EstadoPeriodoHelper.Require(periodo.status_id, $"con_periodo_contable.period_id={periodo.period_id}");
        return new PeriodoContableDto(
            periodo.period_id,
            periodo.code,
            periodo.name,
            NormalizeBusinessDate(periodo.start_date),
            NormalizeBusinessDate(periodo.end_date),
            EstadoPeriodoHelper.ToText(estadoId),
            periodo.closed_at,
            periodo.closed_by,
            estadoId);
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
}
