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

    /// <summary>
    /// Registra el movimiento bancario (partida contable + kardex) y, si el tipo de
    /// transaccion emite cheque, asigna el siguiente numero de la cuenta.
    /// <paramref name="beneficiarioCheque"/> / <paramref name="conceptoCheque"/> permiten
    /// separar el beneficiario del cheque de la descripcion del movimiento (cheque manual);
    /// si son nulos se usa la descripcion, como en el resto de los flujos.
    /// </summary>
    Task<(long BanKardexId, decimal SaldoResultante, long? ChequeId, decimal? NumeroCheque)> RegistrarMovimientoAsync(
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
        CancellationToken ct = default,
        string? beneficiarioCheque = null,
        string? conceptoCheque = null,
        string origenCheque = ChequeOrigen.Transaccion,
        string? descripcionPartidaBanco = null);

    /// <summary>
    /// Emite un cheque MANUAL (suelto, sin compromiso ni orden de pago): valida la cuenta
    /// y el tipo de transaccion, y delega en <see cref="RegistrarMovimientoAsync"/> con
    /// origen <see cref="ChequeOrigen.Manual"/>. El numero lo asigna el correlativo de la cuenta.
    /// </summary>
    Task<ChequeManualResultadoDto> RegistrarChequeManualAsync(
        ChequeManualCreateDto dto,
        string usuario,
        CancellationToken ct = default);

    Task<(long BanKardexIdAnulacion, decimal SaldoResultante)> AnularMovimientoAsync(
        long bancoCuentaId,
        long banKardexIdOriginal,
        string motivo,
        string usuario,
        CancellationToken ct = default);
}
