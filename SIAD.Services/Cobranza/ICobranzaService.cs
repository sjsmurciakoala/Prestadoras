using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Cobranza;

public interface ICobranzaService
{
    Task<IReadOnlyList<CobranzaSaldoDetalleDto>> ObtenerSaldosClienteAsync(string clienteClave, CancellationToken ct = default);
    Task<bool> EstaBloqueadoAsync(string clienteClave, CancellationToken ct = default);
    Task<string?> NumeroALetrasAsync(decimal numero, CancellationToken ct = default);
    Task<CobranzaPlanPreviewDto> CalcularCuotasAsync(CobranzaPlanPreviewRequestDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> GuardarPlanPagoAsync(CobranzaPlanGuardarDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<CobranzaPlanResumenDto>> ListarPlanesAsync(CancellationToken ct = default);
    Task<CobranzaPlanDetalleDto?> ObtenerPlanAsync(string correlativo, CancellationToken ct = default);
}
