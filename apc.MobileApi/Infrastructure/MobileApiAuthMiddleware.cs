using System.Text.Json;
using SIAD.Services.MobileApi;

namespace apc.MobileApi.Infrastructure;

/// <summary>
/// Autenticación de la API móvil: token bearer (Authorization: Bearer &lt;token&gt;)
/// validado contra adm_lector_sesion. La sesión resuelve el tenant y la ruta —
/// nunca por parámetro del cliente (A6). Rutas abiertas: login y diagnóstico.
/// Swagger y estáticos (no /api) pasan sin auth.
/// </summary>
public sealed class MobileApiAuthMiddleware
{
    private readonly RequestDelegate _next;

    public MobileApiAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILectoresMobileService service, MobileApiRequestContext requestContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Sólo se protege el árbol /api; swagger/openapi y estáticos pasan.
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || EsRutaAbierta(path))
        {
            await _next(context);
            return;
        }

        var token = BearerToken.Parse(context.Request);
        var sesion = await service.ValidarSesionAsync(token, context.RequestAborted);
        if (sesion is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { codigo = "NO_AUTORIZADO", mensaje = "Token inválido o expirado." }),
                context.RequestAborted);
            return;
        }

        requestContext.Autenticar(sesion);
        await _next(context);
    }

    private static bool EsRutaAbierta(string path)
    {
        // Match exacto (no prefijo) para no abrir rutas como /api/diagnostico-xyz.
        var p = path.TrimEnd('/');
        return p.Equals("/api/lectores/login", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/api/diagnostico", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("/api/diagnostico/", StringComparison.OrdinalIgnoreCase);
    }
}
