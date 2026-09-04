using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Órdenes de compra a proveedores. La orden se crea en Borrador, se aprueba
/// (transición protegida por permiso desde el controlador) y luego la recepción
/// (alm_compra) la lleva a Recibida parcial / Cerrada. Multiempresa: el filtro y el
/// estampado de company_id los aplica SiadDbContext.
/// </summary>
public interface IOrdenCompraService
{
    /// <summary>Listado con filtros (proveedor, estado, rango de fechas, búsqueda).</summary>
    Task<IReadOnlyList<OrdenCompraListItemDto>> GetAsync(OrdenCompraFilterDto? filtro, CancellationToken ct = default);

    /// <summary>Orden completa (cabecera + renglones) por id, o null si no existe en la empresa actual.</summary>
    Task<OrdenCompraDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Datos de impresión del comprobante (PDF) de la orden: empresa emisora + la orden + total en letras. Null si no existe.</summary>
    Task<OrdenCompraImpresionDto?> GetDatosImpresionAsync(int id, string impresoPor, CancellationToken ct = default);

    /// <summary>
    /// Crea una orden en Borrador. Valida el proveedor y que cada artículo tenga código
    /// de proveedor activo (regla D-B); asigna el correlativo por empresa y calcula totales.
    /// </summary>
    Task<OrdenCompraDto> CrearAsync(OrdenCompraDto dto, string user, CancellationToken ct = default);

    /// <summary>Edita una orden en Borrador (reemplaza sus renglones). Falla si ya no está en Borrador.</summary>
    Task<OrdenCompraDto> ActualizarAsync(int id, OrdenCompraDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Aprueba la orden y estampa quién/cuándo. El permiso se valida en el controlador.
    /// <para>
    /// <b>Sin escalera</b> (control apagado, el estado de fábrica): Borrador → Aprobada en un paso,
    /// comprometiendo presupuesto. <b>Con escalera</b>: equivale a firmar el nivel pendiente, y la
    /// orden solo pasa a Aprobada cuando firma el último — ver <see cref="FirmarAprobacionAsync"/>.
    /// </para>
    /// </summary>
    Task<bool> AprobarAsync(int id, string user, CancellationToken ct = default);

    /// <summary>
    /// Envía una orden en Borrador a la escalera de firmas (Borrador → En aprobación). Deja de ser
    /// editable. Solo con el control encendido; no compromete presupuesto todavía.
    /// </summary>
    Task<bool> EnviarAAprobacionAsync(int id, string user, CancellationToken ct = default);

    /// <summary>
    /// Firma el nivel pendiente de una orden En aprobación. La primera firma compromete el
    /// presupuesto (D2) y la última aprueba la orden. Null si la orden no existe.
    /// </summary>
    Task<OrdenCompraAprobacionResultadoDto?> FirmarAprobacionAsync(
        int id, string? comentario, string user, CancellationToken ct = default);

    /// <summary>
    /// Devuelve a Borrador una orden en aprobación: borra las firmas (D4) y libera lo reservado.
    /// Motivo obligatorio.
    /// </summary>
    Task<bool> DevolverABorradorAsync(int id, string motivo, string user, CancellationToken ct = default);

    /// <summary>Anula la orden. Solo si ningún renglón tiene cantidad aplicada (no se recibió nada).</summary>
    Task<bool> AnularAsync(int id, string user, CancellationToken ct = default);

    /// <summary>
    /// Rechaza una orden en Borrador o en aprobación (estado 5). Motivo obligatorio. Desde la
    /// escalera, además libera el presupuesto que las firmas hubieran reservado.
    /// </summary>
    Task<bool> RechazarAsync(int id, string motivo, string user, CancellationToken ct = default);

    /// <summary>
    /// Cancela una orden aprobada o recibida en parte cuyo saldo ya no se va a recibir (estado 6).
    /// Libera el saldo comprometido pendiente, no el total pedido. Motivo obligatorio.
    /// </summary>
    Task<bool> CancelarAsync(int id, string motivo, string user, CancellationToken ct = default);

    /// <summary>
    /// Cierra anticipadamente una orden recibida en parte, dando por suficiente lo recibido
    /// (estado 4). Libera el saldo comprometido pendiente. Motivo obligatorio.
    /// </summary>
    Task<bool> CerrarAsync(int id, string motivo, string user, CancellationToken ct = default);

    /// <summary>
    /// Artículos que se le pueden comprar a un proveedor: los que existen en almacén CON su
    /// código de proveedor (relación activa y <c>codigo_upc</c> con valor). Es la misma regla
    /// que valida el alta, expuesta para que la pantalla sólo ofrezca lo comprable.
    /// </summary>
    Task<IReadOnlyList<OrdenCompraArticuloLookupDto>> BuscarArticulosProveedorAsync(
        string codProveedor, string? search, CancellationToken ct = default);
}
