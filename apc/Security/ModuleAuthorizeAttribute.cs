using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Routing;
using SIAD.Core.Constants;

namespace apc.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ModuleAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private static readonly object AuthorizationEvaluatedKey = new();
    private readonly string _module;
    private readonly string? _resource;
    private readonly PermissionAction? _explicitAction;

    public ModuleAuthorizeAttribute(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            throw new ArgumentException("El modulo es obligatorio.", nameof(module));
        }

        _module = module;
        _resource = null;
        _explicitAction = null;
    }

    public ModuleAuthorizeAttribute(string module, PermissionAction action)
        : this(module)
    {
        _explicitAction = action;
    }

    public ModuleAuthorizeAttribute(string module, string resource)
        : this(module)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("El recurso es obligatorio.", nameof(resource));
        }

        _resource = resource;
    }

    public ModuleAuthorizeAttribute(string module, string resource, PermissionAction action)
        : this(module, resource)
    {
        _explicitAction = action;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Filters.OfType<IAllowAnonymousFilter>().Any())
        {
            return Task.CompletedTask;
        }

        if (context.HttpContext.Items.ContainsKey(AuthorizationEvaluatedKey))
        {
            return Task.CompletedTask;
        }

        context.HttpContext.Items[AuthorizationEvaluatedKey] = true;

        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        if (user.IsInRole(RoleNames.SuperAdministrador))
        {
            return Task.CompletedTask;
        }

        var filtersToEvaluate = ResolveFilters(context);
        foreach (var filter in filtersToEvaluate)
        {
            if (filter.IsAuthorized(user, context.HttpContext))
            {
                return Task.CompletedTask;
            }
        }

        context.Result = new ForbidResult();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<ModuleAuthorizeAttribute> ResolveFilters(AuthorizationFilterContext context)
    {
        var descriptors = context.ActionDescriptor?.FilterDescriptors;
        if (descriptors is null || descriptors.Count == 0)
        {
            return context.Filters.OfType<ModuleAuthorizeAttribute>().ToList();
        }

        var actionLevel = descriptors
            .Where(d => d.Filter is ModuleAuthorizeAttribute && d.Scope == FilterScope.Action)
            .Select(d => (ModuleAuthorizeAttribute)d.Filter)
            .ToList();

        if (actionLevel.Count > 0)
        {
            return actionLevel;
        }

        return descriptors
            .Where(d => d.Filter is ModuleAuthorizeAttribute && d.Scope == FilterScope.Controller)
            .Select(d => (ModuleAuthorizeAttribute)d.Filter)
            .ToList();
    }

    private bool IsAuthorized(ClaimsPrincipal user, HttpContext httpContext)
    {
        var action = _explicitAction ?? MapMethod(httpContext.Request.Method);
        var endpointResource = ResolveEndpointResource(httpContext);
        var permission = BuildPermission(action, endpointResource);

        return HasPermission(user, permission, action, endpointResource);
    }

    private static PermissionAction MapMethod(string method)
    {
        return method?.ToUpperInvariant() switch
        {
            "GET" => PermissionAction.View,
            "HEAD" => PermissionAction.View,
            "POST" => PermissionAction.Create,
            "PUT" => PermissionAction.Edit,
            "PATCH" => PermissionAction.Edit,
            "DELETE" => PermissionAction.Delete,
            _ => PermissionAction.View
        };
    }

    private string BuildPermission(PermissionAction action, string? endpointResource)
    {
        if (string.IsNullOrWhiteSpace(_resource))
        {
            return PermissionKeyBuilder.BuildModulePermission(_module, action);
        }

        var resource = endpointResource ?? _resource;
        return PermissionKeyBuilder.BuildPermission(_module, resource, action);
    }

    private bool HasPermission(ClaimsPrincipal user, string permission, PermissionAction action, string? endpointResource)
    {
        if (user.HasClaim(PermissionClaimTypes.Permission, permission))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(endpointResource))
        {
            var baseResource = PermissionKeyBuilder.TryGetBaseResource(endpointResource);
            if (!string.IsNullOrWhiteSpace(baseResource))
            {
                var basePermission = PermissionKeyBuilder.BuildPermission(_module, baseResource, action);
                if (user.HasClaim(PermissionClaimTypes.Permission, basePermission))
                {
                    return true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_resource))
        {
            var moduleAction = PermissionKeyBuilder.BuildModulePermission(_module, action);
            if (user.HasClaim(PermissionClaimTypes.Permission, moduleAction))
            {
                return true;
            }
        }

        if (action == PermissionAction.View)
        {
            var legacyPermission = $"module.{_module}";
            return user.HasClaim(PermissionClaimTypes.Permission, legacyPermission);
        }

        return false;
    }

    private string? ResolveEndpointResource(HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(_resource))
        {
            return null;
        }

        if (httpContext.GetEndpoint() is not RouteEndpoint routeEndpoint)
        {
            return null;
        }

        var pattern = routeEndpoint.RoutePattern.RawText;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        return PermissionKeyBuilder.BuildEndpointResource(_resource, pattern);
    }
}
