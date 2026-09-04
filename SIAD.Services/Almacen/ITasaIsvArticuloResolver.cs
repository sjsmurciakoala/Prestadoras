namespace SIAD.Services.Almacen;

/// <summary>
/// Tasa de ISV de compras resuelta para un artículo (capa 1 de la política de ISV:
/// <c>alm_tipo_articulo.impuesto_tasa_id</c> → <c>cfg_impuesto_tasa</c> vigente a la fecha).
/// </summary>
/// <param name="Porcentaje">
/// Porcentaje vigente a la fecha consultada. 0 cuando el artículo no tiene tipo, el tipo no
/// tiene tasa asignada, o la tasa asignada es EXENTA / no está vigente a esa fecha.
/// </param>
/// <param name="TipoTieneTasa">
/// El tipo del artículo tiene una tasa de ISV asignada (<c>impuesto_tasa_id</c> no nulo),
/// aunque sea EXENTA. Sirve para distinguir "sin ISV configurado" (hay que avisar) de
/// "configurado como 0%" (exento a propósito), que NO se avisa.
/// </param>
/// <param name="TipoNombre">Nombre del tipo del artículo (para mensajes). Null si el artículo no tiene tipo.</param>
public readonly record struct TasaIsvArticulo(decimal Porcentaje, bool TipoTieneTasa, string? TipoNombre);

/// <summary>
/// Resuelve la tasa de ISV de compras por artículo (capa 1). Fuente ÚNICA de esa regla, que
/// consumen tanto las órdenes de compra como la recepción de facturas de proveedor.
/// </summary>
public interface ITasaIsvArticuloResolver
{
    /// <summary>
    /// Resuelve la tasa de ISV de compras de cada artículo. Devuelve SIEMPRE una entrada por
    /// cada id consultado (los artículos sin tipo o sin tasa salen con <c>Porcentaje</c> 0 y
    /// <c>TipoTieneTasa</c> false), para que el llamador pueda distinguir "no configurado" de "0%".
    /// La vigencia se evalúa a <paramref name="fecha"/> (la del documento), no a la de hoy.
    /// </summary>
    Task<IReadOnlyDictionary<int, TasaIsvArticulo>> ResolverAsync(
        IReadOnlyCollection<int> articuloIds, DateOnly fecha, CancellationToken ct = default);
}
