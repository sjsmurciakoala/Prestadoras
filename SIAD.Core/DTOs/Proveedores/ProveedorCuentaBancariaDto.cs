using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public sealed class ProveedorCuentaBancariaDto
{
    public long? ProveedorCuentaBancariaId { get; set; }

    [StringLength(80, ErrorMessage = "El banco no puede superar 80 caracteres.")]
    public string? Banco { get; set; }

    [StringLength(50, ErrorMessage = "La cuenta bancaria no puede superar 50 caracteres.")]
    public string? CuentaBancaria { get; set; }

    public int Orden { get; set; }
}
