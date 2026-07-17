using SIAD.Core.DTOs.Clientes;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Clientes;

public interface IClientesService
{
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

    Task<ClienteFotoMedidorHeaderDto?> GetFotoMedidorHeaderAsync(int clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteFotoMedidorItemDto>> GetFotoMedidorAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default);
    Task<byte[]?> GetFotoMedidorImagenAsync(int ide, CancellationToken ct = default);

    Task SetNoCortableAsync(string clave, bool valor, string usuario, string? motivo = null, CancellationToken ct = default);

    /// <summary>Configuración del generador de código de cliente (con preview del próximo).</summary>
    Task<CodigoClienteConfigDto> ObtenerCodigoConfigAsync(CancellationToken ct = default);

    Task<CodigoClienteConfigDto> GuardarCodigoConfigAsync(CodigoClienteConfigDto dto, string usuario, CancellationToken ct = default);

    /// <summary>Siguiente secuencia de caminata sugerida (max+10) para ciclo+libreta; solo sugiere, no consume.</summary>
    Task<string?> SugerirSecuenciaAsync(int cicloId, string libreta, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteEstadoLogItemDto>> GetEstadoLogAsync(string clave, CancellationToken ct = default);
}
