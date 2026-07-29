using Npgsql;
using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IChequesService
{
    /// <summary>
    /// Emite (asigna) el siguiente numero de cheque de la cuenta DENTRO de la
    /// transaccion del llamador: FOR UPDATE sobre ban_cuenta, valida agotamiento
    /// contra cheque_maximo, inserta ban_cheque ('E') e incrementa proximo_cheque.
    /// Lanza InvalidOperationException si la numeracion esta agotada.
    /// Devuelve el cheque_id (PK, para imprimir) y el numero de cheque asignado.
    /// </summary>
    Task<(long ChequeId, decimal NumeroCheque)> EmitirChequeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long bancoCuentaId,
        decimal monto,
        string? beneficiario,
        string? concepto,
        string origen,
        string? origenDocumento,
        long? banKardexId,
        long? partidaId,
        string usuario,
        DateTime fechaEmision,
        CancellationToken ct = default);

    /// <summary>
    /// Marca como anulado ('A') el cheque vigente vinculado a un ban_kardex.
    /// No-op (retorna false) si el movimiento no tiene cheque (DEP/TRF/etc.).
    /// </summary>
    Task<bool> AnularPorKardexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long banKardexIdOriginal,
        long banKardexIdReverso,
        string motivo,
        string usuario,
        CancellationToken ct = default);

    /// <summary>
    /// Consume el siguiente numero de la cuenta y lo registra ya anulado
    /// (cheque danado): origen MANUAL, monto 0, sin movimiento bancario.
    /// Abre su propia transaccion.
    /// </summary>
    Task<decimal> AnularSiguienteNumeroAsync(
        long bancoCuentaId,
        string motivo,
        string usuario,
        CancellationToken ct = default);

    Task<ProximoChequeDto?> GetProximoAsync(long bancoCuentaId, CancellationToken ct = default);

    Task<IReadOnlyList<ChequeListItemDto>> BuscarAsync(ChequeFilterDto filtro, CancellationToken ct = default);

    /// <summary>
    /// Consulta la bitacora de EVENTOS (ban_cheque_bitacora, append-only):
    /// una fila por evento EMITIDO/ANULADO. Orden fecha desc, tope 5000.
    /// </summary>
    Task<IReadOnlyList<ChequeBitacoraListItemDto>> BuscarBitacoraAsync(ChequeBitacoraFilterDto filtro, CancellationToken ct = default);

    /// <summary>
    /// Arma el DTO para imprimir un cheque (comprobante interno COMPAGOL y cheque
    /// para cliente COMPAGOLG): datos de empresa, del cheque, monto en letras y la
    /// distribucion contable de la partida ligada (partida_id), si existe.
    /// Devuelve null si el cheque no existe en la empresa actual.
    /// </summary>
    Task<ChequeImpresionDto?> GetDatosImpresionAsync(long chequeId, string impresoPor, CancellationToken ct = default);

    /// <summary>
    /// cheque_id del cheque VIGENTE ('E') ligado a un movimiento bancario (ban_kardex_id),
    /// para reimprimirlo desde el detalle de la transaccion. null si el movimiento no emitio cheque.
    /// </summary>
    Task<long?> GetChequeIdVigentePorKardexAsync(long banKardexId, CancellationToken ct = default);
}
