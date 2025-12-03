using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Ordenes;

namespace SIAD.Services.Ordenes;

public interface IOrdenesService
{
    Task<IReadOnlyList<OrdenTrabajoListItemDto>> GetOrdenesAsync(OrdenTrabajoFilterDto filtro, CancellationToken cancellationToken = default);
    Task<OrdenTrabajoDetailDto?> GetOrdenAsync(int id, CancellationToken cancellationToken = default);
    Task<OrdenTrabajoOperacionResultadoDto> CrearOrdenAsync(CrearOrdenTrabajoDto dto, CancellationToken cancellationToken = default);
    Task<OrdenTrabajoOperacionResultadoDto> AsignarOrdenesAsync(OrdenTrabajoAsignacionDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsuarioMiOrdenDto>> GetUsuariosMiOrdenAsync(int? tipo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrdenTrabajoTipoDto>> BuscarTiposOrdenAsync(string departamentoAplicacion, string? texto, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrdenTrabajoPropietarioDto>> BuscarPropietariosAsync(string? texto, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrdenTrabajoEstadoDto>> BuscarEstadosOrdenAsync(string? texto, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoordenadaOrdenDto>> GetCoordenadasAsync(CancellationToken cancellationToken = default);
}
