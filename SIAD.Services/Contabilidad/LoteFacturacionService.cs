using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación del lote manual de partidas de facturación (plan 2026-07-02 F3).
/// Preview y generación delegan en la función/SP de BD (el posteo lo hace el
/// motor único); historial y pendientes se leen vía EF.
/// </summary>
public sealed class LoteFacturacionService : ILoteFacturacionService
{
    private static readonly string[] ModosValidos = ["DIA", "PERIODO"];

    private readonly SiadDbContext _context;

    public LoteFacturacionService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LotePreviewLineaDto>> PreviewAsync(long companyId, DateOnly desde, DateOnly hasta,
        string modo, CancellationToken ct = default)
    {
        modo = ValidarParametros(companyId, desde, hasta, modo);

        var conn = _context.Database.GetDbConnection();
        var filas = await conn.QueryAsync<(DateTime fecha_partida, string uso, long account_id, string account_code,
                string account_name, decimal debe, decimal haber, long facturas)>(
            new CommandDefinition(@"
                SELECT fecha_partida, uso, account_id, account_code, account_name, debe, haber, facturas
                FROM public.fn_con_preview_partidas_facturacion(@companyId, @desde, @hasta, @modo)",
                new { companyId, desde, hasta, modo },
                cancellationToken: ct));

        return filas
            .Select(f => new LotePreviewLineaDto
            {
                FechaPartida = DateOnly.FromDateTime(f.fecha_partida),
                Uso = f.uso,
                AccountId = f.account_id,
                AccountCode = f.account_code,
                AccountName = f.account_name,
                Debe = f.debe,
                Haber = f.haber,
                Facturas = f.facturas
            })
            .ToList();
    }

    public async Task<LoteGenerarResultDto> GenerarAsync(long companyId, DateOnly desde, DateOnly hasta,
        string modo, string usuario, CancellationToken ct = default)
    {
        modo = ValidarParametros(companyId, desde, hasta, modo);
        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

        var conn = _context.Database.GetDbConnection();
        var resultado = await conn.QueryFirstAsync<(long? lote_id, int polizas, int facturas, int encoladas, decimal total)>(
            new CommandDefinition(@"
                SELECT lote_id, polizas, facturas, encoladas, total
                FROM public.sp_con_generar_partidas_facturacion(@companyId, @desde, @hasta, @modo, @usuario)",
                new { companyId, desde, hasta, modo, usuario },
                cancellationToken: ct));

        return new LoteGenerarResultDto
        {
            LoteId = resultado.lote_id,
            Polizas = resultado.polizas,
            Facturas = resultado.facturas,
            Encoladas = resultado.encoladas,
            Total = resultado.total
        };
    }

    public async Task<IReadOnlyList<LoteFacturacionDto>> HistorialAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        return await _context.con_lote_facturacions
            .AsNoTracking()
            .Where(l => l.company_id == companyId)
            .OrderByDescending(l => l.created_at)
            .Take(50)
            .Select(l => new LoteFacturacionDto
            {
                LoteId = l.lote_id,
                FechaDesde = l.fecha_desde,
                FechaHasta = l.fecha_hasta,
                ModoAgrupacion = l.modo_agrupacion,
                Facturas = l.facturas,
                Polizas = l.polizas,
                Encoladas = l.encoladas,
                Total = l.total,
                StatusId = l.status_id,
                CreatedAt = l.created_at,
                CreatedBy = l.created_by
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PartidaPendienteDto>> PendientesAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var filas = await _context.con_partida_pendientes
            .AsNoTracking()
            .Where(p => p.company_id == companyId
                && p.module == IntegracionContableModulos.Ventas
                // Solo pendientes del lote: las de comprobantes (REC/MIS/ABO, F4)
                // tienen otro payload y se reprocesan con sp_con_procesar_partida_pendiente.
                && p.origen_tipo == "LOTE_FACTURACION"
                && p.status_id == 1)
            .OrderBy(p => p.fecha_documento)
            .ThenBy(p => p.partida_pendiente_id)
            .Select(p => new
            {
                p.partida_pendiente_id,
                p.fecha_documento,
                p.descripcion,
                p.motivo,
                p.status_id,
                p.intentos,
                p.created_at,
                p.payload
            })
            .ToListAsync(ct);

        return filas
            .Select(p =>
            {
                var dto = new PartidaPendienteDto
                {
                    PartidaPendienteId = p.partida_pendiente_id,
                    FechaDocumento = p.fecha_documento,
                    Descripcion = p.descripcion,
                    Motivo = p.motivo,
                    StatusId = p.status_id,
                    Intentos = p.intentos,
                    CreatedAt = p.created_at
                };

                // El payload guarda el rango y modo originales del lote (F1:
                // datos para regenerar); el reproceso debe usar el rango
                // completo, no solo fecha_documento.
                try
                {
                    using var json = System.Text.Json.JsonDocument.Parse(p.payload);
                    var root = json.RootElement;
                    if (root.TryGetProperty("fecha_desde", out var desde)
                        && DateOnly.TryParse(desde.GetString(), out var fechaDesde))
                    {
                        dto.FechaDesde = fechaDesde;
                    }
                    if (root.TryGetProperty("fecha_hasta", out var hasta)
                        && DateOnly.TryParse(hasta.GetString(), out var fechaHasta))
                    {
                        dto.FechaHasta = fechaHasta;
                    }
                    if (root.TryGetProperty("modo_agrupacion", out var modo))
                    {
                        dto.ModoAgrupacion = modo.GetString();
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // payload ilegible: la pantalla cae a fecha_documento
                }

                return dto;
            })
            .ToList();
    }

    private static string ValidarParametros(long companyId, DateOnly desde, DateOnly hasta, string modo)
    {
        ValidarCompanyId(companyId);

        if (desde > hasta)
        {
            throw new ArgumentException("La fecha inicial no puede ser mayor que la fecha final.", nameof(desde));
        }

        modo = (modo ?? string.Empty).Trim().ToUpperInvariant();
        if (!ModosValidos.Contains(modo))
        {
            throw new ArgumentException("Modo de agrupación no soportado: use DIA o PERIODO.", nameof(modo));
        }

        return modo;
    }

    private static void ValidarCompanyId(long companyId)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }
    }
}
