using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.AppLectores;

/// <summary>
/// DTOs del mantenimiento de credenciales de lectores de la app móvil V3
/// (tabla <c>adm_lector_credencial</c>, autenticación bcrypt vía pgcrypto).
/// Reemplaza al viejo <c>usuarioapc</c> (app Java).
/// </summary>
public record LectorCredencialFilterDto
{
    public string? Search { get; set; }
    public string? Ruta { get; set; }
    public bool? Activo { get; set; }
}

public record LectorCredencialListItemDto(
    long CredencialId,
    string Codigo,
    string? Nombre,
    string? Ruta,
    int? CodCiclo,
    bool Activo);

public sealed class LectorCredencialEditDto
{
    public long? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no puede superar los 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    // Opcional en edición: vacío = no cambia la contraseña. Obligatorio al crear
    // (validado en el servicio).
    [StringLength(60, ErrorMessage = "La contraseña no puede superar los 60 caracteres.")]
    public string? Clave { get; set; }

    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string? Nombre { get; set; }

    [StringLength(20, ErrorMessage = "La ruta no puede superar los 20 caracteres.")]
    public string? Ruta { get; set; }

    public int? CodCiclo { get; set; }

    public bool Activo { get; set; } = true;
}
