using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Services.Presupuesto;

public interface IOrdenesPagoDirectoService
{
    Task<IReadOnlyList<OrdenPagoDirectoListItemDto>> GetAsync(
        OrdenPagoDirectoFilterDto? filtro,
        CancellationToken ct = default);

    Task<IReadOnlyList<OrdenPagoDirectoCentroCostoLookupDto>> GetCentrosCostoAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasGastoAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasContablesAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasContraProcesamientoAsync(
        CancellationToken ct = default);

    Task<OrdenPagoDirectoDetalleDto?> GetByNumeroOrdenAsync(
        int numeroOrden,
        CancellationToken ct = default);

    Task<OrdenPagoDirectoOperacionResultadoDto> CreateAsync(
        OrdenPagoDirectoUpsertDto dto,
        CancellationToken ct = default);

    Task<OrdenPagoDirectoOperacionResultadoDto> GenerarPartidaCreacionAsync(
        int numeroOrden,
        CancellationToken ct = default);

    Task<OrdenPagoDirectoOperacionResultadoDto> UpdateAsync(
        int numeroOrden,
        OrdenPagoDirectoUpsertDto dto,
        CancellationToken ct = default);

    Task<OrdenPagoDirectoOperacionResultadoDto> AnularAsync(
        int numeroOrden,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default);

    Task<OrdenPagoDirectoOperacionResultadoDto> MarkAsProcessedAsync(
        int numeroOrden,
        ProcesarOrdenPagoDirectoDto dto,
        CancellationToken ct = default);
}
