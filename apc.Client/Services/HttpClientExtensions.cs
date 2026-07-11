using System.Net.Http.Json;
using System.Text.Json;

namespace apc.Client.Services;

/// <summary>
/// Extensiones para HttpClient que manejan automáticamente errores comunes como sesiones expiradas.
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

        // Verificar si el usuario no tiene permisos (403 o redirección a AccessDenied)
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || EsRedireccionAccessDenied(response))
        {
            throw new UnauthorizedAccessException("No tiene permisos para realizar esta acción.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var mensaje = await ObtenerMensajeErrorAsync(response, cancellationToken);
            throw new HttpRequestException(mensaje ?? "Error en la solicitud HTTP.");
        }

        var contenido = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(contenido))
        {
            return default;
        }

        if (EsHtmlResponse(response, contenido))
        {
            if (EsRedireccionLogin(response) || EsPaginaLogin(contenido))
            {
                throw new UnauthorizedAccessException("Su sesión ha expirado. Por favor, inicie sesión nuevamente.");
            }

            throw new HttpRequestException("El servidor devolvio HTML en lugar de JSON. Revise la autenticacion o el endpoint solicitado.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(contenido, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new JsonException($"No se pudo interpretar la respuesta JSON. {ex.Message}", ex);
        }
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

        if (EsHtmlResponse(response, contenido))
        {
            if (EsRedireccionLogin(response) || EsPaginaLogin(contenido))
            {
                return "Su sesion ha expirado. Por favor, inicie sesion nuevamente.";
            }

            var requestId = ExtraerRequestId(contenido);
            var requestIdText = string.IsNullOrWhiteSpace(requestId)
                ? string.Empty
                : $" Request ID: {requestId}.";

            return $"El servidor devolvio una pagina de error al procesar la solicitud ({(int)response.StatusCode} {response.ReasonPhrase}).{requestIdText} Revise el log del servidor para ver el detalle.";
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

    private static bool EsHtmlResponse(HttpResponseMessage response, string contenido)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trimmed = contenido.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<");
    }

    private static bool EsPaginaLogin(string contenido)
    {
        return contenido.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase)
               || contenido.Contains("name=\"__RequestVerificationToken\"", StringComparison.OrdinalIgnoreCase)
               || contenido.Contains("<form", StringComparison.OrdinalIgnoreCase)
                  && contenido.Contains("login", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtraerRequestId(string contenido)
    {
        const string marker = "<strong>Request ID:</strong>";
        var markerIndex = contenido.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var codeStart = contenido.IndexOf("<code>", markerIndex, StringComparison.OrdinalIgnoreCase);
        if (codeStart < 0)
        {
            return null;
        }

        codeStart += "<code>".Length;
        var codeEnd = contenido.IndexOf("</code>", codeStart, StringComparison.OrdinalIgnoreCase);
        if (codeEnd <= codeStart)
        {
            return null;
        }

        return contenido[codeStart..codeEnd].Trim();
    }

    private static bool EsRedireccionLogin(HttpResponseMessage response)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.Redirect or System.Net.HttpStatusCode.Moved or System.Net.HttpStatusCode.RedirectKeepVerb)
        {
            var location = response.Headers.Location;
            if (location is not null && location.ToString().Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null && finalUri.AbsolutePath.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool EsRedireccionAccessDenied(HttpResponseMessage response)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.Redirect or System.Net.HttpStatusCode.Moved or System.Net.HttpStatusCode.RedirectKeepVerb)
        {
            var location = response.Headers.Location;
            if (location is not null && location.ToString().Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null && finalUri.AbsolutePath.Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

