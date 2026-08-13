using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Pagos a proveedores sobre las cuentas por pagar de compra (alm_compra_cxp): la vista
/// unificada (contado + crédito) y el registro/anulación de abonos con movimiento bancario.
/// </summary>
public interface ICompraCxpService
{
    Task<IReadOnlyList<CompraCxpListItemDto>> ListarAsync(CompraCxpFilterDto? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<CompraCxpAbonoListItemDto>> ListarAbonosAsync(int cxpId, CancellationToken ct = default);
    Task<IReadOnlyList<CuentaBancariaLookupDto>> ListarCuentasBancariasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CompraCuentaContableLookupDto>> ListarCuentasContablesAsync(CancellationToken ct = default);
    Task<bool> ObtenerContabilidadActivaAsync(CancellationToken ct = default);
    Task<CompraCxpPartidaDto?> ObtenerPartidaAbonoAsync(int cxpId, int numeroAbono, CancellationToken ct = default);
    Task<PagoCompraImpresionDto?> GetDatosImpresionPagoAsync(int cxpId, int numeroAbono, string impresoPor, CancellationToken ct = default);
    Task<PartidaContableImpresionDto?> GetDatosImpresionPartidaPagoAsync(int cxpId, int numeroAbono, string impresoPor, CancellationToken ct = default);
    Task<CompraCxpAbonoResultadoDto> RegistrarAbonoAsync(int cxpId, CompraCxpAbonoUpsertDto dto, string user, CancellationToken ct = default);
    Task<bool> AnularAbonoAsync(int cxpId, int numeroAbono, string motivo, string user, CancellationToken ct = default);
}
