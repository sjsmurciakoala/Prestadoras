using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.AuxiliarLectura;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace SIAD.Services.AuxiliarLectura;

public class AuxiliarLecturaService : IAuxiliarLecturaService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public AuxiliarLecturaService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<AuxiliarLecturaPeriodoDto?> GetPeriodoActualAsync(CancellationToken ct = default)
    {
        // F7: el período comercial vive en adm_periodo_comercial(_ciclo);
        // historialmes queda como espejo de solo lectura para el WS.
        var periodo = await _context.adm_periodo_comercials
            .AsNoTracking()
            .Where(p => p.status_id == EstadoPeriodoComercial.Abierto)
            .OrderByDescending(p => p.anio)
            .ThenByDescending(p => p.mes)
            .Select(p => new
            {
                p.anio,
                p.mes,
                Ciclo = p.ciclos
                    .Where(c => c.status_id == EstadoPeriodoComercial.Abierto)
                    .OrderByDescending(c => c.fecha_apertura)
                    .Select(c => new { c.ciclo_codigo, c.fecha_limite })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        return periodo is null
            ? null
            : new AuxiliarLecturaPeriodoDto(
                periodo.anio,
                periodo.mes,
                periodo.Ciclo?.ciclo_codigo,
                true,
                periodo.Ciclo?.fecha_limite?.ToDateTime(TimeOnly.MinValue));
    }

    public async Task<IReadOnlyList<AuxiliarLecturaDto>> SearchAsync(
        AuxiliarLecturaFilterDto filtro,
        CancellationToken ct = default)
    {
        var baseQuery = ApplyFilters(_context.historicomedicions.AsNoTracking(), filtro);
        var query = ApplySorting(baseQuery, filtro.SortField, filtro.SortDesc == true);

        if (filtro.Skip.HasValue)
            query = query.Skip(filtro.Skip.Value);
        if (filtro.Take.HasValue)
            query = query.Take(filtro.Take.Value);

        var rows = await query
            .Select(h => new RowProjection
            {
                Clave = h.clave,
                Propietario = h.propietario,
                Ruta = h.ruta,
                Contador = h.contador,
                LecturaActual = h.lect_act,
                LecturaAnterior = h.lect_ant,
                Consumo = h.consumo,
                Condicion = h.condicion,
                Fecha = h.fecha,
                Usuario = h.usuario,
                Secuencia = h.secuencia
            })
            .ToListAsync(ct);

        return await MapRowsAsync(rows, ct);
    }

    public async Task<AuxiliarLecturaPagedResponseDto> SearchPagedAsync(
        AuxiliarLecturaFilterDto filtro,
        CancellationToken ct = default)
    {
        var baseQuery = ApplyFilters(_context.historicomedicions.AsNoTracking(), filtro);
        var total = await baseQuery.CountAsync(ct);

        var query = ApplySorting(baseQuery, filtro.SortField, filtro.SortDesc == true);

        if (filtro.Skip.HasValue)
            query = query.Skip(filtro.Skip.Value);
        if (filtro.Take.HasValue)
            query = query.Take(filtro.Take.Value);

        var rows = await query
            .Select(h => new RowProjection
            {
                Clave = h.clave,
                Propietario = h.propietario,
                Ruta = h.ruta,
                Contador = h.contador,
                LecturaActual = h.lect_act,
                LecturaAnterior = h.lect_ant,
                Consumo = h.consumo,
                Condicion = h.condicion,
                Fecha = h.fecha,
                Usuario = h.usuario,
                Secuencia = h.secuencia
            })
            .ToListAsync(ct);

        var items = await MapRowsAsync(rows, ct);
        return new AuxiliarLecturaPagedResponseDto(total, items);
    }

    private static IQueryable<historicomedicion> ApplyFilters(
        IQueryable<historicomedicion> query,
        AuxiliarLecturaFilterDto filtro)
    {
        if (filtro.Anio.HasValue)
            query = query.Where(h => h.ano == filtro.Anio.Value);
        if (filtro.Mes.HasValue)
            query = query.Where(h => h.mes == filtro.Mes.Value);

        var ciclo = filtro.Ciclo?.Trim();
        if (!string.IsNullOrWhiteSpace(ciclo))
            query = query.Where(h => h.ciclo == ciclo);
        if (filtro.SoloPendientes == true)
            query = query.Where(h => string.IsNullOrEmpty(h.usuario));

        return query;
    }

    private static IQueryable<historicomedicion> ApplySorting(
        IQueryable<historicomedicion> query,
        string? sortField,
        bool descending)
    {
        if (string.IsNullOrWhiteSpace(sortField))
            return query.OrderByDescending(h => h.fecha).ThenBy(h => h.clave);

        return (sortField, descending) switch
        {
            (nameof(AuxiliarLecturaDto.Ruta), false) => query.OrderBy(h => h.ruta).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Ruta), true) => query.OrderByDescending(h => h.ruta).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.Clave), false) => query.OrderBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Clave), true) => query.OrderByDescending(h => h.clave),

            (nameof(AuxiliarLecturaDto.Cliente), false) => query.OrderBy(h => h.propietario).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Cliente), true) => query.OrderByDescending(h => h.propietario).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.Secuencia), false) => query.OrderBy(h => h.secuencia).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Secuencia), true) => query.OrderByDescending(h => h.secuencia).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.Contador), false) => query.OrderBy(h => h.contador).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Contador), true) => query.OrderByDescending(h => h.contador).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.LecturaAnterior), false) => query.OrderBy(h => h.lect_ant).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.LecturaAnterior), true) => query.OrderByDescending(h => h.lect_ant).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.LecturaActual), false) => query.OrderBy(h => h.lect_act).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.LecturaActual), true) => query.OrderByDescending(h => h.lect_act).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.Consumo), false) => query.OrderBy(h => h.consumo).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Consumo), true) => query.OrderByDescending(h => h.consumo).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.FechaLectura), false) => query.OrderBy(h => h.fecha).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.FechaLectura), true) => query.OrderByDescending(h => h.fecha).ThenBy(h => h.clave),

            (nameof(AuxiliarLecturaDto.Usuario), false) => query.OrderBy(h => h.usuario).ThenBy(h => h.clave),
            (nameof(AuxiliarLecturaDto.Usuario), true) => query.OrderByDescending(h => h.usuario).ThenBy(h => h.clave),

            _ => query.OrderByDescending(h => h.fecha).ThenBy(h => h.clave)
        };
    }

    private async Task<List<AuxiliarLecturaDto>> MapRowsAsync(IEnumerable<RowProjection> rows, CancellationToken ct)
    {
        var materializedRows = rows.ToList();
        var claves = materializedRows
            .Where(h => !string.IsNullOrWhiteSpace(h.Clave) && string.IsNullOrWhiteSpace(h.Propietario))
            .Select(h => h.Clave!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var clientesPorClave = claves.Length == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await _context.cliente_maestros
                .AsNoTracking()
                .Where(c => claves.Contains(c.maestro_cliente_clave))
                .Select(c => new
                {
                    c.maestro_cliente_clave,
                    c.maestro_cliente_nombre
                })
                .ToDictionaryAsync(
                    c => c.maestro_cliente_clave,
                    c => c.maestro_cliente_nombre,
                    StringComparer.OrdinalIgnoreCase,
                    ct);

        return materializedRows
            .Select(h => new AuxiliarLecturaDto(
                CleanRequired(h.Clave),
                ResolveCliente(h, clientesPorClave),
                CleanOptional(h.Ruta),
                CleanOptional(h.Contador),
                h.LecturaActual,
                h.LecturaAnterior,
                h.Consumo,
                CleanOptional(h.Condicion),
                h.Fecha.HasValue ? h.Fecha.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                CleanOptional(h.Usuario),
                CleanOptional(h.Secuencia)))
            .ToList();
    }

    private static string ResolveCliente(RowProjection row, IReadOnlyDictionary<string, string> clientesPorClave)
    {
        var propietario = CleanRequired(row.Propietario);
        if (!string.IsNullOrWhiteSpace(propietario))
        {
            return propietario;
        }

        var clave = CleanRequired(row.Clave);
        if (!string.IsNullOrWhiteSpace(clave) && clientesPorClave.TryGetValue(clave, out var cliente))
        {
            return CleanRequired(cliente);
        }

        return string.Empty;
    }

    private sealed class RowProjection
    {
        public string? Clave { get; set; }
        public string? Propietario { get; set; }
        public string? Ruta { get; set; }
        public string? Contador { get; set; }
        public decimal? LecturaActual { get; set; }
        public decimal? LecturaAnterior { get; set; }
        public decimal? Consumo { get; set; }
        public string? Condicion { get; set; }
        public DateOnly? Fecha { get; set; }
        public string? Usuario { get; set; }
        public string? Secuencia { get; set; }
    }

    private static string CleanRequired(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? CleanOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<bool> GenerarPeriodoAsync(int anio, int mes, string ciclo, string usuario, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var ahora = DateTime.UtcNow;
        var cicloNormalizado = string.IsNullOrWhiteSpace(ciclo)
            ? "01"
            : NormalizeCiclo(ciclo);

        // Flujo legacy conservado: generar un período cierra el ciclo abierto
        // vigente SIN checklist (así operaba historialmes). El cierre formal
        // con validación de rutas contra facturas vive en la pantalla
        // Períodos comerciales (F7).
        var abierto = await _context.adm_periodo_comercial_ciclos
            .Include(c => c.periodo)
            .Where(c => c.status_id == EstadoPeriodoComercial.Abierto)
            .OrderByDescending(c => c.periodo.anio)
            .ThenByDescending(c => c.periodo.mes)
            .FirstOrDefaultAsync(ct);

        if (abierto is not null)
        {
            abierto.status_id = EstadoPeriodoComercial.Cerrado;
            abierto.fecha_cierre = ahora;
            abierto.cerrado_por = usuario;
            abierto.updated_at = ahora;
            abierto.updated_by = usuario;

            var quedanAbiertos = await _context.adm_periodo_comercial_ciclos
                .AnyAsync(c => c.periodo_comercial_id == abierto.periodo_comercial_id
                    && c.periodo_ciclo_id != abierto.periodo_ciclo_id
                    && c.status_id == EstadoPeriodoComercial.Abierto, ct);

            if (!quedanAbiertos)
            {
                abierto.periodo.status_id = EstadoPeriodoComercial.Cerrado;
                abierto.periodo.fecha_cierre = ahora;
                abierto.periodo.cerrado_por = usuario;
                abierto.periodo.updated_at = ahora;
                abierto.periodo.updated_by = usuario;
            }
        }

        // evitar duplicados: el mismo (anio, mes, ciclo) ya fue generado
        var existente = await _context.adm_periodo_comercial_ciclos
            .AnyAsync(c => c.periodo.anio == anio && c.periodo.mes == mes
                && c.ciclo_codigo == cicloNormalizado, ct);

        if (existente)
            return false;

        // crear (o reutilizar) el período del mes y su ciclo
        var periodo = await _context.adm_periodo_comercials
            .FirstOrDefaultAsync(p => p.anio == anio && p.mes == mes, ct);

        if (periodo is null)
        {
            periodo = new adm_periodo_comercial
            {
                anio = anio,
                mes = (short)mes,
                status_id = EstadoPeriodoComercial.Abierto,
                fecha_apertura = ahora,
                abierto_por = usuario,
                created_at = ahora,
                created_by = usuario
            };
            _context.adm_periodo_comercials.Add(periodo);
        }
        else if (periodo.status_id != EstadoPeriodoComercial.Abierto)
        {
            // legacy permitía generar otro ciclo de un mes ya cerrado (la PK de
            // historialmes era ano+mes+ciclo); reabrir el período conserva esa
            // capacidad para el flujo operativo del auxiliar de lectura
            periodo.status_id = EstadoPeriodoComercial.Abierto;
            periodo.fecha_cierre = null;
            periodo.cerrado_por = null;
            periodo.updated_at = ahora;
            periodo.updated_by = usuario;
        }

        _context.adm_periodo_comercial_ciclos.Add(new adm_periodo_comercial_ciclo
        {
            periodo = periodo,
            ciclo_codigo = cicloNormalizado,
            status_id = EstadoPeriodoComercial.Abierto,
            fecha_apertura = ahora,
            abierto_por = usuario,
            fecha_limite = DateOnly.FromDateTime(new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes))),
            created_at = ahora,
            created_by = usuario
        });

        // obtener lecturas del mes anterior
        var (anioPrev, mesPrev) = mes == 1 ? (anio - 1, 12) : (anio, mes - 1);

        var cicloAnteriorAlterno = cicloNormalizado.TrimStart('0');
        var lecturasPrevias = await _context.historicomedicions
            .Where(h => h.ano == anioPrev
                && h.mes == mesPrev
                && (h.ciclo == cicloNormalizado
                    || (!string.IsNullOrWhiteSpace(cicloAnteriorAlterno) && h.ciclo == cicloAnteriorAlterno)))
            .ToListAsync(ct);

        if (lecturasPrevias.Count > 0)
        {
            foreach (var lectura in lecturasPrevias)
            {
                var clone = new historicomedicion
                {
                    company_id = _currentCompanyService.GetCompanyId(),
                    ano = anio,
                    mes = mes,
                    contador = lectura.contador,
                    ciclo = cicloNormalizado,
                    ruta = lectura.ruta,
                    secuencia = lectura.secuencia,
                    clave = lectura.clave,
                    propietario = lectura.propietario,
                    ubicacion = lectura.ubicacion,
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
        }
        else
        {
            var cicloCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                cicloNormalizado,
                cicloNormalizado.TrimStart('0')
            };
            cicloCodes.RemoveWhere(string.IsNullOrWhiteSpace);

            var cicloIds = await _context.ciclos
                .AsNoTracking()
                .Where(c => cicloCodes.Contains(c.ciclos_codigo))
                .Select(c => c.ciclos_id)
                .ToListAsync(ct);

            var clientesQuery = _context.cliente_maestros
                .AsNoTracking()
                .Where(c => c.estado);

            if (cicloIds.Count > 0)
            {
                clientesQuery = clientesQuery.Where(c => c.ciclos_id.HasValue && cicloIds.Contains(c.ciclos_id.Value));
            }
            else if (int.TryParse(cicloNormalizado, out var cicloIdFallback))
            {
                clientesQuery = clientesQuery.Where(c => c.ciclos_id == cicloIdFallback);
            }

            var clientes = await clientesQuery
                .Select(c => new
                {
                    c.maestro_cliente_clave,
                    c.maestro_cliente_nombre,
                    c.maestro_cliente_indicativo_ruta,
                    c.maestro_cliente_secuencia,
                    c.contador,
                    Detalle = c.cliente_detalles
                        .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                        .Select(d => new
                        {
                            d.detalle_cliente_direccion,
                            MedidorNumero = d.maestro_medidor != null ? d.maestro_medidor.maestro_medidor_numero : null
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            var fechaBase = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var cliente in clientes)
            {
                var ruta = ExtraerRuta(cliente.maestro_cliente_indicativo_ruta);
                var contador = !string.IsNullOrWhiteSpace(cliente.Detalle?.MedidorNumero)
                    ? cliente.Detalle!.MedidorNumero
                    : cliente.contador;

                var registro = new historicomedicion
                {
                    company_id = _currentCompanyService.GetCompanyId(),
                    ano = anio,
                    mes = mes,
                    contador = contador,
                    ciclo = cicloNormalizado,
                    ruta = ruta,
                    secuencia = cliente.maestro_cliente_secuencia,
                    clave = cliente.maestro_cliente_clave,
                    propietario = cliente.maestro_cliente_nombre,
                    ubicacion = cliente.Detalle?.detalle_cliente_direccion,
                    fecha = fechaBase,
                    usuario = null,
                    lect_ant = 0,
                    lect_act = null,
                    fecha_lect_ant = null,
                    fecha_lect_act = null,
                    consumo = 0,
                    consumoant = 0,
                    condicion = null,
                    observacion = null
                };
                _context.historicomedicions.Add(registro);
            }
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static string NormalizeCiclo(string ciclo)
    {
        var trimmed = ciclo.Trim();
        if (trimmed.Length == 0)
            return "01";

        if (int.TryParse(trimmed, out var numeric))
            return numeric.ToString("D2");

        if (trimmed.Length <= 2)
            return trimmed;

        var numericMatch = Regex.Match(trimmed, @"(\d{1,2})$");
        if (numericMatch.Success && int.TryParse(numericMatch.Groups[1].Value, out numeric))
            return numeric.ToString("D2");

        throw new ArgumentException("El ciclo debe enviarse como código corto. Ejemplos válidos: 01, 1 o Ciclo1.");
    }

    private static string? ExtraerRuta(string? indicativoRuta)
    {
        if (string.IsNullOrWhiteSpace(indicativoRuta))
        {
            return null;
        }

        var partes = indicativoRuta
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (partes.Length >= 3)
        {
            var ruta = partes[2];
            return string.IsNullOrWhiteSpace(ruta) ? null : ruta;
        }

        return indicativoRuta.Trim();
    }

    public async Task<bool> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default)
    {
        var pendientes = await _context.historicomedicions
            .Where(h => h.ano == anio && h.mes == mes && string.IsNullOrEmpty(h.usuario))
            .AnyAsync(ct);

        if (pendientes)
            return false;

        var periodo = await _context.adm_periodo_comercials
            .Include(p => p.ciclos)
            .Where(p => p.anio == anio && p.mes == mes)
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
            return false;

        var ahora = DateTime.UtcNow;
        foreach (var cicloAbierto in periodo.ciclos.Where(c => c.status_id == EstadoPeriodoComercial.Abierto))
        {
            cicloAbierto.status_id = EstadoPeriodoComercial.Cerrado;
            cicloAbierto.fecha_cierre = ahora;
            cicloAbierto.cerrado_por = "api";
            cicloAbierto.updated_at = ahora;
            cicloAbierto.updated_by = "api";
        }

        periodo.status_id = EstadoPeriodoComercial.Cerrado;
        periodo.fecha_cierre = ahora;
        periodo.cerrado_por = "api";
        periodo.updated_at = ahora;
        periodo.updated_by = "api";
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

        var periodo = await _context.adm_periodo_comercials
            .Include(p => p.ciclos)
            .Where(p => p.anio == anio && p.mes == mes)
            .FirstOrDefaultAsync(ct);

        if (periodo is not null)
        {
            // EF elimina primero los ciclos (dependientes); el trigger espejo
            // borra las filas correspondientes de historialmes
            _context.adm_periodo_comercial_ciclos.RemoveRange(periodo.ciclos);
            _context.adm_periodo_comercials.Remove(periodo);
        }

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
