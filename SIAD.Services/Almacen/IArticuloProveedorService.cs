using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IArticuloProveedorService
{
    Task<IReadOnlyList<ArticuloProveedorDto>> GetAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default);
    Task<ArticuloProveedorDto> AddAsync(int articuloId, ArticuloProveedorDto dto, string user, CancellationToken ct = default);
    Task<ArticuloProveedorDto> UpdateAsync(int articuloId, int id, ArticuloProveedorDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Deshabilita (soft-delete) la relación para conservar el histórico. Al deshabilitar,
    /// se limpia la marca de principal.
    /// </summary>
    Task<bool> DeshabilitarAsync(int articuloId, int id, string user, CancellationToken ct = default);

    /// <summary>Reactiva una relación previamente deshabilitada.</summary>
    Task<bool> ReactivarAsync(int articuloId, int id, string user, CancellationToken ct = default);
}
