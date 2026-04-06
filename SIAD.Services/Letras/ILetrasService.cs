using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Letras;

namespace SIAD.Services.Letras;

public interface ILetrasService
{
    Task<IReadOnlyList<LetraListItemDto>> GetLetrasAsync(LetraFilterDto filtro, CancellationToken ct = default);
    Task<PagedResult<LetraListItemDto>> GetLetrasPagedAsync(LetraFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<LetraDetailDto?> GetLetraAsync(string letra, CancellationToken ct = default);
    Task CreateLetraAsync(LetraEditDto dto, string user, CancellationToken ct = default);
    Task UpdateLetraAsync(string letra, LetraEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteLetraAsync(string letra, CancellationToken ct = default);
}
