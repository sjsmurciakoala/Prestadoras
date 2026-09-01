using SIAD.Core.DTOs.Mantenimientos;

namespace SIAD.Services.Mantenimientos;

/// <summary>Mantenimiento del catálogo de formatos fiscales (cfg_formato_fiscal).</summary>
public interface IFormatoFiscalService
{
    Task<IReadOnlyList<FormatoFiscalListItemDto>> GetAsync(FormatoFiscalFilterDto? filtro, CancellationToken ct = default);

    Task<FormatoFiscalEditDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Los formatos activos, con máscara de DevExpress, patrón y ejemplo ya derivados.</summary>
    Task<IReadOnlyList<FormatoFiscalLookupDto>> GetLookupAsync(CancellationToken ct = default);

    Task<FormatoFiscalEditDto> CreateAsync(FormatoFiscalEditDto dto, string user, CancellationToken ct = default);

    Task<FormatoFiscalEditDto> UpdateAsync(int id, FormatoFiscalEditDto dto, string user, CancellationToken ct = default);

    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
