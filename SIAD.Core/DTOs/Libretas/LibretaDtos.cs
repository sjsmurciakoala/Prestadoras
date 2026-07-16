using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Libretas;

/// <summary>Fila del catálogo global de libretas (libro del lector, sin ciclo).</summary>
public record LibretaDto(
    long Id,
    string Codigo,
    string? Descripcion,
    bool Activo);

/// <summary>Alta/edición de una libreta.</summary>
public sealed class LibretaUpsertDto
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código admite máximo 10 caracteres.")]
    [RegularExpression("^[0-9A-Za-z]+$", ErrorMessage = "El código admite solo letras y números (viaja dentro del indicativo separado por guiones).")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(100, ErrorMessage = "La descripción admite máximo 100 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
