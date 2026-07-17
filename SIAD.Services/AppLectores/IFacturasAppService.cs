using SIAD.Core.DTOs.AppLectores;

namespace SIAD.Services.AppLectores;

/// <summary>Consulta de facturas emitidas vía sincronización de la app de lectores V3.</summary>
public interface IFacturasAppService
{
    Task<IReadOnlyList<FacturaAppListItemDto>> GetAsync(FacturaAppFilterDto? filtro, CancellationToken ct = default);
}
