using SIAD.Core.DTOs.Impuestos;

namespace SIAD.Services.Impuestos;

/// <summary>
/// Mantenimiento del catálogo GLOBAL de impuestos (cfg_impuesto) y sus tasas con
/// vigencia (cfg_impuesto_tasa). Sin company_id: la ley fija las tasas, no la empresa.
/// </summary>
public interface IImpuestosService
{
    // ----- impuesto -----
    Task<IReadOnlyList<ImpuestoListItemDto>> GetAsync(ImpuestoFilterDto? filtro, CancellationToken ct = default);
    Task<ImpuestoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>El impuesto con todas sus tasas (vigentes e históricas).</summary>
    Task<ImpuestoDetalleDto?> GetDetalleAsync(int id, CancellationToken ct = default);

    Task<ImpuestoEditDto> CreateAsync(ImpuestoEditDto dto, string user, CancellationToken ct = default);
    Task<ImpuestoEditDto> UpdateAsync(int id, ImpuestoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);

    // ----- tasas -----
    Task<IReadOnlyList<ImpuestoTasaDto>> GetTasasAsync(int impuestoId, CancellationToken ct = default);
    Task<ImpuestoTasaDto?> GetTasaByIdAsync(int tasaId, CancellationToken ct = default);
    Task<ImpuestoTasaDto> CreateTasaAsync(ImpuestoTasaDto dto, string user, CancellationToken ct = default);
    Task<ImpuestoTasaDto> UpdateTasaAsync(int tasaId, ImpuestoTasaDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateTasaAsync(int tasaId, string user, CancellationToken ct = default);

    /// <summary>
    /// Cambio de tasa por decreto: cierra la vigencia de la tasa actual y crea una nueva
    /// con el mismo código, en UNA transacción. Si falla la creación, el cierre se revierte.
    /// </summary>
    Task<ImpuestoTasaDto> CambiarTasaAsync(CambiarTasaDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Tasas que rigen a una fecha dada. Es lo que el motor de cálculo debe consultar:
    /// nunca "la tasa actual", siempre "la tasa que regía en la fecha del documento".
    /// </summary>
    Task<IReadOnlyList<ImpuestoTasaLookupDto>> GetTasasVigentesAsync(DateOnly fecha, CancellationToken ct = default);
}
