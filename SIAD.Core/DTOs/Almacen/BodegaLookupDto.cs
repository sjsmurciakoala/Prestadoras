namespace SIAD.Core.DTOs.Almacen;

public sealed class BodegaLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Display => $"{Codigo} — {Nombre}";
}
