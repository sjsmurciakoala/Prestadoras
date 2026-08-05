using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Traslado entre bodegas (Fase 5). Un documento de clase TRASLADO con dos modos, elegidos por
/// <see cref="TrasladoDto.RequiereRecepcion"/>: con recepción (dos pasos, con tránsito) o directo
/// (un paso). Reutiliza <c>alm_movimiento_hdr/_dtl</c>, el correlativo y el motor
/// (<see cref="IInventarioPostingService"/>), que no cambia; el tránsito lo mantiene este servicio.
/// <para>Ver <c>docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md</c>.</para>
/// </summary>
public interface ITrasladoAlmacenService
{
    Task<IReadOnlyList<TrasladoListItemDto>> GetAsync(TrasladoFilterDto? filtro, CancellationToken ct = default);
    Task<TrasladoDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Crea el traslado y postea la salida de origen de cada renglón, cargando el tránsito de
    /// destino. Si <see cref="TrasladoDto.RequiereRecepcion"/> es <c>false</c> (directo), encadena
    /// la recepción total automática en la misma transacción y el documento nace Recibido.
    /// Idempotente por <see cref="TrasladoDto.Uuid"/> (obligatorio).
    /// </summary>
    Task<TrasladoDto> EnviarAsync(TrasladoDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Recibe una tanda (recepción parcial): por cada renglón indicado libera el tránsito y entra a
    /// destino al costo con que viajó. Sube <c>cantidad_recibida</c>; si el traslado queda completo,
    /// pasa a Recibido. Idempotente por el uuid del acto (<see cref="RecepcionTrasladoDto.Uuid"/>).
    /// </summary>
    Task<TrasladoDto> RecibirAsync(int trasladoId, RecepcionTrasladoDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Anula el traslado: revierte las entradas ya recibidas y las salidas de origen, y descarga el
    /// tránsito pendiente. Idempotente. Nunca hace UPDATE sobre el kardex.
    /// </summary>
    Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default);
}
