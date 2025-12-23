using System.Net.Http.Json;
using System.Text.Json;

namespace apc.Client.Services;

/// <summary>
/// Extensiones para HttpClient que manejan automáticamente errores comunes como sesiones expiradas.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Lee una respuesta JSON con manejo especial para 401 (Unauthorized).
    /// </summary>
    public static async Task<T?> ReadFromJsonAsyncWithAuthCheck<T>(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        // Verificar si la sesión ha expirado (401 o redirección al login)
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || EsRedireccionLogin(response))
        {
            throw new UnauthorizedAccessException("Su sesión ha expirado. Por favor, inicie sesión nuevamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await ObtenerMensajeErrorAsync(response, cancellationToken);
            throw new HttpRequestException(mensaje ?? "Error en la solicitud HTTP.");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// GET con manejo automático de autenticación expirada.
    /// </summary>
    public static async Task<T?> GetFromJsonAsyncWithAuthCheck<T>(
        this HttpClient httpClient,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await response.ReadFromJsonAsyncWithAuthCheck<T>(cancellationToken);
    }

    /// <summary>
    /// POST con manejo automático de autenticación expirada.
    /// </summary>
    public static async Task<HttpResponseMessage> PostAsJsonAsyncWithAuthCheck<T>(
        this HttpClient httpClient,
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(requestUri, value, cancellationToken);
        
        // Solo verificar si es 401 o redirección al login, los demás estados se manejan en el llamador
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || EsRedireccionLogin(response))
        {
            throw new UnauthorizedAccessException("Su sesión ha expirado. Por favor, inicie sesión nuevamente.");
        }

        return response;
    }

    /// <summary>
    /// PUT con manejo automático de autenticación expirada.
    /// </summary>
    public static async Task<HttpResponseMessage> PutAsJsonAsyncWithAuthCheck<T>(
        this HttpClient httpClient,
        string requestUri,
        T value,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(requestUri, value, cancellationToken);
        
        // Solo verificar si es 401 o redirección al login, los demás estados se manejan en el llamador
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || EsRedireccionLogin(response))
        {
            throw new UnauthorizedAccessException("Su sesión ha expirado. Por favor, inicie sesión nuevamente.");
        }

        return response;
    }

    /// <summary>
    /// Obtiene un mensaje de error de una respuesta HTTP, intentando parsear JSON o devolviendo el contenido como texto.
    /// </summary>
    public static async Task<string?> ObtenerMensajeErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var contenido = await response.Content.ReadAsStringAsync(cancellationToken);
        
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return $"Error {response.StatusCode}: {response.ReasonPhrase}";
        }

        // Intentar parsear como JSON
        try
        {
            using var document = JsonDocument.Parse(contenido);
            var root = document.RootElement;

            if (root.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }

            if (root.TryGetProperty("message", out var message))
            {
                return message.GetString();
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }
        }
        catch (JsonException)
        {
            // Si no es JSON válido, devolver el contenido como está
            return contenido;
        }

        return contenido;
    }

    private static bool EsRedireccionLogin(HttpResponseMessage response)
    {
        // Detectar redirecciones explícitas a /Account/Login (Location header)
        if (response.StatusCode is System.Net.HttpStatusCode.Redirect or System.Net.HttpStatusCode.Moved or System.Net.HttpStatusCode.RedirectKeepVerb)
        {
            var location = response.Headers.Location;
            if (location is not null && location.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Detectar redirección consumida (HttpClient siguió el 302 y terminó en /Account/Login)
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null && finalUri.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Detectar contenido HTML (normalmente la página de login) cuando esperamos JSON
        if (response.Content?.Headers?.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return false;
    }
}
