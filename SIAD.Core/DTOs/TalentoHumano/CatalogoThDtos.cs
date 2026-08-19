using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.TalentoHumano;

/// <summary>Fila de un catálogo simple de Talento Humano (cargo o departamento).</summary>
public sealed class CatalogoThListItemDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public bool Activo { get; init; }
    /// <summary>Cuántos empleados lo usan (para avisar antes de desactivar).</summary>
    public int Empleados { get; init; }
}

public sealed class CatalogoThEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}

public sealed class CatalogoThLookupDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
}

public sealed class CatalogoThFilterDto
{
    public string? Search { get; set; }
    public bool? Activo { get; set; }
}
