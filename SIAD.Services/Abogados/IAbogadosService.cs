using SIAD.Core.DTOs.Abogados;

namespace SIAD.Services.Abogados;

public interface IAbogadosService
{
    Task<IReadOnlyList<AbogadoListItemDto>> GetAsync(AbogadoFilterDto? filtro, CancellationToken ct = default);
    Task<AbogadoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AbogadoEditDto> CreateAsync(AbogadoEditDto dto, string user, CancellationToken ct = default);
    Task<AbogadoEditDto> UpdateAsync(int id, AbogadoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
