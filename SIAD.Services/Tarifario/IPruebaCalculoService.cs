using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

public interface IPruebaCalculoService
{
    Task<PruebaCalculoResultDto> CalcularAsync(PruebaCalculoRequest request, CancellationToken ct = default);
}
