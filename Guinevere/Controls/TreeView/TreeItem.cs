namespace Guinevere;

/// <summary>
/// One row of a <c>TreeView</c>, in the flattened order the tree is drawn. Callers emit the whole tree;
/// the control hides the rows under a collapsed parent using <see cref="Depth"/>.
/// </summary>
/// <param name="Id">Stable key for expansion and selection. Must be unique in the tree.</param>
/// <param name="Label">The text drawn for the row.</param>
/// <param name="Depth">Nesting level; zero for a root row.</param>
/// <param name="HasChildren">Whether the row gets an expander.</param>
/// <param name="Icon">Optional content drawn before the label, in a square of the theme's icon size.</param>
/// <param name="Tag">Whatever the caller needs handed back in a callback.</param>
/// <param name="Tint">Overrides the label color, for a row that reads differently — a folder, an inactive node.</param>
public readonly record struct TreeItem(
    string Id,
    string Label,
    int Depth,
    bool HasChildren = false,
    Action<Gui>? Icon = null,
    object? Tag = null,
    Color? Tint = null);
