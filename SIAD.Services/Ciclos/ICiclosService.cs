using SIAD.Core.DTOs.Ciclos;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Ciclos;

public interface ICiclosService
{
    Task<IReadOnlyList<CicloListItemDto>> GetAsync(CicloFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<CicloListItemDto>> GetPagedAsync(CicloFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<CicloEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CicloEditDto> CreateAsync(CicloEditDto dto, string user, CancellationToken ct = default);
    Task<CicloEditDto> UpdateAsync(int id, CicloEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
