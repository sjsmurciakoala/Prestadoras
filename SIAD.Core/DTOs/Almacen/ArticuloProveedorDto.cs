using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Relación proveedor ↔ artículo ("UPC") mostrada/editada en la pestaña Proveedores
/// del artículo. El proveedor se identifica por su código (prv_proveedores.cod_proveedor).
/// </summary>
public sealed class ArticuloProveedorDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El proveedor es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código de proveedor no puede superar 20 caracteres.")]
    public string CodProveedor { get; set; } = string.Empty;

    /// <summary>Solo lectura: nombre del proveedor (para la grilla).</summary>
    public string? ProveedorNombre { get; set; }

    /// <summary>Solo lectura: "código — nombre" del proveedor.</summary>
    public string ProveedorDisplay =>
        string.IsNullOrWhiteSpace(ProveedorNombre) ? CodProveedor : $"{CodProveedor} — {ProveedorNombre}";

    /// <summary>Código/UPC con que el proveedor identifica el artículo (opcional).</summary>
    [StringLength(40, ErrorMessage = "El código UPC no puede superar 40 caracteres.")]
    public string? CodigoUpc { get; set; }

    // NOTA: el costo ya no se maneja aquí. El costo del artículo vive en Existencias
    // (alm_articulo_bodega.costo_promedio / ultimo_costo), que lo mantiene el motor de
    // movimientos. La columna alm_articulo_proveedor.costo sigue existiendo en la BD
    // (no se borró, para no perder el histórico), pero no se expone ni se escribe.

    /// <summary>Proveedor principal/preferido (a lo sumo uno por artículo).</summary>
    public bool Principal { get; set; }

    /// <summary>Soft-delete: false = relación deshabilitada (histórico).</summary>
    public bool Activo { get; set; } = true;
}
