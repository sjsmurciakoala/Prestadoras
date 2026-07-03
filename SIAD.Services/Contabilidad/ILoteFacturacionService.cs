using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Lote manual de partidas de facturación (plan 2026-07-02, Fase 3).
/// El posteo real lo hace sp_con_generar_partidas_facturacion vía el motor
/// único (sp_con_postear_poliza); esta capa solo orquesta preview, generación,
/// historial y cola de regularización.
/// </summary>
public interface ILoteFacturacionService
{
    /// <summary>
    /// Preview agregado del lote (fn_con_preview_partidas_facturacion): líneas
    /// por fecha de partida × uso × cuenta, sin escribir nada.
    /// </summary>
    Task<IReadOnlyList<LotePreviewLineaDto>> PreviewAsync(long companyId, DateOnly desde, DateOnly hasta,
        string modo, CancellationToken ct = default);

    /// <summary>
    /// Genera y postea el lote (sp_con_generar_partidas_facturacion). Idempotente:
    /// las facturas ya marcadas en con_partida_factura se excluyen. LoteId null
    /// significa que no había facturas pendientes en el rango.
    /// </summary>
    Task<LoteGenerarResultDto> GenerarAsync(long companyId, DateOnly desde, DateOnly hasta,
        string modo, string usuario, CancellationToken ct = default);

    /// <summary>Últimos 50 lotes generados de la empresa (con_lote_facturacion).</summary>
    Task<IReadOnlyList<LoteFacturacionDto>> HistorialAsync(long companyId, CancellationToken ct = default);

    /// <summary>Pendientes de regularización del módulo VENTAS (estado 1=PENDIENTE).</summary>
    Task<IReadOnlyList<PartidaPendienteDto>> PendientesAsync(long companyId, CancellationToken ct = default);
}
