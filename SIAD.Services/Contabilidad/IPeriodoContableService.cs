using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Servicio para gestionar periodos contables.
/// </summary>
public interface IPeriodoContableService
{
    /// <summary>
    /// Obtiene el periodo activo/abierto para una empresa.
    /// </summary>
    Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Valida que exista un periodo abierto para la empresa.
    /// </summary>
    Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene o crea un periodo inicial para una empresa nueva.
    /// </summary>
    Task<PeriodoContableDto> ObtenerOCrearPeriodoInicialAsync(long companyId, DateTime? fechaInicio = null,
        CancellationToken ct = default);
}
