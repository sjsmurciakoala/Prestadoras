using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.AppLectores;

public record ServicioRolWsFilterDto
{
    public string? Search { get; set; }
    public string? Rol { get; set; }
    public bool? Activo { get; set; }
}

public record ServicioRolWsListItemDto(
    string Rol,
    string Codigo,
    string? ServicioDescripcion,
    bool Activo,
    string? Descripcion);

public sealed class ServicioRolWsEditDto
{
    [Required(ErrorMessage = "El rol es obligatorio.")]
    [StringLength(50, ErrorMessage = "El rol no puede superar los 50 caracteres.")]
    public string Rol { get; set; } = string.Empty;

    [Required(ErrorMessage = "El codigo de servicio es obligatorio.")]
    [StringLength(50, ErrorMessage = "El codigo no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La descripcion no puede superar los 200 caracteres.")]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}
