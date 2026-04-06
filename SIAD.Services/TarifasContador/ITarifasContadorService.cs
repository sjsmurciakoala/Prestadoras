using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TarifasContador;

namespace SIAD.Services.TarifasContador;

public interface ITarifasContadorService
{
    Task<IReadOnlyList<TarifaContadorListItemDto>> GetAsync(TarifaContadorFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<TarifaContadorListItemDto>> GetPagedAsync(
        TarifaContadorFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default);
    Task<TarifaContadorEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TarifaContadorEditDto> CreateAsync(TarifaContadorEditDto dto, string user, CancellationToken ct = default);
    Task<TarifaContadorEditDto> UpdateAsync(int id, TarifaContadorEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
