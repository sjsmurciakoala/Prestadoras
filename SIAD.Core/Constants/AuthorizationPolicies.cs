namespace SIAD.Core.Constants;

/// <summary>
/// Unica policy que no se resuelve por permiso. Super Administrador es el bypass global del
/// sistema y por eso sigue siendo un rol: si se expresara como permiso, perderlo dejaria al
/// portal sin nadie capaz de reasignarlo.
///
/// Todo lo demas se autoriza con el claim 'permission' via <see cref="PermissionNames.Policies"/>.
/// Las antiguas policies por rol (CanContabilidad, CanBancos, CanCompras, CanVentas,
/// Facturacion, CanConfiguracion, CanPresupuestoAprobacion) se retiraron el 2026-09-01.
/// </summary>
public static class AuthorizationPolicies
{
    public const string SuperAdmin = "CanSuperAdmin";
}
