using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IRequisicionesService
{
    Task<IReadOnlyList<RequisicionListItemDto>> GetAsync(RequisicionFilterDto? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDepartamentosAsync(CancellationToken ct = default);
}
