using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;

namespace SIAD.Services.NotasCreditoDebito;

public interface INotasCreditoDebitoService
{
    Task<IReadOnlyList<NotaClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default);
    Task<NotaClienteLookupDto?> ObtenerClienteAsync(string clienteClave, CancellationToken ct = default);
    Task<NotaClienteConfiguracionDto?> ObtenerConfiguracionClienteAsync(string clienteClave, CancellationToken ct = default);
    Task<IReadOnlyList<NotaMotivoDto>> ListarMotivosAsync(CancellationToken ct = default);
    Task<NotaMotivoDto?> ObtenerMotivoAsync(int motivoId, CancellationToken ct = default);
    Task<ResponseModelDto> RegistrarNotaAsync(NotaCrearRequestDto dto, CancellationToken ct = default);
}
