using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Servicio para gestionar períodos contables.
/// </summary>
public interface IPeriodoContableService
{
    /// <summary>
    /// Obtiene el período activo/abierto para una empresa.
    /// </summary>
    Task<PeriodoContableDto?> ObtenerPeriodoActivoAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Valida que exista un período abierto para la empresa.
    /// </summary>
    Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene o crea un período inicial para una empresa nueva.
    /// </summary>
    Task<PeriodoContableDto> ObtenerOCrearPeriodoInicialAsync(long companyId, DateTime? fechaInicio = null, 
        CancellationToken ct = default);
}

/// <summary>
/// DTO para período contable.
/// </summary>
public sealed class PeriodoContableDto
{
    public long PeriodoId { get; set; }
    public long CompanyId { get; set; }
    public int Año { get; set; }
    public byte Mes { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string Estado { get; set; } = "ABIERTO"; // ABIERTO, BLOQUEADO, CERRADO
}
