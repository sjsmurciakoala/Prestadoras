namespace SIAD.Core.DTOs.Almacen;

public sealed class TipoArticuloListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }

    /// <summary>false = los artículos de este tipo no llevan existencias ni kardex (ej. Servicios).</summary>
    public bool ManejaInventario { get; init; } = true;

    /// <summary>
    /// Tratamiento del ISV en compras de este tipo, listo para mostrar: "ISV 15%", "Exento",
    /// o "—" si no tiene tasa asignada (no registra ISV).
    /// </summary>
    public string IsvDisplay { get; init; } = "—";

    public bool Activo { get; init; }
}
