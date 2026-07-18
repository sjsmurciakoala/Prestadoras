using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class TipoArticuloEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres.")]
    public string? Descripcion { get; set; }

    /// <summary>Cuentas contables (código del plan de cuentas). Opcionales.</summary>
    [StringLength(25, ErrorMessage = "La cuenta de inventario no puede superar 25 caracteres.")]
    public string? CuentaInventario { get; set; }

    [StringLength(25, ErrorMessage = "La cuenta de costo de ventas no puede superar 25 caracteres.")]
    public string? CuentaCostoVentas { get; set; }

    [StringLength(25, ErrorMessage = "La cuenta de ventas no puede superar 25 caracteres.")]
    public string? CuentaVentas { get; set; }

    [StringLength(25, ErrorMessage = "La cuenta de ajustes no puede superar 25 caracteres.")]
    public string? CuentaAjustes { get; set; }

    [StringLength(25, ErrorMessage = "La cuenta de devoluciones no puede superar 25 caracteres.")]
    public string? CuentaDevoluciones { get; set; }

    /// <summary>
    /// false = los artículos de este tipo no llevan existencias (ej. Servicios): sin
    /// bodega, sin ubicación y sin kardex. Por defecto true.
    /// </summary>
    public bool ManejaInventario { get; set; } = true;

    public bool Activo { get; set; } = true;
}
