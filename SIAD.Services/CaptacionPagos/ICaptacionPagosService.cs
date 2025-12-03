using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.CaptacionPagos;

public interface ICaptacionPagosService
{
    Task<IReadOnlyList<CajaDto>> ListarCatalogoCajasAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ArqueoDto>> ListarArqueosAsync(CaptacionArqueoFilterDto? filtro, CancellationToken ct = default);

    Task<CaptacionHeaderDto?> ObtenerDetallePagoAsync(string numFactura, CancellationToken ct = default);

    Task<CaptacionPagoResponseDto?> ObtenerPagoAsync(string numFactura, CancellationToken ct = default);

    Task<IReadOnlyList<CaptacionDetailDto>> ObtenerDetallePagoLineasAsync(string numFactura, CancellationToken ct = default);

    Task<ResponseModelDto> RegistrarPagoAsync(PagoCrearDto dto, CancellationToken ct = default);

    Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<ReciboMiscelaneoDto>> ListarPagosMiscelaneosAsync(string? clienteClave, CancellationToken ct = default);
}
