namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Qué clase de movimiento postea el motor de inventario. Distinto de
/// <c>alm_kardex.tipo_transaccion</c> (código legacy de SIMAFI: '102' entrada,
/// '103' ajuste, '202' salida) y de <c>documento_tipo</c> (qué documento lo originó).
/// <para>
/// En esta entrega el motor implementa solo la apertura, los ajustes y la reversa;
/// compras, requisiciones, descargos y traslados llegan con sus propios documentos y
/// por ahora lanzan <see cref="System.NotSupportedException"/>.
/// </para>
/// </summary>
public enum TipoMovimientoInventario
{
    /// <summary>
    /// Apertura de un par (artículo, bodega) que ACABA de nacer: su existencia previa
    /// debe ser 0 y la cantidad la teclea el usuario. Alta de artículo o de bodega.
    /// </summary>
    CargaInicialNueva = 1,

    /// <summary>
    /// Apertura RETROACTIVA de un par que ya trae existencia backfilleada del cierre de
    /// SIMAFI. El asiento DESCRIBE lo que ya hay: la cantidad es la existencia leída, no
    /// un parámetro externo, y la existencia de la fila no cambia. Solo siembra el costo.
    /// </summary>
    CargaInicialReconciliacion = 2,

    /// <summary>Ajuste que aumenta la existencia (sobrante de conteo físico, devolución).</summary>
    AjustePositivo = 3,

    /// <summary>Ajuste que disminuye la existencia (faltante de conteo físico, merma).</summary>
    AjusteNegativo = 4,

    /// <summary>Ajuste que solo corrige el costo: cantidad 0, no mueve existencia.</summary>
    AjusteValor = 5,

    /// <summary>Contra-asiento que anula uno previo. El kardex es inmutable: no se borra, se revierte.</summary>
    Reversa = 6,

    /// <summary>
    /// Entrada por compra a proveedor (recepción de factura). Mueve existencia y recalcula el
    /// costo con el mismo promedio ponderado móvil que <see cref="AjustePositivo"/>; lo que
    /// cambia es la trazabilidad: el asiento queda como COMPRA, no como ajuste.
    /// </summary>
    Compra = 7,

    /// <summary>
    /// Salida de bodega por entrega de materiales (descargo). Sale al costo PROMEDIO vigente
    /// del par —el <c>CostoUnitario</c> del DTO se IGNORA— y NO altera el promedio, igual que
    /// cualquier salida.
    /// <para>
    /// El documento es la <b>línea de descargo</b> (<c>alm_descargo.id</c>), nunca la línea de
    /// requisición: una misma línea de requisición se entrega en varios descargos (28 casos
    /// medidos en el histórico del mirror). Anclar la identidad a la requisición haría que el
    /// segundo despacho colisionara con el primero y saliera mercadería sin asiento.
    /// </para>
    /// </summary>
    SalidaDescargo = 8,

    /// <summary>
    /// Traslado entre bodegas — lado de ENVÍO: sale de la bodega origen al costo promedio vigente
    /// (mismo cálculo que <see cref="AjusteNegativo"/>: resta existencia sin mover el promedio).
    /// Lo que la distingue de un ajuste es su trazabilidad: el asiento queda como TRASLADO (no
    /// ajuste) y lleva <see cref="MovimientoInventarioDto.BodegaDestinoId"/> apuntando al destino.
    /// El documento es la <b>línea del traslado</b> (<c>alm_movimiento_dtl.id</c>).
    /// </summary>
    TrasladoSalida = 9,

    /// <summary>
    /// Traslado entre bodegas — lado de RECEPCIÓN: entra a la bodega destino con promedio
    /// ponderado móvil (mismo cálculo que <see cref="AjustePositivo"/>) al costo con que salió de
    /// origen. El asiento queda como TRASLADO. El documento es la <b>línea de recepción</b>
    /// (<c>alm_traslado_recepcion_dtl.id</c>), nunca la del traslado: un renglón se recibe en
    /// varias recepciones parciales, así que anclar la identidad al traslado haría que la segunda
    /// recepción colisionara con la primera (mismo motivo que <see cref="SalidaDescargo"/>).
    /// </summary>
    TrasladoEntrada = 10
}
