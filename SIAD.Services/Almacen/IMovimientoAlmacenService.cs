using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Documento de movimiento de almacén: entradas y salidas manuales multi-renglón.
/// Es el equivalente del <c>dlgTransaccionesGenericasINV</c> de Centura.
/// <para>
/// El comportamiento de inventario NO lo decide este servicio: lo decide la <c>clase</c>
/// del tipo de movimiento (ENTRADA · SALIDA · VALOR), que se traduce a
/// <c>TipoMovimientoInventario</c> y ejecuta <see cref="IInventarioPostingService"/> —el
/// único punto de escritura del kardex— sin cambios.
/// </para>
/// </summary>
public interface IMovimientoAlmacenService
{
    Task<IReadOnlyList<MovimientoAlmacenListItemDto>> GetAsync(
        MovimientoAlmacenFilterDto? filtro, CancellationToken ct = default);

    Task<MovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Crea el documento Y postea todos sus renglones en la MISMA transacción: o se registran
    /// todos los asientos o no se registra ninguno. Un documento sin asiento sería un papel
    /// sin efecto (mismo criterio que <see cref="IAjusteInventarioService"/>).
    /// <para>Idempotente por <see cref="MovimientoAlmacenDto.Uuid"/>.</para>
    /// </summary>
    /// <param name="puedeUsarTiposSensibles">
    /// ¿El llamador tiene <c>module.inventario.movimientos.autorizar_sensibles</c> (o es
    /// SuperAdministrador)? Lo resuelve el controller leyendo los claims; la REGLA vive aquí,
    /// para que valga por cualquier vía de entrada y sea comprobable con una prueba.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">
    /// El tipo está marcado <c>requiere_autorizacion</c> y el llamador no tiene el permiso.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Tipo inexistente, inactivo o de otra empresa; motivo vacío; sin renglones; artículo sin
    /// ubicación en la bodega; o cualquier guarda del motor (existencia insuficiente, costo 0).
    /// </exception>
    Task<MovimientoAlmacenDto> CrearYPostearAsync(
        MovimientoAlmacenDto dto, string user, bool puedeUsarTiposSensibles, CancellationToken ct = default);

    /// <summary>
    /// Anula el documento posteando una <c>Reversa</c> por renglón. Idempotente: anular dos
    /// veces no revierte dos veces. NUNCA hace UPDATE sobre el kardex, que es inmutable.
    /// </summary>
    /// <returns><c>false</c> si el documento no existe.</returns>
    /// <exception cref="InvalidOperationException">
    /// Si la mercadería de una entrada ya salió: revertir dejaría existencia negativa.
    /// </exception>
    Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default);

    /// <summary>
    /// ¿El tipo de movimiento ya tiene renglones posteados? Lo consulta
    /// <c>TipoMovimientoService</c> para bloquear el cambio de <c>clase</c>.
    /// </summary>
    Task<bool> TipoTieneMovimientosPosteadosAsync(int tipoMovimientoId, CancellationToken ct = default);
}
