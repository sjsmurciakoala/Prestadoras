using System;
using System.Collections.Generic;
using SIAD.Core.Security;

namespace SIAD.Tests.Infrastructure;

/// <summary>
/// Usuario de la sesión para los tests. Reemplaza a <c>CurrentUserService</c>, que lee del
/// <c>HttpContext</c> y no existe fuera de un request.
/// <para>
/// Es <b>mutable</b> a propósito: las pruebas del motor de aprobación necesitan cambiar de
/// persona entre una firma y la siguiente para simular la escalera.
/// </para>
/// </summary>
public sealed class TestCurrentUserService : ICurrentUserService
{
    private string _userName;
    private List<string> _roles;

    public TestCurrentUserService(string userName = "test@siad.local", params string[] roles)
    {
        _userName = userName ?? string.Empty;
        _roles = new List<string>(roles ?? Array.Empty<string>());
    }

    public void Establecer(string userName, params string[] roles)
    {
        _userName = userName ?? string.Empty;
        _roles = new List<string>(roles ?? Array.Empty<string>());
    }

    /// <summary>Normalizado igual que en producción: minúsculas y sin espacios al borde.</summary>
    public string GetUserName() => _userName.Trim().ToLowerInvariant();

    public IReadOnlyCollection<string> GetRoles() => _roles;
}
