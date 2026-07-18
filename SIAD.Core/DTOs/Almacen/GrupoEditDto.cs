using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class GrupoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(6, ErrorMessage = "El código no puede superar los 6 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Tipo de artículo (alm_tipo_articulo) al que pertenece la categoría.</summary>
    public int? TipoArticuloId { get; set; }

    public bool Activo { get; set; } = true;
}
