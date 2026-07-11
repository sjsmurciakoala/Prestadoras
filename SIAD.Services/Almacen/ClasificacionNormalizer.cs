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
}
