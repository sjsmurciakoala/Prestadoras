using SIAD.Core.DTOs.Catalogos;

namespace SIAD.Services.Catalogos;

public interface ICatalogosService
{
    Task<IReadOnlyList<AbogadoLookupDto>> GetAbogadosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BarrioLookupDto>> GetBarriosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ServicioLookupDto>> GetServiciosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TipoUsoLookupDto>> GetTiposUsoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default);

    Task<IReadOnlyList<BarrioDto>> ListarBarriosDtoAsync(CancellationToken ct = default);
    Task<BarrioDto?> GetBarrioAsync(string codigo, CancellationToken ct = default);
    Task<BarrioDto> CrearBarrioAsync(BarrioCreateDto dto, string usuario, CancellationToken ct = default);
    Task<BarrioDto> ActualizarBarrioAsync(string codigo, BarrioUpdateDto dto, string usuario, CancellationToken ct = default);
    Task EliminarBarrioAsync(string codigo, CancellationToken ct = default);

    // ClaseMedidor
    Task<IReadOnlyList<ClaseMedidorLookupDto>> GetClasesMedidorAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ClaseMedidorDto>> ListarClasesMedidorDtoAsync(CancellationToken ct = default);
    Task<ClaseMedidorDto?> GetClaseMedidorAsync(string codigo, CancellationToken ct = default);
    Task<ClaseMedidorDto> CrearClaseMedidorAsync(ClaseMedidorCreateDto dto, string usuario, CancellationToken ct = default);
    Task<ClaseMedidorDto> ActualizarClaseMedidorAsync(string codigo, ClaseMedidorUpdateDto dto, string usuario, CancellationToken ct = default);
    Task EliminarClaseMedidorAsync(string codigo, CancellationToken ct = default);
}
