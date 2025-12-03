using SIAD.Core.DTOs.Clientes;

namespace SIAD.Services.Clientes;

public interface IClientesService
{
    Task<IReadOnlyList<ClienteListItemDto>> GetClientesAsync(CancellationToken cancellationToken = default);

    Task<ClienteDetailDto?> GetClienteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClienteListItemDto>> SearchClientesAsync(ClienteFilterDto filtro, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClienteTarifaDto>> GetTarifasAsync(int clienteId, CancellationToken cancellationToken = default);

    Task<ClienteEstadoCuentaDto> GetEstadoCuentaAsync(int clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteMovimientoDto>> GetMovimientosAsync(int clienteId, CancellationToken ct = default);


}
