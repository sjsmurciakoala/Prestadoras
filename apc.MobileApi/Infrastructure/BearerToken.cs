namespace apc.MobileApi.Infrastructure;

/// <summary>Extracción del token bearer del header Authorization (una sola definición).</summary>
public static class BearerToken
{
    private const string Prefijo = "Bearer ";

    public static string? Parse(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        return header.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase)
            ? header[Prefijo.Length..].Trim()
            : header.Trim();
    }
}
