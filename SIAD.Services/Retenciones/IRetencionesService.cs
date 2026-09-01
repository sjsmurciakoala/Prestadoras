using SIAD.Core.DTOs.Retenciones;

namespace SIAD.Services.Retenciones;

/// <summary>
/// Mantenimiento del catálogo de retenciones a proveedores.
/// <para>
/// Catálogo GLOBAL (cfg_retencion + cfg_retencion_tasa): los % los fija la ley, no la empresa —
/// sin company_id. La cuenta contable del pasivo por empresa (prv_retencion_cuenta) SÍ es
/// tenant-scoped y se administra con los métodos de "cuenta".
/// </para>
/// <para>
/// REGLA CENTRAL (igual que impuestos): las tasas cambian por decreto y NO se editan en sitio —
/// se cierra la vigencia de la actual y se crea una nueva (<see cref="CambiarTasaAsync"/>).
/// </para>
/// </summary>
public interface IRetencionesService
{
    // ----- retención (concepto) -----
    Task<IReadOnlyList<RetencionListItemDto>> GetAsync(RetencionFilterDto? filtro, CancellationToken ct = default);
    Task<RetencionEditDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>La retención con todas sus tasas y la cuenta configurada para la empresa actual.</summary>
    Task<RetencionDetalleDto?> GetDetalleAsync(int id, CancellationToken ct = default);

    Task<RetencionEditDto> CreateAsync(RetencionEditDto dto, string user, CancellationToken ct = default);
    Task<RetencionEditDto> UpdateAsync(int id, RetencionEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);

    // ----- tasas -----
    Task<IReadOnlyList<RetencionTasaDto>> GetTasasAsync(int retencionId, CancellationToken ct = default);
    Task<RetencionTasaDto?> GetTasaByIdAsync(int tasaId, CancellationToken ct = default);
    Task<RetencionTasaDto> CreateTasaAsync(RetencionTasaDto dto, string user, CancellationToken ct = default);
    Task<RetencionTasaDto> UpdateTasaAsync(int tasaId, RetencionTasaDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateTasaAsync(int tasaId, string user, CancellationToken ct = default);

    /// <summary>
    /// Cambio de tasa por decreto: cierra la vigencia de la tasa actual y crea una nueva de la
    /// misma retención, en UNA transacción. Si falla la creación, el cierre se revierte.
    /// </summary>
    Task<RetencionTasaDto> CambiarTasaAsync(CambiarTasaDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Retenciones con la tasa que rige a una fecha dada. Es lo que consumirá F2: nunca "la tasa
    /// actual", siempre "la tasa que regía en la fecha del pago".
    /// </summary>
    Task<IReadOnlyList<RetencionTasaLookupDto>> GetTasasVigentesAsync(DateOnly fecha, CancellationToken ct = default);

    /// <summary>
    /// Lookup del autocálculo de F2: TODAS las retenciones activas resueltas a una fecha — su % vigente
    /// (o null = sin tasa, p. ej. ISV-RET), la cuenta del pasivo por empresa (o null), y la tasa ISV
    /// general vigente para la base SIN_ISV. A diferencia de <see cref="GetTasasVigentesAsync"/>, incluye
    /// las retenciones SIN tasa vigente para que la UI pueda mostrarlas y avisar en lugar de inventar %.
    /// </summary>
    Task<RetencionesAplicablesDto> GetAplicablesAsync(DateOnly fecha, CancellationToken ct = default);

    // ----- cuenta del pasivo por empresa (tenant-scoped) -----

    /// <summary>Cuentas posteables del plan de la empresa actual, para el desplegable de selección.</summary>
    Task<IReadOnlyList<CuentaPosteableDto>> GetCuentasPosteablesAsync(CancellationToken ct = default);

    /// <summary>La cuenta del pasivo configurada para esta retención en la empresa actual (o null).</summary>
    Task<RetencionCuentaDto?> GetCuentaAsync(int retencionId, CancellationToken ct = default);

    /// <summary>Configura (alta o actualización) la cuenta del pasivo para esta retención en la empresa actual.</summary>
    Task<RetencionCuentaDto> SetCuentaAsync(int retencionId, RetencionCuentaDto dto, string user, CancellationToken ct = default);
}
