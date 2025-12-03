using SIAD.Core.DTOs.Medidores;
namespace SIAD.Services.Medidores;

public interface IMedidoresService
{
    Task<IReadOnlyList<MedidorListDto>> SearchAsync(MedidorFilterDto filtro, CancellationToken ct = default);
    Task<MedidorDetailDto?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<MedidorHistorialDto>> GetHistorialAsync(int medidorId, int take = 12, CancellationToken ct = default);
    Task<bool> AssignToClienteAsync(int medidorId, int clienteId, CancellationToken ct = default);
    Task RegistrarLecturaSinMedidorAsync(string clave, DateTime fecha, decimal lectura , string usuario, CancellationToken ct = default);   
}