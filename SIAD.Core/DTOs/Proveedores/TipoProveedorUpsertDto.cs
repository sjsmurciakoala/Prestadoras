using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public class TipoProveedorUpsertDto
{
    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Observaciones { get; set; }
}
