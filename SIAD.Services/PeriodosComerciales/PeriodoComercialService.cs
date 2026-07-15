using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.PeriodosComerciales;
using SIAD.Data;

namespace SIAD.Services.PeriodosComerciales;

/// <summary>
/// Implementación de períodos comerciales (F7 + Fase B). El listado se lee vía
/// EF (filtro global de tenant); las transiciones de estado delegan en los SPs
/// de BD, que validan checklist y sincronizan el espejo historialmes.
/// </summary>
public sealed class PeriodoComercialService : IPeriodoComercialService
{
    // Los SP de apertura devuelven jsonb con claves snake_case.
    private static readonly JsonSerializerOptions JsonSnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SiadDbContext _context;

    public PeriodoComercialService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PeriodoComercialDto>> ListarAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        return await _context.adm_periodo_comercials
            .AsNoTracking()
            .Where(p => p.company_id == companyId)
            .OrderByDescending(p => p.anio)
            .ThenByDescending(p => p.mes)
            .Take(36)
            .Select(p => new PeriodoComercialDto
            {
                PeriodoComercialId = p.periodo_comercial_id,
                Anio = p.anio,
                Mes = p.mes,
                StatusId = p.status_id,
                FechaApertura = p.fecha_apertura,
                AbiertoPor = p.abierto_por,
                FechaCierre = p.fecha_cierre,
                CerradoPor = p.cerrado_por,
                Ciclos = p.ciclos
                    .OrderBy(c => c.ciclo_codigo)
                    .Select(c => new PeriodoComercialCicloDto
                    {
                        PeriodoCicloId = c.periodo_ciclo_id,
                        CicloCodigo = c.ciclo_codigo,
                        StatusId = c.status_id,
                        FechaApertura = c.fecha_apertura,
                        FechaLimite = c.fecha_limite,
                        FechaCierre = c.fecha_cierre,
                        CerradoPor = c.cerrado_por
                    })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RutaCicloDto>> RutasCicloAsync(long companyId, long periodoCicloId,
        CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var conn = _context.Database.GetDbConnection();
        var filas = await conn.QueryAsync<(string codruta, long clientes_activos, long facturas_mes, bool pendiente)>(
            new CommandDefinition(@"
                SELECT codruta, clientes_activos, facturas_mes, pendiente
                FROM public.fn_adm_periodo_ciclo_rutas_pendientes(@companyId, @periodoCicloId)",
                new { companyId, periodoCicloId },
                cancellationToken: ct));

        return filas
            .Select(f => new RutaCicloDto
            {
                CodRuta = f.codruta,
                ClientesActivos = f.clientes_activos,
                FacturasMes = f.facturas_mes,
                Pendiente = f.pendiente
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ChecklistCierreItemDto>> ChecklistCierreAsync(long companyId,
        long periodoComercialId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var conn = _context.Database.GetDbConnection();
        var filas = await conn.QueryAsync<(string item, bool ok, long cantidad, string detalle)>(
            new CommandDefinition(@"
                SELECT item, ok, cantidad, detalle
                FROM public.fn_adm_periodo_comercial_checklist(@companyId, @periodoComercialId)",
                new { companyId, periodoComercialId },
                cancellationToken: ct));

        return filas
            .Select(f => new ChecklistCierreItemDto
            {
                Item = f.item,
                Ok = f.ok,
                Cantidad = f.cantidad,
                Detalle = f.detalle
            })
            .ToList();
    }

    public async Task<AperturaCicloResumenDto> AbrirAsync(long companyId, int anio, int mes, string? ciclo,
        string usuario, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        usuario = NormalizarUsuario(usuario);

        var conn = _context.Database.GetDbConnection();
        var json = await conn.ExecuteScalarAsync<string>(
            new CommandDefinition(@"
                SELECT public.sp_adm_periodo_ciclo_abrir(@companyId, @anio, @mes, @ciclo, @usuario)::text",
                new { companyId, anio, mes, ciclo, usuario },
                cancellationToken: ct));

        return DeserializarResumen(json);
    }

    public async Task<AperturaCicloResumenDto> PreviewAperturaAsync(long companyId, int anio, int mes,
        string? ciclo, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var conn = _context.Database.GetDbConnection();
        var json = await conn.ExecuteScalarAsync<string>(
            new CommandDefinition(@"
                SELECT public.fn_adm_periodo_ciclo_preview(@companyId, @anio, @mes, @ciclo)::text",
                new { companyId, anio, mes, ciclo },
                cancellationToken: ct));

        return DeserializarResumen(json);
    }

    public async Task<SugerenciaAperturaDto?> SugerenciaAperturaAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var conn = _context.Database.GetDbConnection();
        var fila = await conn.QueryFirstOrDefaultAsync<(int anio, int mes, string ciclo, DateTime? fecha_lectura)?>(
            new CommandDefinition(@"
                SELECT anio, mes, ciclo, fecha_lectura
                FROM public.fn_adm_periodo_ciclo_sugerido(@companyId)",
                new { companyId },
                cancellationToken: ct));

        return fila is null
            ? null
            : new SugerenciaAperturaDto
            {
                Anio = fila.Value.anio,
                Mes = fila.Value.mes,
                Ciclo = fila.Value.ciclo,
                FechaLectura = fila.Value.fecha_lectura is null
                    ? null
                    : DateOnly.FromDateTime(fila.Value.fecha_lectura.Value),
            };
    }

    public async Task<DeshacerAperturaResultadoDto> DeshacerAperturaAsync(long companyId, long periodoCicloId,
        string usuario, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        usuario = NormalizarUsuario(usuario);

        var conn = _context.Database.GetDbConnection();
        var json = await conn.ExecuteScalarAsync<string>(
            new CommandDefinition(@"
                SELECT public.sp_adm_periodo_ciclo_deshacer(@companyId, @periodoCicloId, @usuario)::text",
                new { companyId, periodoCicloId, usuario },
                cancellationToken: ct));

        return string.IsNullOrWhiteSpace(json)
            ? new DeshacerAperturaResultadoDto()
            : JsonSerializer.Deserialize<DeshacerAperturaResultadoDto>(json, JsonSnakeCase)
              ?? new DeshacerAperturaResultadoDto();
    }

    private static AperturaCicloResumenDto DeserializarResumen(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("El SP de apertura no devolvió resumen.");
        }

        return JsonSerializer.Deserialize<AperturaCicloResumenDto>(json, JsonSnakeCase)
               ?? throw new InvalidOperationException("No se pudo interpretar el resumen de apertura.");
    }

    public async Task CerrarCicloAsync(long companyId, long periodoCicloId, string usuario, bool forzar,
        CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        usuario = NormalizarUsuario(usuario);

        var conn = _context.Database.GetDbConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(@"
                SELECT public.sp_adm_periodo_ciclo_cerrar(@companyId, @periodoCicloId, @usuario, @forzar)",
                new { companyId, periodoCicloId, usuario, forzar },
                cancellationToken: ct));
    }

    public async Task CerrarMesAsync(long companyId, long periodoComercialId, string usuario,
        CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        usuario = NormalizarUsuario(usuario);

        var conn = _context.Database.GetDbConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(@"
                SELECT public.sp_adm_periodo_comercial_cerrar(@companyId, @periodoComercialId, @usuario)",
                new { companyId, periodoComercialId, usuario },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AvisoPeriodoDto>> AvisosAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var conn = _context.Database.GetDbConnection();
        var filas = await conn.QueryAsync<(string tipo, string severidad, string mensaje, long cantidad)>(
            new CommandDefinition(@"
                SELECT tipo, severidad, mensaje, cantidad
                FROM public.fn_adm_avisos_periodos(@companyId)",
                new { companyId },
                cancellationToken: ct));

        return filas
            .Select(f => new AvisoPeriodoDto
            {
                Tipo = f.tipo,
                Severidad = f.severidad,
                Mensaje = f.mensaje,
                Cantidad = f.cantidad
            })
            .ToList();
    }

    private static string NormalizarUsuario(string usuario) =>
        string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

    private static void ValidarCompanyId(long companyId)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }
    }
}
