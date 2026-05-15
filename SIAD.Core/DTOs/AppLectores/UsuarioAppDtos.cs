using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.AppLectores;

public record UsuarioAppFilterDto
{
    public string? Search { get; set; }
    public string? Ruta { get; set; }
    public bool? Activo { get; set; }
}

public record UsuarioAppListItemDto(
    int Id,
    string Usuario,
    string? Nombre,
    string? Ruta,
    int? CodCiclo,
    bool Activo);

public sealed class UsuarioAppEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [StringLength(25, ErrorMessage = "El usuario no puede superar los 25 caracteres.")]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [StringLength(30, ErrorMessage = "La contrasena no puede superar los 30 caracteres.")]
    public string Clave { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(50, ErrorMessage = "El nombre no puede superar los 50 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(6, ErrorMessage = "La ruta no puede superar los 6 caracteres.")]
    public string? Ruta { get; set; }

    public int? CodCiclo { get; set; }

    public bool Activo { get; set; } = true;
}
