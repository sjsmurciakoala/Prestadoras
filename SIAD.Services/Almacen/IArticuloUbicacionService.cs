using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IArticuloUbicacionService
{
    Task<IReadOnlyList<ArticuloUbicacionDto>> GetAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default);
    Task<ArticuloUbicacionDto> AddAsync(int articuloId, ArticuloUbicacionDto dto, string user, CancellationToken ct = default);
    Task<ArticuloUbicacionDto> UpdateAsync(int articuloId, int id, ArticuloUbicacionDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Deshabilita (soft-delete) la ubicación para conservar el histórico. No se
    /// permite deshabilitar la bodega principal ni dejar el artículo sin bodega activa,
    /// ni deshabilitar una bodega con existencia (o con comprometido / en tránsito):
    /// saldría del rollup de cabecera sin movimiento de kardex y descuadraría el artículo.
    /// Lanza <see cref="InvalidOperationException"/> en cualquiera de esos casos.
    /// </summary>
    Task<bool> DeshabilitarAsync(int articuloId, int id, string user, CancellationToken ct = default);

    /// <summary>Reactiva una ubicación previamente deshabilitada.</summary>
    Task<bool> ReactivarAsync(int articuloId, int id, string user, CancellationToken ct = default);
}
