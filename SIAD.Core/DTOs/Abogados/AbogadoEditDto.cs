using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Abogados;

public sealed class AbogadoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre corto es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre corto no puede superar los 100 caracteres.")]
    public string NombreCorto { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "El nombre largo no puede superar los 300 caracteres.")]
    public string? NombreLargo { get; set; }

    [StringLength(11, ErrorMessage = "El teléfono no puede superar los 11 caracteres.")]
    public string? Telefono { get; set; }

    [StringLength(100, ErrorMessage = "La cuenta contable no puede superar los 100 caracteres.")]
    public string? CodCuenta { get; set; }

    public bool Activo { get; set; } = true;
}
