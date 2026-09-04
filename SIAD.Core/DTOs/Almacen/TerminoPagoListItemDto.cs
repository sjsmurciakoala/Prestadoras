namespace SIAD.Core.DTOs.Almacen;

public sealed class TerminoPagoListItemDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public int Dias { get; init; }
    public bool EsDefault { get; init; }
    public bool Activo { get; init; }
}
