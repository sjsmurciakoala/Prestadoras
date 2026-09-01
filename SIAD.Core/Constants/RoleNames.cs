namespace SIAD.Core.Constants;

/// <summary>
/// Super Administrador es el único rol con significado en el código: actúa como bypass global
/// de toda comprobación de permiso (ver <c>PermissionNames.Policies</c>).
///
/// Los demás roles (Ventas, Contabilidad, Bancos, Cobranzas…) siguen existiendo en la base,
/// pero son solo <b>contenedores de permisos</b>: se administran desde Configuración →
/// Roles y permisos y el código nunca pregunta por su nombre. Las constantes que los nombraban
/// se retiraron el 2026-09-01 al unificar la autorización, para que no vuelva a aparecer un
/// segundo camino basado en roles.
/// </summary>
public static class RoleNames
{
    public const string SuperAdministrador = "Super Administrador";
}
