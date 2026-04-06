using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Roles;

public sealed class UpdateRoleDto
{
    [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
    [StringLength(128, ErrorMessage = "El nombre del rol no puede superar 128 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = [];
}
