using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Configuración de Integración Contable ↔ Comercial por empresa
/// (plan 2026-07-02, Fase 2). Administra con_integracion_config,
/// con_integracion_cuenta y con_integracion_asiento.
/// </summary>
public interface IIntegracionContableService
{
    /// <summary>Obtiene la configuración completa (cabecera + matriz + asientos).</summary>
    Task<IntegracionContableDto> ObtenerAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Guarda la configuración completa. Los errores duros (cuentas no
    /// posteables, filas incompletas o duplicadas, módulo activo con huecos)
    /// lanzan <see cref="InvalidOperationException"/> sin persistir.
    /// </summary>
    Task<IntegracionGuardarResultDto> GuardarAsync(long companyId, IntegracionContableDto dto, string usuario,
        CancellationToken ct = default);

    /// <summary>Aplica un perfil de auto-llenado (sp_con_aplicar_perfil_integracion).</summary>
    Task<IntegracionPerfilResultDto> AplicarPerfilAsync(long companyId, string perfil, string usuario,
        CancellationToken ct = default);

    /// <summary>Valida la configuración persistida (posteo y cobertura según modo).</summary>
    Task<IntegracionValidacionDto> ValidarAsync(long companyId, CancellationToken ct = default);

    /// <summary>Servicios comerciales activos de la empresa (para la matriz).</summary>
    Task<IReadOnlyList<ServicioIntegracionLookupDto>> ListarServiciosAsync(long companyId, CancellationToken ct = default);

    /// <summary>Cuentas del plan que permiten posteo directo (allows_posting).</summary>
    Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasPosteablesAsync(long companyId, CancellationToken ct = default);

    /// <summary>Categorías de servicio activas (para la matriz).</summary>
    Task<IReadOnlyList<CategoriaServicioLookupDto>> ListarCategoriasAsync(CancellationToken ct = default);
}
