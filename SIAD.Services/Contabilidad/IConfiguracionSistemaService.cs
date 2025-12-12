using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Servicio para gestionar la configuración del sistema contable de una empresa.
/// </summary>
public interface IConfiguracionSistemaService
{
    /// <summary>
    /// Obtiene la configuración del sistema para una empresa.
    /// </summary>
    Task<ConfiguracionSistemaDto?> ObtenerAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Guarda la configuración del sistema para una empresa.
    /// </summary>
    Task<ConfiguracionSistemaDto> GuardarAsync(long companyId, ConfiguracionSistemaDto dto, string usuario,
        CancellationToken ct = default);

    /// <summary>
    /// Valida que exista un plan de cuentas completo para la empresa.
    /// </summary>
    Task<bool> ExistePlanCuentasAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Valida que exista un período abierto para la empresa.
    /// </summary>
    Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Inicializa la configuración por defecto para una empresa nueva.
    /// </summary>
    Task<ConfiguracionSistemaDto> InicializarConfiguracionPorDefectoAsync(long companyId, long tenantCompanyId,
        DateTime? fechaInicio = null, CancellationToken ct = default);
}
