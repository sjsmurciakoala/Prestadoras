namespace SIAD.Services.Almacen;

/// <summary>
/// Recalcula el rollup de cabecera de un artículo (<c>alm_articulo.existencia</c>,
/// <c>existencia_minima</c> y <c>cantidad</c>) como la suma de sus ubicaciones ACTIVAS.
/// <para>
/// Existe como servicio propio porque el cálculo lo necesitan tres consumidores
/// (<c>ArticuloUbicacionService</c>, <c>ArticulosService.CreateAsync</c> y el motor de
/// posteo) y hasta ahora vivía encerrado en un método privado.
/// </para>
/// </summary>
public interface IArticuloRollupService
{
    /// <summary>
    /// Deja la cabecera del artículo cuadrada con la suma de sus bodegas activas.
    /// Idempotente: llamarlo dos veces da el mismo resultado.
    /// </summary>
    Task RecomputeAsync(int articuloId, CancellationToken ct = default);
}
