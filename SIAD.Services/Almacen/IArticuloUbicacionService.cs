using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IArticuloUbicacionService
{
    Task<IReadOnlyList<ArticuloUbicacionDto>> GetAsync(int articuloId, CancellationToken ct = default);
    Task<ArticuloUbicacionDto> AddAsync(int articuloId, ArticuloUbicacionDto dto, string user, CancellationToken ct = default);
    Task<ArticuloUbicacionDto> UpdateAsync(int articuloId, int id, ArticuloUbicacionDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(int articuloId, int id, CancellationToken ct = default);
}
