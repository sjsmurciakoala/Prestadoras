using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

public interface IProveedoresService
{
    Task EnsureProveedorGenericoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProveedorListItemDto>> GetProveedoresAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProveedorListItemDto>> SearchProveedoresAsync(ProveedorFilterDto filtro, CancellationToken cancellationToken = default);

    Task<ProveedorDetailDto?> GetProveedorAsync(string codigo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProveedorTipoLookupDto>> GetTiposAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProveedorBancoLookupDto>> GetBancosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProveedorBancoListItemDto>> GetBancosCatalogoAsync(CancellationToken cancellationToken = default);

    Task<ProveedorBancoDetailDto?> GetBancoAsync(long id, CancellationToken cancellationToken = default);

    Task<long> CreateBancoAsync(ProveedorBancoUpsertDto dto, string user, CancellationToken cancellationToken = default);

    Task UpdateBancoAsync(long id, ProveedorBancoUpsertDto dto, string user, CancellationToken cancellationToken = default);

    Task DeleteBancoAsync(long id, CancellationToken cancellationToken = default);

    Task<string> CreateAsync(ProveedorUpsertDto dto, string user, CancellationToken cancellationToken = default);

    Task UpdateAsync(string codigo, ProveedorUpsertDto dto, string user, CancellationToken cancellationToken = default);

    Task DeleteAsync(string codigo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoProveedorListItemDto>> GetTiposCatalogoAsync(CancellationToken cancellationToken = default);

    Task<TipoProveedorDetailDto?> GetTipoAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateTipoAsync(TipoProveedorUpsertDto dto, CancellationToken cancellationToken = default);

    Task UpdateTipoAsync(int id, TipoProveedorUpsertDto dto, CancellationToken cancellationToken = default);

    Task DeleteTipoAsync(int id, CancellationToken cancellationToken = default);
}
