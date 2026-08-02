using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public record TipoContactoLookupDto(long Id, string Nombre);

public record TipoContactoListItemDto(long Id, string Nombre, string? Observaciones, bool Activo);

public record TipoContactoDetailDto(long Id, string Nombre, string? Observaciones, bool Activo);

public class TipoContactoUpsertDto
{
    [Required(ErrorMessage = "El nombre es requerido.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "Las observaciones no pueden superar 250 caracteres.")]
    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
}
