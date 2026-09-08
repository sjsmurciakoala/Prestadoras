using SIAD.Core.DTOs.ActivosFijos;

namespace SIAD.Services.ActivosFijos;

/// <summary>
/// Catálogos del módulo de Activos Fijos: métodos de depreciación y estados (de sistema,
/// iguales para todas las empresas) y tipos de activo y ubicaciones (por empresa).
/// </summary>
public interface ICatalogosActivosFijosService
{
    Task<IReadOnlyList<CatalogoAfDto>> GetMetodosDepreciacionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoAfDto>> GetEstadosAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TipoActivoListItemDto>> GetTiposAsync(bool? soloActivos, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<TipoActivoLookupDto>> GetTiposLookupAsync(CancellationToken ct = default);
    Task<TipoActivoEditDto?> GetTipoByIdAsync(int id, CancellationToken ct = default);
    Task<TipoActivoEditDto> GuardarTipoAsync(TipoActivoEditDto dto, string user, CancellationToken ct = default);
    Task DesactivarTipoAsync(int id, string user, CancellationToken ct = default);

    Task<IReadOnlyList<UbicacionActivoListItemDto>> GetUbicacionesAsync(bool? soloActivos, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<UbicacionActivoLookupDto>> GetUbicacionesLookupAsync(CancellationToken ct = default);
    Task<UbicacionActivoEditDto?> GetUbicacionByIdAsync(int id, CancellationToken ct = default);
    Task<UbicacionActivoEditDto> GuardarUbicacionAsync(UbicacionActivoEditDto dto, string user, CancellationToken ct = default);
    Task DesactivarUbicacionAsync(int id, string user, CancellationToken ct = default);
}
