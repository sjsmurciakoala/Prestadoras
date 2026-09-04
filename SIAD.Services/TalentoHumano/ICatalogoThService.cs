using SIAD.Core.DTOs.TalentoHumano;

namespace SIAD.Services.TalentoHumano;

/// <summary>Los dos catálogos simples de Talento Humano que respaldan al empleado.</summary>
public enum CatalogoTh
{
    Cargo,
    Departamento
}

public interface ICatalogoThService
{
    Task<IReadOnlyList<CatalogoThListItemDto>> GetAsync(CatalogoTh tipo, CatalogoThFilterDto? filtro, CancellationToken ct = default);
    Task<CatalogoThEditDto?> GetByIdAsync(CatalogoTh tipo, int id, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogoThLookupDto>> GetLookupAsync(CatalogoTh tipo, CancellationToken ct = default);
    Task<CatalogoThEditDto> CreateAsync(CatalogoTh tipo, CatalogoThEditDto dto, string user, CancellationToken ct = default);
    Task<CatalogoThEditDto> UpdateAsync(CatalogoTh tipo, int id, CatalogoThEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(CatalogoTh tipo, int id, string user, CancellationToken ct = default);
}
