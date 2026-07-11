namespace SIAD.Core.DTOs.Almacen;

/// <summary>Unidad activa para poblar dropdowns (form de artículos, selección de base).</summary>
public sealed class UnidadMedidaLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Abreviatura { get; init; }

    /// <summary>"CODIGO - Nombre" para mostrar en el combo.</summary>
    public string Display => string.IsNullOrWhiteSpace(Abreviatura)
        ? $"{Codigo} - {Nombre}"
        : $"{Codigo} - {Nombre} ({Abreviatura})";
}
