namespace Guinevere;

/// <summary>One link of a <c>Breadcrumb</c> trail.</summary>
/// <param name="Label">The crumb's text.</param>
/// <param name="OnClick">Invoked when the crumb is clicked or keyboard-activated. A crumb with no
/// action is treated as the current page: drawn dimmed and not interactive.</param>
/// <param name="IsCurrent">Marks the crumb as the current page even if an action was supplied,
/// so it points at the real position instead of a fake link.</param>
/// <param name="Icon">An optional glyph drawn before the label. It goes through the same
/// main-font/icon-font fallback as <see cref="Gui.DrawText"/>, so icon-font glyphs and emoji both work.</param>
/// <param name="Children">An optional list of descendant crumbs. When present, the "›" to the left of
/// the crumb becomes clickable and opens a context menu listing them, which is how you reach
/// next-level items without visiting the crumb itself.</param>
public readonly record struct BreadcrumbItem(
    string Label,
    Action? OnClick = null,
    bool IsCurrent = false,
    string? Icon = null,
    IReadOnlyList<BreadcrumbItem>? Children = null);
