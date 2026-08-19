using System;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Orden de posteo que recibe el motor de inventario. Describe QUÉ mover; el motor decide
/// el resultado (existencia y costo promedio posteriores) y escribe el asiento.
/// <para>
/// El <c>company_id</c> NO viaja aquí a propósito: lo resuelve el servidor con
/// <c>ICurrentCompanyService</c>. Un id de empresa en el body sería un vector de fuga
/// entre tenants.
/// </para>
/// </summary>
public sealed class MovimientoInventarioDto
{
    /// <summary>Qué clase de movimiento postear.</summary>
    public TipoMovimientoInventario Tipo { get; init; }

    /// <summary>Fila (artículo, bodega) afectada: <c>alm_articulo_bodega.id</c>.</summary>
    public int ArticuloBodegaId { get; init; }

    /// <summary>
    /// Cantidad del movimiento, SIEMPRE positiva: el signo lo determina <see cref="Tipo"/>.
    /// En <see cref="TipoMovimientoInventario.CargaInicialReconciliacion"/> debe coincidir
    /// con la existencia leída bajo bloqueo, o el posteo se rechaza.
    /// En <see cref="TipoMovimientoInventario.AjusteValor"/> debe ser 0.
    /// </summary>
    public decimal Cantidad { get; init; }

    /// <summary>
    /// Costo unitario de entrada. Obligatorio y &gt; 0 en aperturas y en ajustes de valor.
    /// Base: SIN ISV (decisión D2 del contador, 2026-07-29) — el ISV se calcula aparte y
    /// solo cuando el tipo de artículo tiene tasa configurada.
    /// </summary>
    public decimal CostoUnitario { get; init; }

    /// <summary>
    /// Fecha contable del asiento. Obligatoria: un asiento con fecha NULL se ordena al
    /// principio del kardex y desaparece de cualquier filtro por rango.
    /// </summary>
    public DateOnly Fecha { get; init; }

    /// <summary>Tipo de documento origen (<c>TipoDocumentoInventario</c>).</summary>
    public string DocumentoTipo { get; init; } = string.Empty;

    /// <summary>Id del documento origen dentro de la tabla que corresponda a <see cref="DocumentoTipo"/>.</summary>
    public int DocumentoId { get; init; }

    /// <summary>Asiento que se revierte. Solo en <see cref="TipoMovimientoInventario.Reversa"/>.</summary>
    public int? KardexIdRevertido { get; init; }

    /// <summary>
    /// Bodega destino, SOLO informativa, en los dos lados de un traslado
    /// (<see cref="TipoMovimientoInventario.TrasladoSalida"/> /
    /// <see cref="TipoMovimientoInventario.TrasladoEntrada"/>): se copia a
    /// <c>alm_kardex.bodega_destino_id</c>. La bodega AFECTADA por el asiento es siempre la del par
    /// (<see cref="ArticuloBodegaId"/>), no ésta. NULL en el resto de los movimientos.
    /// </summary>
    public int? BodegaDestinoId { get; init; }

    /// <summary>
    /// Si es <c>true</c>, el motor NO recalcula el rollup del artículo (<c>alm_articulo</c>) tras
    /// postear: lo hará el servicio llamador una sola vez por artículo, al final y en orden
    /// ascendente de <c>articulo_id</c>. Lo usa el traslado (multi-bodega) para no abrir un deadlock
    /// ABBA sobre <c>alm_articulo</c> cuando dos traslados tocan los mismos artículos en distinto
    /// orden. Default <c>false</c>: los demás flujos siguen recalculando por línea como hasta hoy.
    /// </summary>
    public bool DeferirRollup { get; init; }

    /// <summary>Texto libre que queda en el asiento (motivo del ajuste, origen del costo).</summary>
    public string? Observacion { get; init; }

    /// <summary>
    /// Si es <c>true</c>, una SALIDA DE DESCARGO puede dejar la existencia en negativo aunque el
    /// interruptor de existencia negativa (empresa/bodega) esté apagado: el usuario ya confirmó el
    /// negativo en la pantalla del descargo. Solo lo respeta <see cref="TipoMovimientoInventario.SalidaDescargo"/>;
    /// el resto de los movimientos sigue gobernado por <c>PermiteNegativoAsync</c>. Default <c>false</c>.
    /// </summary>
    public bool PermitirNegativo { get; init; }

    /// <summary>
    /// Discriminador de reintento del uuid de apertura: 1 + número de aperturas del par ya
    /// revertidas. Lo calcula el servicio; permite reabrir un par sin chocar con el índice
    /// único. Ver decisión 3 del diseño.
    /// </summary>
    public int Intento { get; init; } = 1;
}
