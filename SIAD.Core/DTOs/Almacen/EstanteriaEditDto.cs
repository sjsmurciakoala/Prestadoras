using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class EstanteriaEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "La bodega es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una bodega.")]
    public int BodegaId { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string? Nombre { get; set; }

    public bool Activo { get; set; } = true;
}
