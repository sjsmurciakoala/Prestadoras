namespace apc.Client.Layout.Navigation;

/// <summary>
/// Capacidades que condicionan la visibilidad de un item del menu segun la CONFIGURACION
/// de la empresa (no segun permisos). Las resuelve SidebarNavigation.
/// </summary>
public static class SidebarCapabilities
{
    /// <summary>
    /// Hay al menos un tipo de transaccion de salida que emite cheque, sin el cual el
    /// cheque manual no se puede registrar (ver ChequeEmisionState).
    /// </summary>
    public const string ChequeManual = "cheque-manual";
}
