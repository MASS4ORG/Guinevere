namespace Guinevere;

/// <summary>One link of a <c>Breadcrumb</c> trail.</summary>
/// <param name="Label">The crumb's text.</param>
/// <param name="OnClick">Invoked when the crumb is clicked or keyboard-activated. A crumb with no
/// action is treated as the current page: drawn dimmed and not interactive.</param>
/// <param name="IsCurrent">Marks the crumb as the current page even if an action was supplied,
/// so it points at the real position instead of a fake link.</param>
public readonly record struct BreadcrumbItem(
    string Label,
    Action? OnClick = null,
    bool IsCurrent = false);