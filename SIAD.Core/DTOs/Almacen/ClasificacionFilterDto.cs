namespace SIAD.Core.DTOs.Almacen;

/// <summary>Filtro común para catálogos de clasificación (tipo, línea, grupo).</summary>
public sealed class ClasificacionFilterDto
{
    public string? Search { get; set; }
    public bool? Activo { get; set; }
}
