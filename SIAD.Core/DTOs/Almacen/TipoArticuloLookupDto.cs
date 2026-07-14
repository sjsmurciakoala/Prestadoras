namespace SIAD.Core.DTOs.Almacen;

public sealed class TipoArticuloLookupDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Display => $"{Codigo} - {Nombre}";

    /// <summary>
    /// false = los artículos de este tipo no llevan existencias ni kardex (ej. Servicios).
    /// El formulario del artículo lo necesita para bloquear la pestaña Existencias al
    /// elegir el tipo.
    /// </summary>
    public bool ManejaInventario { get; init; } = true;
}
