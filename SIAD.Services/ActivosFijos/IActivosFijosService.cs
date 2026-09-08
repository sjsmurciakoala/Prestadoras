using SIAD.Core.DTOs.ActivosFijos;

namespace SIAD.Services.ActivosFijos;

/// <summary>Maestro de activos fijos: registro, ficha, asignaciones y componentes.</summary>
public interface IActivosFijosService
{
    Task<IReadOnlyList<ActivoFijoListItemDto>> GetAsync(ActivoFijoFilterDto? filtro, CancellationToken ct = default);
    Task<ActivoFijoResumenDto> GetResumenAsync(CancellationToken ct = default);
    Task<ActivoFijoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ActivoFijoEditDto> GuardarAsync(ActivoFijoEditDto dto, string user, CancellationToken ct = default);

    Task<IReadOnlyList<ActivoAsignacionDto>> GetAsignacionesAsync(int activoId, CancellationToken ct = default);
    Task AsignarAsync(int activoId, ActivoAsignacionRequestDto dto, string user, CancellationToken ct = default);

    /// <summary>Detalle mensual de depreciación del activo, más reciente primero.</summary>
    Task<IReadOnlyList<ActivoDepreciacionDto>> GetDepreciacionesAsync(int activoId, CancellationToken ct = default);

    /// <summary>Totales del historial enfrentados a lo que declara el maestro.</summary>
    Task<ActivoDepreciacionResumenDto> GetDepreciacionResumenAsync(int activoId, CancellationToken ct = default);

    Task<IReadOnlyList<ActivoComponenteDto>> GetComponentesAsync(int activoId, CancellationToken ct = default);
    Task<ActivoComponenteDto> GuardarComponenteAsync(int activoId, ActivoComponenteDto dto, string user, CancellationToken ct = default);
    Task EliminarComponenteAsync(int componenteId, CancellationToken ct = default);
}
