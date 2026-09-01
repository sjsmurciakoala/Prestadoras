namespace SIAD.Core.DTOs.Usuarios;

/// <summary>
/// Resultado de un restablecimiento de contraseña. La contraseña viaja en claro una única vez:
/// no se guarda en ningún lado y no se puede volver a consultar.
/// </summary>
public sealed class RestablecerPasswordResultadoDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>True si la contraseña la generó el servidor en vez de fijarla el administrador.</summary>
    public bool Generada { get; set; }
}
