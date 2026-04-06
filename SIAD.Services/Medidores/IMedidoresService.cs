using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Medidores;
namespace SIAD.Services.Medidores;

public interface IMedidoresService
{
    Task<IReadOnlyList<MedidorListDto>> SearchAsync(MedidorFilterDto filtro, CancellationToken ct = default);
    Task<MedidorDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<PagedResult<MedidorListItemDto>> GetPagedAsync(MedidorFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<MedidorEditDto?> GetEditByIdAsync(int id, CancellationToken ct = default);
    Task<MedidorEditDto> CreateAsync(MedidorEditDto dto, string user, CancellationToken ct = default);
    Task<MedidorEditDto> UpdateAsync(int id, MedidorEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);
    Task<IReadOnlyList<MedidorHistorialDto>> GetHistorialAsync(int medidorId, int take = 12, CancellationToken ct = default);
    Task<bool> AssignToClienteAsync(int medidorId, int clienteId, CancellationToken ct = default);
    Task RegistrarLecturaSinMedidorAsync(string clave, DateTime fecha, decimal lectura , string usuario, CancellationToken ct = default);   
}
