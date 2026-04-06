namespace apc.Client.Layout.Navigation;

public sealed class SidebarNavSection
{
    public required string Label { get; init; }
    public required IReadOnlyList<SidebarNavItem> Items { get; init; }
    public string? RequiredPolicy { get; init; }
}
