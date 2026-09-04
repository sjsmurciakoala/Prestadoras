using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Carga inicial de existencias: el corte que convierte el inventario tecleado en asientos
/// de kardex con su costo.
/// <para>
/// Dos modos, según de dónde venga el par:
/// </para>
/// <list type="bullet">
///   <item><b>Unitario</b> (<see cref="PostearAperturaAsync"/>): el par acaba de nacer con
///   un alta de artículo o de bodega. Existencia previa 0, cantidad tecleada, fecha = hoy.</item>
///   <item><b>Lote retroactivo</b> (<see cref="EjecutarLoteAsync"/>): el par ya trae la
///   existencia del cierre de SIMAFI. El asiento la DESCRIBE (no la suma) y solo siembra
///   el costo, con la fecha de corte.</item>
/// </list>
/// </summary>
public interface ICargaInicialInventarioService
{
    /// <summary>Apertura unitaria de un par recién creado. Fecha = hoy.</summary>
    Task<PosteoResultDto> PostearAperturaAsync(
        int articuloId, int bodegaId, decimal cantidad, decimal costo, string user, CancellationToken ct = default);

    /// <summary>
    /// Universo del corte: pares con existencia y SIN apertura vigente, clasificados por lo
    /// que impide postearlos (sin costo, negativos, descontinuados, bodega inactiva).
    /// <paramref name="bodegaId"/> acota a una bodega; null = todas.
    /// </summary>
    Task<IReadOnlyList<CargaInicialPendienteDto>> GetPendientesAsync(int? bodegaId = null, CancellationToken ct = default);

    /// <summary>Dry-run: cuántos, de qué clase y cuánto valor. NO escribe nada.</summary>
    Task<CargaInicialSimulacionDto> SimularLoteAsync(int? bodegaId = null, CancellationToken ct = default);

    /// <summary>
    /// Postea el lote retroactivo con la fecha de corte dada (obligatoria; se persiste en
    /// la configuración). Idempotente: repetirlo no duplica, lo impide el uuid.
    /// <para>
    /// <paramref name="bodegaId"/> permite cortar UNA bodega a la vez, que es como se
    /// opera un almacén grande: se cierra un depósito, se verifica y se pasa al siguiente.
    /// null = todas.
    /// </para>
    /// </summary>
    Task<CargaInicialResultadoDto> EjecutarLoteAsync(
        DateOnly fechaCorte, int tamanoLote, string user, int? bodegaId = null, CancellationToken ct = default);

    /// <summary>
    /// Flujo de excepción de los pares sin costo: el costo tecleado entra DIRECTO al
    /// asiento, no se guarda en ninguna tabla intermedia — el asiento es el registro.
    /// </summary>
    Task<CargaInicialResultadoDto> PostearConCostoManualAsync(
        IReadOnlyList<CargaInicialCostoManualDto> items, DateOnly fechaCorte, string user, CancellationToken ct = default);

    /// <summary>
    /// Revierte la apertura vigente del par y la vuelve a abrir con el costo correcto, en
    /// una sola transacción. Rechaza si el par tiene movimientos posteriores a la apertura:
    /// ahí la corrección es un AJUSTE, porque revertir el punto cero dejaría colgando todo
    /// lo que vino después.
    /// </summary>
    Task<PosteoResultDto> ReabrirAsync(
        int articuloId, int bodegaId, decimal nuevoCosto, string motivo, string user, CancellationToken ct = default);

    /// <summary>Marca el corte como cerrado tras verificar que no quedan pares sin costo ni negativos.</summary>
    Task CerrarAperturaAsync(string user, CancellationToken ct = default);

    Task<CargaInicialConfigDto> GetConfigAsync(CancellationToken ct = default);
}
