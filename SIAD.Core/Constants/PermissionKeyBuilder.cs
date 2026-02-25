using System.Text.RegularExpressions;

namespace SIAD.Core.Constants;

public static class PermissionKeyBuilder
{
    private static readonly Regex RouteParamRegex = new(@"\{([^}:]+)(:[^}]+)?\}", RegexOptions.Compiled);
    private static readonly Regex NonAlphaNumRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static string BuildPermission(string module, string resource, PermissionAction action)
    {
        return $"module.{module}.{resource}.{GetActionSegment(action)}";
    }

    public static string BuildModulePermission(string module, PermissionAction action)
    {
        return $"module.{module}.{GetActionSegment(action)}";
    }

    public static string BuildEndpointResource(string baseResource, string routePattern)
    {
        var normalized = NormalizeRoutePattern(routePattern);
        return $"{baseResource}__{normalized}";
    }

    public static string NormalizeRoutePattern(string routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
        {
            return "root";
        }

        var route = routePattern.Trim();
        if (route.StartsWith("/", StringComparison.Ordinal))
        {
            route = route[1..];
        }

        route = RouteParamRegex.Replace(route, m => m.Groups[1].Value);

        if (route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            route = route[4..];
        }

        route = route.ToLowerInvariant();
        route = NonAlphaNumRegex.Replace(route, "_");
        route = route.Trim('_');

        return string.IsNullOrEmpty(route) ? "root" : route;
    }

    public static string? TryGetBaseResource(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        var idx = resource.IndexOf("__", StringComparison.Ordinal);
        if (idx <= 0)
        {
            return null;
        }

        return resource[..idx];
    }

    public static string GetActionSegment(PermissionAction action)
    {
        return action switch
        {
            PermissionAction.View => "view",
            PermissionAction.Create => "create",
            PermissionAction.Edit => "edit",
            PermissionAction.Delete => "delete",
            _ => "view"
        };
    }
}
