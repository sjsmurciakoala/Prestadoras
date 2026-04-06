using SIAD.Core.DTOs.Clientes;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Clientes;

public interface IClientesService
{
    Task<string> GenerarCodigoClienteAsync(CancellationToken ct = default);
    Task<ClienteCreateResponseDto> CrearClienteAsync(ClienteCreateDto dto, string usuarioCreacion, CancellationToken ct = default);
    Task<ClienteDetailDto> ActualizarClienteAsync(int id, ClienteUpdateDto dto, string usuarioModificacion, CancellationToken ct = default);

    Task<IReadOnlyList<ClienteListItemDto>> GetClientesAsync(CancellationToken cancellationToken = default);

    Task<ClienteDetailDto?> GetClienteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClienteListItemDto>> SearchClientesAsync(ClienteFilterDto filtro, CancellationToken cancellationToken = default);

    Task<PagedResult<ClienteListItemDto>> SearchClientesPagedAsync(string? search, bool soloActivos, int skip, int take, string? sortField, bool sortDesc, CancellationToken cancellationToken = default);

    Task<ClienteEstadoCuentaDto> GetEstadoCuentaAsync(int clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteMovimientoDto>> GetMovimientosAsync(int clienteId, CancellationToken ct = default);
    Task<PagedResult<ClienteMovimientoDto>> GetMovimientosPagedAsync(int clienteId, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<ClienteHistoricoConsumoResponseDto> GetHistoricoConsumoAsync(int clienteId, DateTime desde, DateTime hasta, CancellationToken ct = default);
    // NOTE: Added paged historico consumo to avoid loading large ranges in memory.
    Task<ClienteHistoricoConsumoPagedResponseDto> GetHistoricoConsumoPagedAsync(int clienteId, DateTime desde, DateTime hasta, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);

    Task<ClienteConfiguracionTarifaHeaderDto?> GetConfiguracionTarifaHeaderAsync(int clienteId, string usuario, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteConfiguracionTarifaDetalleDto>> GetConfiguracionTarifaDetalleAsync(
        int clienteId,
        int? categoriaSeleccionada,
        string usuario,
        CancellationToken ct = default);
    Task<ResponseModelDto> ActualizarConfiguracionTarifaAsync(
        int clienteId,
        ClienteConfiguracionTarifaUpdateRequest request,
        string usuario,
        CancellationToken ct = default);
    Task<ResponseModelDto> AgregarConfiguracionTarifaServicioAsync(
        int clienteId,
        ClienteConfiguracionTarifaAddRequest request,
        string usuario,
        CancellationToken ct = default);

    Task<ClienteFotoMedidorHeaderDto?> GetFotoMedidorHeaderAsync(int clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteFotoMedidorItemDto>> GetFotoMedidorAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default);
    Task<byte[]?> GetFotoMedidorImagenAsync(int ide, CancellationToken ct = default);

}
