using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Conceptos;

public sealed class ConceptoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El depto es obligatorio.")]
    [StringLength(2, ErrorMessage = "El depto no puede superar 2 caracteres.")]
    public string Depto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo es obligatorio.")]
    [StringLength(2, ErrorMessage = "El tipo no puede superar 2 caracteres.")]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion es obligatoria.")]
    [StringLength(80, ErrorMessage = "La descripcion no puede superar 80 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El concepto es obligatorio.")]
    [StringLength(200, ErrorMessage = "El concepto no puede superar 200 caracteres.")]
    public string Concepto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El depto app mi trabajo es obligatorio.")]
    [StringLength(2, ErrorMessage = "El depto app mi trabajo no puede superar 2 caracteres.")]
    public string DeptoAppMiTrabajo { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
