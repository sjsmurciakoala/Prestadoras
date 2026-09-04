using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Usuarios;

/// <summary>
/// Solicitud de restablecimiento de contraseña hecha por un administrador.
/// Si <see cref="Password"/> viene vacío el servidor genera una contraseña temporal.
/// </summary>
public sealed class RestablecerPasswordDto
{
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres.")]
    public string? Password { get; set; }
}
