using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Cobranza;

public class CorteMasivoService : ICorteMasivoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public CorteMasivoService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<CorteMasivoHdrDto> GenerarAsync(
        GenerarCorteMasivoRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        var candidatos = await ObtenerCandidatosAsync(companyId, request, ct);

        var correlativo = await GenerarCorrelativoAsync(ct);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var hoy = DateOnly.FromDateTime(ahora);

        var hdr = new cln_corte_masivo_hdr
        {
            company_id       = companyId,
            correlativo      = correlativo,
            fecha_generacion = hoy,
            criterio         = GenerarDescripcionCriterio(request),
            periodo_anio     = request.PeriodoAnio,
            periodo_mes      = request.PeriodoMes,
            ciclo_id         = request.CicloId,
            barrio_codigo    = request.BarrioCodigo,
            valor_minimo     = request.ValorMinimo > 0 ? request.ValorMinimo : null,
            categoria_id     = request.CategoriaId,
            dias_corte       = request.DiasCorte,
            total_clientes   = candidatos.Count,
            estado           = "GENERADO",
            usuario          = usuario,
            usuariocreacion  = usuario,
            fechacreacion    = ahora
        };
        // Todo el lote (header + detalles + órdenes de trabajo) se persiste de forma
        // atómica: si algo falla, el `await using` revierte la transacción y no queda
        // un corte a medias.
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        _context.cln_corte_masivo_hdrs.Add(hdr);
        await _context.SaveChangesAsync(ct);

        var detalles = candidatos.Select(c => new cln_corte_masivo_dtl
        {
            hdr_id         = hdr.id,
            company_id     = companyId,
            cliente_clave  = c.ClienteClave,
            nombre_cliente = c.NombreCliente,
            saldo_adeudado = c.SaldoAdeudado,
            dias_sin_pago  = c.DiasSinPago,
            pagado         = false
        }).ToList();

        _context.cln_corte_masivo_dtls.AddRange(detalles);
        await _context.SaveChangesAsync(ct);

        // orden_trabajo no tiene índice único en orden_numero y el número se asigna como
        // MAX()+1; este advisory lock transaccional serializa la asignación frente a otra
        // generación concurrente para evitar números duplicados. Se libera al cerrar la tx.
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(833301)", ct);

        var maxNumero = await _context.orden_trabajos
            .AsNoTracking()
            .Select(o => (int?)o.orden_numero)
            .MaxAsync(ct) ?? 50000;

        var ahora2 = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var hoy2   = DateOnly.FromDateTime(ahora2);
        var fechaCorte = hoy2.AddDays(request.DiasCorte);
        int siguiente = maxNumero + 1;

        // Inserta todas las órdenes en un solo lote (EF rellena orden_id) y luego vincula
        // cada detalle con su OT, en vez de un SaveChanges por cliente (evita N round-trips).
        var ordenes = new List<orden_trabajo>(detalles.Count);
        foreach (var dtl in detalles)
        {
            var ot = new orden_trabajo
            {
                orden_numero           = siguiente++,
                maestro_cliente_clave  = dtl.cliente_clave,
                concepto               = $"ORDEN DE CORTE - Lote {hdr.correlativo}",
                estado                 = "P",
                tipo                   = "33",
                fecha                  = fechaCorte,
                fecha_creacion         = ahora2,
                usuario                = usuario,
                ano                    = request.PeriodoAnio,
                mes                    = request.PeriodoMes,
                saldo                  = dtl.saldo_adeudado
            };
            ordenes.Add(ot);
            _context.orden_trabajos.Add(ot);
        }

        await _context.SaveChangesAsync(ct);

        for (int i = 0; i < detalles.Count; i++)
            detalles[i].orden_id = ordenes[i].orden_id;

        await _context.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return ToHdrDto(hdr);
    }

    public async Task<int> CancelarOrdenesCorteClienteAsync(
        string clienteClave, string usuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
            return 0;

        var clave = clienteClave.Trim();

        // Detalles de corte aún vigentes (sin marcar como pagados) del cliente. El filtro
        // global de company aplica porque cln_corte_masivo_dtl es tenant-scoped.
        var detalles = await _context.cln_corte_masivo_dtls
            .Where(d => d.cliente_clave == clave && !d.pagado)
            .ToListAsync(ct);

        if (detalles.Count == 0)
            return 0;

        var ordenIds = detalles
            .Where(d => d.orden_id.HasValue)
            .Select(d => d.orden_id!.Value)
            .Distinct()
            .ToList();

        // Cancela solo las OT de corte (tipo 33) que sigan pendientes. Se llega a ellas
        // únicamente a través de los detalles de corte de esta empresa, así que es
        // tenant-safe aunque orden_trabajo no esté scopeada por company.
        var ordenesCanceladas = 0;
        if (ordenIds.Count > 0)
        {
            var ordenes = await _context.orden_trabajos
                .Where(o => ordenIds.Contains(o.orden_id) && o.tipo == "33" && o.estado == "P")
                .ToListAsync(ct);

            foreach (var ot in ordenes)
            {
                ot.estado = "C"; // Cancelada
                ot.usuario = usuario;
            }

            ordenesCanceladas = ordenes.Count;
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        foreach (var dtl in detalles)
        {
            dtl.pagado = true;
            dtl.fecha_pago = hoy;
        }

        await _context.SaveChangesAsync(ct);
        return ordenesCanceladas;
    }

    public async Task<IReadOnlyList<CorteMasivoHdrDto>> ListarAsync(CancellationToken ct = default)
    {
        return await _context.cln_corte_masivo_hdrs
            .AsNoTracking()
            .OrderByDescending(h => h.fecha_generacion)
            .ThenByDescending(h => h.id)
            .Select(h => new CorteMasivoHdrDto(
                h.id, h.correlativo, h.fecha_generacion,
                h.criterio, h.periodo_anio, h.periodo_mes,
                h.total_clientes, h.estado))
            .ToListAsync(ct);
    }

    public async Task<CorteMasivoDetalleDto?> ObtenerDetalleAsync(int hdrId, CancellationToken ct = default)
        => await ObtenerInternoAsync(hdrId, soloSinPago: false, ct);

    public async Task<CorteMasivoDetalleDto?> ObtenerParaReimpresionAsync(int hdrId, CancellationToken ct = default)
        => await ObtenerInternoAsync(hdrId, soloSinPago: true, ct);

    // ── Privados ──────────────────────────────────────────────────────────────

    private async Task<CorteMasivoDetalleDto?> ObtenerInternoAsync(
        int hdrId, bool soloSinPago, CancellationToken ct)
    {
        var hdr = await _context.cln_corte_masivo_hdrs
            .AsNoTracking()
            .Where(h => h.id == hdrId)
            .Select(h => new CorteMasivoHdrDto(
                h.id, h.correlativo, h.fecha_generacion,
                h.criterio, h.periodo_anio, h.periodo_mes,
                h.total_clientes, h.estado))
            .FirstOrDefaultAsync(ct);

        if (hdr is null) return null;

        var query = _context.cln_corte_masivo_dtls
            .AsNoTracking()
            .Where(d => d.hdr_id == hdrId);

        if (soloSinPago)
            query = query.Where(d => !d.pagado);

        var clientes = await query
            .OrderBy(d => d.nombre_cliente)
            .Select(d => new CorteMasivoDtlDto(
                d.cliente_clave, d.nombre_cliente,
                d.saldo_adeudado, d.dias_sin_pago, d.pagado,
                d.orden_id,
                _context.orden_trabajos
                    .Where(o => o.orden_id == d.orden_id)
                    .Select(o => (int?)o.orden_numero)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new CorteMasivoDetalleDto(hdr, clientes);
    }

    private async Task<List<CandidatoCorteRow>> ObtenerCandidatosAsync(
        long companyId, GenerarCorteMasivoRequest request, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        var sb = new StringBuilder("""
            SELECT
                cm.maestro_cliente_clave  AS ClienteClave,
                cm.maestro_cliente_nombre AS NombreCliente,
                COALESCE(ta_s.saldo, 0)   AS SaldoAdeudado,
                CASE
                    WHEN ta_p.ultima_pago IS NULL THEN NULL
                    ELSE CURRENT_DATE - ta_p.ultima_pago
                END AS DiasSinPago
            FROM cliente_maestro cm
            LEFT JOIN LATERAL (
                -- Saldo = suma de movimientos vigentes; la vista absorbe la convencion
                -- invertida de estados (facturas vigentes 'A'; abonos vigentes 'C').
                SELECT SUM(COALESCE(ta.debitos, 0) - COALESCE(ta.creditos, 0)) AS saldo
                FROM public.vw_transaccion_abonado_vigente ta
                WHERE ta.company_id    = cm.company_id
                  AND ta.cliente_clave = cm.maestro_cliente_clave
            ) ta_s ON TRUE
            LEFT JOIN LATERAL (
                SELECT MAX(ta.fecha_docu) AS ultima_pago
                FROM transaccion_abonado ta
                WHERE ta.company_id    = cm.company_id
                  AND ta.cliente_clave = cm.maestro_cliente_clave
                  AND (ta.tipotransaccion ILIKE '%PAGO%'
                       OR (ta.tipotransaccion IN ('201', '202') AND ta.estado = 'C'))
            ) ta_p ON TRUE
            WHERE cm.company_id = @CompanyId
              AND cm.estado = TRUE
              AND COALESCE(cm.no_cortable, FALSE) = FALSE
              AND COALESCE(cm.bloqueado_cobranza, FALSE) = FALSE
              AND COALESCE(ta_s.saldo, 0) > 0
            """);

        if (request.ValorMinimo > 0)
            sb.Append(" AND COALESCE(ta_s.saldo, 0) >= @ValorMinimo");

        if (request.CicloId.HasValue)
            sb.Append(" AND cm.ciclos_id = @CicloId");

        if (!string.IsNullOrWhiteSpace(request.BarrioCodigo))
            sb.Append(" AND cm.barrio_codigo = @BarrioCodigo");

        if (request.CategoriaId.HasValue)
            sb.Append(" AND cm.categoria_servicio_id = @CategoriaId");

        var rows = await connection.QueryAsync<CandidatoCorteRow>(
            new CommandDefinition(sb.ToString(),
                new
                {
                    CompanyId    = companyId,
                    ValorMinimo  = request.ValorMinimo,
                    CicloId      = request.CicloId,
                    BarrioCodigo = request.BarrioCodigo,
                    CategoriaId  = request.CategoriaId
                },
                cancellationToken: ct));

        return rows.ToList();
    }

    private async Task<string> GenerarCorrelativoAsync(CancellationToken ct)
    {
        var ultimo = await _context.cln_corte_masivo_hdrs
            .AsNoTracking()
            .Where(h => h.correlativo != null)
            .OrderByDescending(h => h.correlativo)
            .Select(h => h.correlativo)
            .FirstOrDefaultAsync(ct);

        if (int.TryParse(ultimo, out var numero))
            return (numero + 1).ToString("D6");

        return 1.ToString("D6");
    }

    private static string GenerarDescripcionCriterio(GenerarCorteMasivoRequest req)
    {
        var desc = $"{req.PeriodoMes:D2}/{req.PeriodoAnio}";
        if (req.CicloId.HasValue)                            desc += $" C{req.CicloId}";
        if (!string.IsNullOrEmpty(req.BarrioCodigo))         desc += $" B{req.BarrioCodigo}";
        if (req.CategoriaId.HasValue)                        desc += $" CAT{req.CategoriaId}";
        if (req.ValorMinimo > 0)                             desc += $" V{req.ValorMinimo:N0}";
        return desc.Length > 30 ? desc[..30] : desc;
    }

    private static CorteMasivoHdrDto ToHdrDto(cln_corte_masivo_hdr h)
        => new(h.id, h.correlativo, h.fecha_generacion, h.criterio,
               h.periodo_anio, h.periodo_mes, h.total_clientes, h.estado);

    private sealed class CandidatoCorteRow
    {
        public string ClienteClave { get; init; } = string.Empty;
        public string? NombreCliente { get; init; }
        public decimal SaldoAdeudado { get; init; }
        public int? DiasSinPago { get; init; }
    }
}
