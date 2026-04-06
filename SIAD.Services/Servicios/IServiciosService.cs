using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Servicios;

namespace SIAD.Services.Servicios;

public interface IServiciosService
{
    Task<IReadOnlyList<ServicioListItemDto>> GetAsync(ServicioFilterDto? filtro, CancellationToken ct = default);
    Task<PagedResult<ServicioListItemDto>> GetPagedAsync(ServicioFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<ServicioEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServicioEditDto> CreateAsync(ServicioEditDto dto, string user, CancellationToken ct = default);
    Task<ServicioEditDto> UpdateAsync(int id, ServicioEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
