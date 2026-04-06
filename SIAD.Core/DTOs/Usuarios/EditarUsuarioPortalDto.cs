using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Usuarios;

public sealed class EditarUsuarioPortalDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "La empresa es obligatoria.")]
    public long? CompanyId { get; set; }

    public List<string> Roles { get; set; } = [];

    public bool Bloqueado { get; set; }
}
