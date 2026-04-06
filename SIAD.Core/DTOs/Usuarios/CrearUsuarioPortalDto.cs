using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Usuarios;

public sealed class CrearUsuarioPortalDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public long? CompanyId { get; set; }

    public List<string> Roles { get; set; } = [];
}
