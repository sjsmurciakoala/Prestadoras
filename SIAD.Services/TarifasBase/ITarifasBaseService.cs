using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TarifasBase;

namespace SIAD.Services.TarifasBase;

public interface ITarifasBaseService
{
    Task<IReadOnlyList<TarifaBaseListItemDto>> GetAsync(TarifaBaseFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<TarifaBaseListItemDto>> GetPagedAsync(
        TarifaBaseFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default);
    Task<TarifaBaseEditDto?> GetByIdAsync(int tipo, int categoriaId, string codigo, CancellationToken ct = default);
    Task<TarifaBaseEditDto> CreateAsync(TarifaBaseEditDto dto, string user, CancellationToken ct = default);
    Task<TarifaBaseEditDto> UpdateAsync(int tipo, int categoriaId, string codigo, TarifaBaseEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(int tipo, int categoriaId, string codigo, CancellationToken ct = default);
}
