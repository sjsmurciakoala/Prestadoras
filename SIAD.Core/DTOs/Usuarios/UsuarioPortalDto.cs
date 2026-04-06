namespace SIAD.Core.DTOs.Usuarios;

public sealed class UsuarioPortalDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmado { get; set; }
    public bool Bloqueado { get; set; }
    public long? CompanyId { get; set; }
    public List<string> Roles { get; set; } = [];
}
