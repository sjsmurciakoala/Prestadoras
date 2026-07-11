using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IComprasService
{
    Task<IReadOnlyList<CompraListItemDto>> GetAsync(CompraFilterDto? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetProveedoresAsync(CancellationToken ct = default);
}
