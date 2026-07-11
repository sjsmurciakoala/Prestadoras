using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class LineaEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(2, ErrorMessage = "El código no puede superar los 2 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(25, ErrorMessage = "La cuenta contable no puede superar los 25 caracteres.")]
    public string? CuentaContable { get; set; }

    [StringLength(30)]
    public string? CuentaContableAnterior { get; set; }

    public bool Activo { get; set; } = true;
}
