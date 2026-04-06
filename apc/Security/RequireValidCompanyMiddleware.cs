using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.Tenancy;

namespace apc.Security;

public sealed class RequireValidCompanyMiddleware
{
    private static readonly PathString[] StaticPrefixes =
    {
        new("/_blazor"),
        new("/_framework"),
        new("/_content"),
        new("/css"),
        new("/js"),
        new("/images"),
        new("/lib")
    };

    private readonly RequestDelegate next;

    public RequireValidCompanyMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantCompanyService tenantCompanyService)
    {
        if (ShouldSkip(context))
        {
            await next(context);
            return;
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (IsBootstrapPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var companyId = GetCompanyId(user);
        var ct = context.RequestAborted;

        bool hasValidCompany;
        bool hasCompanies;
        try
        {
            hasValidCompany = companyId > 0 &&
                              await tenantCompanyService.ExisteEmpresaAsync(companyId, ct);
            if (hasValidCompany)
            {
                await next(context);
                return;
            }

            hasCompanies = await tenantCompanyService.HayEmpresasAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El cliente se desconectó antes de que terminara la consulta.
            // No hay nada que hacer: la respuesta ya no tiene destinatario.
            return;
        }

        if (IsApiRequest(context.Request.Path))
        {
            await WriteApiResponseAsync(context, companyId, hasCompanies);
            return;
        }

        context.Response.Redirect(hasCompanies ? "/contabilidad/empresas" : "/contabilidad/empresas/nueva");
    }

    private static bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path;
        if (HttpMethods.IsOptions(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            return true;
        }

        if (path.HasValue && Path.HasExtension(path.Value))
        {
            return true;
        }

        return StaticPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBootstrapPath(PathString path)
    {
        return path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase) ||
               IsCompanyRecoveryPage(path) ||
               path.StartsWithSegments("/api/contabilidad/empresas", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/tenant", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/admin/seed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompanyRecoveryPage(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Equals("/contabilidad/empresas", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("/contabilidad/empresas/nueva", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("/contabilidad/empresas/editar/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiRequest(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }

    private static long GetCompanyId(ClaimsPrincipal user)
    {
        var claimValue = user.FindFirst(TenantClaimTypes.CompanyId)?.Value;
        return long.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var companyId) && companyId > 0
            ? companyId
            : 0;
    }

    private static async Task WriteApiResponseAsync(HttpContext context, long companyId, bool hasCompanies)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";

        var detail = companyId <= 0
            ? "El usuario autenticado no tiene una empresa activa asignada."
            : hasCompanies
                ? $"La empresa activa {companyId} no existe o ya no esta disponible."
                : "No existe ninguna empresa registrada. Cree la primera empresa antes de continuar.";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Empresa activa invalida",
            Detail = detail
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
