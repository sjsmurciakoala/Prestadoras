using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.AppLectores;

public interface IConfiguracionAppService
{
    Task<IReadOnlyList<ConfiguracionAppListItemDto>> GetAsync(ConfiguracionAppFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<ConfiguracionAppListItemDto>> GetPagedAsync(ConfiguracionAppFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<ConfiguracionAppEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ConfiguracionAppEditDto> CreateAsync(ConfiguracionAppEditDto dto, string user, CancellationToken ct = default);
    Task<ConfiguracionAppEditDto> UpdateAsync(int id, ConfiguracionAppEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
