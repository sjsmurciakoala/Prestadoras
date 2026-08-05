using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Motor de posteo del inventario: la ÚNICA vía por la que la existencia y el costo de un
/// par (artículo, bodega) pueden cambiar dejando rastro en el kardex.
/// <para>
/// Hasta ahora la existencia se tecleaba y se persistía directo, sin asiento: no había
/// quién, cuándo, con qué documento ni a qué costo. Este servicio cierra ese hueco.
/// </para>
/// <para>
/// <b>Idempotente por uuid.</b> Cada movimiento deriva un UUIDv5 determinista de su
/// identidad; reintentar el mismo posteo devuelve el asiento existente
/// (<c>PosteoResultDto.YaExistia = true</c>) en vez de duplicarlo. Es indispensable en un
/// libro que la BD hace inmutable: un duplicado no se puede borrar, solo revertir.
/// </para>
/// </summary>
public interface IInventarioPostingService
{
    /// <summary>
    /// Postea un movimiento: bloquea la fila, valida, calcula el resultado, escribe el
    /// asiento y recalcula el rollup de cabecera. Todo dentro de una sola transacción
    /// (la del llamador si ya hay una abierta).
    /// </summary>
    /// <exception cref="InvalidOperationException">Regla de negocio incumplida.</exception>
    /// <exception cref="NotSupportedException">Tipo de movimiento aún no implementado.</exception>
    Task<PosteoResultDto> PostearAsync(MovimientoInventarioDto movimiento, string user, CancellationToken ct = default);

    /// <summary>
    /// true si el par (artículo, bodega) tiene una apertura VIGENTE: un
    /// <c>CARGA_INICIAL</c> cuya reversa no fue posteada. Se usa para impedir una segunda
    /// apertura y para decidir si una ubicación con existencia puede reactivarse.
    /// </summary>
    Task<bool> TieneAperturaVigenteAsync(int articuloId, int bodegaId, CancellationToken ct = default);
}
