using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public class ProveedorUpsertDto
{
    [StringLength(20)]
    public string Codigo { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un tipo de proveedor valido.")]
    public int CodTipoProveedor { get; set; }

    [Required]
    [StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(150)]
    public string? RazonSocial { get; set; }

    [StringLength(20)]
    public string? Rtn { get; set; }

    [Required]
    [StringLength(1000)]
    public string Direccion { get; set; } = string.Empty;

    [StringLength(150)]
    public string? NombreContacto { get; set; }

    [StringLength(20)]
    public string? Telefono { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [Required]
    [StringLength(20)]
    public string CuentaContable { get; set; } = string.Empty;

    public List<ProveedorCuentaBancariaDto> CuentasBancarias { get; set; } = new();

    public bool Activo { get; set; } = true;
}
