namespace Guinevere;

/// <summary>One link of a <c>Breadcrumb</c> trail.</summary>
/// <param name="Content">What the crumb shows: text, an icon, or <c>Content.Row(icon, text)</c> for both.
/// The crumb fits it through layout.</param>
/// <param name="OnClick">Invoked when the crumb is clicked or keyboard-activated. A crumb with no
/// action is treated as the current page: drawn dimmed and not interactive.</param>
/// <param name="IsCurrent">Marks the crumb as the current page even if an action was supplied,
/// so it points at the real position instead of a fake link.</param>
/// <param name="Children">An optional list of descendant crumbs. When present, the "›" to the left of
/// the crumb becomes clickable and opens a context menu listing them, which is how you reach
/// next-level items without visiting the crumb itself. Their menu entries show the content's label.</param>
public readonly record struct BreadcrumbItem(
    Content Content,
    Action? OnClick = null,
    bool IsCurrent = false,
    IReadOnlyList<BreadcrumbItem>? Children = null);
