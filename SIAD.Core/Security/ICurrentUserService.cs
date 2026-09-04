namespace SIAD.Core.Security;

/// <summary>
/// Identidad del usuario de la sesión, para los servicios de dominio.
/// <para>
/// <b>Por qué existe:</b> el motor de aprobación necesita saber quién firma y con qué roles, y
/// <c>SIAD.Services</c> no puede —ni debe— conocer <c>HttpContext</c>. Es el mismo patrón que
/// <see cref="SIAD.Core.Tenancy.ICurrentCompanyService"/> resuelve para la empresa.
/// </para>
/// <para>
/// La identidad vive en ASP.NET Identity (schema <c>identity</c>, otro DbContext), no en el
/// modelo funcional: por eso el usuario viaja como texto, igual que en
/// <c>usuariocreacion</c> / <c>aprobado_por</c> de los documentos.
/// </para>
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Nombre de usuario de la sesión (<c>User.Identity.Name</c>, en la práctica el email),
    /// <b>normalizado a minúsculas</b>. Cadena vacía si no hay usuario autenticado.
    /// <para>
    /// La normalización no es cosmética: es lo que hace comparables la elegibilidad del
    /// aprobador y la regla "nadie aprueba su propia orden".
    /// </para>
    /// </summary>
    string GetUserName();

    /// <summary>
    /// Roles del usuario de la sesión, tal como los declara Identity (con sus mayúsculas).
    /// Vacío si no hay usuario autenticado.
    /// </summary>
    IReadOnlyCollection<string> GetRoles();
}
