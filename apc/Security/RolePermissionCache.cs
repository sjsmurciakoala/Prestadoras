using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using SIAD.Core.Constants;

namespace apc.Security;

/// <summary>
/// Permisos de cada rol, cacheados en memoria.
///
/// Los permisos NO viajan dentro de la cookie de sesión: con un catálogo de ~140 permisos y
/// usuarios con varios roles, la cookie llegaba a 33 KB y el servidor rechazaba la petición con
/// HTTP 431 (el límite de Kestrel son 32 KB). En su lugar se resuelven aquí en cada petición,
/// desde esta caché, y se inyectan al principal en <see cref="PermissionsClaimsTransformation"/>.
/// </summary>
public sealed class RolePermissionCache
{
    private const string PrefijoClave = "siad:permisos-rol:";
    private static readonly TimeSpan Duracion = TimeSpan.FromMinutes(10);

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IMemoryCache _cache;

    public RolePermissionCache(RoleManager<IdentityRole> roleManager, IMemoryCache cache)
    {
        _roleManager = roleManager;
        _cache = cache;
    }

    public async Task<IReadOnlyList<string>> ObtenerPermisosAsync(string rol)
    {
        if (string.IsNullOrWhiteSpace(rol))
        {
            return [];
        }

        var clave = PrefijoClave + rol;
        if (_cache.TryGetValue(clave, out IReadOnlyList<string>? permisos) && permisos is not null)
        {
            return permisos;
        }

        var entidad = await _roleManager.FindByNameAsync(rol);
        if (entidad is null)
        {
            return [];
        }

        var claims = await _roleManager.GetClaimsAsync(entidad);
        permisos = claims
            .Where(c => c.Type == PermissionClaimTypes.Permission)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cache.Set(clave, permisos, Duracion);
        return permisos;
    }

    /// <summary>
    /// Invalida un rol tras editar sus permisos, para que el cambio se note sin esperar
    /// a que expire la caché. La llama <c>RolesPortalController</c> al guardar.
    /// </summary>
    public void Invalidar(string rol)
    {
        if (!string.IsNullOrWhiteSpace(rol))
        {
            _cache.Remove(PrefijoClave + rol);
        }
    }
}
