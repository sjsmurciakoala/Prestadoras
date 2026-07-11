namespace SIAD.Core.DTOs.Almacen;

public sealed class UbicacionFilterDto
{
    public string? Search { get; set; }
    public bool? Activo { get; set; }
    public int? BodegaId { get; set; }
    public int? EstanteriaId { get; set; }
}
