using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class CategoriaUnidadEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(30, ErrorMessage = "El nombre no puede superar los 30 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "La descripción no puede superar los 100 caracteres.")]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}
