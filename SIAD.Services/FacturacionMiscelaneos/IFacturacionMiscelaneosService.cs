using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.FacturacionMiscelaneos;

namespace SIAD.Services.FacturacionMiscelaneos;

public interface IFacturacionMiscelaneosService
{
    Task<IReadOnlyList<ClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default);

    Task<ClienteMiscelaneoDto?> ObtenerClienteAsync(string clienteClave, CancellationToken ct = default);

    Task<IReadOnlyList<MiscelaneoCatalogoDto>> ListarCatalogoAsync(CancellationToken ct = default);

    Task<ResponseModelDto> CrearReciboAsync(FacturaMiscelaneoCrearDto dto, CancellationToken ct = default);

    Task<FacturaMiscelaneoResponseDto?> ObtenerReciboAsync(int numeroRecibo, CancellationToken ct = default);

    // CRUD catálogo misceláneos
    Task<MiscelaneoCatalogoEditDto?> ObtenerCatalogoItemAsync(int id, CancellationToken ct = default);

    Task<MiscelaneoCatalogoEditDto> CrearCatalogoItemAsync(MiscelaneoCatalogoEditDto dto, string user, CancellationToken ct = default);

    Task<MiscelaneoCatalogoEditDto> ActualizarCatalogoItemAsync(int id, MiscelaneoCatalogoEditDto dto, string user, CancellationToken ct = default);

    Task<bool> EliminarCatalogoItemAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<MiscelaneoConsultaDto>> ConsultarRecibosAsync(
        MiscelaneosConsultaFiltroDto filtro, CancellationToken ct = default);
}
