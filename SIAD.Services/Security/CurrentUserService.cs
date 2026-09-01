using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SIAD.Core.Security;

namespace SIAD.Services.Security;

/// <summary>
/// Identidad del usuario de la sesión leída del <see cref="HttpContext"/>. Mismo patrón que
/// <c>CurrentCompanyService</c> resuelve para la empresa.
/// <para>
/// En procesos sin request (barridos, tareas de fondo) no hay usuario: devuelve cadena vacía y
/// ningún rol. El motor de aprobación trata eso como "no se pudo identificar al usuario que
/// firma" y se niega a firmar — que es exactamente lo que debe pasar.
/// </para>
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetUserName()
    {
        var nombre = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        return string.IsNullOrWhiteSpace(nombre)
            ? string.Empty
            // Normalizado en el borde: todo lo que compare usuarios aguas abajo (elegibilidad del
            // aprobador, regla de autoaprobación) lo hace sobre el mismo formato.
            : nombre.Trim().ToLowerInvariant();
    }

    public IReadOnlyCollection<string> GetRoles()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Array.Empty<string>();
        }

        // Sin LINQ (regla hodsoft-sin-linq): recorrido explícito de los claims de rol.
        var roles = new List<string>();
        foreach (var claim in user.Claims)
        {
            if (string.Equals(claim.Type, ClaimTypes.Role, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(claim.Value))
            {
                roles.Add(claim.Value.Trim());
            }
        }

        return roles;
    }
}
