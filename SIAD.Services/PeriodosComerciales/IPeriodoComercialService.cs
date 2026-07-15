using SIAD.Core.DTOs.PeriodosComerciales;

namespace SIAD.Services.PeriodosComerciales;

/// <summary>
/// Períodos comerciales (F7 + Fase B apertura-ciclo-único): listado, apertura
/// integral y cierres con checklist. Las transiciones de estado viven en SPs
/// de BD (sp_adm_periodo_ciclo_abrir/deshacer, sp_adm_periodo_comercial_cerrar,
/// sp_adm_periodo_ciclo_cerrar).
/// </summary>
public interface IPeriodoComercialService
{
    Task<IReadOnlyList<PeriodoComercialDto>> ListarAsync(long companyId, CancellationToken ct = default);

    /// <summary>Rutas del ciclo con su avance de facturación del mes.</summary>
    Task<IReadOnlyList<RutaCicloDto>> RutasCicloAsync(long companyId, long periodoCicloId, CancellationToken ct = default);

    /// <summary>Checklist del cierre del mes comercial.</summary>
    Task<IReadOnlyList<ChecklistCierreItemDto>> ChecklistCierreAsync(long companyId, long periodoComercialId, CancellationToken ct = default);

    /// <summary>
    /// Apertura integral (Fase B): valida secuencia, crea período+ciclo con
    /// fecha límite del calendario de facturación, genera la planilla de
    /// lectura y devuelve el resumen con avisos.
    /// </summary>
    Task<AperturaCicloResumenDto> AbrirAsync(long companyId, int anio, int mes, string? ciclo, string usuario, CancellationToken ct = default);

    /// <summary>Qué pasaría al abrir (sin escribir): mismos avisos + bloqueos.</summary>
    Task<AperturaCicloResumenDto> PreviewAperturaAsync(long companyId, int anio, int mes, string? ciclo, CancellationToken ct = default);

    /// <summary>Próximo ciclo a abrir según el calendario de facturación (null si no hay).</summary>
    Task<SugerenciaAperturaDto?> SugerenciaAperturaAsync(long companyId, CancellationToken ct = default);

    /// <summary>Deshace una apertura (borra planilla+ciclo) si no hay lecturas ni facturas.</summary>
    Task<DeshacerAperturaResultadoDto> DeshacerAperturaAsync(long companyId, long periodoCicloId, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Planilla de lectura del ciclo (historicomedicion del año/mes/ciclo).
    /// Fase C: reemplaza la consulta del Auxiliar de Lectura eliminado.
    /// </summary>
    Task<IReadOnlyList<PlanillaCicloFilaDto>> PlanillaCicloAsync(long companyId, long periodoCicloId, CancellationToken ct = default);

    /// <summary>Cierra un ciclo; sin forzar exige cero rutas pendientes.</summary>
    Task CerrarCicloAsync(long companyId, long periodoCicloId, string usuario, bool forzar, CancellationToken ct = default);

    /// <summary>Cierra el mes comercial (checklist en verde obligatorio).</summary>
    Task CerrarMesAsync(long companyId, long periodoComercialId, string usuario, CancellationToken ct = default);

    /// <summary>Avisos de períodos para el banner del portal.</summary>
    Task<IReadOnlyList<AvisoPeriodoDto>> AvisosAsync(long companyId, CancellationToken ct = default);
}
