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

    /// <summary>Aprueba (Borrador → Aprobada) y estampa quién/cuándo. El permiso se valida en el controlador.</summary>
    Task<bool> AprobarAsync(int id, string user, CancellationToken ct = default);

    /// <summary>Anula la orden. Solo si ningún renglón tiene cantidad aplicada (no se recibió nada).</summary>
    Task<bool> AnularAsync(int id, string user, CancellationToken ct = default);

    /// <summary>
    /// Artículos que se le pueden comprar a un proveedor: los que existen en almacén CON su
    /// código de proveedor (relación activa y <c>codigo_upc</c> con valor). Es la misma regla
    /// que valida el alta, expuesta para que la pantalla sólo ofrezca lo comprable.
    /// </summary>
    Task<IReadOnlyList<OrdenCompraArticuloLookupDto>> BuscarArticulosProveedorAsync(
        string codProveedor, string? search, CancellationToken ct = default);
}
