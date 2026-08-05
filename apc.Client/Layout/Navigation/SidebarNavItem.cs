namespace apc.Client.Layout.Navigation;

public sealed class SidebarNavItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string IconCssClass { get; init; }
    public string? NavigateUrl { get; init; }
    public IReadOnlyList<string> MatchPrefixes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SidebarNavItem> Children { get; init; } = Array.Empty<SidebarNavItem>();
    public bool MatchExact { get; init; }

    /// <summary>
    /// Capacidad que debe estar disponible para mostrar el item (ver <see cref="SidebarCapabilities"/>).
    /// Null = siempre visible. Es distinto del permiso: aqui se filtra por configuracion de la
    /// empresa, no por rol.
    /// </summary>
    public string? RequiredCapability { get; init; }

    /// <summary>
    /// Reorganización 2026-08-05 (5 secciones): las opciones que antes vivían
    /// en la sección Parámetros (solo Super Administrador) ahora conviven en
    /// Configuración — este flag conserva esa restricción a nivel de opción.
    /// </summary>
    public bool SoloSuperAdmin { get; init; }

    public bool HasChildren => Children.Count > 0;
}
