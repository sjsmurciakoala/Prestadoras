using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class EstanteEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "La estantería es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una estantería.")]
    public int EstanteriaId { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "La descripción no puede superar los 150 caracteres.")]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}
