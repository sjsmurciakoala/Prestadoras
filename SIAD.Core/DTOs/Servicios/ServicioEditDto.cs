using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Servicios;

public sealed class ServicioEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El codigo es obligatorio.")]
    [StringLength(50, ErrorMessage = "El codigo no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion corta es obligatoria.")]
    [StringLength(100, ErrorMessage = "La descripcion corta no puede superar los 100 caracteres.")]
    public string DescripcionCorta { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La descripcion larga no puede superar los 300 caracteres.")]
    public string? DescripcionLarga { get; set; }

    public bool Activo { get; set; } = true;

    public bool FacturableApp { get; set; }

    [Range(0, 9999, ErrorMessage = "El orden debe estar entre 0 y 9999.")]
    public int AppOrden { get; set; }

    [StringLength(20, ErrorMessage = "El grupo no puede superar los 20 caracteres.")]
    public string? AppGrupo { get; set; }

    public long? CuentaContableId { get; set; }
}
