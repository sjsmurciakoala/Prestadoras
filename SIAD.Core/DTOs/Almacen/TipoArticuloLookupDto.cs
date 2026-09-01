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

    /// <summary>
    /// Cuentas contables (código del plan) configuradas en el tipo. El artículo ya no
    /// lleva cuenta contable propia: hereda las de su tipo, y el formulario del artículo
    /// las muestra en solo lectura al elegir el tipo.
    /// </summary>
    public string? CuentaInventario { get; init; }
    public string? CuentaCostoVentas { get; init; }
    public string? CuentaVentas { get; init; }
    public string? CuentaAjustes { get; init; }
    public string? CuentaDevoluciones { get; init; }

    /// <summary>
    /// Tasa de ISV en compras configurada en el tipo (catálogo global). Null si el tipo no
    /// aplica ISV. El artículo hereda esta clasificación de su tipo.
    /// </summary>
    public int? ImpuestoTasaId { get; init; }

    /// <summary>Etiqueta lista para mostrar la tasa heredada: "ISV 15%", "Exento", o null si no tiene.</summary>
    public string? ImpuestoTasaDisplay { get; init; }
}
