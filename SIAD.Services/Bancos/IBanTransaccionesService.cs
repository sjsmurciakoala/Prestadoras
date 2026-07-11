using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IBanTransaccionesService
{
    Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(
        long companyId,
        long? bancoId = null,
        long? bancoCuentaId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        bool incluirAnuladas = false,
        CancellationToken ct = default);

    Task<BanTransaccionListDto?> GetTransaccionByIdAsync(
        long banKardexId,
        long companyId,
        CancellationToken ct = default);

    Task<BanTransaccionDetalleDto?> GetTransaccionDetalleAsync(
        long banKardexId,
        long companyId,
        CancellationToken ct = default);

    /// <summary>
    /// Arma el estado de cuenta de una cuenta bancaria para el período dado
    /// (saldo anterior + movimientos + totales + saldo final). Devuelve null si la cuenta no existe.
    /// </summary>
    Task<EstadoCuentaDto?> GetEstadoCuentaAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken ct = default);

    Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoAsync(
        long bancoCuentaId,
        string idTipoTransaccion,
        DateOnly fechaMovimiento,
        string descripcion,
        string? referencia,
        string? sourceDocument,
        decimal tasaCambio,
        decimal monto,
        IReadOnlyList<BanTransaccionContraLineaDto> contraCuentas,
        string usuario,
        CancellationToken ct = default);

    Task<(long BanKardexIdAnulacion, decimal SaldoResultante)> AnularMovimientoAsync(
        long bancoCuentaId,
        long banKardexIdOriginal,
        string motivo,
        string usuario,
        CancellationToken ct = default);
}
