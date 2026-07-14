using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Impuestos;

public sealed class ImpuestoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}
