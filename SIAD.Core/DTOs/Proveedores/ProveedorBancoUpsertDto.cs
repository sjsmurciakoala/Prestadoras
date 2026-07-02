using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public class ProveedorBancoUpsertDto
{
    [Required]
    [StringLength(80)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
