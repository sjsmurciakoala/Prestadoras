using SIAD.Core.DTOs.Auditoria;

namespace SIAD.Services.Auditoria;

public interface IAuditoriaConfigService
{
    Task<IReadOnlyList<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default);
    Task GuardarAsync(IReadOnlyList<AuditoriaConfigItemDto> items, string user, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogoTablaDisponibleDto>> TablasDisponiblesAsync(CancellationToken ct = default);
    Task AgregarAlCatalogoAsync(AgregarMaestroDto dto, string user, CancellationToken ct = default);
    Task DesactivarCatalogoAsync(string tabla, string user, CancellationToken ct = default);
}
