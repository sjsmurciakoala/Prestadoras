using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Cuentas por pagar unificadas: las facturas de compra y los compromisos vistos como un solo
/// listado de documentos por pagar, y el pago de varios de ellos en una sola operación.
/// </summary>
public interface ICuentasPorPagarService
{
    Task<IReadOnlyList<CxpDocumentoDto>> ListarAsync(
        CxpUnificadaFilterDto? filtro, CancellationToken ct = default);

    Task<CxpResumenDto> ObtenerResumenAsync(
        CxpUnificadaFilterDto? filtro, CancellationToken ct = default);

    /// <summary>
    /// Paga varios documentos —de cualquiera de las dos ramas y de cualquier proveedor— dentro
    /// de una sola transacción: se registran todos o no se registra ninguno.
    /// </summary>
    Task<CxpLoteResultadoDto> PagarLoteAsync(
        CxpLoteUpsertDto dto, string user, CancellationToken ct = default);
}
