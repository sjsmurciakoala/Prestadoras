using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

public interface IClienteServicioTarifarioService
{
    Task<IReadOnlyList<ClienteServicioItemDto>> GetServiciosClienteAsync(int clienteId, CancellationToken ct = default);
    Task<ClienteServicioCatalogosDto> GetCatalogosAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarAsync(int clienteId, ClienteServicioSaveRequest request, string usuario, CancellationToken ct = default);
    Task<ResponseModelDto> DesactivarAsync(int clienteId, ClienteServicioDesactivarRequest request, string usuario, CancellationToken ct = default);
}
