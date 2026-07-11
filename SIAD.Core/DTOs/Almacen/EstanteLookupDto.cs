namespace SIAD.Core.DTOs.Almacen;

public sealed class EstanteLookupDto
{
    public int Id { get; init; }
    public int EstanteriaId { get; init; }
    public string Codigo { get; init; } = string.Empty;

    // Código compuesto legible: "<bodega>-<estanteria>-<estante>". Lo arma el servicio.
    public string UbicacionCodigo { get; init; } = string.Empty;

    public string Display => UbicacionCodigo;
}
