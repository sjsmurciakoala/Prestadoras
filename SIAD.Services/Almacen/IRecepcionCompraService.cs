using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Recepción de compras (factura de proveedor): el alta que entra mercadería al inventario.
/// <para>
/// Cada renglón nace como una línea de <c>alm_compra</c> y se postea al kardex con el motor
/// (<c>TipoMovimientoInventario.Compra</c>), así que la existencia y el costo promedio del
/// par (artículo, bodega) cambian por la vía auditada, nunca por un UPDATE suelto.
/// </para>
/// <para>
/// Modo Con O/C: una orden admite <b>varias facturas</b>. Cada recepción descarga
/// <c>cantidad_aplicada</c> en los renglones que recibe y la orden pasa a Recibida parcial;
/// cuando todos sus renglones quedan cubiertos, a Cerrada.
/// </para>
/// </summary>
public interface IRecepcionCompraService
{
    /// <summary>Listado con filtros (proveedor, estado, O/C, rango de fechas, búsqueda).</summary>
    Task<IReadOnlyList<RecepcionCompraListItemDto>> GetAsync(RecepcionCompraFilterDto? filtro, CancellationToken ct = default);

    /// <summary>Recepción completa (cabecera + renglones), o null si no existe en la empresa actual.</summary>
    Task<RecepcionCompraDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Registra la factura y postea sus renglones al kardex, todo en una transacción: o entra
    /// completa o no entra nada.
    /// <para>
    /// Valida el proveedor, que cada artículo tenga código de proveedor (regla D-B), el costo
    /// &gt; 0 (regla D-E) y —en modo Con O/C— que no se reciba más de lo pendiente. Resuelve el
    /// ISV por el tipo de artículo y decide si capitaliza al costo según la política de la
    /// empresa (<c>cfg_compra_isv</c>) o el override de la factura.
    /// </para>
    /// <para>
    /// Idempotente por <see cref="RecepcionCompraDto.Uuid"/>: reenviar el mismo documento
    /// devuelve el ya registrado en vez de duplicarlo.
    /// </para>
    /// </summary>
    Task<RecepcionCompraDto> CrearAsync(RecepcionCompraDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Anula una recepción registrada: revierte en el kardex el asiento de cada renglón,
    /// devuelve lo recibido a la orden de compra (si la hubo) y marca la factura como Anulada.
    /// Todo en una transacción.
    /// <para>
    /// El kardex es inmutable: nada se borra, se postea un contra-asiento REVERSA por cada
    /// línea. Por eso la anulación exige que la mercadería <b>siga estando</b>: si ya se
    /// consumió, la reversa dejaría la existencia en negativo y la operación se rechaza (hay
    /// que corregir con un ajuste, no con una anulación).
    /// </para>
    /// <para>
    /// El <b>costo promedio no vuelve atrás</b>: el promedio ponderado móvil no es reversible
    /// sin reprocesar todo el histórico del par. La reversa saca la cantidad al costo con que
    /// entró y deja el promedio vigente.
    /// </para>
    /// </summary>
    /// <param name="motivo">Texto opcional; se anexa a las observaciones del documento.</param>
    /// <returns>false si la recepción no existe en la empresa actual.</returns>
    Task<bool> AnularAsync(int id, string? motivo, string user, CancellationToken ct = default);

    /// <summary>
    /// Renglones de una O/C con lo que falta por recibir (pendiente = pedida − aplicada).
    /// Devuelve solo los que tienen pendiente &gt; 0; lista vacía si la orden no existe, no es
    /// de la empresa actual o no está en un estado recibible.
    /// </summary>
    Task<IReadOnlyList<OrdenCompraPendienteDto>> ObtenerPendientesOrdenAsync(int ordenCompraId, CancellationToken ct = default);

    /// <summary>
    /// Órdenes de compra que todavía admiten recepción (Aprobadas o Recibidas parcialmente),
    /// opcionalmente filtradas por proveedor. Es lo que alimenta el combo de O/C.
    /// </summary>
    Task<IReadOnlyList<OrdenCompraListItemDto>> ObtenerOrdenesRecibiblesAsync(string? codProveedor, CancellationToken ct = default);
}
