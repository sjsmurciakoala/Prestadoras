using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface ITerminoPagoService
{
    Task<IReadOnlyList<TerminoPagoListItemDto>> GetAsync(ClasificacionFilterDto? filtro, CancellationToken ct = default);
    Task<TerminoPagoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<TerminoPagoLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<TerminoPagoEditDto> CreateAsync(TerminoPagoEditDto dto, string user, CancellationToken ct = default);
    Task<TerminoPagoEditDto> UpdateAsync(int id, TerminoPagoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
}
