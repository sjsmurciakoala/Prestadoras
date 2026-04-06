using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Conceptos;

namespace SIAD.Services.Conceptos;

public interface IConceptosService
{
    Task<IReadOnlyList<ConceptoListItemDto>> GetAsync(ConceptoFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<ConceptoListItemDto>> GetPagedAsync(ConceptoFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<ConceptoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ConceptoEditDto> CreateAsync(ConceptoEditDto dto, string user, CancellationToken ct = default);
    Task<ConceptoEditDto> UpdateAsync(int id, ConceptoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
