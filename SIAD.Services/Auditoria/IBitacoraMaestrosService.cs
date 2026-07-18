using SIAD.Core.DTOs.Auditoria;

namespace SIAD.Services.Auditoria;

public interface IBitacoraMaestrosService
{
    Task<IReadOnlyList<BitacoraMaestroListItemDto>> BuscarAsync(BitacoraMaestroFilterDto filtro, CancellationToken ct = default);
}
