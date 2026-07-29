using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public sealed class ProveedorContactoDto
{
    public long? ProveedorContactoId { get; set; }

    public long? TipoContactoId { get; set; }

    /// <summary>Solo lectura: nombre del tipo resuelto para mostrar en el detalle.</summary>
    public string? TipoContacto { get; set; }

    [StringLength(150, ErrorMessage = "El nombre del contacto no puede superar 150 caracteres.")]
    public string? Nombre { get; set; }

    [StringLength(100, ErrorMessage = "El cargo no puede superar 100 caracteres.")]
    public string? Cargo { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; set; }

    [StringLength(10, ErrorMessage = "La extensión no puede superar 10 caracteres.")]
    public string? Extension { get; set; }

    [StringLength(30, ErrorMessage = "El celular no puede superar 30 caracteres.")]
    public string? Celular { get; set; }

    [StringLength(150, ErrorMessage = "El email no puede superar 150 caracteres.")]
    public string? Email { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar 500 caracteres.")]
    public string? Observaciones { get; set; }

    public int Orden { get; set; }
}
