using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.CaptacionPagos;

public interface ICaptacionPagosService
{
    // ==================== EXISTENTE ====================
    Task<IReadOnlyList<CajaDto>> ListarCatalogoCajasAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ArqueoDto>> ListarArqueosAsync(CaptacionArqueoFilterDto? filtro, CancellationToken ct = default);

    Task<PagedResult<ArqueoDto>> ListarArqueosPagedAsync(
        CaptacionArqueoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default);

    Task<CaptacionHeaderDto?> ObtenerDetallePagoAsync(string numFactura, CancellationToken ct = default);

    Task<CaptacionPagoResponseDto?> ObtenerPagoAsync(string numFactura, CancellationToken ct = default);

    Task<IReadOnlyList<CaptacionDetailDto>> ObtenerDetallePagoLineasAsync(string numFactura, CancellationToken ct = default);

    Task<ResponseModelDto> RegistrarPagoAsync(PagoCrearDto dto, CancellationToken ct = default);

    Task<ResponseModelDto> ReversarPagoAsync(ReversoRequestDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<ReciboMiscelaneoDto>> ListarPagosMiscelaneosAsync(string? clienteClave, CancellationToken ct = default);

    Task<PagedResult<ReciboMiscelaneoDto>> ListarPagosMiscelaneosPagedAsync(
        string? clienteClave,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default);

    // ==================== AUTOCOMPLETADO Y BÚSQUEDA ====================
    Task<IReadOnlyList<BusquedaFacturaDto>> BuscarFacturasAsync(string term, CancellationToken ct = default);

    Task<bool> ExisteRegistroPagoAsync(string numFactura, CancellationToken ct = default);

    // ==================== POSTEO MANUAL ====================
    Task<IReadOnlyList<SaldoPosteoManualDto>> ObtenerSaldosPosteoManualAsync(string clienteClave, CancellationToken ct = default);

    Task<ResponseModelDto> RegistrarPagoManualAsync(PagoManualCrearDto dto, CancellationToken ct = default);

    Task<ResponseModelDto> ReversarPagoManualAsync(ReversoManualRequestDto dto, CancellationToken ct = default);

    // ==================== POSTEO MISCELÁNEOS ====================
    Task<IReadOnlyList<ReciboMiscelaneoDetalleDto>> ObtenerDetalleReciboMiscelaneoAsync(long recibo, CancellationToken ct = default);

    Task<ResponseModelDto> RegistrarPagoMiscelaneoAsync(PagoMiscelaneoCrearDto dto, CancellationToken ct = default);

    Task<ResponseModelDto> ReversarPagoMiscelaneoAsync(ReversoMiscelaneoRequestDto dto, CancellationToken ct = default);

    // ==================== COMBOS Y AUXILIARES ====================
    Task<IReadOnlyList<ClienteComboDto>> ListarClientesAsync(string? query = null, CancellationToken ct = default);

    Task<IReadOnlyList<BancoDto>> ListarBancosAsync(CancellationToken ct = default);

    Task<PeriodoActualDto> ObtenerPeriodoActualAsync(CancellationToken ct = default);
}
