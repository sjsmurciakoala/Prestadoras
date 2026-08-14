using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Evaluación (scorecard) de proveedores por período.
/// <para>
/// Las métricas automáticas las calcula <c>fn_prv_evaluacion_metricas</c>
/// (<c>Database/2026-08-14_prv_evaluacion.sql</c>); este servicio aplica los pesos del catálogo,
/// redistribuye el peso de los criterios sin datos, resuelve la clase y persiste el resultado.
/// </para>
/// </summary>
public interface IEvaluacionProveedorService
{
    Task<IReadOnlyList<EvaluacionPeriodoDto>> GetPeriodosAsync(CancellationToken ct = default);

    Task<EvaluacionPeriodoDto?> GetPeriodoAsync(int periodoId, CancellationToken ct = default);

    Task<EvaluacionPeriodoDto> CrearPeriodoAsync(
        EvaluacionPeriodoUpsertDto dto, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Recalcula todas las evaluaciones del período. Respeta lo capturado a mano en los criterios
    /// manuales y falla si el período está cerrado.
    /// </summary>
    Task<EvaluacionCalculoResultadoDto> CalcularAsync(
        int periodoId, string usuario, CancellationToken ct = default);

    /// <summary>Congela el período: deja de poder recalcularse y de admitir capturas.</summary>
    Task<bool> CerrarPeriodoAsync(int periodoId, string usuario, CancellationToken ct = default);

    Task<IReadOnlyList<EvaluacionRankingItemDto>> GetRankingAsync(
        int periodoId, EvaluacionFilterDto? filtro = null, CancellationToken ct = default);

    Task<EvaluacionFichaDto?> GetFichaAsync(
        int periodoId, string codProveedor, CancellationToken ct = default);

    /// <summary>
    /// Califica un criterio manual y/o guarda el plan de acción, y devuelve la ficha recalculada
    /// (capturar cambia el puntaje: el peso deja de redistribuirse).
    /// </summary>
    Task<EvaluacionFichaDto> CapturarAsync(
        int periodoId, string codProveedor, EvaluacionCapturaDto dto, string usuario,
        CancellationToken ct = default);

    /// <summary>Criterios ACTIVOS, en el orden del catálogo (lo que usa el cálculo).</summary>
    Task<IReadOnlyList<EvaluacionCriterioDto>> GetCriteriosAsync(CancellationToken ct = default);

    /// <summary>Catálogo completo, incluidos los inactivos: es lo que edita el mantenimiento (F3).</summary>
    Task<IReadOnlyList<EvaluacionCriterioDto>> GetCriteriosCatalogoAsync(CancellationToken ct = default);

    Task<EvaluacionCriterioDto> CrearCriterioAsync(
        EvaluacionCriterioUpsertDto dto, string usuario, CancellationToken ct = default);

    Task<EvaluacionCriterioDto> ActualizarCriterioAsync(
        int id, EvaluacionCriterioUpsertDto dto, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Borra un criterio. Falla si ya se usó en alguna evaluación: en ese caso hay que
    /// desactivarlo, para no dejar historia que nadie pueda explicar.
    /// </summary>
    Task<bool> EliminarCriterioAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<EvaluacionClaseDto>> GetClasesAsync(CancellationToken ct = default);

    Task<EvaluacionClaseDto> CrearClaseAsync(
        EvaluacionClaseUpsertDto dto, CancellationToken ct = default);

    Task<EvaluacionClaseDto> ActualizarClaseAsync(
        int id, EvaluacionClaseUpsertDto dto, CancellationToken ct = default);

    Task<bool> EliminarClaseAsync(int id, CancellationToken ct = default);

    /// <summary>Datos de la ficha impresa (F5). Null si el proveedor no tiene evaluación.</summary>
    Task<EvaluacionFichaImpresionDto?> GetDatosFichaImpresionAsync(
        int periodoId, string codProveedor, string? impresoPor = null, CancellationToken ct = default);

    /// <summary>Datos del cuadro comparativo del período (F5). Null si el período no existe.</summary>
    Task<EvaluacionComparativoImpresionDto?> GetDatosComparativoImpresionAsync(
        int periodoId, EvaluacionFilterDto? filtro = null, string? impresoPor = null,
        CancellationToken ct = default);
}
