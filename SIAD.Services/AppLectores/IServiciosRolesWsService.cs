using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.AppLectores;

public interface IServiciosRolesWsService
{
    Task<IReadOnlyList<ServicioRolWsListItemDto>> GetAsync(ServicioRolWsFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<ServicioRolWsListItemDto>> GetPagedAsync(
        ServicioRolWsFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default);
    Task<ServicioRolWsEditDto?> GetByIdAsync(string rol, string codigo, CancellationToken ct = default);
    Task<ServicioRolWsEditDto> CreateAsync(ServicioRolWsEditDto dto, string user, CancellationToken ct = default);
    Task<ServicioRolWsEditDto> UpdateAsync(string rol, string codigo, ServicioRolWsEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(string rol, string codigo, CancellationToken ct = default);
}
