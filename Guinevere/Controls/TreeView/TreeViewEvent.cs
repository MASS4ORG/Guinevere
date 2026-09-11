namespace Guinevere;

/// <summary>A click on a tree row.</summary>
/// <param name="Item">The row that was clicked.</param>
/// <param name="Button">Which button.</param>
/// <param name="ClickCount">One for a single click, two for a double click.</param>
public readonly record struct TreeViewEvent(TreeItem Item, MouseButton Button, int ClickCount);
