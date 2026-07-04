using SIAD.Core.DTOs.PeriodosComerciales;

namespace SIAD.Services.PeriodosComerciales;

/// <summary>
/// Períodos comerciales (F7): listado, apertura secuencial y cierres con
/// checklist. Las transiciones de estado viven en SPs de BD
/// (sp_adm_periodo_comercial_abrir/cerrar, sp_adm_periodo_ciclo_cerrar).
/// </summary>
public interface IPeriodoComercialService
{
    Task<IReadOnlyList<PeriodoComercialDto>> ListarAsync(long companyId, CancellationToken ct = default);

    /// <summary>Rutas del ciclo con su avance de facturación del mes.</summary>
    Task<IReadOnlyList<RutaCicloDto>> RutasCicloAsync(long companyId, long periodoCicloId, CancellationToken ct = default);

    /// <summary>Checklist del cierre del mes comercial.</summary>
    Task<IReadOnlyList<ChecklistCierreItemDto>> ChecklistCierreAsync(long companyId, long periodoComercialId, CancellationToken ct = default);

    /// <summary>
    /// Abre el período del mes (y su ciclo). Exige que el período del mes
    /// calendario anterior, si existe, esté cerrado.
    /// </summary>
    Task<long> AbrirAsync(long companyId, int anio, int mes, string? ciclo, string usuario, CancellationToken ct = default);

    /// <summary>Cierra un ciclo; sin forzar exige cero rutas pendientes.</summary>
    Task CerrarCicloAsync(long companyId, long periodoCicloId, string usuario, bool forzar, CancellationToken ct = default);

    /// <summary>Cierra el mes comercial (checklist en verde obligatorio).</summary>
    Task CerrarMesAsync(long companyId, long periodoComercialId, string usuario, CancellationToken ct = default);

    /// <summary>Avisos de períodos para el banner del portal.</summary>
    Task<IReadOnlyList<AvisoPeriodoDto>> AvisosAsync(long companyId, CancellationToken ct = default);
}
