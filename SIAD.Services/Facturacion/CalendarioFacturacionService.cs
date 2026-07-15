using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Facturacion;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Facturacion;

/// <summary>
/// Implementación del calendario de facturación. Usa el filtro global de
/// tenant de SiadDbContext (calendariopro es ICompanyScopedEntity desde la
/// Fase A); el companyId explícito refuerza el scope y valida entrada.
/// </summary>
public sealed class CalendarioFacturacionService : ICalendarioFacturacionService
{
    private readonly SiadDbContext _context;

    public CalendarioFacturacionService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<int>> ListarAniosAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        return await _context.calendariopros
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .Select(c => c.ano)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync(ct);
    }

    public async Task<CalendarioAnioDto> ObtenerAnioAsync(long companyId, int anio, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        ValidarAnio(anio);

        var filas = await _context.calendariopros
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.ano == anio)
            .OrderBy(c => c.mes)
            .ThenBy(c => c.ciclo)
            .ToListAsync(ct);

        return new CalendarioAnioDto { Anio = anio, Filas = filas.Select(MapDto).ToList() };
    }

    public async Task<CalendarioAnioDto> GuardarAnioAsync(long companyId, int anio, List<CalendarioCicloDto> filas,
        string usuario, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        ValidarAnio(anio);
        ArgumentNullException.ThrowIfNull(filas);

        var entrada = Normalizar(anio, filas);
        var error = CalendarioFacturacionReglas.ValidarConjunto(entrada);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        // Transacción propia solo si no hay una ambiente (los tests de
        // integración corren dentro de la transacción del fixture).
        await using var txPropia = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;

        var existentes = await _context.calendariopros
            .Where(c => c.company_id == companyId && c.ano == anio)
            .ToListAsync(ct);
        var existentesPorIde = existentes.ToDictionary(c => c.ide);

        var idesEntrantes = new HashSet<int>(entrada.Where(f => f.Ide > 0).Select(f => f.Ide));

        var hayBorrados = false;
        foreach (var borrar in existentes.Where(c => !idesEntrantes.Contains(c.ide)))
        {
            _context.calendariopros.Remove(borrar);
            hayBorrados = true;
        }

        // Los borrados se confirman ANTES del upsert: EF no garantiza el orden
        // DELETE/UPDATE dentro de un SaveChanges, y borrar el ciclo '01' para
        // renombrar otro a '01' en el mismo lote chocaría con el índice único.
        // Ambas fases comparten la misma transacción, así que sigue siendo atómico.
        if (hayBorrados)
        {
            await _context.SaveChangesAsync(ct);
        }

        foreach (var dto in entrada)
        {
            if (dto.Ide > 0 && existentesPorIde.TryGetValue(dto.Ide, out var fila))
            {
                Aplicar(dto, fila);
            }
            else
            {
                var nueva = new calendariopro { company_id = companyId, ano = anio };
                Aplicar(dto, nueva);
                _context.calendariopros.Add(nueva);
            }
        }

        await _context.SaveChangesAsync(ct);
        if (txPropia is not null)
        {
            await txPropia.CommitAsync(ct);
        }

        return await ObtenerAnioAsync(companyId, anio, ct);
    }

    public async Task<CalendarioAnioDto> CopiarAnioAsync(long companyId, int anioOrigen, int anioDestino,
        string usuario, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        ValidarAnio(anioOrigen);
        ValidarAnio(anioDestino);

        if (anioOrigen == anioDestino)
        {
            throw new InvalidOperationException("El año origen y el destino no pueden ser el mismo.");
        }

        // El chequeo barato va primero: si el destino ya tiene calendario no
        // vale la pena materializar las ~252 filas del origen.
        var destinoExiste = await _context.calendariopros
            .AnyAsync(c => c.company_id == companyId && c.ano == anioDestino, ct);
        if (destinoExiste)
        {
            throw new InvalidOperationException(
                $"El año {anioDestino} ya tiene calendario; elimine o edite sus filas en lugar de copiar encima.");
        }

        var origen = await _context.calendariopros
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.ano == anioOrigen)
            .ToListAsync(ct);

        if (origen.Count == 0)
        {
            throw new InvalidOperationException($"El año {anioOrigen} no tiene calendario cargado.");
        }

        await using var txPropia = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;

        foreach (var fila in origen)
        {
            _context.calendariopros.Add(new calendariopro
            {
                company_id = companyId,
                ano = anioDestino,
                mes = fila.mes,
                ciclo = fila.ciclo,
                fechaal = Desplazar(fila.fechaal, anioOrigen, anioDestino),
                fechalec = Desplazar(fila.fechalec, anioOrigen, anioDestino),
                fechafac = Desplazar(fila.fechafac, anioOrigen, anioDestino),
                fecharefac = Desplazar(fila.fecharefac, anioOrigen, anioDestino),
                fechavence = Desplazar(fila.fechavence, anioOrigen, anioDestino),
                diasvence = fila.diasvence,
                fechafac2 = Desplazar(fila.fechafac2, anioOrigen, anioDestino),
                fechavence2 = Desplazar(fila.fechavence2, anioOrigen, anioDestino),
            });
        }

        await _context.SaveChangesAsync(ct);
        if (txPropia is not null)
        {
            await txPropia.CommitAsync(ct);
        }

        return await ObtenerAnioAsync(companyId, anioDestino, ct);
    }

    /// <summary>
    /// Desplaza la fecha tantos años como va del año comercial origen al
    /// destino, conservando día/mes. El arrastre se preserva: la fechavence de
    /// un ciclo de diciembre que cae en enero del año siguiente sigue cayendo
    /// en enero del "destino + 1". 29-feb cae a 28-feb en años no bisiestos.
    /// </summary>
    public static DateOnly? Desplazar(DateOnly? fecha, int anioOrigen, int anioDestino)
    {
        if (fecha is null)
        {
            return null;
        }

        var f = fecha.Value;
        var anioFinal = f.Year + (anioDestino - anioOrigen);
        var dia = Math.Min(f.Day, DateTime.DaysInMonth(anioFinal, f.Month));
        return new DateOnly(anioFinal, f.Month, dia);
    }

    private static CalendarioCicloDto MapDto(calendariopro c) => new()
    {
        Ide = c.ide,
        Anio = c.ano,
        Mes = c.mes,
        Ciclo = c.ciclo.Trim(),
        FechaAl = ADateTime(c.fechaal),
        FechaLectura = ADateTime(c.fechalec),
        FechaFacturacion = ADateTime(c.fechafac),
        FechaRefacturacion = ADateTime(c.fecharefac),
        FechaVencimiento = ADateTime(c.fechavence),
        DiasVencimiento = c.diasvence,
        FechaFacturacion2 = ADateTime(c.fechafac2),
        FechaVencimiento2 = ADateTime(c.fechavence2),
    };

    private static void Aplicar(CalendarioCicloDto dto, calendariopro fila)
    {
        fila.mes = dto.Mes;
        fila.ciclo = dto.Ciclo;
        fila.fechaal = ADateOnly(dto.FechaAl);
        fila.fechalec = ADateOnly(dto.FechaLectura);
        fila.fechafac = ADateOnly(dto.FechaFacturacion);
        fila.fecharefac = ADateOnly(dto.FechaRefacturacion);
        fila.fechavence = ADateOnly(dto.FechaVencimiento);
        fila.diasvence = dto.DiasVencimiento;
        fila.fechafac2 = ADateOnly(dto.FechaFacturacion2);
        fila.fechavence2 = ADateOnly(dto.FechaVencimiento2);
    }

    private static List<CalendarioCicloDto> Normalizar(int anio, List<CalendarioCicloDto> filas)
    {
        return filas.Select(f => new CalendarioCicloDto
        {
            Ide = f.Ide,
            Anio = anio,
            Mes = f.Mes,
            Ciclo = CalendarioFacturacionReglas.NormalizarCiclo(f.Ciclo),
            FechaAl = f.FechaAl,
            FechaLectura = f.FechaLectura,
            FechaFacturacion = f.FechaFacturacion,
            FechaRefacturacion = f.FechaRefacturacion,
            FechaVencimiento = f.FechaVencimiento,
            DiasVencimiento = f.DiasVencimiento,
            FechaFacturacion2 = f.FechaFacturacion2,
            FechaVencimiento2 = f.FechaVencimiento2,
        }).ToList();
    }

    private static DateTime? ADateTime(DateOnly? fecha) =>
        fecha?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? ADateOnly(DateTime? fecha) =>
        fecha is null ? null : DateOnly.FromDateTime(fecha.Value);

    private static void ValidarCompanyId(long companyId)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }
    }

    private static void ValidarAnio(int anio)
    {
        if (anio is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(anio), "El año debe estar entre 2000 y 2100.");
        }
    }
}
