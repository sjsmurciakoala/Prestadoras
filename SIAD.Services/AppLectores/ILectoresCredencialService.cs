using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.AppLectores;

/// <summary>
/// Mantenimiento de credenciales de lectores de la app móvil V3
/// (<c>adm_lector_credencial</c>, bcrypt). Scoped al tenant actual.
/// </summary>
public interface ILectoresCredencialService
{
    Task<IReadOnlyList<LectorCredencialListItemDto>> GetAsync(LectorCredencialFilterDto? filtro, CancellationToken ct = default);

    Task<PagedResult<LectorCredencialListItemDto>> GetPagedAsync(
        LectorCredencialFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);

    Task<LectorCredencialEditDto?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<LectorCredencialEditDto> CreateAsync(LectorCredencialEditDto dto, string user, CancellationToken ct = default);

    Task<LectorCredencialEditDto> UpdateAsync(long id, LectorCredencialEditDto dto, string user, CancellationToken ct = default);

    Task<bool> DeactivateAsync(long id, string user, CancellationToken ct = default);
}
