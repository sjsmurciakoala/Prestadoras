namespace SIAD.Services.Almacen;

/// <summary>Helpers de normalización compartidos por los catálogos de clasificación.</summary>
internal static class ClasificacionNormalizer
{
    public static string Requerido(string value, int maxLength, string campo, bool mayus = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {campo} es obligatorio.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El {campo} supera {maxLength} caracteres.", nameof(value));
        }

        return mayus ? trimmed.ToUpperInvariant() : trimmed;
    }

    public static string? Opcional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    public static string Usuario(string? user) => string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();

    /// <summary>
    /// Término con el que buscar el número de un documento del módulo.
    /// <para>
    /// Los listados pintan el correlativo con relleno de ceros (<c>ToString("00000")</c>) y eso
    /// es lo que la gente teclea o copia de la pantalla; en base es un entero, cuyo
    /// <c>ToString()</c> nunca trae esos ceros. Sin normalizar, buscar «00052» no encuentra la
    /// orden 52 y el buscador parece roto justo con el dato que el sistema muestra.
    /// </para>
    /// <para>
    /// Un término de solo ceros («0», «000») se colapsa a <c>"0"</c>: dejarlo vacío haría que
    /// <c>Contains("")</c> devolviera TODAS las filas.
    /// </para>
    /// </summary>
    public static string NumeroBuscado(string termino)
    {
        var limpio = (termino ?? string.Empty).Trim().TrimStart('0');
        return limpio.Length == 0 ? "0" : limpio;
    }
}
