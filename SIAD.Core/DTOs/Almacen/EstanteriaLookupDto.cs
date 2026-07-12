namespace SIAD.Core.DTOs.Almacen;

public sealed class EstanteriaLookupDto
{
    public int Id { get; init; }
    public int BodegaId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string? Nombre { get; init; }
    public string Display => string.IsNullOrWhiteSpace(Nombre) ? Codigo : $"{Codigo} — {Nombre}";
}
