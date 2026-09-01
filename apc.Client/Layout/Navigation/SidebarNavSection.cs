namespace apc.Client.Layout.Navigation;

public sealed class SidebarNavSection
{
    /// <summary>
    /// Clave estable de la sección. No es el rótulo: SidebarNavigation reconstruye estas
    /// secciones al filtrar por capacidades o al entrar en modo recuperación, así que la
    /// sección abierta no se puede identificar por referencia; y el rótulo cambia con
    /// cualquier retoque de redacción, lo que dejaría huérfano lo guardado en el navegador.
    /// </summary>
    public required string Id { get; init; }

    public required string Label { get; init; }
    public required IReadOnlyList<SidebarNavItem> Items { get; init; }
    public string? RequiredPolicy { get; init; }

    /// <summary>
    /// Rutas que pertenecen a la sección aunque no tengan entrada propia en el menú: las
    /// vistas de detalle, los informes que se abren desde un panel, las pantallas a las que
    /// sólo se llega desde otra. Sirven para que el acordeón sepa dónde está el usuario; a
    /// diferencia de los MatchPrefixes de un item, no encienden nada.
    /// </summary>
    public IReadOnlyList<string> MatchPrefixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Sólo las secciones con rótulo se pliegan; la de Inicio no tiene cabecera que pulsar
    /// y queda siempre visible.
    /// </summary>
    public bool EsPlegable => !string.IsNullOrEmpty(Label);
}
