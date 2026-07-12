using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IDescargosService
{
    Task<IReadOnlyList<DescargoListItemDto>> GetAsync(DescargoFilterDto? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDepartamentosAsync(CancellationToken ct = default);
}
