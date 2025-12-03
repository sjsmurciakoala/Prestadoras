using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.AuxiliarLectura;
using SIAD.Core.Entities;
using SIAD.Data;
using System.Linq;

namespace SIAD.Services.AuxiliarLectura;

public class AuxiliarLecturaService : IAuxiliarLecturaService
{
    private readonly SiadDbContext _context;

    public AuxiliarLecturaService(SiadDbContext context) => _context = context;

    public async Task<AuxiliarLecturaPeriodoDto?> GetPeriodoActualAsync(CancellationToken ct = default)
    {
        var periodo = await _context.historialmes
            .AsNoTracking()
            .Where(p => p.cerrarperiodo == 'P')
            .OrderByDescending(p => p.ano)
            .ThenByDescending(p => p.mes)
            .FirstOrDefaultAsync(ct);

        return periodo is null
            ? null
            : new AuxiliarLecturaPeriodoDto(
                (int)periodo.ano,
                (int)periodo.mes,
                periodo.ciclo,
                periodo.cerrado == 'A',
                periodo.fechacierre?.ToDateTime(TimeOnly.MinValue));
    }

    public async Task<IReadOnlyList<AuxiliarLecturaDto>> SearchAsync(
        AuxiliarLecturaFilterDto filtro,
        CancellationToken ct = default)
    {
        var query = _context.historicomedicions.AsNoTracking();

        if (filtro.Anio.HasValue)
            query = query.Where(h => h.ano == filtro.Anio.Value);
        if (filtro.Mes.HasValue)
            query = query.Where(h => h.mes == filtro.Mes.Value);
        if (!string.IsNullOrWhiteSpace(filtro.Ciclo))
            query = query.Where(h => h.ciclo == filtro.Ciclo);
        if (filtro.SoloPendientes == true)
            query = query.Where(h => string.IsNullOrEmpty(h.usuario));

        query = query
            .OrderByDescending(h => h.fecha)
            .ThenBy(h => h.clave);

        if (filtro.Skip.HasValue)
            query = query.Skip(filtro.Skip.Value);
        if (filtro.Take.HasValue)
            query = query.Take(filtro.Take.Value);

        var lecturas = await query
            .Select(h => new AuxiliarLecturaDto(
                h.clave ?? string.Empty,
                h.propietario ?? string.Empty,
                h.ruta,
                h.contador,
                h.lect_act,
                h.lect_ant,
                h.consumo,
                h.condicion,
                h.fecha.HasValue ? h.fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                h.usuario))
            .ToListAsync(ct);

        return lecturas;
    }

    public async Task<bool> GenerarPeriodoAsync(int anio, int mes, string usuario, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        // cerrar el periodo abierto si existe
        var abierto = await _context.historialmes
            .Where(p => p.cerrarperiodo == 'P')
            .FirstOrDefaultAsync(ct);

        if (abierto is not null)
        {
            abierto.cerrarperiodo = 'C';
            abierto.cerrado = 'C';
            abierto.usuariocierre = usuario;
            _context.historialmes.Update(abierto);
        }

        // evitar duplicados
        var existente = await _context.historialmes
            .AnyAsync(p => p.ano == anio && p.mes == mes, ct);

        if (existente)
            return false;

        // crear periodo
        var periodo = new historialme
        {
            ano = anio,
            mes = mes,
            ciclo = abierto?.ciclo ?? "01",
            fecha = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            usuarioapertura = usuario,
            cerrado = 'A',
            cerrarperiodo = 'P',
            fechacierre = DateOnly.FromDateTime(new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes)))
        };

        _context.historialmes.Add(periodo);

        // obtener lecturas del mes anterior
        var (anioPrev, mesPrev) = mes == 1 ? (anio - 1, 12) : (anio, mes - 1);

        var lecturasPrevias = await _context.historicomedicions
            .Where(h => h.ano == anioPrev && h.mes == mesPrev)
            .ToListAsync(ct);

        foreach (var lectura in lecturasPrevias)
        {
            var clone = new historicomedicion
            {
                ano = anio,
                mes = mes,
                contador = lectura.contador,
                ciclo = lectura.ciclo,
                ruta = lectura.ruta,
                secuencia = lectura.secuencia,
                clave = lectura.clave,
                fecha = DateOnly.FromDateTime(DateTime.UtcNow),
                usuario = null,
                lect_ant = lectura.lect_act,
                lect_act = null,
                fecha_lect_ant = lectura.fecha_lect_act,
                fecha_lect_act = null,
                consumo = 0,
                consumoant = lectura.consumo,
                condicion = lectura.condicion,
                observacion = null
            };
            _context.historicomedicions.Add(clone);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<bool> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var pendientes = await _context.historicomedicions
            .Where(h => h.ano == anio && h.mes == mes && string.IsNullOrEmpty(h.usuario))
            .AnyAsync(ct);

        if (pendientes)
            return false;

        var periodo = await _context.historialmes
            .Where(p => p.ano == anio && p.mes == mes)
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
            return false;

        periodo.cerrarperiodo = 'C';
        periodo.cerrado = 'C';
        periodo.usuariocierre = "api";
        periodo.fechacierre = DateOnly.FromDateTime(DateTime.UtcNow);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EliminarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var lecturas = await _context.historicomedicions
            .Where(h => h.ano == anio && h.mes == mes)
            .ToListAsync(ct);

        if (lecturas.Any(h => !string.IsNullOrEmpty(h.usuario)))
            return false;

        var periodo = await _context.historialmes
            .Where(p => p.ano == anio && p.mes == mes)
            .FirstOrDefaultAsync(ct);

        if (periodo is not null)
            _context.historialmes.Remove(periodo);

        if (lecturas.Count > 0)
            _context.historicomedicions.RemoveRange(lecturas);

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task RegistrarLecturasMasivasAsync(LecturaMasivaDto payload, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        foreach (var item in payload.Lecturas)
        {
            var query = _context.historicomedicions
                .Where(h => h.ano == payload.Anio && h.mes == payload.Mes && h.clave == item.Clave);

            if (!string.IsNullOrWhiteSpace(payload.Ciclo))
                query = query.Where(h => h.ciclo == payload.Ciclo);

            var lectura = await query.FirstOrDefaultAsync(ct);

            if (lectura is null)
                continue;

            lectura.lect_ant = item.LecturaAnterior;
            lectura.lect_act = item.LecturaActual;
            lectura.consumo = item.LecturaActual - item.LecturaAnterior;
            lectura.usuario = string.IsNullOrWhiteSpace(item.Usuario) ? lectura.usuario : item.Usuario;
            lectura.fecha_lect_act = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!string.IsNullOrWhiteSpace(payload.Ciclo))
                lectura.ciclo = payload.Ciclo;
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
