using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class BodegaEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Direccion { get; set; }

    [StringLength(100)]
    public string? Responsable { get; set; }

    public bool Activo { get; set; } = true;
}
