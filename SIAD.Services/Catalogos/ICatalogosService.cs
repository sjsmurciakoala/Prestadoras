using SIAD.Core.DTOs.Catalogos;

namespace SIAD.Services.Catalogos;

public interface ICatalogosService
{
    Task<IReadOnlyList<AbogadoLookupDto>> GetAbogadosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BarrioLookupDto>> GetBarriosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ServicioLookupDto>> GetServiciosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TipoUsoLookupDto>> GetTiposUsoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default);
}
