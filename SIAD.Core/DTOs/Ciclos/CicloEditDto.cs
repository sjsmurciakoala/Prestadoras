using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Ciclos;

public sealed class CicloEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El codigo es obligatorio.")]
    [StringLength(50, ErrorMessage = "El codigo no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion corta es obligatoria.")]
    [StringLength(100, ErrorMessage = "La descripcion corta no puede superar los 100 caracteres.")]
    public string DescripcionCorta { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion larga es obligatoria.")]
    [StringLength(300, ErrorMessage = "La descripcion larga no puede superar los 300 caracteres.")]
    public string DescripcionLarga { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
