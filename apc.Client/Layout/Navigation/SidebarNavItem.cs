namespace apc.Client.Layout.Navigation;

public sealed class SidebarNavItem
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string IconCssClass { get; init; }
    public string? NavigateUrl { get; init; }
    public IReadOnlyList<string> MatchPrefixes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SidebarNavItem> Children { get; init; } = Array.Empty<SidebarNavItem>();

    public bool HasChildren => Children.Count > 0;
}
