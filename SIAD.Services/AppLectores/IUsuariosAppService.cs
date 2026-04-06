using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.AppLectores;

public interface IUsuariosAppService
{
    Task<IReadOnlyList<UsuarioAppListItemDto>> GetAsync(UsuarioAppFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<UsuarioAppListItemDto>> GetPagedAsync(UsuarioAppFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<UsuarioAppEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UsuarioAppEditDto> CreateAsync(UsuarioAppEditDto dto, string user, CancellationToken ct = default);
    Task<UsuarioAppEditDto> UpdateAsync(int id, UsuarioAppEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
